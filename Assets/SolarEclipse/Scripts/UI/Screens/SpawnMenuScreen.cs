using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Spawn menu (Esc while holding the Spawn Gun). Tabs: AI (Goredoll only), Items (empty, wired for later),
    /// Props, Guns (Equip buttons), Entities (nextbots) and Options (noclip, freeze-on-spawn, undo, cleanup).
    /// Selecting a card arms the Spawn Gun; middle-click in the world spawns it.
    /// </summary>
    public sealed class SpawnMenuScreen : UIScreen
    {
        static int tab;
        static readonly string[] TabNames = { "AI", "ITEMS", "PROPS", "GUNS", "ENTITIES", "OPTIONS" };
        static readonly string[] Looks = { "Random", "Classic" };
        static int lookIndex = 0;
        string search = "";
        readonly Queue<SpawnableDef> toRender = new Queue<SpawnableDef>();
        Texture2D portraitRandom, portraitClassic;

        public override bool PausesTime => false;
        public override bool IsModal => true;

        public override void OnOpen()
        {
            AudioManager.Ui("ui_open");
            foreach (var d in SpawnCatalog.Props) if (!IconFactory.Has("prop_" + d.Id)) toRender.Enqueue(d);
        }

        public override bool OnBack() { AudioManager.Ui("ui_close"); UIRoot.Pop(); return true; }

        public override void Tick()
        {
            // Photograph one prop per frame for the cards.
            if (toRender.Count > 0)
            {
                var d = toRender.Dequeue();
                try
                {
                    var go = d.Spawn(new Vector3(0, -4000, 0), Quaternion.Euler(0, 30, 0));
                    foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
                    foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
                    foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
                    go.transform.SetParent(null, true);
                    IconFactory.Render("prop_" + d.Id, go, new Vector3(-0.6f, -0.5f, 0.7f), 160, 1.1f, d.Glyph);
                }
                catch (System.Exception e) { Debug.LogWarning("Icon for " + d.Id + " failed: " + e.Message); IconFactory.Register("prop_" + d.Id, IconFactory.Get(d.Glyph)); }
            }
            if (portraitRandom == null && tab == 0) portraitClassic = IconFactory.Has("portrait_classic") ? IconFactory.Get("portrait_classic") : GoredollFactory.Portrait("portrait_classic", 0);
            if (portraitRandom == null && tab == 0) portraitRandom = IconFactory.Has("portrait_random") ? IconFactory.Get("portrait_random") : GoredollFactory.Portrait("portrait_random", -1);
            // Zombie portraits (one per frame).
            if (tab == 0)
                foreach (var k in Zombies.Kinds)
                {
                    var zn = "prop_zombie_" + k.ToString().ToLowerInvariant();
                    if (IconFactory.Has(zn)) continue;
                    try { Zombies.Portrait(k); }
                    catch (System.Exception e) { Debug.LogWarning("Zombie portrait failed: " + e.Message); }
                    if (!IconFactory.Has(zn)) IconFactory.Register(zn, IconFactory.Get("skull"));
                    break;
                }
            if (tab == 0) NpcMods.TickIcons(); // armed + custom Goredoll portraits
        }

        public override void Draw()
        {
            float a = Intro(0, 0.2f);
            UI.Dim(0.45f * a);
            var r = UI.Centered(Mathf.Min(1400, UI.VW - 80), Mathf.Min(900, UI.VH - 80));
            UI.Panel(r, "Spawn Menu", a);
            var sel = SpawnCatalog.Selected;
            UI.Label(new Rect(r.x + 300, r.y + 22, r.width - 330, 36), sel != null ? "Selected: " + sel.Name + "   ·   Middle-click to spawn" : "Nothing selected", 18, UI.Accent2, TextAnchor.MiddleRight);
            int newTab = UI.Tabs(new Rect(r.x + 24, r.y + 76, r.width - 48, 50), tab, TabNames, 19);
            if (newTab != tab) { tab = newTab; AudioManager.Ui("ui_tab"); }
            var view = new Rect(r.x + 24, r.y + 142, r.width - 48, r.height - 230);

            switch (tab)
            {
                case 0: DrawAi(view); break;
                case 1: DrawGrid(view, SpawnCatalog.Items, "items", "No items yet — this tab is ready for them. (Coming in a future update.)"); break;
                case 2:
                {
                    search = UI.TextField(new Rect(view.x, view.y, 360, 44), search, "propsearch", false, "Search props...");
                    var list = SpawnCatalog.Props.FindAll(d => string.IsNullOrEmpty(search) || d.Name.ToLowerInvariant().Contains(search.ToLowerInvariant()));
                    DrawGrid(new Rect(view.x, view.y + 56, view.width, view.height - 56), list, "props", "No props match \"" + search + "\".");
                    break;
                }
                case 3: DrawGuns(view); break;
                case 4: DrawGrid(view, SpawnCatalog.Entities, "entities", "No entities."); break;
                case 5: DrawOptions(view); break;
            }

            if (UI.Button(new Rect(r.x + 24, r.yMax - 76, 220, 54), "CLOSE", ButtonKind.Normal, IconFactory.Get("close"))) OnBack();
            UI.Label(new Rect(r.x + 270, r.yMax - 76, r.width - 300, 54), "Spawn Gun: MMB spawn · hold LMB grab · scroll distance · hold R rotate · F freeze · RMB throw/unfreeze · X delete · Z undo · +/- scale", 15, UI.Muted, TextAnchor.MiddleRight, FontStyle.Normal, true, true);
        }

        // ------------------------------------------------------------------ AI

        void DrawAi(Rect view)
        {
            var def = SpawnCatalog.AI[0];
            var card = new Rect(view.x, view.y, 360, view.height);
            bool selected = SpawnCatalog.Selected == def;
            UI.Rounded(card, selected ? new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.35f) : UI.PanelLight, false);
            if (selected) UI.Outline(card, UI.Accent, 2f);
            var portrait = lookIndex == 1 ? portraitClassic : portraitRandom;
            if (portrait != null) UI.Tex(new Rect(card.x + 20, card.y + 16, card.width - 40, card.height - 260), portrait, Color.white, ScaleMode.ScaleToFit);
            UI.Label(new Rect(card.x + 20, card.yMax - 236, card.width - 40, 44), "GOREDOLL", 36, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(new Rect(card.x + 20, card.yMax - 192, card.width - 40, 70), def.Description, 16, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            if (UI.Button(new Rect(card.x + 20, card.yMax - 116, card.width - 40, 46), selected ? "SELECTED" : "SELECT", selected ? ButtonKind.Primary : ButtonKind.Normal, null, 20, true, null, selected))
                Select(def);
            if (UI.Button(new Rect(card.x + 20, card.yMax - 62, card.width - 40, 46), "SPAWN IN FRONT OF ME", ButtonKind.Ghost, IconFactory.Get("plus"), 17))
                SpawnInFront(def);

            var opts = new Rect(card.xMax + 24, view.y, view.width - card.width - 24, view.height);
            UI.Label(new Rect(opts.x, opts.y, opts.width, 34), "Goredoll options", 24, Color.white);
            float y = opts.y + 46;
            int preset = System.Array.IndexOf(GoredollFactory.Presets, SpawnCatalog.GoredollPreset);
            if (preset < 0) preset = 0;
            if (UI.Selector(new Rect(opts.x, y, opts.width, 56), "Personality", ref preset, GoredollFactory.Presets, "Normal: mixed. Coward: flees and begs. Brave: stands ground. Aggressive: fights you. Passive: barely reacts."))
                SpawnCatalog.GoredollPreset = GoredollFactory.Presets[preset];
            y += 66;
            if (UI.Selector(new Rect(opts.x, y, opts.width, 56), "Look", ref lookIndex, Looks, "Random clothes/skin/hair, or the classic green shirt and jeans."))
                SpawnCatalog.GoredollVariant = lookIndex == 1 ? 0 : -1;
            y += 66;
            float half = Mathf.Floor(opts.width * 0.5f) - 8;
            UI.Toggle(new Rect(opts.x, y, half, 56), "Big heads", ref Rules.BigHeads, "Applies to Goredolls spawned from now on.");
            UI.Selector(new Rect(opts.x + half + 16, y, half, 56), "Armor", ref Armor.SpawnLevel, Armor.Options, "Helmet and/or vest on Goredolls spawned from now on. Soldiers always wear both.");
            y += 76;
            int alive = 0, dead = 0;
            foreach (var g in Goredoll.All) if (g != null) { if (g.IsDead) dead++; else alive++; }
            UI.Label(new Rect(opts.x, y, opts.width, 30), $"In the world: {alive} alive · {dead} dead · limit {Settings.Data.maxRagdolls}", 18, UI.Muted);
            y += 44;
            if (UI.Button(new Rect(opts.x, y, 300, 50), "REVIVE ALL", ButtonKind.Normal, IconFactory.Get("heart"), 18))
            {
                int n = 0;
                foreach (var g in Goredoll.All.ToArray()) if (g != null && g.IsDead && g.Revive()) n++;
                GameEvents.ShowToast("Revived " + n + " Goredoll" + (n == 1 ? "" : "s"));
            }
            if (UI.Button(new Rect(opts.x + 316, y, 300, 50), "REMOVE ALL", ButtonKind.Danger, IconFactory.Get("trash"), 18))
                foreach (var g in Goredoll.All.ToArray()) if (g != null) g.Despawn();
            if (opts.width >= 632 + 160 && UI.Button(new Rect(opts.x + 632, y, Mathf.Min(300, opts.width - 632), 112), "CUSTOMIZE", ButtonKind.Primary, IconFactory.Get("person"), 20))
                UIRoot.Push(new CustomizerScreen());
            y += 62;
            if (UI.Button(new Rect(opts.x, y, 300, 50), "REMOVE DEAD", ButtonKind.Ghost, IconFactory.Get("skull"), 18))
                foreach (var g in Goredoll.All.ToArray()) if (g != null && g.IsDead) g.Despawn();
            if (UI.Button(new Rect(opts.x + 316, y, 300, 50), "MAKE THEM DANCE", ButtonKind.Ghost, IconFactory.Get("music"), 18))
                foreach (var g in Goredoll.All) if (g != null && g.Brain != null) g.Brain.ForceDance = true;
            y += 66;
            var zombies = SpawnCatalog.AI.FindAll(d => d.Id.StartsWith("zombie_") || d.Id.StartsWith("armed_"));
            zombies.AddRange(CustomDolls.Defs);
            zombies.AddRange(SolarEclipse.Mods.NpcModKind.Defs); // Advanced Modding NPCs (MOD)
            if (opts.width < 632 + 160 && UI.Button(new Rect(opts.xMax - 220, y, 220, 44), "CUSTOMIZE", ButtonKind.Primary, IconFactory.Get("person"), 18))
                UIRoot.Push(new CustomizerScreen());
            if (zombies.Count > 0 && opts.yMax - y > 90)
            {
                UI.Label(new Rect(opts.x, y, opts.width - 240, 30), "Zombies, armed Goredolls and your custom dolls.", 18, Color.white);
                DrawGrid(new Rect(opts.x, y + 36, opts.width, opts.yMax - y - 36), zombies, "zombies", "");
            }
        }

        // ------------------------------------------------------------------ grids

        void DrawGrid(Rect view, List<SpawnableDef> defs, string id, string empty)
        {
            if (defs.Count == 0)
            {
                UI.Rounded(new Rect(view.x, view.y, view.width, 160), UI.PanelLight, false);
                UI.Tex(new Rect(view.x + 30, view.y + 40, 80, 80), IconFactory.Get("box"), UI.Muted);
                UI.Label(new Rect(view.x + 130, view.y, view.width - 160, 160), empty, 22, UI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, true, true);
                return;
            }
            const float cw = 200, ch = 230, gap = 16;
            int cols = Mathf.Max(1, Mathf.FloorToInt((view.width - 16 + gap) / (cw + gap)));
            int rows = Mathf.CeilToInt(defs.Count / (float)cols);
            float content = rows * (ch + gap);
            float s = UI.BeginScroll(id, view, content);
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                var r = new Rect((i % cols) * (cw + gap), (i / cols) * (ch + gap) - s, cw, ch);
                if (r.yMax < 0 || r.y > view.height) continue;
                bool sel = SpawnCatalog.Selected == d;
                bool hover = UI.Hover(r);
                UI.Rounded(r, sel ? new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.4f) : hover ? new Color(1, 1, 1, 0.12f) : UI.PanelLight, false);
                if (sel) UI.Outline(r, UI.Accent, 2f);
                Texture icon = IconFactory.Has("prop_" + d.Id) ? IconFactory.Get("prop_" + d.Id)
                    : d.Category == "Entities" ? (Texture)Nextbots.Face(d.Id.Replace("nextbot_", "")) : IconFactory.Get(d.Glyph);
                UI.Tex(new Rect(r.x + 20, r.y + 14, cw - 40, 140), icon, Color.white, ScaleMode.ScaleToFit);
                UI.Label(new Rect(r.x + 10, r.y + 160, cw - 20, 28), d.Name, 18, Color.white, TextAnchor.MiddleCenter);
                UI.Label(new Rect(r.x + 10, r.y + 186, cw - 20, 40), d.Description, 13, UI.Muted, TextAnchor.UpperCenter, FontStyle.Normal, false, true);
                if (UI.Hit(r)) Select(d);
            }
            UI.EndScroll(id, view, content);
        }

        void DrawGuns(Rect view)
        {
            var p = PlayerController.Instance;
            var list = WeaponCatalog.All.FindAll(w => w.Kind != WeaponKind.Fists && Mods.ModWeapons.Listed(w)); // installed mods only (hidden in MP unless the host allows them)
            list.AddRange(SolarEclipse.Mods.ScriptedWeaponMods.MenuDefs); // Advanced Modding scripted weapons (not in the catalog)
            const float rowH = 110;
            float content = list.Count * (rowH + 12) + 70;
            float s = UI.BeginScroll("guns", view, content);
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                var r = new Rect(0, i * (rowH + 12) - s, view.width - 16, rowH);
                UI.Rounded(r, UI.Hover(r) ? new Color(1, 1, 1, 0.1f) : UI.PanelLight, false);
                UI.Tex(new Rect(r.x + 16, r.y + 10, 200, rowH - 20), Hud.WeaponIcon(w), Color.white, ScaleMode.ScaleToFit);
                UI.Label(new Rect(r.x + 240, r.y + 12, 500, 36), w.Name.ToUpperInvariant(), 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
                if (Mods.ModWeapons.IsMod(w) && !Mods.ModWeapons.IsOfficial(w)) // "Modded" tag (Mods/ModWeapon.cs)
                {
                    float tx = r.x + 250 + Mathf.Min(480, UI.TextWidth(w.Name.ToUpperInvariant(), 28, FontStyle.Normal, UI.HeavyFont));
                    UI.Rounded(new Rect(tx, r.y + 16, 96, 28), UI.Accent2, true);
                    UI.Label(new Rect(tx, r.y + 16, 96, 28), Mods.ModWeapons.Tag, 15, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold, false);
                }
                UI.Label(new Rect(r.x + 240, r.y + 50, r.width - 520, 50), w.Description, 16, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                if (w.Kind == WeaponKind.Firearm)
                    UI.Label(new Rect(r.xMax - 470, r.y + 12, 200, 86), $"DMG {w.Damage}{(w.Pellets > 1 ? "×" + w.Pellets : "")}\nRPM {w.Rpm}\nMAG {w.Mag}", 15, UI.Accent2, TextAnchor.MiddleLeft);
                bool owned = p != null && p.Inventory.Has(w.Id);
                bool equipped = owned && p.Inventory.Current != null && p.Inventory.Current.Def.Id == w.Id;
                bool custom = Attachments.Supports(w); // Weapons/Attachments.cs: EQUIP on top, CUSTOMIZE below
                if (custom && UI.Button(new Rect(r.xMax - 250, r.y + 58, 230, 46), "CUSTOMIZE", ButtonKind.Ghost, IconFactory.Get("wrench"), 18))
                    UIRoot.Push(new WeaponCustomizeScreen(w.Id));
                if (UI.Button(custom ? new Rect(r.xMax - 250, r.y + 6, 230, 46) : new Rect(r.xMax - 250, r.y + 25, 230, 60), equipped ? "EQUIPPED" : "EQUIP", equipped ? ButtonKind.Primary : ButtonKind.Normal, IconFactory.Get("hand"), 22, p != null && !equipped))
                {
                    if (!SolarEclipse.Mods.ScriptedWeaponMods.TryGive(p, w.Id)) p.Inventory.Give(w.Id);
                    AudioManager.Ui("ui_success");
                    GameEvents.ShowToast(w.Name + " equipped");
                    if (w.Kind != WeaponKind.SpawnGun) OnBack();
                }
            }
            var all = new Rect(0, list.Count * (rowH + 12) - s, 320, 54);
            if (p != null && UI.Button(all, "GIVE ALL WEAPONS", ButtonKind.Ghost, IconFactory.Get("plus"), 18)) { p.Inventory.GiveAll(); AudioManager.Ui("ui_success"); }
            UI.EndScroll("guns", view, content);
        }

        void DrawOptions(Rect view)
        {
            var p = PlayerController.Instance;
            float y = view.y;
            Rect Row() { var r = new Rect(view.x, y, Mathf.Min(760, view.width), 56); y += 66; return r; }
            if (p != null)
            {
                bool nc = p.Noclip;
                if (UI.Toggle(Row(), "Noclip (V)", ref nc, "Fly through everything — except the map's Boundaries.")) p.SetNoclip(nc);
            }
            UI.Toggle(Row(), "Spawn props frozen", ref SpawnCatalog.SpawnFrozen, "New props hang in the air until you unfreeze them (RMB with the Spawn Gun).");
            UI.Toggle(Row(), "No fall damage", ref Rules.NoFallDamage);
            UI.Toggle(Row(), "AI ignore player", ref Rules.AiIgnorePlayer);
            int alive = SpawnGun.UndoStack.FindAll(g => g != null).Count;
            UI.Label(Row(), "Things you spawned that still exist: " + alive, 18, UI.Muted);
            var b = Row();
            if (UI.Button(new Rect(b.x, b.y, 240, 52), "UNDO (Z)", ButtonKind.Normal, IconFactory.Get("undo"), 18))
            {
                for (int i = SpawnGun.UndoStack.Count - 1; i >= 0; i--)
                {
                    var go = SpawnGun.UndoStack[i];
                    SpawnGun.UndoStack.RemoveAt(i);
                    if (go != null) { SpawnGun.DeleteObject(go); break; }
                }
            }
            if (UI.Button(new Rect(b.x + 256, b.y, 300, 52), "DELETE ALL I SPAWNED", ButtonKind.Danger, IconFactory.Get("trash"), 18))
            {
                foreach (var go in SpawnGun.UndoStack) SpawnGun.DeleteObject(go);
                SpawnGun.UndoStack.Clear();
            }
            var c = Row();
            if (UI.Button(new Rect(c.x, c.y, 300, 52), "CLEAR BLOOD & GIBS", ButtonKind.Ghost, IconFactory.Get("drop"), 18)) { Decals.ClearAll(); Debris.ClearAll(); BloodFX.Clear(); }
        }

        // ------------------------------------------------------------------ helpers

        static void Select(SpawnableDef d)
        {
            SpawnCatalog.Selected = d;
            AudioManager.Ui("ui_click");
            var p = PlayerController.Instance;
            if (p != null && !(p.Inventory.Current is SpawnGun))
            {
                int idx = p.Inventory.Give("spawngun");
                if (idx >= 0) p.Inventory.Select(idx, true);
            }
            GameEvents.ShowToast(d.Name + " selected — middle-click to spawn");
        }

        static void SpawnInFront(SpawnableDef d)
        {
            var p = PlayerController.Instance;
            if (p == null) return;
            var pos = p.transform.position + MathX.Flat(p.AimDirection).normalized * 2.5f;
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 10f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) pos = hit.point;
            var yaw = Quaternion.LookRotation(MathX.Flat(p.transform.position - pos) + Vector3.forward * 0.001f);
            SpawnCatalog.Spawn(d, pos, yaw);
            AudioManager.Play("sg_spawn", pos, 0.7f);
        }
    }
}
