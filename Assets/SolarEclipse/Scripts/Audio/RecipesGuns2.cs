using System;
using static SolarEclipse.Audio.SynthKit;
using W = SolarEclipse.Audio.RecipesWeapons;

namespace SolarEclipse.Audio
{
    /// <summary>Second gun pack: pump shotgun, bolt-action sniper, .357 revolver, minigun and harpoon gun.</summary>
    public static class RecipesGuns2
    {
        static readonly W.GunVoice Pump = new W.GunVoice { BodyCut = 1600, BodyTau = 0.11f, ThumpHz = 46, ThumpAmt = 1.5f, CrackAmt = 0.4f, TailTau = 0.6f, TailCut = 850, Drive = 3.8f, Room = 0.85f, Wet = 0.48f, Length = 1.4f };
        static readonly W.GunVoice Sniper = new W.GunVoice { BodyCut = 4200, BodyTau = 0.06f, ThumpHz = 58, ThumpAmt = 1.3f, CrackAmt = 1.4f, TailTau = 0.7f, TailCut = 1300, Drive = 3.6f, Room = 0.9f, Wet = 0.55f, Length = 1.9f };
        static readonly W.GunVoice Magnum = new W.GunVoice { BodyCut = 2600, BodyTau = 0.06f, ThumpHz = 70, ThumpAmt = 1.2f, CrackAmt = 0.8f, TailTau = 0.4f, TailCut = 1100, Drive = 3.3f, Room = 0.75f, Wet = 0.42f, Length = 1.1f };
        static readonly W.GunVoice Mini = new W.GunVoice { BodyCut = 3200, BodyTau = 0.018f, ThumpHz = 95, ThumpAmt = 0.55f, CrackAmt = 0.45f, TailTau = 0.07f, TailCut = 1500, Drive = 2.6f, Room = 0.4f, Wet = 0.22f, Length = 0.22f };

        public static void Define(Action<string, int, Func<FastRng, int, int, float[]>, int> def)
        {
            def("gun_shotgun", 2, (r, s, v) => W.Gunshot(r, s, Pump), 44100);
            def("gun_sniper", 2, (r, s, v) => W.Gunshot(r, s, Sniper), 44100);
            def("gun_revolver", 2, (r, s, v) => W.Gunshot(r, s, Magnum), 44100);
            def("gun_minigun", 4, (r, s, v) => W.Gunshot(r, s, Mini), 44100);
            def("gun_harpoon", 2, (r, s, v) => HarpoonShot(r, s), 44100);
            def("pump_action", 2, (r, s, v) => PumpAction(r, s), 44100);
            def("bolt_cycle", 2, (r, s, v) => BoltCycle(r, s), 44100);
            def("revolver_open", 1, (r, s, v) => Seq(s, 0.4f, (0f, W.Click(r, s, 0.9f, 0.5f), 0.9f), (0.02f, Slide(r, s, 0.15f, 2400, 1200), 0.5f)), 44100);
            def("revolver_close", 1, (r, s, v) => Seq(s, 0.35f, (0f, Slide(r, s, 0.07f, 1200, 2600), 0.4f), (0.07f, W.Click(r, s, 0.75f, 1f), 1f), (0.12f, Ratchet(r, s, 3, 0.03f), 0.5f)), 44100);
            def("speedloader", 1, (r, s, v) => Seq(s, 0.35f, (0f, Ratchet(r, s, 6, 0.012f), 0.6f), (0.1f, W.Click(r, s, 0.6f, 0.6f), 0.8f)), 44100);
            def("hammer_cock", 2, (r, s, v) => Seq(s, 0.25f, (0f, Ratchet(r, s, 2, 0.035f), 0.7f), (0.09f, W.Click(r, s, 1.25f, 0.4f), 1f)), 44100);
            def("minigun_windup", 1, (r, s, v) => Motor(r, s, 0.7f, 120f, 900f, true), 44100);
            def("minigun_winddown", 1, (r, s, v) => Motor(r, s, 1.2f, 900f, 90f, false), 44100);
            def("minigun_spin", 1, (r, s, v) => MotorLoop(r, s), 44100);
            def("harpoon_load", 1, (r, s, v) => Seq(s, 0.5f, (0f, Slide(r, s, 0.25f, 900, 2400), 0.6f), (0.26f, W.Click(r, s, 0.6f, 1f), 1f), (0.3f, Hiss(r, s, 0.18f), 0.35f)), 44100);
            def("harpoon_hit", 3, (r, s, v) => HarpoonHit(r, s), 44100);
        }

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

        static float[] Ratchet(FastRng r, int rate, int clicks, float gap)
        {
            var b = Buf(clicks * gap + 0.1f, rate);
            for (int i = 0; i < clicks; i++) Add(b, W.Click(r, rate, 1.5f + r.Range(-0.1f, 0.1f), 0.05f, 0.05f), (int)(i * gap * rate), 0.6f);
            return b;
        }

