using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// PROFILE: your name, profile icon, hat and body look, with a live rotating 3D preview, plus your badges.
    /// Saved on this device (Core/PlayerProfile.cs) and shown to other players in multiplayer.
    /// </summary>
    public sealed class ProfileScreen : UIScreen
    {
        static readonly string[] Tabs = { "ICON", "HAT", "BODY" };
        static readonly Color[] SkinTones =
        {
            new Color(0.98f, 0.86f, 0.77f), new Color(0.96f, 0.80f, 0.69f), new Color(0.91f, 0.72f, 0.58f), new Color(0.82f, 0.62f, 0.47f),
            new Color(0.68f, 0.48f, 0.34f), new Color(0.52f, 0.36f, 0.25f), new Color(0.38f, 0.26f, 0.18f), new Color(0.27f, 0.18f, 0.12f),
        };
        static readonly Color[] HairColors =
        {
            new Color(0.06f, 0.06f, 0.06f), new Color(0.12f, 0.08f, 0.05f), new Color(0.3f, 0.18f, 0.08f), new Color(0.55f, 0.4f, 0.2f),
            new Color(0.85f, 0.7f, 0.4f), new Color(0.6f, 0.25f, 0.1f), new Color(0.62f, 0.62f, 0.62f), new Color(0.92f, 0.92f, 0.9f),
            new Color(0.2f, 0.35f, 0.85f), new Color(0.9f, 0.3f, 0.6f),
        };
        static readonly Color[] ClothColors =
        {
            new Color(0.25f, 0.55f, 0.25f), new Color(0.75f, 0.12f, 0.12f), new Color(0.15f, 0.3f, 0.6f), new Color(0.9f, 0.9f, 0.88f),
            new Color(0.15f, 0.15f, 0.17f), new Color(0.95f, 0.6f, 0.15f), new Color(0.5f, 0.3f, 0.6f), new Color(0.6f, 0.6f, 0.62f),
            new Color(0.33f, 0.36f, 0.24f), new Color(0.95f, 0.55f, 0.7f), new Color(0.95f, 0.85f, 0.2f), new Color(0.2f, 0.6f, 0.65f),
        };
        static readonly Color[] PantsColors =
        {
            new Color(0.2f, 0.3f, 0.5f), new Color(0.15f, 0.2f, 0.35f), new Color(0.55f, 0.48f, 0.35f), new Color(0.12f, 0.12f, 0.13f),
            new Color(0.35f, 0.33f, 0.28f), new Color(0.36f, 0.34f, 0.25f), new Color(0.6f, 0.6f, 0.62f), new Color(0.45f, 0.12f, 0.12f),
        };
        static readonly string[] HairStyles = { "Short", "Buzz", "Bald", "Long" };
        static readonly string[] ShirtStyles = { "T-shirt", "Long sleeves" };

        static int tab;
        int icon, hat;
        Color iconColor, hatColor;
        CustomLook look;
        bool customBody;
        string startState;

        // Preview studio (same approach as the Goredoll customizer)
        const int PreviewLayer = 31;
        GameObject studio, model;
        Camera cam;
        RenderTexture rt;
        string builtKey;
        float yaw = 200f;
        bool previewFailed;

        public override bool IsModal => true;
        public override bool BlocksGameplay => true;

        public override void OnOpen()
        {
            AudioManager.Ui("ui_open");
            var p = PlayerProfile.Local;
            icon = p.Icon; hat = p.Hat; iconColor = p.IconColor; hatColor = p.HatColor;
            customBody = p.BodyLook != null;
            look = customBody ? p.BodyLook.Clone() : PlayerProfile.DefaultLook(PlayerProfile.LocalName);
            startState = State();
        }

        public override void OnClose() => DestroyPreview();

        public override bool OnBack()
        {
            if (State() != startState) Save(false);
            AudioManager.Ui("ui_close");
            UIRoot.Pop();
            return true;
        }

        string State() => icon + "|" + hat + "|" + iconColor + "|" + hatColor + "|" + (customBody ? look.Encode() : "") + "|" + Settings.Data.playerName;

        void Save(bool toast)
        {
            var p = PlayerProfile.Local;
            p.Icon = icon; p.Hat = hat; p.IconColor = iconColor; p.HatColor = hatColor;
            if (customBody) { look.Name = CustomLook.CleanName(PlayerProfile.LocalName); look.Armor = 0; look.Role = 0; }
            p.Look = customBody ? look.Encode() : "";
            Settings.Save();
            PlayerProfile.Save();
            startState = State();
            if (toast) { GameEvents.ShowToast("Profile saved"); AudioManager.Ui("ui_success"); }
        }

        // ------------------------------------------------------------------ preview

        void EnsureStudio()
        {
            if (studio != null || previewFailed) return;
            try
            {
                studio = new GameObject("ProfileStudio");
                studio.transform.position = new Vector3(0, -5600, 0);
                var camGo = new GameObject("PreviewCam");
                camGo.transform.SetParent(studio.transform, false);
                camGo.transform.localPosition = new Vector3(0, 1.15f, 4.2f);
                camGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.05f, -1f), Vector3.up);
                cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.13f, 1f);
                cam.cullingMask = 1 << PreviewLayer;
                cam.fieldOfView = 31f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 30f;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                AddLight(new Vector3(-0.5f, -0.7f, -1f), 1.2f, Color.white);
                AddLight(new Vector3(0.7f, -0.2f, 0.6f), 0.5f, new Color(0.75f, 0.8f, 1f));
                var desc = new RenderTextureDescriptor(512, 640, RenderTextureFormat.ARGB32, 24)
                {
                    sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear, msaaSamples = 1,
                };
                rt = new RenderTexture(desc) { name = "ProfilePreview" };
                rt.Create();
                cam.targetTexture = rt;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Profile preview failed: " + e.Message);
                previewFailed = true;
                DestroyPreview();
            }
        }

        void AddLight(Vector3 dir, float intensity, Color color)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(studio.transform, false);
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.cullingMask = 1 << PreviewLayer;
            l.transform.rotation = Quaternion.LookRotation(dir);
        }

        void DestroyPreview()
        {
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
            if (studio != null) Object.Destroy(studio);
            studio = null; model = null; cam = null; builtKey = null;
        }

        public override void Tick()
        {
            EnsureStudio();
            if (studio == null || cam == null) return;
            var key = look.Encode() + "|" + hat + "|" + hatColor;
            if (key != builtKey)
            {
                builtKey = key;
                if (model != null) Object.Destroy(model);
                model = CustomDolls.BuildMannequin(look.ToAppearance(), 0, look.Glasses, null, PreviewLayer);
                var head = model.transform.Find(Anatomy.Parts[PartType.Head].Name);
                if (head != null) Hats.Attach(head, hat, hatColor, PreviewLayer);
                model.transform.SetParent(studio.transform, false);
                model.transform.localPosition = Vector3.zero;
            }
            yaw += Time.unscaledDeltaTime * 35f;
            if (model != null) model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var oldAmbient = RenderSettings.ambientLight;
            var oldMode = RenderSettings.ambientMode;
            try
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
                cam.Render();
            }
            catch (System.Exception e) { Debug.LogWarning("Profile preview render failed: " + e.Message); previewFailed = true; DestroyPreview(); }
            finally
            {
                RenderSettings.ambientLight = oldAmbient;
                RenderSettings.ambientMode = oldMode;
            }
        }

        // ------------------------------------------------------------------ UI

        public override void Draw()
        {
            float a = Intro(0, 0.2f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(Mathf.Min(1560, UI.VW - 40), Mathf.Min(1020, UI.VH - 30));
            UI.Panel(r, "Profile", a);

            float top = r.y + 78, bottom = r.yMax - 20;
            float previewW = Mathf.Clamp(r.width * 0.3f, 260f, 440f);
            var preview = new Rect(r.x + 24, top, previewW, bottom - top - 150);
            UI.Rounded(preview, UI.PanelLight, false);
            if (rt != null && !previewFailed) UI.Tex(new Rect(preview.x + 8, preview.y + 8, preview.width - 16, preview.height - 16), rt, Color.white, ScaleMode.ScaleToFit);
            else UI.Tex(new Rect(preview.x + preview.width * 0.25f, preview.y + preview.height * 0.3f, preview.width * 0.5f, preview.width * 0.5f), IconFactory.Get("person"), UI.Muted, ScaleMode.ScaleToFit);

            // Card: how others see you
            var card = new Rect(preview.x, preview.yMax + 12, previewW, 138);
            UI.Rounded(card, new Color(0, 0, 0, 0.35f), false);
            PlayerProfile.DrawIcon(new Rect(card.x + 14, card.y + 14, 64, 64), icon, iconColor);
            UI.Label(new Rect(card.x + 90, card.y + 14, card.width - 100, 64), PlayerProfile.LocalName, 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold, true, false, UI.HeavyFont);
            int mask = PlayerProfile.Local.BadgeMask;
            float chipH = 34;
            bool compact = BadgeChips.Width(mask, chipH) > card.width - 28;
            BadgeChips.Draw(new Rect(card.x + 14, card.y + 92, card.width - 28, chipH), mask, compact);

            var right = new Rect(preview.xMax + 24, top, r.xMax - preview.xMax - 48, bottom - top);
            UI.Label(new Rect(right.x, right.y, 150, 56), "Username", 20, Color.white);
            var nm = UI.TextField(new Rect(right.x + 160, right.y, right.width - 160, 56), Settings.Data.playerName, "profile_name", false, "Your name");
            if (nm != Settings.Data.playerName) Settings.Data.playerName = nm != null && nm.Length > 20 ? nm.Substring(0, 20) : nm ?? "";

            int nt = UI.Tabs(new Rect(right.x, right.y + 70, right.width, 56), tab, Tabs, 20);
            if (nt != tab) { tab = nt; AudioManager.Ui("ui_tab"); }
            const float buttonsH = 66f;
            var body = new Rect(right.x, right.y + 140, right.width, right.height - 140 - buttonsH - 14);
            switch (tab)
            {
                case 0: DrawIcons(body); break;
                case 1: DrawHats(body); break;
                case 2: DrawBody(body); break;
            }

            float bw = (right.width - 24) / 3f, by = right.yMax - buttonsH;
            if (UI.Button(new Rect(right.x, by, bw, buttonsH), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"), 22)) OnBack();
            if (UI.Button(new Rect(right.x + bw + 12, by, bw, buttonsH), "RANDOM", ButtonKind.Normal, IconFactory.Get("dice"), 22)) Randomize();
            if (UI.Button(new Rect(right.x + (bw + 12) * 2, by, bw, buttonsH), "SAVE", ButtonKind.Primary, IconFactory.Get("save"), 22)) Save(true);
        }

        void Randomize()
        {
            AudioManager.Ui("ui_click");
            if (tab == 0) { icon = Random.Range(0, PlayerProfile.IconGlyphs.Length); iconColor = PlayerProfile.Palette[Random.Range(0, PlayerProfile.Palette.Length)]; }
            else if (tab == 1) { hat = Random.Range(1, Hats.Count); hatColor = PlayerProfile.Palette[Random.Range(0, PlayerProfile.Palette.Length)]; }
            else { look = CustomLook.FromAppearance(Appearance.Random(-1)); customBody = true; }
        }

        /// <summary>Grid of big tappable cells; returns the content height.</summary>
        static int Columns(float width, float minCell) => Mathf.Max(3, Mathf.FloorToInt((width + 10) / (minCell + 10)));

        void DrawIcons(Rect body)
        {
            const float paletteH = 84f;
            var grid = new Rect(body.x, body.y, body.width, body.height - paletteH - 10);
            int n = PlayerProfile.IconGlyphs.Length;
            int cols = Columns(grid.width - 16, 104);
            float cell = Mathf.Floor((grid.width - 16 - (cols - 1) * 10) / cols);
            int rows = Mathf.CeilToInt(n / (float)cols);
            float content = rows * (cell + 10);
            float s = UI.BeginScroll("profile_icons", grid, content);
            for (int i = 0; i < n; i++)
            {
                var cr = new Rect((i % cols) * (cell + 10), (i / cols) * (cell + 10) - s, cell, cell);
                if (cr.yMax < 0 || cr.y > grid.height) continue;
                bool sel = i == icon;
                UI.Rounded(cr, sel ? UI.Accent.WithAlpha(0.4f) : UI.Hover(cr) ? new Color(1, 1, 1, 0.12f) : UI.PanelLight, true);
                if (sel) UI.Outline(cr, UI.Accent, 3f);
                float pad = cell * 0.14f;
                PlayerProfile.DrawIcon(new Rect(cr.x + pad, cr.y + pad, cell - pad * 2, cell - pad * 2), i, iconColor);
                if (UI.Hit(cr)) { icon = i; AudioManager.Ui("ui_click"); }
            }
            UI.EndScroll("profile_icons", grid, content);
            Palette(new Rect(body.x, body.yMax - paletteH, body.width, paletteH), "Colour", PlayerProfile.Palette, ref iconColor);
        }

        void DrawHats(Rect body)
        {
            const float paletteH = 84f;
            var grid = new Rect(body.x, body.y, body.width, body.height - paletteH - 10);
            int n = Hats.Count;
            int cols = Columns(grid.width - 16, 200);
            float cw = Mathf.Floor((grid.width - 16 - (cols - 1) * 10) / cols), ch = 78f;
            int rows = Mathf.CeilToInt(n / (float)cols);
            float content = rows * (ch + 10);
            float s = UI.BeginScroll("profile_hats", grid, content);
            for (int i = 0; i < n; i++)
            {
                var cr = new Rect((i % cols) * (cw + 10), (i / cols) * (ch + 10) - s, cw, ch);
                if (cr.yMax < 0 || cr.y > grid.height) continue;
                bool sel = i == hat;
                UI.Rounded(cr, sel ? UI.Accent.WithAlpha(0.4f) : UI.Hover(cr) ? new Color(1, 1, 1, 0.12f) : UI.PanelLight, true);
                if (sel) UI.Outline(cr, UI.Accent, 3f);
                UI.Label(cr, Hats.Names[i].ToUpperInvariant(), 19, i == 0 ? UI.Muted : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
                if (UI.Hit(cr)) { hat = i; AudioManager.Ui("ui_click"); }
            }
            UI.EndScroll("profile_hats", grid, content);
            Palette(new Rect(body.x, body.yMax - paletteH, body.width, paletteH), "Colour", PlayerProfile.Palette, ref hatColor);
        }

        const float RowH = 74f;

        void DrawBody(Rect view)
        {
            const int rows = 10;
            float content = rows * RowH + 10;
            float s = UI.BeginScroll("profile_body", view, content);
            float w = view.width - 16;
            float y = -s;
            var before = look.Encode();
            int i;
            Palette(new Rect(0, y, w, 64), "Skin", SkinTones, ref look.Skin); y += RowH;
            i = look.HairStyle;
            if (UI.Selector(new Rect(0, y, w, 62), "Hair style", ref i, HairStyles)) look.HairStyle = i;
            y += RowH;
            Palette(new Rect(0, y, w, 64), "Hair", HairColors, ref look.Hair); y += RowH;
            Palette(new Rect(0, y, w, 64), "Shirt", ClothColors, ref look.Shirt); y += RowH;
            i = look.LongSleeves ? 1 : 0;
            if (UI.Selector(new Rect(0, y, w, 62), "Shirt style", ref i, ShirtStyles)) look.LongSleeves = i == 1;
            y += RowH;
            Palette(new Rect(0, y, w, 64), "Pants", PantsColors, ref look.Pants); y += RowH;
            UI.Slider(new Rect(0, y, w, 62), "Height", ref look.Height, CustomLook.MinHeight, CustomLook.MaxHeight, "0.00", null, "x"); y += RowH;
            UI.Slider(new Rect(0, y, w, 62), "Build", ref look.Build, CustomLook.MinBuild, CustomLook.MaxBuild, "0.00", null, "x"); y += RowH;
            float half = Mathf.Floor(w * 0.5f) - 8;
            UI.Toggle(new Rect(0, y, half, 62), "Beard", ref look.Beard);
            UI.Toggle(new Rect(half + 16, y, half, 62), "Glasses", ref look.Glasses);
            y += RowH;
            if (UI.Button(new Rect(0, y, w, 62), "RESET TO DEFAULT BODY", ButtonKind.Ghost, IconFactory.Get("refresh"), 18))
            {
                look = PlayerProfile.DefaultLook(PlayerProfile.LocalName); customBody = false;
                AudioManager.Ui("ui_click");
            }
            UI.EndScroll("profile_body", view, content);
            if (look.Encode() != before) customBody = true;
        }

        /// <summary>Label + big tappable colour swatches.</summary>
        static void Palette(Rect row, string label, Color[] palette, ref Color value)
        {
            UI.Label(new Rect(row.x, row.y, 140, row.height), label, 19, Color.white);
            float x0 = row.x + 150, avail = row.width - 150;
            float size = Mathf.Clamp(Mathf.Floor((avail - (palette.Length - 1) * 6f) / palette.Length), 30f, 60f);
            int best = -1; float bd = 0.004f;
            for (int k = 0; k < palette.Length; k++)
            {
                var d = palette[k] - value;
                float dist = d.r * d.r + d.g * d.g + d.b * d.b;
                if (dist < bd) { bd = dist; best = k; }
            }
            for (int k = 0; k < palette.Length; k++)
            {
                var sr = new Rect(x0 + k * (size + 6f), row.y + (row.height - size) * 0.5f, size, size);
                if (sr.xMax > row.xMax + 1f) break;
                var c = palette[k]; c.a = 1f;
                UI.Rounded(sr, c, true);
                if (k == best) { UI.Outline(sr, Color.white, 3f); UI.Outline(new Rect(sr.x - 3, sr.y - 3, sr.width + 6, sr.height + 6), UI.Accent, 2f); }
                if (UI.Hit(sr)) { value = palette[k]; AudioManager.Ui("ui_click"); }
            }
        }
    }
}
