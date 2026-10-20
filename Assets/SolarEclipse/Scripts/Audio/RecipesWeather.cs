using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Weather sounds: a seamless rain loop, thunder claps (near / far) and kill-streak announcer stingers.</summary>
    public static class RecipesWeather
    {
        public static void Define(Action<string, int, Func<FastRng, int, int, float[]>, int> def)
        {
            def("amb_rain", 1, (r, s, v) => Rain(r, s, false), 22050);
            def("amb_rain_heavy", 1, (r, s, v) => Rain(r, s, true), 22050);
            def("thunder_near", 2, (r, s, v) => Thunder(r, s, v, true), 22050);
            def("thunder_far", 3, (r, s, v) => Thunder(r, s, v, false), 22050);
            // Kill-streak stingers: tier 0 (double kill) .. tier 7 (godlike), each one bigger and higher.
            for (int t = 0; t < 8; t++)
            {
                int tier = t;
                def("ks_" + tier, 1, (r, s, v) => Stinger(r, s, tier), 32000);
            }
        }

        /// <summary>~6 s loop of filtered noise plus random droplet ticks, crossfaded head-to-tail.</summary>
        static float[] Rain(FastRng r, int rate, bool heavy)
        {
            const float len = 6.5f;
            int n = (int)(len * rate);
            var b = new float[n];
            var lp = Biquad.LowPass(rate, heavy ? 5200f : 4200f);
            var hp = Biquad.HighPass(rate, 300f);
            var rumble = Biquad.LowPass(rate, 180f);
            var dropBp = Biquad.BandPass(rate, 3200f, 3f);
            float dropEnv = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float wash = hp.Process(lp.Process(r.Bipolar())) * (0.55f + 0.12f * Sin(t * 0.31f) + 0.06f * Sin(t * 1.7f));
                if (r.Next() < (heavy ? 0.0045f : 0.0025f)) dropEnv = r.Range(0.3f, 1f);
                dropEnv *= 0.9965f;
                float drop = dropBp.Process(r.Bipolar()) * dropEnv;
                float low = rumble.Process(r.Bipolar()) * (heavy ? 1.6f : 0.9f);
                b[i] = wash + drop * 0.7f + low;
            }
            int xf = rate / 2;
            for (int i = 0; i < xf; i++)
            {
                float k = i / (float)xf;
                b[i] = b[i] * k + b[n - xf + i] * (1f - k);
            }
            var o = new float[n - xf];
            Array.Copy(b, 0, o, 0, o.Length);
            Normalize(o, heavy ? 0.6f : 0.45f);
            return o;
        }

        /// <summary>A crack (near only) followed by a long rolling low rumble.</summary>
        static float[] Thunder(FastRng r, int rate, int variant, bool near)
        {
            float len = near ? 5.5f : 6.5f;
            var b = Buf(len, rate);
            var lpA = Biquad.LowPass(rate, near ? 900f : 380f);
            var lpB = Biquad.LowPass(rate, near ? 300f : 160f);
            var crackHp = Biquad.HighPass(rate, 900f);
            float roll = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                // Several overlapping swells give the "rolling" shape.
                float env = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float st = (near ? 0.05f : 0.2f) + k * (0.55f + 0.2f * variant) + 0.1f * k * k;
                    float a = 1f / (1 + k * 0.6f);
                    env += a * Env.AD(t - st, 0.06f + k * 0.04f, 0.5f + k * 0.35f);
                }
                roll = roll * 0.995f + r.Bipolar() * 0.005f;
                float body = lpA.Process(r.Bipolar()) * 0.6f + lpB.Process(r.Bipolar()) * 1.6f + roll * 6f;
                float s = body * env;
                if (near) s += crackHp.Process(r.Bipolar()) * Env.AD(t, 0.002f, 0.09f) * 1.2f + lpA.Process(r.Bipolar()) * Env.AD(t - 0.08f, 0.01f, 0.35f) * 1.4f;
                b[i] = s;
            }
            Drive(b, near ? 1.8f : 1.3f);
            Fade(b, rate, 0.002f, 1.2f);
            Normalize(b, near ? 0.95f : 0.8f);
            return b;
        }

        /// <summary>Deep synth hit + a rising tonal sweep and a chord stab; higher tiers are longer, brighter and higher.</summary>
        static float[] Stinger(FastRng r, int rate, int tier)
        {
            float len = 1.1f + tier * 0.12f;
            var b = Buf(len, rate);
            float root = 55f * (float)Math.Pow(2, tier / 12f * 2f); // climbs two semitones per tier
            var noiseLp = Biquad.LowPass(rate, 1800f + tier * 400f);
            double ph1 = 0, ph2 = 0, ph3 = 0, ph4 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                // Sub kick: pitch drops from 4x to 1x of the root.
                float kf = root * (1f + 3f * Env.Exp(t, 0.04f));
                ph1 += kf / rate;
                float kick = (float)Math.Sin(ph1 * Math.PI * 2) * Env.AD(t, 0.002f, 0.35f + tier * 0.03f) * 1.1f;
                // Sweep: saw-ish rising from root*4 to root*16.
                float sf = root * 4f * (float)Math.Pow(4, Math.Min(1f, t / 0.35f));
                ph2 += sf / rate;
                float saw = (float)(2 * (ph2 - Math.Floor(ph2)) - 1);
                float sweep = saw * Env.AR(t, 0.02f, 0.25f, 0.2f) * 0.25f;
                // Chord stab (root, fifth, octave) after the sweep lands.
                ph3 += root * 8f / rate; ph4 += root * 12f / rate;
                float chord = ((float)Math.Sin(ph3 * Math.PI * 2) + (float)Math.Sin(ph4 * Math.PI * 2) * 0.8f + Sin(t * root * 16f) * 0.5f)
                              * Env.AD(t - 0.3f, 0.01f, 0.35f + tier * 0.06f) * 0.35f;
                float hit = noiseLp.Process(r.Bipolar()) * Env.AD(t, 0.001f, 0.08f) * 0.6f;
                b[i] = kick + sweep + chord + hit;
            }
            Drive(b, 1.5f + tier * 0.1f);
            var o = WithReverb(b, rate, 0.7f, 0.25f, 0.6f);
            Fade(o, rate, 0.001f, 0.25f);
            Normalize(o, 0.9f);
            return o;
        }
    }
}
