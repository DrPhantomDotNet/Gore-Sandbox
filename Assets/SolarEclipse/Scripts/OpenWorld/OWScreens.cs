using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A small 3D studio that renders you in an Open World outfit (shops + locker).</summary>
    public sealed class OWPreview
    {
        const int Layer = 31;
        GameObject studio, model;
        Camera cam;
        RenderTexture rt;
        string built;
        bool failed;
        public float Yaw = 200f;
        public Texture Texture => rt;

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
            return null;
        }

        public void Render(string outfit)
        {
            if (failed) return;
            try
            {
                if (studio == null)
                {
                    studio = new GameObject("OWStudio");
                    studio.transform.position = new Vector3(0, -5800, 0);
                    var camGo = new GameObject("Cam");
                    camGo.transform.SetParent(studio.transform, false);
                    camGo.transform.localPosition = new Vector3(0, 1.1f, 4.4f);
                    camGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.04f, -1f), Vector3.up);
                    cam = camGo.AddComponent<Camera>();
                    cam.enabled = false;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.08f, 0.07f, 0.12f, 1f);
                    cam.cullingMask = 1 << Layer;
                    cam.fieldOfView = 31f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 30f;
                    cam.allowHDR = false; cam.allowMSAA = false;
                    foreach (var (dir, k, c) in new[] { (new Vector3(-0.5f, -0.7f, -1f), 1.2f, Color.white), (new Vector3(0.7f, -0.2f, 0.6f), 0.5f, new Color(1f, 0.8f, 0.6f)) })
                    {
                        var l = new GameObject("Light").AddComponent<Light>();
                        l.transform.SetParent(studio.transform, false);
                        l.type = LightType.Directional; l.intensity = k; l.color = c; l.cullingMask = 1 << Layer;
                        l.transform.rotation = Quaternion.LookRotation(dir);
                    }
                    rt = new RenderTexture(new RenderTextureDescriptor(448, 640, RenderTextureFormat.ARGB32, 24) { sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear, msaaSamples = 1 }) { name = "OWPreview" };
                    rt.Create();
                    cam.targetTexture = rt;
                }
                if (outfit != built)
                {
                    built = outfit;
                    if (model != null) Object.Destroy(model);
                    var prof = PlayerProfile.Local;
                    var look = (prof != null && prof.BodyLook != null ? prof.BodyLook : PlayerProfile.DefaultLook(PlayerProfile.LocalName)).ToAppearance();
                    OWOutfit.Apply(look, outfit);
                    model = CustomDolls.BuildMannequin(look, 0, false, null, Layer);
                    var t = model.transform;
                    OWOutfit.Attach(Find(t, Anatomy.Parts[PartType.Head].Name), Find(t, Anatomy.Parts[PartType.Chest].Name), Find(t, Anatomy.Parts[PartType.ForearmL].Name), outfit, Layer);
                    IconFactory.SetLayer(model, Layer);
                    model.transform.SetParent(studio.transform, false);
                    model.transform.localPosition = Vector3.zero;
                }
                Yaw += Time.unscaledDeltaTime * 30f;
                model.transform.localRotation = Quaternion.Euler(0, Yaw, 0);
                var amb = RenderSettings.ambientLight; var mode = RenderSettings.ambientMode;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
                cam.Render();
                RenderSettings.ambientMode = mode; RenderSettings.ambientLight = amb;
            }
            catch (System.Exception e) { Debug.LogWarning("[OpenWorld] preview: " + e.Message); failed = true; Destroy(); }
        }

        public void Destroy()
        {
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
            if (studio != null) Object.Destroy(studio);
            studio = null; model = null; cam = null; built = null;
        }

        /// <summary>Outfit code with one slot swapped (to try something on).</summary>
        public static string With(OWItem it)
        {
            var s = OWOutfit.Split(OWSave.OutfitCode);
            var list = new List<string>(s);
            while (list.Count < 7) list.Add("");
            int slot = it.Cat == OWCat.Top ? 0 : it.Cat == OWCat.Pants ? 1 : it.Cat == OWCat.Shoes ? 2 : it.Cat == OWCat.Jewelry ? 3 : it.Cat == OWCat.Glasses ? 4 : it.Cat == OWCat.Mask ? 5 : it.Cat == OWCat.Hat ? 6 : -1;
            if (slot >= 0) list[slot] = it.Id;
            return string.Join(";", list);
        }

        public static bool Wearable(OWCat c) => c == OWCat.Top || c == OWCat.Pants || c == OWCat.Shoes || c == OWCat.Jewelry || c == OWCat.Glasses || c == OWCat.Mask || c == OWCat.Hat;
    }

    // ====================================================================== shops

    /// <summary>A shop: tabs per category, a grid of items with BUY / EQUIP / WEAR, and a try-on preview for clothes.</summary>
    public sealed class ShopScreen : UIScreen
    {
        readonly string title;
        readonly OWCat[] cats;
        int tab;
        OWItem hovered, selected;
        readonly OWPreview preview = new OWPreview();

        public ShopScreen(string title, params OWCat[] cats) { this.title = title; this.cats = cats; }
        public override bool IsModal => true;

        public override void OnOpen() { AudioManager.Ui("ui_open"); }
        public override void OnClose() => preview.Destroy();
        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }

        OWCat Cat => cats[Mathf.Clamp(tab, 0, cats.Length - 1)];
        bool ShowPreview => OWPreview.Wearable(Cat);

        public override void Tick()
        {
            if (!ShowPreview) return;
            var it = hovered ?? selected;
            preview.Render(it != null && OWPreview.Wearable(it.Cat) ? OWPreview.With(it) : OWSave.OutfitCode);
        }

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(Mathf.Min(1600, UI.VW - 60), Mathf.Min(940, UI.VH - 40));
            UI.Panel(r, title, a);
            UI.Label(new Rect(r.xMax - 420, r.y + 18, 390, 44), "$" + OWSave.Cash.ToString("N0"), 30, new Color(0.45f, 1f, 0.5f), TextAnchor.MiddleRight, FontStyle.Normal, true, false, UI.HeavyFont);
            if (cats.Length > 1)
            {
                var names = new string[cats.Length];
                for (int i = 0; i < cats.Length; i++) names[i] = OWItems.CatNames[(int)cats[i]].ToUpperInvariant();
                int nt = UI.Tabs(new Rect(r.x + 24, r.y + 80, r.width - 48, 50), tab, names, 19);
                if (nt != tab) { tab = nt; hovered = selected = null; AudioManager.Ui("ui_tab"); }
            }
            float top = r.y + (cats.Length > 1 ? 146 : 86);
            var body = new Rect(r.x + 24, top, r.width - 48, r.yMax - top - 96);
            if (ShowPreview)
            {
                var pr = new Rect(body.x, body.y, 330, body.height);
                UI.Rounded(pr, UI.PanelLight);
                if (preview.Texture != null) UI.Tex(new Rect(pr.x + 10, pr.y + 10, pr.width - 20, pr.height - 60), preview.Texture, Color.white, ScaleMode.ScaleToFit);
                UI.Label(new Rect(pr.x, pr.yMax - 46, pr.width, 40), (hovered ?? selected) != null ? "Trying on: " + (hovered ?? selected).Name : "You", 16, UI.Muted, TextAnchor.MiddleCenter);
                body = new Rect(body.x + 350, body.y, body.width - 350, body.height);
            }
            DrawGrid(body);

            if (UI.Button(new Rect(r.x + 24, r.yMax - 78, 220, 56), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
            string hint = Cat == OWCat.WeaponSkin ? "Skins go on the gun you're holding (change them any time in your penthouse locker)." :
                          Cat == OWCat.Gun ? "Hold G (or tap GUNS) to switch between the guns you own." :
                          Cat == OWCat.CarUpgrade ? "Paint and neon go on the car you call. Nitro, armour and the air horn work on every car you call." :
                          Cat == OWCat.Car ? "Call your car from the phone (GARAGE) or the garage in your penthouse." :
                          Cat == OWCat.Apartment ? "Buy the penthouse first. Furniture shows up inside it." :
                          ShowPreview ? "Hover or tap an item to try it on." : "";
            UI.Label(new Rect(r.x + 264, r.yMax - 78, r.width - 600, 56), hint, 15, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
            if (Cat == OWCat.Apartment && OWSave.Owns("a_apartment") && UI.Button(new Rect(r.xMax - 324, r.yMax - 78, 300, 56), "ENTER PENTHOUSE", ButtonKind.Primary, IconFactory.Get("home"), 19))
            { UIRoot.Pop(); OWDirector.EnterApartment(); }
        }

        void DrawGrid(Rect body)
        {
            var items = OWItems.In(Cat);
            bool touch = Settings.TouchControlsActive;
            float cw = touch ? 300f : 280f, ch = touch ? 230f : 210f, g = 14f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((body.width - 16 + g) / (cw + g)));
            cw = (body.width - 16 - (cols - 1) * g) / cols;
            float content = Mathf.CeilToInt(items.Count / (float)cols) * (ch + g);
            float s = UI.BeginScroll("owshop_" + title + tab, body, content);
            hovered = null;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var cr = new Rect((i % cols) * (cw + g), (i / cols) * (ch + g) - s, cw, ch);
                if (cr.yMax < 0 || cr.y > body.height) continue;
                bool own = OWSave.Owns(it.Id);
                bool hover = UI.Hover(cr);
                if (hover && OWPreview.Wearable(it.Cat)) hovered = it;
                UI.Rounded(cr, hover ? new Color(1, 1, 1, 0.1f) : UI.PanelLight, true);
                if (selected == it) UI.Outline(cr, UI.Accent2, 2f);
                // Picture: weapon icon, or colour swatches.
                var pic = new Rect(cr.x + 12, cr.y + 12, cw - 24, 70);
                if (it.Cat == OWCat.Gun)
                {
                    var def = WeaponCatalog.Get(it.Weapon);
                    var icon = def != null ? Hud.WeaponIcon(def) : null;
                    if (icon != null) UI.Tex(pic, icon, Color.white, ScaleMode.ScaleToFit);
                }
                else
                {
                    UI.Rounded(new Rect(pic.x, pic.y, pic.width * 0.6f, pic.height), it.Color, true);
                    UI.Rounded(new Rect(pic.x + pic.width * 0.62f, pic.y, pic.width * 0.38f, pic.height), it.Color2, true);
                    UI.Tex(new Rect(pic.center.x - 22, pic.center.y - 22, 44, 44), IconFactory.Get(CatIcon(it.Cat)), new Color(1, 1, 1, 0.75f));
                }
                UI.Label(new Rect(cr.x + 12, cr.y + 86, cw - 24, 30), it.Name, 19, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(cr.x + 12, cr.y + 114, cw - 24, 44), string.IsNullOrEmpty(it.Desc) ? CatDesc(it) : it.Desc, 13, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                var br = new Rect(cr.x + 12, cr.yMax - 58, cw - 24, 48);
                if (!own)
                {
                    bool needsFlat = it.Cat == OWCat.Apartment && it.Variant > 0 && !OWSave.Owns("a_apartment");
                    if (UI.Button(br, needsFlat ? "NEEDS PENTHOUSE" : "BUY  $" + it.Price.ToString("N0"), ButtonKind.Primary, null, 17, !needsFlat && OWSave.Cash >= it.Price))
                    {
                        selected = it;
                        if (OWSave.Buy(it)) OnBought(it);
                    }
                }
                else DrawOwned(br, it);
                if (UI.Hit(new Rect(cr.x, cr.y, cr.width, cr.height - 64))) selected = it;
            }
            UI.EndScroll("owshop_" + title + tab, body, content);
        }

        static string CatIcon(OWCat c)
        {
            switch (c)
            {
                case OWCat.Car: return "car";
                case OWCat.CarUpgrade: return "wrench";
                case OWCat.Jewelry: return "diamond";
                case OWCat.Glasses: return "eye";
                case OWCat.Mask: return "clown";
                case OWCat.Hat: return "crown";
                case OWCat.WeaponSkin: return "gun";
                case OWCat.Apartment: return "home";
                case OWCat.Emote: return "hand";
                default: return "person";
            }
        }

        static string CatDesc(OWItem it)
        {
            switch (it.Cat)
            {
                case OWCat.Top: return it.LongSleeves ? "Long sleeves." : "Short sleeves.";
                case OWCat.Pants: return "Legwear.";
                case OWCat.Shoes: return "Footwear.";
                case OWCat.Glasses: return "Eyewear.";
                case OWCat.Mask: return "Hides your face. Perfect for heists.";
                case OWCat.Hat: return "Headwear.";
                case OWCat.WeaponSkin: return it.Variant == 2 ? "Glowing finish." : it.Variant == 1 ? "Two-tone finish." : "Solid finish.";
                case OWCat.Emote: return "Play it from your phone or with J.";
            }
            return "";
        }

        static void OnBought(OWItem it)
        {
            switch (it.Cat)
            {
                case OWCat.Gun: OWSave.Equip(OWCat.Gun, it.Id); OWDirector.GiveGun(it.Id, true); break;
                case OWCat.Car: if (string.IsNullOrEmpty(OWSave.D.car)) OWSave.Equip(OWCat.Car, it.Id); break;
                case OWCat.CarUpgrade: if (it.Variant <= 1) OWSave.Equip(OWCat.CarUpgrade, it.Id); break;
                case OWCat.WeaponSkin: ApplySkinToHeld(it); break;
                case OWCat.Emote: OWSave.Equip(OWCat.Emote, it.Id); break;
                case OWCat.Apartment: OWApartment.Refresh(); break;
                default: if (OWPreview.Wearable(it.Cat)) OWSave.Equip(it.Cat, it.Id); break;
            }
        }

        static void ApplySkinToHeld(OWItem skin)
        {
            var gun = OWItems.Get(OWSave.D.gun);
            if (gun == null) return;
            OWSave.SetSkin(gun.Weapon, skin.Id);
            OWDirector.GiveGun(gun.Id, true);
            GameEvents.ShowToast(skin.Name + " skin on your " + gun.Name + ".");
        }

        static void DrawOwned(Rect br, OWItem it)
        {
            var d = OWSave.D;
            switch (it.Cat)
            {
                case OWCat.Gun:
                    if (UI.Button(br, d.gun == it.Id ? "IN HAND" : "EQUIP", d.gun == it.Id ? ButtonKind.Ghost : ButtonKind.Normal, null, 17, d.gun != it.Id))
                    { OWSave.Equip(OWCat.Gun, it.Id); OWDirector.GiveGun(it.Id, true); }
                    break;
                case OWCat.Car:
                    if (UI.Button(br, d.car == it.Id ? "YOUR CAR" : "SELECT", d.car == it.Id ? ButtonKind.Ghost : ButtonKind.Normal, null, 17, d.car != it.Id)) OWSave.Equip(OWCat.Car, it.Id);
                    break;
                case OWCat.CarUpgrade:
                    if (it.Variant <= 1)
                    {
                        bool on = d.paint == it.Id || d.neon == it.Id;
                        if (UI.Button(br, on ? "REMOVE" : "APPLY", on ? ButtonKind.Ghost : ButtonKind.Normal, null, 17))
                        {
                            if (on) { if (d.paint == it.Id) d.paint = ""; if (d.neon == it.Id) d.neon = ""; OWSave.Save(); }
                            else OWSave.Equip(OWCat.CarUpgrade, it.Id);
                        }
                    }
                    else UI.Button(br, "INSTALLED", ButtonKind.Ghost, null, 17, false);
                    break;
                case OWCat.WeaponSkin:
                {
                    var gun = OWItems.Get(d.gun);
                    bool on = gun != null && OWSave.SkinFor(gun.Weapon) == it.Id;
                    if (UI.Button(br, on ? "ON YOUR GUN" : "APPLY TO GUN", on ? ButtonKind.Ghost : ButtonKind.Normal, null, 16, !on)) ApplySkinToHeld(it);
                    break;
                }
                case OWCat.Emote:
                    if (UI.Button(br, "PLAY", ButtonKind.Normal, null, 17)) { UIRoot.Pop(); OWDirector.PlayEmote(it.Id); }
                    break;
                case OWCat.Apartment: UI.Button(br, "OWNED", ButtonKind.Ghost, null, 17, false); break;
                default:
                {
                    bool worn = OWSave.Equipped(it.Cat) == it.Id;
                    if (UI.Button(br, worn ? "TAKE OFF" : "WEAR", worn ? ButtonKind.Ghost : ButtonKind.Normal, null, 17)) OWSave.Equip(it.Cat, worn ? "" : it.Id);
                    break;
                }
            }
        }
    }

    // ====================================================================== locker

    /// <summary>Penthouse locker: wear anything you own, pick skins for each gun.</summary>
    public sealed class LockerScreen : UIScreen
    {
        static readonly OWCat[] Cats = { OWCat.Top, OWCat.Pants, OWCat.Shoes, OWCat.Jewelry, OWCat.Glasses, OWCat.Mask, OWCat.Hat, OWCat.WeaponSkin };
        int tab;
        readonly OWPreview preview = new OWPreview();
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");
        public override void OnClose() => preview.Destroy();
        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }
        public override void Tick() => preview.Render(OWSave.OutfitCode);

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(Mathf.Min(1500, UI.VW - 60), Mathf.Min(920, UI.VH - 40));
            UI.Panel(r, "Locker", a);
            var names = new string[Cats.Length];
            for (int i = 0; i < Cats.Length; i++) names[i] = Cats[i] == OWCat.WeaponSkin ? "GUN SKINS" : OWItems.CatNames[(int)Cats[i]].ToUpperInvariant();
            int nt = UI.Tabs(new Rect(r.x + 24, r.y + 80, r.width - 48, 50), tab, names, 17);
            if (nt != tab) { tab = nt; AudioManager.Ui("ui_tab"); }
            var body = new Rect(r.x + 24, r.y + 146, r.width - 48, r.height - 240);
            var pr = new Rect(body.x, body.y, 340, body.height);
            UI.Rounded(pr, UI.PanelLight);
            if (preview.Texture != null) UI.Tex(new Rect(pr.x + 10, pr.y + 10, pr.width - 20, pr.height - 20), preview.Texture, Color.white, ScaleMode.ScaleToFit);
            var list = new Rect(body.x + 360, body.y, body.width - 360, body.height);
            if (Cats[tab] == OWCat.WeaponSkin) DrawSkins(list); else DrawWear(list, Cats[tab]);
            if (UI.Button(new Rect(r.x + 24, r.yMax - 78, 220, 56), "DONE", ButtonKind.Primary, IconFactory.Get("check"))) OnBack();
            UI.Label(new Rect(r.x + 264, r.yMax - 78, r.width - 300, 56), "Other players see your outfit in multiplayer. Buy more at THREADS, MASK MART and the ECLIPSE BOUTIQUE (Downtown / Uptown).", 15, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
        }

        void DrawWear(Rect list, OWCat cat)
        {
            var owned = OWItems.In(cat).FindAll(i => OWSave.Owns(i.Id));
            string cur = OWSave.Equipped(cat);
            float rowH = 64f;
            float content = (owned.Count + 1) * (rowH + 8);
            float s = UI.BeginScroll("owlocker" + tab, list, content);
            for (int i = -1; i < owned.Count; i++)
            {
                var rr = new Rect(0, (i + 1) * (rowH + 8) - s, list.width - 16, rowH);
                if (rr.yMax < 0 || rr.y > list.height) continue;
                var it = i >= 0 ? owned[i] : null;
                bool on = it == null ? string.IsNullOrEmpty(cur) : cur == it.Id;
                UI.Rounded(rr, on ? new Color(UI.Accent2.r, UI.Accent2.g, UI.Accent2.b, 0.35f) : UI.Hover(rr) ? new Color(1, 1, 1, 0.1f) : UI.PanelLight, true);
                if (it != null) UI.Rounded(new Rect(rr.x + 10, rr.y + 10, 44, 44), it.Color, true);
                UI.Label(new Rect(rr.x + 70, rr.y, rr.width - 200, rowH), it == null ? "Nothing" : it.Name, 19, Color.white, TextAnchor.MiddleLeft, on ? FontStyle.Bold : FontStyle.Normal);
                if (on) UI.Label(new Rect(rr.xMax - 140, rr.y, 120, rowH), "WEARING", 15, UI.Accent2, TextAnchor.MiddleRight, FontStyle.Bold);
                if (UI.Hit(rr) && !on) { OWSave.Equip(cat, it == null ? "" : it.Id); AudioManager.Ui("cloth"); }
            }
            UI.EndScroll("owlocker" + tab, list, content);
            if (owned.Count == 0) UI.Label(new Rect(list.x, list.y + 80, list.width, 60), "You don't own any " + OWItems.CatNames[(int)cat].ToLowerInvariant() + " yet.", 18, UI.Muted);
        }

        void DrawSkins(Rect list)
        {
            var guns = OWItems.In(OWCat.Gun).FindAll(g => OWSave.Owns(g.Id));
            var skins = OWItems.In(OWCat.WeaponSkin).FindAll(sk => OWSave.Owns(sk.Id));
            var names = new List<string> { "Default" };
            foreach (var sk in skins) names.Add(sk.Name);
            var opts = names.ToArray();
            float rowH = 66f, content = guns.Count * (rowH + 8);
            float s = UI.BeginScroll("owskins", list, content);
            for (int i = 0; i < guns.Count; i++)
            {
                var g = guns[i];
                var rr = new Rect(0, i * (rowH + 8) - s, list.width - 16, rowH);
                if (rr.yMax < 0 || rr.y > list.height) continue;
                string cur = OWSave.SkinFor(g.Weapon);
                int idx = Mathf.Max(0, skins.FindIndex(x => x.Id == cur) + 1);
                int before = idx;
                UI.Selector(rr, g.Name, ref idx, opts);
                if (idx != before)
                {
                    OWSave.SetSkin(g.Weapon, idx == 0 ? "" : skins[idx - 1].Id);
                    if (OWSave.D.gun == g.Id) OWDirector.GiveGun(g.Id, true);
                }
            }
            UI.EndScroll("owskins", list, content);
            if (skins.Count == 0) UI.Label(new Rect(list.x, list.y + guns.Count * 74 + 20, list.width, 60), "No skins yet: Bullet Bros. sells them (Downtown).", 18, UI.Muted);
        }
    }

    // ====================================================================== garage

    public sealed class GarageScreen : UIScreen
    {
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");
        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(Mathf.Min(1200, UI.VW - 60), Mathf.Min(880, UI.VH - 40));
            UI.Panel(r, "Garage", a);
            DrawBody(new Rect(r.x + 24, r.y + 86, r.width - 48, r.height - 180), false);
            if (UI.Button(new Rect(r.x + 24, r.yMax - 78, 220, 56), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
        }

        /// <summary>Your cars + CALL CAR (also the phone's GARAGE app).</summary>
        public static void DrawBody(Rect body, bool phone)
        {
            var d = OWSave.D;
            var cars = OWItems.In(OWCat.Car).FindAll(c => OWSave.Owns(c.Id));
            if (cars.Count == 0)
            {
                UI.Label(new Rect(body.x, body.y, body.width, 120), "You don't own a car yet. ECLIPSE MOTORS in Midtown sells 12 of them, from a $4,000 hatchback to a supercar.", 18, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                if (UI.Button(new Rect(body.x, body.y + 130, Mathf.Min(body.width, 360), 60), "PIN ECLIPSE MOTORS", ButtonKind.Normal, IconFactory.Get("pin_map"), 17)) { OWDirector.Waypoint = OWMap.Place("ECLIPSE MOTORS"); GameEvents.ShowToast("Pinned on your map."); }
                return;
            }
            float rowH = phone ? 92f : 96f;
            var list = new Rect(body.x, body.y, body.width, body.height - 90);
            float content = cars.Count * (rowH + 8);
            float s = UI.BeginScroll("owgarage" + phone, list, content);
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                var rr = new Rect(0, i * (rowH + 8) - s, list.width - 16, rowH);
                if (rr.yMax < 0 || rr.y > list.height) continue;
                bool sel = d.car == c.Id;
                UI.Rounded(rr, sel ? new Color(UI.Accent2.r, UI.Accent2.g, UI.Accent2.b, 0.3f) : UI.PanelLight, true);
                UI.Rounded(new Rect(rr.x + 10, rr.y + 12, 18, rr.height - 24), c.Color, true);
                UI.Label(new Rect(rr.x + 40, rr.y + 4, rr.width - 60, 34), c.Name, 20, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                Bar(new Rect(rr.x + 40, rr.y + 44, (rr.width - 70) / 4f - 10, 40), "Speed", c.Speed / 1.6f);
                Bar(new Rect(rr.x + 40 + (rr.width - 70) / 4f, rr.y + 44, (rr.width - 70) / 4f - 10, 40), "Accel", c.Accel / 1.6f);
                Bar(new Rect(rr.x + 40 + (rr.width - 70) / 2f, rr.y + 44, (rr.width - 70) / 4f - 10, 40), "Grip", c.Grip / 1.4f);
                Bar(new Rect(rr.x + 40 + (rr.width - 70) * 0.75f, rr.y + 44, (rr.width - 70) / 4f - 10, 40), "Armour", c.Health / 2f);
                if (UI.Hit(rr) && !sel) { OWSave.Equip(OWCat.Car, c.Id); AudioManager.Ui("ui_click"); }
            }
            UI.EndScroll("owgarage" + phone, list, content);
            var up = new List<string>();
            if (!string.IsNullOrEmpty(d.paint)) up.Add(OWItems.Get(d.paint)?.Name);
            if (!string.IsNullOrEmpty(d.neon)) up.Add(OWItems.Get(d.neon)?.Name);
            if (OWSave.HasUpgrade(2)) up.Add("Nitro"); if (OWSave.HasUpgrade(3)) up.Add("Armour"); if (OWSave.HasUpgrade(4)) up.Add("Air horn");
            UI.Label(new Rect(body.x, body.yMax - 84, body.width, 24), up.Count > 0 ? "Upgrades: " + string.Join(", ", up) : "No upgrades yet (ECLIPSE CUSTOMS).", 14, UI.Muted);
            if (UI.Button(new Rect(body.x, body.yMax - 58, body.width, 58), "CALL MY " + (OWItems.Get(d.car)?.Name ?? "CAR").ToUpperInvariant(), ButtonKind.Primary, IconFactory.Get("car"), 19, !string.IsNullOrEmpty(d.car) && OWCar.LocalDriving == null))
            {
                OWDirector.CallMyCar();
                UIRoot.Remove(UIRoot.Top);
            }
        }

        static void Bar(Rect r, string label, float f)
        {
            UI.Label(new Rect(r.x, r.y, r.width, 20), label, 13, UI.Muted);
            UI.Fill(new Rect(r.x, r.y + 24, r.width, 6), new Color(1, 1, 1, 0.12f));
            UI.Fill(new Rect(r.x, r.y + 24, r.width * Mathf.Clamp01(f), 6), UI.Accent2);
        }
    }

    // ====================================================================== heist board

    public sealed class HeistBoardScreen : UIScreen
    {
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open");
        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(Mathf.Min(1300, UI.VW - 60), Mathf.Min(920, UI.VH - 40));
            UI.Panel(r, "Heist Board", a);
            UI.Label(new Rect(r.x + 300, r.y + 20, r.width - 330, 40), "\"You want in? Bring a crew.\" - Big Marcus", 17, UI.Muted, TextAnchor.MiddleRight, FontStyle.Italic);
            DrawList(new Rect(r.x + 24, r.y + 86, r.width - 48, r.height - 180));
            if (UI.Button(new Rect(r.x + 24, r.yMax - 78, 220, 56), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
            UI.Label(new Rect(r.x + 264, r.yMax - 78, r.width - 300, 56),
                NetState.Active ? "Multiplayer: a heist starts when at least 2 players are in the crew and everyone pressed READY. Payouts are split, +10% for each extra member."
                                : "Singleplayer: you can go solo for 60% of the payout. Host an Open World server to do heists with friends.",
                15, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
        }

        public static void DrawList(Rect body)
        {
            byte me = OWDirector.MyId;
            float rowH = 150f, content = OWHeists.Defs.Length * (rowH + 10);
            float s = UI.BeginScroll("owheists", body, content);
            for (int i = 0; i < OWHeists.Defs.Length; i++)
            {
                var d = OWHeists.Defs[i];
                var rr = new Rect(0, i * (rowH + 10) - s, body.width - 16, rowH);
                if (rr.yMax < 0 || rr.y > body.height) continue;
                bool thisOne = OWHeists.Index == i && OWHeists.Phase != OWHeists.Idle;
                bool busy = OWHeists.Phase != OWHeists.Idle && !thisOne;
                bool inCrew = thisOne && OWHeists.InCrew(me);
                UI.Rounded(rr, inCrew ? new Color(0.35f, 0.12f, 0.08f, 0.9f) : UI.PanelLight, true);
                UI.Label(new Rect(rr.x + 16, rr.y + 8, rr.width - 260, 34), d.Name, 22, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(rr.xMax - 250, rr.y + 8, 234, 34), "$" + d.Payout.ToString("N0"), 22, new Color(0.45f, 1f, 0.5f), TextAnchor.MiddleRight, FontStyle.Normal, true, false, UI.HeavyFont);
                UI.Label(new Rect(rr.x + 16, rr.y + 42, rr.width - 260, 52), d.Desc, 14, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                for (int k = 0; k < 5; k++) UI.Tex(new Rect(rr.xMax - 140 + k * 24, rr.y + 50, 20, 20), IconFactory.Get("star"), k < d.Stars ? UI.Warn : new Color(1, 1, 1, 0.15f));
                UI.Label(new Rect(rr.xMax - 250, rr.y + 48, 100, 24), "Heat", 13, UI.Muted, TextAnchor.MiddleRight);
                // Crew line + buttons
                string crew = thisOne ? "Crew: " + CrewText() : busy ? "Another crew is busy with " + OWHeists.Current.Name + "." : "No crew yet.";
                UI.Label(new Rect(rr.x + 16, rr.yMax - 48, rr.width - 420, 40), crew, 15, thisOne ? Color.white : UI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
                var b1 = new Rect(rr.xMax - 396, rr.yMax - 58, 186, 48);
                var b2 = new Rect(rr.xMax - 200, rr.yMax - 58, 186, 48);
                if (!inCrew)
                {
                    if (UI.Button(b2, "JOIN", ButtonKind.Primary, IconFactory.Get("users"), 17, !busy && (!thisOne || OWHeists.Phase == OWHeists.Lobby)))
                        NetManager.I.OWRequest(OWReq.HeistJoin, "", i);
                }
                else
                {
                    if (OWHeists.Phase == OWHeists.Lobby)
                    {
                        bool ready = OWHeists.Ready.Contains(me);
                        if (UI.Button(b2, ready ? "NOT READY" : "READY", ready ? ButtonKind.Normal : ButtonKind.Primary, IconFactory.Get("check"), 17))
                            NetManager.I.OWRequest(OWReq.HeistReady, "", ready ? 0 : 1);
                    }
                    else UI.Label(b2, OWHeists.Phase == OWHeists.Travel ? "GO!" : OWHeists.Phase == OWHeists.Hit ? "IN PROGRESS" : "ESCAPE!", 17, UI.Warn, TextAnchor.MiddleCenter, FontStyle.Bold);
                    if (UI.Button(b1, "LEAVE", ButtonKind.Danger, null, 16)) NetManager.I.OWRequest(OWReq.HeistLeave);
                }
            }
            UI.EndScroll("owheists", body, content);
        }

        static string CrewText()
        {
            var parts = new List<string>();
            foreach (var id in OWHeists.Crew) parts.Add((id == OWDirector.MyId ? "You" : OWPhone.NameOf(id)) + (OWHeists.Ready.Contains(id) ? " (ready)" : ""));
            string t = string.Join(", ", parts);
            if (OWHeists.Phase == OWHeists.Lobby && OWHeists.Crew.Count < OWHeists.MinCrew) t += "  -  waiting for " + (OWHeists.MinCrew - OWHeists.Crew.Count) + " more";
            return t;
        }
    }

    // ====================================================================== gun menu (hold G)

    /// <summary>Your guns. PC: hold G, point at a gun, let go. Phones: tap GUNS, tap a gun.</summary>
    public sealed class GunMenuScreen : UIScreen
    {
        readonly bool hold;
        OWItem hover;
        public GunMenuScreen(bool holdMode) { hold = holdMode; }
        public override bool IsModal => true;
        public override void OnOpen() => AudioManager.Ui("ui_open", 0.5f);
        public override bool OnBack() { UIRoot.Remove(this); return true; }

        public override void Tick()
        {
            if (hold && !Input.GetKey(KeyCode.G))
            {
                if (hover != null) Pick(hover);
                UIRoot.Remove(this);
            }
        }

        void Pick(OWItem it)
        {
            if (it.Id != OWSave.D.gun) { OWSave.Equip(OWCat.Gun, it.Id); OWDirector.GiveGun(it.Id, true); }
            else { var p = PlayerController.Instance; if (p != null && p.Inventory.Slots[1] != null) p.Inventory.Select(1); }
        }

        public override void Draw()
        {
            float a = Intro(0, 0.15f);
            UI.Dim(0.35f * a);
            var guns = OWItems.In(OWCat.Gun).FindAll(g => OWSave.Owns(g.Id));
            int cols = Mathf.Min(6, Mathf.Max(3, guns.Count));
            float cw = Settings.TouchControlsActive ? 230f : 200f, ch = cw * 0.72f, g = 14f;
            int rows = Mathf.CeilToInt(guns.Count / (float)cols);
            var r = UI.Centered(cols * (cw + g) - g + 48, rows * (ch + g) - g + 150);
            UI.Rounded(r, new Color(0.05f, 0.05f, 0.07f, 0.88f * a));
            UI.Label(new Rect(r.x, r.y + 12, r.width, 40), "YOUR GUNS", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal, true, false, UI.HeavyFont);
            hover = null;
            for (int i = 0; i < guns.Count; i++)
            {
                var it = guns[i];
                var cr = new Rect(r.x + 24 + (i % cols) * (cw + g), r.y + 64 + (i / cols) * (ch + g), cw, ch);
                bool h = UI.Hover(cr);
                if (h) hover = it;
                bool cur = OWSave.D.gun == it.Id;
                UI.Rounded(cr, h ? new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.8f) : cur ? new Color(1, 1, 1, 0.16f) : new Color(1, 1, 1, 0.06f), true);
                var def = WeaponCatalog.Get(it.Weapon);
                var icon = def != null ? Hud.WeaponIcon(def) : null;
                if (icon != null) UI.Tex(new Rect(cr.x + 12, cr.y + 10, cr.width - 24, cr.height - 50), icon, Color.white, ScaleMode.ScaleToFit);
                UI.Label(new Rect(cr.x, cr.yMax - 38, cr.width, 30), it.Name + (it.Starter ? "  (INF)" : ""), 16, Color.white, TextAnchor.MiddleCenter, cur ? FontStyle.Bold : FontStyle.Normal);
                if (UI.Hit(cr)) { Pick(it); UIRoot.Remove(this); return; }
            }
            UI.Label(new Rect(r.x, r.yMax - 46, r.width, 30), hold ? "Point at a gun and let go of G.  More guns at BULLET BROS. (Downtown)." : "Tap a gun.  More guns at BULLET BROS. (Downtown).", 15, UI.Muted, TextAnchor.MiddleCenter);
            if (!hold && UI.Button(new Rect(r.xMax - 64, r.y + 10, 50, 50), "", ButtonKind.Ghost, IconFactory.Get("cross"))) UIRoot.Remove(this);
        }
    }
}
