using System;

namespace SolarEclipse.Audio
{
    /// <summary>Thread-safe random for sound generation (UnityEngine.Random is main-thread only).</summary>
    public sealed class FastRng
    {
        uint s;
        public FastRng(int seed) { s = (uint)(seed * 2654435761u) | 1u; }
        public uint NextU() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        public float Next() => (NextU() & 0xFFFFFF) / 16777216f;
        public float Range(float a, float b) => a + (b - a) * Next();
        public float Bipolar() => Next() * 2f - 1f;
        public int RangeInt(int a, int b) => a + (int)(Next() * (b - a));
    }

    /// <summary>RBJ biquad filter.</summary>
    public sealed class Biquad
    {
        float b0, b1, b2, a1, a2, x1, x2, y1, y2;
        readonly int rate;

        public Biquad(int rate) { this.rate = rate; }

        public static Biquad LowPass(int rate, float f, float q = 0.707f) { var b = new Biquad(rate); b.SetLowPass(f, q); return b; }
        public static Biquad HighPass(int rate, float f, float q = 0.707f) { var b = new Biquad(rate); b.SetHighPass(f, q); return b; }
        public static Biquad BandPass(int rate, float f, float q = 1f) { var b = new Biquad(rate); b.SetBandPass(f, q); return b; }
        public static Biquad Peak(int rate, float f, float q, float gainDb) { var b = new Biquad(rate); b.SetPeak(f, q, gainDb); return b; }

        void Coeffs(out float w0, out float cos, out float alpha, float f, float q)
        {
            f = Math.Max(10f, Math.Min(f, rate * 0.45f));
            w0 = 2f * (float)Math.PI * f / rate;
            cos = (float)Math.Cos(w0);
            alpha = (float)Math.Sin(w0) / (2f * Math.Max(q, 0.05f));
        }

        void Set(float nb0, float nb1, float nb2, float na0, float na1, float na2)
        {
            b0 = nb0 / na0; b1 = nb1 / na0; b2 = nb2 / na0; a1 = na1 / na0; a2 = na2 / na0;
        }

        public void SetLowPass(float f, float q = 0.707f)
        {
            Coeffs(out _, out var c, out var a, f, q);
            Set((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a);
        }

        public void SetHighPass(float f, float q = 0.707f)
        {
            Coeffs(out _, out var c, out var a, f, q);
            Set((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a);
        }

        /// <summary>Constant 0 dB peak gain band-pass.</summary>
        public void SetBandPass(float f, float q = 1f)
        {
            Coeffs(out _, out var c, out var a, f, q);
            Set(a, 0, -a, 1 + a, -2 * c, 1 - a);
        }

        public void SetPeak(float f, float q, float gainDb)
        {
            Coeffs(out _, out var c, out var a, f, q);
            float A = (float)Math.Pow(10, gainDb / 40f);
            Set(1 + a * A, -2 * c, 1 - a * A, 1 + a / A, -2 * c, 1 - a / A);
        }

        public float Process(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            if (float.IsNaN(y) || float.IsInfinity(y)) y = 0;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    /// <summary>Schroeder reverb: 4 parallel combs into 2 series all-passes.</summary>
    public sealed class Reverb
    {
        readonly float[][] comb;
        readonly int[] combIdx;
        readonly float[] combStore;
        readonly float[][] ap;
        readonly int[] apIdx;
        readonly float feedback, damp;

        public Reverb(int rate, float room = 0.7f, float damping = 0.3f)
        {
            float scale = rate / 44100f;
            int[] combLen = { 1116, 1188, 1277, 1356 };
            int[] apLen = { 556, 441 };
            comb = new float[4][];
            combIdx = new int[4];
            combStore = new float[4];
            for (int i = 0; i < 4; i++) comb[i] = new float[(int)(combLen[i] * scale * (0.6f + room * 0.8f))];
            ap = new float[2][];
            apIdx = new int[2];
            for (int i = 0; i < 2; i++) ap[i] = new float[(int)(apLen[i] * scale)];
            feedback = 0.7f + room * 0.26f;
            damp = damping;
        }

        public float Process(float x)
        {
            float o = 0;
            for (int i = 0; i < 4; i++)
            {
                var buf = comb[i];
                float y = buf[combIdx[i]];
                combStore[i] = y * (1 - damp) + combStore[i] * damp;
                buf[combIdx[i]] = x + combStore[i] * feedback;
                combIdx[i] = (combIdx[i] + 1) % buf.Length;
                o += y;
            }
            o *= 0.25f;
            for (int i = 0; i < 2; i++)
            {
                var buf = ap[i];
                float b = buf[apIdx[i]];
                buf[apIdx[i]] = o + b * 0.5f;
                o = b - o;
                apIdx[i] = (apIdx[i] + 1) % buf.Length;
            }
            return o;
        }
    }

    /// <summary>One-pole low-pass for smoothing control signals / cheap filtering.</summary>
    public struct OnePole
    {
        float y, a;
        public OnePole(int rate, float cutoff) { y = 0; a = 1f - (float)Math.Exp(-2 * Math.PI * cutoff / rate); }
        public float Process(float x) { y += a * (x - y); return y; }
    }

    public static class Env
    {
        public static float Exp(float t, float tau) => t < 0 ? 0 : (float)Math.Exp(-t / Math.Max(tau, 1e-5f));
        public static float AD(float t, float attack, float decayTau)
            => t < 0 ? 0 : t < attack ? t / Math.Max(attack, 1e-5f) : (float)Math.Exp(-(t - attack) / Math.Max(decayTau, 1e-5f));
        public static float AR(float t, float attack, float hold, float release)
        {
            if (t < 0) return 0;
            if (t < attack) return t / Math.Max(attack, 1e-5f);
            if (t < attack + hold) return 1;
            return Math.Max(0, 1 - (t - attack - hold) / Math.Max(release, 1e-5f));
        }
        public static float Smooth(float t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }
    }
}
