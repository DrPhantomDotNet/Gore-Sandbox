using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>Impacts, footsteps, explosions, interactables and ambience loops.</summary>
    public static class RecipesWorld
    {
        public static float[] Footstep(FastRng r, int rate, string surface)
        {
            float[] b;
            switch (surface)
            {
                case "grass":
                {
                    b = Buf(0.25f, rate);
                    var bp = Biquad.BandPass(rate, r.Range(2500, 4000), 0.7f);
                    var am = new OnePole(rate, 80);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = i / (float)rate;
                        b[i] = bp.Process(r.Bipolar()) * Math.Abs(am.Process(r.Bipolar() * 6)) * Env.AD(t, 0.01f, 0.06f);
                    }
                    Add(b, RecipesWeapons.Thud(r, rate, 0.2f, 0.12f), 0, 0.25f);
                    break;
                }
                case "metal":
                    b = Modal(r, rate, 0.35f, new[] { 420f, 1150f, 2100f, 3400f }, new[] { 0.12f, 0.08f, 0.05f, 0.03f }, 0.05f, 0.3f);
                    Add(b, RecipesWeapons.Thud(r, rate, 0.3f, 0.1f), 0, 0.5f);
                    break;
                case "wood":
                    b = Modal(r, rate, 0.25f, new[] { 180f, 420f, 760f }, new[] { 0.05f, 0.03f, 0.02f }, 0.08f, 0.4f);
                    Add(b, RecipesWeapons.Thud(r, rate, 0.4f, 0.12f), 0, 0.6f);
                    break;
                case "carpet":
                    b = RecipesWeapons.Thud(r, rate, 0.15f, 0.12f);
                    LowPass(b, rate, 400);
                    break;
                default: // concrete
                    b = NoiseBurst(rate, r, 0.18f, 400, 5000, 0.001f, 0.018f);
                    Add(b, RecipesWeapons.Thud(r, rate, 0.3f, 0.12f), 0, 0.6f);
                    Add(b, NoiseBurst(rate, r, 0.1f, 2000, 8000, 0.02f, 0.02f), (int)(0.02f * rate), 0.15f);
                    break;
            }
            Normalize(b, 0.5f);
            return b;
        }

        public static float[] Explosion(FastRng r, int rate, bool distant)
        {
            float len = distant ? 2.5f : 2.2f;
            var b = Buf(len, rate);
            var lp = new Biquad(rate);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                if ((i & 31) == 0) lp.SetLowPass(distant ? 300 : 120 + 2200 * Env.Exp(t, 0.12f), 0.8f);
                float n = lp.Process(r.Bipolar()) * Env.AD(t, 0.004f, distant ? 0.6f : 0.45f) * 2.5f;
                ph += (28 + 50 * Env.Exp(t, 0.15f)) / rate;
                float sub = Sin(ph) * Env.AD(t, 0.003f, 0.5f) * 1.2f;
                float crackle = (!distant && r.Next() < 0.004f * Env.Exp(t, 0.5f)) ? r.Bipolar() * 3f : 0f;
                b[i] = n + sub + crackle;
            }
            Drive(b, distant ? 1.5f : 3f);
            b = WithReverb(b, rate, 0.9f, distant ? 0.7f : 0.4f, 1.5f, 0.5f);
            Normalize(b, 0.98f);
            return b;
        }

        public static float[] WoodImpact(FastRng r, int rate)
        {
            var b = Modal(r, rate, 0.4f, new[] { r.Range(160f, 240f), r.Range(380f, 520f), r.Range(700f, 950f), r.Range(1300f, 1700f) }, new[] { 0.09f, 0.06f, 0.04f, 0.02f }, 0.05f, 0.6f);
            Add(b, RecipesWeapons.Thud(r, rate, 0.5f, 0.2f), 0, 0.6f);
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] MetalImpact(FastRng r, int rate, bool heavy)
        {
            float b0 = heavy ? r.Range(180, 260) : r.Range(380, 520);
            var b = Modal(r, rate, heavy ? 1.6f : 0.9f, new[] { b0, b0 * 2.41f, b0 * 4.13f, b0 * 6.05f, b0 * 8.9f }, new[] { 0.6f, 0.45f, 0.3f, 0.2f, 0.12f }, 0.02f, 0.5f);
            Add(b, RecipesWeapons.Thud(r, rate, heavy ? 0.8f : 0.4f, 0.25f), 0, 0.7f);
            Normalize(b, 0.75f);
            return b;
        }

        public static float[] Ricochet(FastRng r, int rate)
        {
            var b = Buf(0.6f, rate);
            float ph = 0;
            float f0 = r.Range(2500, 4000), f1 = r.Range(800, 1400);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                ph += (f1 + (f0 - f1) * Env.Exp(t, 0.15f)) / rate;
                b[i] = (Sin(ph) + r.Bipolar() * 0.3f) * Env.AD(t, 0.003f, 0.15f);
            }
            Normalize(b, 0.4f);
            return b;
        }

        public static float[] BulletImpact(FastRng r, int rate, string surface)
        {
            float[] b;
            switch (surface)
            {
                case "metal": b = Modal(r, rate, 0.4f, new[] { 1800f, 2900f, 4400f }, new[] { 0.12f, 0.08f, 0.05f }, 0.1f, 1f); break;
                case "wood": b = WoodImpact(r, rate); break;
                case "dirt": b = NoiseBurst(rate, r, 0.25f, 150, 2500, 0.001f, 0.04f); break;
                case "glass": b = GlassBreak(r, rate, false); break;
                default:
                    b = NoiseBurst(rate, r, 0.25f, 600, 9000, 0.0005f, 0.015f);
                    Add(b, NoiseBurst(rate, r, 0.2f, 2000, 6000, 0.01f, 0.03f), (int)(0.005f * rate), 0.3f);
                    break;
            }
            Normalize(b, 0.6f);
            return b;
        }

        public static float[] BulletWhiz(FastRng r, int rate)
        {
            var b = Buf(0.35f, rate);
            var bp = new Biquad(rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                if ((i & 15) == 0) bp.SetBandPass(3500 - 2000 * t / 0.35f, 4f);
                b[i] = bp.Process(r.Bipolar()) * (float)Math.Sin(Math.PI * t / 0.35f);
            }
            Normalize(b, 0.4f);
            return b;
        }

        public static float[] GlassBreak(FastRng r, int rate, bool big)
        {
            float len = big ? 1.4f : 0.6f;
            var b = Buf(len, rate);
            Add(b, NoiseBurst(rate, r, 0.3f, 2500, 12000, 0.0005f, 0.05f), 0, 1f);
            int shards = big ? 40 : 14;
            for (int k = 0; k < shards; k++)
            {
                var tink = Modal(r, rate, 0.15f, new[] { r.Range(3000, 8000), r.Range(6000, 11000) }, new[] { 0.04f, 0.03f }, 0.05f, 0.2f);
                Add(b, tink, (int)(r.Next() * (len - 0.15f) * rate * (k < shards / 2 ? 0.3f : 1f)), r.Range(0.2f, 0.6f));
            }
            Normalize(b, 0.75f);
            return b;
        }

        public static float[] CrateBreak(FastRng r, int rate)
        {
            var b = Buf(0.9f, rate);
            for (int k = 0; k < 6; k++) Add(b, WoodImpact(r, rate), (int)(r.Next() * 0.25f * rate), r.Range(0.4f, 1f));
            Add(b, NoiseBurst(rate, r, 0.4f, 400, 4000, 0.001f, 0.08f), 0, 0.8f);
            Normalize(b, 0.85f);
            return b;
        }

        public static float[] Bell(FastRng r, int rate)
        {
            float f = r.Range(380, 440);
            var b = Modal(r, rate, 3f, new[] { f, f * 2.0f, f * 2.4f, f * 3f, f * 4.2f, f * 5.4f }, new[] { 1.6f, 1.2f, 0.9f, 0.7f, 0.4f, 0.3f }, 0.003f, 0.3f);
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] Boing(FastRng r, int rate)
        {
            var b = Buf(0.7f, rate);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float f = 120 + 380 * (1 - Env.Exp(t, 0.1f)) + 40 * (float)Math.Sin(t * 38);
                ph += f / rate;
                b[i] = Sin(ph) * Env.AD(t, 0.005f, 0.25f);
            }
            Normalize(b, 0.7f);
            return b;
        }

        public static float[] Button(FastRng r, int rate)
        {
            var b = RecipesWeapons.Click(r, rate, 0.6f, 0.6f, 0.15f);
            Add(b, Chirp(rate, 0.1f, 1600, 1600, 1, 0.03f), (int)(0.03f * rate), 0.3f);
            Normalize(b, 0.6f);
            return b;
        }

        public static float[] Hiss(FastRng r, int rate, float seconds)
        {
            var b = Buf(seconds, rate);
            var bp = Biquad.BandPass(rate, 4500, 0.8f);
            for (int i = 0; i < b.Length; i++) b[i] = bp.Process(r.Bipolar()) * Env.AR(i / (float)rate, 0.02f, seconds * 0.7f, seconds * 0.28f);
            Normalize(b, 0.55f);
            return b;
        }

        public static float[] Creak(FastRng r, int rate)
        {
            var b = Buf(0.9f, rate);
            float ph = 0;
            var bp = Biquad.BandPass(rate, 900, 3f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float f = 70 + 40 * (float)Math.Sin(t * 4) + r.Bipolar() * 8;
                ph += f / rate;
                float saw = (ph % 1f) * 2f - 1f;
                b[i] = bp.Process(saw) * Env.AR(t, 0.05f, 0.6f, 0.25f);
            }
            Normalize(b, 0.5f);
            return b;
        }

        public static float[] Squeak(FastRng r, int rate)
        {
            // Rubber duck: nasal fast up-glide.
            return Voice(rate, r, 0.3f, t => 700 + 900 * (float)Math.Min(1, t * 8), t => new Vowel(1200, 2400, 3600), t => Env.AR(t, 0.01f, 0.15f, 0.12f), 0.3f, 0f, 1f);
        }

        public static float[] Ding(FastRng r, int rate) => RecipesWeapons.Chime(r, rate, 1318f);

        public static float[] Buzzer(FastRng r, int rate)
        {
            var b = Buf(0.6f, rate);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                ph += 110f / rate;
                b[i] = ((ph % 1f) < 0.5f ? 0.6f : -0.6f) * Env.AR(i / (float)rate, 0.01f, 0.45f, 0.1f);
            }
            LowPass(b, rate, 2000);
            Normalize(b, 0.5f);
            return b;
        }

        public static float[] Static(FastRng r, int rate, float seconds)
        {
            var b = Buf(seconds, rate);
            var bp = Biquad.BandPass(rate, 3000, 0.5f);
            for (int i = 0; i < b.Length; i++) b[i] = bp.Process(r.Bipolar()) * (0.7f + 0.3f * r.Next());
            Fade(b, rate, 0.02f, 0.02f);
            Normalize(b, 0.3f);
            return b;
        }

        public static float[] Fan(FastRng r, int rate)
        {
            var b = Buf(2f, rate);
            var lp = Biquad.LowPass(rate, 600);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                b[i] = lp.Process(r.Bipolar()) * (0.6f + 0.4f * Sin(t * 24f)) + Sin(t * 96f) * 0.15f;
            }
            Fade(b, rate, 0.01f, 0.01f);
            Normalize(b, 0.5f);
            return b;
        }

        public static float[] PressSlam(FastRng r, int rate)
        {
            var b = MetalImpact(r, rate, true);
            Add(b, RecipesWeapons.Thud(r, rate, 1f, 0.5f), 0, 1f);
            Normalize(b, 0.9f);
            return b;
        }

        // ---------------------------------------------------------------- ambience loops (seamless)

        static float[] Loopify(float[] b, int rate, float crossfade)
        {
            int n = (int)(crossfade * rate);
            var o = new float[b.Length - n];
            Array.Copy(b, o, o.Length);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n;
                o[i] = b[o.Length + i] * (1 - a) + b[i] * a;
            }
            return o;
        }

        public static float[] Wind(FastRng r, int rate)
        {
            var b = Buf(12f, rate);
            var bp = new Biquad(rate);
            var lfo = new OnePole(rate, 0.3f);
            float g = 0;
            for (int i = 0; i < b.Length; i++)
            {
                if ((i & 63) == 0) { g = 0.4f + Math.Abs(lfo.Process(r.Bipolar() * 40f)); bp.SetBandPass(250 + 500 * g, 0.6f); }
                b[i] = bp.Process(r.Bipolar()) * g;
            }
            b = Loopify(b, rate, 2f);
            Normalize(b, 0.4f);
            return b;
        }

        public static float[] Crickets(FastRng r, int rate)
        {
            var b = Buf(8f, rate);
            for (int c = 0; c < 5; c++)
            {
                float f = r.Range(3800, 5200), period = r.Range(0.5f, 1.1f), offset = r.Next() * period;
                float pan = r.Range(0.2f, 1f);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)rate;
                    float lt = (t + offset) % period;
                    float chirp = lt < 0.12f ? (float)Math.Abs(Math.Sin(lt * 80 * Math.PI)) : 0f;
                    b[i] += Sin(t * f) * chirp * pan * 0.2f;
                }
            }
            Fade(b, rate, 0.05f, 0.05f);
            Normalize(b, 0.25f);
            return b;
        }

        public static float[] FluorescentHum(FastRng r, int rate)
        {
            var b = Buf(4f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float s = Sin(t * 120) * 0.5f + Sin(t * 240) * 0.3f + Sin(t * 360) * 0.15f + Sin(t * 480) * 0.08f;
                b[i] = s * (0.8f + 0.2f * Sin(t * 0.5f)) + r.Bipolar() * 0.02f;
            }
            Normalize(b, 0.35f);
            return b;
        }

        public static float[] CityRumble(FastRng r, int rate)
        {
            var b = Buf(10f, rate);
            var lp = Biquad.LowPass(rate, 220);
            var lfo = new OnePole(rate, 0.2f);
            for (int i = 0; i < b.Length; i++) b[i] = lp.Process(r.Bipolar()) * (0.6f + Math.Abs(lfo.Process(r.Bipolar() * 30)));
            b = Loopify(b, rate, 2f);
            Normalize(b, 0.4f);
            return b;
        }

        public static float[] Drone(FastRng r, int rate, float root)
        {
            var b = Buf(10f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float s = 0;
                // integer-ish multiples of 0.1 Hz make the 10 s loop seamless
                s += Sin(t * root) * 0.5f + Sin(t * (root * 1.5f)) * 0.25f + Sin(t * root * 2.003f) * 0.2f;
                s *= 0.7f + 0.3f * Sin(t * 0.1f);
                b[i] = s;
            }
            Normalize(b, 0.4f);
            return b;
        }

        public static float[] NextbotChase(FastRng r, int rate)
        {
            var b = Buf(4f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float wob = 1f + 0.06f * Sin(t * 3.5f);
                float s = Sq(t * 220 * wob) + Sq(t * 233 * wob) + Sq(t * 311 * wob) * 0.7f + Sq(t * 55) * 0.8f;
                b[i] = s * 0.25f + r.Bipolar() * 0.15f * (0.5f + 0.5f * Sin(t * 8));
            }
            LowPass(b, rate, 3500);
            Drive(b, 2.5f);
            Fade(b, rate, 0.01f, 0.01f);
            Normalize(b, 0.8f);
            return b;
        }

        static float Sq(float phase) => (phase % 1f) < 0.5f ? 1f : -1f;

        public static float[] Jumpscare(FastRng r, int rate)
        {
            var b = Buf(1.2f, rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                b[i] = (Sq(t * (400 + 300 * Sin(t * 30))) * 0.4f + r.Bipolar() * 0.8f) * Env.AD(t, 0.002f, 0.4f);
            }
            Drive(b, 4f);
            Normalize(b, 0.95f);
            return b;
        }

        public static float[] RadioTune(FastRng r, int rate)
        {
            // Cheerful chiptune loop for the in-world radio.
            float bpm = 132, beat = 60f / bpm;
            int[] melody = { 0, 4, 7, 12, 11, 7, 4, 2, 0, 4, 7, 9, 7, 4, 2, -1 };
            int[] bass = { 0, 0, -5, -5, -3, -3, -7, -5 };
            float len = melody.Length * beat * 0.5f * 2;
            var b = Buf(len, rate);
            float ph = 0, pb = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                int step = (int)(t / (beat * 0.5f)) % melody.Length;
                float lt = t % (beat * 0.5f);
                int note = melody[step];
                float f = 523.25f * (float)Math.Pow(2, note / 12f);
                ph += f / rate;
                float lead = (note < 0 ? 0 : 1) * ((ph % 1f) < 0.25f ? 0.3f : -0.1f) * Env.AD(lt, 0.005f, 0.12f);
                int bstep = (int)(t / beat) % bass.Length;
                pb += 130.8f * (float)Math.Pow(2, bass[bstep] / 12f) / rate;
                float bs = ((pb % 1f) < 0.5f ? 0.25f : -0.25f) * Env.AD(t % beat, 0.005f, 0.3f);
                float hat = (t % (beat * 0.5f)) < 0.02f ? r.Bipolar() * 0.12f : 0f;
                b[i] = lead + bs + hat;
            }
            Normalize(b, 0.5f);
            return b;
        }
    }
}
