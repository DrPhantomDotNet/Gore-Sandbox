using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>
    /// The cinematic intro (UI/Screens/IntroScreen.cs): one pre-timed score that follows the shot list, plus the
    /// alley gunshot, the logo slam and the street lamp buzz. Timings here must match IntroCinematic's timeline.
    /// </summary>
    public static class RecipesIntro
    {
        // Timeline marks (seconds) shared with IntroCinematic.
        public const float ShotTime = 11.0f, SilenceTime = 10.6f, BlackTime = 14.5f, LogoTime = 15.2f, SwapTime = 18.9f, EndTime = 20.8f;

        static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
        static float Ramp(float t, float a, float b) => Clamp01((t - a) / (b - a));

        /// <summary>
        /// ~21 s score: rain bed, low drone and a dark pad, a riser that cuts to dead silence right before the shot,
        /// tinnitus and a muffled sub during the slow motion, a second riser into the logo and a resolving chord.
        /// </summary>
        public static float[] Score(FastRng r, int rate)
        {
            var b = Buf(EndTime + 0.4f, rate);
            var rainHp = Biquad.HighPass(rate, 900);
            var rainLp = Biquad.LowPass(rate, 6500);
            var muffled = Biquad.LowPass(rate, 380);
            var riseBp = new Biquad(rate);
            var logoBp = new Biquad(rate);
            var padLp = Biquad.LowPass(rate, 900);
            double phD1 = 0, phD2 = 0, phD3 = 0, phSub = 0, phRise = 0, phRing = 0;
            double[] padPh = new double[4];
            float[] padA = { 110f, 130.81f, 164.81f, 220f };       // A minor, dark
            double[] endPh = new double[5];
            float[] endF = { 55f, 110f, 164.81f, 220f, 261.63f };  // resolves under the logo
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float s = 0f;
                float noise = r.Bipolar();

                // Rain: bright hiss before the shot, muffled (dazed) after it.
                float rainAmp = 0.16f * Ramp(t, 0f, 1.8f) * (1f - Ramp(t, SilenceTime - 0.05f, SilenceTime));
                float rain = rainLp.Process(rainHp.Process(noise));
                s += rain * rainAmp * (0.85f + 0.15f * (float)Math.Sin(t * 1.3f));
                float mufAmp = 0.5f * Ramp(t, ShotTime + 0.4f, ShotTime + 1.5f) * (1f - Ramp(t, BlackTime - 0.4f, BlackTime));
                s += muffled.Process(noise) * mufAmp;

                // Drone: D1 + A1 with a slow beat, swelling to the silence.
                float drone = 0.32f * Ramp(t, 0f, 3.5f) * (1f - Ramp(t, SilenceTime - 0.04f, SilenceTime)) * (0.8f + 0.2f * Ramp(t, 4f, 10f));
                if (drone > 0f)
                {
                    phD1 += 36.71 / rate; phD2 += 55.0 / rate; phD3 += 55.35 / rate;
                    s += drone * (Sin((float)(phD1 % 1.0)) * 0.6f + Sin((float)(phD2 % 1.0)) * 0.35f + Sin((float)(phD3 % 1.0)) * 0.35f);
                }

                // Dark pad (soft saws through a low-pass) from the reveal.
                float padAmp = 0.06f * Ramp(t, 3.8f, 6.5f) * (1f - Ramp(t, SilenceTime - 0.04f, SilenceTime));
                float pad = 0f;
                for (int k = 0; k < padA.Length; k++)
                {
                    padPh[k] += padA[k] * (1.0 + 0.002 * Math.Sin(t * (0.7 + k * 0.3))) / rate;
                    pad += (float)(padPh[k] % 1.0) * 2f - 1f;
                }
                s += padLp.Process(pad) * padAmp;

                // Riser 1: into the silence before the shot.
                if (t > 7.4f && t < SilenceTime)
                {
                    float x = Ramp(t, 7.4f, SilenceTime);
                    if ((i & 31) == 0) riseBp.SetBandPass(250 + 5200 * x * x, 1.4f);
                    phRise += (98 + 300 * x * x) / rate;
                    float saw = (float)(phRise % 1.0) * 2f - 1f;
                    s += (riseBp.Process(noise) * 0.9f + riseBp.Process(saw) * 0.4f) * (float)Math.Pow(x, 1.8) * 0.55f;
                }

                // After the shot: sub rumble and tinnitus.
                if (t > ShotTime && t < BlackTime)
                {
                    float u = t - ShotTime;
                    phSub += (30 + 10 * Math.Exp(-u * 1.5)) / rate;
                    s += Sin((float)(phSub % 1.0)) * 0.45f * Env.AD(u, 0.05f, 1.8f);
                    phRing += 3720.0 / rate;
                    s += Sin((float)(phRing % 1.0)) * 0.05f * Ramp(u, 0.05f, 0.4f) * (1f - Ramp(t, BlackTime - 1.2f, BlackTime));
                }

                // Riser 2: reverse-cymbal swell into the logo, hard cut.
                if (t > 13.0f && t < LogoTime)
                {
                    float x = Ramp(t, 13.0f, LogoTime);
                    if ((i & 31) == 0) logoBp.SetBandPass(400 + 7600 * x * x, 0.9f);
                    s += logoBp.Process(noise) * (float)Math.Pow(x, 2.6) * 0.8f;
                }

                // Resolving chord under the logo, fading as the menu takes over.
                if (t > LogoTime)
                {
                    float amp = 0.07f * Ramp(t, LogoTime, LogoTime + 0.8f) * (1f - Ramp(t, SwapTime - 0.4f, EndTime + 0.3f));
                    float c = 0f;
                    for (int k = 0; k < endF.Length; k++)
                    {
                        endPh[k] += endF[k] * (1.0 + 0.0015 * Math.Sin(t * (0.5 + k * 0.4))) / rate;
                        c += Sin((float)(endPh[k] % 1.0)) * (k == 0 ? 1.6f : 1f);
                    }
                    s += c * amp;
                }
                b[i] = s;
            }
            Fade(b, rate, 0.02f, 0.3f);
            Normalize(b, 0.8f);
            return b;
        }

        /// <summary>One rifle shot down a wet alley: sharp crack, chest-thump boom and a long slap-back tail.</summary>
        public static float[] Gunshot(FastRng r, int rate)
        {
            var b = Buf(0.9f, rate);
            var crack = NoiseBurst(rate, r, 0.12f, 1800, 9000, 0.0005f, 0.018f);
            var body = NoiseBurst(rate, r, 0.5f, 120, 2600, 0.001f, 0.09f);
            Add(b, crack, 0, 1.1f);
            Add(b, body, 0, 0.9f);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                ph += (38f + 90f * Env.Exp(t, 0.04f)) / rate;
                b[i] += Sin(ph) * Env.AD(t, 0.001f, 0.22f) * 1.4f;
            }
            Drive(b, 2.4f);
            b = WithReverb(b, rate, 0.9f, 0.55f, 2.6f, 0.45f);
            Normalize(b, 0.98f);
            return b;
        }

        /// <summary>The "GORE SANDBOX" logo slam: sub drop, crash, metallic ring and a big hall tail.</summary>
        public static float[] LogoSlam(FastRng r, int rate)
        {
            var b = Buf(2.2f, rate);
            var crash = NoiseBurst(rate, r, 1.2f, 200, 5000, 0.001f, 0.28f);
            Add(b, crash, 0, 0.9f);
            float[] f = { 173f, 287f, 409f, 563f, 811f, 1123f };
            float[] d = { 1.4f, 1.1f, 0.8f, 0.6f, 0.45f, 0.3f };
            float phSub = 0;
            var ph = new float[f.Length];
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                phSub += (28f + 70f * Env.Exp(t, 0.12f)) / rate;
                float s = Sin(phSub) * Env.AD(t, 0.002f, 0.9f) * 1.5f;
                for (int k = 0; k < f.Length; k++)
                {
                    ph[k] += f[k] / rate;
                    s += Sin(ph[k]) * Env.AD(t, 0.001f, d[k]) * 0.16f;
                }
                b[i] += s;
            }
            Drive(b, 1.8f);
            b = WithReverb(b, rate, 0.94f, 0.6f, 3.2f, 0.4f);
            Normalize(b, 0.98f);
            return b;
        }

        /// <summary>Street lamp buzz-crackle when it flickers.</summary>
        public static float[] LampBuzz(FastRng r, int rate)
        {
            var b = Buf(0.35f, rate);
            var bp = Biquad.BandPass(rate, 1800, 1.2f);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                ph += 100f / rate;
                float hum = Sin(ph) > 0.2f ? 1f : -1f;
                float crackle = r.Next() < 0.004f ? r.Bipolar() * 4f : 0f;
                b[i] = (hum * 0.35f + bp.Process(r.Bipolar()) * 0.6f + crackle) * Env.AR(t, 0.005f, 0.12f, 0.15f);
            }
            Normalize(b, 0.6f);
            return b;
        }
    }
}
