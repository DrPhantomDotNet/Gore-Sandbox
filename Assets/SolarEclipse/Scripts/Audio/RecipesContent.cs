using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Content Update sounds: weapons (flamer, laser, freeze ray, crossbow, nail gun, tranq, turret), machines and animals.</summary>
    public static class RecipesContent
    {
        const double Tau = Math.PI * 2;

        static float[] Noise(FastRng r, float len, int rate, float lo, float hi, float a, float d) => NoiseBurst(rate, r, len, lo, hi, a, d);

        public static float[] Flamer(FastRng r, int rate)
        {
            var b = Noise(r, 0.35f, rate, 150f, 2400f, 0.01f, 0.25f);
            var low = Noise(r, 0.35f, rate, 40f, 300f, 0.01f, 0.3f);
            for (int i = 0; i < b.Length; i++) b[i] = b[i] * 0.6f + low[i] * 0.8f;
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] Laser(FastRng r, int rate)
        {
            var b = Buf(0.18f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double f = 1800 - 900 * t / 0.18;
                b[i] = (float)((Math.Sin(Tau * f * t) * 0.5 + Math.Sin(Tau * f * 2.01 * t) * 0.25 + r.Bipolar() * 0.12) * Math.Exp(-t * 12));
            }
            Normalize(b, 0.6f);
            return b;
        }

        public static float[] Freeze(FastRng r, int rate)
        {
            var b = Noise(r, 0.3f, rate, 3000f, 9000f, 0.005f, 0.2f);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                b[i] += (float)(Math.Sin(Tau * (2400 + 600 * Math.Sin(t * 60)) * t) * 0.15 * Math.Exp(-t * 6));
            }
            Normalize(b, 0.55f);
            return b;
        }

        public static float[] Twang(FastRng r, int rate)
        {
            var b = Buf(0.45f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double f = 160 + 40 * Math.Exp(-t * 30);
                b[i] = (float)((Math.Sin(Tau * f * t) + 0.4 * Math.Sin(Tau * f * 3 * t)) * Math.Exp(-t * 9) + (t < 0.01 ? r.Bipolar() * 0.8 : 0));
            }
            Normalize(b, 0.75f);
            return b;
        }

        public static float[] Nail(FastRng r, int rate)
        {
            var b = Noise(r, 0.12f, rate, 800f, 6000f, 0.001f, 0.02f);
            var k = Chirp(rate, 0.12f, 300f, 80f, 0.02f, 0.03f);
            for (int i = 0; i < b.Length && i < k.Length; i++) b[i] = b[i] * 0.7f + k[i] * 0.6f;
            Normalize(b, 0.75f);
            return b;
        }

        public static float[] Puff(FastRng r, int rate)
        {
            var b = Noise(r, 0.2f, rate, 300f, 3000f, 0.003f, 0.05f);
            Normalize(b, 0.55f);
            return b;
        }

        public static float[] Thump(FastRng r, int rate, float pitch)
        {
            var b = Chirp(rate, 0.35f, 140f * pitch, 45f * pitch, 0.05f, 0.12f);
            var n = Noise(r, 0.35f, rate, 100f, 1500f, 0.002f, 0.04f);
            for (int i = 0; i < b.Length && i < n.Length; i++) b[i] = b[i] + n[i] * 0.4f;
            Normalize(b, 0.8f);
            return b;
        }

        /// <summary>Electric crackle (tesla coil, third rail).</summary>
        public static float[] Zap(FastRng r, int rate)
        {
            var b = Buf(0.3f, rate);
            float hold = 0;
            for (int i = 0; i < b.Length; i++)
            {
                if (r.Next() < 0.08f) hold = r.Bipolar();
                double t = i / (double)rate;
                b[i] = (float)(hold * Math.Exp(-t * 6) + Math.Sin(Tau * 120 * t) * 0.25 * Math.Exp(-t * 4));
            }
            Normalize(b, 0.75f);
            return b;
        }

        /// <summary>Machine grinding loop (wood chipper, shredder, grinder): 1 s.</summary>
        public static float[] Grind(FastRng r, int rate)
        {
            var b = Buf(1f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double s = Math.Sign(Math.Sin(Tau * 47 * t)) * 0.3 + Math.Sin(Tau * 94 * t) * 0.3 + r.Bipolar() * 0.35;
                b[i] = (float)(s * (0.8 + 0.2 * Math.Sin(Tau * 6 * t)));
            }
            LowPass(b, rate, 2500f);
            Normalize(b, 0.6f);
            return b;
        }

        /// <summary>Train horn.</summary>
        public static float[] TrainHorn(FastRng r, int rate)
        {
            var b = Buf(1.6f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double env = Math.Min(1, t / 0.08) * Math.Min(1, (1.6 - t) / 0.2);
                b[i] = (float)((Saw(t * 277) + Saw(t * 349) + Saw(t * 415)) * 0.25 * env);
            }
            LowPass(b, rate, 2200f);
            Normalize(b, 0.85f);
            return b;
        }

        static double Saw(double ph) => 2 * (ph - Math.Floor(ph)) - 1;

        // ------------------------------------------------------------------ animals

        public static float[] Bark(FastRng r, int rate)
        {
            var b = Buf(0.28f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double f = 520 - 300 * t / 0.28;
                double env = Math.Min(1, t / 0.01) * Math.Exp(-t * 14);
                b[i] = (float)((Math.Sin(Tau * f * t) * 0.6 + Saw(t * f * 0.5) * 0.3 + r.Bipolar() * 0.25) * env);
            }
            LowPass(b, rate, 3000f);
            Normalize(b, 0.75f);
            return b;
        }

        public static float[] Oink(FastRng r, int rate)
        {
            var b = Buf(0.4f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double f = 180 + 60 * Math.Sin(t * 30);
                double env = Math.Min(1, t / 0.02) * Math.Exp(-t * 6);
                b[i] = (float)((Saw(t * f) * 0.5 + r.Bipolar() * 0.45) * env);
            }
            LowPass(b, rate, 1600f);
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] Moo(FastRng r, int rate)
        {
            var b = Buf(1.2f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double f = 110 + 30 * Math.Sin(t * 2.5);
                double env = Math.Min(1, t / 0.15) * Math.Min(1, (1.2 - t) / 0.3);
                b[i] = (float)((Saw(t * f) * 0.6 + Math.Sin(Tau * f * 2 * t) * 0.3) * env);
            }
            LowPass(b, rate, 900f);
            Normalize(b, 0.75f);
            return b;
        }

        public static float[] Bell(FastRng r, int rate, float f)
        {
            var b = Buf(0.6f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                b[i] = (float)((Math.Sin(Tau * f * t) + 0.5 * Math.Sin(Tau * f * 2.76 * t)) * Math.Exp(-t * 7));
            }
            Normalize(b, 0.6f);
            return b;
        }
    }
}
