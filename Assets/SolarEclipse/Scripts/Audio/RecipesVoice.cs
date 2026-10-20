using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>
    /// Goredoll and player vocal sounds made with the formant voice: screams, pain, choking, breathing, coughs, begging.
    /// <paramref name="v"/> is the variant index, used to vary pitch and timing.
    /// </summary>
    public static class RecipesVoice
    {
        static float Vib(float t, float rate, float depth) => 1f + depth * (float)Math.Sin(t * rate * TwoPi);

        public static float[] PainGrunt(FastRng r, int rate, int v)
        {
            float len = r.Range(0.22f, 0.4f);
            float p0 = r.Range(105, 140);
            var vowel = v % 3 == 0 ? UH : v % 3 == 1 ? A : O;
            return Voice(rate, r, len, t => p0 * (1.1f - 0.25f * t / len), t => vowel, t => Env.AR(t, 0.008f, len * 0.4f, len * 0.5f), 0.35f, 0.4f);
        }

        public static float[] Scream(FastRng r, int rate, int v)
        {
            float len = r.Range(0.9f, 1.6f);
            float p = r.Range(260, 380);
            var b = Voice(rate, r, len,
                t => p * (0.85f + 0.3f * (float)Math.Sin(Math.PI * Math.Min(1, t / len * 1.1f))) * Vib(t, 6.5f, 0.025f),
                t => Vowel.Lerp(A, AE, t / len),
                t => Env.AR(t, 0.05f, len * 0.6f, len * 0.35f), 0.45f, 0.9f, 1.08f);
            Drive(b, 1.8f);
            Normalize(b, 0.9f);
            return b;
        }

        public static float[] Argh(FastRng r, int rate, int v)
        {
            float len = r.Range(0.5f, 0.8f);
            float p = r.Range(150, 200);
            return Voice(rate, r, len, t => p * (1f - 0.3f * t / len), t => Vowel.Lerp(A, R, Math.Max(0, (t / len - 0.6f) * 2.5f)), t => Env.AR(t, 0.02f, len * 0.5f, len * 0.45f), 0.4f, 0.75f);
        }

        public static float[] Moan(FastRng r, int rate, int v)
        {
            float len = r.Range(1.0f, 1.8f);
            float p = r.Range(95, 125);
            return Voice(rate, r, len, t => p * (1.05f - 0.2f * t / len) * Vib(t, 4f, 0.02f), t => Vowel.Lerp(O, U, t / len), t => Env.AR(t, 0.15f, len * 0.4f, len * 0.45f), 0.5f, 0.35f);
        }

        public static float[] Whimper(FastRng r, int rate, int v)
        {
            float len = r.Range(0.4f, 0.7f);
            float p = r.Range(230, 300);
            return Voice(rate, r, len, t => p * (1.1f - 0.25f * t / len) * Vib(t, 9f, 0.04f), t => Vowel.Lerp(I, U, t / len), t => Env.AR(t, 0.03f, len * 0.3f, len * 0.6f) * (0.6f + 0.4f * (float)Math.Abs(Math.Sin(t * 22))), 0.6f, 0.2f);
        }

        /// <summary>"No! No! Noo!" style begging (n → o transitions).</summary>
        public static float[] NoNoNo(FastRng r, int rate, int v)
        {
            int words = 2 + v % 2;
            float wordLen = r.Range(0.28f, 0.36f), gap = 0.08f;
            float len = words * (wordLen + gap) + 0.3f;
            float p = r.Range(170, 230);
            return Voice(rate, r, len,
                t => { int w = (int)(t / (wordLen + gap)); float lt = t - w * (wordLen + gap); return p * (1.15f + 0.1f * w) * (1f - 0.2f * lt / wordLen); },
                t => { float lt = t % (wordLen + gap); return Vowel.Lerp(N, O, lt / 0.06f); },
                t =>
                {
                    int w = (int)(t / (wordLen + gap));
                    if (w >= words) return Env.AR(t - w * (wordLen + gap), 0.02f, 0.15f, 0.15f) * 0f;
                    float lt = t - w * (wordLen + gap);
                    return lt < wordLen ? Env.AR(lt, 0.03f, wordLen * 0.5f, wordLen * 0.4f) : 0f;
                }, 0.35f, 0.5f);
        }

        public static float[] Hey(FastRng r, int rate, int v)
        {
            float len = 0.45f;
            float p = r.Range(140, 180);
            var b = Voice(rate, r, len, t => p * (1.15f - 0.25f * t / len), t => Vowel.Lerp(E, I, t / len), t => t < 0.05f ? 0.2f : Env.AR(t - 0.05f, 0.02f, 0.2f, 0.18f), 0.8f, 0.2f);
            return b;
        }

        public static float[] Huh(FastRng r, int rate, int v)
        {
            float len = 0.4f;
            float p = r.Range(110, 140);
            return Voice(rate, r, len, t => p * (1f + 0.45f * t / len), t => UH, t => t < 0.06f ? 0.25f : Env.AR(t - 0.06f, 0.02f, 0.15f, 0.15f), 0.7f, 0.15f);
        }

        public static float[] Whoa(FastRng r, int rate, int v)
        {
            float len = 0.6f;
            float p = r.Range(150, 190);
            return Voice(rate, r, len, t => p * (1.2f - 0.3f * t / len), t => Vowel.Lerp(Vowel.Lerp(U, O, t * 6), A, (t - 0.15f) * 3f), t => Env.AR(t, 0.04f, 0.3f, 0.25f), 0.4f, 0.3f);
        }

        public static float[] Ow(FastRng r, int rate, int v)
        {
            float len = r.Range(0.35f, 0.5f);
            float p = r.Range(170, 240);
            return Voice(rate, r, len, t => p * (1.1f - 0.35f * t / len), t => Vowel.Lerp(A, U, t / len), t => Env.AR(t, 0.01f, len * 0.4f, len * 0.5f), 0.35f, 0.5f);
        }

        public static float[] Cough(FastRng r, int rate, int v, bool wet)
        {
            int coughs = 1 + v % 3;
            var b = Buf(coughs * 0.35f + 0.2f, rate);
            for (int k = 0; k < coughs; k++)
            {
                var c = Buf(0.3f, rate);
                var bp = Biquad.BandPass(rate, r.Range(700, 1100), 1.2f);
                var lp = Biquad.LowPass(rate, 2500);
                for (int i = 0; i < c.Length; i++)
                {
                    float t = i / (float)rate;
                    c[i] = (bp.Process(r.Bipolar()) * 1.5f + lp.Process(r.Bipolar()) * 0.5f) * Env.AD(t, 0.004f, 0.06f);
                }
                Add(b, c, (int)(k * r.Range(0.28f, 0.36f) * rate), 1f - k * 0.15f);
            }
            if (wet) Add(b, RecipesGore.Gurgle(r, rate, 0.4f), (int)(0.1f * rate), 0.6f);
            Normalize(b, 0.8f);
            return b;
        }

        /// <summary>Choking on blood after a neck wound: strangled voicing interrupted by gurgles and gasps.</summary>
        public static float[] Choke(FastRng r, int rate, int v)
        {
            float len = r.Range(1.6f, 2.4f);
            float p = r.Range(120, 160);
            var voice = Voice(rate, r, len, t => p * (1f + 0.3f * (float)Math.Sin(t * 5f)) * Vib(t, 11f, 0.05f),
                t => Vowel.Lerp(UH, I, 0.5f + 0.5f * (float)Math.Sin(t * 3f)),
                t => Env.AR(t, 0.05f, len * 0.7f, len * 0.25f) * (0.3f + 0.7f * (float)Math.Abs(Math.Sin(t * 9f + v))), 0.9f, 1f, 1.05f);
            var b = Buf(len + 0.2f, rate);
            Add(b, voice, 0, 0.7f);
            Add(b, RecipesGore.Gurgle(r, rate, len), 0, 0.9f);
            for (int k = 0; k < 2; k++) Add(b, Breath(r, rate, true, 2, true), (int)(r.Range(0.2f, len - 0.4f) * rate), 0.6f);
            Normalize(b, 0.85f);
            return b;
        }

        /// <summary>One breath. intensity 0 (calm) .. 3 (agonised). Wet adds bubbling (lung injury).</summary>
        public static float[] Breath(FastRng r, int rate, bool inhale, int intensity, bool wet)
        {
            float len = inhale ? 0.75f - intensity * 0.12f : 0.95f - intensity * 0.13f;
            var b = Buf(len + 0.05f, rate);
            var bp = new Biquad(rate);
            var lp = Biquad.LowPass(rate, 2200 + intensity * 700);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float x = t / len;
                if ((i & 15) == 0) bp.SetBandPass(inhale ? 1100 + 700 * x : 1500 - 700 * x, 1.1f - intensity * 0.15f);
                float env = inhale ? Env.AR(t, len * 0.45f, len * 0.25f, len * 0.3f) : Env.AR(t, len * 0.1f, len * 0.3f, len * 0.6f);
                b[i] = lp.Process(bp.Process(r.Bipolar()) * env);
            }
            Normalize(b, 0.35f + intensity * 0.15f);
            if (intensity >= 2)
            {
                // Voiced component: a strained "uhh" on the breath.
                float p = inhale ? 140 + intensity * 20 : 100 + intensity * 10;
                var voiced = Voice(rate, r, len, t => p * (inhale ? 1f + 0.2f * t / len : 1f - 0.2f * t / len), t => inhale ? Vowel.Lerp(UH, A, t / len) : Vowel.Lerp(A, UH, t / len),
                    t => inhale ? Env.AR(t, len * 0.5f, len * 0.2f, len * 0.3f) : Env.AR(t, len * 0.1f, len * 0.3f, len * 0.6f), 0.9f, 0.3f * intensity);
                Add(b, voiced, 0, 0.1f + 0.1f * (intensity - 1));
            }
            if (wet) Add(b, RecipesGore.Gurgle(r, rate, len * 0.8f), (int)(len * 0.1f * rate), 0.25f + intensity * 0.08f);
            Normalize(b, 0.3f + intensity * 0.17f);
            return b;
        }

        public static float[] Gasp(FastRng r, int rate, int v)
        {
            var b = Breath(r, rate, true, 3, false);
            var shortened = new float[(int)(0.35f * rate)];
            Array.Copy(b, (int)(0.15f * rate), shortened, 0, Math.Min(shortened.Length, b.Length - (int)(0.15f * rate)));
            Fade(shortened, rate, 0.01f, 0.1f);
            Normalize(shortened, 0.7f);
            return shortened;
        }

        public static float[] Hum(FastRng r, int rate, int v)
        {
            // Idle humming: a little tune on "mmm".
            float[] notes = { 0, 2, 4, 2, 0, -3, 0 };
            float noteLen = 0.28f;
            float len = notes.Length * noteLen + 0.2f;
            float root = r.Range(110, 140);
            return Voice(rate, r, len, t => root * (float)Math.Pow(2, notes[Math.Min(notes.Length - 1, (int)(t / noteLen))] / 12f) * Vib(t, 5f, 0.01f),
                t => N, t => Env.AR(t, 0.05f, len - 0.4f, 0.3f) * 0.8f, 0.15f, 0.05f);
        }

        public static float[] Whistle(FastRng r, int rate, int v)
        {
            float[] notes = { 0, 4, 7, 12, 7, 4, 5, 2 };
            float noteLen = 0.2f;
            var b = Buf(notes.Length * noteLen + 0.3f, rate);
            float ph = 0;
            float root = r.Range(900, 1200);
            var bp = Biquad.BandPass(rate, root * 1.5f, 1f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                int n = Math.Min(notes.Length - 1, (int)(t / noteLen));
                float f = root * (float)Math.Pow(2, notes[n] / 12f) * Vib(t, 6f, 0.008f);
                ph += f / rate;
                float env = Env.AR(t, 0.05f, b.Length / (float)rate - 0.4f, 0.3f);
                b[i] = (Sin(ph) + bp.Process(r.Bipolar()) * 0.15f) * env;
            }
            Normalize(b, 0.4f);
            return b;
        }

        public static float[] DeathGroan(FastRng r, int rate, int v)
        {
            float len = r.Range(1.2f, 1.8f);
            float p = r.Range(90, 115);
            return Voice(rate, r, len, t => p * (1f - 0.35f * t / len), t => Vowel.Lerp(A, UH, t / len), t => Env.AR(t, 0.1f, len * 0.3f, len * 0.65f), 0.7f, 0.6f);
        }

        public static float[] PlayerHurt(FastRng r, int rate, int v)
        {
            float len = 0.3f;
            float p = r.Range(120, 150);
            return Voice(rate, r, len, t => p * (1.1f - 0.3f * t / len), t => v % 2 == 0 ? UH : A, t => Env.AR(t, 0.005f, 0.1f, 0.18f), 0.4f, 0.6f, 0.95f);
        }

        public static float[] Sneeze(FastRng r, int rate, int v)
        {
            var b = Buf(1.1f, rate);
            Add(b, Voice(rate, r, 0.5f, t => 200 + 120 * t, t => Vowel.Lerp(A, I, t * 2), t => Env.AR(t, 0.3f, 0.1f, 0.1f) * 0.6f, 0.9f, 0.2f), 0, 0.6f);
            Add(b, RecipesWeapons.Whoosh(r, rate, 0.35f, 2500, 5000, 0.8f), (int)(0.55f * rate), 1f);
            Normalize(b, 0.7f);
            return b;
        }
    }
}
