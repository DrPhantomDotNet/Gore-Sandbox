using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Letterbox bars + caption over a story cutscene. Tap / click skips the current shot.</summary>
    public sealed class CutsceneScreen : UIScreen
    {
        public string Caption;
        public float CaptionAt;
        public bool SkipShot;

        public override bool IsModal => false;
        public override bool ShowCursor => false;
        public override bool BlocksGameplay => true;

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            float bars = MathX.SmoothStep01(Age / 0.5f) * 120f;
            UI.Fill(new Rect(0, 0, W, bars), Color.black);
            UI.Fill(new Rect(0, H - bars, W, bars), Color.black);
            if (!string.IsNullOrEmpty(Caption))
            {
                float a = Mathf.Clamp01((Time.unscaledTime - CaptionAt) / 0.5f);
                UI.Label(new Rect(4, H - 112, W, 100), Caption, 34, new Color(0, 0, 0, 0.8f * a), TextAnchor.MiddleCenter, FontStyle.Bold, true, true);
                UI.Label(new Rect(0, H - 115, W, 100), Caption, 34, new Color(1f, 0.92f, 0.75f, a), TextAnchor.MiddleCenter, FontStyle.Bold, true, true);
            }
            if (Age > 0.8f) UI.Label(new Rect(W - 300, 30, 270, 60), Settings.TouchControlsActive ? "TAP TO SKIP" : "CLICK TO SKIP", 18, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleRight, FontStyle.Bold);
            if (Age > 0.4f && UI.Hit(new Rect(0, 0, W, H))) SkipShot = true;
        }

        public override bool OnBack() { SkipShot = true; return true; }
    }

    /// <summary>Fades to black, holds (with an optional caption) and fades back.</summary>
    public sealed class StoryFadeScreen : UIScreen
    {
        readonly float hold;
        readonly string caption;
        public bool Done { get; private set; }
        public StoryFadeScreen(float seconds, string caption) { hold = Mathf.Max(0.3f, seconds); this.caption = caption; }
        public override bool IsModal => true;
        public override bool ShowCursor => false;
        public override bool BlocksGameplay => true;

        public override void Tick()
        {
            if (!Done && Age > hold + 1.2f) { Done = true; UIRoot.Remove(this); }
        }

        public override void Draw()
        {
            float t = Age;
            float a = t < 0.6f ? t / 0.6f : t < 0.6f + hold ? 1f : 1f - (t - 0.6f - hold) / 0.6f;
            a = Mathf.Clamp01(a);
            UI.Fill(new Rect(0, 0, UI.VW, UI.VH), new Color(0, 0, 0, a));
            if (!string.IsNullOrEmpty(caption))
                UI.Label(new Rect(60, UI.VH * 0.4f, UI.VW - 120, 160), caption, 44, new Color(1f, 0.92f, 0.75f, a * Mathf.Clamp01((t - 0.5f) / 0.4f)), TextAnchor.MiddleCenter, FontStyle.Bold, true, true);
        }

        public override bool OnBack() => true;
    }

    /// <summary>"THE END": the ending's name and text, then the next thing (credits, menu).</summary>
    public sealed class TheEndScreen : UIScreen
    {
        readonly string ending, text, footer;
        readonly Action then;
        readonly Typer typer = new Typer { Sound = "story_talk", Volume = 0.35f };
        bool left, typing;

        public TheEndScreen(string ending, string text, string footer, Action then)
        {
            this.ending = string.IsNullOrEmpty(ending) ? "" : ending.ToUpperInvariant();
            this.text = ChatFilter.Active ? ChatFilter.Apply(text ?? "") : text ?? "";
            this.footer = footer;
            this.then = then;
        }

        public override bool IsModal => true;
        public override bool ShowCursor => true;
        public override bool BlocksGameplay => true;

        public override void OnOpen()
        {
            AudioManager.StopMusic();
            AudioManager.Play2D("story_passed", 0.8f, 0.8f, AudioBus.Ui);
        }

        public override void Tick()
        {
            if (!typing && Age > 2.1f) { typing = true; typer.Set(text, 40f); }
            if (typing) typer.Update();
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH, t = Age;
            UI.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, MathX.SmoothStep01(t / 1.2f)));
            float a = Mathf.Clamp01((t - 0.8f) / 0.6f);
            UI.Label(new Rect(0, H * 0.16f, W, 170), "THE END", 150, new Color(1f, 0.78f, 0.16f, a), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            if (ending.Length > 0) UI.Label(new Rect(0, H * 0.16f + 170, W, 60), "ENDING:  " + ending, 34, new Color(1, 1, 1, Mathf.Clamp01((t - 1.5f) / 0.5f)), TextAnchor.MiddleCenter, FontStyle.Bold);
            if (t > 2.2f)
            {
                string vis = typer.Visible;
                UI.Label(new Rect(W / 2f - 700, H * 0.47f, 1400, 230), vis, 32, new Color(0.9f, 0.9f, 0.9f, 1f), TextAnchor.UpperCenter, FontStyle.Normal, true, true);
            }
            if (!string.IsNullOrEmpty(footer) && typing && typer.Done && t > 2.4f)
                UI.Label(new Rect(0, H * 0.75f, W, 70), footer, 46, new Color(1f, 0.78f, 0.16f, Mathf.Clamp01(typer.SinceDone / 0.6f)), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            if (t > 2.4f && typing && typer.Done && !left)
            {
                if (UI.Button(new Rect(W / 2f - 180, H - 160, 360, 96), "CONTINUE", ButtonKind.Primary, null, 34)) Leave();
            }
            else if (t > 2.4f && !typer.Done && UI.Hit(new Rect(0, 0, W, H))) typer.Skip();
        }

        void Leave()
        {
            if (left) return;
            left = true;
            AudioManager.Ui("ui_click");
            UIRoot.Remove(this);
            then?.Invoke();
        }

        public override bool OnBack() { if (typer.Done) Leave(); else typer.Skip(); return true; }
    }

    /// <summary>Scrolling end credits. Tap to speed up; skippable at the end.</summary>
    public sealed class CreditsRollScreen : UIScreen
    {
        readonly string[] lines;
        readonly Action then;
        float scroll;
        bool left;

        public CreditsRollScreen(string[] lines, Action then) { this.lines = lines; this.then = then; }
        public override bool IsModal => true;
        public override bool ShowCursor => true;
        public override bool BlocksGameplay => true;

        public override void OnOpen() { AudioManager.PlayMusic("intro_score", 0.6f); }

        float Height => lines.Length * 62f + UI.VH;

        public override void Tick()
        {
            bool fast = Input.GetMouseButton(0) || Input.touchCount > 0;
            scroll += Time.unscaledDeltaTime * (fast ? 360f : 85f);
            if (scroll > Height + 100f) Leave();
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            UI.Fill(new Rect(0, 0, W, H), Color.black);
            float y = H - scroll;
            foreach (var raw in lines)
            {
                if (y > -70 && y < H + 10)
                {
                    bool title = raw.StartsWith("#"), head = raw.StartsWith("*");
                    string s = title || head ? raw.Substring(1) : raw;
                    UI.Label(new Rect(0, y, W, 60), s, title ? 64 : head ? 26 : 34, title ? new Color(1f, 0.78f, 0.16f) : head ? UI.Muted : Color.white, TextAnchor.MiddleCenter, title ? FontStyle.Normal : head ? FontStyle.Bold : FontStyle.Normal, true, false, title ? UI.HeavyFont : null);
                }
                y += raw.StartsWith("#") ? 90f : 62f;
            }
            UI.Label(new Rect(W - 380, H - 70, 360, 50), Settings.TouchControlsActive ? "HOLD TO SPEED UP" : "HOLD CLICK TO SPEED UP", 18, new Color(1, 1, 1, 0.4f), TextAnchor.MiddleRight, FontStyle.Bold);
            if (Age > 3f && UI.Button(new Rect(30, H - 90, 200, 64), "SKIP", ButtonKind.Ghost, null, 22)) Leave();
        }

        void Leave()
        {
            if (left) return;
            left = true;
            AudioManager.StopMusic();
            UIRoot.Remove(this);
            then?.Invoke();
        }

        public override bool OnBack() { Leave(); return true; }
    }
}