        static float[] Hiss(FastRng r, int rate, float seconds)
        {
            var b = NoiseBurst(rate, r, seconds, 2500, 9000, 0.005f, seconds * 0.4f);
            return b;
        }

        static float[] PumpAction(FastRng r, int rate) => Seq(rate, 0.55f,
            (0f, W.Click(r, rate, 0.7f, 0.8f), 0.8f), (0.01f, Slide(r, rate, 0.13f, 1400, 600), 0.9f), (0.14f, W.Click(r, rate, 0.55f, 1.2f), 1f),
            (0.24f, Slide(r, rate, 0.1f, 600, 1500), 0.8f), (0.34f, W.Click(r, rate, 0.8f, 1f), 1f), (0.36f, W.Click(r, rate, 1.3f, 0.2f), 0.4f));

        static float[] BoltCycle(FastRng r, int rate) => Seq(rate, 0.8f,
            (0f, W.Click(r, rate, 1.1f, 0.3f), 0.7f), (0.12f, Slide(r, rate, 0.12f, 2200, 3600), 0.6f), (0.24f, W.Click(r, rate, 0.9f, 0.6f), 0.9f),
            (0.38f, Slide(r, rate, 0.1f, 3600, 2000), 0.55f), (0.48f, W.Click(r, rate, 0.7f, 1f), 1f), (0.6f, W.Click(r, rate, 1.2f, 0.3f), 0.6f));

        static float[] HarpoonShot(FastRng r, int rate)
        {
            var b = Buf(0.9f, rate);
            var hp = Biquad.HighPass(rate, 600);
            var lp = Biquad.LowPass(rate, 5000);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float blast = lp.Process(hp.Process(r.Bipolar())) * Env.AD(t, 0.001f, 0.09f) * 1.4f;
                ph += (70f * (1f + Env.Exp(t, 0.02f))) / rate;
                float thunk = Sin(ph) * Env.AD(t, 0.001f, 0.05f) * 1.2f;
                float twang = Sin(t * 410f * (1f + 0.02f * Sin(t * 30f))) * Env.AD(t, 0.01f, 0.25f) * 0.35f;
                b[i] = blast + thunk + twang;
            }
            Add(b, W.Click(r, rate, 0.6f, 1f), 0, 0.8f);
            Drive(b, 1.6f);
            b = WithReverb(b, rate, 0.6f, 0.3f, 0.5f);
            Normalize(b, 0.9f);
            return b;
        }

        static float[] HarpoonHit(FastRng r, int rate)
        {
            var b = Modal(r, rate, 0.7f, new[] { 520f * r.Range(0.9f, 1.1f), 1310f, 2270f, 3400f }, new[] { 0.3f, 0.2f, 0.12f, 0.08f }, 0.03f, 0.9f);
            var thud = W.Thud(r, rate, 0.8f, 0.3f);
            Add(b, thud, 0, 0.9f);
            Normalize(b, 0.85f);
            return b;
        }

        static float[] Motor(FastRng r, int rate, float seconds, float f0, float f1, bool up)
        {
            var b = Buf(seconds, rate);
            var lp = Biquad.LowPass(rate, 3000);
            float ph = 0, ph2 = 0, rot = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float x = t / seconds;
                float k = up ? 1f - (float)Math.Pow(1f - x, 2f) : (float)Math.Pow(1f - x, 1.5f) * 1f;
                float f = up ? f0 + (f1 - f0) * k : f1 + (f0 - f1) * (1f - k);
                ph += f / rate; ph2 += f * 2.01f / rate; rot += f / 6f / rate;
                float whine = Sin(ph) * 0.5f + Sin(ph2) * 0.25f;
                float clatter = lp.Process(r.Bipolar()) * (0.5f + 0.5f * Sin(rot * 6f)) * 0.4f;
                float amp = up ? Math.Min(1f, x * 4f) : (1f - x);
                b[i] = (whine + clatter) * amp;
            }
            Normalize(b, 0.6f);
            return b;
        }

        /// <summary>Seamless 1 s loop at full spin (integer cycles so it loops without a click).</summary>
        static float[] MotorLoop(FastRng r, int rate)
        {
            var b = Buf(1f, rate);
            var lp = Biquad.LowPass(rate, 2500);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float whine = Sin(t * 900f) * 0.5f + Sin(t * 1809f) * 0.22f + Sin(t * 150f) * 0.3f;
                float clatter = lp.Process(r.Bipolar()) * (0.6f + 0.4f * Sin(t * 150f)) * 0.35f;
                b[i] = whine + clatter;
            }
            Fade(b, rate, 0.004f, 0.004f);
            Normalize(b, 0.55f);
            return b;
        }
    }
}
