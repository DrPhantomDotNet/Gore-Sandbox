using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Goredoll customizer (Spawn menu > AI > CUSTOMIZE): name, skin, hair, eyes, clothes, body, beard, glasses, armour
    /// and weapon, with a live rotating 3D preview. Up to 12 designs are saved (CustomDolls) and can be armed in the
    /// Spawn Gun. Everything is tap/drag friendly (big swatches and buttons) for mobile.
    /// </summary>
    public sealed class CustomizerScreen : UIScreen
    {
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
        static readonly Color[] EyeColors =
        {
            new Color(0.35f, 0.2f, 0.1f), new Color(0.15f, 0.1f, 0.06f), new Color(0.2f, 0.45f, 0.7f), new Color(0.3f, 0.5f, 0.3f),
            new Color(0.45f, 0.48f, 0.5f), new Color(0.55f, 0.4f, 0.15f),
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
        static readonly Color DarkShoes = new Color(0.12f, 0.1f, 0.09f), WhiteShoes = new Color(0.85f, 0.85f, 0.85f);
        static readonly string[] HairStyles = { "Short", "Buzz", "Bald", "Long" };
        static readonly string[] ShirtStyles = { "T-shirt", "Long sleeves" };
        static readonly string[] ShoeStyles = { "Dark", "White" };
        static readonly string[] Roles = { "Unarmed", "Pistol", "Rifle" };

        CustomLook look;
        int slot = -1;
        string savedCode;

        // Preview studio
        const int PreviewLayer = 31;
        GameObject studio, model;
        Camera cam;
        RenderTexture rt;
        string builtCode;
        float yaw = 200f;
        bool previewFailed;

        public override bool IsModal => true;

        public override void OnOpen()
        {
            AudioManager.Ui("ui_open");
            look = RandomLook();
        }

        public override void OnClose() => DestroyPreview();

        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }

        static CustomLook RandomLook()
        {
            var l = CustomLook.FromAppearance(Appearance.Random(-1));
            l.Name = "Custom " + l.Name;
            l.Name = CustomLook.CleanName(l.Name);
            return l;
        }

        // ------------------------------------------------------------------ preview

        void EnsureStudio()
        {
            if (studio != null || previewFailed) return;
            try
            {
                studio = new GameObject("CustomizerStudio");
                studio.transform.position = new Vector3(0, -5200, 0);
                var camGo = new GameObject("PreviewCam");
                camGo.transform.SetParent(studio.transform, false);
                camGo.transform.localPosition = new Vector3(0, 1.05f, 4.4f);
                camGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.05f, -1f), Vector3.up);
                cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.13f, 1f);
                cam.cullingMask = 1 << PreviewLayer;
                cam.fieldOfView = 30f;
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
                rt = new RenderTexture(desc) { name = "CustomizerPreview" };
                rt.Create();
                cam.targetTexture = rt;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Customizer preview failed: " + e.Message);
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
            studio = null; model = null; cam = null; builtCode = null;
        }

        public override void Tick()
        {
            CustomDolls.RenderPendingIcon();
            EnsureStudio();
            if (studio == null || cam == null) return;
            var code = look.Encode();
            if (code != builtCode)
            {
                builtCode = code;
                if (model != null) Object.Destroy(model);
                model = CustomDolls.BuildMannequin(look.ToAppearance(), look.Armor, look.Glasses, look.Role == 1 ? "glock" : look.Role == 2 ? "ar15" : null, PreviewLayer);
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
            catch (System.Exception e) { Debug.LogWarning("Customizer preview render failed: " + e.Message); previewFailed = true; DestroyPreview(); }
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
            UI.Dim(0.55f * a);
            var r = UI.Centered(Mathf.Min(1560, UI.VW - 40), Mathf.Min(1020, UI.VH - 30));
            UI.Panel(r, "Goredoll Customizer", a);
            UI.Label(new Rect(r.x + 400, r.y + 22, r.width - 430, 36), slot >= 0 ? "Editing saved doll " + (slot + 1) : "New doll (not saved)", 18, UI.Accent2, TextAnchor.MiddleRight);

            float top = r.y + 78, bottom = r.yMax - 20;
            const float stripH = 150f;
            float previewW = Mathf.Clamp(r.width * 0.32f, 280f, 480f);
            var preview = new Rect(r.x + 24, top, previewW, bottom - stripH - 16 - top);
            DrawPreview(preview);
            DrawSaved(new Rect(r.x + 24, bottom - stripH, previewW, stripH));

            var right = new Rect(preview.xMax + 24, top, r.xMax - preview.xMax - 48, bottom - top);
            // Name (outside the scroll view so the text field behaves).
            UI.Label(new Rect(right.x, right.y, 150, 56), "Name", 20, Color.white);
            var nm = UI.TextField(new Rect(right.x + 160, right.y, right.width - 160, 56), look.Name, "custom_doll_name", false, "Name");
            if (nm != look.Name) look.Name = nm != null && nm.Length > 16 ? nm.Substring(0, 16) : nm;

            const float buttonsH = 140f;
            var view = new Rect(right.x, right.y + 70, right.width, right.height - 70 - buttonsH - 10);
            DrawControls(view);
            DrawButtons(new Rect(right.x, right.yMax - buttonsH, right.width, buttonsH));
        }

        void DrawPreview(Rect p)
        {
            UI.Rounded(p, UI.PanelLight, false);
            if (rt != null && !previewFailed) UI.Tex(new Rect(p.x + 8, p.y + 8, p.width - 16, p.height - 16), rt, Color.white, ScaleMode.ScaleToFit);
            else UI.Tex(new Rect(p.x + p.width * 0.25f, p.y + p.height * 0.3f, p.width * 0.5f, p.width * 0.5f), IconFactory.Get("person"), UI.Muted, ScaleMode.ScaleToFit);
            UI.Label(new Rect(p.x + 16, p.yMax - 44, p.width - 32, 36), CustomLook.CleanName(look.Name), 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
        }

        void DrawSaved(Rect s)
        {
            var list = CustomDolls.Saved;
            UI.Label(new Rect(s.x, s.y, s.width, 26), "Saved (" + list.Count + "/" + CustomDolls.MaxSaved + ") - tap to edit", 16, UI.Muted);
            const float cw = 92, ch = 112, gap = 8;
            float content = list.Count * (cw + gap);
            var view = new Rect(s.x, s.y + 32, s.width, ch);
            // Horizontal strip: lay the cards out in rows when they don't fit (scrolls vertically).
            int cols = Mathf.Max(1, Mathf.FloorToInt((s.width + gap) / (cw + gap)));
            int rows = Mathf.CeilToInt(list.Count / (float)cols);
            content = rows * (ch + gap);
            if (list.Count == 0)
            {
                UI.Rounded(view, UI.PanelLight, false);
                UI.Label(view, "Nothing saved yet - press SAVE.", 16, UI.Muted, TextAnchor.MiddleCenter);
                return;
            }
            float sc = UI.BeginScroll("custom_saved", view, content);
            for (int i = 0; i < list.Count; i++)
            {
                var cr = new Rect((i % cols) * (cw + gap), (i / cols) * (ch + gap) - sc, cw, ch);
                if (cr.yMax < 0 || cr.y > view.height) continue;
                bool sel = i == slot;
                UI.Rounded(cr, sel ? new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.45f) : UI.Hover(cr) ? new Color(1, 1, 1, 0.12f) : UI.PanelLight, true);
                if (sel) UI.Outline(cr, UI.Accent, 2f);
                var key = "prop_" + CustomDolls.IdPrefix + list[i];
                UI.Tex(new Rect(cr.x + 6, cr.y + 4, cw - 12, ch - 34), IconFactory.Has(key) ? IconFactory.Get(key) : IconFactory.Get("person"), Color.white, ScaleMode.ScaleToFit);
                var l = CustomLook.Decode(list[i]);
                UI.Label(new Rect(cr.x + 2, cr.yMax - 30, cw - 4, 28), l != null ? l.Name : "?", 13, Color.white, TextAnchor.MiddleCenter);
                if (UI.Hit(cr) && l != null)
                {
                    look = l; slot = i; savedCode = list[i];
                    AudioManager.Ui("ui_click");
                }
            }
            UI.EndScroll("custom_saved", view, content);
        }

        const float RowH = 66f;

        void DrawControls(Rect view)
        {
            const int rows = 13;
            float content = rows * RowH + 10;
            float s = UI.BeginScroll("custom_controls", view, content);
            float w = view.width - 16;
            float y = -s;
            int i;

            i = look.Role;
            if (UI.Selector(new Rect(0, y, w, 56), "Weapon", ref i, Roles, "Unarmed uses the spawn-menu personality. Pistol = Gunman, Rifle = Soldier: they shoot at you and at zombies.")) look.Role = i;
            y += RowH;
            i = look.Armor;
            if (UI.Selector(new Rect(0, y, w, 56), "Armor", ref i, Armor.Options, "Helmet and/or vest. Pistol rounds bounce off, rifles are weakened, heavy rounds go through.")) look.Armor = i;
            y += RowH;
            Swatches(new Rect(0, y, w, 56), "Skin", SkinTones, ref look.Skin);
            y += RowH;
            i = look.HairStyle;
            if (UI.Selector(new Rect(0, y, w, 56), "Hair style", ref i, HairStyles)) look.HairStyle = i;
            y += RowH;
            Swatches(new Rect(0, y, w, 56), "Hair colour", HairColors, ref look.Hair);
            y += RowH;
            Swatches(new Rect(0, y, w, 56), "Eye colour", EyeColors, ref look.Eyes);
            y += RowH;
            Swatches(new Rect(0, y, w, 56), "Shirt", ClothColors, ref look.Shirt);
            y += RowH;
            i = look.LongSleeves ? 1 : 0;
            if (UI.Selector(new Rect(0, y, w, 56), "Shirt style", ref i, ShirtStyles)) look.LongSleeves = i == 1;
            y += RowH;
            Swatches(new Rect(0, y, w, 56), "Pants", PantsColors, ref look.Pants);
            y += RowH;
            i = look.Shoes.r > 0.5f ? 1 : 0;
            if (UI.Selector(new Rect(0, y, w, 56), "Shoes", ref i, ShoeStyles)) look.Shoes = i == 1 ? WhiteShoes : DarkShoes;
            y += RowH;
            UI.Slider(new Rect(0, y, w, 56), "Height", ref look.Height, CustomLook.MinHeight, CustomLook.MaxHeight, "0.00", null, "x");
            y += RowH;
            UI.Slider(new Rect(0, y, w, 56), "Build", ref look.Build, CustomLook.MinBuild, CustomLook.MaxBuild, "0.00", null, "x");
            y += RowH;
            float half = Mathf.Floor(w * 0.5f) - 8;
            UI.Toggle(new Rect(0, y, half, 56), "Beard", ref look.Beard);
            UI.Toggle(new Rect(half + 16, y, half, 56), "Glasses", ref look.Glasses);
            UI.EndScroll("custom_controls", view, content);
        }

        /// <summary>Label + a row of big tappable colour swatches (wraps to the available width).</summary>
        static void Swatches(Rect row, string label, Color[] palette, ref Color value)
        {
            UI.Label(new Rect(row.x, row.y, 150, row.height), label, 19, Color.white);
            float x0 = row.x + 160, avail = row.width - 160;
            float size = Mathf.Clamp(Mathf.Floor((avail - (palette.Length - 1) * 6f) / palette.Length), 30f, 56f);
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

        void DrawButtons(Rect b)
        {
            float gap = 12f, h = 62f;
            float w3 = (b.width - gap * 2f) / 3f;
            // Row 1: editing
            if (UI.Button(new Rect(b.x, b.y, w3, h), "RANDOM", ButtonKind.Normal, null, 20))
            {
                var keepRole = look.Role; var keepArmor = look.Armor;
                look = RandomLook(); look.Role = keepRole; look.Armor = keepArmor;
                AudioManager.Ui("ui_click");
            }
            if (UI.Button(new Rect(b.x + w3 + gap, b.y, w3, h), slot >= 0 ? "SAVE" : "SAVE NEW", ButtonKind.Primary, IconFactory.Get("save"), 20)) Save(false);
            if (slot >= 0)
            {
                if (UI.Button(new Rect(b.x + (w3 + gap) * 2f, b.y, (w3 - gap) * 0.5f, h), "COPY", ButtonKind.Normal, null, 18)) Save(true);
                if (UI.Button(new Rect(b.x + (w3 + gap) * 2f + (w3 + gap) * 0.5f, b.y, (w3 - gap) * 0.5f, h), "DELETE", ButtonKind.Danger, IconFactory.Get("trash"), 18))
                {
                    CustomDolls.Delete(slot);
                    slot = -1; savedCode = null;
                    GameEvents.ShowToast("Custom doll deleted");
                }
            }
            else if (UI.Button(new Rect(b.x + (w3 + gap) * 2f, b.y, w3, h), "NEW", ButtonKind.Normal, IconFactory.Get("plus"), 20))
            {
                look = RandomLook(); slot = -1; savedCode = null;
            }
            // Row 2: use it
            float y2 = b.y + h + 14f;
            if (UI.Button(new Rect(b.x, y2, w3, h), "USE IN SPAWN GUN", ButtonKind.Primary, null, 18)) UseInSpawnGun(false);
            if (UI.Button(new Rect(b.x + w3 + gap, y2, w3, h), "SPAWN IN FRONT", ButtonKind.Ghost, IconFactory.Get("plus"), 18)) UseInSpawnGun(true);
            if (Application.isEditor)
            {
                // Unity editor only: copy the look code for a story actor (Look code field, see DEVELOPING.md).
                float hw = (w3 - gap) * 0.5f;
                if (UI.Button(new Rect(b.x + (w3 + gap) * 2f, y2, hw, h), "LOOK CODE", ButtonKind.Normal, null, 16, true, "Copies this look's code for a story actor"))
                {
                    GUIUtility.systemCopyBuffer = look.Encode();
                    Debug.Log("[Customizer] Look code copied: " + look.Encode());
                    GameEvents.ShowToast("Look code copied: paste it into a story actor's Look code");
                }
                if (UI.Button(new Rect(b.x + (w3 + gap) * 2f + hw + gap, y2, hw, h), "BACK", ButtonKind.Normal, IconFactory.Get("close"), 18)) OnBack();
            }
            else if (UI.Button(new Rect(b.x + (w3 + gap) * 2f, y2, w3, h), "BACK", ButtonKind.Normal, IconFactory.Get("close"), 20)) OnBack();
        }

        bool Save(bool asNew)
        {
            look.Name = CustomLook.CleanName(look.Name);
            int s = CustomDolls.Save(look, asNew ? -1 : slot);
            if (s < 0)
            {
                GameEvents.ShowToast("All " + CustomDolls.MaxSaved + " slots are full - delete one first.");
                AudioManager.Ui("ui_error");
                return false;
            }
            slot = s;
            savedCode = CustomDolls.Saved[s];
            GameEvents.ShowToast("Saved " + look.Name);
            AudioManager.Ui("ui_click");
            return true;
        }

        void UseInSpawnGun(bool spawnNow)
        {
            // Unsaved changes are saved first so the Spawn Gun entry matches what you see.
            if (savedCode != look.Encode() && !Save(false)) return;
            var def = CustomDolls.DefForId(CustomDolls.IdPrefix + savedCode);
            if (def == null) return;
            SpawnCatalog.Selected = def;
            var p = PlayerController.Instance;
            if (p == null) return;
            if (!spawnNow)
            {
                if (!(p.Inventory.Current is SpawnGun))
                {
                    int idx = p.Inventory.Give("spawngun");
                    if (idx >= 0) p.Inventory.Select(idx, true);
                }
                GameEvents.ShowToast(def.Name + " selected - middle-click to spawn");
                return;
            }
            var pos = p.transform.position + MathX.Flat(p.AimDirection).normalized * 2.5f;
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 10f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) pos = hit.point;
            var rot = Quaternion.LookRotation(MathX.Flat(p.transform.position - pos) + Vector3.forward * 0.001f);
            SpawnCatalog.Spawn(def, pos, rot);
            AudioManager.Play("sg_spawn", pos, 0.7f);
        }
    }
}
