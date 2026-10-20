using System;

namespace SolarEclipse.Audio
{
    /// <summary>Building blocks shared by all sound recipes. Everything here is pure C# so it can run off the main thread.</summary>
    public static class SynthKit
    {
        public const float TwoPi = (float)(Math.PI * 2);

        public static float[] Buf(float seconds, int rate) => new float[Math.Max(1, (int)(seconds * rate))];

        public static float Sin(float phase01) => (float)Math.Sin(phase01 * TwoPi);

        public static void Normalize(float[] b, float peak = 0.9f)
        {
            float max = 0;
            for (int i = 0; i < b.Length; i++) max = Math.Max(max, Math.Abs(b[i]));
            if (max < 1e-6f) return;
            float g = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] *= g;
        }

        public static void Gain(float[] b, float g) { for (int i = 0; i < b.Length; i++) b[i] *= g; }

        public static void Fade(float[] b, int rate, float fadeIn, float fadeOut)
        {
            int fi = (int)(fadeIn * rate), fo = (int)(fadeOut * rate);
            for (int i = 0; i < fi && i < b.Length; i++) b[i] *= i / (float)fi;
            for (int i = 0; i < fo && i < b.Length; i++) b[b.Length - 1 - i] *= i / (float)fo;
        }

        public static void Drive(float[] b, float drive)
        {
            float norm = (float)Math.Tanh(drive);
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * drive) / norm;
        }

        public static void Add(float[] dst, float[] src, int offset, float gain)
        {
            for (int i = 0; i < src.Length; i++)
            {
                int j = i + offset;
                if (j < 0) continue;
                if (j >= dst.Length) break;
                dst[j] += src[i] * gain;
            }
        }

        /// <summary>Returns a longer buffer with a reverb tail mixed in.</summary>
        public static float[] WithReverb(float[] b, int rate, float room, float wet, float tailSeconds, float damping = 0.35f)
        {
            var o = new float[b.Length + (int)(tailSeconds * rate)];
            var rv = new Reverb(rate, room, damping);
            var hp = Biquad.HighPass(rate, 120);
            for (int i = 0; i < o.Length; i++)
            {
                float x = i < b.Length ? b[i] : 0;
                o[i] = x + rv.Process(hp.Process(x)) * wet;
            }
            Fade(o, rate, 0, Math.Min(tailSeconds * 0.6f, 0.4f));
            return o;
        }

        public static void LowPass(float[] b, int rate, float f, float q = 0.707f)
        {
            var bq = Biquad.LowPass(rate, f, q);
            for (int i = 0; i < b.Length; i++) b[i] = bq.Process(b[i]);
        }

        public static void HighPass(float[] b, int rate, float f, float q = 0.707f)
        {
            var bq = Biquad.HighPass(rate, f, q);
            for (int i = 0; i < b.Length; i++) b[i] = bq.Process(b[i]);
        }

        /// <summary>Filtered noise burst with a decay envelope.</summary>
        public static float[] NoiseBurst(int rate, FastRng rng, float seconds, float lowCut, float highCut, float attack, float decayTau)
        {
            var b = Buf(seconds, rate);
            var hp = Biquad.HighPass(rate, lowCut);
            var lp = Biquad.LowPass(rate, highCut);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                b[i] = lp.Process(hp.Process(rng.Bipolar())) * Env.AD(t, attack, decayTau);
            }
            return b;
        }

        /// <summary>Damped sine with a pitch glide (thumps, drips, pops).</summary>
        public static float[] Chirp(int rate, float seconds, float f0, float f1, float glideTau, float decayTau, float attack = 0.001f)
        {
            var b = Buf(seconds, rate);
            float phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float f = f1 + (f0 - f1) * Env.Exp(t, glideTau);
                phase += f / rate;
                b[i] = Sin(phase) * Env.AD(t, attack, decayTau);
            }
            return b;
        }

        /// <summary>Sum of decaying inharmonic partials (metal, glass, bells, wood knocks).</summary>
        public static float[] Modal(FastRng rng, int rate, float seconds, float[] freqs, float[] decays, float detune = 0.02f, float strikeNoise = 0.2f)
        {
            var b = Buf(seconds, rate);
            var ph = new float[freqs.Length];
            var fr = new float[freqs.Length];
            for (int k = 0; k < freqs.Length; k++) fr[k] = freqs[k] * (1 + rng.Bipolar() * detune);
            var hp = Biquad.HighPass(rate, 1500);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float s = 0;
                for (int k = 0; k < fr.Length; k++)
                {
                    ph[k] += fr[k] / rate;
                    if (ph[k] > 1) ph[k] -= 1;
                    s += Sin(ph[k]) * Env.Exp(t, decays[Math.Min(k, decays.Length - 1)]) / (1 + k * 0.4f);
                }
                s += hp.Process(rng.Bipolar()) * strikeNoise * Env.Exp(t, 0.004f);
                b[i] = s * Env.AD(t, 0.0008f, 100f);
            }
            return b;
        }

        // ---------------------------------------------------------------- voice

        public struct Vowel
        {
            public float F1, F2, F3;
            public Vowel(float f1, float f2, float f3) { F1 = f1; F2 = f2; F3 = f3; }
            public static Vowel Lerp(Vowel a, Vowel b, float t)
            {
                t = Math.Max(0, Math.Min(1, t));
                return new Vowel(a.F1 + (b.F1 - a.F1) * t, a.F2 + (b.F2 - a.F2) * t, a.F3 + (b.F3 - a.F3) * t);
            }
            public Vowel Scale(float s) => new Vowel(F1 * s, F2 * s, F3 * s);
        }

        public static readonly Vowel A = new Vowel(730, 1090, 2440);
        public static readonly Vowel E = new Vowel(530, 1840, 2480);
        public static readonly Vowel I = new Vowel(300, 2250, 3000);
        public static readonly Vowel O = new Vowel(570, 840, 2410);
        public static readonly Vowel U = new Vowel(320, 870, 2240);
        public static readonly Vowel AE = new Vowel(660, 1720, 2410);
        public static readonly Vowel UH = new Vowel(520, 1190, 2390);
        public static readonly Vowel N = new Vowel(260, 1050, 2300);
        public static readonly Vowel R = new Vowel(490, 1350, 1690);
        public static readonly Vowel L = new Vowel(360, 1000, 2600);

        /// <summary>
        /// Source-filter voice: Rosenberg glottal pulses + aspiration noise through three formant band-passes.
        /// pitch(t) Hz, vowel(t) formants, amp(t) 0..1, breath = noise mix, rough = jitter/growl.
        /// </summary>
        public static float[] Voice(int rate, FastRng rng, float seconds, Func<float, float> pitch, Func<float, Vowel> vowel, Func<float, float> amp, float breath, float rough, float formantScale = 1f)
        {
            var b = Buf(seconds, rate);
            var f1 = new Biquad(rate); var f2 = new Biquad(rate); var f3 = new Biquad(rate); var f4 = new Biquad(rate);
            f4.SetBandPass(3500 * formantScale, 4);
            var brLp = Biquad.LowPass(rate, 6000);
            float phase = 0, prevG = 0, jitter = 0, shimmer = 1;
            var smooth = new OnePole(rate, 30);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                if ((i & 31) == 0)
                {
                    var v = vowel(t).Scale(formantScale);
                    f1.SetBandPass(v.F1, v.F1 / 80f);
                    f2.SetBandPass(v.F2, v.F2 / 110f);
                    f3.SetBandPass(v.F3, v.F3 / 160f);
                }
                float p = Math.Max(30f, pitch(t));
                float prevPhase = phase;
                phase += p * (1 + jitter) / rate;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    jitter = rng.Bipolar() * 0.012f * (0.3f + rough);
                    shimmer = 1f + rng.Bipolar() * 0.15f * rough;
                }
                float ph = phase;
                float g = ph < 0.4f ? 0.5f * (1 - (float)Math.Cos(Math.PI * ph / 0.4f)) : ph < 0.56f ? (float)Math.Cos(Math.PI * (ph - 0.4f) / 0.32f) : 0f;
                float src = (g - prevG) * 8f * shimmer;
                prevG = g;
                // Growl: subharmonic amplitude modulation.
                if (rough > 0) src *= 1f + rough * 0.6f * Sin(phase * 0.5f + (prevPhase > phase ? 0.5f : 0f));
                float open = g + 0.15f;
                src += brLp.Process(rng.Bipolar()) * breath * open;
                float y = f1.Process(src) * 1.0f + f2.Process(src) * 0.65f + f3.Process(src) * 0.35f + f4.Process(src) * 0.12f;
                b[i] = y * smooth.Process(amp(t));
            }
            Normalize(b, 0.9f);
            return b;
        }
    }
}
