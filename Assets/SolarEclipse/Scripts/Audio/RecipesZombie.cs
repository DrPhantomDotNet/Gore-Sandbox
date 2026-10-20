using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>
    /// Zombie vocals and eating sounds: low wet groans, chase growls, attack snarls, bites, chewing, the Screamer's
    /// shriek and a death gurgle. All built from the formant voice with heavy roughness, subharmonics and drive.
    /// </summary>
    public static class RecipesZombie
    {
        public static void Define(Action<string, int, Func<FastRng, int, int, float[]>, int> def)
        {
            def("z_groan", 5, (r, s, v) => Groan(r, s, v), 22050);
            def("z_growl", 4, (r, s, v) => Growl(r, s, v), 22050);
            def("z_snarl", 3, (r, s, v) => Snarl(r, s, v), 22050);
            def("z_bite", 3, (r, s, v) => Bite(r, s, v), 22050);
            def("z_chew", 4, (r, s, v) => Chew(r, s, v), 22050);
            def("z_shriek", 2, (r, s, v) => Shriek(r, s, v), 22050);
            def("z_death", 2, (r, s, v) => DeathGurgle(r, s, v), 22050);
            def("z_rise", 2, (r, s, v) => Rise(r, s, v), 22050);
        }

        /// <summary>Adds a wet, bubbly gurgle (random short band-passed noise blips) to a buffer.</summary>
        static void Gurgle(FastRng r, int rate, float[] b, float amount, float density)
        {
            var bp = Biquad.BandPass(rate, 700f, 3f);
            float blip = 0f, blipEnv = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                if (r.Next() < density / rate) { blipEnv = 1f; bp.SetBandPass(r.Range(350f, 1400f), r.Range(4f, 9f)); }
                blipEnv *= 0.9985f;
                blip = bp.Process(r.Bipolar()) * blipEnv;
                b[i] += blip * amount;
            }
        }

        static float[] Groan(FastRng r, int rate, int v)
        {
            float len = r.Range(1.3f, 2.3f);
            float p = r.Range(58f, 80f);
            var start = v % 2 == 0 ? UH : O;
            var b = Voice(rate, r, len,
                t => p * (1f + 0.12f * (float)Math.Sin(t * 2.3f + v)) * (1f - 0.18f * t / len),
                t => Vowel.Lerp(start, U, t / len),
                t => Env.AR(t, 0.18f, len * 0.45f, len * 0.4f) * (0.75f + 0.25f * (float)Math.Abs(Math.Sin(t * 5.5f))),
                0.65f, 1.6f, 0.82f);
            Gurgle(r, rate, b, 0.25f, 9f);
            Drive(b, 2.2f);
            LowPass(b, rate, 2600f);
            Normalize(b, 0.85f);
            return b;
        }

        static float[] Growl(FastRng r, int rate, int v)
        {
            float len = r.Range(0.7f, 1.2f);
            float p = r.Range(70f, 95f);
            var b = Voice(rate, r, len,
                t => p * (1.15f - 0.3f * t / len) * (1f + 0.05f * Sin(t * 23f)),
                t => Vowel.Lerp(A, R, t / len),
                t => Env.AR(t, 0.05f, len * 0.5f, len * 0.4f),
                0.55f, 2f, 0.85f);
            Gurgle(r, rate, b, 0.18f, 14f);
            Drive(b, 3f);
            LowPass(b, rate, 3200f);
            Normalize(b, 0.9f);
            return b;
        }

        static float[] Snarl(FastRng r, int rate, int v)
        {
            float len = r.Range(0.45f, 0.7f);
            float p = r.Range(110f, 150f);
            var b = Voice(rate, r, len,
                t => p * (0.8f + 0.5f * (float)Math.Sin(Math.PI * Math.Min(1, t / len))),
                t => Vowel.Lerp(AE, A, t / len),
                t => Env.AR(t, 0.015f, len * 0.5f, len * 0.4f),
                0.7f, 2.2f, 0.95f);
            Drive(b, 3.5f);
            Normalize(b, 0.9f);
            return b;
        }

        /// <summary>A wet crunch: teeth snap, a tearing squelch and a little bone crack.</summary>
        static float[] Bite(FastRng r, int rate, int v)
        {
            float len = 0.45f;
            var b = Buf(len, rate);
            var squelch = Biquad.BandPass(rate, 500f + v * 120f, 1.5f);
            var crack = Biquad.HighPass(rate, 1800f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float snap = crack.Process(r.Bipolar()) * Env.AD(t, 0.001f, 0.012f) * 0.9f;
                float tear = squelch.Process(r.Bipolar()) * Env.AD(t - 0.03f, 0.02f, 0.12f) * (0.6f + 0.4f * Sin(t * 37f)) * 2.2f;
                float bone = crack.Process(r.Bipolar()) * Env.AD(t - 0.12f - v * 0.03f, 0.001f, 0.008f) * 0.6f;
                b[i] = snap + tear + bone;
            }
            Drive(b, 1.6f);
            Normalize(b, 0.85f);
            return b;
        }

        /// <summary>Several wet chews with a low "mm" grunt underneath.</summary>
        static float[] Chew(FastRng r, int rate, int v)
        {
            float len = 1.2f + v * 0.15f;
            var b = Buf(len, rate);
            var bp = Biquad.BandPass(rate, 380f, 1.2f);
            var bp2 = Biquad.BandPass(rate, 1300f, 2.5f);
            float chewRate = r.Range(2.8f, 3.8f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float ph = t * chewRate % 1f;
                float env = Env.AD(ph, 0.02f, 0.09f);
                b[i] = (bp.Process(r.Bipolar()) * 2.2f + bp2.Process(r.Bipolar()) * 0.9f) * env;
            }
            Gurgle(r, rate, b, 0.35f, 18f);
            var grunt = Voice(rate, r, len, t => 75f + 8f * Sin(t * chewRate), t => Vowel.Lerp(N, U, 0.5f), t => 0.35f * Env.AR(t, 0.1f, len * 0.6f, len * 0.3f), 0.5f, 1.4f, 0.8f);
            Add(b, grunt, 0, 0.5f);
            Fade(b, rate, 0.01f, 0.1f);
            Normalize(b, 0.8f);
            return b;
        }

        /// <summary>The Screamer: a long, piercing, distorted shriek with a ragged rising pitch.</summary>
        static float[] Shriek(FastRng r, int rate, int v)
        {
            float len = r.Range(1.6f, 2.1f);
            float p = r.Range(420f, 520f);
            var b = Voice(rate, r, len,
                t => p * (0.7f + 0.55f * (float)Math.Sin(Math.PI * Math.Min(1, t / len * 1.2f))) * (1f + 0.04f * Sin(t * 11f)),
                t => Vowel.Lerp(A, I, Math.Min(1f, t / len * 1.4f)),
                t => Env.AR(t, 0.06f, len * 0.6f, len * 0.35f),
                0.85f, 2.4f, 1.25f);
            Drive(b, 4f);
            HighPass(b, rate, 250f);
            b = WithReverb(b, rate, 0.7f, 0.25f, 0.6f);
            Normalize(b, 0.95f);
            return b;
        }

        static float[] DeathGurgle(FastRng r, int rate, int v)
        {
            float len = r.Range(1.2f, 1.6f);
            float p = r.Range(65f, 80f);
            var b = Voice(rate, r, len,
                t => p * (1f - 0.45f * t / len),
                t => Vowel.Lerp(A, U, t / len),
                t => Env.AR(t, 0.02f, len * 0.25f, len * 0.7f),
                0.6f, 1.8f, 0.8f);
            Gurgle(r, rate, b, 0.55f, 25f);
            Drive(b, 2f);
            LowPass(b, rate, 2200f);
            Normalize(b, 0.85f);
            return b;
        }

        /// <summary>A corpse reanimating: a deep inhale rattle into a rising groan.</summary>
        static float[] Rise(FastRng r, int rate, int v)
        {
            float len = 1.8f;
            float p = r.Range(55f, 70f);
            var b = Voice(rate, r, len,
                t => p * (0.8f + 0.5f * t / len),
                t => Vowel.Lerp(U, A, t / len),
                t => Env.AR(t, 0.4f, len * 0.4f, len * 0.3f),
                0.9f, 1.8f, 0.8f);
            Gurgle(r, rate, b, 0.3f, 12f);
            Drive(b, 2.4f);
            Normalize(b, 0.85f);
            return b;
        }
    }
}
