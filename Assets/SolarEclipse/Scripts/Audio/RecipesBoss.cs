using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Sounds for the "????" boss realm: clock ticks, slams, bells, screams, stingers, glitches and the boss music.</summary>
    public static class RecipesBoss
    {
        public static void Define(Action<string, int, Func<FastRng, int, int, float[]>, int> def)
        {
            def("boss_clock", 1, (r, s, v) => Clock(r, s, false), 32000);
            def("boss_clock_fast", 1, (r, s, v) => Clock(r, s, true), 32000);
            def("boss_slam", 3, (r, s, v) => Slam(r, s, v), 44100);
            def("boss_bell", 1, (r, s, v) => Bell(r, s), 44100);
            def("boss_scream", 2, (r, s, v) => Scream(r, s, 2.2f, v), 44100);
            def("boss_jumpscare", 2, (r, s, v) => Jumpscare(r, s, v), 44100);
            def("boss_orbie_scare", 1, (r, s, v) => OrbieScare(r, s), 44100);
            def("boss_music", 1, (r, s, v) => Music(r, s), 32000);
            def("boss_light", 1, (r, s, v) => LightRealm(r, s), 32000);
            def("boss_shimmer", 1, (r, s, v) => Shimmer(r, s), 44100);
            def("boss_glitch", 4, (r, s, v) => Glitch(r, s, 0.5f + v * 0.15f), 44100);
            def("boss_crash_buzz", 1, (r, s, v) => CrashBuzz(r, s), 44100);
            def("orbie_blip", 3, (r, s, v) => OrbieBlip(r, s, 1.0f + v * 0.12f), 44100);
            def("realm_type", 3, (r, s, v) => RealmType(r, s), 44100);
            def("boss_whoosh", 1, (r, s, v) => DarkWhoosh(r, s), 44100);
        }

        // ------------------------------------------------------------------ helpers

        static float Saw(float ph) { ph -= (float)Math.Floor(ph); return ph * 2f - 1f; }
        static float Sq(float ph) { ph -= (float)Math.Floor(ph); return ph < 0.5f ? 1f : -1f; }
        static float Tanh(float x) => (float)Math.Tanh(x);

        static float[] Tick(FastRng r, int rate, float pitch, float gain)
        {
            var b = Buf(0.18f, rate);
            var bp = Biquad.BandPass(rate, 2600f * pitch, 3f);
            var bp2 = Biquad.BandPass(rate, 5200f * pitch, 5f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float n = r.Bipolar();
                float click = bp.Process(n) * Env.AD(t, 0.0003f, 0.006f) * 3f + bp2.Process(n) * Env.AD(t, 0.0002f, 0.003f) * 2f;
                float body = Sin(t * 1180f * pitch) * Env.AD(t, 0.0005f, 0.025f) * 0.35f + Sin(t * 3170f * pitch) * Env.AD(t, 0.0003f, 0.012f) * 0.2f;
                float thump = Sin(t * 70f) * Env.AD(t, 0.002f, 0.05f) * 0.5f;
                b[i] = (click + body + thump) * gain;
            }
            return b;
        }

        static void AddWrap(float[] dst, float[] src, int offset, float gain)
        {
            for (int i = 0; i < src.Length; i++) dst[(offset + i) % dst.Length] += src[i] * gain;
        }

        // ------------------------------------------------------------------ clock

        static float[] Clock(FastRng r, int rate, bool fast)
        {
            float len = fast ? 1f : 2f;
            var b = Buf(len, rate);
            int ticks = fast ? 4 : 2;
            for (int k = 0; k < ticks; k++)
            {
                var t = Tick(r, rate, k % 2 == 0 ? 1f : 0.82f, k % 2 == 0 ? 1f : 0.85f);
                AddWrap(b, t, (int)(k * len / ticks * rate), 1f);
            }
            if (fast)
            {
                // A heartbeat under the fast ticking.
                for (int k = 0; k < 2; k++)
                {
                    int off = (int)(k * 0.5f * rate);
                    var hb = Buf(0.35f, rate);
                    for (int i = 0; i < hb.Length; i++)
                    {
                        float t = i / (float)rate;
                        hb[i] = Sin(t * 48f) * (Env.AD(t, 0.004f, 0.06f) + Env.AD(t - 0.16f, 0.004f, 0.07f) * 0.8f);
                    }
                    AddWrap(b, hb, off, 0.9f);
                }
            }
            // Very quiet room tone so the void isn't digital silence.
            var lp = Biquad.LowPass(rate, 180f);
            for (int i = 0; i < b.Length; i++) b[i] += lp.Process(r.Bipolar()) * 0.06f;
            var wet = WithReverb(b, rate, 0.8f, 0.25f, 0f);
            Array.Resize(ref wet, b.Length);
            Normalize(wet, 0.85f);
            return wet;
        }

        // ------------------------------------------------------------------ cinematic typewriter slam

        static float[] Slam(FastRng r, int rate, int v)
        {
            var b = Buf(2.6f, rate);
            var hp = Biquad.HighPass(rate, 1800f);
            float metal = 1.0f + v * 0.07f;
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                // Type bar strike.
                float clack = hp.Process(r.Bipolar()) * Env.AD(t, 0.0002f, 0.012f) * 1.6f;
                float ring = (Sin(t * 1730f * metal) * 0.5f + Sin(t * 2890f * metal) * 0.35f + Sin(t * 4410f * metal) * 0.25f) * Env.AD(t, 0.0005f, 0.09f);
                // Sub boom with pitch drop.
                float f = 38f + 70f * (float)Math.Exp(-t / 0.08f);
                ph += f / rate;
                float boom = Sin(ph) * Env.AD(t, 0.002f, 0.7f) * 1.4f;
                // Low noise impact body.
                float body = r.Bipolar() * Env.AD(t, 0.001f, 0.06f) * 0.6f;
                b[i] = Tanh((clack + ring + boom + body) * 1.6f);
            }
            var low = Biquad.LowPass(rate, 9000f);
            for (int i = 0; i < b.Length; i++) b[i] = low.Process(b[i]);
            var w = WithReverb(b, rate, 0.92f, 0.4f, 1.5f);
            Normalize(w, 0.95f);
            return w;
        }

        // ------------------------------------------------------------------ bell toll

        static float[] Bell(FastRng r, int rate)
        {
            float f0 = 98f;
            var partials = new[] { 0.5f, 1f, 1.19f, 1.56f, 2f, 2.51f, 2.66f, 3.01f, 4.1f };
            var decays = new[] { 6f, 5f, 3.5f, 3f, 2.4f, 1.6f, 1.3f, 1f, 0.6f };
            var amps = new[] { 0.6f, 1f, 0.7f, 0.5f, 0.6f, 0.35f, 0.3f, 0.25f, 0.18f };
            var b = Buf(7f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float s = 0;
                for (int k = 0; k < partials.Length; k++)
                {
                    float beat = 1f + 0.25f * Sin(t * (0.7f + k * 0.31f));
                    s += Sin(t * f0 * partials[k] * (1f + k * 0.0007f)) * amps[k] * Env.AD(t, 0.003f, decays[k]) * beat;
                }
                s += r.Bipolar() * Env.AD(t, 0.0005f, 0.01f) * 0.5f;
                b[i] = s;
            }
            var w = WithReverb(b, rate, 0.95f, 0.45f, 2f);
            Normalize(w, 0.95f);
            return w;
        }

        // ------------------------------------------------------------------ scream

        public static float[] Scream(FastRng r, int rate, float seconds, int v)
        {
            var b = Buf(seconds, rate);
            float[] ph = new float[5];
            float[] det = { 1f, 1.013f, 0.987f, 1.5f, 0.5f };
            var bp1 = Biquad.BandPass(rate, 900f, 1.5f);
            var bp2 = Biquad.BandPass(rate, 2600f, 2f);
            var bp3 = Biquad.BandPass(rate, 3900f, 3f);
            float baseF = v == 0 ? 520f : 430f;
            float fm = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float u = t / seconds;
                float pitch = baseF * (0.7f + 0.6f * (float)Math.Sin(Math.Min(1f, u * 3f) * Math.PI * 0.5f)) * (1f - 0.35f * u * u);
                pitch *= 1f + 0.03f * Sin(t * 7.3f) + 0.02f * r.Bipolar();
                fm += 133f / rate;
                float s = 0;
                for (int k = 0; k < ph.Length; k++)
                {
                    ph[k] += pitch * det[k] / rate;
                    s += Saw(ph[k] + 0.3f * Sin(fm)) * (k < 3 ? 0.5f : 0.25f);
                }
                s += r.Bipolar() * 0.7f;
                float f = bp1.Process(s) * 1.2f + bp2.Process(s) * 1.0f + bp3.Process(s) * 0.6f;
                float amp = Env.AR(t, 0.04f, seconds * 0.55f, seconds * 0.4f);
                b[i] = Tanh(f * 3f) * amp;
            }
            var w = WithReverb(b, rate, 0.8f, 0.3f, 0.8f);
            Normalize(w, 0.95f);
            return w;
        }

        // ------------------------------------------------------------------ jumpscare stinger

        static float[] Jumpscare(FastRng r, int rate, int v)
        {
            var b = Buf(2.4f, rate);
            float[] freqs = { 233f, 247f, 262f, 349f, 370f, 494f, 523f };
            float[] ph = new float[freqs.Length];
            float sub = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float s = 0;
                float glide = 1f + 0.6f * (float)Math.Exp(-t / 0.15f) + (v == 1 ? -0.1f * t : 0.05f * t);
                for (int k = 0; k < freqs.Length; k++)
                {
                    ph[k] += freqs[k] * glide / rate;
                    s += Saw(ph[k]) * 0.3f;
                }
                float noise = r.Bipolar() * (Env.AD(t, 0.001f, 0.25f) * 1.5f + 0.15f);
                sub += (55f + 120f * (float)Math.Exp(-t / 0.1f)) / rate;
                float boom = Sin(sub) * Env.AD(t, 0.001f, 0.8f) * 1.5f;
                float amp = Env.AR(t, 0.002f, 1.2f, 1.0f);
                b[i] = Tanh((s + noise + boom) * 2.5f) * amp;
            }
            var scream = Scream(r, rate, 1.8f, v);
            Add(b, scream, (int)(0.03f * rate), 0.9f);
            var w = WithReverb(b, rate, 0.85f, 0.3f, 1f);
            Normalize(w, 0.98f);
            return w;
        }

        static float[] OrbieScare(FastRng r, int rate)
        {
            var b = Jumpscare(r, rate, 0);
            var bell = Slam(r, rate, 2);
            Add(b, bell, 0, 0.8f);
            var scream = Scream(r, rate, 2.2f, 1);
            Add(b, scream, (int)(0.1f * rate), 0.7f);
            for (int i = 0; i < b.Length; i++) b[i] = Tanh(b[i] * 1.8f);
            Normalize(b, 0.98f);
            return b;
        }

        // ------------------------------------------------------------------ boss music (seamless loop)

        static float Mtof(float m) => 440f * (float)Math.Pow(2, (m - 69) / 12f);

        static float[] Music(FastRng r, int rate)
        {
            const float bpm = 132f;
            float beat = 60f / bpm;
            int bars = 8;
            float len = bars * 4 * beat;
            var b = Buf(len, rate);
            int N = b.Length;

            // Drone: detuned saws on E1 and F1 (a semitone rub), slowly breathing filter.
            {
                var lp = Biquad.LowPass(rate, 200f, 1.2f);
                float p1 = 0, p2 = 0, p3 = 0;
                for (int i = 0; i < N; i++)
                {
                    float t = i / (float)rate;
                    float u = t / len;
                    p1 += Mtof(28) / rate; p2 += Mtof(28) * 1.006f / rate; p3 += Mtof(29) / rate;
                    float s = Saw(p1) + Saw(p2) + Saw(p3) * (0.4f + 0.4f * Sin(u * 2f));
                    if (i % 64 == 0) lp.SetLowPass(160f + 260f * (0.5f + 0.5f * Sin(u * 4f)), 1.6f);
                    b[i] += lp.Process(s) * 0.32f;
                }
            }

            // Pulsing bass ostinato (8ths) with accent pattern.
            int[] bassNotes = { 28, 28, 40, 28, 28, 31, 28, 29 };
            for (int bar = 0; bar < bars; bar++)
                for (int k = 0; k < 8; k++)
                {
                    float start = (bar * 4 + k * 0.5f) * beat;
                    int note = bassNotes[k] + (bar % 4 == 3 && k >= 4 ? 1 : 0);
                    var n = Buf(beat * 0.48f, rate);
                    var lp = Biquad.LowPass(rate, 900f, 2f);
                    float ph = 0;
                    for (int i = 0; i < n.Length; i++)
                    {
                        float t = i / (float)rate;
                        ph += Mtof(note) / rate;
                        if (i % 32 == 0) lp.SetLowPass(250f + 1400f * (float)Math.Exp(-t / 0.05f), 3f);
                        n[i] = Tanh(lp.Process(Saw(ph) + Sq(ph * 0.5f) * 0.5f) * 2f) * Env.AR(t, 0.003f, beat * 0.3f, 0.04f) * (k % 4 == 0 ? 1f : 0.7f);
                    }
                    AddWrap(b, n, (int)(start * rate), 0.35f);
                }

            // Kick on every beat, extra kicks for drive.
            for (int bar = 0; bar < bars; bar++)
                for (int k = 0; k < 4; k++)
                {
                    var kick = Kick(r, rate);
                    AddWrap(b, kick, (int)((bar * 4 + k) * beat * rate), 0.9f);
                    if (k == 3 && bar % 2 == 1) AddWrap(b, kick, (int)((bar * 4 + k + 0.5f) * beat * rate), 0.6f);
                }

            // Metallic hats (16ths) and a snare/clang on 2 and 4.
            for (int s16 = 0; s16 < bars * 16; s16++)
            {
                var hat = Buf(0.06f, rate);
                var hp = Biquad.HighPass(rate, 7000f);
                for (int i = 0; i < hat.Length; i++) hat[i] = hp.Process(r.Bipolar()) * Env.AD(i / (float)rate, 0.0005f, s16 % 4 == 2 ? 0.03f : 0.012f);
                AddWrap(b, hat, (int)(s16 * beat * 0.25f * rate), s16 % 2 == 0 ? 0.16f : 0.09f);
                if (s16 % 8 == 4)
                {
                    var sn = Buf(0.4f, rate);
                    var bp = Biquad.BandPass(rate, 1900f, 0.8f);
                    for (int i = 0; i < sn.Length; i++)
                    {
                        float t = i / (float)rate;
                        sn[i] = bp.Process(r.Bipolar()) * Env.AD(t, 0.001f, 0.09f) * 2.2f + (Sin(t * 185f) + Sin(t * 571f) * 0.6f + Sin(t * 1347f) * 0.4f) * Env.AD(t, 0.001f, 0.12f) * 0.5f;
                    }
                    AddWrap(b, sn, (int)(s16 * beat * 0.25f * rate), 0.45f);
                }
            }

            // Dissonant brass-like stabs: E + F + Bb + B cluster on bar 1 beat 1 and the "and" of 3.
            int[] cluster = { 52, 53, 58, 59, 64 };
            for (int bar = 0; bar < bars; bar++)
            {
                float[] at = bar % 2 == 0 ? new[] { 0f, 2.5f } : new[] { 0f, 1.5f, 3.5f };
                foreach (var a in at)
                {
                    var st = Buf(0.9f, rate);
                    var lp = Biquad.LowPass(rate, 3000f, 1.5f);
                    float[] ph = new float[cluster.Length];
                    for (int i = 0; i < st.Length; i++)
                    {
                        float t = i / (float)rate;
                        float s = 0;
                        for (int k = 0; k < cluster.Length; k++) { ph[k] += Mtof(cluster[k] - (bar >= 4 ? 1 : 0)) * (1f + 0.004f * k) / rate; s += Saw(ph[k]); }
                        if (i % 32 == 0) lp.SetLowPass(400f + 3500f * (float)Math.Exp(-t / 0.12f), 2f);
                        st[i] = Tanh(lp.Process(s) * 0.8f) * Env.AR(t, 0.005f, 0.12f, 0.35f);
                    }
                    AddWrap(b, st, (int)((bar * 4 + a) * beat * rate), 0.28f);
                }
            }

            // High eerie whine that rises across the loop and resolves back at the seam.
            {
                float ph = 0;
                for (int i = 0; i < N; i++)
                {
                    float u = i / (float)N;
                    ph += Mtof(88 + 3f * u) / rate;
                    b[i] += Sin(ph) * 0.02f * Sin(u * 0.5f) * (1f + Sin(u * 64f) * 0.5f);
                }
            }

            for (int i = 0; i < N; i++) b[i] = Tanh(b[i] * 1.3f);
            Normalize(b, 0.88f);
            return b;
        }

        static float[] Kick(FastRng r, int rate)
        {
            var b = Buf(0.45f, rate);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                ph += (45f + 140f * (float)Math.Exp(-t / 0.03f)) / rate;
                b[i] = Tanh((Sin(ph) * Env.AD(t, 0.001f, 0.18f) + r.Bipolar() * Env.AD(t, 0.0003f, 0.004f) * 0.5f) * 2f);
            }
            return b;
        }

        // ------------------------------------------------------------------ light realm

        static float[] LightRealm(FastRng r, int rate)
        {
            float len = 8f;
            var b = Buf(len, rate);
            float[] notes = { 76, 79, 83, 88, 91, 95 };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float u = t / len;
                float s = 0;
                for (int k = 0; k < notes.Length; k++)
                {
                    float trem = 0.6f + 0.4f * Sin(u * (k + 1) + k * 0.17f);
                    s += Sin(t * Mtof(notes[k])) * trem * (1f / (k + 1.5f));
                }
                s += Sin(t * Mtof(52)) * 0.25f;
                b[i] = s;
            }
            var w = WithReverb(b, rate, 0.9f, 0.5f, 0f);
            Array.Resize(ref w, b.Length);
            Fade(w, rate, 0.05f, 0.05f);
            Normalize(w, 0.7f);
            return w;
        }

        static float[] Shimmer(FastRng r, int rate)
        {
            var b = Buf(5f, rate);
            for (int k = 0; k < 40; k++)
            {
                float start = r.Range(0f, 2.5f);
                float f = Mtof(84 + new[] { 0, 4, 7, 11, 12, 16 }[r.RangeInt(0, 6)]);
                var n = Buf(2f, rate);
                for (int i = 0; i < n.Length; i++) { float t = i / (float)rate; n[i] = Sin(t * f) * Env.AD(t, 0.01f, 0.6f); }
                Add(b, n, (int)(start * rate), 0.2f);
            }
            float sweep = 0;
            var hp = Biquad.HighPass(rate, 3000f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                sweep += (200f + 1800f * t / 5f) / rate;
                b[i] += hp.Process(r.Bipolar()) * Env.AR(t, 1.5f, 0.5f, 2f) * 0.25f + Sin(sweep) * 0.15f * Env.AR(t, 0.5f, 1.5f, 2.5f);
            }
            var w = WithReverb(b, rate, 0.95f, 0.5f, 2f);
            Normalize(w, 0.85f);
            return w;
        }

        // ------------------------------------------------------------------ glitches

        static float[] Glitch(FastRng r, int rate, float seconds)
        {
            var b = Buf(seconds, rate);
            int i = 0;
            while (i < b.Length)
            {
                int seg = (int)(r.Range(0.008f, 0.07f) * rate);
                int type = r.RangeInt(0, 4);
                float f = r.Range(80f, 3000f);
                float crush = r.Range(4f, 24f);
                float hold = 0; int holdN = 0, holdLen = r.RangeInt(2, 40);
                for (int k = 0; k < seg && i < b.Length; k++, i++)
                {
                    float t = k / (float)rate;
                    float s;
                    switch (type)
                    {
                        case 0: s = Sq(t * f); break;
                        case 1: s = r.Bipolar(); break;
                        case 2: s = Saw(t * f) * Sq(t * 37f); break;
                        default: s = 0f; break;
                    }
                    if (holdN-- <= 0) { hold = s; holdN = holdLen; }
                    s = (float)Math.Round(hold * crush) / crush;
                    b[i] = s * 0.6f;
                }
            }
            Fade(b, rate, 0.002f, 0.02f);
            Normalize(b, 0.8f);
            return b;
        }

        static float[] CrashBuzz(FastRng r, int rate)
        {
            // A stuck audio buffer: one 35 ms grain repeated.
            var grain = Buf(0.035f, rate);
            for (int i = 0; i < grain.Length; i++) { float t = i / (float)rate; grain[i] = Sq(t * 220f) * 0.5f + Saw(t * 333f) * 0.3f + r.Bipolar() * 0.2f; }
            var b = Buf(2.5f, rate);
            for (int i = 0; i < b.Length; i++) b[i] = grain[i % grain.Length];
            Fade(b, rate, 0.001f, 0.01f);
            Normalize(b, 0.55f);
            return b;
        }

        // ------------------------------------------------------------------ voices / typing

        static float[] OrbieBlip(FastRng r, int rate, float pitch)
        {
            var b = Buf(0.12f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                b[i] = (Sin(t * 1320f * pitch) + Sin(t * 1980f * pitch) * 0.3f + Sin(t * 2640f * pitch) * 0.15f) * Env.AD(t, 0.002f, 0.03f);
            }
            Normalize(b, 0.35f);
            return b;
        }

        static float[] RealmType(FastRng r, int rate)
        {
            var b = Buf(0.25f, rate);
            var bp = Biquad.BandPass(rate, 1400f, 2f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                b[i] = Tanh((bp.Process(r.Bipolar()) * Env.AD(t, 0.0003f, 0.01f) * 3f + Sin(t * 95f) * Env.AD(t, 0.002f, 0.05f)) * 2f);
            }
            var w = WithReverb(b, rate, 0.7f, 0.3f, 0.3f);
            Normalize(w, 0.5f);
            return w;
        }

        static float[] DarkWhoosh(FastRng r, int rate)
        {
            var b = Buf(1.6f, rate);
            var bp = Biquad.BandPass(rate, 300f, 1.2f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                if (i % 32 == 0) bp.SetBandPass(150f + 900f * t / 1.6f, 1.5f);
                b[i] = bp.Process(r.Bipolar()) * Env.AR(t, 1.0f, 0.2f, 0.3f) * 2f + Sin(t * 41f) * Env.AR(t, 1f, 0.2f, 0.3f) * 0.5f;
            }
            Normalize(b, 0.8f);
            return b;
        }
    }
}
