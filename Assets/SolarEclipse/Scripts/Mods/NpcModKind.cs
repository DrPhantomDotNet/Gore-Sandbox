using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>The data part of an NPC mod (ModPackage.data, JSON). The behaviour is ModPackage.script.</summary>
    [Serializable]
    public sealed class NpcModData
    {
        public string look = "";        // CustomLook code (Goredoll/CustomDolls.cs)
        public float health = 100f;     // 10-500
        public float speed = 3.5f;      // 0.5-7
        public float strength = 1f;     // melee damage x10 (0.2-6)
        public float bravery = 0.5f, aggression = 0.5f; // 0-1 (readable flavour for the default script)
        public int team;                // 0 neutral, 1 hostile, 2 friendly
        public string weapon = "";      // WeaponCatalog id or ""

        public const float MinHealth = 10f, MaxHealth = 500f;

        public static NpcModData FromJson(string json)
        {
            NpcModData d = null;
            try { d = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<NpcModData>(json); } catch { d = null; }
            return d;
        }

        /// <summary>Clamps everything into the allowed ranges (anti-cheat). Returns notes about what was changed.</summary>
        public void Sanitize()
        {
            health = Mathf.Clamp(float.IsNaN(health) ? 100f : health, MinHealth, MaxHealth);
            speed = Mathf.Clamp(float.IsNaN(speed) ? 3.5f : speed, 0.5f, (float)SandApi.CapSpeed);
            strength = Mathf.Clamp(float.IsNaN(strength) ? 1f : strength, 0.2f, 6f);
            bravery = Mathf.Clamp01(bravery); aggression = Mathf.Clamp01(aggression);
            team = Mathf.Clamp(team, 0, 2);
            if (!string.IsNullOrEmpty(weapon)) { var w = WeaponCatalog.Get(weapon); if (w == null || !WeaponAllowed(w)) weapon = ""; }
            if (look == null || look.Length > 120 || CustomLook.Decode(look) == null) look = new CustomLook().Encode();
        }

        /// <summary>NPCs may hold ordinary guns and melee weapons, never tools, explosives or the admin gun.</summary>
        public static bool WeaponAllowed(WeaponDef w) => w != null && (w.Kind == WeaponKind.Firearm || w.Kind == WeaponKind.Knife || w.Kind == WeaponKind.Melee);

        public static List<WeaponDef> AllowedWeapons() => WeaponCatalog.All.FindAll(w => WeaponAllowed(w));

        public static Color? NamedColor(string n)
        {
            switch ((n ?? "").ToLowerInvariant())
            {
                case "red": return new Color(0.8f, 0.12f, 0.1f);
                case "green": return new Color(0.2f, 0.6f, 0.2f);
                case "blue": return new Color(0.15f, 0.3f, 0.8f);
                case "yellow": return new Color(0.95f, 0.85f, 0.2f);
                case "orange": return new Color(0.95f, 0.5f, 0.1f);
                case "purple": return new Color(0.5f, 0.2f, 0.7f);
                case "white": return new Color(0.92f, 0.92f, 0.92f);
                case "black": return new Color(0.08f, 0.08f, 0.09f);
                case "grey": case "gray": return new Color(0.45f, 0.45f, 0.48f);
                case "pink": return new Color(0.95f, 0.5f, 0.7f);
                case "cyan": return new Color(0.2f, 0.8f, 0.85f);
                case "brown": return new Color(0.4f, 0.25f, 0.12f);
            }
            return null;
        }
    }

    /// <summary>
    /// ModKind.Npc: custom NPCs with a look, stats, a weapon and a SandScript behaviour. Installed NPCs appear in the
    /// spawn menu's AI tab (tagged MOD) and spawn as Goredolls whose preset carries "~c&lt;look&gt;~m&lt;id&gt;~w&lt;weapon&gt;", so
    /// multiplayer clients build the same person and weapon; the ScriptedBrain only runs on the host.
    /// </summary>
    public static class NpcModKind
    {
        public const string IdPrefix = "modnpc_";
        public static readonly List<SpawnableDef> Defs = new List<SpawnableDef>();
        static readonly Dictionary<string, ModPackage> installed = new Dictionary<string, ModPackage>();
        static readonly Dictionary<string, SandProgram> programs = new Dictionary<string, SandProgram>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ModHub.Register(new ModKindHandler
            {
                Kind = ModKind.Npc, Title = "NPC", Icon = "person", Advanced = true,
                OpenEditor = p => UIRoot.Push(new NpcModEditorScreen(p)),
                Validate = Validate, Install = Install, Uninstall = Uninstall,
            });
            NpcMods.ModNpcHook = OnDollBuilt;
        }

        public static string Validate(ModPackage p)
        {
            if (p == null) return "Empty mod.";
            if (p.Kind != ModKind.Npc) return "Not an NPC mod.";
            if ((p.data ?? "").Length > 16 * 1024) return "Mod data is too big (max 16 KB).";
            if ((p.script ?? "").Length > SandApi.MaxSourceBytes) return "Script is too big (max 8 KB).";
            if (string.IsNullOrEmpty(p.name) || p.name.Length > 40) return "The NPC needs a name (max 40 letters).";
            var d = NpcModData.FromJson(p.data);
            if (d == null) return "The NPC data is damaged.";
            if (d.health > NpcModData.MaxHealth || d.health < NpcModData.MinHealth) return "Health must be 10-500.";
            if (d.speed > SandApi.CapSpeed || d.speed < 0.5f) return "Speed must be 0.5-7.";
            if (d.strength > 6f || d.strength < 0.2f) return "Strength must be 0.2-6.";
            if (d.team < 0 || d.team > 2) return "Unknown team.";
            if (!string.IsNullOrEmpty(d.weapon) && !NpcModData.WeaponAllowed(WeaponCatalog.Get(d.weapon))) return "NPCs can't use the weapon '" + d.weapon + "'.";
            if (!string.IsNullOrEmpty(p.script))
            {
                var err = SandProgram.Check(p.script, ScriptTarget.Npc);
                if (err != null) return "Script error: " + err;
            }
            return null;
        }

        public static void Install(ModPackage p)
        {
            if (p == null || Validate(p) != null) return;
            var id = AdvancedModding.SafeId(p.id);
            if (id.Length == 0) return;
            installed[id] = p;
            programs.Remove(id);
            Defs.RemoveAll(d => d.Id == IdPrefix + id);
            SpawnCatalog.AI.RemoveAll(d => d.Id == IdPrefix + id);
            var def = new SpawnableDef
            {
                Id = IdPrefix + id, Name = p.name, Category = "AI", Glyph = "person",
                Description = "MOD - " + (string.IsNullOrEmpty(p.description) ? "Custom NPC" : p.description) + (string.IsNullOrEmpty(p.author) ? "" : " (by " + p.author + ")"),
                Spawn = (pos, rot) => Spawn(id, pos, rot.eulerAngles.y)?.gameObject,
            };
            Defs.Add(def);
            SpawnCatalog.AI.Add(def); // SpawnCatalog.Find (host side of client spawn requests)
        }

        public static void Uninstall(ModPackage p)
        {
            if (p == null) return;
            var id = AdvancedModding.SafeId(p.id);
            installed.Remove(id);
            programs.Remove(id);
            Defs.RemoveAll(d => d.Id == IdPrefix + id);
            SpawnCatalog.AI.RemoveAll(d => d.Id == IdPrefix + id);
        }

        public static bool IsInstalled(string id) => installed.ContainsKey(AdvancedModding.SafeId(id));

        /// <summary>The preset string for a mod NPC (travels to clients in the SpawnDoll message).</summary>
        public static string PresetFor(ModPackage p)
        {
            var d = NpcModData.FromJson(p.data) ?? new NpcModData();
            d.Sanitize();
            var look = CustomLook.Decode(d.look) ?? new CustomLook();
            look.Name = CustomLook.CleanName(p.name);
            look.Role = 0;
            return "Normal~a" + Mathf.Clamp(look.Armor, 0, 3) + "~c" + look.Encode() + "~m" + AdvancedModding.SafeId(p.id) + (string.IsNullOrEmpty(d.weapon) ? "" : "~w" + AdvancedModding.SafeId(d.weapon));
        }

        public static Goredoll Spawn(string id, Vector3 pos, float yaw)
        {
            if (!installed.TryGetValue(id, out var p)) return null;
            return GoredollFactory.Spawn(pos, yaw, PresetFor(p), -1);
        }

        /// <summary>NpcMods.Attach hook (host and clients): weapon model, and on the host the ScriptedBrain.</summary>
        static void OnDollBuilt(Goredoll doll, string tokens)
        {
            string modId = Token(tokens, "~m"), weapon = Token(tokens, "~w");
            if (!string.IsNullOrEmpty(weapon) && NpcModData.WeaponAllowed(WeaponCatalog.Get(weapon))) MountWeapon(doll, weapon);
            if (doll.NetProxy || !NetState.Authority || string.IsNullOrEmpty(modId)) return;
            if (!installed.TryGetValue(modId, out var p)) return;
            var d = NpcModData.FromJson(p.data) ?? new NpcModData();
            d.Sanitize();
            SandProgram prog = null;
            if (!programs.TryGetValue(modId, out prog))
            {
                try { prog = string.IsNullOrEmpty(p.script) ? null : SandProgram.Parse(p.script, ScriptTarget.Npc); }
                catch (SandError e) { ModScriptLog.Add(p.name, "ERROR " + e.Pretty); prog = null; }
                programs[modId] = prog;
            }
            doll.gameObject.AddComponent<ScriptedBrain>().Init(doll, d, prog, modId, CustomLook.CleanName(p.name));
        }

        static string Token(string tokens, string key)
        {
            if (string.IsNullOrEmpty(tokens)) return null;
            int i = tokens.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return null;
            int end = tokens.IndexOf('~', i + 2);
            return AdvancedModding.SafeId(end < 0 ? tokens.Substring(i + 2) : tokens.Substring(i + 2, end - i - 2));
        }

        /// <summary>Puts (or replaces / removes) the weapon model in the NPC's right hand.</summary>
        public static void MountWeapon(Goredoll doll, string weaponId)
        {
            if (doll == null || !doll.Parts.TryGetValue(PartType.HandR, out var hand) || hand == null) return;
            var old = hand.transform.Find("ModWeapon");
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            if (string.IsNullOrEmpty(weaponId)) return;
            var m = ArmedDolls.MountGun(hand.transform, weaponId, doll.gameObject.layer);
            if (m != null)
            {
                m.gameObject.name = "ModWeapon";
                foreach (var c in m.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            }
        }

        /// <summary>A new NPC mod with the default look, stats and template script (the editor's starting point).</summary>
        public static ModPackage NewPackage()
        {
            var d = new NpcModData { look = new CustomLook { Name = "My NPC" }.Encode() };
            return new ModPackage { kind = (int)ModKind.Npc, name = "My NPC", description = "A custom NPC.", data = JsonUtility.ToJson(d), script = AdvancedScripts.NpcTemplate };
        }
    }
}
