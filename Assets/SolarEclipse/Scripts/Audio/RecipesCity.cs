using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Open World sounds: car engine loop, horns, police siren, cash register, phone ring and message ping.</summary>
    public static class RecipesCity
    {
        const double Tau = Math.PI * 2;

        /// <summary>Engine idle loop (1 s; the car pitches it up with speed).</summary>
        public static float[] Engine(FastRng r, int rate)
        {
            var b = Buf(1f, rate);
            double f = 38; // whole number of cycles in 1 s: loops cleanly
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double ph = t * f;
                double saw = 2 * (ph - Math.Floor(ph)) - 1;
                double s = saw * 0.45 + Math.Sin(Tau * f * 2 * t) * 0.3 + Math.Sin(Tau * f * 0.5 * t) * 0.35;
                s *= 0.75 + 0.25 * Math.Sin(Tau * 4 * t);
                b[i] = (float)(s * 0.6 + r.Bipolar() * 0.04);
            }
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] Horn(FastRng r, int rate, bool air)
        {
            float len = air ? 1.1f : 0.45f;
            var b = Buf(len, rate);
            double f1 = air ? 185 : 415, f2 = air ? 233 : 523;
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double env = Math.Min(1, t / 0.02) * Math.Min(1, (len - t) / 0.06);
                double s = Math.Sign(Math.Sin(Tau * f1 * t)) * 0.5 + Math.Sign(Math.Sin(Tau * f2 * t)) * 0.5;
                if (air) s += Math.Sin(Tau * f1 * 0.5 * t) * 0.6;
                b[i] = (float)(s * env * 0.4);
            }
            Normalize(b, air ? 0.95f : 0.8f);
            return b;
        }

        /// <summary>Wailing siren, 2 s loop.</summary>
        public static float[] Siren(FastRng r, int rate)
        {
            var b = Buf(2f, rate);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double f = 700 + 500 * (0.5 - 0.5 * Math.Cos(Tau * 0.5 * t));
                ph += f / rate;
                b[i] = (float)(Math.Sin(Tau * ph) * 0.5 + Math.Sin(Tau * ph * 2) * 0.15);
            }
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>"Ka-ching": a bell ding plus a drawer slide.</summary>
        public static float[] Cash(FastRng r, int rate)
        {
            var b = Buf(0.7f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double ding = t > 0.08 ? Math.Exp(-(t - 0.08) * 6) * (Math.Sin(Tau * 2093 * t) + 0.5 * Math.Sin(Tau * 3136 * t)) : 0;
                double slide = t < 0.12 ? r.Bipolar() * (1 - t / 0.12) * 0.4 : 0;
                b[i] = (float)(ding * 0.5 + slide);
            }
            Normalize(b, 0.8f);
            return b;
        }

        /// <summary>Phone ringtone (one ring, 1.6 s with the pause).</summary>
        public static float[] Ring(FastRng r, int rate)
        {
            var b = Buf(1.6f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                bool on = t < 0.9 && ((int)(t / 0.05)) % 2 == 0;
                double s = on ? Math.Sin(Tau * 1200 * t) * 0.5 + Math.Sin(Tau * 1500 * t) * 0.5 : 0;
                b[i] = (float)(s * 0.5);
            }
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>Text message ping.</summary>
        public static float[] Ping(FastRng r, int rate)
        {
            var b = Buf(0.35f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)rate;
                double f = t < 0.12 ? 1318 : 1760;
                b[i] = (float)(Math.Sin(Tau * f * t) * Math.Exp(-(t % 0.12) * 18) * 0.6);
            }
            Normalize(b, 0.7f);
            return b;
        }
    }
}
