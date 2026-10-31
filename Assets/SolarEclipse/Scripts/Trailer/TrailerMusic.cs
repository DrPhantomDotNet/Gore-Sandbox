using System;
using SolarEclipse.Audio;

namespace SolarEclipse
{
    /// <summary>
    /// The trailer track: 2:00 of electronic music at 128 BPM in A minor (Am - F - C - G), made in code.
    /// Bars (1.875 s each): 0-5 drone + heartbeat, 5-10 riser into the logo hit, 10-17 the beat, 17-31 the build,
    /// 31-32 silence, 32-43 drop 1, 43-55 drop 2 (harder, sirens), 55-59 stutter, 59-64 outro.
    /// Generated on a worker thread (no Unity calls).
    /// </summary>
    public static class TrailerMusic
    {
        public const int Rate = 32000;
        public const float Bpm = 128f, Beat = 60f / Bpm, Bar = Beat * 4f, Length = 120f;
        public static float BarTime(float bar) => bar * Bar;

        static readonly float[][] Chords =
        {
            new[] { 220f, 261.63f, 329.63f },   // Am
            new[] { 174.61f, 220f, 261.63f },   // F
            new[] { 261.63f, 329.63f, 392f },   // C
            new[] { 196f, 246.94f, 293.66f },   // G
        };
        static readonly float[] Roots = { 55f, 43.65f, 65.41f, 49f };

        enum Sec { Intro, Riser, Beat, Build, Silence, Drop1, Drop2, Stutter, Outro }
        static Sec SectionOf(int bar) =>
            bar < 5 ? Sec.Intro : bar < 10 ? Sec.Riser : bar < 17 ? Sec.Beat : bar < 31 ? Sec.Build : bar < 32 ? Sec.Silence :
            bar < 43 ? Sec.Drop1 : bar < 55 ? Sec.Drop2 : bar < 59 ? Sec.Stutter : Sec.Outro;

        public static float[] Generate(Action<float> progress = null)
        {
            int n = (int)(Length * Rate);
            var drums = new float[n];
            var music = new float[n];
            var fx = new float[n];
            var rng = new FastRng(1337);

            var kick = Kick(rng); var clap = Clap(rng); var hat = Hat(rng, 0.045f); var openHat = Hat(rng, 0.22f);
            var heart = Heartbeat(); var boom = Boom(rng); var snare = Snare(rng);
            int samplesPerBeat = (int)(Beat * Rate);
            int bars = (int)(Length / Bar) + 1;

            // ---------------- drums
            for (int bar = 0; bar < bars; bar++)
            {
                var sec = SectionOf(bar);
                for (int s16 = 0; s16 < 16; s16++)
                {
                    int at = (int)((bar * Bar + s16 * Beat / 4f) * Rate);
                    if (at >= n) break;
                    bool beat = s16 % 4 == 0;
                    switch (sec)
                    {
                        case Sec.Beat:
                            if (beat) Mix(drums, kick, at, 0.95f);
                            if (s16 % 4 == 2) Mix(drums, hat, at, 0.35f);
                            if (bar >= 14 && (s16 == 4 || s16 == 12)) Mix(drums, clap, at, 0.45f);
                            break;
                        case Sec.Build:
                        {
                            bool roll = bar >= 29;
                            if (beat && !(bar == 30 && s16 >= 8)) Mix(drums, kick, at, 1f);
                            if (s16 == 4 || s16 == 12) Mix(drums, clap, at, 0.6f);
                            Mix(drums, hat, at, s16 % 4 == 2 ? 0.4f : 0.18f);
                            if (roll)
                            {
                                int div = bar == 29 ? 2 : 1; // 8ths then 16ths
                                if (s16 % div == 0) Mix(drums, snare, at, 0.25f + 0.5f * ((bar - 29) * 16 + s16) / 32f);
                            }
                            break;
                        }
                        case Sec.Drop1:
                        case Sec.Drop2:
                        {
                            if (beat) Mix(drums, kick, at, 1.15f);
                            if (s16 == 4 || s16 == 12) Mix(drums, clap, at, 0.75f);
                            if (s16 % 4 == 2) Mix(drums, openHat, at, 0.35f);
                            else Mix(drums, hat, at, sec == Sec.Drop2 ? 0.3f : 0.2f);
                            if (sec == Sec.Drop2 && (s16 == 14 || s16 == 15) && bar % 2 == 1) Mix(drums, snare, at, 0.45f);
                            break;
                        }
                        case Sec.Stutter:
                            if (beat || (bar % 2 == 1 && s16 >= 12)) Mix(drums, kick, at, 1.1f);
                            if (s16 == 4 || s16 == 12) Mix(drums, clap, at, 0.7f);
                            if (s16 % 2 == 0) Mix(drums, hat, at, 0.25f);
                            break;
                    }
                }
            }
            progress?.Invoke(0.15f);

            // Heartbeats + impacts (by time)
            foreach (var t in new[] { 0.8f, 2.6f, 4.4f, 6.2f, 8.0f, 58.6f, 117.2f }) Mix(fx, heart, (int)(t * Rate), 0.9f);
            foreach (var t in new[] { BarTime(10), BarTime(32), BarTime(43) }) Mix(fx, boom, (int)(t * Rate), 1.1f);
            Mix(fx, boom, (int)(BarTime(59) * Rate), 0.6f);

            // ---------------- drone (intro, outro)
            {
                float ph1 = 0, ph2 = 0, ph3 = 0; var lp = new OnePole(); float cutoff;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    int bar = (int)(t / Bar);
                    var sec = SectionOf(bar);
                    float g = sec == Sec.Intro ? Math.Min(1f, t / 4f) : sec == Sec.Riser ? 1f : sec == Sec.Outro ? Math.Min(1f, (t - BarTime(59)) / 2f) * Math.Max(0f, (Length - t) / 6f) : 0f;
                    if (g <= 0f) { lp.Reset(); continue; }
                    ph1 += 55f / Rate; ph2 += 55.4f / Rate; ph3 += 27.5f / Rate;
                    float v = Saw(ph1) + Saw(ph2) + (float)Math.Sin(ph3 * 6.2832f) * 1.2f;
                    cutoff = 250f + 120f * (float)Math.Sin(t * 0.6f);
                    lp.Set(cutoff, Rate);
                    music[i] += lp.Process(v) * 0.11f * g;
                }
            }
            progress?.Invoke(0.25f);

