using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Interface blips, the SolarEclipse logo sequence and the menu music.</summary>
    public static class RecipesUi
    {
        public static float[] Blip(FastRng r, int rate, float f0, float f1, float seconds, float gain = 0.5f)
        {
            var b = Buf(seconds, rate);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float f = f0 + (f1 - f0) * (t / seconds);
                ph += f / rate;
                b[i] = (Sin(ph) + Sin(ph * 2) * 0.2f) * Env.AD(t, 0.002f, seconds * 0.3f);
            }
            Normalize(b, gain);
            return b;
        }

        public static float[] Notes(int rate, float[] freqs, float spacing, float decay, float gain)
        {
            var b = Buf(freqs.Length * spacing + decay * 4, rate);
            for (int k = 0; k < freqs.Length; k++)
            {
                var n = Buf(decay * 4, rate);
                float ph = 0;
                for (int i = 0; i < n.Length; i++)
                {
                    float t = i / (float)rate;
                    ph += freqs[k] / rate;
                    n[i] = (Sin(ph) + Sin(ph * 2) * 0.3f + Sin(ph * 3) * 0.1f) * Env.AD(t, 0.003f, decay);
                }
                Add(b, n, (int)(k * spacing * rate), 1f);
            }
            Normalize(b, gain);
            return b;
        }

        public static float[] Achievement(FastRng r, int rate)
        {
            var b = Notes(rate, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.09f, 0.25f, 0.6f);
            var chord = Notes(rate, new[] { 523.25f, 659.25f, 783.99f }, 0f, 0.6f, 0.5f);
            Add(b, chord, (int)(0.38f * rate), 0.6f);
            b = WithReverb(b, rate, 0.7f, 0.3f, 0.8f);
            Normalize(b, 0.6f);
            return b;
        }

        public static float[] Riser(FastRng r, int rate, float seconds)
        {
            var b = Buf(seconds, rate);
            var bp = new Biquad(rate);
            float ph1 = 0, ph2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float x = t / seconds;
                if ((i & 31) == 0) bp.SetBandPass(200 + 6000 * x * x, 1.5f);
                ph1 += (55 + 220 * x * x) / rate;
                ph2 += (55.4f + 221 * x * x) / rate;
                float saw = ((ph1 % 1f) + (ph2 % 1f)) - 1f;
                b[i] = (bp.Process(r.Bipolar()) * 0.8f + bp.Process(saw) * 0.5f) * (float)Math.Pow(x, 1.6f);
            }
            Normalize(b, 0.8f);
            return b;
        }

        public static float[] Impact(FastRng r, int rate)
        {
            var b = Buf(3.5f, rate);
            float ph = 0;
            var lp = Biquad.LowPass(rate, 900);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                ph += (40 + 80 * Env.Exp(t, 0.08f)) / rate;
                b[i] = Sin(ph) * Env.AD(t, 0.002f, 0.9f) * 1.3f + lp.Process(r.Bipolar()) * Env.AD(t, 0.001f, 0.25f) * 1.6f;
            }
            Drive(b, 2f);
            b = WithReverb(b, rate, 0.92f, 0.5f, 2.5f, 0.4f);
            Normalize(b, 0.98f);
            return b;
        }

        public static float[] Shimmer(FastRng r, int rate)
        {
            var b = Buf(3f, rate);
            float[] f = { 1567.98f, 2093f, 2637f, 3135.96f, 4186f };
            for (int k = 0; k < f.Length; k++)
            {
                float ph = 0, ph2 = 0, start = k * 0.12f;
                for (int i = (int)(start * rate); i < b.Length; i++)
                {
                    float t = i / (float)rate - start;
                    ph += f[k] / rate;
                    ph2 += f[k] * 1.003f / rate;
                    b[i] += (Sin(ph) + Sin(ph2)) * Env.AD(t, 0.01f, 0.8f) * 0.2f;
                }
            }
            b = WithReverb(b, rate, 0.9f, 0.5f, 1.5f);
            Normalize(b, 0.55f);
            return b;
        }

        /// <summary>Dark ambient menu loop: Am-F-Dm-E pads, heartbeat pulse and sparse bell plucks.</summary>
        public static float[] MenuMusic(FastRng r, int rate)
        {
            float bpm = 70, beat = 60f / bpm;
            float bar = beat * 4;
            int bars = 8;
            float len = bar * bars;
            var b = Buf(len, rate);
            float[][] chords =
            {
                new[] { 110f, 130.81f, 164.81f }, new[] { 87.31f, 110f, 130.81f },
                new[] { 73.42f, 87.31f, 110f }, new[] { 82.41f, 103.83f, 123.47f },
            };
            var lp = new Biquad(rate);
            var phases = new float[6];
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                int ci = (int)(t / (bar * 2)) % chords.Length;
                var chord = chords[ci];
                if ((i & 63) == 0) lp.SetLowPass(500 + 300 * Sin(t / len * 2f), 1.2f);
                float pad = 0;
                for (int k = 0; k < 3; k++)
                {
                    phases[k] += chord[k] * 2 / rate;
                    phases[k + 3] += chord[k] * 2 * 1.006f / rate;
                    pad += ((phases[k] % 1f) * 2 - 1) + ((phases[k + 3] % 1f) * 2 - 1);
                }
                float chordT = t % (bar * 2);
                float padEnv = Math.Min(1, chordT / 1.5f) * Math.Min(1, (bar * 2 - chordT) / 0.4f + 0.3f);
                float sample = lp.Process(pad * 0.12f) * padEnv;
                // heartbeat kick on beats 1 and 1-and
                float bt = t % beat;
                sample += Sin(bt * (45 + 60 * Env.Exp(bt, 0.03f))) * Env.AD(bt, 0.002f, 0.12f) * 0.5f;
                float bt2 = (t - 0.28f) % beat;
                if (t > 0.28f) sample += Sin(bt2 * 40) * Env.AD(bt2, 0.002f, 0.1f) * 0.3f;
                b[i] = sample;
            }
            // Bell plucks
            for (int k = 0; k < 10; k++)
            {
                float f = r.Next() < 0.5f ? 880f : r.Next() < 0.5f ? 1046.5f : 659.25f;
                var bell = Modal(r, rate, 2f, new[] { f, f * 2.76f, f * 5.4f }, new[] { 0.9f, 0.4f, 0.2f }, 0.002f, 0f);
                Add(b, bell, (int)(r.Range(0.5f, len - 2.5f) * rate), 0.12f);
            }
            b = WithReverb(b, rate, 0.85f, 0.35f, 0f);
            Fade(b, rate, 0.02f, 0.02f);
            Normalize(b, 0.6f);
            return b;
        }
    }
}
