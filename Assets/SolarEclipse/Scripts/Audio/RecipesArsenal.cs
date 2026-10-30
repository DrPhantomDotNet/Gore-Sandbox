using System;
using static SolarEclipse.Audio.SynthKit;
using W = SolarEclipse.Audio.RecipesWeapons;

namespace SolarEclipse.Audio
{
    /// <summary>
    /// Arsenal pack sounds (Weapons/ArsenalPack.cs): a distinct gunshot voice per gun (pistol / heavy pistol / SMG /
    /// rifle / battle rifle / LMG / shotgun / double barrel / .50 cal), lever and break-action handling, melee swings
    /// and impacts (wood, metal, flesh, the frying pan), and a two-stroke chainsaw (start, idle + rev loops, cutting, stop).
    /// </summary>
    public static class RecipesArsenal
    {
        static W.GunVoice V(float bodyCut, float bodyTau, float thumpHz, float thump, float crack, float tailTau, float tailCut, float drive, float room, float wet, float len) =>
            new W.GunVoice { BodyCut = bodyCut, BodyTau = bodyTau, ThumpHz = thumpHz, ThumpAmt = thump, CrackAmt = crack, TailTau = tailTau, TailCut = tailCut, Drive = drive, Room = room, Wet = wet, Length = len };

        // Pistols: snappy 9mm, deep .45 thump, .50 AE blast.
        static readonly W.GunVoice Deagle = V(2300, 0.075f, 62, 1.45f, 0.9f, 0.45f, 1000, 3.6f, 0.8f, 0.45f, 1.2f);
        static readonly W.GunVoice M1911 = V(2000, 0.05f, 78, 1.0f, 0.4f, 0.24f, 1050, 2.6f, 0.6f, 0.38f, 0.65f);
        static readonly W.GunVoice Beretta = V(2700, 0.032f, 100, 0.65f, 0.42f, 0.17f, 1300, 2.2f, 0.55f, 0.34f, 0.5f);
        static readonly W.GunVoice Glock18 = V(2900, 0.02f, 118, 0.5f, 0.4f, 0.08f, 1500, 2.5f, 0.42f, 0.24f, 0.26f);
        static readonly W.GunVoice Tec9 = V(3300, 0.022f, 130, 0.42f, 0.5f, 0.1f, 1700, 2.9f, 0.45f, 0.26f, 0.3f);
        // SMGs
        static readonly W.GunVoice Mp5 = V(2500, 0.022f, 105, 0.5f, 0.28f, 0.11f, 1300, 2.3f, 0.48f, 0.28f, 0.32f);
        static readonly W.GunVoice P90 = V(4200, 0.018f, 125, 0.4f, 0.75f, 0.1f, 1900, 2.6f, 0.45f, 0.26f, 0.3f);
        static readonly W.GunVoice Vector = V(2200, 0.016f, 92, 0.6f, 0.25f, 0.07f, 1200, 2.4f, 0.4f, 0.22f, 0.24f);
        static readonly W.GunVoice Ump = V(1900, 0.03f, 84, 0.8f, 0.3f, 0.14f, 1100, 2.6f, 0.5f, 0.3f, 0.38f);
        static readonly W.GunVoice Thompson = V(1700, 0.035f, 76, 0.95f, 0.3f, 0.18f, 950, 2.9f, 0.6f, 0.36f, 0.45f);
        // Rifles
        static readonly W.GunVoice Ak47 = V(3000, 0.055f, 64, 1.15f, 1.0f, 0.38f, 1250, 3.3f, 0.75f, 0.44f, 0.95f);
        static readonly W.GunVoice M4a1 = V(4100, 0.04f, 76, 0.85f, 1.05f, 0.3f, 1600, 2.9f, 0.7f, 0.4f, 0.8f);
        static readonly W.GunVoice ScarH = V(3200, 0.06f, 58, 1.3f, 1.15f, 0.45f, 1200, 3.5f, 0.8f, 0.46f, 1.1f);
        static readonly W.GunVoice Famas = V(4400, 0.03f, 82, 0.75f, 1.0f, 0.22f, 1700, 2.8f, 0.62f, 0.36f, 0.6f);
        static readonly W.GunVoice G36 = V(3800, 0.038f, 74, 0.85f, 0.95f, 0.28f, 1500, 2.8f, 0.68f, 0.4f, 0.75f);
        static readonly W.GunVoice Lever = V(2100, 0.07f, 60, 1.25f, 0.7f, 0.55f, 950, 3.2f, 0.85f, 0.5f, 1.3f);
        // Shotguns, LMG, .50
        static readonly W.GunVoice DBarrel = V(1400, 0.13f, 42, 1.7f, 0.45f, 0.7f, 800, 4f, 0.88f, 0.5f, 1.6f);
        static readonly W.GunVoice Aa12 = V(1800, 0.06f, 55, 1.2f, 0.35f, 0.25f, 900, 3.3f, 0.6f, 0.34f, 0.55f);
        static readonly W.GunVoice M249 = V(3400, 0.028f, 70, 0.8f, 0.85f, 0.12f, 1400, 2.9f, 0.5f, 0.28f, 0.4f);
        static readonly W.GunVoice Barrett = V(2600, 0.1f, 40, 1.9f, 1.5f, 0.9f, 900, 4.2f, 0.95f, 0.6f, 2.4f);