            // ---------------- riser into the logo (bars 5-10) and into the drops (bars 29-31, 41-43)
            Riser(fx, rng, BarTime(5), BarTime(10), 0.7f);
            Riser(fx, rng, BarTime(28), BarTime(31), 0.6f);
            Riser(fx, rng, BarTime(41), BarTime(43), 0.5f);
            Riser(fx, rng, 110.4f, 111.6f, 0.4f); // the bullet whoosh at the end

            // ---------------- bass, pads, arp
            var bassLp = new SvfLow(); var padLp = new SvfLow(); var leadLp = new SvfLow();
            float bp1 = 0, bp2 = 0, sub = 0;
            var padPh = new float[15];
            float leadPh = 0;
            var delay = new float[(int)(Beat * 0.75f * Rate)];
            int dIdx = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int bar = (int)(t / Bar);
                var sec = SectionOf(bar);
                int chord = bar % 4;
                float inBar = t - bar * Bar;
                int s16 = (int)(inBar / (Beat / 4f));
                float in16 = inBar - s16 * Beat / 4f;
                float in8 = inBar % (Beat / 2f);
                float m = 0f;

                // Bass
                bool bassOn = sec == Sec.Beat || sec == Sec.Build || sec == Sec.Drop1 || sec == Sec.Drop2 || sec == Sec.Stutter;
                if (bassOn)
                {
                    float f = Roots[chord] * (sec == Sec.Drop2 && s16 % 4 == 3 ? 2f : 1f);
                    bp1 += f / Rate; bp2 += f * 1.007f / Rate; sub += f / Rate;
                    float env = sec == Sec.Beat || sec == Sec.Build ? Env(in8, 0.004f, 0.18f) : sec == Sec.Drop2 ? Env(in16, 0.002f, 0.09f) * 1.1f : Env(in8, 0.003f, 0.22f);
                    float raw = Saw(bp1) + Saw(bp2);
                    float cut = sec == Sec.Beat ? 400f : sec == Sec.Build ? 400f + 1400f * Prog(t, BarTime(17), BarTime(31)) : sec == Sec.Drop2 ? 2200f : 1500f;
                    bassLp.Set(cut, 1.2f, Rate);
                    float b = bassLp.Process(raw) * 0.5f + (float)Math.Sin(sub * 6.2832f) * 0.6f;
                    if (sec == Sec.Drop1 || sec == Sec.Drop2 || sec == Sec.Stutter) b = (float)Math.Tanh(b * (sec == Sec.Drop2 ? 3.2f : 2.2f)) * 0.8f;
                    m += b * env * 0.55f;
                }

