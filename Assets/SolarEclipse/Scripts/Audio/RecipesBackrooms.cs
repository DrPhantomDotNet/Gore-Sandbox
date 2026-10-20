using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Backrooms ambience: the fluorescent mains buzz (seamless loop) and a ballast "tick-bzzt" for flickering panels.</summary>
    public static class RecipesBackrooms
    {
        public static void Define(Action<string, int, Func<FastRng, int, int, float[]>, int> def)
        {
            def("amb_backrooms", 1, (r, s, v) => Hum(r, s), 22050);
            def("backrooms_flicker", 3, (r, s, v) => FlickerBuzz(r, s, v), 22050);
        }

        /// <summary>
        /// ~7.5 s loop: 8 s are rendered and the last 0.5 s is crossfaded into the head, so the loop point continues
        /// exactly where the rendered signal would have gone next (no click, for tones and noise alike).
        /// </summary>
        static float[] Hum(FastRng r, int rate)
        {
            const float len = 8f;
            int n = (int)(len * rate);
            var b = new float[n];
            var buzzBp = Biquad.BandPass(rate, 2400f, 1.2f);
            var hissLp = Biquad.LowPass(rate, 5200f);
            var noise = new float[n];
            for (int i = 0; i < n; i++) noise[i] = r.Bipolar();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                // Core 120 Hz hum with harmonics; odd-ish upper partials give the "electric" edge.
                float hum = Sin(t * 120f) * 0.42f + Sin(t * 240f) * 0.28f + Sin(t * 360f) * 0.2f + Sin(t * 480f) * 0.08f
                          + Sin(t * 600f) * 0.05f + Sin(t * 840f) * 0.035f + Sin(t * 1080f) * 0.025f;
                // A slightly detuned second ballast beating against the first (slow 0.25 Hz beat).
                hum += Sin(t * 120.25f) * 0.18f + Sin(t * 240.5f) * 0.08f;
                // Gritty buzz: 120 Hz pulse train driving band-passed noise.
                float pulse = Sin(t * 120f);
                float grit = buzzBp.Process(noise[i]) * (pulse > 0.75f ? 1f : 0.25f);
                float hiss = hissLp.Process(noise[(i * 7) % n]) * 0.012f;
                float wobble = 0.86f + 0.1f * Sin(t * 0.125f) + 0.04f * Sin(t * 0.625f + 0.3f);
                b[i] = (hum * 0.75f + grit * 0.22f) * wobble + hiss;
            }
            // Crossfade the head into the tail so the filtered noise loops cleanly.
            int xf = rate / 2;
            for (int i = 0; i < xf; i++)
            {
                float k = i / (float)xf;
                int tail = n - xf + i;
                b[i] = b[i] * k + b[tail] * (1f - k);
            }
            var o = new float[n - xf];
            Array.Copy(b, 0, o, 0, o.Length);
            Normalize(o, 0.4f);
            return o;
        }

        /// <summary>A relay tick, a short harsh buzz and a fading hum: a fluorescent tube cutting out.</summary>
        static float[] FlickerBuzz(FastRng r, int rate, int variant)
        {
            float len = 0.35f + variant * 0.08f;
            var b = Buf(len, rate);
            var bp = Biquad.BandPass(rate, 1800f + variant * 400f, 2f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float tick = r.Bipolar() * (float)Math.Exp(-t * 900f) * 0.9f;
                float gate = (float)Math.Floor(t * (40f + variant * 13f)) % 3 == 1 ? 0.2f : 1f;
                float buzz = (bp.Process(r.Bipolar()) * 1.4f + Sin(t * 120f) * 0.5f + Sin(t * 240f) * 0.3f) * gate;
                float env = Math.Min(1f, t * 200f) * (float)Math.Exp(-t * (6f + variant));
                b[i] = tick + buzz * env;
            }
            Normalize(b, 0.7f);
            return b;
        }
    }
}
