using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Story mode sounds: dialogue blips, the barrier fading, the "MISSION PASSED" / "MISSION FAILED" stings,
    /// Z's rage roar sting and a driving boss-fight loop.</summary>
    public static class RecipesStory
    {
        public static void Define(Action<string, int, Func<FastRng, int, int, float[]>, int> def)
        {
            def("story_talk", 3, (r, s, v) => Talk(r, s, 0.9f + v * 0.1f), 44100);
            def("story_barrier", 1, (r, s, v) => BarrierOff(r, s), 44100);
            def("story_passed", 1, (r, s, v) => Passed(r, s), 44100);
            def("story_failed", 1, (r, s, v) => Failed(r, s), 44100);
            def("story_enraged", 1, (r, s, v) => Enraged(r, s), 44100);
            def("story_fight", 1, (r, s, v) => FightLoop(r, s), 32000);
        }

        static float Tri(float ph) { ph -= (float)Math.Floor(ph); return ph < 0.5f ? ph * 4f - 1f : 3f - ph * 4f; }
        static float Saw(float ph) { ph -= (float)Math.Floor(ph); return ph * 2f - 1f; }
        static float Env1(float t, float a, float d) => t < 0f ? 0f : Env.AD(t, a, d);

        /// <summary>Short low "voice" blip for the typewriter.</summary>
        static float[] Talk(FastRng r, int rate, float pitch)
        {
            var b = Buf(0.07f, rate);
            float f = 180f * pitch;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                b[i] = (Tri(t * f) * 0.7f + Sin(t * f * 2f) * 0.25f) * Env1(t, 0.004f, 0.025f);
            }
            LowPass(b, rate, 1800f);
            Normalize(b, 0.5f);
            return b;
        }

        /// <summary>Glassy shimmer sliding down while the wall dissolves.</summary>
        static float[] BarrierOff(FastRng r, int rate)
        {
            var b = Buf(1.6f, rate);
            float ph1 = 0, ph2 = 0, ph3 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float f = 1400f * (float)Math.Pow(0.25f, t / 1.4f);
                ph1 += f / rate; ph2 += f * 1.5f / rate; ph3 += f * 2.01f / rate;
                float shimmer = 0.6f + 0.4f * Sin(t * 13f);
                b[i] = (Sin(ph1) * 0.5f + Sin(ph2) * 0.3f + Sin(ph3) * 0.2f) * shimmer * Env.AR(t, 0.02f, 0.6f, 0.9f) + r.Bipolar() * 0.05f * Env1(t, 0.01f, 0.3f);
            }
            var o = WithReverb(b, rate, 0.7f, 0.4f, 1.2f);
            Normalize(o, 0.7f);
            return o;
        }

        /// <summary>Triumphant brass-ish major chord stab with a rising swell.</summary>
        static float[] Passed(FastRng r, int rate)
        {
            var b = Buf(3.2f, rate);
            float[] chord1 = { 196f, 246.9f, 293.7f, 392f };   // G major
            float[] chord2 = { 261.6f, 329.6f, 392f, 523.3f }; // C major
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float s = 0f;
                // Two quick pickup hits, then the long chord.
                foreach (var f in chord1) s += (Saw(t * f) * 0.5f + Sin(t * f) * 0.5f) * (Env1(t, 0.005f, 0.08f) + Env1(t - 0.16f, 0.005f, 0.08f)) * 0.5f;
                float tl = t - 0.34f;
                if (tl >= 0f)
                {
                    float swell = Env.AR(tl, 0.03f, 1.6f, 1.1f);
                    float vib = 1f + 0.004f * Sin(tl * 5.5f);
                    foreach (var f in chord2) s += (Saw(tl * f * vib) * 0.45f + Sin(tl * f * 0.5f) * 0.3f) * swell * 0.55f;
                }
                // Timpani-ish boom on the downbeat.
                if (tl >= 0f) s += Sin(tl * (70f - tl * 20f)) * Env1(tl, 0.003f, 0.35f) * 1.2f;
                b[i] = s;
            }
            LowPass(b, rate, 3200f);
            var o = WithReverb(b, rate, 0.75f, 0.3f, 1.4f);
            Normalize(o, 0.85f);
            return o;
        }

        /// <summary>Low descending minor stab: you failed.</summary>
        static float[] Failed(FastRng r, int rate)
        {
            var b = Buf(2.6f, rate);
            float[] chord = { 110f, 130.8f, 164.8f };
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float bend = (float)Math.Pow(0.5f, t / 2.2f);
                float s = 0f;
                foreach (var f in chord) s += Saw(t * f * (0.5f + 0.5f * bend)) * 0.4f;
                ph += 45f * bend / rate;
                s += Sin(ph) * 0.9f;
                b[i] = s * Env.AR(t, 0.01f, 0.9f, 1.4f) + r.Bipolar() * 0.08f * Env1(t, 0.002f, 0.12f);
            }
            LowPass(b, rate, 1400f);
            var o = WithReverb(b, rate, 0.8f, 0.35f, 1.2f);
            Normalize(o, 0.85f);
            return o;
        }

        /// <summary>Distorted roar + hit for "Z is enraged!".</summary>
        static float[] Enraged(FastRng r, int rate)
        {
            var b = Buf(1.8f, rate);
            var bp = Biquad.BandPass(rate, 700f, 1.2f);
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float f = 95f + 40f * Sin(t * 7f) * 0.3f + 30f * (float)Math.Exp(-t * 2f);
                ph += f / rate;
                float growl = Saw(ph) * (0.7f + 0.3f * r.Bipolar());
                float n = bp.Process(r.Bipolar()) * 2f;
                float hit = Sin(t * (60f - t * 15f)) * Env1(t, 0.002f, 0.3f) * 1.5f;
                b[i] = (growl * 0.6f + n * 0.5f) * Env.AR(t, 0.05f, 0.9f, 0.6f) + hit;
            }
            Drive(b, 2.2f);
            LowPass(b, rate, 2600f);
            Normalize(b, 0.85f);
            return b;
        }

        /// <summary>Seamless 8-bar driving loop (kick, snare, bass) at 130 bpm.</summary>
        static float[] FightLoop(FastRng r, int rate)
        {
            const float bpm = 130f;
            float beat = 60f / bpm;
            float len = beat * 16f;
            var b = Buf(len, rate);
            float[] bassNotes = { 55f, 55f, 65.4f, 49f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float bt = t / beat;
                int bi = (int)bt;
                float inBeat = (bt - bi) * beat;
                float s = 0f;
                // Kick on every beat.
                s += Sin(inBeat * (50f + 90f * (float)Math.Exp(-inBeat * 30f))) * Env1(inBeat, 0.001f, 0.16f) * 1.1f;
                // Snare on 2 and 4.
                if (bi % 2 == 1) s += r.Bipolar() * Env1(inBeat, 0.001f, 0.09f) * 0.45f + Sin(inBeat * 190f) * Env1(inBeat, 0.001f, 0.05f) * 0.3f;
                // Hats on the off-beats.
                float off = inBeat - beat * 0.5f;
                if (off >= 0f) s += r.Bipolar() * Env1(off, 0.0005f, 0.02f) * 0.18f;
                // Bass: eighth notes, one note per bar.
                float f = bassNotes[(bi / 4) % bassNotes.Length];
                float eighth = (float)(bt * 2f - Math.Floor(bt * 2f)) * beat * 0.5f;
                s += (Saw(t * f) * 0.5f + Sin(t * f * 0.5f) * 0.4f) * Env.AR(eighth, 0.005f, 0.12f, 0.08f) * 0.55f;
                b[i] = s;
            }
            LowPass(b, rate, 5200f);
            Drive(b, 1.3f);
            Normalize(b, 0.7f);
            return b;
        }
    }
}
