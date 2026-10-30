using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>
    /// The Weapon Editor (Modding Hub > MY MODS > NEW WEAPON). Build a gun or melee weapon out of parts, place its
    /// special points (bullet source, hold point, support hand, sights, shell eject), pick the trigger and tune every
    /// stat. Everything is clamped by <see cref="WeaponRules"/> (the anti-cheat limits) when it's saved or tested.
    /// </summary>
    public sealed class WeaponEditorScreen : UIScreen
    {
        readonly ModPackage pkg;
        WeaponDesign d;
        int tab, part = -1, point;
        string status;
        bool statusBad;
        float statusTime;

        static readonly string[] TabNames = { "BASICS", "PARTS", "POINTS", "TRIGGER", "STATS" };
        static readonly string[] PointNames = { "Bullet source", "Hold point", "Support hand", "Rear sight", "Front sight", "Shell eject" };
        static readonly string[] MeleePointNames = { "Tip", "Hold point" };
        static readonly Color[] PointColors = { new Color(1f, 0.35f, 0.2f), new Color(0.3f, 0.9f, 0.4f), new Color(0.3f, 0.6f, 1f), new Color(1f, 0.9f, 0.3f), new Color(1f, 0.6f, 0.1f), new Color(0.8f, 0.5f, 1f) };
        static readonly Color[] Palette =
        {
            new Color(0.11f, 0.11f, 0.12f), new Color(0.24f, 0.24f, 0.26f), new Color(0.7f, 0.72f, 0.75f), new Color(0.95f, 0.95f, 0.95f),
            new Color(0.45f, 0.28f, 0.15f), new Color(0.62f, 0.45f, 0.28f), new Color(0.36f, 0.4f, 0.24f), new Color(0.76f, 0.66f, 0.46f),
            new Color(0.8f, 0.12f, 0.1f), new Color(0.95f, 0.5f, 0.1f), new Color(0.95f, 0.85f, 0.2f), new Color(0.2f, 0.65f, 0.25f),
            new Color(0.2f, 0.85f, 0.95f), new Color(0.15f, 0.3f, 0.8f), new Color(0.55f, 0.2f, 0.75f), new Color(0.95f, 0.5f, 0.7f),
        };

        // Preview studio
        const int PreviewLayer = 31;
        GameObject studio, model;
        Camera cam;
        RenderTexture rt;
        string builtKey;
        float yaw = 120f, pitch = 15f, zoom = 1f;
        bool spin = true, previewFailed;
        Rect previewRect;

        public override bool IsModal => true;
        public override bool BlocksGameplay => true;

        public WeaponEditorScreen(ModPackage p)
        {
            pkg = p ?? ModStore.NewPackage(ModKind.Weapon, "My Weapon");
            WeaponRules.Parse(pkg.data, out d, null);
            if (d == null || d.parts == null || d.parts.Count == 0) d = WeaponDesign.Template(0);
        }

        public override void OnOpen() { AudioManager.Ui("ui_open"); }
        public override void OnClose() => DestroyPreview();

        // ------------------------------------------------------------------ actions

        void Status(string text, bool bad)
        {
            status = text; statusBad = bad; statusTime = Time.unscaledTime;
            AudioManager.Ui(bad ? "ui_error" : "ui_success");
        }

        void Commit() => pkg.data = JsonUtility.ToJson(d);

        bool Save()
        {
            if (string.IsNullOrWhiteSpace(pkg.name)) { Status("Give your weapon a name first.", true); return false; }
            Commit();
            var err = ModHub.Validate(pkg);
            if (err != null) { Status(err, true); return false; }
            WeaponRules.Parse(pkg.data, out var clamped, null);
            if (clamped != null) d = clamped;
            AdvancedModding.Save(pkg);
            string notes = ModWeapons.LastNotes.Count > 0 ? "  (limited: " + string.Join(", ", ModWeapons.LastNotes.ToArray()) + ")" : "";
            Status("Saved to My Mods and installed. Find it in the spawn menu with the MODDED tag." + notes, false);
            return true;
        }

        void Test()
        {
            Commit();
            var err = ModHub.Validate(ModStore.Copy(pkg));
            if (err != null) { Status(err, true); return; }
            bool playing = GameManager.I != null && GameManager.I.State == GameState.Playing;
            ModWeapons.Test(pkg);
            if (playing) while (UIRoot.Count > 0) UIRoot.Pop(); // otherwise loading the test map clears the menus

        }

        void LoadTemplate(int t)
        {
            d = WeaponDesign.Template(t);
            part = -1;
            Status("Started from the " + WeaponDesign.Templates[t] + " template.", false);
        }

        public override bool OnBack()
        {
            AudioManager.Ui("ui_close");
            UIRoot.Pop();
            return true;
        }

        // ------------------------------------------------------------------ preview

        void EnsureStudio()
        {
            if (studio != null || previewFailed) return;
            try
            {
                studio = new GameObject("WeaponEditorStudio");
                studio.transform.position = new Vector3(0, -5800, 0);
                var camGo = new GameObject("PreviewCam");
                camGo.transform.SetParent(studio.transform, false);
                cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f, 1f);
                cam.cullingMask = 1 << PreviewLayer;
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 20f;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                AddLight(new Vector3(-0.5f, -0.7f, 0.6f), 1.2f, Color.white);
                AddLight(new Vector3(0.7f, -0.2f, -0.6f), 0.55f, new Color(0.75f, 0.8f, 1f));
                var desc = new RenderTextureDescriptor(768, 512, RenderTextureFormat.ARGB32, 24)
                {
                    sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear, msaaSamples = 1,
                };
                rt = new RenderTexture(desc) { name = "WeaponEditorPreview" };
                rt.Create();
                cam.targetTexture = rt;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Weapon editor preview failed: " + e.Message);
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

        Vector3 Centre()
        {
            if (d.parts.Count == 0) return Vector3.zero;
            var b = new Bounds(d.parts[0].pos, d.parts[0].size);
            foreach (var p in d.parts) b.Encapsulate(new Bounds(p.pos, p.size));
            return b.center;
        }

        float Extent()
        {
            float e = 0.2f;
            var c = Centre();
            foreach (var p in d.parts) e = Mathf.Max(e, (p.pos - c).magnitude + p.size.magnitude * 0.5f);
            return e;
        }

        public override void Tick()
        {
            EnsureStudio();
            if (studio == null || cam == null) return;
            string key = JsonUtility.ToJson(d) + "|" + part;
            if (key != builtKey)
            {
                builtKey = key;
                if (model != null) Object.Destroy(model);
                var wm = ModWeapons.BuildModel(d, PreviewLayer, pkg.name, true);
                model = wm.gameObject;
                SetLayer(model.transform);
                model.transform.SetParent(studio.transform, false);
                model.transform.localPosition = Vector3.zero;
            }
            if (spin) yaw += Time.unscaledDeltaTime * 25f;
            var c = Centre();
            float dist = Extent() * 3.6f / Mathf.Max(0.3f, zoom);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.localPosition = c + rot * new Vector3(0, 0, -dist);
            cam.transform.localRotation = rot;
            var oldAmbient = RenderSettings.ambientLight;
            var oldMode = RenderSettings.ambientMode;
            try
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
                cam.Render();
            }
            catch (System.Exception e) { Debug.LogWarning("Weapon editor render failed: " + e.Message); previewFailed = true; DestroyPreview(); }
            finally
            {
                RenderSettings.ambientLight = oldAmbient;
                RenderSettings.ambientMode = oldMode;
            }
        }

        static void SetLayer(Transform t)
        {
            t.gameObject.layer = PreviewLayer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i));
        }

        /// <summary>Draws a marker on the preview for a point in model space.</summary>
        void Marker(Vector3 local, Color c, string label, bool big)
        {
            if (cam == null || studio == null) return;
            var vp = cam.WorldToViewportPoint(studio.transform.TransformPoint(local));
            if (vp.z <= 0) return;
            // The texture is drawn ScaleToFit inside previewRect.
            float aspect = 768f / 512f, w = previewRect.width, h = previewRect.width / aspect;
            if (h > previewRect.height) { h = previewRect.height; w = h * aspect; }
            float x0 = previewRect.x + (previewRect.width - w) * 0.5f, y0 = previewRect.y + (previewRect.height - h) * 0.5f;
            var p = new Vector2(x0 + vp.x * w, y0 + (1f - vp.y) * h);
            float s = big ? 22f : 14f;
            UI.Rounded(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), c, true);
            if (big && !string.IsNullOrEmpty(label)) UI.Label(new Rect(p.x + 14, p.y - 14, 260, 28), label, 16, c);
        }

        // ------------------------------------------------------------------ draw

        public override void Draw()
        {
            UI.Fill(new Rect(0, 0, UI.VW, UI.VH), new Color(0.03f, 0.03f, 0.04f, 0.97f));
            UI.Label(new Rect(30, 14, UI.VW - 900, 60), "<b>WEAPON EDITOR</b>  <color=#999999>" + (string.IsNullOrEmpty(pkg.name) ? "" : pkg.name) + "</color>", 32, Color.white);
            float bw = 200;
            if (UI.Button(new Rect(UI.VW - (bw + 16) * 4 - 14, 14, bw, 60), "BACK", ButtonKind.Ghost, IconFactory.Get("arrow_left"), 22)) { OnBack(); return; }
            if (UI.Button(new Rect(UI.VW - (bw + 16) * 3 - 14, 14, bw, 60), "TEST", ButtonKind.Normal, IconFactory.Get("play"), 22, true, "Try it right away on a test map.")) { Test(); return; }
            if (UI.Button(new Rect(UI.VW - (bw + 16) * 2 - 14, 14, bw, 60), "TEMPLATE", ButtonKind.Normal, IconFactory.Get("refresh"), 20)) { tab = 0; }
            if (UI.Button(new Rect(UI.VW - (bw + 16) - 14, 14, bw, 60), "SAVE", ButtonKind.Primary, IconFactory.Get("check"), 24)) Save();

            if (!string.IsNullOrEmpty(status) && Time.unscaledTime - statusTime < 8f)
                UI.Label(new Rect(30, 78, UI.VW - 60, 36), status, 18, statusBad ? UI.Bad : UI.Good);

            float top = 122, bottom = UI.VH - 20;
            bool narrow = UI.VW < 1500;
            float leftW = narrow ? UI.VW * 0.44f : UI.VW * 0.5f;
            previewRect = new Rect(20, top, leftW, bottom - top - 80);
            UI.Rounded(previewRect, UI.PanelLight, false);
            if (rt != null && !previewFailed) UI.Tex(previewRect, rt, Color.white, ScaleMode.ScaleToFit);
            else UI.Label(previewRect, "Preview not available on this device.", 20, UI.Muted, TextAnchor.MiddleCenter);
            DrawMarkers();

            // View controls under the preview
            float vy = previewRect.yMax + 12, vb = (leftW - 5 * 10) / 6f;
            if (UI.Button(new Rect(20, vy, vb, 60), "<", ButtonKind.Normal, null, 26)) { spin = false; yaw += 30f; }
            if (UI.Button(new Rect(20 + (vb + 10), vy, vb, 60), ">", ButtonKind.Normal, null, 26)) { spin = false; yaw -= 30f; }
            if (UI.Button(new Rect(20 + (vb + 10) * 2, vy, vb, 60), "SIDE", ButtonKind.Normal, null, 18)) { spin = false; yaw = 90f; pitch = 0f; }
            if (UI.Button(new Rect(20 + (vb + 10) * 3, vy, vb, 60), "TOP", ButtonKind.Normal, null, 18)) { spin = false; yaw = 90f; pitch = 85f; }
            if (UI.Button(new Rect(20 + (vb + 10) * 4, vy, vb, 60), zoom > 1.5f ? "ZOOM -" : "ZOOM +", ButtonKind.Normal, null, 18)) zoom = zoom > 1.5f ? 1f : 2.2f;
            if (UI.Button(new Rect(20 + (vb + 10) * 5, vy, vb, 60), "SPIN", spin ? ButtonKind.Primary : ButtonKind.Normal, null, 18)) spin = !spin;

            var right = new Rect(previewRect.xMax + 20, top, UI.VW - previewRect.xMax - 40, bottom - top);
            int nt = UI.Tabs(new Rect(right.x, right.y, right.width, 56), tab, TabNames, narrow ? 16 : 20);
            if (nt != tab) { tab = nt; AudioManager.Ui("ui_tab"); }
            var body = new Rect(right.x, right.y + 68, right.width, right.height - 68);
            switch (tab)
            {
                case 0: DrawBasics(body); break;
                case 1: DrawParts(body); break;
                case 2: DrawPoints(body); break;
                case 3: DrawTrigger(body); break;
                case 4: DrawStats(body); break;
            }
        }

        void DrawMarkers()
        {
            if (tab == 2)
            {
                var pts = Points();
                for (int i = 0; i < pts.Length; i++) Marker(pts[i], PointColors[i], (d.melee ? MeleePointNames : PointNames)[i], i == point);
            }
            else if (tab == 1 && part >= 0 && part < d.parts.Count) Marker(d.parts[part].pos, UI.Accent2, d.parts[part].name, true);
        }

        Vector3[] Points()
        {
            if (d.melee) return new[] { d.muzzlePos, d.gripPos };
            return new[] { d.muzzlePos, d.gripPos, d.supportPos, d.sightRear, d.sightFront, d.ejectPos };
        }

        void SetPoint(int i, Vector3 v)
        {
            if (d.melee) { if (i == 0) d.muzzlePos = v; else d.gripPos = v; return; }
            switch (i)
            {
                case 0: d.muzzlePos = v; break;
                case 1: d.gripPos = v; break;
                case 2: d.supportPos = v; break;
                case 3: d.sightRear = v; break;
                case 4: d.sightFront = v; break;
                case 5: d.ejectPos = v; break;
            }
        }

        // A simple scrolling column of rows
        float rowY;
        Rect col;
        Rect Row(float h = 64) { var r = new Rect(0, rowY, col.width - 18, h); rowY += h + 10; return r; }

        delegate void Rows();
        readonly Dictionary<int, float> contentHeights = new Dictionary<int, float>();

        void Scroll(string id, Rect view, Rows rows)
        {
            int key = id.GetHashCode();
            contentHeights.TryGetValue(key, out var content);
            if (content < view.height) content = view.height;
            col = view;
            float s = UI.BeginScroll(id, view, content);
            rowY = -s;
            rows();
            contentHeights[key] = rowY + s + 20;
            UI.EndScroll(id, view, content);
        }

        // ------------------------------------------------------------------ tabs

        void DrawBasics(Rect body)
        {
            Scroll("we_basics", body, () =>
            {
                UI.Label(Row(30), "NAME", 18, UI.Muted);
                var n = UI.TextField(Row(), pkg.name, "we_name", false, "Weapon name");
                pkg.name = n != null && n.Length > 32 ? n.Substring(0, 32) : n ?? "";
                UI.Label(Row(30), "DESCRIPTION", 18, UI.Muted);
                var ds = UI.TextField(Row(), pkg.description, "we_desc", false, "What is it?");
                pkg.description = ds != null && ds.Length > 200 ? ds.Substring(0, 200) : ds ?? "";

                bool melee = d.melee;
                if (UI.Toggle(Row(), "Melee weapon (knife, sword, bat...)", ref melee, "Melee weapons swing instead of shooting.") && melee != d.melee)
                {
                    d.melee = melee;
                    point = 0;
                }
                UI.Selector(Row(), "Icon", ref d.icon, WeaponDesign.Icons);

                UI.Label(Row(40), "START FROM A TEMPLATE (replaces your parts)", 18, UI.Muted);
                var tr = Row(64);
                int per = 4;
                float tw = (tr.width - (per - 1) * 10) / per;
                for (int i = 0; i < WeaponDesign.Templates.Length; i++)
                {
                    if (i > 0 && i % per == 0) tr = Row(64);
                    if (UI.Button(new Rect(tr.x + (i % per) * (tw + 10), tr.y, tw, 64), WeaponDesign.Templates[i], ButtonKind.Normal, null, 18)) LoadTemplate(i);
                }
                UI.Label(Row(150), "Tips: build the look in PARTS, then place the bullet source and the hand positions in POINTS. TRIGGER picks how it fires and STATS sets damage, recoil and more. Mods are data only, so they can't carry viruses, and every number has a fair limit so nobody can make a cheat weapon.", 17, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            });
        }

        void DrawParts(Rect body)
        {
            float listW = Mathf.Min(300, body.width * 0.38f);
            var list = new Rect(body.x, body.y, listW, body.height - 150);
            float rowH = 54, content = d.parts.Count * (rowH + 6);
            float s = UI.BeginScroll("we_parts", list, content);
            for (int i = 0; i < d.parts.Count; i++)
            {
                var rr = new Rect(0, i * (rowH + 6) - s, list.width - 16, rowH);
                if (rr.yMax < 0 || rr.y > list.height) continue;
                string tag = i == d.magPart ? "  [mag]" : i == d.slidePart ? "  [slide]" : "";
                if (UI.Button(rr, d.parts[i].name + tag, i == part ? ButtonKind.Primary : ButtonKind.Normal, null, 17)) part = i;
            }
            UI.EndScroll("we_parts", list, content);

            float by = list.yMax + 10, hb = (listW - 10) / 2f;
            bool full = d.parts.Count >= WeaponRules.MaxParts;
            if (UI.Button(new Rect(body.x, by, hb, 60), "ADD", ButtonKind.Normal, IconFactory.Get("plus"), 18, !full))
            {
                var c = part >= 0 && part < d.parts.Count ? d.parts[part] : null;
                d.parts.Add(new WPart { name = "Part " + (d.parts.Count + 1), pos = c != null ? c.pos + new Vector3(0, 0.03f, 0) : Vector3.zero, color = c != null ? c.color : Palette[1] });
                part = d.parts.Count - 1;
            }
            if (UI.Button(new Rect(body.x + hb + 10, by, hb, 60), "COPY", ButtonKind.Normal, null, 18, !full && part >= 0))
            {
                var c = d.parts[part].Clone();
                c.name += " copy";
                c.pos += new Vector3(0.03f, 0, 0);
                d.parts.Add(c);
                part = d.parts.Count - 1;
            }
            if (UI.Button(new Rect(body.x, by + 70, listW, 60), "DELETE", ButtonKind.Danger, IconFactory.Get("trash"), 18, part >= 0 && d.parts.Count > 1))
            {
                d.parts.RemoveAt(part);
                if (d.magPart == part) d.magPart = -1; else if (d.magPart > part) d.magPart--;
                if (d.slidePart == part) d.slidePart = -1; else if (d.slidePart > part) d.slidePart--;
                part = Mathf.Min(part, d.parts.Count - 1);
            }

            var edit = new Rect(body.x + listW + 16, body.y, body.width - listW - 16, body.height);
            if (part < 0 || part >= d.parts.Count)
            {
                UI.Label(edit, "Pick a part on the left to edit it, or ADD a new one.", 20, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                return;
            }
            var p = d.parts[part];
            Scroll("we_part_edit", edit, () =>
            {
                var n = UI.TextField(Row(), p.name, "we_part_name", false, "Part name");
                p.name = string.IsNullOrEmpty(n) ? "Part" : n.Length > 24 ? n.Substring(0, 24) : n;
                UI.Selector(Row(), "Shape", ref p.shape, WeaponDesign.Shapes);
                UI.Selector(Row(), "Material", ref p.mat, WeaponDesign.Materials, "Glow parts light up.");
                ColorRow(ref p.color);
                Vec("Position", ref p.pos, -WeaponRules.MaxOffset * 0.5f, WeaponRules.MaxOffset * 0.5f, "0.000");
                Vec("Size", ref p.size, 0.002f, 0.8f, "0.000");
                Vec("Rotation", ref p.rot, -180f, 180f, "0");
                if (!d.melee)
                {
                    bool isMag = d.magPart == part, isSlide = d.slidePart == part;
                    if (UI.Toggle(Row(), "This is the magazine (drops out on reload)", ref isMag)) d.magPart = isMag ? part : -1;
                    if (UI.Toggle(Row(), "This is the slide / bolt (moves when firing)", ref isSlide)) d.slidePart = isSlide ? part : -1;
                }
            });
        }

        void ColorRow(ref Color c)
        {
            var r = Row(48);
            float sw = Mathf.Min(48, (r.width - (Palette.Length - 1) * 4) / Palette.Length);
            for (int i = 0; i < Palette.Length; i++)
            {
                var sr = new Rect(r.x + i * (sw + 4), r.y, sw, 48);
                bool sel = (Palette[i] - c).maxColorComponent < 0.02f && (c - Palette[i]).maxColorComponent < 0.02f;
                UI.Rounded(sel ? new Rect(sr.x - 3, sr.y - 3, sr.width + 6, sr.height + 6) : sr, sel ? Color.white : new Color(0, 0, 0, 0.6f), true);
                if (UI.Button(new Rect(sr.x + 3, sr.y + 3, sr.width - 6, sr.height - 6), "", ButtonKind.Ghost, null, 10)) c = Palette[i];
                UI.Rounded(new Rect(sr.x + 3, sr.y + 3, sr.width - 6, sr.height - 6), Palette[i], true);
            }
            UI.Slider(Row(), "Red", ref c.r, 0f, 1f);
            UI.Slider(Row(), "Green", ref c.g, 0f, 1f);
            UI.Slider(Row(), "Blue", ref c.b, 0f, 1f);
            c.a = 1f;
        }

        void Vec(string label, ref Vector3 v, float min, float max, string fmt)
        {
            UI.Label(Row(30), label.ToUpperInvariant(), 17, UI.Muted);
            UI.Slider(Row(), "X (left / right)", ref v.x, min, max, fmt);
            UI.Slider(Row(), "Y (down / up)", ref v.y, min, max, fmt);
            UI.Slider(Row(), "Z (back / forward)", ref v.z, min, max, fmt);
        }

        void DrawPoints(Rect body)
        {
            var names = d.melee ? MeleePointNames : PointNames;
            if (point >= names.Length) point = 0;
            float bw = (body.width - (names.Length > 3 ? 2 : names.Length - 1) * 10) / Mathf.Min(3, names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                var b = new Rect(body.x + (i % 3) * (bw + 10), body.y + (i / 3) * 64, bw, 56);
                if (UI.Button(b, names[i], i == point ? ButtonKind.Primary : ButtonKind.Normal, null, 17)) point = i;
                UI.Rounded(new Rect(b.x + 8, b.y + 8, 10, 10), PointColors[i], true);
            }
            float rowsTop = body.y + ((names.Length + 2) / 3) * 64 + 8;
            var rest = new Rect(body.x, rowsTop, body.width, body.yMax - rowsTop);
            Scroll("we_points", rest, () =>
            {
                string[] help = d.melee
                    ? new[] { "Where the weapon hits from: the tip of the blade or the head of the bat.", "Where your right hand holds it." }
                    : new[] { "Where bullets and the muzzle flash come out (the end of the barrel).", "Where your right hand holds the grip.", "Where your left hand goes (on the handguard or under the grip).", "The back of the iron sights. Aiming lines this up with the front sight.", "The front sight post. It must be in front of the rear sight.", "Where empty shells fly out." };
                UI.Label(Row(56), help[point], 17, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                var v = Points()[point];
                Vec(names[point], ref v, -WeaponRules.MaxOffset * 0.5f, WeaponRules.MaxOffset * 0.5f, "0.000");
                SetPoint(point, v);
                if (!d.melee && point == 0)
                {
                    UI.Label(Row(30), "BULLET DIRECTION (degrees)", 17, UI.Muted);
                    UI.Slider(Row(), "Up / down", ref d.muzzleRot.x, -30f, 30f, "0");
                    UI.Slider(Row(), "Left / right", ref d.muzzleRot.y, -30f, 30f, "0");
                }
                if (!d.melee && point == 2) UI.Toggle(Row(), "Use the support hand", ref d.useSupport, "Off: one-handed, like a pistol.");
                if (UI.Button(Row(60), "SNAP TO SELECTED PART", ButtonKind.Normal, null, 18, part >= 0 && part < d.parts.Count, "Moves this point to the centre of the part picked in PARTS."))
                    SetPoint(point, d.parts[part].pos);
            });
        }

        void DrawTrigger(Rect body)
        {
            Scroll("we_trigger", body, () =>
            {
                if (d.melee)
                {
                    UI.Selector(Row(), "Melee type", ref d.meleeType, WeaponDesign.MeleeTypes);
                    UI.Slider(Row(), "Swing speed", ref d.meleeSpeed, 0.4f, 2f, "0.00", null, "x");
                    UI.Label(Row(80), "Melee weapons swing when you press fire. Blades cut limbs off, blunt weapons knock people back.", 17, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                    return;
                }
                UI.Selector(Row(), "Trigger", ref d.trigger, WeaponDesign.Triggers);
                switch (d.trigger)
                {
                    case 2:
                    {
                        float b = d.burstCount;
                        UI.Slider(Row(), "Shots per burst", ref b, 2, WeaponRules.MaxBurst, "0");
                        d.burstCount = Mathf.RoundToInt(b);
                        break;
                    }
                    case 3: UI.Slider(Row(), "Charge time", ref d.chargeTime, 0.2f, 3f, "0.0", "Hold fire this long, then release.", " s"); break;
                }
                UI.Slider(Row(), "Fire rate", ref d.rpm, 30f, WeaponRules.MaxRpm, "0", null, " rpm");
                UI.Selector(Row(), "Bullet type", ref d.projectile, WeaponDesign.Projectiles, "Hitscan hits instantly. Bolts fly visibly. Explosive rounds blow up.");
                if (d.projectile > 0) UI.Slider(Row(), "Bolt speed", ref d.projectileSpeed, 10f, 200f, "0", null, " m/s");
                if (d.projectile == 2) UI.Slider(Row(), "Explosion radius", ref d.explosionRadius, 0.5f, WeaponRules.MaxExplosion, "0.0", null, " m");
                float pellets = d.pellets;
                UI.Slider(Row(), "Pellets per shot", ref pellets, 1, WeaponRules.MaxPellets, "0", "More than 1 makes it a shotgun.");
                d.pellets = Mathf.RoundToInt(pellets);
                UI.Toggle(Row(), "Always show tracers", ref d.alwaysTracers);
                ColorRow(ref d.tracer);
                UI.Label(Row(30), "MAGAZINE", 17, UI.Muted);
                float mag = d.magSize;
                UI.Slider(Row(), "Rounds", ref mag, 1, WeaponRules.MaxMag, "0");
                d.magSize = Mathf.RoundToInt(mag);
                UI.Selector(Row(), "Reload", ref d.reloadStyle, WeaponDesign.ReloadStyles);
                UI.Slider(Row(), d.reloadStyle == 1 ? "Time per round" : "Reload time", ref d.reloadTime, 0.2f, 5f, "0.0", null, " s");
                UI.Label(Row(30), "SOUND", 17, UI.Muted);
                UI.Selector(Row(), "Gunshot", ref d.sound, WeaponDesign.SoundNames);
                UI.Slider(Row(), "Pitch", ref d.pitch, 0.5f, 2f, "0.00");
                if (UI.Button(Row(60), "PLAY SOUND", ButtonKind.Normal, IconFactory.Get("speaker"), 18)) AudioManager.Ui(d.FireSound, 0.8f);
            });
        }

        void DrawStats(Rect body)
        {
            Scroll("we_stats", body, () =>
            {
                if (d.melee)
                {
                    UI.Slider(Row(), "Damage", ref d.meleeDamage, 5f, WeaponRules.MaxDamage, "0");
                    UI.Slider(Row(), "Reach", ref d.meleeReach, 0.8f, 3f, "0.0", null, " m");
                    UI.Slider(Row(), "Weight", ref d.weight, 0.3f, 8f, "0.0", "Heavier weapons slow you down.", " kg");
                    return;
                }
                UI.Slider(Row(), "Damage", ref d.damage, 1f, WeaponRules.MaxDamage, "0");
                UI.Slider(Row(), "Range", ref d.range, 10f, WeaponRules.MaxRange, "0", null, " m");
                UI.Slider(Row(), "Force (knockback)", ref d.force, 0f, WeaponRules.MaxForce, "0");
                UI.Slider(Row(), "Penetration", ref d.penetration, 0f, WeaponRules.MaxPen, "0.0", "How much it goes through bodies and thin walls.");
                UI.Label(Row(30), "ACCURACY", 17, UI.Muted);
                UI.Slider(Row(), "Hip spread", ref d.hipSpread, 0f, 10f, "0.0", null, " deg");
                UI.Slider(Row(), "Aim spread", ref d.aimSpread, 0f, 6f, "0.0", null, " deg");
                UI.Label(Row(30), "RECOIL", 17, UI.Muted);
                UI.Slider(Row(), "Vertical", ref d.recoilV, 0f, 8f, "0.0");
                UI.Slider(Row(), "Horizontal", ref d.recoilH, 0f, 4f, "0.0");
                UI.Slider(Row(), "Kick", ref d.kick, 0f, 3f, "0.0");
                UI.Label(Row(30), "HANDLING", 17, UI.Muted);
                UI.Slider(Row(), "Aim zoom", ref d.aimFov, 0.3f, 1f, "0.00", "Lower zooms in more when aiming.");
                UI.Slider(Row(), "Aim speed", ref d.aimSpeed, 0.4f, 2f, "0.00", null, "x");
                UI.Slider(Row(), "Move speed", ref d.moveSpeed, 0.6f, 1.15f, "0.00", null, "x");
                UI.Slider(Row(), "Weight", ref d.weight, 0.3f, 15f, "0.0", null, " kg");
            });
        }
    }
}
