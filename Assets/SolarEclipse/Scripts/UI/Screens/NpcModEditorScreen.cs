using System;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>Shared layout bits of the Advanced Modding editors.</summary>
    abstract class AdvancedEditorBase : UIScreen
    {
        protected readonly ModPackage pkg;
        protected string status, statusErr;
        protected float y;
        protected Rect col;

        protected AdvancedEditorBase(ModPackage p) { pkg = p; }

        protected Rect Row(float h = 64) { var r = new Rect(col.x, y, col.width, h); y += h + 12; return r; }

        protected void Header(string title)
        {
            UI.Fill(new Rect(0, 0, UI.VW, UI.VH), new Color(0.03f, 0.03f, 0.04f, 0.97f));
            UI.Label(new Rect(30, 14, UI.VW - 600, 60), title, 36, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            if (UI.Button(new Rect(UI.VW - 250, 14, 220, 60), "BACK", ButtonKind.Ghost, IconFactory.Get("back"), 22)) { UIRoot.Pop(); return; }
            if (!string.IsNullOrEmpty(statusErr)) UI.Label(new Rect(30, 78, UI.VW - 60, 40), statusErr, 20, UI.Bad);
            else if (!string.IsNullOrEmpty(status)) UI.Label(new Rect(30, 78, UI.VW - 60, 40), status, 20, UI.Good);
        }

        protected void NameFields()
        {
            UI.Label(Row(30), "NAME", 18, UI.Muted);
            pkg.name = Clip(UI.TextField(Row(), pkg.name, "adv_name", false, "Name"), 40);
            UI.Label(Row(30), "DESCRIPTION", 18, UI.Muted);
            pkg.description = Clip(UI.TextField(Row(), pkg.description, "adv_desc", false, "What does it do?"), 200);
        }

        protected static string Clip(string s, int n) => s == null ? "" : s.Length > n ? s.Substring(0, n) : s;

        protected bool SaveButton(Rect r)
        {
            if (!UI.Button(r, "SAVE", ButtonKind.Primary, IconFactory.Get("check"), 24)) return false;
            var err = ModHub.Validate(pkg);
            if (err != null) { statusErr = err; status = null; AudioManager.Ui("ui_error"); return false; }
            AdvancedModding.Save(pkg);
            statusErr = null; status = "Saved and installed.";
            AudioManager.Ui("ui_success");
            return true;
        }
    }

    /// <summary>Editor for ModKind.Npc: name, look, stats, team, weapon and the behaviour script.</summary>
    sealed class NpcModEditorScreen : AdvancedEditorBase
    {
        readonly NpcModData data;
        readonly CustomLook look;
        int tab;
        static readonly string[] Tabs = { "INFO & STATS", "LOOK", "BEHAVIOUR" };
        static readonly string[] ColorNames = { "White", "Black", "Grey", "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple", "Pink", "Brown", "Tan", "Pale", "Dark" };
        static readonly Color[] ColorValues =
        {
            new Color(0.92f, 0.92f, 0.92f), new Color(0.08f, 0.08f, 0.09f), new Color(0.45f, 0.45f, 0.48f), new Color(0.8f, 0.12f, 0.1f), new Color(0.95f, 0.5f, 0.1f),
            new Color(0.95f, 0.85f, 0.2f), new Color(0.2f, 0.6f, 0.2f), new Color(0.2f, 0.8f, 0.85f), new Color(0.15f, 0.3f, 0.8f), new Color(0.5f, 0.2f, 0.7f),
            new Color(0.95f, 0.5f, 0.7f), new Color(0.4f, 0.25f, 0.12f), new Color(0.91f, 0.72f, 0.58f), new Color(0.98f, 0.86f, 0.78f), new Color(0.45f, 0.3f, 0.2f),
        };
        string[] weaponIds, weaponNames;

        public NpcModEditorScreen(ModPackage p) : base(p ?? NpcModKind.NewPackage())
        {
            data = NpcModData.FromJson(pkg.data) ?? new NpcModData();
            data.Sanitize();
            look = CustomLook.Decode(data.look) ?? new CustomLook();
            var list = NpcModData.AllowedWeapons();
            weaponIds = new string[list.Count + 1]; weaponNames = new string[list.Count + 1];
            weaponIds[0] = ""; weaponNames[0] = "None (fists)";
            for (int i = 0; i < list.Count; i++) { weaponIds[i + 1] = list[i].Id; weaponNames[i + 1] = list[i].Name; }
        }

        void Commit()
        {
            look.Name = CustomLook.CleanName(pkg.name);
            data.look = look.Encode();
            pkg.data = JsonUtility.ToJson(data);
            pkg.kind = (int)ModKind.Npc;
        }

        public override void Draw()
        {
            Header("NPC MOD");
            col = new Rect(60, 196, Mathf.Min(1100, UI.VW - 520), UI.VH - 220);
            tab = UI.Tabs(new Rect(60, 124, Mathf.Min(1100, UI.VW - 520), 60), tab, Tabs, 20);
            y = col.y;
            float content = 1100;
            var view = new Rect(col.x - 10, col.y, col.width + 20, col.height);
            float s = UI.BeginScroll("npcmod_" + tab, view, content);
            col = new Rect(10, -s + 10, col.width, col.height);
            y = col.y;
            if (tab == 0) DrawInfo();
            else if (tab == 1) DrawLook();
            else DrawBehaviour();
            UI.EndScroll("npcmod_" + tab, view, content);
            Commit();

            // right column: actions
            float rx = UI.VW - 420;
            if (SaveButton(new Rect(rx, 196, 380, 80))) { }
            bool inGame = PlayerController.Instance != null && GameManager.I != null && GameManager.I.State == GameState.Playing;
            if (UI.Button(new Rect(rx, 290, 380, 70), "TEST SPAWN", ButtonKind.Normal, IconFactory.Get("person"), 22, inGame, "Saves, then spawns it in front of you."))
            {
                var err = ModHub.Validate(pkg);
                if (err != null) statusErr = err;
                else
                {
                    AdvancedModding.Save(pkg);
                    NpcModKind.Install(pkg);
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        var fwd = MathX.Flat(cam.transform.forward).normalized;
                        var pos = cam.transform.position + fwd * 3f;
                        if (Physics.Raycast(pos + Vector3.up, Vector3.down, out var hit, 10f, Layers.SightMask, QueryTriggerInteraction.Ignore)) pos = hit.point;
                        var def = SpawnCatalog.Find(NpcModKind.IdPrefix + AdvancedModding.SafeId(pkg.id));
                        if (def != null) SpawnCatalog.Spawn(def, pos, Quaternion.LookRotation(-fwd));
                    }
                    UIRoot.Pop();
                }
            }
            UI.Label(new Rect(rx, 380, 380, 300),
                "Scripts are SandScript: a safe, built-in language. They can only use the game's own commands, numbers are capped, and slow or broken scripts are switched off.",
                17, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
        }

        void DrawInfo()
        {
            NameFields();
            UI.Slider(Row(), "HEALTH", ref data.health, NpcModData.MinHealth, NpcModData.MaxHealth, "0");
            UI.Slider(Row(), "SPEED", ref data.speed, 0.5f, (float)SandApi.CapSpeed, "0.0", null, " m/s");
            UI.Slider(Row(), "STRENGTH", ref data.strength, 0.2f, 6f, "0.0", "Melee damage = 10 x strength (max 60).", "x");
            UI.Slider(Row(), "BRAVERY", ref data.bravery, 0f, 1f, "0.00");
            UI.Slider(Row(), "AGGRESSION", ref data.aggression, 0f, 1f, "0.00");
            UI.Selector(Row(), "TEAM", ref data.team, new[] { "Neutral", "Hostile", "Friendly" }, "Friendly NPCs never hurt players.");
            int wi = Mathf.Max(0, Array.IndexOf(weaponIds, data.weapon ?? ""));
            if (UI.Selector(Row(), "WEAPON", ref wi, weaponNames)) data.weapon = weaponIds[wi];
        }

        int ColorIndex(Color c)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < ColorValues.Length; i++)
            {
                var d = ColorValues[i] - c;
                float dd = d.r * d.r + d.g * d.g + d.b * d.b;
                if (dd < bd) { bd = dd; best = i; }
            }
            return best;
        }

        void ColorRow(string label, ref Color c)
        {
            int i = ColorIndex(c);
            var r = Row();
            UI.Rounded(new Rect(r.xMax - 56, r.y + 8, 48, r.height - 16), c, true);
            if (UI.Selector(new Rect(r.x, r.y, r.width - 70, r.height), label, ref i, ColorNames)) c = ColorValues[i];
        }

        void DrawLook()
        {
            ColorRow("SKIN", ref look.Skin);
            ColorRow("SHIRT", ref look.Shirt);
            ColorRow("PANTS", ref look.Pants);
            ColorRow("SHOES", ref look.Shoes);
            ColorRow("HAIR", ref look.Hair);
            UI.Selector(Row(), "HAIR STYLE", ref look.HairStyle, new[] { "Short", "Buzz", "Bald", "Long" });
            UI.Toggle(Row(), "LONG SLEEVES", ref look.LongSleeves);
            UI.Toggle(Row(), "BEARD", ref look.Beard);
            UI.Toggle(Row(), "GLASSES", ref look.Glasses);
            UI.Slider(Row(), "HEIGHT", ref look.Height, CustomLook.MinHeight, CustomLook.MaxHeight, "0.00", null, "x");
            UI.Slider(Row(), "BUILD", ref look.Build, CustomLook.MinBuild, CustomLook.MaxBuild, "0.00", null, "x");
            UI.Selector(Row(), "ARMOUR", ref look.Armor, new[] { "None", "Helmet", "Vest", "Full" });
        }

        void DrawBehaviour()
        {
            var err = string.IsNullOrEmpty(pkg.script) ? null : SandProgram.Check(pkg.script, ScriptTarget.Npc);
            UI.Label(Row(40), string.IsNullOrEmpty(pkg.script) ? "No script: the NPC just stands there." : err == null ? "Script OK." : err, 20, err == null ? UI.Good : UI.Bad);
            if (UI.Button(Row(90), "EDIT SCRIPT", ButtonKind.Primary, IconFactory.Get("wrench"), 26))
                UIRoot.Push(new ScriptEditorScreen("NPC script - " + pkg.name, pkg.script, ScriptTarget.Npc, s => pkg.script = s));
            if (UI.Button(Row(), "USE TEMPLATE", ButtonKind.Ghost, null, 20))
                UIRoot.Push(new ConfirmDialog("TEMPLATE", "Replace the script with the starter template?", () => pkg.script = AdvancedScripts.NpcTemplate, "REPLACE"));
            var preview = pkg.script ?? "";
            if (preview.Length > 900) preview = preview.Substring(0, 900) + "\n...";
            var r = Row(600);
            UI.Rounded(r, new Color(0.08f, 0.08f, 0.1f, 1f));
            UI.Label(new Rect(r.x + 16, r.y + 10, r.width - 32, r.height - 20), preview, 17, Color.white, TextAnchor.UpperLeft, FontStyle.Normal, false, true, UI.BuiltinFont);
            int n = 0;
            foreach (var l in ModScriptLog.Lines) if (l.StartsWith("[" + CustomLook.CleanName(pkg.name) + "]")) n++;
            if (n > 0) UI.Label(Row(30), n + " log line(s) from this NPC - see the script editor.", 16, UI.Muted);
        }
    }

    /// <summary>Editor for ModKind.ScriptedWeapon: base weapon, colour, damage and the on fire / on hit script.</summary>
    sealed class ScriptedWeaponEditorScreen : AdvancedEditorBase
    {
        readonly ScriptedWeaponData data;

        public ScriptedWeaponEditorScreen(ModPackage p) : base(p ?? NewPackage())
        {
            data = ScriptedWeaponData.FromJson(pkg.data) ?? new ScriptedWeaponData();
            data.Sanitize();
        }

        static ModPackage NewPackage() => new ModPackage
        {
            kind = (int)ModKind.ScriptedWeapon, name = "My Weapon", description = "A scripted weapon.",
            data = JsonUtility.ToJson(new ScriptedWeaponData()), script = AdvancedScripts.WeaponTemplate,
        };

        public override void Draw()
        {
            Header("SCRIPTED WEAPON");
            var view = new Rect(50, 130, Mathf.Min(1100, UI.VW - 520), UI.VH - 150);
            const float content = 1300;
            float s = UI.BeginScroll("swmod", view, content);
            col = new Rect(10, 10 - s, view.width - 20, content);
            y = col.y;
            NameFields();
            int bi = Mathf.Max(0, Array.IndexOf(ScriptedWeaponMods.Bases, data.baseWeapon));
            if (UI.Selector(Row(), "BASE", ref bi, ScriptedWeaponMods.BaseNames)) data.baseWeapon = ScriptedWeaponMods.Bases[bi];
            var tr = Row(50);
            UI.Label(tr, "COLOUR", 20, UI.Muted);
            UI.Rounded(new Rect(tr.xMax - 120, tr.y + 4, 110, 42), data.Tint, true);
            UI.Slider(Row(), "RED", ref data.r, 0f, 1f, "0.00");
            UI.Slider(Row(), "GREEN", ref data.g, 0f, 1f, "0.00");
            UI.Slider(Row(), "BLUE", ref data.b, 0f, 1f, "0.00");
            UI.Slider(Row(), "DAMAGE", ref data.damageMul, 0.5f, 1.5f, "0.00", "Firearms only.", "x");
            var err = string.IsNullOrEmpty(pkg.script) ? null : SandProgram.Check(pkg.script, ScriptTarget.Weapon);
            UI.Label(Row(40), string.IsNullOrEmpty(pkg.script) ? "No script: behaves like the base weapon." : err == null ? "Script OK." : err, 20, err == null ? UI.Good : UI.Bad);
            if (UI.Button(Row(90), "EDIT SCRIPT", ButtonKind.Primary, IconFactory.Get("wrench"), 26))
                UIRoot.Push(new ScriptEditorScreen("Weapon script - " + pkg.name, pkg.script, ScriptTarget.Weapon, sc => pkg.script = sc));
            var r = Row(420);
            UI.Rounded(r, new Color(0.08f, 0.08f, 0.1f, 1f));
            var preview = pkg.script ?? "";
            if (preview.Length > 700) preview = preview.Substring(0, 700) + "\n...";
            UI.Label(new Rect(r.x + 16, r.y + 10, r.width - 32, r.height - 20), preview, 17, Color.white, TextAnchor.UpperLeft, FontStyle.Normal, false, true, UI.BuiltinFont);
            UI.EndScroll("swmod", view, content);

            pkg.kind = (int)ModKind.ScriptedWeapon;
            pkg.data = JsonUtility.ToJson(data);

            float rx = UI.VW - 420;
            SaveButton(new Rect(rx, 130, 380, 80));
            var pc = PlayerController.Instance;
            bool inGame = pc != null && GameManager.I != null && GameManager.I.State == GameState.Playing;
            if (UI.Button(new Rect(rx, 224, 380, 70), "SAVE & EQUIP", ButtonKind.Normal, IconFactory.Get("hand"), 22, inGame))
            {
                var e = ModHub.Validate(pkg);
                if (e != null) statusErr = e;
                else { AdvancedModding.Save(pkg); ScriptedWeaponMods.Install(pkg); ScriptedWeaponMods.Give(pc, pkg); UIRoot.Pop(); }
            }
            UI.Label(new Rect(rx, 310, 380, 300),
                "Weapon scripts run on fire, on hit and on equip. Extra damage, knockback and fire are capped so modded weapons stay fair.",
                17, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
        }
    }
}