                // Pads (supersaw chords)
                bool padOn = sec == Sec.Beat || sec == Sec.Build || sec == Sec.Drop1 || sec == Sec.Drop2 || sec == Sec.Stutter || sec == Sec.Riser;
                if (padOn)
                {
                    float p = 0f;
                    var ch = Chords[chord];
                    for (int k = 0; k < 3; k++)
                        for (int d = 0; d < 5; d++)
                        {
                            int idx = k * 5 + d;
                            padPh[idx] += ch[k] * (1f + (d - 2) * 0.006f) / Rate;
                            p += Saw(padPh[idx]);
                        }
                    float cut = sec == Sec.Riser ? 300f + 900f * Prog(t, BarTime(5), BarTime(10)) : sec == Sec.Beat ? 900f : sec == Sec.Build ? 900f + 2600f * Prog(t, BarTime(17), BarTime(31)) : 5000f;
                    padLp.Set(cut, 0.8f, Rate);
                    float lvl = sec == Sec.Drop1 || sec == Sec.Drop2 ? 0.07f : sec == Sec.Stutter ? 0.06f : 0.045f;
                    float attack = Math.Min(1f, inBar / 0.08f);
                    m += padLp.Process(p) * lvl * attack;
                }

                // Arp lead (16ths through the chord, an octave up)
                bool leadOn = (sec == Sec.Build && bar >= 21) || sec == Sec.Drop1 || sec == Sec.Drop2;
                float lead = 0f;
                if (leadOn)
                {
                    var ch = Chords[chord];
                    int[] pattern = { 0, 1, 2, 1, 0, 2, 1, 2 };
                    float f = ch[pattern[s16 % 8]] * (sec == Sec.Drop2 && s16 % 8 >= 4 ? 4f : 2f);
                    leadPh += f / Rate;
                    float sq = (leadPh % 1f) < 0.5f ? 1f : -1f;
                    leadLp.Set(sec == Sec.Build ? 1500f + 2500f * Prog(t, BarTime(21), BarTime(31)) : 4200f, 1.4f, Rate);
                    lead = leadLp.Process(sq) * Env(in16, 0.002f, 0.07f) * (sec == Sec.Build ? 0.12f : 0.15f);
                }
                // Echo on the lead (3/16 delay)
                float echo = delay[dIdx];
                delay[dIdx] = lead + echo * 0.38f;
                dIdx = (dIdx + 1) % delay.Length;
                m += lead + echo * 0.4f;

                // Drop 2: siren synth every 2 bars
                if (sec == Sec.Drop2 && bar % 2 == 0)
                {
                    float sf = 650f + 450f * (float)Math.Sin(inBar * 3.1416f * 1.5f);
                    fx[i] += (float)Math.Sin(t * sf * 6.2832f) * 0.06f * Math.Min(1f, inBar / 0.2f);
                }

                // Stutter: gate the music on a 16th pattern
                if (sec == Sec.Stutter)
                {
                    int[] gates = { 1, 0, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 0, 0, 1, 1 };
                    if (gates[(s16 + bar * 3) % 16] == 0) m *= 0.08f;
                }
                music[i] += m;
                if ((i & 0xFFFF) == 0) progress?.Invoke(0.3f + 0.55f * i / n);
            }

            // ---------------- sidechain (music ducks under every kick)
            for (int bar = 0; bar < bars; bar++)
            {
                var sec = SectionOf(bar);
                if (sec != Sec.Beat && sec != Sec.Build && sec != Sec.Drop1 && sec != Sec.Drop2 && sec != Sec.Stutter) continue;
                for (int b = 0; b < 4; b++)
                {
                    int at = (int)((bar * Bar + b * Beat) * Rate);
                    for (int k = 0; k < samplesPerBeat && at + k < n; k++)
                    {
                        float x = k / (float)samplesPerBeat;
                        music[at + k] *= 0.25f + 0.75f * (float)Math.Min(1.0, x * 2.6);
                    }
                }
            }

