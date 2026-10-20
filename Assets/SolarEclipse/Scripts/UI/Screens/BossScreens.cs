using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Typewriter helper: reveals text over time and plays a blip per character.</summary>
    public sealed class Typer
    {
        public string Text = "";
        public float Cps = 32f;
        public string Sound = "orbie_blip";
        public float Volume = 0.6f;
        float start;
        int lastShown;

        public void Set(string text, float cps = 32f) { Text = text; Cps = cps; start = Time.unscaledTime; lastShown = 0; }
        public int Shown => Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - start) * Cps), 0, Text.Length);
        public bool Done => Shown >= Text.Length;
        public float SinceDone => Done ? Time.unscaledTime - start - Text.Length / Cps : 0f;
        public void Skip() => start = Time.unscaledTime - Text.Length / Cps - 0.01f;

        /// <summary>Call once per frame (from Tick). Plays the typing sound.</summary>
        public void Update()
        {
            int s = Shown;
            if (s > lastShown)
            {
                char c = Text[s - 1];
                if (c != ' ' && c != '.' && (s % 2 == 0 || Cps < 20f)) AudioManager.Play2D(Sound, Volume, UnityEngine.Random.Range(0.92f, 1.08f), AudioBus.Ui);
                lastShown = s;
            }
        }

        public string Visible => Text.Substring(0, Shown);
    }

    // ====================================================================== Orbie (map select gate)

    /// <summary>Orbie, the glowing white orb that guards the "????" map on the map select screen.</summary>
    public sealed class OrbieScreen : UIScreen
    {
        sealed class Step { public string Text; public string[] Options; public Action<int> Next; public float Hold = 1.3f; public float Cps = 30f; }

        readonly MapDef map;
        readonly Typer typer = new Typer();
        Step step;
        bool leaving;
        float leaveAt;
        Action afterLeave;

        public OrbieScreen(MapDef map) { this.map = map; }
        public override bool IsModal => true;

        public override void OnOpen()
        {
            AudioManager.Ui("logo_shimmer", 0.6f);
            Go(new Step { Text = "Are you sure you want to enter this map?", Options = new[] { "Yes", "No" }, Next = c => { if (c == 0) Yes(); else No(); } });
        }

        void Go(Step s) { step = s; typer.Set(s.Text, s.Cps); }

        void Yes()
        {
            Go(new Step
            {
                Text = "Okay.. Well I warned you.",
                Next = _ => Go(new Step
                {
                    Text = "...", Cps = 2.5f, Hold = 0.8f,
                    Next = __ =>
                    {
                        int kills = Mathf.FloorToInt(Stats.Get(Stats.Kills));
                        if (kills < 10)
                            Go(new Step { Text = "You need to kill at least 10 goredolls to unlock this map.  (" + kills + "/10)", Options = new[] { "Okay" }, Next = ___ => Close(null) });
                        else
                            Go(new Step { Text = "So you have blood on your hands, you may enter.", Hold = 1.6f, Next = ___ => Close(() => GameManager.I.StartMap(map, new MapOptions { Time = TimeOfDay.Night, StartingNpcs = 0, Loadout = 0 })) });
                    },
                }),
            });
        }

        void No()
        {
            Go(new Step
            {
                Text = "Okay well.. Goodbye!",
                Next = _ => Close(() =>
                {
                    UIRoot.Replace(new MainMenuScreen());
                    Stats.UnlockById("scaredy_cat");
                }),
            });
        }

        void Close(Action then)
        {
            leaving = true;
            leaveAt = Time.unscaledTime;
            afterLeave = then;
            AudioManager.Ui("ui_close");
        }

        public override void Tick()
        {
            typer.Update();
            if (leaving)
            {
                if (Time.unscaledTime - leaveAt > 0.45f)
                {
                    leaving = false;
                    UIRoot.Remove(this);
                    afterLeave?.Invoke();
                }
                return;
            }
            if (step != null && step.Options == null && typer.Done && typer.SinceDone > step.Hold)
            {
                var s = step; step = null;
                s.Next?.Invoke(0);
            }
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            float a = Intro(0, 0.4f) * (leaving ? 1f - Mathf.Clamp01((Time.unscaledTime - leaveAt) / 0.4f) : 1f);
            UI.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.82f * a));
            float t = Time.unscaledTime;
            // Orbie
            float bob = Mathf.Sin(t * 1.6f) * 14f;
            float pulse = 1f + Mathf.Sin(t * 2.3f) * 0.04f;
            float size = 300f * pulse;
            var c = new Vector2(W / 2f, H * 0.36f + bob);
            UI.Tex(new Rect(c.x - size * 1.4f, c.y - size * 1.4f, size * 2.8f, size * 2.8f), BossArt.Glow(), new Color(0.75f, 0.85f, 1f, 0.22f * a));
            UI.Tex(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), BossArt.Orbie(), new Color(1, 1, 1, a));
            UI.Label(new Rect(0, c.y + size * 0.36f, W, 30), "ORBIE", 18, new Color(0.8f, 0.9f, 1f, 0.7f * a), TextAnchor.MiddleCenter, FontStyle.Bold);

            // Speech box
            var box = new Rect(W / 2f - 520, H * 0.6f, 1040, 230);
            UI.Rounded(box, new Color(0.03f, 0.04f, 0.07f, 0.92f * a));
            UI.Outline(box, new Color(0.8f, 0.9f, 1f, 0.45f * a), 2);
            if (step != null)
            {
                string vis = typer.Visible + (typer.Done ? "" : (Mathf.FloorToInt(t * 6f) % 2 == 0 ? "_" : " "));
                UI.Label(new Rect(box.x + 40, box.y + 24, box.width - 80, 110), vis, 34, new Color(0.93f, 0.97f, 1f, a), TextAnchor.MiddleCenter, FontStyle.Normal, true, true);
                if (step.Options != null && typer.Done && !leaving)
                {
                    int n = step.Options.Length;
                    float bw = 260, gap = 30, total = n * bw + (n - 1) * gap;
                    for (int i = 0; i < n; i++)
                    {
                        var br = new Rect(W / 2f - total / 2f + i * (bw + gap), box.yMax - 86, bw, 62);
                        if (UI.Button(br, step.Options[i], i == 0 ? ButtonKind.Primary : ButtonKind.Normal, null, 26))
                        {
                            var s = step; step = null;
                            AudioManager.Ui("ui_click");
                            s.Next?.Invoke(i);
                            break;
                        }
                    }
                }
            }
            if (step != null && !typer.Done && UI.Hit(box)) typer.Skip();
        }

        public override bool OnBack() { if (!leaving) Close(null); return true; }
    }

    // ====================================================================== in-realm "???" dialog

    /// <summary>The "???" warning box inside the realm. With options it waits for a choice; without, it closes itself.</summary>
    public sealed class RealmDialog : UIScreen
    {
        readonly string text;
        readonly string[] options;
        readonly Action<int> onChoice;
        readonly float hold;
        readonly Typer typer = new Typer { Sound = "realm_type", Volume = 0.55f };
        bool chosen;
        public bool Closed { get; private set; }
        int lastFrame = -1;

        public RealmDialog(string text, string[] options, Action<int> onChoice, float hold = 2.2f)
        { this.text = text; this.options = options; this.onChoice = onChoice; this.hold = hold; }

        public override bool IsModal => true;
        public override bool ShowCursor => options != null;
        public override bool BlocksGameplay => options != null;

        public override void OnOpen()
        {
            typer.Set(text, 22f);
            AudioManager.Play2D("boss_glitch", 0.35f, 0.8f, AudioBus.Sfx);
            AudioManager.Play2D("boss_whoosh", 0.5f, 1f, AudioBus.Sfx);
        }

        void Close() { if (Closed) return; Closed = true; UIRoot.Remove(this); }

        public override void Draw()
        {
            if (Time.frameCount != lastFrame && Event.current.type == EventType.Repaint)
            {
                lastFrame = Time.frameCount;
                typer.Update();
                if (options == null && typer.Done && typer.SinceDone > hold) Close();
            }
            float W = UI.VW, H = UI.VH, t = Time.unscaledTime;
            float a = Intro(0, 0.25f);
            UI.Gradient(new Rect(0, H * 0.45f, W, H * 0.55f), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.85f * a));
            var r = new Rect(W / 2f - 560, H - 420, 1120, options != null ? 330 : 220);
            float jx = (BossArt.Hash(Mathf.FloorToInt(t * 20f), 1) < 0.06f) ? 6f : 0f;
            r.x += jx;
            UI.Fill(r, new Color(0.02f, 0f, 0f, 0.93f * a));
            UI.Outline(r, new Color(0.75f, 0.05f, 0.05f, (0.6f + 0.3f * Mathf.Sin(t * 5f)) * a), 3);
            UI.Fill(new Rect(r.x, r.y, r.width, 46), new Color(0.3f, 0f, 0f, 0.85f * a));
            BossArt.GlitchText(new Rect(r.x + 18, r.y, 200, 46), "???", 30, new Color(1f, 0.85f, 0.85f, a), 0.8f, UI.HeavyFont, TextAnchor.MiddleLeft);
            UI.Label(new Rect(r.xMax - 260, r.y, 240, 46), "WARNING", 20, new Color(1f, 0.6f, 0.6f, 0.7f * a), TextAnchor.MiddleRight, FontStyle.Bold);
            string vis = typer.Visible;
            UI.Label(new Rect(r.x + 40, r.y + 62, r.width - 80, 110), "<color=#ff4444>??? - </color>" + vis, 34, new Color(0.95f, 0.9f, 0.9f, a), TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            if (!typer.Done && options != null && UI.Hit(r)) typer.Skip();
            if (options != null && typer.Done && !chosen)
            {
                float bw = 330, gap = 22, total = options.Length * bw + (options.Length - 1) * gap;
                for (int i = 0; i < options.Length; i++)
                {
                    var br = new Rect(r.center.x - total / 2f + i * (bw + gap), r.yMax - 92, bw, 64);
                    if (UI.Button(br, options[i], i == options.Length - 1 ? ButtonKind.Danger : ButtonKind.Normal, null, 22))
                    {
                        chosen = true;
                        AudioManager.Ui("ui_click");
                        Close();
                        onChoice?.Invoke(i);
                        break;
                    }
                }
            }
        }

        public override bool OnBack() => true; // there is no escape
    }

    // ====================================================================== LEAVE

    /// <summary>Everything else disappears: only "LEAVE" slamming in letter by letter and a Leave Map button.</summary>
    public sealed class LeaveScreen : UIScreen
    {
        readonly Action onFinished;
        readonly Action onBlackout;
        int lettersShown;
        bool bell, blackout, finished;
        readonly Typer typer = new Typer { Sound = "realm_type", Volume = 0.7f };
        bool typing;
        int lastFrame = -1;
        float nextGlitch;

        const string Word = "LEAVE";
        const float LetterStart = 0.6f, LetterGap = 0.55f, BellAt = 10f, BlackAt = 20f, BlackLen = 1.05f, PayAt = 22f, EndAt = 26.5f;

        public LeaveScreen(Action onBlackout, Action onFinished) { this.onBlackout = onBlackout; this.onFinished = onFinished; }
        public override bool ShowCursor => true;

        void Step()
        {
            float t = Age;
            int want = Mathf.Clamp(Mathf.FloorToInt((t - LetterStart) / LetterGap) + 1, 0, Word.Length);
            while (lettersShown < want)
            {
                lettersShown++;
                AudioManager.Play2D("boss_slam", 1f, lettersShown == Word.Length ? 0.85f : 1f + (lettersShown - 1) * 0.02f, AudioBus.Sfx);
                CameraFX.Shake(0.35f);
            }
            if (!bell && t >= BellAt)
            {
                bell = true;
                AudioManager.Play2D("boss_bell", 1f, 1f, AudioBus.Sfx);
                AudioManager.Play2D("boss_slam", 0.9f, 0.7f, AudioBus.Sfx);
                CameraFX.Shake(0.8f);
            }
            if (bell && !blackout && t > nextGlitch)
            {
                nextGlitch = t + UnityEngine.Random.Range(0.5f, 1.6f);
                AudioManager.Play2D("boss_glitch", 0.35f, UnityEngine.Random.Range(0.8f, 1.2f), AudioBus.Sfx);
            }
            if (!blackout && t >= BlackAt)
            {
                blackout = true;
                AudioManager.Play2D("boss_whoosh", 0.8f, 0.7f, AudioBus.Sfx);
                onBlackout?.Invoke();
            }
            if (!typing && t >= PayAt) { typing = true; typer.Set("You're going to pay for this", 14f); }
            if (typing) typer.Update();
            if (!finished && t >= EndAt)
            {
                finished = true;
                UIRoot.Remove(this);
                onFinished?.Invoke();
            }
        }

        public override void Draw()
        {
            if (Time.frameCount != lastFrame && Event.current.type == EventType.Repaint) { lastFrame = Time.frameCount; Step(); }
            float W = UI.VW, H = UI.VH, t = Age;
            UI.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, Mathf.Clamp01(t / 0.3f) * 0.9f));
            var tr = new Rect(0, H * 0.18f, W, 380);
            if (!bell)
            {
                // Letters slam in, each with a short scale punch.
                float total = 0f;
                float[] w = new float[Word.Length];
                for (int i = 0; i < Word.Length; i++) { w[i] = UI.TextWidth(Word[i].ToString(), 300, FontStyle.Normal, UI.HeavyFont) + 12; total += w[i]; }
                float x = W / 2f - total / 2f;
                for (int i = 0; i < lettersShown; i++)
                {
                    float age = t - (LetterStart + i * LetterGap);
                    float punch = 1f + Mathf.Max(0f, 0.6f - age * 4f);
                    int size = Mathf.RoundToInt(300 * punch);
                    float sx = Mathf.Sin(t * 41f + i) * 3f, sy = Mathf.Cos(t * 37f + i * 2) * 3f;
                    var lr = new Rect(x + sx - (punch - 1f) * w[i] * 0.5f, tr.y + sy, w[i] * punch + 40, tr.height);
                    UI.Label(new Rect(lr.x + 6, lr.y, lr.width, lr.height), Word[i].ToString(), size, new Color(0.6f, 0f, 0f, 0.7f), TextAnchor.MiddleLeft, FontStyle.Normal, false, false, UI.HeavyFont);
                    UI.Label(lr, Word[i].ToString(), size, new Color(0.95f, 0.92f, 0.9f), TextAnchor.MiddleLeft, FontStyle.Normal, false, false, UI.HeavyFont);
                    x += w[i];
                }
            }
            else if (!blackout)
            {
                float shake = 14f;
                var sr = new Rect(tr.x + UnityEngine.Random.Range(-shake, shake), tr.y + UnityEngine.Random.Range(-shake, shake), tr.width, tr.height);
                BossArt.GlitchText(sr, "LEAVE!!!", 300, new Color(1f, 0.15f, 0.12f), 2.2f);
            }

            bool enabled = !blackout;
            var br = new Rect(W / 2f - 190, H * 0.72f, 380, 84);
            if (UI.Button(br, "LEAVE MAP", enabled ? ButtonKind.Danger : ButtonKind.Ghost, IconFactory.Get("home"), 30, enabled) && enabled)
            {
                finished = true;
                GameManager.I.ReturnToMenu();
                return;
            }

            if (typing)
            {
                string vis = typer.Visible;
                UI.Label(new Rect(0, H * 0.42f, W, 90), "<color=#ff3333>??? - </color>" + vis, 52, new Color(0.95f, 0.85f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            if (blackout && t < BlackAt + BlackLen) UI.Fill(new Rect(0, 0, W, H), Color.black);
        }

        public override bool OnBack() => true;
    }

    // ====================================================================== kicked

    /// <summary>"Kicked" style message box with a glitching body.</summary>
    public sealed class RealmKickedScreen : UIScreen
    {
        float nextGlitch;
        public override bool IsModal => true;
        public override void OnOpen() { AudioManager.Play2D("boss_glitch", 0.8f, 0.9f, AudioBus.Ui); AudioManager.Ui("ui_error"); }

        public override void Draw()
        {
            float a = Intro(0, 0.2f);
            if (Event.current.type == EventType.Repaint && Time.unscaledTime > nextGlitch)
            {
                nextGlitch = Time.unscaledTime + UnityEngine.Random.Range(0.8f, 2.5f);
                if (Age > 0.5f) AudioManager.Play2D("boss_glitch", 0.25f, UnityEngine.Random.Range(0.7f, 1.3f), AudioBus.Ui);
            }
            UI.Dim(0.7f * a);
            var r = UI.Centered(760, 340);
            UI.Panel(r, "KICKED FROM REALM", a);
            BossArt.GlitchText(new Rect(r.x + 20, r.y + 96, r.width - 40, 120), "Don't enter my realm again", 44, new Color(1f, 0.9f, 0.9f, a), 1.4f);
            if (UI.Button(new Rect(r.xMax - 250, r.yMax - 86, 220, 60), "OK", ButtonKind.Primary)) UIRoot.Pop();
        }
    }

    // ====================================================================== fake crash

    /// <summary>
    /// Sanity hit 100%: the frame freezes into a "Not Responding" window with corrupted slices and a stuck-audio buzz,
    /// then Orbie fills the screen: "I WARNED YOU". Then straight back to the main menu. Nothing actually crashes.
    /// </summary>
    public sealed class FakeCrashScreen : UIScreen
    {
        readonly Texture2D frozen;
        readonly List<AudioSource> sources = new List<AudioSource>();
        bool scared, done;
        int lastFrame = -1;
        const float ScareAt = 3.2f, EndAt = 5.2f;

        public FakeCrashScreen(Texture2D frozen) { this.frozen = frozen; }
        public override bool PausesTime => true;
        public override bool ShowCursor => false;

        AudioSource Play(string name, float vol)
        {
            var s = AudioManager.Play2D(name, vol, 1f, AudioBus.Sfx);
            if (s != null) { s.ignoreListenerPause = true; sources.Add(s); }
            return s;
        }

        public override void OnOpen() => Play("boss_crash_buzz", 0.5f);

        public override void OnClose()
        {
            foreach (var s in sources) if (s != null) s.ignoreListenerPause = false;
            if (frozen != null) UnityEngine.Object.Destroy(frozen);
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH, t = Age;
            if (Time.frameCount != lastFrame && Event.current.type == EventType.Repaint)
            {
                lastFrame = Time.frameCount;
                if (!scared && t >= ScareAt) { scared = true; Play("boss_orbie_scare", 1f); }
                if (!done && t >= EndAt)
                {
                    done = true;
                    GameManager.I.ReturnToMenu();
                    return;
                }
            }
            UI.Fill(new Rect(0, 0, W, H), Color.black);
            if (!scared)
            {
                if (frozen != null)
                {
                    UI.Tex(new Rect(0, 0, W, H), frozen, Color.white);
                    // Corrupted slices: rows of the frozen frame shifted sideways.
                    int frame = Mathf.FloorToInt(t * (t > 1.6f ? 14f : 3f));
                    int n = t > 1.6f ? 14 : 4;
                    for (int k = 0; k < n; k++)
                    {
                        float y = BossArt.Hash(frame, k) * H, h = 6 + BossArt.Hash(frame, k + 50) * 60;
                        float off = (BossArt.Hash(frame, k + 100) - 0.5f) * 160f;
                        var uv = new Rect(0, 1f - (y + h) / H, 1, h / H);
                        var col = BossArt.Hash(frame, k + 7) < 0.3f ? new Color(1f, 0.4f, 0.4f) : BossArt.Hash(frame, k + 8) < 0.3f ? new Color(0.4f, 1f, 1f) : Color.white;
                        UI.TexUV(new Rect(off, y, W, h), frozen, uv, col);
                    }
                }
                // Not Responding: whitened frame + window title.
                float wash = Mathf.Clamp01((t - 0.6f) / 0.8f) * 0.45f;
                UI.Fill(new Rect(0, 0, W, H), new Color(1f, 1f, 1f, wash));
                if (t > 0.6f)
                {
                    UI.Fill(new Rect(0, 0, W, 44), new Color(0.95f, 0.95f, 0.95f, 1f));
                    UI.Label(new Rect(16, 0, W, 44), GameInfo.Title + " (Not Responding)", 20, new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleLeft, FontStyle.Normal, false);
                    UI.Label(new Rect(W - 200, 0, 190, 44), "_     []     X", 20, new Color(0.2f, 0.2f, 0.2f), TextAnchor.MiddleRight, FontStyle.Normal, false);
                    // Busy cursor ring.
                    var m = new Vector2(W / 2f + 40f, H / 2f + 30f);
                    for (int i = 0; i < 10; i++)
                    {
                        float ang = t * 6f + i * 0.628f;
                        float al = (i + 1) / 10f;
                        UI.Tex(new Rect(m.x + Mathf.Cos(ang) * 16f - 4, m.y + Mathf.Sin(ang) * 16f - 4, 8, 8), BossArt.Glow(), new Color(0.2f, 0.5f, 1f, al));
                    }
                }
                if (t > 2.2f)
                {
                    var r = UI.Centered(640, 220);
                    UI.Fill(r, new Color(0.94f, 0.94f, 0.94f));
                    UI.Outline(r, new Color(0.5f, 0.5f, 0.5f), 1);
                    UI.Label(new Rect(r.x + 24, r.y + 18, r.width - 48, 40), GameInfo.Title + " is not responding", 24, new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleLeft, FontStyle.Bold, false);
                    UI.Label(new Rect(r.x + 24, r.y + 66, r.width - 48, 70), "If you close the program, you might lose information.", 19, new Color(0.2f, 0.2f, 0.2f), TextAnchor.UpperLeft, FontStyle.Normal, false, true);
                    UI.Fill(new Rect(r.xMax - 360, r.yMax - 62, 160, 42), new Color(0.85f, 0.85f, 0.85f));
                    UI.Label(new Rect(r.xMax - 360, r.yMax - 62, 160, 42), "Close", 18, Color.black, TextAnchor.MiddleCenter, FontStyle.Normal, false);
                    UI.Fill(new Rect(r.xMax - 186, r.yMax - 62, 160, 42), new Color(0.85f, 0.85f, 0.85f));
                    UI.Label(new Rect(r.xMax - 186, r.yMax - 62, 160, 42), "Wait", 18, Color.black, TextAnchor.MiddleCenter, FontStyle.Normal, false);
                }
            }
            else
            {
                float k = t - ScareAt;
                float s = Mathf.Lerp(2.6f, 1.6f, Mathf.Clamp01(k * 5f)) * H;
                float jx = UnityEngine.Random.Range(-30f, 30f), jy = UnityEngine.Random.Range(-30f, 30f);
                UI.Fill(new Rect(0, 0, W, H), Mathf.FloorToInt(k * 20f) % 4 == 0 ? new Color(0.25f, 0f, 0f) : Color.black);
                UI.Tex(new Rect(W / 2f - s * 0.75f + jx, H / 2f - s * 0.75f + jy, s * 1.5f, s * 1.5f), BossArt.Glow(), new Color(1f, 0.8f, 0.8f, 0.35f));
                UI.Tex(new Rect(W / 2f - s / 2f + jx, H / 2f - s / 2f + jy, s, s), BossArt.Orbie(true), Color.white);
                BossArt.GlitchText(new Rect(0, H * 0.72f, W, 260), "I WARNED YOU", 210, new Color(1f, 0.1f, 0.1f), 2.5f);
            }
        }

        public override bool OnBack() => true;
    }
}
