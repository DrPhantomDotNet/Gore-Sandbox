using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Scrolling credits. Click "You" for a surprise.</summary>
    public sealed class CreditsScreen : UIScreen
    {
        struct Line { public string Role, Name; public int Size; public bool Header; }

        static readonly Line[] Lines =
        {
            new Line { Name = GameInfo.Title, Size = 70, Header = true },
            new Line { Name = "a " + GameInfo.Studio + " production", Size = 26 },
            new Line { Size = 40 },
            new Line { Role = "OWNER · DEVELOPER", Name = "Quibbo / DrPhantom", Size = 44 },
            new Line { Size = 20 },
            new Line { Role = "DEVELOPER", Name = "AlexKiller", Size = 44 },
            new Line { Size = 20 },
            new Line { Role = "PLAYING THE GAME", Name = "You", Size = 44 },
            new Line { Size = 60 },
            new Line { Role = "STARRING", Name = "The Goredolls", Size = 32 },
            new Line { Role = "PHYSICS", Name = "NVIDIA PhysX via Unity", Size = 26 },
            new Line { Role = "ENGINE", Name = "Unity 6", Size = 26 },
            new Line { Role = "EVERY SOUND", Name = "Synthesised live by the game", Size = 26 },
            new Line { Role = "EVERY MODEL & TEXTURE", Name = "Generated procedurally at runtime", Size = 26 },
            new Line { Size = 50 },
            new Line { Role = "SPECIAL THANKS", Name = "Everyone who reported a bug", Size = 26 },
            new Line { Name = "Everyone who found a rubber duck", Size = 26 },
            new Line { Name = "The physics engine, for not exploding (mostly)", Size = 26 },
            new Line { Size = 60 },
            new Line { Name = "No Goredolls were harmed in the making of this game.", Size = 22 },
            new Line { Name = "(That is a lie.)", Size = 18 },
        };

        float scroll;
        int youClicks;

        public override void OnOpen() { AudioManager.Ui("ui_open"); scroll = 0; }

        public override void Tick()
        {
            scroll += Time.unscaledDeltaTime * (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space) ? 160f : 45f);
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            UI.Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.82f * Intro()));
            float y = H * 0.85f - scroll;
            float total = 0;
            foreach (var l in Lines)
            {
                float h = (l.Role != null ? 34 : 0) + Mathf.Max(l.Size * 1.4f, 10);
                float alpha = Mathf.Clamp01((y - 60) / 120f) * Mathf.Clamp01((H - y) / 160f);
                if (l.Role != null) UI.Label(new Rect(0, y, W, 34), l.Role, 20, UI.Accent2.WithAlpha(alpha), TextAnchor.MiddleCenter, FontStyle.Bold);
                float ny = y + (l.Role != null ? 34 : 0);
                if (l.Name != null)
                {
                    var r = new Rect(0, ny, W, l.Size * 1.4f);
                    UI.Label(r, l.Name, l.Size, (l.Header ? UI.Accent : UI.TextColor).WithAlpha(alpha), TextAnchor.MiddleCenter, FontStyle.Normal, true, false, l.Header || l.Size >= 40 ? UI.HeavyFont : null);
                    if (l.Name == "You" && alpha > 0.3f)
                    {
                        float w = UI.TextWidth("You", l.Size, FontStyle.Normal, UI.HeavyFont);
                        if (UI.Hit(new Rect(W / 2 - w, ny, w * 2, r.height)))
                        {
                            youClicks++;
                            AudioManager.Ui("ui_success");
                            Toasts.Show("THANK YOU", youClicks < 3 ? "Seriously. Thanks for playing." : "Okay, okay, we get it. You're the best.", UI.Good);
                            if (youClicks == 3) EasterEggs.Found("You're Welcome", "Clicked yourself in the credits three times.");
                        }
                    }
                }
                y += h + 10;
                total += h + 10;
            }
            if (scroll > total + H) scroll = 0;
            if (UI.Button(new Rect(40, H - 96, 220, 60), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
            UI.Label(new Rect(W - 520, H - 70, 490, 40), "hold click or space to speed up", 18, UI.Muted, TextAnchor.MiddleRight, FontStyle.Italic);
        }

        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }
    }
}
