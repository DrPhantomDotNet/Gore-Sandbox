using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Main menu over the live 3D backdrop: dripping title, Play / Settings / Mods / Credits / Quit,
    /// changelog + stats buttons, version + access label, rotating tips, and a handful of hidden secrets.
    /// </summary>
    public sealed class MainMenuScreen : UIScreen
    {
        static readonly string[] Tips =
        {
            "Shoot a Goredoll in the middle of the back to sever the spinal cord.",
            "Press E while looking at a Goredoll to inspect its wounds.",
            "Hold LMB with the Spawn Gun to drag anything. Hold R to rotate it.",
            "Neck wounds make Goredolls choke on their own blood.",
            "Press V with noclip enabled to fly through walls (but not the Boundaries).",
            "The AR-17 is a semi-automatic shotgun. Get close.",
            "The Admin Gun has five modes. Press B to cycle them.",
            "Goredolls remember who hurt them.",
            "Press F to kick. Kick while airborne for a dropkick.",
            "Press F5 if you know the password.",
            "There are rubber ducks hidden on every map.",
            "Goredolls breathe faster as they lose blood.",
        };

        static readonly KeyCode[] Konami = { KeyCode.UpArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.B, KeyCode.A };
        int konamiIndex;
        string typed = "";
        int titleClicks;
        float titleBleed;
        readonly float[] dripSeeds = new float[64];

        public override bool BlocksGameplay => true;

        public override void OnOpen()
        {
            for (int i = 0; i < dripSeeds.Length; i++) dripSeeds[i] = Random.value;
            AudioManager.PlayMusic("music_menu", 0.85f);
        }

        public override bool OnBack()
        {
            UIRoot.Push(new ConfirmDialog("Quit the game?", "The Goredolls will be relieved.", () => GameManager.I.Quit(), "QUIT", ButtonKind.Danger));
            return true;
        }

        public override void Tick()
        {
            if (UIRoot.Top != this) return;
            // Hidden key sequences.
            if (Input.anyKeyDown)
            {
                if (Input.GetKeyDown(Konami[konamiIndex])) { konamiIndex++; if (konamiIndex == Konami.Length) { konamiIndex = 0; EasterEggs.Konami(); } }
                else if (Input.GetKeyDown(Konami[0])) konamiIndex = 1;
                else if (!Input.GetKeyDown(KeyCode.Mouse0)) konamiIndex = 0;
                foreach (char ch in Input.inputString)
                {
                    if (!char.IsLetter(ch)) continue;
                    typed = (typed + char.ToLowerInvariant(ch));
                    if (typed.Length > 16) typed = typed.Substring(typed.Length - 16);
                    EasterEggs.CheckTyped(ref typed);
                }
            }
            titleBleed = Mathf.MoveTowards(titleBleed, 0f, Time.unscaledDeltaTime * 0.05f);
        }

        public override void Draw()
        {
            float W = UI.VW, H = UI.VH;
            // Readability gradient on the left + vignette + grain
            UI.Tex(new Rect(0, 0, W * 0.62f, H), HorizontalFade, new Color(0.02f, 0.0f, 0.0f, 0.82f));
            UI.Tex(new Rect(0, 0, W, H), TextureFactory.Vignette, new Color(0, 0, 0, 0.85f));
            UI.Tex(new Rect(0, 0, W, H), TextureFactory.Grain, Color.white);

            DrawTitle(90, 70);

            float y = 380;
            string[] labels = { "PLAY", "PLAY MULTIPLAYER", "MAP EDITOR", "SETTINGS", "MODS", "CREDITS", "QUIT" };
            string[] icons = { "play", "users", "building", "gear", "wrench", "book", "exit" };
            float yy = y;
            for (int i = 0; i < labels.Length; i++)
            {
                float a = Intro(0.15f + i * 0.07f, 0.4f);
                bool big = i == 0, mp = i == 1;
                var r = new Rect(90 - (1 - a) * 220, yy, big || mp ? 440 : 380, big ? 72 : mp ? 60 : 56);
                yy += big ? 80 : mp ? 76 : 64;
                var kind = big ? ButtonKind.Primary : i == labels.Length - 1 ? ButtonKind.Danger : ButtonKind.Normal;
                var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
                if (UI.Button(r, labels[i], kind, IconFactory.Get(icons[i]), big ? 34 : mp ? 26 : 26)) OnMain(i);
                // Story mode (World/StoryMode.cs), right under PLAY.
                if (big)
                {
                    if (UI.Button(new Rect(90 - (1 - a) * 220, yy, 440, 60), "PLAY STORY", ButtonKind.Normal, IconFactory.Get("book"), 26, true, "Chapter 1: The East Side - 4 missions")) UIRoot.Push(new StoryMenuScreen());
                    yy += 68;
                }
                GUI.color = old;
            }

            // Top-right buttons (like the original: changelog, stats, social)
            float tx = W - 30;
            tx -= 300; if (UI.Button(new Rect(tx, 26, 290, 54), "STATS & ACHIEVEMENTS", ButtonKind.Ghost, IconFactory.Get("trophy"), 20)) UIRoot.Push(new StatsScreen());
            tx -= 210; if (UI.Button(new Rect(tx, 26, 200, 54), "CHANGELOG", ButtonKind.Ghost, IconFactory.Get("book"), 20)) UIRoot.Push(new ChangelogScreen());
            if (GoreContent.HasExtras) { tx -= 190; if (UI.Button(new Rect(tx, 26, 180, 54), "EXTRAS", ButtonKind.Ghost, IconFactory.Get("star"), 20, true, "Stories and screens made in the Unity editor")) UIRoot.Push(new ExtrasScreen()); }
            tx -= 190; if (UI.Button(new Rect(tx, 26, 180, 54), "HOW TO", ButtonKind.Ghost, IconFactory.Get("hint"), 20)) UIRoot.Push(new HelpScreen());
            if (!string.IsNullOrEmpty(GameInfo.DiscordUrl)) { tx -= 190; if (UI.Button(new Rect(tx, 26, 180, 54), "DISCORD", ButtonKind.Ghost, IconFactory.Get("chat"), 20, true, "Join the community: feedback, bug reports, updates")) Application.OpenURL(GameInfo.DiscordUrl); }
            if (!string.IsNullOrEmpty(GameInfo.ItchUrl)) { tx -= 190; if (UI.Button(new Rect(tx, 26, 180, 54), "ITCH.IO", ButtonKind.Ghost, IconFactory.Get("globe"), 20, true, "The game's itch.io page")) Application.OpenURL(GameInfo.ItchUrl); }

            // Profile card under the top-right buttons: your icon, name and badges. Tap to edit (UI/Screens/ProfileScreen.cs).
            DrawProfileCard(new Rect(W - 30 - 420, 94, 420, 92));

            // Bottom-left: version + access, exactly as specified
            if (GameInfo.EarlyAccess)
            {
                UI.Rounded(new Rect(24, H - 146, 220, 36), UI.Accent.WithAlpha(0.85f), true);
                UI.Label(new Rect(24, H - 146, 220, 36), "EARLY ACCESS", 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            UI.Rounded(new Rect(24, H - 104, 270, 80), new Color(0, 0, 0, 0.45f));
            UI.Label(new Rect(42, H - 100, 260, 36), "Version: " + GameInfo.Version, 24, UI.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Label(new Rect(42, H - 64, 260, 36), "Access: " + GameInfo.Access, 24, UI.Accent2, TextAnchor.MiddleLeft, FontStyle.Bold);

            // Bottom-right: studio + tips
            int tip = (int)(Time.unscaledTime / 6f) % Tips.Length;
            float tipA = Mathf.Clamp01(Mathf.Sin((Time.unscaledTime % 6f) / 6f * Mathf.PI) * 3f);
            UI.Label(new Rect(W - 820, H - 96, 790, 36), "TIP: " + Tips[tip], 20, UI.TextColor.WithAlpha(0.8f * tipA), TextAnchor.MiddleRight, FontStyle.Italic);
            UI.Label(new Rect(W - 520, H - 56, 490, 36), "© " + GameInfo.Studio + "  ·  " + Stats.UnlockedCount() + "/" + Stats.Achievements.Count + " achievements", 18, UI.Muted, TextAnchor.MiddleRight);

            // Poke the backdrop Goredoll by clicking it.
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && UIRoot.Top == this && e.mousePosition.x / UI.Scale > 560)
                MenuBackdrop.TryPoke(e.mousePosition);
        }

        static void DrawProfileCard(Rect r)
        {
            var prof = PlayerProfile.Local;
            bool hover = UI.Hover(r);
            UI.Rounded(r, hover ? new Color(0.2f, 0.2f, 0.24f, 0.85f) : new Color(0, 0, 0, 0.5f), false);
            PlayerProfile.DrawIcon(new Rect(r.x + 12, r.y + 12, r.height - 24, r.height - 24), prof);
            float x = r.x + r.height + 2;
            UI.Label(new Rect(x, r.y + 8, r.width - (x - r.x) - 130, 40), PlayerProfile.LocalName, 24, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold, true, false, UI.HeavyFont);
            BadgeChips.Draw(new Rect(x, r.y + 52, r.width - (x - r.x) - 12, 28), prof.BadgeMask, BadgeChips.Width(prof.BadgeMask, 28) > r.width - (x - r.x) - 12);
            UI.Rounded(new Rect(r.xMax - 124, r.y + 12, 112, 36), UI.Accent.WithAlpha(0.85f), true);
            UI.Label(new Rect(r.xMax - 124, r.y + 12, 112, 36), "PROFILE", 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (UI.Hit(r)) { AudioManager.Ui("ui_click"); UIRoot.Push(new ProfileScreen()); }
        }

        void OnMain(int i)
        {
            switch (i)
            {
                case 0: UIRoot.Push(new MapSelectScreen()); break;
                case 1: UIRoot.Push(new MultiplayerScreen()); break;
                case 2: UIRoot.Push(new MapEditorHubScreen()); break; // World/MapEditor.cs
                case 3: UIRoot.Push(new SettingsScreen(false)); break;
                case 4: UIRoot.Push(new ModsScreen()); break;
                case 5: UIRoot.Push(new CreditsScreen()); break;
                case 6: OnBack(); break;
            }
        }

        void DrawTitle(float x, float y)
        {
            string title = GameInfo.Title;
            int size = 120;
            float a = Intro(0f, 0.6f);
            var titleRect = new Rect(x, y + (1 - a) * -30, 900, 150);
            float w = UI.TextWidth(title, size, FontStyle.Normal, UI.HeavyFont);
            // Drips under the letters
            int drips = Mathf.Min(dripSeeds.Length, 14 + (int)(titleBleed * 40));
            for (int i = 0; i < drips; i++)
            {
                float s = dripSeeds[i];
                float dx = x + 12 + s * (w - 24);
                float len = (14f + 70f * Mathf.Repeat(s * 7.3f + Time.unscaledTime * (0.03f + s * 0.05f), 1f)) * (1f + titleBleed * 2f) * a;
                float dw = 4f + (s * 13f % 6f);
                UI.Rounded(new Rect(dx, y + 118, dw, len), new Color(0.55f, 0.02f, 0.03f, 0.95f), true);
                UI.Tex(new Rect(dx - 2, y + 112 + len, dw + 4, dw + 4), TextureFactory.Circle, new Color(0.55f, 0.02f, 0.03f, 0.95f));
            }
            UI.Label(new Rect(titleRect.x + 6, titleRect.y + 8, titleRect.width, titleRect.height), title, size, new Color(0, 0, 0, 0.8f * a), TextAnchor.MiddleLeft, FontStyle.Normal, false, false, UI.HeavyFont);
            UI.Label(titleRect, title, size, Color.Lerp(new Color(0.85f, 0.08f, 0.1f), new Color(1f, 0.25f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.3f)).WithAlpha(a), TextAnchor.MiddleLeft, FontStyle.Normal, false, false, UI.HeavyFont);
            UI.Label(new Rect(x + 6, y + 150, 900, 40), GameInfo.Subtitle.ToUpperInvariant(), 22, UI.Muted.WithAlpha(a), TextAnchor.MiddleLeft, FontStyle.Bold);

            if (UI.Hit(new Rect(x, y, w, 150)))
            {
                titleClicks++;
                titleBleed = Mathf.Min(1f, titleBleed + 0.12f);
                AudioManager.Ui("splat", 0.6f);
                if (titleClicks == 10) EasterEggs.Found("Bleeding Title", "The title can only take so much.");
            }
        }

        static Texture2D horizontalFade;
        static Texture2D HorizontalFade
        {
            get
            {
                if (horizontalFade != null) return horizontalFade;
                horizontalFade = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < 64; x++) horizontalFade.SetPixel(x, 0, new Color(1, 1, 1, 1f - Mathf.Pow(x / 63f, 1.5f)));
                horizontalFade.Apply();
                return horizontalFade;
            }
        }
    }
}
