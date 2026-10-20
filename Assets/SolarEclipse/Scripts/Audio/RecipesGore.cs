using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Flesh, bone and blood sounds.</summary>
    public static class RecipesGore
    {
        /// <summary>Tiny wet bubble pops scattered over a time window.</summary>
        static void Bubbles(float[] b, FastRng r, int rate, int count, float start, float spread, float fLow, float fHigh, float gain)
        {
            for (int k = 0; k < count; k++)
            {
                float f0 = r.Range(fLow, fHigh);
                var pop = Chirp(rate, 0.05f, f0, f0 * r.Range(1.6f, 2.6f), 0.01f, r.Range(0.006f, 0.018f));
                Add(b, pop, (int)((start + r.Next() * spread) * rate), gain * r.Range(0.4f, 1f));
            }
        }

        static float[] Squelch(FastRng r, int rate, float seconds, float f0, float f1)
        {
            var b = Buf(seconds, rate);
            var bp = new Biquad(rate);
            var am = new OnePole(rate, 60);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                if ((i & 15) == 0) bp.SetBandPass(f0 + (f1 - f0) * Math.Min(1, t / (seconds * 0.6f)), 3f);
                float wobble = 0.5f + Math.Abs(am.Process(r.Bipolar() * 8f));
                b[i] = bp.Process(r.Bipolar()) * wobble * Env.AD(t, 0.003f, seconds * 0.35f);
            }
            return b;
        }

        public static float[] FleshHit(FastRng r, int rate)
        {
            var b = Buf(0.45f, rate);
            Add(b, RecipesWeapons.Thud(r, rate, 0.5f, 0.3f), 0, 0.8f);
            Add(b, Squelch(r, rate, 0.25f, r.Range(500, 800), r.Range(1300, 2000)), (int)(0.004f * rate), 1.1f);
            Bubbles(b, r, rate, 6, 0.01f, 0.15f, 400, 900, 0.35f);
            Normalize(b, 0.9f);
            return b;
        }

        public static float[] Headshot(FastRng r, int rate)
        {
            var b = Buf(0.7f, rate);
            Add(b, BoneCrack(r, rate), 0, 0.9f);
            Add(b, Squelch(r, rate, 0.4f, 400, 1600), (int)(0.005f * rate), 1.2f);
            Bubbles(b, r, rate, 14, 0.02f, 0.3f, 350, 1000, 0.4f);
            Normalize(b, 0.95f);
            return b;
        }

        public static float[] BoneCrack(FastRng r, int rate)
        {
            var b = Buf(0.35f, rate);
            int cracks = r.RangeInt(2, 5);
            float t0 = 0;
            for (int k = 0; k < cracks; k++)
            {
                var c = NoiseBurst(rate, r, 0.03f, 1500, 9000, 0.0002f, r.Range(0.002f, 0.006f));
                Add(b, c, (int)(t0 * rate), r.Range(0.6f, 1f));
                t0 += r.Range(0.004f, 0.02f);
            }
            var creak = Buf(0.15f, rate);
            var bp = Biquad.BandPass(rate, r.Range(900, 1600), 6);
            for (int i = 0; i < creak.Length; i++) creak[i] = bp.Process(r.Bipolar()) * Env.AD(i / (float)rate, 0.005f, 0.04f);
            Add(b, creak, 0, 0.6f);
            Add(b, RecipesWeapons.Thud(r, rate, 0.4f, 0.2f), 0, 0.4f);
            Normalize(b, 0.9f);
            return b;
        }

        public static float[] Stab(FastRng r, int rate)
        {
            var b = Buf(0.5f, rate);
            Add(b, RecipesWeapons.Thud(r, rate, 0.3f, 0.2f), 0, 0.6f);
            Add(b, Squelch(r, rate, 0.35f, 700, 1300), 0, 1.2f);
            Bubbles(b, r, rate, 8, 0.05f, 0.25f, 300, 800, 0.4f);
            Normalize(b, 0.9f);
            return b;
        }

        public static float[] Slice(FastRng r, int rate)
        {
            var b = Buf(0.5f, rate);
            var hp = Biquad.HighPass(rate, 3000);
            for (int i = 0; i < (int)(0.15f * rate); i++) b[i] += hp.Process(r.Bipolar()) * Env.AD(i / (float)rate, 0.01f, 0.04f) * 0.7f;
            Add(b, Squelch(r, rate, 0.3f, 1200, 600), (int)(0.02f * rate), 1f);
            Bubbles(b, r, rate, 6, 0.08f, 0.2f, 400, 900, 0.3f);
            Normalize(b, 0.85f);
            return b;
        }

        public static float[] Gib(FastRng r, int rate)
        {
            var b = Buf(1.1f, rate);
            Add(b, RecipesWeapons.Thud(r, rate, 0.9f, 0.5f), 0, 1f);
            Add(b, Squelch(r, rate, 0.6f, 300, 1400), 0, 1.3f);
            Add(b, BoneCrack(r, rate), (int)(0.01f * rate), 0.6f);
            Bubbles(b, r, rate, 40, 0.02f, 0.8f, 250, 1100, 0.45f);
            Normalize(b, 0.95f);
            return b;
        }

        public static float[] Dismember(FastRng r, int rate)
        {
            var b = Buf(0.9f, rate);
            Add(b, BoneCrack(r, rate), 0, 0.9f);
            Add(b, Squelch(r, rate, 0.5f, 600, 1500), (int)(0.01f * rate), 1.1f);
            Add(b, Spray(r, rate, 0.6f), (int)(0.05f * rate), 0.5f);
            Bubbles(b, r, rate, 20, 0.05f, 0.6f, 300, 1000, 0.35f);
            Normalize(b, 0.95f);
            return b;
        }

        /// <summary>Hissing turbulent blood jet.</summary>
        public static float[] Spray(FastRng r, int rate, float seconds)
        {
            var b = Buf(seconds, rate);
            var bp = Biquad.BandPass(rate, r.Range(2200, 3500), 0.9f);
            var lp = Biquad.LowPass(rate, 700);
            var am = new OnePole(rate, 30);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float turb = 0.6f + Math.Abs(am.Process(r.Bipolar() * 5f));
                b[i] = (bp.Process(r.Bipolar()) + lp.Process(r.Bipolar()) * 0.5f) * turb * Env.AR(t, 0.01f, seconds * 0.6f, seconds * 0.35f);
            }
            Normalize(b, 0.6f);
            return b;
        }

        /// <summary>One arterial pulse: a surge of hissing blood.</summary>
        public static float[] ArterialPulse(FastRng r, int rate)
        {
            var b = Spray(r, rate, 0.45f);
            for (int i = 0; i < b.Length; i++) b[i] *= Env.AD(i / (float)rate, 0.03f, 0.15f) * 1.5f;
            Bubbles(b, r, rate, 4, 0.05f, 0.3f, 500, 1200, 0.2f);
            Normalize(b, 0.6f);
            return b;
        }

        public static float[] Drip(FastRng r, int rate)
        {
            float f = r.Range(500, 900);
            var b = Chirp(rate, 0.12f, f, f * r.Range(1.8f, 2.6f), 0.012f, 0.025f);
            var click = NoiseBurst(rate, r, 0.01f, 2000, 8000, 0.0002f, 0.001f);
            Add(b, click, 0, 0.2f);
            Normalize(b, 0.4f);
            return b;
        }

        public static float[] Splat(FastRng r, int rate)
        {
            var b = Buf(0.35f, rate);
            Add(b, NoiseBurst(rate, r, 0.2f, 300, 3000, 0.002f, 0.03f), 0, 0.8f);
            Bubbles(b, r, rate, 10, 0.0f, 0.2f, 500, 1300, 0.35f);
            Normalize(b, 0.6f);
            return b;
        }

        public static float[] Gurgle(FastRng r, int rate, float seconds)
        {
            var b = Buf(seconds, rate);
            var lp = Biquad.LowPass(rate, 500);
            for (int i = 0; i < b.Length; i++) b[i] = lp.Process(r.Bipolar()) * 0.3f * Env.AR(i / (float)rate, 0.05f, seconds * 0.6f, seconds * 0.3f);
            Bubbles(b, r, rate, (int)(seconds * 45), 0, seconds * 0.9f, 200, 700, 0.6f);
            Normalize(b, 0.6f);
            return b;
        }

        public static float[] Heartbeat(FastRng r, int rate)
        {
            var b = Buf(1f, rate);
            Add(b, Chirp(rate, 0.2f, 70, 45, 0.03f, 0.05f), 0, 1f);
            Add(b, Chirp(rate, 0.2f, 60, 40, 0.03f, 0.05f), (int)(0.28f * rate), 0.7f);
            LowPass(b, rate, 200);
            Normalize(b, 0.7f);
            return b;
        }
    }
}
