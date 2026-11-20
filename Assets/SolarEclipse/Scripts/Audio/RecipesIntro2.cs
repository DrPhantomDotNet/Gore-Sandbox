using System;
using static SolarEclipse.Audio.SynthKit;

namespace SolarEclipse.Audio
{
    /// <summary>
    /// Gore Sandbox 2 intro (Game/Intro2Cinematic.cs): one pre-timed score. A heartbeat and ECG beeps in the dark that
    /// speed up, fluorescent hum and muffled rain in the morgue, a low drone, a gasp-jolt, every light bursting, a
    /// flatline and the title hit. Timings must match Intro2Cinematic.
    /// </summary>
    public static class RecipesIntro2
    {
        public const float RoomTime = 2.0f, CloseTime = 8.5f, TwitchTime = 10.4f, JoltTime = 11.3f, BurstTime = 12.2f,
            FlatTime = 12.4f, TitleTime = 13.6f, SwapTime = 16.6f, EndTime = 17.2f;

        static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
        static float Ramp(float t, float a, float b) => Clamp01((t - a) / (b - a));

        /// <summary>Heart rate over the intro: slow and steady, racing after the twitch, gone at the burst.</summary>
        public static float Bpm(float t) => t < TwitchTime ? 54f + 6f * Ramp(t, 4f, TwitchTime) : 60f + 70f * Ramp(t, TwitchTime, JoltTime);

        public static float[] Score(FastRng r, int rate)
        {
            var b = Buf(EndTime + 0.6f, rate);
            var rainHp = Biquad.HighPass(rate, 700);
            var rainLp = Biquad.LowPass(rate, 2600); // rain outside a closed window: dull
            var hitLp = Biquad.LowPass(rate, 1800);
            double phD1 = 0, phD2 = 0, phHum = 0, phFlat = 0, phBeep = 0;
            double[] braam = new double[5];
            float[] braamF = { 36.71f, 55.0f, 73.42f, 110.0f, 110.6f };
            // Precompute beat times from the changing heart rate.
            var beats = new System.Collections.Generic.List<float>();
            for (float bt = 0.35f; bt < BurstTime; bt += 60f / Bpm(bt)) beats.Add(bt);
            int bi = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                float s = 0f;
                float noise = r.Bipolar();
                bool roomOn = t >= RoomTime - 0.4f && t < BurstTime;

                // Rain outside (muffled), from the room shot until the burst.
                s += rainLp.Process(rainHp.Process(noise)) * 0.07f * (roomOn ? Ramp(t, RoomTime - 0.4f, RoomTime + 1.2f) : 0f);

                // Fluorescent hum: 100 Hz with odd harmonics, dropping out with the light flickers.
                if (roomOn)
                {
                    phHum += 100.0 / rate;
                    float p = (float)(phHum % 1.0);
                    float hum = Sin(p) * 0.5f + Sin((p * 3f) % 1f) * 0.25f + Sin((p * 5f) % 1f) * 0.12f;
                    bool flick = Flicker(t);
                    s += hum * 0.035f * Ramp(t, RoomTime - 0.4f, RoomTime + 0.6f) * (flick ? 0.1f : 1f);
                }

                // Low drone that swells toward the jolt and cuts at the burst.
                float drone = 0.22f * Ramp(t, 0.5f, 4f) * (0.7f + 0.3f * Ramp(t, 6f, JoltTime)) * (t < BurstTime ? 1f : 0f);
                if (drone > 0f)
                {
                    phD1 += 41.2 / rate; phD2 += 61.7 / rate;
                    s += drone * (Sin((float)(phD1 % 1.0)) * 0.6f + Sin((float)(phD2 % 1.0)) * 0.3f);
                }

                // Heartbeat (lub-dub) + ECG beep on every beat.
                while (bi < beats.Count && beats[bi] < t - 0.6f) bi++;
                for (int k = bi; k < beats.Count && beats[k] <= t; k++)
                {
                    float u = t - beats[k];
                    float lub = u < 0.18f ? (float)Math.Sin(2 * Math.PI * (48f - 60f * u) * u) * (float)Math.Exp(-u * 26f) : 0f;
                    float u2 = u - 0.26f;
                    float dub = u2 > 0f && u2 < 0.16f ? (float)Math.Sin(2 * Math.PI * (60f - 70f * u2) * u2) * (float)Math.Exp(-u2 * 30f) * 0.65f : 0f;
                    s += (lub + dub) * 0.75f;
                    if (u < 0.09f)
                    {
                        phBeep += 960.0 / rate;
                        float env = Clamp01(u / 0.004f) * Clamp01((0.09f - u) / 0.02f);
                        s += Sin((float)(phBeep % 1.0)) * 0.09f * env;
                    }
                }

                // The jolt: a deep thump and a sharp intake under the gasp.
                float uj = t - JoltTime;
                if (uj > 0f && uj < 0.7f) s += hitLp.Process(noise) * 0.5f * (float)Math.Exp(-uj * 9f) + Sin((float)((40f * uj) % 1f)) * 0.6f * (float)Math.Exp(-uj * 7f);

                // Lights burst: crackle and glass.
                float ub = t - BurstTime;
                if (ub > 0f && ub < 0.9f)
                {
                    float crack = r.Next() < 0.08f * (float)Math.Exp(-ub * 5f) ? r.Bipolar() * 0.9f : 0f;
                    s += crack + noise * 0.25f * (float)Math.Exp(-ub * 14f);
                }

                // Flatline: one long ECG tone in the dark.
                if (t >= FlatTime && t < TitleTime + 2.4f)
                {
                    phFlat += 960.0 / rate;
                    float env = Ramp(t, FlatTime, FlatTime + 0.02f) * (1f - Ramp(t, TitleTime + 1.2f, TitleTime + 2.4f));
                    s += Sin((float)(phFlat % 1.0)) * 0.1f * env;
                }

                // Title hit: a braam (detuned low saws through a closing filter) and a noise crack.
                float ut = t - TitleTime;
                if (ut > 0f)
                {
                    float env = Clamp01(ut / 0.01f) * (float)Math.Exp(-ut * 0.75f);
                    float br = 0f;
                    for (int k = 0; k < braam.Length; k++)
                    {
                        braam[k] += braamF[k] / rate;
                        float ph = (float)(braam[k] % 1.0);
                        br += (ph * 2f - 1f) * (k < 2 ? 0.45f : 0.25f);
                    }
                    s += hitLp.Process(br) * 0.35f * env + noise * 0.35f * (float)Math.Exp(-ut * 18f);
                }
                b[i] = s;
            }
            var wet = WithReverb(b, rate, 0.7f, 0.18f, 1.2f);
            Fade(wet, rate, 0.01f, 0.5f);
            Normalize(wet, 0.92f);
            return wet;
        }

        /// <summary>Ear ringing after a blast: a thin high tone that fades over a few seconds.</summary>
        public static float[] Tinnitus(FastRng r, int rate)
        {
            var b = Buf(5f, rate);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)rate;
                ph += (6200.0 + 30.0 * Math.Sin(t * 3.0)) / rate;
                b[i] = Sin((float)(ph % 1.0)) * (float)Math.Exp(-t * 0.6f) * Clamp01(t / 0.05f) * 0.5f;
            }
            return b;
        }

        static readonly float[] Flickers = { 3.1f, 3.25f, 5.9f, 7.7f, 9.3f };
        public static bool Flicker(float t)
        {
            foreach (var f in Flickers) if (t >= f && t < f + 0.08f) return true;
            return false;
        }
        public static float[] FlickerTimes => Flickers;
    }
}
