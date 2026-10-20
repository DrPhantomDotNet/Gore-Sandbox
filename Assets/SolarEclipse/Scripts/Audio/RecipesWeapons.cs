using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Firearm, melee and mechanical weapon sounds.</summary>
    public static class RecipesWeapons
    {
        public struct GunVoice
        {
            public float BodyCut, BodyTau, ThumpHz, ThumpAmt, CrackAmt, TailTau, TailCut, Drive, Room, Wet, Length;
        }

        public static readonly GunVoice Glock = new GunVoice { BodyCut = 2400, BodyTau = 0.035f, ThumpHz = 95, ThumpAmt = 0.7f, CrackAmt = 0.35f, TailTau = 0.18f, TailCut = 1200, Drive = 2.2f, Room = 0.55f, Wet = 0.35f, Length = 0.5f };
        public static readonly GunVoice Uzi = new GunVoice { BodyCut = 2900, BodyTau = 0.024f, ThumpHz = 110, ThumpAmt = 0.5f, CrackAmt = 0.3f, TailTau = 0.12f, TailCut = 1400, Drive = 2.5f, Room = 0.5f, Wet = 0.3f, Length = 0.35f };
        public static readonly GunVoice Ar15 = new GunVoice { BodyCut = 3800, BodyTau = 0.045f, ThumpHz = 72, ThumpAmt = 0.95f, CrackAmt = 0.95f, TailTau = 0.35f, TailCut = 1500, Drive = 3f, Room = 0.72f, Wet = 0.42f, Length = 0.9f };
        public static readonly GunVoice Ar17 = new GunVoice { BodyCut = 1800, BodyTau = 0.09f, ThumpHz = 52, ThumpAmt = 1.3f, CrackAmt = 0.35f, TailTau = 0.5f, TailCut = 900, Drive = 3.4f, Room = 0.82f, Wet = 0.45f, Length = 1.3f };

        public static float[] Gunshot(FastRng r, int rate, GunVoice g)
        {
            var b = Buf(g.Length, rate);
            var crackHp = Biquad.HighPass(rate, 2500);
            var bodyLp = Biquad.LowPass(rate, g.BodyCut * r.Range(0.9f, 1.1f), 0.9f);
            var tailLp = Biquad.LowPass(rate, g.TailCut);
            var mid = Biquad.Peak(rate, 900 * r.Range(0.85f, 1.15f), 1.2f, 4f);
            float thumpPhase = 0;
            float thumpHz = g.ThumpHz * r.Range(0.92f, 1.08f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float n = r.Bipolar();
                float transient = t < 0.0015f ? n * 1.2f : 0f;
                float crack = crackHp.Process(n) * Env.Exp(t, 0.0045f) * g.CrackAmt * 1.6f;
                float body = bodyLp.Process(n) * Env.AD(t, 0.0005f, g.BodyTau) * 1.8f;
                thumpPhase += (thumpHz * (1f + 1.2f * Env.Exp(t, 0.012f))) / rate;
                float thump = Sin(thumpPhase) * Env.AD(t, 0.001f, 0.06f) * g.ThumpAmt;
                float tail = tailLp.Process(n) * Env.AD(t, 0.004f, g.TailTau) * 0.35f;
                b[i] = mid.Process(transient + crack + body + thump + tail);
            }
            Drive(b, g.Drive);
            b = WithReverb(b, rate, g.Room, g.Wet, g.Length * 0.8f);
            Normalize(b, 0.95f);
            return b;
        }

        /// <summary>Far-away echo for gunfire across the map.</summary>
        public static float[] DistantTail(FastRng r, int rate)
        {
            var b = NoiseBurst(rate, r, 1.6f, 80, 700, 0.01f, 0.35f);
            b = WithReverb(b, rate, 0.92f, 0.8f, 1.2f, 0.6f);
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] AdminShot(FastRng r, int rate)
        {
            var b = Buf(1.2f, rate);
            float ph = 0, ph2 = 0;
            var lp = Biquad.LowPass(rate, 4000);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float f = 180 + 2600 * Env.Exp(t, 0.06f);
                ph += f / rate; ph2 += f * 1.503f / rate;
                float saw = (ph % 1f) * 2f - 1f;
                float zap = (saw * 0.6f + Sin(ph2) * 0.4f) * Env.AD(t, 0.002f, 0.18f);
                float thump = Sin(t * 60f * (1 + 2 * Env.Exp(t, 0.02f))) * Env.AD(t, 0.001f, 0.12f) * 1.4f;
                float shimmer = Sin(t * (3200 + 400 * Sin(t * 7f))) * Env.AD(t, 0.05f, 0.4f) * 0.25f;
                b[i] = lp.Process(zap) + thump + shimmer + r.Bipolar() * Env.Exp(t, 0.01f) * 0.6f;
            }
            Drive(b, 2f);
            b = WithReverb(b, rate, 0.8f, 0.4f, 0.8f);
            Normalize(b, 0.95f);
            return b;
        }

        /// <summary>Short metallic click, the basis of all gun mechanics.</summary>
        public static float[] Click(FastRng r, int rate, float pitch = 1f, float body = 0.4f, float length = 0.12f)
        {
            var b = Buf(length, rate);
            var ring = Biquad.BandPass(rate, 3200 * pitch * r.Range(0.9f, 1.1f), 9);
            var ring2 = Biquad.BandPass(rate, 5100 * pitch * r.Range(0.9f, 1.1f), 12);
            var low = Biquad.LowPass(rate, 600);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float n = r.Bipolar() * Env.Exp(t, 0.0025f);
                b[i] = ring.Process(n) * 3f + ring2.Process(n) * 2f + low.Process(r.Bipolar()) * Env.Exp(t, 0.01f) * body;
            }
            Normalize(b, 0.8f);
            return b;
        }

        static float[] Sequence(int rate, float length, params (float time, float[] sound, float gain)[] parts)
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

        public static float[] SlideBack(FastRng r, int rate) => Sequence(rate, 0.35f, (0f, Click(r, rate, 0.8f), 0.8f), (0.01f, Slide(r, rate, 0.12f, 2500, 4500), 0.5f), (0.13f, Click(r, rate, 1.1f, 0.6f), 1f));
        public static float[] SlideForward(FastRng r, int rate) => Sequence(rate, 0.3f, (0f, Slide(r, rate, 0.07f, 4500, 2500), 0.4f), (0.07f, Click(r, rate, 0.7f, 0.9f), 1f), (0.09f, Click(r, rate, 1.3f, 0.2f), 0.4f));
        public static float[] MagOut(FastRng r, int rate, float pitch) => Sequence(rate, 0.4f, (0f, Click(r, rate, pitch, 0.3f), 0.9f), (0.03f, Slide(r, rate, 0.18f, 1800 * pitch, 900 * pitch), 0.6f));
        public static float[] MagIn(FastRng r, int rate, float pitch) => Sequence(rate, 0.35f, (0f, Slide(r, rate, 0.08f, 900 * pitch, 1800 * pitch), 0.5f), (0.08f, Click(r, rate, pitch * 0.8f, 0.9f), 1f), (0.1f, Click(r, rate, pitch * 1.2f, 0.4f), 0.7f));
        public static float[] ChargingHandle(FastRng r, int rate) => Sequence(rate, 0.55f, (0f, Click(r, rate, 0.9f), 0.7f), (0.02f, Slide(r, rate, 0.15f, 2000, 3800), 0.6f), (0.18f, Click(r, rate, 1.2f), 0.9f), (0.3f, Slide(r, rate, 0.06f, 3800, 2000), 0.5f), (0.36f, Click(r, rate, 0.75f, 1f), 1f));
        public static float[] BoltRelease(FastRng r, int rate) => Sequence(rate, 0.3f, (0f, Click(r, rate, 1.4f, 0.1f), 0.5f), (0.04f, Click(r, rate, 0.7f, 1f), 1f));
        public static float[] ShellIn(FastRng r, int rate) => Sequence(rate, 0.3f, (0f, Slide(r, rate, 0.06f, 700, 1400), 0.6f), (0.06f, Click(r, rate, 0.55f, 1.2f), 1f));
        public static float[] DryFire(FastRng r, int rate) => Click(r, rate, 1.2f, 0.15f, 0.1f);
        public static float[] FireMode(FastRng r, int rate) => Sequence(rate, 0.2f, (0f, Click(r, rate, 1.6f, 0.05f), 0.6f), (0.03f, Click(r, rate, 1.9f, 0.05f), 0.5f));
        public static float[] EquipGun(FastRng r, int rate) => Sequence(rate, 0.5f, (0f, Cloth(r, rate, 0.25f), 0.6f), (0.18f, Click(r, rate, 0.9f, 0.4f), 0.8f), (0.22f, Click(r, rate, 1.3f, 0.1f), 0.4f));

        public static float[] Cloth(FastRng r, int rate, float seconds)
        {
            var b = Buf(seconds, rate);
            var bp = Biquad.BandPass(rate, 1800, 0.8f);
            var mod = new OnePole(rate, 25);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float m = Math.Abs(mod.Process(r.Bipolar() * 6f));
                b[i] = bp.Process(r.Bipolar()) * m * Env.AR(t, seconds * 0.2f, seconds * 0.4f, seconds * 0.4f);
            }
            Normalize(b, 0.5f);
            return b;
        }

        /// <summary>Brass casing bouncing on the floor.</summary>
        public static float[] Casing(FastRng r, int rate, bool shotgunShell)
        {
            var b = Buf(0.6f, rate);
            float t0 = 0, gap = r.Range(0.07f, 0.11f), amp = 1f;
            float[] partials = shotgunShell ? new[] { 900f, 1700f, 2600f } : new[] { 3300f, 4800f, 7100f, 9300f };
            for (int k = 0; k < 6 && t0 < 0.55f; k++)
            {
                var hit = Modal(r, rate, 0.12f, partials, shotgunShell ? new[] { 0.02f, 0.015f, 0.01f } : new[] { 0.05f, 0.035f, 0.025f, 0.015f }, 0.05f, 0.4f);
                Add(b, hit, (int)(t0 * rate), amp);
                t0 += gap; gap *= r.Range(0.5f, 0.7f); amp *= r.Range(0.45f, 0.7f);
            }
            Normalize(b, 0.6f);
            return b;
        }

        public static float[] Whoosh(FastRng r, int rate, float seconds, float fLow, float fHigh, float q = 1.4f)
        {
            var b = Buf(seconds, rate);
            var bp = new Biquad(rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float x = t / seconds;
                if ((i & 15) == 0) bp.SetBandPass(fLow + (fHigh - fLow) * (float)Math.Sin(Math.PI * x), q);
                b[i] = bp.Process(r.Bipolar()) * (float)Math.Pow(Math.Sin(Math.PI * Math.Min(1, x * 1.2f)), 1.5f);
            }
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] KnifeDraw(FastRng r, int rate)
        {
            var b = Whoosh(r, rate, 0.45f, 2500, 6500, 3f);
            var ring = Modal(r, rate, 0.6f, new[] { 2900f, 4400f, 6700f, 8800f }, new[] { 0.35f, 0.25f, 0.18f, 0.12f }, 0.01f, 0.1f);
            Add(b, ring, (int)(0.25f * rate), 0.4f);
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] KnifeHitHard(FastRng r, int rate)
        {
            var b = Modal(r, rate, 0.5f, new[] { 2500f, 4100f, 6700f }, new[] { 0.25f, 0.18f, 0.1f }, 0.03f, 1f);
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] Thud(FastRng r, int rate, float weight, float seconds = 0.35f)
        {
            var b = Buf(seconds, rate);
            var lp = Biquad.LowPass(rate, 180 + 300 * (1 - weight));
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                ph += (55 + 40 * (1 - weight)) * (1 + Env.Exp(t, 0.015f)) / rate;
                b[i] = lp.Process(r.Bipolar()) * Env.AD(t, 0.001f, 0.05f + weight * 0.05f) * 2f + Sin(ph) * Env.AD(t, 0.001f, 0.07f) * weight;
            }
            Normalize(b, 0.85f);
            return b;
        }

        public static float[] PunchHit(FastRng r, int rate)
        {
            var b = Thud(r, rate, 0.6f, 0.3f);
            var slap = NoiseBurst(rate, r, 0.05f, 1500, 7000, 0.0005f, 0.006f);
            Add(b, slap, 0, 0.8f);
            Normalize(b, 0.9f);
            return b;
        }

        public static float[] SpawnGunHum(FastRng r, int rate)
        {
            var b = Buf(2f, rate);
            var lp = Biquad.LowPass(rate, 900, 2f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                // integer cycles per 2 s loop so it loops seamlessly
                float s = Sin(t * 110f) * 0.5f + Sin(t * 220f) * 0.25f + Sin(t * 331f) * 0.12f;
                s *= 0.75f + 0.25f * Sin(t * 3f);
                b[i] = lp.Process(s + r.Bipolar() * 0.05f);
            }
            Normalize(b, 0.5f);
            return b;
        }

        public static float[] SpawnPop(FastRng r, int rate)
        {
            var b = Chirp(rate, 0.5f, 1400, 140, 0.05f, 0.12f);
            var sparkle = Modal(r, rate, 0.5f, new[] { 3000f, 4500f, 6100f, 7900f }, new[] { 0.2f, 0.15f, 0.1f, 0.08f }, 0.05f, 0f);
            Add(b, sparkle, (int)(0.03f * rate), 0.35f);
            Normalize(b, 0.8f);
            return b;
        }

        public static float[] Zap(FastRng r, int rate, float seconds, float f0, float f1)
        {
            var b = Buf(seconds, rate);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float f = f1 + (f0 - f1) * (1 - t / seconds);
                ph += f / rate;
                float sq = (ph % 1f) < 0.5f ? 1f : -1f;
                b[i] = (sq * 0.5f + r.Bipolar() * 0.5f * Env.Exp(t, 0.05f)) * Env.AD(t, 0.002f, seconds * 0.4f);
            }
            LowPass(b, rate, 5000);
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] Chime(FastRng r, int rate, float baseHz)
        {
            var b = Modal(r, rate, 1.2f, new[] { baseHz, baseHz * 2.01f, baseHz * 3.02f, baseHz * 4.17f }, new[] { 0.5f, 0.35f, 0.2f, 0.12f }, 0.002f, 0.05f);
            Normalize(b, 0.6f);
            return b;
        }
    }
}
