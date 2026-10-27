using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Sandbox tools: rope tool, gravity gun, bullet time and photo mode sounds.</summary>
    public static class RecipesTools
    {
        public static void Define(Action<string, int, Func<FastRng, int, int, float[]>, int> def)
        {
            def("rope_attach", 3, (r, s, v) => RopeCreak(r, s, 0.35f, 1f), 44100);
            def("rope_pick", 2, (r, s, v) => RopeCreak(r, s, 0.12f, 1.6f), 44100);
            def("rope_cut", 2, (r, s, v) => RopeSnap(r, s), 44100);
            def("gg_hum", 1, (r, s, v) => GravHum(r, s), 44100);
            def("gg_grab", 2, (r, s, v) => Sweep(r, s, 0.25f, 180f, 700f, 0.4f), 44100);
            def("gg_drop", 2, (r, s, v) => Sweep(r, s, 0.2f, 600f, 150f, 0.4f), 44100);
            def("gg_punt", 3, (r, s, v) => Punt(r, s), 44100);
            def("gg_dry", 2, (r, s, v) => Sweep(r, s, 0.12f, 900f, 500f, 0.7f), 44100);
            def("bt_in", 1, (r, s, v) => TimeWarp(r, s, true), 44100);
            def("bt_out", 1, (r, s, v) => TimeWarp(r, s, false), 44100);
            def("photo_shutter", 2, (r, s, v) => Shutter(r, s), 44100);
        }

        /// <summary>Fibre creak: a burst of filtered noise grains.</summary>
        static float[] RopeCreak(FastRng r, int rate, float seconds, float pitch)
        {
            var b = Buf(seconds, rate);
            var bp = Biquad.BandPass(rate, 900f * pitch * r.Range(0.85f, 1.15f), 3f);
            float grain = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                if (r.Next() < 0.004f) grain = r.Range(0.4f, 1f);
                grain *= 0.996f;
                b[i] = bp.Process(r.Bipolar() * grain) * Env.AR(t, 0.01f, seconds * 0.5f, seconds * 0.4f);
            }
            Normalize(b, 0.6f);
            return b;
        }

        static float[] RopeSnap(FastRng r, int rate)
        {
            var b = Buf(0.3f, rate);
            var hp = Biquad.HighPass(rate, 1200f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                b[i] = hp.Process(r.Bipolar()) * Env.Exp(t, 0.03f) + Sin(t * 180f) * Env.Exp(t, 0.06f) * 0.4f;
            }
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>Seamless 2 s electric hum (integer cycles per loop).</summary>
        static float[] GravHum(FastRng r, int rate)
        {
            var b = Buf(2f, rate);
            var lp = Biquad.LowPass(rate, 1400f, 1.5f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float s = Sin(t * 70f) * 0.5f + Sin(t * 140f) * 0.3f + Sin(t * 211f) * 0.15f + Sin(t * 420f) * 0.08f;
                s *= 0.7f + 0.3f * Sin(t * 4f);
                b[i] = lp.Process(s + r.Bipolar() * 0.06f);
            }
            Normalize(b, 0.5f);
            return b;
        }

        static float[] Sweep(FastRng r, int rate, float seconds, float f0, float f1, float noise)
        {
            var b = Buf(seconds, rate);
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float k = t / seconds;
                float f = f0 + (f1 - f0) * k;
                ph += f / rate;
                float saw = (ph % 1f) * 2f - 1f;
                b[i] = (saw * 0.6f + r.Bipolar() * noise) * Env.AD(t, 0.005f, seconds * 0.35f);
            }
            var lp = Biquad.LowPass(rate, 4000f);
            for (int i = 0; i < b.Length; i++) b[i] = lp.Process(b[i]);
            Normalize(b, 0.6f);
            return b;
        }

        /// <summary>Heavy "zap-thump": a low boom under a falling electric crack.</summary>
        static float[] Punt(FastRng r, int rate)
        {
            var b = Buf(0.7f, rate);
            var hp = Biquad.HighPass(rate, 2000f);
            float ph = 0f, ph2 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float f = 60f + 140f * Env.Exp(t, 0.04f);
                ph += f / rate;
                ph2 += (2400f - 2000f * Math.Min(1f, t / 0.2f)) / rate;
                float boom = Sin(ph) * Env.AD(t, 0.002f, 0.18f);
                float crack = hp.Process(r.Bipolar()) * Env.Exp(t, 0.05f) * 0.8f;
                float zap = ((ph2 % 1f) < 0.5f ? 1f : -1f) * Env.Exp(t, 0.08f) * 0.25f;
                b[i] = boom + crack + zap;
            }
            Drive(b, 2f);
            Normalize(b, 0.85f);
            return WithReverb(b, rate, 0.6f, 0.25f, 0.4f);
        }

        /// <summary>Bullet-time whoosh: pitch dives (in) or rises (out) with a low heartbeat thump.</summary>
        static float[] TimeWarp(FastRng r, int rate, bool down)
        {
            var b = Buf(0.9f, rate);
            var bp = Biquad.BandPass(rate, 600f, 1.2f);
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float k = t / 0.9f;
                float f = down ? 420f - 340f * k : 80f + 340f * k;
                ph += f / rate;
                float tone = Sin(ph) * 0.5f;
                float air = bp.Process(r.Bipolar()) * 0.7f;
                float env = down ? Env.AR(t, 0.05f, 0.3f, 0.5f) : Env.AR(t, 0.3f, 0.3f, 0.25f);
                float thump = Sin(t * 55f) * Env.Exp(t - (down ? 0.35f : 0.05f), 0.08f) * (t > (down ? 0.35f : 0.05f) ? 0.7f : 0f);
                b[i] = (tone + air) * env + thump;
            }
            Normalize(b, 0.7f);
            return b;
        }

        static float[] Shutter(FastRng r, int rate)
        {
            var b = Buf(0.25f, rate);
            var hp = Biquad.HighPass(rate, 1800f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float c1 = Env.Exp(t, 0.006f);
                float c2 = t > 0.09f ? Env.Exp(t - 0.09f, 0.008f) : 0f;
                b[i] = hp.Process(r.Bipolar()) * (c1 + c2 * 0.8f);
            }
            Normalize(b, 0.6f);
            return b;
        }
    }
}