            // ---------------- mix + master
            var outBuf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                var sec = SectionOf((int)(t / Bar));
                // Section loudness: quiet intro, growing build, the drops hit hardest.
                float g = sec == Sec.Beat ? 0.62f : sec == Sec.Build ? 0.68f + 0.3f * Prog(t, BarTime(17), BarTime(31)) : sec == Sec.Drop1 ? 1.15f : sec == Sec.Drop2 ? 1.3f : sec == Sec.Stutter ? 1.2f : 0.8f;
                float v = (drums[i] + music[i]) * g + fx[i];
                if (sec == Sec.Silence) v = fx[i]; // only the heartbeat
                outBuf[i] = (float)Math.Tanh(v * 1.25f);
            }
            SynthKit.Normalize(outBuf, 0.92f);
            progress?.Invoke(1f);
            return outBuf;
        }

        // ------------------------------------------------------------------ instruments

        static float Saw(float ph) => 2f * (ph - (float)Math.Floor(ph)) - 1f;
        static float Env(float t, float a, float d) => t < a ? t / a : (float)Math.Exp(-(t - a) / d);
        static float Prog(float t, float a, float b) => Math.Max(0f, Math.Min(1f, (t - a) / (b - a)));

        static void Mix(float[] dst, float[] src, int at, float g)
        {
            for (int i = 0; i < src.Length && at + i < dst.Length; i++) if (at + i >= 0) dst[at + i] += src[i] * g;
        }

        static float[] Kick(FastRng r)
        {
            var b = new float[(int)(0.42f * Rate)];
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 45f + 120f * (float)Math.Exp(-t * 32f);
                ph += f / Rate;
                float v = (float)Math.Sin(ph * 6.2832f) * (float)Math.Exp(-t * 6.5f);
                if (t < 0.006f) v += r.Bipolar() * 0.5f * (1f - t / 0.006f);
                b[i] = (float)Math.Tanh(v * 1.8f);
            }
            return b;
        }

        static float[] Clap(FastRng r)
        {
            var b = new float[(int)(0.3f * Rate)];
            var bp = Biquad.BandPass(Rate, 1400f, 0.9f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float e = 0f;
                foreach (var o in new[] { 0f, 0.011f, 0.022f }) if (t >= o) e = Math.Max(e, (float)Math.Exp(-(t - o) * (o < 0.02f ? 90f : 14f)));
                b[i] = bp.Process(r.Bipolar()) * e * 2.2f;
            }
            return b;
        }

        static float[] Snare(FastRng r)
        {
            var b = new float[(int)(0.18f * Rate)];
            var hp = Biquad.HighPass(Rate, 900f);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += 190f / Rate;
                b[i] = (hp.Process(r.Bipolar()) * 0.8f + (float)Math.Sin(ph * 6.2832f) * 0.5f) * (float)Math.Exp(-t * 22f);
            }
            return b;
        }

        static float[] Hat(FastRng r, float len)
        {
            var b = new float[(int)(len * Rate)];
            var hp = Biquad.HighPass(Rate, 7000f, 0.9f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = hp.Process(r.Bipolar()) * (float)Math.Exp(-t / (len * 0.3f));
            }
            return b;
        }

        static float[] Heartbeat()
        {
            var b = new float[(int)(0.7f * Rate)];
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float v = 0f;
                if (t < 0.25f) v += (float)Math.Sin(t * 52f * 6.2832f) * (float)Math.Exp(-t * 16f);
                if (t > 0.22f) { float u = t - 0.22f; v += 0.75f * (float)Math.Sin(u * 46f * 6.2832f) * (float)Math.Exp(-u * 14f); }
                b[i] = (float)Math.Tanh(v * 2.2f);
            }
            return b;
        }

        static float[] Boom(FastRng r)
        {
            var b = new float[(int)(3.5f * Rate)];
            float ph = 0;
            var lp = Biquad.LowPass(Rate, 3000f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 32f + 70f * (float)Math.Exp(-t * 6f);
                ph += f / Rate;
                float v = (float)Math.Sin(ph * 6.2832f) * (float)Math.Exp(-t * 0.9f) * 1.3f;
                v += lp.Process(r.Bipolar()) * (float)Math.Exp(-t * 2.2f) * 0.7f;
                b[i] = (float)Math.Tanh(v * 1.5f);
            }
            return b;
        }

        static void Riser(float[] dst, FastRng r, float from, float to, float gain)
        {
            int a = (int)(from * Rate), z = Math.Min(dst.Length, (int)(to * Rate));
            var bp = new Biquad(Rate);
            float ph = 0;
            for (int i = a; i < z; i++)
            {
                float k = (i - a) / (float)(z - a);
                if (((i - a) & 63) == 0) bp.SetBandPass(300f + 7000f * k * k, 1.6f);
                ph += (200f + 1600f * k * k) / Rate;
                float v = bp.Process(r.Bipolar()) * 1.4f + (float)Math.Sin(ph * 6.2832f) * 0.25f * k;
                dst[i] += v * k * k * gain;
            }
        }

        sealed class OnePole
        {
            float y, a;
            public void Set(float f, int rate) { a = 1f - (float)Math.Exp(-2.0 * Math.PI * f / rate); }
            public float Process(float x) { y += a * (x - y); return y; }
            public void Reset() { y = 0; }
        }

        /// <summary>Chamberlin state-variable low-pass (cheap, fine for slow cutoff changes).</summary>
        sealed class SvfLow
        {
            float low, band, f, q;
            public void Set(float cutoff, float res, int rate)
            {
                f = 2f * (float)Math.Sin(Math.PI * Math.Min(cutoff, rate * 0.2f) / rate);
                q = 1f / Math.Max(0.5f, res);
            }
            public float Process(float x)
            {
                low += f * band;
                float high = x - low - q * band;
                band += f * high;
                if (float.IsNaN(low) || Math.Abs(low) > 50f) { low = 0; band = 0; }
                return low;
            }
        }
    }
}
