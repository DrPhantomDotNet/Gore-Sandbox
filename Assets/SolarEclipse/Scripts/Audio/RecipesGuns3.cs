using System;
using static SolarEclipse.Audio.SynthKit;
using W = SolarEclipse.Audio.RecipesWeapons;

namespace SolarEclipse.Audio
{
    /// <summary>Gun pack 3: MAC-11 and the suppressed shot used by the suppressor attachment.</summary>
    public static class RecipesGuns3
    {
        // Short, snappy and bright: at 1150 rpm the shots overlap, so the tail stays very short.
        static readonly W.GunVoice Mac = new W.GunVoice { BodyCut = 3300, BodyTau = 0.016f, ThumpHz = 125, ThumpAmt = 0.45f, CrackAmt = 0.45f, TailTau = 0.07f, TailCut = 1700, Drive = 2.7f, Room = 0.42f, Wet = 0.24f, Length = 0.24f };

        public static void Define(Action<string, int, Func<FastRng, int, int, float[]>, int> def)
        {
            def("gun_mac11", 4, (r, s, v) => W.Gunshot(r, s, Mac), 44100);
            def("gun_suppressed", 3, (r, s, v) => Suppressed(r, s), 44100);
        }

        /// <summary>A "thwip": muffled low puff, a mechanical action clack and a little hiss, no supersonic crack.</summary>
        static float[] Suppressed(FastRng r, int rate)
        {
            var b = Buf(0.32f, rate);
            var lp = Biquad.LowPass(rate, 900f * r.Range(0.85f, 1.15f), 0.8f);
            var bp = Biquad.HighPass(rate, 3500f);
            float ph = 0f, hz = 150f * r.Range(0.9f, 1.1f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float n = r.Bipolar();
                float puff = lp.Process(n) * Env.AD(t, 0.0008f, 0.022f) * 2.2f;
                ph += hz * (1f + 0.8f * Env.Exp(t, 0.01f)) / rate;
                float thump = Sin(ph) * Env.AD(t, 0.001f, 0.03f) * 0.7f;
                float hiss = bp.Process(n) * Env.AD(t, 0.002f, 0.05f) * 0.12f;
                b[i] = puff + thump + hiss;
            }
            Add(b, W.Click(r, rate, 1.3f, 0.5f, 0.08f), (int)(0.004f * rate), 0.55f);
            Drive(b, 1.6f);
            b = WithReverb(b, rate, 0.3f, 0.12f, 0.15f);
            Normalize(b, 0.8f);
            return b;
        }
    }
}
