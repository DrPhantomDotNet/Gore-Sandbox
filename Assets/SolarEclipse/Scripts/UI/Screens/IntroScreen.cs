using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// ~21 s startup cinematic between the studio splash and the main menu (3D in Game/IntroCinematic.cs, score in
    /// Audio/RecipesIntro.cs). This screen owns the clock and draws the film layer: fades, letterbox bars, title
    /// cards, the GORE SANDBOX logo slam and the skip prompt. Tap/click/any key once shows "Tap again to skip";
    /// a second press within 2.5 s fades to black and jumps to the crane-down into the menu (about 2 s).
    /// Settings > Interface > "Play intro on startup" turns it off. Ends with GameManager.OnSplashFinished() exactly
    /// like the splash used to (setup screen / update check follow).
    /// </summary>
    public sealed class IntroScreen : UIScreen
    {
        public static bool ShouldPlay => Settings.Data.playIntro && GameManager.I != null && SoundBank.Ready;

        const float SkipFade = 0.35f;
        const float ShotTime = IntroCinematic.ShotTime, BlackTime = IntroCinematic.BlackTime, LogoTime = IntroCinematic.LogoTime,
            SwapTime = IntroCinematic.SwapTime, EndTime = IntroCinematic.EndTime;

        IntroCinematic cine;
        float t, armedUntil = -1f, skipStart = -1f;
        int lastPressFrame = -1;
        bool finished;

        public override void OnOpen() => cine = IntroCinematic.Create();

        public override void OnClose()
        {
            if (cine != null) { cine.Dispose(); cine = null; }
        }

        public override bool OnBack() { Press(); return true; }

        public override void Tick()
        {
            if (finished || cine == null) return;
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)) Press();

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float prev = t;
            if (skipStart >= 0f && Time.unscaledTime - skipStart >= SkipFade)
            {
                // Jump straight to the crane-down into the menu (behind a black frame, so no visible cut).
                skipStart = -1f;
                cine.StopAudio();
                cine.SwapToMenu();
                prev = t = SwapTime;
            }
            t += dt;
            cine.Step(t, prev, dt);
            if (t >= EndTime) Finish();
        }

        void Press()
        {
            if (Time.frameCount == lastPressFrame) return; // Esc arrives both as a key and as "back"
            lastPressFrame = Time.frameCount;
            if (Age < 0.3f || skipStart >= 0f || t >= SwapTime - 0.4f) return;
            if (Time.unscaledTime < armedUntil) { skipStart = Time.unscaledTime; armedUntil = -1f; }
            else armedUntil = Time.unscaledTime + 2.5f;
        }

        void Finish()
        {
            finished = true;
            if (cine != null) { cine.Dispose(); cine = null; }
            GameManager.I.OnSplashFinished();
        }

        // ------------------------------------------------------------------ drawing

        static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        float Veil()
        {
            float v;
            if (t < 1.6f) v = 1f - S(t / 1.6f);
            else if (t < BlackTime - 0.5f) v = 0f;
            else if (t < BlackTime) v = S((t - (BlackTime - 0.5f)) / 0.5f);
            else if (t < LogoTime) v = 1f;
            else if (t < SwapTime - 0.5f) v = Mathf.Lerp(1f, 0.5f, S((t - LogoTime) / 0.12f));
            else if (t < SwapTime) v = Mathf.Lerp(0.5f, 1f, S((t - (SwapTime - 0.5f)) / 0.5f));
            else v = 1f - S((t - SwapTime) / 1.0f);
            if (skipStart >= 0f) v = Mathf.Max(v, Mathf.Clamp01((Time.unscaledTime - skipStart) / SkipFade));
            return v;
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            var full = new Rect(-20, -20, W + 40, H + 40);
            float after = S((t - SwapTime) / 1.4f);

            UI.Tex(new Rect(0, 0, W, H), TextureFactory.Vignette, new Color(0, 0, 0, 0.85f * (1f - after)));
            UI.Fill(full, new Color(0, 0, 0, Veil()));
            if (cine != null && cine.Flash > 0.01f) UI.Fill(full, new Color(1f, 0.97f, 0.92f, cine.Flash));

            // Letterbox (retracts while the camera settles into the menu).
            float bar = H * 0.115f * (1f - S((t - (SwapTime + 0.5f)) / 1.2f));
            if (bar > 0.5f)
            {
                UI.Fill(new Rect(-20, -20, W + 40, bar + 20), Color.black);
                UI.Fill(new Rect(-20, H - bar, W + 40, bar + 20), Color.black);
            }

            // Title cards
            float a = Card(0.8f, 3.9f);
            if (a > 0f)
            {
                UI.Label(new Rect(0, H * 0.44f - 30, W, 60), "S O L A R E C L I P S E    G A M E S", 30, UI.Accent2.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Bold);
                UI.Label(new Rect(0, H * 0.44f + 26, W, 40), "presents", 20, new Color(1, 1, 1, 0.6f * a), TextAnchor.MiddleCenter, FontStyle.Italic);
            }
            Lower("EVERY BONE CAN BREAK.", Card(4.5f, 6.6f), H, bar);
            Lower("EVERY WOUND TELLS A STORY.", Card(6.8f, 8.7f), H, bar);

            DrawLogo(W, H);

            // Skip prompt
            if (Time.unscaledTime < armedUntil && skipStart < 0f)
            {
                var r = new Rect(W - 380, H - bar - 84, 350, 56);
                UI.Rounded(r, new Color(0, 0, 0, 0.6f));
                UI.Label(r, Settings.TouchControlsActive ? "Tap again to skip" : "Press again to skip", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            else if (t > 1.2f && t < 5.5f && skipStart < 0f)
                UI.Label(new Rect(W - 420, H - bar - 70, 390, 40), Settings.TouchControlsActive ? "tap twice to skip" : "press twice to skip", 18, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleRight, FontStyle.Italic, false);
        }

        float Card(float t0, float t1) => S((t - t0) / 0.6f) * (1f - S((t - (t1 - 0.6f)) / 0.6f));

        void Lower(string text, float a, float H, float bar)
        {
            if (a <= 0f) return;
            float rise = (1f - a) * 12f;
            UI.Label(new Rect(0, H - bar - 140 + rise, UI.VW, 60), text, 36, new Color(0.95f, 0.93f, 0.9f, a), TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        void DrawLogo(float W, float H)
        {
            if (t < LogoTime || t > SwapTime + 0.1f) return;
            float u = t - LogoTime;
            float fade = 1f - S((t - (SwapTime - 0.45f)) / 0.45f);
            float slam = Mathf.Clamp01(u / 0.14f);
            slam = slam * slam; // accelerates into the hit
            float alpha = Mathf.Clamp01(u / 0.06f) * fade;
            float scale = Mathf.Lerp(1.7f, 1f, slam) * (1f + 0.05f * Mathf.Clamp01(u / (SwapTime - LogoTime)));
            float shake = Mathf.Exp(-u * 4f) * 16f * Settings.Data.screenShake * (u > 0.14f ? 1f : 0f);
            var offset = new Vector2(Mathf.Sin(t * 91f) * shake, Mathf.Cos(t * 73f) * shake) * UI.Scale;
            var c = new Vector2(W / 2f, H * 0.45f);

            // Flash + anamorphic streak on impact
            float hit = u > 0.12f ? Mathf.Exp(-(u - 0.12f) * 5f) : 0f;
            if (hit > 0.01f) UI.Fill(new Rect(-20, -20, W + 40, H + 40), new Color(1f, 0.9f, 0.85f, 0.6f * hit));
            UI.Tex(new Rect(c.x - W * 0.7f, c.y - 26, W * 1.4f, 52), TextureFactory.Streak, new Color(1f, 0.15f, 0.1f, (0.25f + 0.75f * hit) * alpha));
            UI.Tex(new Rect(c.x - 520, c.y - 260, 1040, 520), TextureFactory.Glow, new Color(0.7f, 0.02f, 0.02f, 0.35f * alpha));

            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(c.x * UI.Scale, c.y * UI.Scale) + offset);
            var r = new Rect(-200, c.y - 100, W + 400, 200);
            for (int g = 0; g < 4; g++)
            {
                var o = new Vector2(Mathf.Cos(g * 1.57f), Mathf.Sin(g * 1.57f)) * 5f;
                UI.Label(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), GameInfo.Title, 150, new Color(0.9f, 0.05f, 0.05f, 0.22f * alpha), TextAnchor.MiddleCenter, FontStyle.Normal, false, false, UI.HeavyFont);
            }
            UI.Label(r, GameInfo.Title, 150, new Color(0.96f, 0.94f, 0.9f, alpha), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            GUI.matrix = m;

            float line = S((u - 0.2f) / 0.5f) * fade;
            if (line > 0f) UI.Fill(new Rect(W / 2 - 330 * line, c.y + 92, 660 * line, 3), UI.Accent.WithAlpha(0.9f * line));
            float sub = S((u - 0.45f) / 0.6f) * fade;
            if (sub > 0f)
                UI.Label(new Rect(0, c.y + 100, W, 60), GameInfo.EarlyAccess ? "E A R L Y    A C C E S S" : "v" + GameInfo.Version, 30, UI.Accent2.WithAlpha(sub), TextAnchor.MiddleCenter, FontStyle.Bold);
        }
    }
}
