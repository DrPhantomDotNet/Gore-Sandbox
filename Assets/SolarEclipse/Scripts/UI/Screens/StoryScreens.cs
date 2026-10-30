using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Story dialogue box: speaker name, typewriter text and big touch-friendly choice buttons along the bottom of the
    /// screen. Blocks player control while open (time keeps running). Without choices it closes itself after a
    /// moment, or when tapped. Lines go through the profanity filter when it is on.
    /// </summary>
    public sealed class StoryDialog : UIScreen
    {
        readonly string speaker, text;
        readonly string[] options;
        readonly Action<int> onChoice;
        readonly float hold;
        readonly Color nameColor;
        readonly Typer typer = new Typer { Sound = "story_talk", Volume = 0.45f };
        bool chosen;
        int lastFrame = -1;
        public bool Closed { get; private set; }

        public StoryDialog(string speaker, string text, string[] options, Action<int> onChoice, Color? nameColor = null, float hold = 2.4f)
        {
            this.speaker = speaker;
            this.text = ChatFilter.Active ? ChatFilter.Apply(text) : text;
            this.options = options;
            this.onChoice = onChoice;
            this.hold = hold;
            this.nameColor = nameColor ?? UI.Accent2;
        }

        public override bool IsModal => true;
        public override bool ShowCursor => true;
        public override bool BlocksGameplay => true;

        public override void OnOpen() { typer.Set(text, 34f); AudioManager.Ui("ui_click", 0.4f); }

        void Close(int choice)
        {
            if (Closed) return;
            Closed = true;
            UIRoot.Remove(this);
            onChoice?.Invoke(choice);
        }

        public override void Draw()
        {
            if (Time.frameCount != lastFrame && Event.current.type == EventType.Repaint)
            {
                lastFrame = Time.frameCount;
                typer.Update();
                if (options == null && typer.Done && typer.SinceDone > hold) Close(0);
            }
            if (Closed) return;
            float W = UI.VW, H = UI.VH, t = Time.unscaledTime;
            float a = Intro(0, 0.25f);
            UI.Gradient(new Rect(0, H * 0.5f, W, H * 0.5f), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.75f * a));
            float bh = options != null ? 330 : 230;
            var r = new Rect(W / 2f - 640, H - bh - 36 + (1f - a) * 40f, 1280, bh);
            UI.Rounded(r, new Color(0.03f, 0.03f, 0.04f, 0.92f * a));
            UI.Outline(r, new Color(1f, 1f, 1f, 0.15f * a), 2);
            // Speaker tab
            float nw = Mathf.Max(220f, UI.TextWidth(speaker, 30, FontStyle.Bold) + 60f);
            var tab = new Rect(r.x + 30, r.y - 30, nw, 56);
            UI.Rounded(tab, nameColor.WithAlpha(0.95f * a), true);
            UI.Label(tab, speaker.ToUpperInvariant(), 30, Color.black.WithAlpha(a), TextAnchor.MiddleCenter, FontStyle.Bold, false);

            string vis = typer.Visible + (typer.Done ? "" : (Mathf.FloorToInt(t * 6f) % 2 == 0 ? "_" : " "));
            UI.Label(new Rect(r.x + 44, r.y + 40, r.width - 88, 120), vis, 38, UI.TextColor.WithAlpha(a), TextAnchor.UpperLeft, FontStyle.Normal, true, true);

            if (options != null)
            {
                if (typer.Done && !chosen)
                {
                    int n = options.Length;
                    float bw = 360, gap = 40, total = n * bw + (n - 1) * gap;
                    for (int i = 0; i < n; i++)
                    {
                        var br = new Rect(r.center.x - total / 2f + i * (bw + gap), r.yMax - 128, bw, 100);
                        if (UI.Button(br, options[i], i == 0 ? ButtonKind.Primary : ButtonKind.Normal, null, 40))
                        {
                            chosen = true;
                            AudioManager.Ui("ui_click");
                            Close(i);
                            break;
                        }
                    }
                }
                else if (!typer.Done && UI.Hit(r)) typer.Skip();
            }
            else
            {
                if (typer.Done) UI.Label(new Rect(r.x, r.yMax - 52, r.width - 40, 40), Settings.TouchControlsActive ? "TAP TO CONTINUE" : "CLICK TO CONTINUE", 20, UI.Muted.WithAlpha(a * (0.6f + 0.4f * Mathf.Sin(t * 4f))), TextAnchor.MiddleRight, FontStyle.Bold);
                if (UI.Hit(new Rect(0, 0, W, H))) { if (typer.Done) Close(0); else typer.Skip(); }
            }
        }

        public override bool OnBack()
        {
            if (options == null) { if (typer.Done) Close(0); else typer.Skip(); }
            return true; // a choice has to be made
        }
    }

    // ====================================================================== MISSION PASSED

    /// <summary>GTA-style "MISSION PASSED": big title punch-in, subtitle, sting; then fades to black and goes back to the
    /// main menu, where Orbie says the chapter ending line.</summary>
    public sealed class MissionPassedScreen : UIScreen
    {
        readonly string subtitle, line;
        readonly Action then;
        const float FadeAt = 5.2f, FadeTime = 1.2f;
        bool left;

        /// <param name="then">What happens after the fade (null = main menu + Orbie's line, the old chapter ending).</param>
        public MissionPassedScreen(string subtitle, string line = "CHAPTER 1 COMPLETE  -  RESPECT +", Action then = null) { this.subtitle = subtitle; this.line = line; this.then = then; }

        public override bool IsModal => true;
        public override bool ShowCursor => false;
        public override bool BlocksGameplay => true;

        public override void OnOpen()
        {
            AudioManager.StopMusic();
            AudioManager.Play2D("story_passed", 1f, 1f, AudioBus.Ui);
            CameraFX.Shake(0.25f);
        }

        public override void Tick()
        {
            if (!left && Age > FadeAt + FadeTime + 0.15f)
            {
                left = true;
                UIRoot.Remove(this);
                if (then != null) { then(); return; }
                GameManager.I.ReturnToMenu();
                UIRoot.Push(new StoryOrbieScreen("That's it for the story for now.. there will be more but it takes a lot of time to make this."));
            }
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH, t = Age;
            // Letterbox bars slide in.
            float bars = MathX.SmoothStep01(t / 0.6f) * 110f;
            UI.Fill(new Rect(0, 0, W, bars), Color.black);
            UI.Fill(new Rect(0, H - bars, W, bars), Color.black);
            UI.Gradient(new Rect(0, H * 0.25f, W, H * 0.5f), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.0f));
            UI.Fill(new Rect(0, H * 0.33f, W, H * 0.3f), new Color(0, 0, 0, 0.35f * MathX.SmoothStep01(t / 0.4f)));

            // Title: punches in big, settles.
            float k = MathX.SmoothStep01((t - 0.1f) / 0.35f);
            float a = Mathf.Clamp01((t - 0.1f) / 0.15f) * (1f - Mathf.Clamp01((t - FadeAt) / 0.6f));
            int size = Mathf.RoundToInt(Mathf.Lerp(190f, 130f, k));
            var gold = new Color(1f, 0.78f, 0.16f, a);
            var titleR = new Rect(0, H * 0.36f, W, 170);
            UI.Label(new Rect(titleR.x + 5, titleR.y + 6, titleR.width, titleR.height), "MISSION PASSED", size, new Color(0, 0, 0, 0.8f * a), TextAnchor.MiddleCenter, FontStyle.Normal, false, false, UI.HeavyFont);
            UI.Label(titleR, "MISSION PASSED", size, gold, TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);

            float sa = Mathf.Clamp01((t - 0.8f) / 0.4f) * (1f - Mathf.Clamp01((t - FadeAt) / 0.6f));
            UI.Label(new Rect(0, H * 0.36f + 168, W, 60), subtitle, 40, new Color(1f, 1f, 1f, sa), TextAnchor.MiddleCenter, FontStyle.Bold);
            float ca = Mathf.Clamp01((t - 1.4f) / 0.4f) * (1f - Mathf.Clamp01((t - FadeAt) / 0.6f));
            UI.Label(new Rect(0, H * 0.36f + 230, W, 44), line, 26, new Color(0.85f, 0.85f, 0.85f, ca), TextAnchor.MiddleCenter, FontStyle.Bold);

            // Fade to black.
            float fade = Mathf.Clamp01((t - FadeAt) / FadeTime);
            if (fade > 0f) UI.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, fade));
        }

        public override bool OnBack() => true;
    }

    // ====================================================================== MISSION FAILED

    public sealed class MissionFailedScreen : UIScreen
    {
        readonly string reason;
        readonly Action retry;
        /// <summary>Extra action after MENU (the story maker uses it to reopen the editor).</summary>
        public Action OnMenu;

        public MissionFailedScreen(string reason, Action retry) { this.reason = reason; this.retry = retry; }

        public override bool IsModal => true;
        public override bool ShowCursor => true;
        public override bool BlocksGameplay => true;

        public override void OnOpen()
        {
            AudioManager.StopMusic();
            AudioManager.Play2D("story_failed", 1f, 1f, AudioBus.Ui);
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH, t = Age;
            float a = MathX.SmoothStep01(t / 0.5f);
            UI.Fill(new Rect(0, 0, W, H), new Color(0.12f, 0f, 0f, 0.72f * a));
            UI.Tex(new Rect(0, 0, W, H), TextureFactory.Vignette, new Color(0, 0, 0, 0.9f * a));
            float k = MathX.SmoothStep01((t - 0.1f) / 0.35f);
            int size = Mathf.RoundToInt(Mathf.Lerp(170f, 124f, k));
            var tr = new Rect(0, H * 0.28f, W, 170);
            UI.Label(new Rect(tr.x + 5, tr.y + 6, tr.width, tr.height), "MISSION FAILED", size, new Color(0, 0, 0, 0.8f * a), TextAnchor.MiddleCenter, FontStyle.Normal, false, false, UI.HeavyFont);
            UI.Label(tr, "MISSION FAILED", size, new Color(0.95f, 0.12f, 0.1f, a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            float sa = Mathf.Clamp01((t - 0.6f) / 0.4f);
            UI.Label(new Rect(W / 2f - 700, H * 0.28f + 175, 1400, 70), ChatFilter.Active ? ChatFilter.Apply(reason) : reason, 38, new Color(1f, 1f, 1f, sa), TextAnchor.MiddleCenter, FontStyle.Bold, true, true);
            if (t < 0.9f) return;
            float bw = 380, gap = 50;
            var r1 = new Rect(W / 2f - bw - gap / 2f, H * 0.66f, bw, 104);
            var r2 = new Rect(W / 2f + gap / 2f, H * 0.66f, bw, 104);
            if (UI.Button(r1, "RETRY", ButtonKind.Primary, IconFactory.Get("play"), 40)) { AudioManager.Ui("ui_click"); UIRoot.Remove(this); retry?.Invoke(); return; }
            if (UI.Button(r2, "MENU", ButtonKind.Normal, IconFactory.Get("exit"), 40)) { AudioManager.Ui("ui_click"); UIRoot.Remove(this); GameManager.I.ReturnToMenu(); OnMenu?.Invoke(); }
        }

        public override bool OnBack() => true;
    }

    // ====================================================================== Orbie (end of chapter)

    /// <summary>Orbie on the main menu after the chapter: one line, one OKAY button.</summary>
    public sealed class StoryOrbieScreen : UIScreen
    {
        readonly string line;
        readonly Typer typer = new Typer();
        bool leaving;
        float leaveAt;

        public StoryOrbieScreen(string line) { this.line = ChatFilter.Active ? ChatFilter.Apply(line) : line; }
        public override bool IsModal => true;

        public override void OnOpen()
        {
            AudioManager.Ui("logo_shimmer", 0.6f);
            typer.Set(line, 30f);
        }

        public override void Tick()
        {
            if (Age > 0.6f) typer.Update();
            if (leaving && Time.unscaledTime - leaveAt > 0.45f) { leaving = false; UIRoot.Remove(this); }
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH, t = Time.unscaledTime;
            float a = Intro(0, 0.5f) * (leaving ? 1f - Mathf.Clamp01((t - leaveAt) / 0.4f) : 1f);
            UI.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.82f * a));
            float bob = Mathf.Sin(t * 1.6f) * 14f;
            float size = 300f * (1f + Mathf.Sin(t * 2.3f) * 0.04f);
            var c = new Vector2(W / 2f, H * 0.34f + bob);
            UI.Tex(new Rect(c.x - size * 1.4f, c.y - size * 1.4f, size * 2.8f, size * 2.8f), BossArt.Glow(), new Color(0.75f, 0.85f, 1f, 0.22f * a));
            UI.Tex(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), BossArt.Orbie(), new Color(1, 1, 1, a));
            UI.Label(new Rect(0, c.y + size * 0.36f, W, 30), "ORBIE", 18, new Color(0.8f, 0.9f, 1f, 0.7f * a), TextAnchor.MiddleCenter, FontStyle.Bold);

            var box = new Rect(W / 2f - 560, H * 0.58f, 1120, 270);
            UI.Rounded(box, new Color(0.03f, 0.04f, 0.07f, 0.92f * a));
            UI.Outline(box, new Color(0.8f, 0.9f, 1f, 0.45f * a), 2);
            string vis = Age > 0.6f ? typer.Visible + (typer.Done ? "" : (Mathf.FloorToInt(t * 6f) % 2 == 0 ? "_" : " ")) : "";
            UI.Label(new Rect(box.x + 40, box.y + 20, box.width - 80, 130), vis, 34, new Color(0.93f, 0.97f, 1f, a), TextAnchor.MiddleCenter, FontStyle.Normal, true, true);
            if (typer.Done && !leaving)
            {
                if (UI.Button(new Rect(W / 2f - 160, box.yMax - 104, 320, 84), "OKAY", ButtonKind.Primary, null, 32)) Leave();
            }
            else if (!leaving && Age > 0.6f && UI.Hit(box)) typer.Skip();
        }

        void Leave() { leaving = true; leaveAt = Time.unscaledTime; AudioManager.Ui("ui_close"); }

        public override bool OnBack() { if (!leaving) Leave(); return true; }
    }
}
