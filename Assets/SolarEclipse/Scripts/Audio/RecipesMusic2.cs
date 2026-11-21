using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Gore Sandbox 2 combat music: a 16-second 120 BPM loop (kick, snare, hats, driving bass, dark stabs).</summary>
    public static class RecipesMusic2
    {
        public static float[] Combat(FastRng r, int rate)
        {
            const float bpm = 120f, beat = 60f / bpm, bar = beat * 4f;
            const int bars = 8;
            float len = bar * bars;
            var b = Buf(len, rate);
            float[] roots = { 55f, 55f, 43.65f, 49f, 55f, 55f, 41.2f, 49f }; // A A F G A A E G
            var lp = new Biquad(rate);
            float bassPh = 0f, subPh = 0f;
            uint n = 12345u;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                int barI = (int)(t / bar) % bars;
                float root = roots[barI];
                float bt = t % beat, eighth = t % (beat / 2f), sixteenth = t % (beat / 4f);
                int beatI = (int)(t / beat) % 4;
                float s = 0f;
                // kick: beats 1 and 3, plus a pickup on the "and" of 4
                if (beatI == 0 || beatI == 2) s += Sin(bt * (50f + 90f * Env.Exp(bt, 0.025f))) * Env.AD(bt, 0.001f, 0.16f) * 0.9f;
                float pk = (t - beat * 3.5f) % bar;
                if (pk >= 0f && pk < 0.2f) s += Sin(pk * (50f + 90f * Env.Exp(pk, 0.025f))) * Env.AD(pk, 0.001f, 0.12f) * 0.6f;
                // noise source
                n ^= n << 13; n ^= n >> 17; n ^= n << 5; float noise = (n & 0xFFFF) / 32768f - 1f;
                // snare: beats 2 and 4
                if (beatI == 1 || beatI == 3) s += (noise * 0.7f + Sin(bt * 190f) * 0.4f) * Env.AD(bt, 0.001f, 0.09f) * 0.55f;
                // hats: 8ths, accent on offbeats
                s += noise * Env.AD(eighth, 0.0005f, 0.018f) * (((int)(t / (beat / 2f)) & 1) == 1 ? 0.22f : 0.12f);
                // bass: 16th-note saw on the root, filter pumping with the beat
                bassPh += root * 2f / rate; subPh += root / rate;
                float saw = (bassPh % 1f) * 2f - 1f;
                if ((i & 31) == 0) lp.SetLowPass(220f + 900f * Env.Exp(sixteenth, 0.04f), 2.2f);
                float gate = sixteenth < beat / 4f * 0.8f ? 1f : 0.2f;
                s += lp.Process(saw * 0.35f) * gate + Sin(subPh) * 0.25f;
                // dark stab on the first beat of every second bar
                if (barI % 2 == 0 && t % bar < 0.5f)
                {
                    float st = t % bar;
                    float st1 = Sin(st * root * 4f) + Sin(st * root * 4f * 1.189f) + Sin(st * root * 4f * 1.498f);
                    s += st1 * Env.AD(st, 0.005f, 0.18f) * 0.12f;
                }
                b[i] = s;
            }
            Drive(b, 1.4f);
            Fade(b, rate, 0.005f, 0.005f);
            Normalize(b, 0.7f);
            return b;
        }
    }
}