        public static void Define(Action<string, int, Func<FastRng, int, int, float[]>, int> def)
        {
            void Gun(string name, W.GunVoice g, int variants = 3) => def(name, variants, (r, s, v) => W.Gunshot(r, s, g), 44100);
            Gun("gun_deagle", Deagle, 2);
            Gun("gun_m1911", M1911);
            Gun("gun_beretta", Beretta);
            Gun("gun_glock18", Glock18, 4);
            Gun("gun_tec9", Tec9, 4);
            Gun("gun_mp5", Mp5, 4);
            Gun("gun_p90", P90, 4);
            Gun("gun_vector", Vector, 4);
            Gun("gun_ump", Ump, 4);
            Gun("gun_thompson", Thompson, 4);
            Gun("gun_ak47", Ak47);
            Gun("gun_m4a1", M4a1);
            Gun("gun_scarh", ScarH);
            Gun("gun_famas", Famas, 4);
            Gun("gun_g36", G36);
            Gun("gun_lever", Lever, 2);
            Gun("gun_aa12", Aa12, 3);
            Gun("gun_m249", M249, 4);
            def("gun_dbarrel", 2, (r, s, v) => Boom(r, s, DBarrel, 55f, 0.6f), 44100);
            def("gun_barrett", 2, (r, s, v) => Boom(r, s, Barrett, 38f, 1f), 44100);
            def("lever_action", 2, (r, s, v) => LeverAction(r, s), 44100);
            def("break_open", 2, (r, s, v) => Seq(s, 0.35f, (0f, W.Click(r, s, 0.6f, 0.9f), 1f), (0.03f, Slide(r, s, 0.12f, 900, 500), 0.5f)), 44100);
            def("break_close", 2, (r, s, v) => Seq(s, 0.3f, (0f, Slide(r, s, 0.05f, 500, 900), 0.4f), (0.05f, W.Click(r, s, 0.5f, 1.2f), 1f), (0.06f, W.Click(r, s, 1.1f, 0.3f), 0.5f)), 44100);
            // Melee
            def("melee_swing", 4, (r, s, v) => W.Whoosh(r, s, 0.3f, 420, 1900), 44100);
            def("melee_swing_heavy", 3, (r, s, v) => W.Whoosh(r, s, 0.46f, 200, 1050, 1.2f), 44100);
            def("blunt_flesh", 4, (r, s, v) => BluntFlesh(r, s), 44100);
            def("blunt_world", 3, (r, s, v) => MetalHit(r, s, new[] { 640f, 1510f, 2760f, 4100f }, 0.35f), 44100);
            def("wood_thock", 3, (r, s, v) => WoodThock(r, s), 44100);
            def("pan_bonk", 3, (r, s, v) => MetalHit(r, s, new[] { 410f, 1090f, 1980f, 3150f }, 1f), 44100);
            def("blade_chop", 3, (r, s, v) => BladeChop(r, s), 44100);
            // Chainsaw
            def("chainsaw_idle", 1, (r, s, v) => EngineLoop(r, s, 1f, 44f, 0.15f), 32000);
            def("chainsaw_rev", 1, (r, s, v) => EngineLoop(r, s, 1f, 128f, 1f), 32000);
            def("chainsaw_start", 1, (r, s, v) => SawStart(r, s), 32000);
            def("chainsaw_rev_up", 2, (r, s, v) => Engine(r, s, 0.45f, t => 44f + 84f * Smooth01(t / 0.35f), t => 0.6f + 0.4f * Smooth01(t / 0.2f), 0.7f), 32000);
            def("chainsaw_stop", 1, (r, s, v) => Engine(r, s, 1.1f, t => 20f + 108f * (float)Math.Exp(-t / 0.25f), t => (float)Math.Exp(-t / 0.4f), 0.4f), 32000);
            def("chainsaw_cut", 3, (r, s, v) => SawCut(r, s), 32000);
        }

