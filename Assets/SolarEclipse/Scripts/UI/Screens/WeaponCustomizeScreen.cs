using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Weapon customization (touch friendly): pick a gun on the left, attachments per slot on the right, live 3D
    /// preview (preview camera -> RenderTexture, like the Goredoll customizer). Choices save instantly
    /// (Attachments.Set -> PlayerPrefs); guns you are carrying are rebuilt with the new parts when the screen closes.
    /// Opened from the spawn menu GUNS tab and the pause menu.
    /// </summary>
    public sealed class WeaponCustomizeScreen : UIScreen
    {
        string weaponId;
        readonly List<WeaponDef> guns;
        readonly HashSet<string> changed = new HashSet<string>();

        const int PreviewLayer = 31;
        GameObject studio, pivot, model;
        Camera cam;
        RenderTexture rt;
        string builtKey;
        float yawT;
        bool previewFailed;

        public override bool IsModal => true;

        public WeaponCustomizeScreen(string id = null)
        {
            guns = WeaponCatalog.All.FindAll(Attachments.Supports);
            if (id == null || !Attachments.Supports(id))
            {
                var cur = PlayerController.Instance != null && PlayerController.Instance.Inventory != null ? PlayerController.Instance.Inventory.Current : null;
                id = cur != null && Attachments.Supports(cur.Def) ? cur.Def.Id : guns.Count > 0 ? guns[0].Id : "glock";
            }
            weaponId = id;
        }

        public override void OnOpen() => AudioManager.Ui("ui_open");

        public override void OnClose()
        {
            DestroyPreview();
            foreach (var id in changed) Attachments.RefreshLive(id);
            changed.Clear();
        }

        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }

        // ------------------------------------------------------------------ preview

        void EnsureStudio()
        {
            if (studio != null || previewFailed) return;
            try
            {
                studio = new GameObject("WeaponCustomizeStudio");
                studio.transform.position = new Vector3(0, -5600, 0);
                pivot = new GameObject("Pivot");
                pivot.transform.SetParent(studio.transform, false);
                var camGo = new GameObject("PreviewCam");
                camGo.transform.SetParent(studio.transform, false);
                cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.13f, 1f);
                cam.cullingMask = 1 << PreviewLayer;
                cam.fieldOfView = 26f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 20f;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                AddLight(new Vector3(-0.4f, -0.6f, 0.7f), 1.15f, Color.white);
                AddLight(new Vector3(0.6f, -0.2f, -0.7f), 0.55f, new Color(0.75f, 0.8f, 1f));
                var desc = new RenderTextureDescriptor(896, 512, RenderTextureFormat.ARGB32, 24)
                {
                    sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear, msaaSamples = 1,
                };
                rt = new RenderTexture(desc) { name = "WeaponPreview" };
                rt.Create();
                cam.targetTexture = rt;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Weapon preview failed: " + e.Message);
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
            studio = null; pivot = null; model = null; cam = null; builtKey = null;
        }

        void Rebuild()
        {
            if (model != null) Object.Destroy(model);
            model = null;
            var def = WeaponCatalog.Get(weaponId);
            if (def == null) return;
            var wm = WeaponModels.Build(weaponId, PreviewLayer);
            Attachments.BuildVisuals(wm, def, Attachments.Get(weaponId), false);
            model = wm.gameObject;
            IconFactory.SetLayer(model, PreviewLayer);
            // Centre the gun on the pivot and frame it (side view: muzzle to the right).
            model.transform.SetParent(pivot.transform, false);
            pivot.transform.localRotation = Quaternion.identity;
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var bounds = rs[0].bounds;
            foreach (var r in rs) bounds.Encapsulate(r.bounds);
            model.transform.position -= bounds.center - pivot.transform.position;
            float radius = Mathf.Max(0.08f, bounds.extents.magnitude);
            float dist = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.62f;
            cam.transform.localPosition = new Vector3(dist, dist * 0.12f, 0f);
            cam.transform.localRotation = Quaternion.LookRotation(new Vector3(-1f, -0.12f, 0f), Vector3.up);
        }

        public override void Tick()
        {
            EnsureStudio();
            if (studio == null || cam == null) return;
            var key = weaponId + ":" + string.Join(",", Attachments.Get(weaponId));
            if (key != builtKey)
            {
                builtKey = key;
                try { Rebuild(); }
                catch (System.Exception e) { Debug.LogWarning("Weapon preview build failed: " + e.Message); }
            }
            yawT += Time.unscaledDeltaTime;
            if (pivot != null) pivot.transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(yawT * 0.6f) * 28f, Mathf.Sin(yawT * 0.9f) * 2f);
            var oldAmbient = RenderSettings.ambientLight;
            var oldMode = RenderSettings.ambientMode;
            try
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
                cam.Render();
            }
            catch (System.Exception e) { Debug.LogWarning("Weapon preview render failed: " + e.Message); previewFailed = true; DestroyPreview(); }
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
            var r = UI.Centered(Mathf.Min(1680, UI.VW - 30), Mathf.Min(1040, UI.VH - 20));
            UI.Panel(r, "Customize Weapon", a);
            var def = WeaponCatalog.Get(weaponId);
            UI.Label(new Rect(r.x + 420, r.y + 22, r.width - 450, 36), "Saved automatically - applies to your guns when you close this", 17, UI.Accent2, TextAnchor.MiddleRight);

            float top = r.y + 78, bottom = r.yMax - 20;
            // Gun list (big touch rows).
            var list = new Rect(r.x + 20, top, 280, bottom - top);
            DrawList(list);

            // Preview + stats.
            float midW = Mathf.Clamp((r.width - 340) * 0.5f, 380f, 700f);
            var mid = new Rect(list.xMax + 16, top, midW, bottom - top);
            float pvH = Mathf.Min(mid.width * 0.57f, mid.height - 300f);
            var pv = new Rect(mid.x, mid.y, mid.width, pvH);
            UI.Rounded(pv, UI.PanelLight, false);
            if (rt != null && !previewFailed) UI.Tex(new Rect(pv.x + 6, pv.y + 6, pv.width - 12, pv.height - 12), rt, Color.white, ScaleMode.ScaleToFit);
            else if (def != null) UI.Tex(new Rect(pv.x + 20, pv.y + 20, pv.width - 40, pv.height - 40), Hud.WeaponIcon(def), Color.white, ScaleMode.ScaleToFit);
            if (def != null) UI.Label(new Rect(pv.x + 16, pv.y + 8, pv.width - 32, 40), def.Name.ToUpperInvariant(), 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            DrawStats(new Rect(mid.x, pv.yMax + 12, mid.width, mid.yMax - pv.yMax - 12 - 74), def);
            if (UI.Button(new Rect(mid.x, mid.yMax - 64, mid.width * 0.5f - 6, 64), "RESET", ButtonKind.Ghost, IconFactory.Get("refresh"), 22))
            {
                for (int s = 0; s < Attachments.Options.Length; s++) Attachments.Set(weaponId, s, 0);
                changed.Add(weaponId);
                AudioManager.Ui("ui_click");
            }
            if (UI.Button(new Rect(mid.x + mid.width * 0.5f + 6, mid.yMax - 64, mid.width * 0.5f - 6, 64), "DONE", ButtonKind.Primary, IconFactory.Get("check"), 24)) OnBack();

            // Slots.
            var right = new Rect(mid.xMax + 16, top, r.xMax - mid.xMax - 36, bottom - top);
            DrawSlots(right);
        }

        void DrawList(Rect v)
        {
            const float rowH = 84, gap = 8;
            float content = guns.Count * (rowH + gap);
            float s = UI.BeginScroll("wc_guns", v, content);
            for (int i = 0; i < guns.Count; i++)
            {
                var g = guns[i];
                var row = new Rect(0, i * (rowH + gap) - s, v.width - 12, rowH);
                if (row.yMax < 0 || row.y > v.height) continue;
                bool sel = g.Id == weaponId;
                UI.Rounded(row, sel ? new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.4f) : UI.Hover(row) ? new Color(1, 1, 1, 0.12f) : UI.PanelLight, true);
                if (sel) UI.Outline(row, UI.Accent, 2f);
                UI.Tex(new Rect(row.x + 8, row.y + 8, 110, rowH - 16), Hud.WeaponIcon(g), Color.white, ScaleMode.ScaleToFit);
                UI.Label(new Rect(row.x + 124, row.y + 8, row.width - 130, 36), g.Name, 20, Color.white);
                int n = Attachments.CountFitted(g.Id);
                UI.Label(new Rect(row.x + 124, row.y + 44, row.width - 130, 30), n == 0 ? "Stock" : n + " attachment" + (n == 1 ? "" : "s"), 15, n == 0 ? UI.Muted : UI.Accent2);
                if (UI.Hit(row) && !sel) { weaponId = g.Id; AudioManager.Ui("ui_tab"); }
            }
            UI.EndScroll("wc_guns", v, content);
        }

        void DrawSlots(Rect v)
        {
            var sel = Attachments.Get(weaponId);
            int slots = Attachments.Options.Length;
            const float labelH = 30, btnH = 70, gap = 18;
            float content = slots * (labelH + btnH + gap) + 60;
            float s = UI.BeginScroll("wc_slots", v, content);
            float y = -s;
            for (int slot = 0; slot < slots; slot++)
            {
                var opts = Attachments.Options[slot];
                var cur = opts[sel[slot]];
                UI.Label(new Rect(0, y, v.width - 12, labelH), Attachments.SlotNames[slot] + ":  " + cur.Name, 19, Color.white);
                y += labelH + 4;
                float w = (v.width - 12 - (opts.Count - 1) * 8) / opts.Count;
                for (int o = 0; o < opts.Count; o++)
                {
                    var br = new Rect(o * (w + 8), y, w, btnH);
                    bool fits = Attachments.Fits(weaponId, slot, o);
                    bool on = sel[slot] == o;
                    if (UI.Button(br, fits ? opts[o].Name.ToUpperInvariant() : "N/A", on ? ButtonKind.Primary : ButtonKind.Normal, null, w < 150 ? 15 : 18, fits && !on, fits ? opts[o].Description : "Doesn't fit this gun.", on))
                    {
                        Attachments.Set(weaponId, slot, o);
                        changed.Add(weaponId);
                        AudioManager.Ui("ui_click");
                    }
                }
                y += btnH + gap;
            }
            UI.Label(new Rect(0, y, v.width - 12, 50), "Tap an option to fit it. Grey = doesn't fit this gun.", 15, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            UI.EndScroll("wc_slots", v, content);
        }

        void DrawStats(Rect v, WeaponDef def)
        {
            UI.Rounded(v, UI.PanelLight, false);
            if (def == null) return;
            var sel = Attachments.Get(weaponId);
            var m = Attachments.Compute(def, sel);
            var lines = new List<string>();
            int mag = Mathf.Max(1, Mathf.RoundToInt(def.Mag * m.MagMul));
            lines.Add("Magazine: " + mag + (mag != def.Mag ? "  (stock " + def.Mag + ")" : ""));
            lines.Add("Reload: " + (def.ShellReload ? def.ReloadTime.ToString("0.00") + " s per shell" : (def.ReloadTime * m.ReloadMul).ToString("0.00") + " s"));
            lines.Add("Recoil: " + Pct(m.RecoilMul) + "    Hip spread: " + Pct(m.HipSpreadMul));
            lines.Add("Aimed spread: " + Pct(m.AimSpreadMul) + "    Aim speed: " + Pct(m.AimSpeedMul));
            if (m.DamageMul != 1f || m.RangeMul != 1f) lines.Add("Damage: " + Pct(m.DamageMul) + "    Range: " + Pct(m.RangeMul));
            var extra = new List<string>();
            if (m.Suppressed) extra.Add("suppressed");
            if (m.Laser) extra.Add("laser");
            if (m.Optic == 3) extra.Add("4x zoom");
            else if (m.Optic > 0) extra.Add("optic");
            if (extra.Count > 0) lines.Add("+ " + string.Join(", ", extra));
            UI.Label(new Rect(v.x + 16, v.y + 10, v.width - 32, v.height - 20), string.Join("\n", lines), 17, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
        }

        static string Pct(float mul)
        {
            int p = Mathf.RoundToInt((mul - 1f) * 100f);
            return p == 0 ? "stock" : (p > 0 ? "+" : "") + p + "%";
        }
    }
}
