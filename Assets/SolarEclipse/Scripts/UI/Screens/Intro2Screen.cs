using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 startup cinematic (3D in Game/Intro2Cinematic.cs, score in Audio/RecipesIntro2.cs). Owns the clock
    /// and draws the film layer: fades, letterbox, the studio card, the ECG-line title reveal with blood running down
    /// the letters, and the skip prompt. Press/tap once for "press again to skip". Ends with GameManager.OnSplashFinished().
    /// </summary>
    public sealed class Intro2Screen : UIScreen
    {
        const float RoomTime = Intro2Cinematic.RoomTime, BurstTime = Intro2Cinematic.BurstTime, FlatTime = Intro2Cinematic.FlatTime,
            TitleTime = Intro2Cinematic.TitleTime, SwapTime = Intro2Cinematic.SwapTime, EndTime = Intro2Cinematic.EndTime;
        const float SkipFade = 0.35f;

        Intro2Cinematic cine;
        float t, armedUntil = -1f, skipStart = -1f;
        int lastPressFrame = -1;
        bool finished;
        readonly float[] drips = new float[24];

        public static float Time01 { get; private set; }

        public override void OnOpen()
        {
            cine = Intro2Cinematic.Create();
            for (int i = 0; i < drips.Length; i++) drips[i] = Random.value;
        }

        public override void OnClose() { if (cine != null) { cine.Dispose(); cine = null; } }
        public override bool OnBack() { Press(); return true; }

        public override void Tick()
        {
            if (finished || cine == null) return;
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)) Press();
            float dt = Mathf.Min(UnityEngine.Time.unscaledDeltaTime, 0.1f);
            if (skipStart >= 0f && UnityEngine.Time.unscaledTime - skipStart >= SkipFade) { Finish(); return; }
            float prev = t;
            t += dt;
            Time01 = t;
            cine.Step(t, prev, dt);
            if (t >= EndTime) Finish();
        }

        void Press()
        {
            if (UnityEngine.Time.frameCount == lastPressFrame) return;
            lastPressFrame = UnityEngine.Time.frameCount;
            if (Age < 0.3f || skipStart >= 0f || t >= SwapTime) return;
            if (UnityEngine.Time.unscaledTime < armedUntil) { skipStart = UnityEngine.Time.unscaledTime; armedUntil = -1f; }
            else armedUntil = UnityEngine.Time.unscaledTime + 2.5f;
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            if (cine != null) { cine.Dispose(); cine = null; }
            GameManager.I.OnSplashFinished();
        }

        static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        float Card(float t0, float t1) => S((t - t0) / 0.6f) * (1f - S((t - (t1 - 0.6f)) / 0.6f));

        float Veil()
        {
            float v;
            if (t < RoomTime) v = 1f;
            else if (t < RoomTime + 1.4f) v = 1f - S((t - RoomTime) / 1.4f);
            else if (t >= 8.35f && t < 8.55f) v = 1f;                   // cut to the close-up
            else if (t >= BurstTime + 0.05f) v = 1f;                    // lights gone
            else v = 0f;
            if (t > SwapTime) v = 1f;
            if (skipStart >= 0f) v = Mathf.Max(v, Mathf.Clamp01((UnityEngine.Time.unscaledTime - skipStart) / SkipFade));
            return v;
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            var full = new Rect(-20, -20, W + 40, H + 40);
            UI.Tex(new Rect(0, 0, W, H), TextureFactory.Vignette, new Color(0, 0, 0, 0.9f));
            UI.Fill(full, new Color(0, 0, 0, Veil()));
            if (cine != null && cine.Flash > 0.01f) UI.Fill(full, new Color(0.85f, 1f, 0.95f, cine.Flash));
            UI.Tex(new Rect(0, 0, W, H), TextureFactory.Grain, Color.white);

            float bar = H * 0.12f;
            UI.Fill(new Rect(-20, -20, W + 40, bar + 20), Color.black);
            UI.Fill(new Rect(-20, H - bar, W + 40, bar + 20), Color.black);

            float a = Card(0.5f, RoomTime + 0.6f);
            if (a > 0f)
            {
                UI.Label(new Rect(0, H * 0.45f - 30, W, 60), "S O L A R E C L I P S E    G A M E S", 30, UI.Accent2.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Bold);
                UI.Label(new Rect(0, H * 0.45f + 26, W, 40), "presents", 20, new Color(1, 1, 1, 0.6f * a), TextAnchor.MiddleCenter, FontStyle.Italic);
            }
            Lower("CAUSE OF DEATH: UNKNOWN.", Card(4.2f, 7.4f), H, bar);
            DrawTitle(W, H);

            if (UnityEngine.Time.unscaledTime < armedUntil && skipStart < 0f)
            {
                var r = new Rect(W - 380, H - bar - 84, 350, 56);
                UI.Rounded(r, new Color(0, 0, 0, 0.6f));
                UI.Label(r, Settings.TouchControlsActive ? "Tap again to skip" : "Press again to skip", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            else if (t > 1f && t < 5f && skipStart < 0f)
                UI.Label(new Rect(W - 420, H - bar - 70, 390, 40), Settings.TouchControlsActive ? "tap twice to skip" : "press twice to skip", 18, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleRight, FontStyle.Italic, false);
        }

        void Lower(string text, float a, float H, float bar)
        {
            if (a <= 0f) return;
            UI.Label(new Rect(0, H - bar - 120 + (1f - a) * 12f, UI.VW, 50), text, 30, new Color(0.9f, 0.95f, 0.92f, a * 0.85f), TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void DrawTitle(float W, float H)
        {
            // Flatline across the screen first, then the title cuts in on the hit.
            if (t >= FlatTime && t < TitleTime + 2.4f)
            {
                float k = S((t - FlatTime) / 0.5f) * (1f - S((t - TitleTime - 1.6f) / 0.8f));
                UI.Fill(new Rect(0, H * 0.5f - 1.5f, W * Mathf.Clamp01((t - FlatTime) / 0.9f), 3), new Color(1f, 0.15f, 0.1f, 0.85f * k));
            }
            if (t < TitleTime || t > SwapTime + 0.2f) return;
            float u = t - TitleTime;
            float fade = 1f - S((t - (SwapTime - 0.5f)) / 0.5f);
            float alpha = Mathf.Clamp01(u / 0.05f) * fade;
            float hit = Mathf.Exp(-u * 4f);
            float scale = 1f + 0.06f * hit + 0.03f * Mathf.Clamp01(u / 3f);
            float shake = hit * 14f * Settings.Data.screenShake;
            var c = new Vector2(W / 2f, H * 0.46f);
            var off = new Vector2(Mathf.Sin(t * 91f), Mathf.Cos(t * 73f)) * shake * UI.Scale;
            UI.Tex(new Rect(c.x - 600, c.y - 300, 1200, 600), TextureFactory.Glow, new Color(0.75f, 0.02f, 0.02f, 0.45f * alpha));
            UI.Tex(new Rect(c.x - W * 0.7f, c.y - 22, W * 1.4f, 44), TextureFactory.Streak, new Color(1f, 0.12f, 0.08f, (0.2f + 0.8f * hit) * alpha));

            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(off, Quaternion.identity, Vector3.one);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(c.x * UI.Scale, c.y * UI.Scale) + off);
            const string title = "GORE SANDBOX";
            var r = new Rect(-200, c.y - 95, W + 400, 190);
            for (int g = 0; g < 4; g++)
            {
                var o = new Vector2(Mathf.Cos(g * 1.57f), Mathf.Sin(g * 1.57f)) * 5f;
                UI.Label(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), title, 140, new Color(0.9f, 0.05f, 0.05f, 0.25f * alpha), TextAnchor.MiddleCenter, FontStyle.Normal, false, false, UI.HeavyFont);
            }
            UI.Label(r, title, 140, new Color(0.95f, 0.93f, 0.9f, alpha), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            // Blood running down from the letters.
            float tw = UI.TextWidth(title, 140, FontStyle.Normal, UI.HeavyFont);
            for (int i = 0; i < drips.Length; i++)
            {
                float s = drips[i];
                float len = (10f + 90f * S((u - s * 0.8f) / (1.5f + s))) * alpha;
                float dx = c.x - tw / 2f + 10 + s * (tw - 20);
                float dw = 3f + (s * 17f % 6f);
                UI.Rounded(new Rect(dx, c.y + 58, dw, len), new Color(0.55f, 0.02f, 0.03f, 0.95f * alpha), true);
            }
            GUI.matrix = m;
            // The "2", stamped in red.
            float two = S((u - 0.35f) / 0.25f) * fade;
            if (two > 0f)
            {
                float s2 = 1f + (1f - two) * 0.8f;
                var r2 = new Rect(c.x + tw / 2f - 40, c.y - 150 * s2, 260 * s2, 300 * s2);
                UI.Label(r2, "2", Mathf.RoundToInt(260 * s2), new Color(0.85f, 0.05f, 0.05f, two), TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            }
            float sub = S((u - 0.8f) / 0.6f) * fade;
            if (sub > 0f)
            {
                UI.Fill(new Rect(W / 2 - 330 * sub, c.y + 160, 660 * sub, 3), UI.Accent.WithAlpha(0.9f * sub));
                UI.Label(new Rect(0, c.y + 170, W, 50), "v" + GameInfo.Version, 26, UI.Accent2.WithAlpha(sub), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }
    }
}