        static float Smooth01(float t) { t = Math.Max(0f, Math.Min(1f, t)); return t * t * (3f - 2f * t); }

        static float[] Seq(int rate, float length, params (float time, float[] sound, float gain)[] parts)
        {
            var b = Buf(length, rate);
            foreach (var p in parts) Add(b, p.sound, (int)(p.time * rate), p.gain);
            Normalize(b, 0.85f);
            return b;
        }

        static float[] Slide(FastRng r, int rate, float seconds, float f0, float f1)
        {
            var b = Buf(seconds, rate);
            var bp = new Biquad(rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                if ((i & 15) == 0) bp.SetBandPass(f0 + (f1 - f0) * (t / seconds), 2.5f);
                b[i] = bp.Process(r.Bipolar()) * Env.AR(t, 0.01f, seconds * 0.6f, seconds * 0.3f);
            }
            return b;
        }

        /// <summary>A gunshot plus a sub-bass boom (double barrel, .50 BMG).</summary>
        static float[] Boom(FastRng r, int rate, W.GunVoice g, float subHz, float subAmt)
        {
            var shot = W.Gunshot(r, rate, g);
            var sub = Chirp(rate, 0.6f, subHz * 2f, subHz, 0.04f, 0.18f);
            Add(shot, sub, 0, subAmt);
            Normalize(shot, 0.95f);
            return shot;
        }

        static float[] LeverAction(FastRng r, int rate) => Seq(rate, 0.5f,
            (0f, W.Click(r, rate, 0.85f, 0.6f), 0.9f), (0.02f, Slide(r, rate, 0.12f, 1400, 2600), 0.55f), (0.15f, W.Click(r, rate, 1.15f, 0.4f), 0.8f),
            (0.22f, Slide(r, rate, 0.1f, 2600, 1400), 0.5f), (0.32f, W.Click(r, rate, 0.7f, 1f), 1f));

        static float[] BluntFlesh(FastRng r, int rate)
        {
            var b = W.Thud(r, rate, 0.75f, 0.35f);
            var smack = NoiseBurst(rate, r, 0.08f, 300, 2600, 0.001f, 0.012f);
            Add(b, smack, 0, 0.6f);
            var crunch = Buf(0.15f, rate);
            var bp = Biquad.BandPass(rate, 1800 * r.Range(0.85f, 1.15f), 1.3f);
            for (int i = 0; i < crunch.Length; i++)
            {
                float t = i / (float)rate;
                float grain = r.Next() < 0.06f ? r.Bipolar() * 3f : r.Bipolar() * 0.2f;
                crunch[i] = bp.Process(grain) * Env.Exp(t, 0.04f);
            }
            Add(b, crunch, (int)(0.004f * rate), 0.35f);
            Drive(b, 1.5f);
            Normalize(b, 0.85f);
            return b;
        }

        static float[] MetalHit(FastRng r, int rate, float[] modes, float ring)
        {
            var b = W.Thud(r, rate, 0.7f, 0.3f);
            float len = 0.3f + 1.1f * ring;
            var m = Modal(r, rate, len, modes, new[] { 0.18f + 0.6f * ring, 0.12f + 0.4f * ring, 0.08f + 0.25f * ring, 0.05f + 0.15f * ring }, 0.015f, 0.5f);
            var o = Buf(len, rate);
            Add(o, b, 0, 0.8f);
            Add(o, m, 0, 0.55f + 0.25f * ring);
            o = WithReverb(o, rate, 0.4f, 0.15f, 0.3f);
            Normalize(o, 0.85f);
            return o;
        }

        static float[] WoodThock(FastRng r, int rate)
        {
            var m = Modal(r, rate, 0.35f, new[] { 360f * r.Range(0.9f, 1.1f), 910f, 1720f, 2650f }, new[] { 0.09f, 0.06f, 0.04f, 0.025f }, 0.03f, 0.6f);
            var b = W.Thud(r, rate, 0.55f, 0.3f);
            var o = Buf(0.4f, rate);
            Add(o, b, 0, 0.7f);
            Add(o, m, 0, 0.9f);
            Normalize(o, 0.85f);
            return o;
        }

