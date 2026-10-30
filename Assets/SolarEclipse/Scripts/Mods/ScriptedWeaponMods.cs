using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>The data part of a scripted weapon mod (ModPackage.data, JSON).</summary>
    [Serializable]
    public sealed class ScriptedWeaponData
    {
        public string baseWeapon = "glock";   // one of ScriptedWeaponMods.Bases
        public float r = 1f, g = 1f, b = 1f;  // colour tint of the model
        public float damageMul = 1f;          // 0.5-1.5 (firearms)

        public static ScriptedWeaponData FromJson(string json)
        {
            try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<ScriptedWeaponData>(json); } catch { return null; }
        }

        public void Sanitize()
        {
            if (Array.IndexOf(ScriptedWeaponMods.Bases, baseWeapon) < 0 || WeaponCatalog.Get(baseWeapon) == null) baseWeapon = "glock";
            r = Mathf.Clamp01(r); g = Mathf.Clamp01(g); b = Mathf.Clamp01(b);
            damageMul = Mathf.Clamp(float.IsNaN(damageMul) ? 1f : damageMul, 0.5f, 1.5f);
        }

        public Color Tint => new Color(r, g, b);
    }

    /// <summary>
    /// ModKind.ScriptedWeapon: a built-in weapon (pistol, SMG, rifle, shotgun, knife) re-coloured, with SandScript hooks
    /// (on fire / on hit / on equip / every N). The weapon keeps its catalog definition, so multiplayer sync works
    /// unchanged; the script runs only on the host / in singleplayer, for the host's own weapon.
    /// </summary>
    public static class ScriptedWeaponMods
    {
        public static readonly string[] Bases = { "glock", "uzi", "ar15", "ar17", "knife" };
        public static readonly string[] BaseNames = { "Pistol", "SMG", "Rifle", "Shotgun", "Knife" };
        public const string IdPrefix = "modw_";

        static readonly Dictionary<string, ModPackage> installed = new Dictionary<string, ModPackage>();
        /// <summary>Display entries for the spawn menu's weapon list (Id = IdPrefix + mod id; not in WeaponCatalog).</summary>
        public static readonly List<WeaponDef> MenuDefs = new List<WeaponDef>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ModHub.Register(new ModKindHandler
            {
                Kind = ModKind.ScriptedWeapon, Title = "Scripted weapon", Icon = "gun", Advanced = true,
                OpenEditor = p => UIRoot.Push(new ScriptedWeaponEditorScreen(p)),
                Validate = Validate, Install = Install, Uninstall = Uninstall,
            });
        }

        public static string Validate(ModPackage p)
        {
            if (p == null) return "Empty mod.";
            if (p.Kind != ModKind.ScriptedWeapon) return "Not a scripted weapon.";
            if ((p.data ?? "").Length > 16 * 1024) return "Mod data is too big (max 16 KB).";
            if ((p.script ?? "").Length > SandApi.MaxSourceBytes) return "Script is too big (max 8 KB).";
            if (string.IsNullOrEmpty(p.name) || p.name.Length > 40) return "The weapon needs a name (max 40 letters).";
            var d = ScriptedWeaponData.FromJson(p.data);
            if (d == null) return "The weapon data is damaged.";
            if (Array.IndexOf(Bases, d.baseWeapon) < 0) return "Unknown base weapon '" + d.baseWeapon + "'.";
            if (d.damageMul < 0.5f || d.damageMul > 1.5f) return "Damage must be 50-150%.";
            if (!string.IsNullOrEmpty(p.script))
            {
                var err = SandProgram.Check(p.script, ScriptTarget.Weapon);
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
            var d = ScriptedWeaponData.FromJson(p.data); d.Sanitize();
            var baseDef = WeaponCatalog.Get(d.baseWeapon);
            MenuDefs.RemoveAll(w => w.Id == IdPrefix + id);
            MenuDefs.Add(new WeaponDef
            {
                Id = IdPrefix + id, Name = p.name, Kind = baseDef.Kind, Glyph = baseDef.Glyph,
                Description = "MOD - " + (string.IsNullOrEmpty(p.description) ? "Scripted " + baseDef.Name : p.description),
                Damage = baseDef.Damage * d.damageMul, Rpm = baseDef.Rpm, Mag = baseDef.Mag, Pellets = baseDef.Pellets,
            });
        }

        public static void Uninstall(ModPackage p)
        {
            if (p == null) return;
            var id = AdvancedModding.SafeId(p.id);
            installed.Remove(id);
            MenuDefs.RemoveAll(w => w.Id == IdPrefix + id);
        }

        /// <summary>Spawn menu EQUIP for a MenuDefs entry. False if the id isn't a scripted weapon.</summary>
        public static bool TryGive(PlayerController player, string menuId)
        {
            if (player == null || string.IsNullOrEmpty(menuId) || !menuId.StartsWith(IdPrefix, StringComparison.Ordinal)) return false;
            if (!installed.TryGetValue(menuId.Substring(IdPrefix.Length), out var p)) return false;
            Give(player, p);
            return true;
        }

        public static void Give(PlayerController player, ModPackage p)
        {
            if (player == null || p == null) return;
            var d = ScriptedWeaponData.FromJson(p.data) ?? new ScriptedWeaponData();
            d.Sanitize();
            var baseDef = WeaponCatalog.Get(d.baseWeapon);
            if (baseDef == null) return;
            // Replace an older copy of the same mod weapon.
            foreach (var kv in new List<KeyValuePair<Weapon, ScriptedWeaponInstance>>(ScriptedWeaponDriver.Live))
                if (kv.Value.ModId == p.id)
                {
                    int slot = Array.IndexOf(player.Inventory.Slots, kv.Key);
                    if (slot >= 0) { player.Inventory.GiveCustom(Create(p, d, baseDef), slot, true); return; }
                }
            player.Inventory.GiveCustom(Create(p, d, baseDef), -1, true);
        }

        static Weapon Create(ModPackage p, ScriptedWeaponData d, WeaponDef baseDef)
        {
            var w = Weapon.Create(baseDef);
            SandProgram prog = null;
            try { prog = string.IsNullOrEmpty(p.script) ? null : SandProgram.Parse(p.script, ScriptTarget.Weapon); }
            catch (SandError e) { ModScriptLog.Add(p.name, "ERROR " + e.Pretty); }
            ScriptedWeaponDriver.Track(w, new ScriptedWeaponInstance(p.id, CustomLook.CleanName(p.name), prog, d));
            return w;
        }
    }

    /// <summary>One scripted weapon in a player's inventory: its script runtime and the ISandHost the script talks to.</summary>
    public sealed class ScriptedWeaponInstance : ISandHost
    {
        public readonly string ModId, Name;
        public readonly SandRuntime Runtime;
        public readonly ScriptedWeaponData Data;
        internal Weapon Weapon;
        internal bool Styled, WasEquipped;
        internal int LastAmmo = -1;

        // context of the current event
        internal Vector3 Point, Normal = Vector3.up, Dir = Vector3.forward;
        internal Goredoll Target;
        internal float HitDamage;

        float nextProjectile, nextDamage, nextKnock, nextIgnite, nextFx, nextSound;
        int projectilesThisSecond; float projWindow;
        readonly List<object> ents = new List<object>();

        public ScriptedWeaponInstance(string id, string name, SandProgram prog, ScriptedWeaponData data)
        {
            ModId = id; Name = name; Data = data;
            if (prog != null) Runtime = new SandRuntime(prog, this);
        }

        PlayerController Holder => Weapon != null ? PlayerController.Instance : null;
        Vector3 Eye => Camera.main != null ? Camera.main.transform.position : Holder != null ? Holder.transform.position + Vector3.up * 1.6f : Vector3.zero;
        Vector3 Aim => Camera.main != null ? Camera.main.transform.forward : Vector3.forward;

        public double Now => Time.time;

        public int Entity(string word)
        {
            object o = null;
            switch (word)
            {
                case "target": o = Target != null && !Target.IsDead ? Target : null; break;
                case "self": case "player": case "attacker": o = Holder; break;
                case "npc":
                    {
                        float bd = 40f;
                        var from = Holder != null ? Holder.transform.position : Point;
                        foreach (var g in Goredoll.All) { if (g == null || g.IsDead) continue; float d = Vector3.Distance(g.Position, from); if (d < bd) { bd = d; o = g; } }
                        break;
                    }
            }
            if (o == null) return 0;
            int i = ents.IndexOf(o);
            if (i >= 0) return i + 1;
            if (ents.Count >= 32) ents.Clear();
            ents.Add(o);
            return ents.Count;
        }

        object Ent(SVal v) { int id = v.EntId; if (id <= 0 || id > ents.Count) return null; var o = ents[id - 1]; return o is UnityEngine.Object uo && uo == null ? null : o; }

        public bool GetProp(string name, out SVal v)
        {
            switch (name)
            {
                case "damage": v = SVal.Num(Weapon != null ? Weapon.Def.Damage * Data.damageMul : 0); return true;
                case "hit_damage": v = SVal.Num(Math.Round(HitDamage, 1)); return true;
                case "ammo": v = SVal.Num(Weapon is Firearm fa ? fa.Ammo : 0); return true;
                case "time": v = SVal.Num(Time.time); return true;
                case "has_target": v = SVal.Bool(Target != null && !Target.IsDead); return true;
                case "health": { var ph = Holder != null ? Holder.GetComponent<PlayerHealth>() : null; v = SVal.Num(ph != null ? Mathf.Round(ph.Current) : 100); return true; }
            }
            v = SVal.Zero; return false;
        }

        public void SetProp(string name, SVal v) { } // weapon stats come from the mod data (no cheating through scripts)

        public void Log(string text) => ModScriptLog.Add(Name, text);

        public SVal Call(string name, SVal[] a)
        {
            double N(int i, double min, double max, double def = 0) => i < a.Length ? SandApi.Clamp(a[i].AsNum, min, max) : def;
            string S(int i) => i < a.Length ? a[i].ToString() : "";
            switch (name)
            {
                case "distance": { var o = Ent(a[0]); var from = Holder != null ? Holder.transform.position : Point; return SVal.Num(o is Goredoll g ? Math.Round(Vector3.Distance(g.Position, from), 2) : o is Component c ? Math.Round(Vector3.Distance(c.transform.position, from), 2) : 999); }
                case "is_dead": { var o = Ent(a[0]); return SVal.Bool(o == null || (o is Goredoll g && g.IsDead)); }
                case "health_of": { var o = Ent(a[0]); return SVal.Num(o is Goredoll g ? (g.IsDead ? 0 : Mathf.Round(100f * (1f - g.Vitals.BloodLossFraction))) : 100); }
                case "say": if (Time.time >= nextSound && Holder != null) { nextSound = Time.time + 0.5f; GameEvents.ShowToast(Name + ": " + ModChat.Clean(S(0))); } break;
                case "effect":
                case "spawn_effect":
                    if (Time.time >= nextFx) { nextFx = Time.time + 0.05f; ModSounds.Effect(S(0), Point, Normal); }
                    break;
                case "sound":
                case "play_sound":
                    if (Time.time >= nextSound) { nextSound = Time.time + 0.15f; ModSounds.Play(S(0), Point); }
                    break;
                case "explode":
                    if (Time.time >= nextProjectile + 1.5f)
                    {
                        nextProjectile = Time.time;
                        float r = (float)N(0, 0.5, SandApi.CapExplode, 2);
                        Explosion.Create(Point, r, 25f + r * 12f, Weapon, 0.8f);
                    }
                    break;
                case "spawn_projectile": SpawnProjectile(S(0).ToLowerInvariant()); break;
                case "damage_target":
                    if (Target != null && !Target.IsDead && Time.time >= nextDamage)
                    {
                        nextDamage = Time.time + 0.08f;
                        var part = NearestPart(Target, Point);
                        if (part != null)
                            part.TakeDamage(new DamageInfo
                            {
                                Amount = (float)N(0, 0, SandApi.CapDamage), Force = Dir * 2f, Point = Point, Normal = Normal, Direction = Dir,
                                Type = DamageType.Blunt, Source = Weapon, Weapon = Name,
                            });
                    }
                    break;
                case "knockback":
                    if (Target != null && Time.time >= nextKnock)
                    {
                        nextKnock = Time.time + 0.1f;
                        float k = (float)N(0, 0, SandApi.CapKnockback);
                        var dir = MathX.Flat(Dir).sqrMagnitude > 0.01f ? MathX.Flat(Dir).normalized : Vector3.forward;
                        Target.Launch(dir * k * 0.4f + Vector3.up * k * 0.12f);
                        if (k > 12f && Target.Ragdoll != null) Target.Ragdoll.Fall(1.5f);
                    }
                    break;
                case "ignite":
                    if (Target != null && !Target.IsDead && Time.time >= nextIgnite)
                    {
                        nextIgnite = Time.time + 0.5f;
                        ScriptedWeaponDriver.Ignite(Target, (float)N(0, 0.2, SandApi.CapIgnite, 2), Weapon, Name);
                    }
                    break;
            }
            return SVal.Zero;
        }

        static BodyPart NearestPart(Goredoll g, Vector3 p)
        {
            BodyPart best = null; float bd = float.MaxValue;
            foreach (var part in g.Parts.Values)
            {
                if (part == null || part.Severed) continue;
                float d = (part.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = part; }
            }
            return best;
        }

        void SpawnProjectile(string kind)
        {
            if (Weapon == null || Holder == null) return;
            if (Time.time - projWindow > 1f) { projWindow = Time.time; projectilesThisSecond = 0; }
            if (++projectilesThisSecond > 10) return; // max 10 extra shots per second
            var baseDef = Weapon.Def;
            var shot = new WeaponDef
            {
                Id = baseDef.Id, Name = Name, Kind = WeaponKind.Firearm, Damage = Mathf.Min(baseDef.Kind == WeaponKind.Firearm ? baseDef.Damage * 0.6f : 15f, (float)SandApi.CapDamage),
                Penetration = 0.3f, Force = 6f, Caliber = 9f, Range = 200f, Pellets = 1,
            };
            var eye = Eye; var aim = Aim;
            switch (kind)
            {
                case "pellets":
                    shot.Damage = Mathf.Min(shot.Damage, 10f);
                    for (int i = 0; i < 5; i++) Ballistics.Fire(eye, Quaternion.Euler(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f), 0) * aim, shot, Holder, eye + aim * 0.5f);
                    break;
                case "fireball":
                    {
                        var end = Ballistics.Fire(eye, aim, shot, Holder, eye + aim * 0.5f);
                        for (int i = 0; i < 6; i++) FX.Fire(Vector3.Lerp(eye + aim, end, i / 5f), Vector3.up * 0.3f, 0.3f);
                        break;
                    }
                case "spark":
                    {
                        shot.Damage = Mathf.Min(shot.Damage, 12f);
                        var end = Ballistics.Fire(eye, aim, shot, Holder, eye + aim * 0.5f);
                        FX.Sparks(end, -aim, 12, 6f);
                        FX.Flash(end, new Color(0.6f, 0.8f, 1f), 4f, 2f, 0.1f);
                        break;
                    }
                case "rocket":
                    {
                        if (Physics.Raycast(eye, aim, out var hit, 60f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                            Explosion.Create(hit.point + hit.normal * 0.2f, 2f, 45f, Weapon, 0.8f);
                        projectilesThisSecond += 5; // rockets count as several shots
                        break;
                    }
                default: Ballistics.Fire(eye, aim, shot, Holder, eye + aim * 0.5f); break;
            }
        }
    }

    /// <summary>Watches the local player's scripted weapons: equip / fire / hit detection, ticking, burning targets.</summary>
    public sealed class ScriptedWeaponDriver : MonoBehaviour
    {
        public static readonly Dictionary<Weapon, ScriptedWeaponInstance> Live = new Dictionary<Weapon, ScriptedWeaponInstance>();
        static ScriptedWeaponDriver inst;

        sealed class Burn { public Goredoll Doll; public float Until, Next; public object Source; public string Name; }
        readonly List<Burn> burns = new List<Burn>();

        public static void Track(Weapon w, ScriptedWeaponInstance i)
        {
            i.Weapon = w;
            Live[w] = i;
            if (inst == null)
            {
                var go = new GameObject("ScriptedWeapons");
                DontDestroyOnLoad(go);
                inst = go.AddComponent<ScriptedWeaponDriver>();
                GameEvents.HitMarker += OnHitMarker;
            }
        }

        public static void Ignite(Goredoll g, float seconds, object source, string name)
        {
            if (inst == null || g == null) return;
            foreach (var b in inst.burns) if (b.Doll == g) { b.Until = Mathf.Max(b.Until, Time.time + seconds); return; }
            if (inst.burns.Count >= 12) return;
            inst.burns.Add(new Burn { Doll = g, Until = Time.time + seconds, Next = Time.time, Source = source, Name = name });
        }

        static ScriptedWeaponInstance Current(out Weapon w)
        {
            w = null;
            var p = PlayerController.Instance;
            if (p == null || p.Inventory == null) return null;
            w = p.Inventory.Current;
            return w != null && Live.TryGetValue(w, out var i) ? i : null;
        }

        static void OnHitMarker(Vector3 point, float damage)
        {
            if (!NetState.Authority) return;
            var i = Current(out _);
            if (i == null || i.Runtime == null) return;
            Goredoll best = null; float bd = 2.5f;
            foreach (var g in Goredoll.All)
            {
                if (g == null) continue;
                foreach (var part in g.Parts.Values)
                {
                    if (part == null) continue;
                    float d = Vector3.Distance(part.transform.position, point);
                    if (d < bd) { bd = d; best = g; }
                }
            }
            i.Point = point; i.Target = best; i.HitDamage = damage;
            i.Dir = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            i.Normal = -i.Dir;
            if (i.Weapon.Def.Kind != WeaponKind.Firearm) i.Runtime.Fire("fire"); // melee: a landed swing counts as "fire"
            i.Runtime.Fire("hit");
        }

        void Update()
        {
            // Forget weapons that were destroyed / dropped.
            if (Live.Count > 0)
            {
                List<Weapon> dead = null;
                var p = PlayerController.Instance;
                foreach (var w in Live.Keys)
                    if (p == null || Array.IndexOf(p.Inventory.Slots, w) < 0) (dead ?? (dead = new List<Weapon>())).Add(w);
                if (dead != null) foreach (var w in dead) Live.Remove(w);
            }

            var cur = Current(out var weapon);
            foreach (var kv in Live)
            {
                var i = kv.Value;
                if (!i.Styled && kv.Key.Model != null) { i.Styled = true; Style(kv.Key, i); }
                bool equipped = kv.Key == weapon;
                if (!equipped) { i.WasEquipped = false; continue; }
                if (!NetState.Authority || i.Runtime == null) continue;
                try
                {
                    if (!i.WasEquipped) { i.WasEquipped = true; i.Point = PlayerController.Instance.transform.position; i.Runtime.Fire("equip"); }
                    if (kv.Key is Firearm fa)
                    {
                        if (i.LastAmmo >= 0 && fa.Ammo < i.LastAmmo)
                        {
                            var cam = Camera.main;
                            i.Dir = cam != null ? cam.transform.forward : Vector3.forward;
                            i.Point = cam != null ? cam.transform.position + i.Dir * 0.8f : PlayerController.Instance.transform.position;
                            i.Normal = i.Dir; i.Target = null;
                            i.Runtime.Fire("fire");
                        }
                        i.LastAmmo = fa.Ammo;
                    }
                    i.Runtime.Tick();
                }
                catch (Exception e) { Debug.LogWarning("[SandScript " + i.Name + "] " + e.Message); }
            }

            for (int k = burns.Count - 1; k >= 0; k--)
            {
                var b = burns[k];
                if (b.Doll == null || Time.time > b.Until) { burns.RemoveAt(k); continue; }
                if (Time.time < b.Next) continue;
                b.Next = Time.time + 0.35f;
                var chest = b.Doll.Parts.TryGetValue(PartType.Chest, out var c) ? c : null;
                if (chest == null) continue;
                FX.Fire(chest.transform.position + UnityEngine.Random.insideUnitSphere * 0.2f, Vector3.up * 0.8f, 0.4f);
                if (!b.Doll.IsDead && NetState.Authority)
                    chest.TakeDamage(new DamageInfo { Amount = 2.5f, Point = chest.transform.position, Normal = Vector3.up, Direction = Vector3.down, Type = DamageType.Blunt, Source = b.Source, Weapon = b.Name + " (fire)" });
            }
        }

        static void Style(Weapon w, ScriptedWeaponInstance i)
        {
            var tint = i.Data.Tint;
            if (tint != Color.white)
                foreach (var r in w.Model.GetComponentsInChildren<Renderer>(true))
                {
                    var mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", tint);
                    mpb.SetColor("_Color", tint);
                    r.SetPropertyBlock(mpb);
                }
            if (w is Firearm fa && Mathf.Abs(i.Data.damageMul - 1f) > 0.01f)
            {
                var src = fa.ShotDef ?? fa.Def;
                fa.ShotDef = new WeaponDef
                {
                    Id = src.Id, Name = src.Name, Description = src.Description, Glyph = src.Glyph, Kind = src.Kind,
                    Damage = src.Damage * i.Data.damageMul, Rpm = src.Rpm, HipSpread = src.HipSpread, AimSpread = src.AimSpread, Recoil = src.Recoil,
                    Range = src.Range, Penetration = src.Penetration, Force = src.Force, Caliber = src.Caliber, Mag = src.Mag, Pellets = src.Pellets,
                    FireSound = src.FireSound,
                };
            }
        }
    }
}