        static float[] BladeChop(FastRng r, int rate)
        {
            var b = W.Thud(r, rate, 0.55f, 0.3f);
            var wet = NoiseBurst(rate, r, 0.22f, 250, 1400, 0.002f, 0.05f);
            var edge = NoiseBurst(rate, r, 0.05f, 2500, 7000, 0.0005f, 0.008f);
            var o = Buf(0.35f, rate);
            Add(o, b, 0, 0.8f);
            Add(o, wet, (int)(0.003f * rate), 0.7f);
            Add(o, edge, 0, 0.5f);
            Drive(o, 1.4f);
            Normalize(o, 0.85f);
            return o;
        }

        // ------------------------------------------------------------------ two-stroke engine

        /// <summary>Two-stroke engine: each firing is a decaying noisy pop on a buzzy sawtooth, plus chain whine.</summary>
        static float[] Engine(FastRng r, int rate, float seconds, Func<float, float> hz, Func<float, float> amp, float whine)
        {
            var b = Buf(seconds, rate);
            var lp = Biquad.LowPass(rate, 1800f, 0.9f);
            var body = Biquad.Peak(rate, 220f, 1.1f, 6f);
            var wbp = Biquad.BandPass(rate, 3200f, 2f);
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float f = hz(t);
                ph += f / rate;
                float frac = ph - (float)Math.Floor(ph);
                float pop = (float)Math.Exp(-frac * 7f);
                float saw = frac * 2f - 1f;
                float n = r.Bipolar();
                float s = lp.Process(saw * 0.45f + n * pop * 0.9f) + wbp.Process(n) * whine * 0.25f * (0.6f + 0.4f * pop);
                b[i] = body.Process(s) * amp(t);
            }
            Drive(b, 2.2f);
            Normalize(b, 0.8f);
            return b;
        }

        /// <summary>A seamless engine loop: whole number of cycles and a crossfaded seam.</summary>
        static float[] EngineLoop(FastRng r, int rate, float seconds, float hz, float whine)
        {
            float f = (float)Math.Round(hz * seconds) / seconds;
            float fade = 0.08f;
            var raw = Engine(r, rate, seconds + fade, t => f * (1f + 0.012f * (float)Math.Sin(t * Math.PI * 2f * 3f / seconds)), t => 1f, whine);
            int n = (int)(seconds * rate), fn = (int)(fade * rate);
            var b = new float[n];
            Array.Copy(raw, b, n);
            for (int i = 0; i < fn && i < n; i++)
            {
                float k = i / (float)fn;
                b[i] = b[i] * k + raw[n + i] * (1f - k);
            }
            Normalize(b, 0.75f);
            return b;
        }

        static float[] SawStart(FastRng r, int rate)
        {
            var b = Buf(1.3f, rate);
            Add(b, Slide(r, rate, 0.28f, 600, 2400), 0, 0.5f); // pull cord
            for (int i = 0; i < 6; i++) Add(b, W.Click(r, rate, 0.6f + 0.1f * i, 0.5f), (int)((0.02f + i * 0.04f) * rate), 0.35f);
            var eng = Engine(r, rate, 0.95f, t => t < 0.15f ? 18f + 60f * t : 70f - 26f * Smooth01((t - 0.15f) / 0.5f), t => Smooth01(t / 0.08f) * (t < 0.2f ? 1f : 0.75f), 0.3f);
            Add(b, eng, (int)(0.3f * rate), 0.9f);
            Normalize(b, 0.85f);
            return b;
        }

        static float[] SawCut(FastRng r, int rate)
        {
            var b = Buf(0.4f, rate);
            var bp = Biquad.BandPass(rate, 1400f * r.Range(0.85f, 1.15f), 1.2f);
            var wet = Biquad.LowPass(rate, 600f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float am = 0.6f + 0.4f * Sin(t * 118f);
                float n = r.Bipolar();
                float grind = bp.Process(n) * am;
                float slosh = wet.Process(r.Next() < 0.03f ? r.Bipolar() * 4f : n * 0.2f);
                b[i] = (grind * 1.2f + slosh * 0.8f) * Env.AR(t, 0.01f, 0.25f, 0.12f);
            }
            Drive(b, 2f);
            Normalize(b, 0.8f);
            return b;
        }
    }
}
