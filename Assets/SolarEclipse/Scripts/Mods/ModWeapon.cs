using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>
    /// Turns Weapon Editor mods into real weapons: a WeaponDef appended to <see cref="WeaponCatalog.All"/> (after every
    /// built-in weapon; installed mods are added sorted by id at startup), a model built from the parts list, and a
    /// Firearm / melee weapon driven by the design. Def ids are "mod_&lt;mod id&gt;".
    /// </summary>
    public static class ModWeapons
    {
        public const string Prefix = "mod_";
        public const string Tag = "MODDED";

        sealed class Entry
        {
            public ModPackage Pkg;
            public WeaponDesign D;
            public WeaponDef Def;
            public bool Installed, Testing, Session;
        }

        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        static readonly Queue<string> iconQueue = new Queue<string>();
        static int builtinCount = -1;
        static string pendingTest;
        static float pendingTestTime;

        /// <summary>What validation had to clamp the last time it ran (shown to the user).</summary>
        public static readonly List<string> LastNotes = new List<string>();

        /// <summary>Number of built-in weapons (every mod's catalog index is at or above this).</summary>
        public static int BuiltinCount => builtinCount >= 0 ? builtinCount : WeaponCatalog.All.Count;

        public static void Register()
        {
            if (builtinCount < 0) builtinCount = WeaponCatalog.All.Count;
            ModHub.Register(new ModKindHandler
            {
                Kind = ModKind.Weapon, Title = "Weapon", Icon = "gun", Advanced = false,
                OpenEditor = p => UIRoot.Push(new WeaponEditorScreen(p)),
                Validate = Validate,
                Install = p => Install(p),
                Uninstall = Uninstall,
            });
        }

        // ------------------------------------------------------------------ queries

        public static bool IsMod(WeaponDef d) => d != null && d.Id != null && d.Id.StartsWith(Prefix);
        public static string DefId(string modId) => Prefix + modId;
        static Entry Get(WeaponDef d) => d != null && d.Id != null && entries.TryGetValue(d.Id, out var e) ? e : null;
        public static WeaponDesign Design(WeaponDef d) => Get(d)?.D;
        public static ModPackage Package(WeaponDef d) => Get(d)?.Pkg;
        public static WeaponDef DefFor(string modId) => entries.TryGetValue(DefId(modId), out var e) ? e.Def : null;

        /// <summary>Modded weapons can't be used in multiplayer unless the host allows them.</summary>
        public static bool Usable(WeaponDef d) => !IsMod(d) || !NetState.Active || ModNet.Allowed;

        /// <summary>Shown in the spawn menu / give lists: installed (or being tested) and usable here.</summary>
        public static bool Listed(WeaponDef d)
        {
            if (!IsMod(d)) return true;
            var e = Get(d);
            return e != null && (e.Installed || e.Testing) && Usable(d);
        }

        /// <summary>HUD / name suffix for modded weapons.</summary>
        public static string Suffix(WeaponDef d) => IsMod(d) ? "  [" + Tag + "]" : "";

        // ------------------------------------------------------------------ handler

        public static string Validate(ModPackage p)
        {
            LastNotes.Clear();
            if (p.Kind != ModKind.Weapon) return "Not a weapon mod.";
            if (!string.IsNullOrEmpty(p.script)) return "Weapon Editor mods can't contain scripts (use Advanced Modding).";
            var err = WeaponRules.Parse(p.data, out var d, LastNotes);
            if (err != null) return err;
            p.data = JsonUtility.ToJson(d); // store the clamped values
            return null;
        }

        static WeaponDesign Parse(ModPackage p)
        {
            WeaponRules.Parse(p.data, out var d, null);
            return d;
        }

        public static void Install(ModPackage p)
        {
            var d = Parse(p);
            if (d == null) return;
            var e = Apply(p, d);
            e.Installed = true;
        }

        public static void Uninstall(ModPackage p)
        {
            if (!entries.TryGetValue(DefId(p.id), out var e)) return;
            e.Installed = false;
            // The def stays in the catalog (indices never shift) but is hidden; take it out of the player's hands.
            RemoveFromInventory(e.Def);
        }

        static void RemoveFromInventory(WeaponDef def)
        {
            var pc = PlayerController.Instance;
            if (pc == null || pc.Inventory == null) return;
            for (int i = 0; i < Inventory.SlotCount; i++)
                if (pc.Inventory.Slots[i] != null && pc.Inventory.Slots[i].Def == def) pc.Inventory.Remove(i);
        }

        /// <summary>Creates or updates the catalog entry for a (validated) weapon mod.</summary>
        static Entry Apply(ModPackage p, WeaponDesign d)
        {
            if (builtinCount < 0) builtinCount = WeaponCatalog.All.Count;
            var id = DefId(p.id);
            if (!entries.TryGetValue(id, out var e))
            {
                e = new Entry { Def = new WeaponDef { Id = id } };
                entries[id] = e;
                WeaponCatalog.All.Add(e.Def);
            }
            e.Pkg = p;
            e.D = d;
            Fill(e.Def, p, d);
            iconQueue.Enqueue(id);
            return e;
        }

        /// <summary>Multiplayer: the host's validated copy of a mod for this session (overrides the local copy).</summary>
        public static WeaponDef ApplySession(ModPackage p)
        {
            var d = Parse(p);
            if (d == null) return null;
            var e = Apply(p, d);
            e.Session = true;
            return e.Def;
        }

        /// <summary>After leaving a multiplayer session: put back the local copies of mods the host had overridden.</summary>
        public static void EndSession()
        {
            foreach (var e in entries.Values)
            {
                if (!e.Session) continue;
                e.Session = false;
                var local = ModStore.FindInstalled(e.Pkg.id);
                if (local != null) { var d = Parse(local); if (d != null) Apply(local, d); }
            }
        }

        static void Fill(WeaponDef def, ModPackage p, WeaponDesign d)
        {
            def.Name = string.IsNullOrEmpty(p.name) ? "Modded weapon" : p.name;
            def.Description = (string.IsNullOrEmpty(p.description) ? "" : p.description + " ") + "(Modded by " + p.author + ")";
            def.Glyph = d.Glyph;
            def.WeightKg = d.weight;
            if (d.melee)
            {
                def.Kind = WeaponKind.Melee;
                def.Damage = d.meleeDamage;
                def.Force = d.meleeType == 1 ? 45f : 15f;
                def.HipPos = new Vector3(0.15f, -0.19f, 0.32f);
                def.Mag = 1; def.Pellets = 1; def.Auto = false; def.ShellReload = false;
                def.FireSound = null;
                return;
            }
            def.Kind = WeaponKind.Firearm;
            def.Damage = d.damage; def.Rpm = d.rpm; def.Pellets = d.pellets;
            def.HipSpread = d.hipSpread; def.AimSpread = d.aimSpread; def.Recoil = d.recoilV; def.Range = d.range;
            def.Penetration = d.penetration; def.Force = d.force; def.Caliber = d.pellets > 1 ? 8.4f : d.damage > 60f ? 9f : 7f;
            def.Mag = d.magSize; def.CasingType = d.pellets > 1 ? 2 : d.damage > 45f ? 1 : 0;
            def.Auto = d.trigger == 1; def.ShellReload = d.reloadStyle == 1;
            def.ReloadTime = d.reloadTime; def.EmptyExtra = 0.4f;
            def.AimFov = d.aimFov; def.AimDistance = 0.18f; def.NoiseRadius = d.sound == 10 ? 30f : 100f;
            def.FireSound = d.FireSound;
            def.MagOut = "mag_out_rifle"; def.MagIn = def.ShellReload ? "shell_in" : "mag_in_rifle";
            def.Charge = d.trigger == 4 ? (d.pellets > 1 ? "pump_action" : "bolt_cycle") : "charging_handle";
            bool longGun = d.muzzlePos.z - d.gripPos.z > 0.38f;
            def.HipPos = longGun ? new Vector3(0.12f, -0.16f, 0.16f) : new Vector3(0.12f, -0.15f, 0.32f);
        }

        // ------------------------------------------------------------------ models

        /// <summary>Model-space centre of the right hand for guns (like the built-in pistols) and melee weapons.</summary>
        static readonly Vector3 GunGrip = new Vector3(0f, -0.045f, -0.065f), MeleeGrip = new Vector3(0f, 0f, -0.075f);

        public static WeaponModel BuildModel(string defId, int layer)
        {
            if (!entries.TryGetValue(defId, out var e)) return WeaponModels.Root("Modded", layer);
            return BuildModel(e.D, layer, e.Def.Name, false);
        }

        public static Material MaterialFor(WPart p)
        {
            switch (p.mat)
            {
                case 0: return Mats.LitMetal;
                case 3:
                {
                    // Quantised so a mod can't create thousands of materials.
                    var c = new Color(Mathf.Round(p.color.r * 8f) / 8f, Mathf.Round(p.color.g * 8f) / 8f, Mathf.Round(p.color.b * 8f) / 8f, 1f);
                    return Mats.Glow(c, 2.5f);
                }
                default: return Mats.Lit;
            }
        }

        static float Gloss(int mat) => mat == 0 ? 0.55f : mat == 1 ? 0.25f : mat == 2 ? 0.08f : 0.6f;

        /// <summary>Adds one primitive (centred on the origin, full extents = size) to a builder.</summary>
        public static void AddShape(MeshBuilder mb, int shape, Vector3 size)
        {
            switch (shape)
            {
                case 1: // cylinder along Z
                    mb.Push(Vector3.zero, Quaternion.identity, new Vector3(size.x, size.y, 1f));
                    mb.Cylinder(new Vector3(0, 0, -size.z * 0.5f), new Vector3(0, 0, size.z * 0.5f), 0.5f, 0.5f, 14);
                    mb.Pop();
                    break;
                case 2:
                    mb.Sphere(Vector3.zero, size * 0.5f, 10, 14);
                    break;
                case 3: // capsule along Z
                {
                    float m = Mathf.Max(0.001f, Mathf.Min(size.x, size.y));
                    float r = m * 0.5f, half = Mathf.Max(0.0002f, size.z * 0.5f - r);
                    mb.Push(Vector3.zero, Quaternion.identity, new Vector3(size.x / m, size.y / m, 1f));
                    mb.Capsule(new Vector3(0, 0, -half), new Vector3(0, 0, half), r, r, 12, 3);
                    mb.Pop();
                    break;
                }
                case 4: // wedge / ramp: right triangle in YZ, extruded along X
                    mb.Push(Vector3.zero, Quaternion.Euler(0f, 90f, 0f), new Vector3(size.z, size.y, size.x));
                    mb.Prism(new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, 0.5f) }, 1f, Vector3.zero);
                    mb.Pop();
                    break;
                case 5: Tube(mb, size, 0.68f, 16); break;
                default: mb.Box(Vector3.zero, size); break;
            }
        }

        static void Tube(MeshBuilder mb, Vector3 size, float inner, int seg)
        {
            float rx = size.x * 0.5f, ry = size.y * 0.5f, hz = size.z * 0.5f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                var d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                var o0 = new Vector3(d0.x * rx, d0.y * ry, 0f); var o1 = new Vector3(d1.x * rx, d1.y * ry, 0f);
                var n0 = o0 * inner; var n1 = o1 * inner;
                var mid = (d0 + d1).normalized;
                var fz = new Vector3(0, 0, hz); var bz = new Vector3(0, 0, -hz);
                mb.Quad(mb.Vert(o0 + bz), mb.Vert(o1 + bz), mb.Vert(o1 + fz), mb.Vert(o0 + fz), mid);            // outer wall
                mb.Quad(mb.Vert(n0 + bz), mb.Vert(n1 + bz), mb.Vert(n1 + fz), mb.Vert(n0 + fz), -mid);           // inner wall
                mb.Quad(mb.Vert(o0 + fz), mb.Vert(o1 + fz), mb.Vert(n1 + fz), mb.Vert(n0 + fz), Vector3.forward); // front ring
                mb.Quad(mb.Vert(o0 + bz), mb.Vert(o1 + bz), mb.Vert(n1 + bz), mb.Vert(n0 + bz), Vector3.back);    // back ring
            }
        }

        /// <summary>
        /// Builds the model. In game the whole design is shifted so the hold point sits where a hand holds the built-in
        /// guns; the editor (<paramref name="editor"/>) keeps the design's own coordinates.
        /// </summary>
        public static WeaponModel BuildModel(WeaponDesign d, int layer, string name, bool editor)
        {
            var m = WeaponModels.Root(string.IsNullOrEmpty(name) ? "Modded" : name, layer);
            if (d == null || d.parts == null) return m;
            var off = editor ? Vector3.zero : (d.melee ? MeleeGrip : GunGrip) - d.gripPos;
            int n = Mathf.Min(d.parts.Count, WeaponRules.MaxParts);
            for (int i = 0; i < n; i++)
            {
                var p = d.parts[i];
                var mb = new MeshBuilder { Color = p.color, Gloss = Gloss(p.mat) };
                AddShape(mb, p.shape, p.size);
                var t = m.Piece("P" + i, mb, MaterialFor(p), p.pos + off);
                t.localRotation = Quaternion.Euler(p.rot);
                if (p.mat == 3) { var r = t.GetComponent<Renderer>(); if (r != null) m.GlowRenderers.Add(r); }
                if (i == d.magPart) { m.Mag = t; m.MagRest = t.localPosition; m.MagMesh = t.GetComponent<MeshFilter>().sharedMesh; }
                if (i == d.slidePart) { m.Slide = t; m.SlideRest = t.localPosition; }
            }
            if (d.melee)
            {
                m.GripR = m.Anchor("GripR", d.gripPos + off + new Vector3(0.02f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f));
                m.SupportL = m.Anchor("SupportL", d.gripPos + off + new Vector3(-0.03f, -0.03f, 0f), Quaternion.Euler(30f, 20f, 65f));
                m.Muzzle = m.Anchor("Tip", d.muzzlePos + off, Quaternion.identity);
                m.Sight = m.Anchor("Sight", d.gripPos + off + new Vector3(0, 0.05f, 0), Quaternion.identity);
                return m;
            }
            m.GripR = m.Anchor("GripR", d.gripPos + off + new Vector3(0.024f, 0f, 0f), Quaternion.Euler(15f, 0f, -90f));
            bool forward = d.supportPos.z - d.gripPos.z > 0.12f;
            var sp = d.useSupport ? d.supportPos : d.gripPos + new Vector3(-0.03f, -0.02f, 0.04f);
            m.SupportL = m.Anchor("SupportL", sp + off + new Vector3(-0.004f, 0f, 0f), forward && d.useSupport ? Quaternion.Euler(-15f, 0f, 165f) : Quaternion.Euler(30f, 20f, 65f));
            m.Muzzle = m.Anchor("Muzzle", d.muzzlePos + off, Quaternion.Euler(d.muzzleRot));
            m.Eject = m.Anchor("Eject", d.ejectPos + off, Quaternion.identity);
            if (m.Mag != null) m.MagWell = m.Anchor("MagWell", m.MagRest + Vector3.down * 0.05f, Quaternion.identity);
            if ((d.sightFront - d.sightRear).sqrMagnitude > 0.0004f && d.sightFront.z > d.sightRear.z)
            {
                m.SightRear = m.Anchor("SightRear", d.sightRear + off, Quaternion.identity);
                m.SightFront = m.Anchor("SightFront", d.sightFront + off, Quaternion.identity);
            }
            else m.Sight = m.Anchor("Sight", d.sightRear + off, Quaternion.identity);
            return m;
        }

        // ------------------------------------------------------------------ weapons

        public static Weapon CreateWeapon(WeaponDef def)
        {
            if (!IsMod(def)) return null;
            var d = Design(def);
            if (d != null && d.melee) return new ModMelee { Def = def };
            return new ModFirearm { Def = def };
        }

        /// <summary>Fires one bullet/pellet of a modded gun (local shooter, or the host/clients replaying a remote shot).</summary>
        public static void FireRound(WeaponDef def, WeaponDesign d, Vector3 origin, Vector3 dir, Vector3 muzzle, int index, object src, bool authority, int mask = 0)
        {
            if (d == null) { Ballistics.Fire(origin, dir, def, src, muzzle, DamageType.Bullet, true, authority, mask); return; }
            if (d.projectile == 0)
            {
                var end = Ballistics.Fire(origin, dir, def, src, muzzle, DamageType.Bullet, false, authority, mask);
                if ((Rules.BulletTracers || d.alwaysTracers) && (def.Pellets == 1 || index % 3 == 0)) Tracers.Spawn(muzzle, end, d.tracer);
                return;
            }
            ModBolt.Launch(def, d, origin, muzzle, dir, src, authority, mask);
        }

        /// <summary>Replays a remote player's modded shot (host: real damage with the host's copy; clients: visuals).</summary>
        public static void FireRemote(WeaponDef def, Vector3 origin, List<Vector3> dirs, object src, Vector3 muzzle, bool authority, int mask)
        {
            var d = Design(def);
            if (d != null && d.melee) return;
            int n = Mathf.Min(dirs.Count, d != null ? d.pellets : 16);
            for (int i = 0; i < n; i++) FireRound(def, d, origin, dirs[i], muzzle, i, src, authority, mask);
        }

        // ------------------------------------------------------------------ test fire + icons

        /// <summary>The editor's TEST button: equips the weapon now, or loads a test range first.</summary>
        public static void Test(ModPackage p)
        {
            var copy = ModStore.Copy(p);
            if (copy == null) return;
            var err = ModStore.Validate(copy);
            if (err != null) { Toasts.Show("TEST", err, UI.Bad); return; }
            var d = Parse(copy);
            var e = Apply(copy, d);
            e.Testing = true;
            pendingTest = e.Def.Id;
            pendingTestTime = Time.realtimeSinceStartup;
            var gm = GameManager.I;
            if (gm == null) return;
            if (gm.State != GameState.Playing)
            {
                var map = MapCatalog.All.Find(m => m.Id == "flatgrass") ?? MapCatalog.All[0];
                gm.StartMap(map, new MapOptions { Time = TimeOfDay.Day, StartingNpcs = 4, Loadout = 0 });
            }
        }

        public static void Tick()
        {
            if (pendingTest != null)
            {
                var gm = GameManager.I;
                var pc = PlayerController.Instance;
                if (gm != null && gm.State == GameState.Playing && pc != null && pc.Inventory != null && !UIRoot.AnyBlocking)
                {
                    var id = pendingTest;
                    pendingTest = null;
                    var def = WeaponCatalog.Get(id);
                    if (def != null)
                    {
                        if (!Usable(def)) Toasts.Show("TEST", "Modded weapons are turned off on this server.", UI.Warn);
                        else
                        {
                            // Rebuild it if it's held (the design may have changed).
                            for (int i = 0; i < Inventory.SlotCount; i++) if (pc.Inventory.Slots[i] != null && pc.Inventory.Slots[i].Def == def) pc.Inventory.Remove(i);
                            pc.Inventory.Give(id);
                            GameEvents.ShowToast("Testing " + def.Name + " [" + Tag + "]");
                        }
                    }
                }
                else if (Time.realtimeSinceStartup - pendingTestTime > 120f) pendingTest = null;
            }
            if (iconQueue.Count > 0 && SoundBank.Ready)
            {
                var id = iconQueue.Dequeue();
                try
                {
                    var model = BuildModel(id, 31);
                    IconFactory.Render("weapon_" + id, model.gameObject, new Vector3(-1f, -0.15f, 0.05f), 160, 1.05f);
                }
                catch (System.Exception ex) { Debug.LogWarning("[Mods] icon failed: " + ex.Message); }
            }
        }
    }

    // ====================================================================== guns

    /// <summary>A modded gun: the stock Firearm (reloads, ADS, recoil, casings) plus the editor's trigger types,
    /// projectile types, pitched sound, extra recoil, aim speed and move speed.</summary>
    public sealed class ModFirearm : Firearm
    {
        int burstLeft;
        float charge, cycleAt = -1f;
        bool wasHeld, warned;
        WeaponDesign D => ModWeapons.Design(Def);

        public override float MoveSpeedMul => D != null ? D.moveSpeed : 1f;

        public override string ModeText
        {
            get
            {
                var d = D;
                if (d == null) return base.ModeText;
                switch (d.trigger)
                {
                    case 2: return "BURST x" + d.burstCount;
                    case 3: return charge > 0f ? "CHARGE " + Mathf.RoundToInt(Mathf.Clamp01(charge) * 100f) + "%" : "CHARGE";
                    case 4: return d.pellets > 1 ? "PUMP" : "BOLT";
                    default: return base.ModeText;
                }
            }
        }

        protected override void OnAttach()
        {
            base.OnAttach();
            var d = D;
            if (d != null) Mods.AimSpeedMul = d.aimSpeed;
        }

        public override void Tick(InputFrame f, float dt)
        {
            var d = D;
            if (!ModWeapons.Usable(Def))
            {
                if ((f.Fire || f.Reload) && !warned) { warned = true; GameEvents.ShowToast("Modded weapons are turned off on this server."); }
                f.Fire = f.FireHeld = f.Reload = false;
            }
            else if (d != null)
            {
                switch (d.trigger)
                {
                    case 2: // burst
                        if (f.Fire && burstLeft <= 0 && Ammo > 0 && !Reloading) burstLeft = d.burstCount;
                        if (Ammo <= 0 || Reloading) burstLeft = 0;
                        if (burstLeft > 0) f.Fire = true;
                        break;
                    case 3: // charge: hold to charge, release when full to fire
                    {
                        bool held = f.FireHeld && Ammo > 0 && !Reloading && !Player.Sprinting;
                        bool pressed = f.Fire;
                        f.Fire = false;
                        if (held)
                        {
                            if (charge <= 0f) AudioManager.Play("charging_handle", Eye, 0.35f, 1.4f, 0f);
                            charge += dt / d.chargeTime;
                            if (charge >= 1f) CameraFX.Shake(0.004f);
                        }
                        else if (wasHeld)
                        {
                            if (charge >= 1f) f.Fire = true;
                            charge = 0f;
                        }
                        else if (Ammo <= 0 && pressed) f.Fire = true; // dry fire / auto reload
                        wasHeld = held;
                        f.FireHeld = false;
                        break;
                    }
                }
            }
            if (cycleAt > 0f && Time.time >= cycleAt)
            {
                cycleAt = -1f;
                AudioManager.Play(Def.Charge, Eye, 0.6f, 1f, 0f);
            }
            // The stock Firearm plays Def.FireSound unpitched; mute it for this tick and play our own in OnShot.
            var snd = Def.FireSound;
            Def.FireSound = "mod_silent";
            try { base.Tick(f, dt); }
            finally { Def.FireSound = snd; }
            if (d != null && !d.useSupport && !Reloading)
            {
                Vm.LeftOnAnchor = false;
                Vm.LeftPos = new Vector3(-0.17f, -0.2f, 0.33f);
                Vm.LeftRot = Quaternion.Euler(-12f, 10f, 72f);
            }
            if (d != null && d.trigger == 3 && charge > 0f && Model != null)
                Vm.TargetPos += Random.insideUnitSphere * 0.0015f * Mathf.Clamp01(charge);
        }

        protected override void OnShot()
        {
            var d = D;
            if (d == null) return;
            if (Def.FireSound != null) AudioManager.Play(Def.FireSound, Eye, 1f, d.pitch * Random.Range(0.97f, 1.03f), 0f, 1.5f, 80f, AudioBus.Sfx);
            if (d.recoilH > 0f || d.kick > 0f)
            {
                float a = Mathf.Lerp(1f, 0.6f, AimWeight);
                CameraFX.Punch(0f, Random.Range(-1f, 1f) * d.recoilH * 0.6f * a);
                Vm.AddRecoil(new Vector3(0f, 0f, -0.01f * d.kick), new Vector3(-5f * d.kick * a, Random.Range(-5f, 5f) * d.recoilH, 0f));
            }
            if (d.trigger == 2 && burstLeft > 0) burstLeft--;
            if (d.trigger == 4 && Ammo > 0) cycleAt = Time.time + Mathf.Min(0.25f, 30f / Mathf.Max(1f, Def.Rpm));
            ModNet.NoteUsed(Def);
        }

        protected override void FireRound(Vector3 origin, Vector3 dir, Vector3 muzzle, int index, bool authority) =>
            ModWeapons.FireRound(ShotDef ?? Def, D, origin, dir, muzzle, index, Player, authority);

        protected override void OnEquip()
        {
            base.OnEquip();
            ModNet.Share(Def);
        }
    }

    /// <summary>Visible slow bolt / explosive round. Moves every frame with a raycast from its last position.</summary>
    public sealed class ModBolt : MonoBehaviour
    {
        WeaponDef def;
        WeaponDesign d;
        object src;
        bool authority;
        int mask;
        Vector3 vel;
        float life, gravity;

        public static void Launch(WeaponDef def, WeaponDesign d, Vector3 origin, Vector3 muzzle, Vector3 dir, object src, bool authority, int mask)
        {
            var go = new GameObject("ModBolt");
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, true);
            // Start at the muzzle but aim at the point the eye ray is heading for (no parallax at range).
            var target = origin + dir * 40f;
            if (Physics.Raycast(origin, dir, out var hit, 40f, mask == 0 ? Layers.ShootMask : mask, QueryTriggerInteraction.Ignore)) target = hit.point;
            var start = Vector3.Distance(muzzle, origin) < 3f ? muzzle : origin;
            var fdir = (target - start).sqrMagnitude > 0.01f ? (target - start).normalized : dir;
            go.transform.position = start;
            go.transform.rotation = Quaternion.LookRotation(fdir);
            var mb = new MeshBuilder { Color = d.tracer };
            bool boom = d.projectile == 2;
            mb.Sphere(Vector3.zero, boom ? new Vector3(0.05f, 0.05f, 0.09f) : new Vector3(0.025f, 0.025f, 0.16f), 6, 8);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Bolt");
            var mr = go.AddComponent<MeshRenderer>();
            var c = new Color(Mathf.Round(d.tracer.r * 8f) / 8f, Mathf.Round(d.tracer.g * 8f) / 8f, Mathf.Round(d.tracer.b * 8f) / 8f, 1f);
            mr.sharedMaterial = Mats.Glow(c, 3f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var b = go.AddComponent<ModBolt>();
            b.def = def; b.d = d; b.src = src; b.authority = authority; b.mask = mask == 0 ? Layers.ShootMask : mask;
            b.vel = fdir * d.projectileSpeed;
            b.gravity = boom ? 4f : 0f;
            b.life = Mathf.Min(8f, def.Range / Mathf.Max(5f, d.projectileSpeed));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            var pos = transform.position;
            vel += Vector3.down * gravity * dt;
            var step = vel * dt;
            if (Physics.Raycast(pos, step.normalized, out var hit, step.magnitude, mask, QueryTriggerInteraction.Ignore)) { Impact(hit.point, step.normalized); return; }
            transform.position = pos + step;
            if (vel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(vel);
            if (life <= 0f) { if (d.projectile == 2) Impact(transform.position, vel.normalized); else Destroy(gameObject); }
        }

        void Impact(Vector3 point, Vector3 dir)
        {
            if (d.projectile == 2)
            {
                if (authority) Explosion.Create(point - dir * 0.1f, d.explosionRadius, def.Damage, src, 1f);
            }
            else
            {
                // Short hitscan from just before the impact: same damage / penetration / gore as bullets.
                Ballistics.BeginShot();
                Ballistics.Fire(point - dir * 0.3f, dir, def, src, point, DamageType.Bullet, false, authority, mask);
                if (authority) Ballistics.EndShot(dir);
                FX.Flash(point, d.tracer, 3f, 1.5f, 0.06f);
            }
            Destroy(gameObject);
        }
    }

    // ====================================================================== melee

    /// <summary>A modded knife / bat: LMB swing, RMB heavy swing. Blades can cut limbs, blunt weapons knock back.</summary>
    public sealed class ModMelee : Weapon
    {
        float t = -1f;
        bool heavy, hitDone;
        int side = 1;
        WeaponDesign D => ModWeapons.Design(Def);
        bool Blade => D == null || D.meleeType == 0;

        public override bool Busy => t >= 0f;
        public override float MoveSpeedMul => D != null ? D.moveSpeed : 1f;
        public override string ModeText => Blade ? "BLADE" : "BLUNT";

        protected override void OnEquip()
        {
            Vm.Right.SetPose(Blade ? HandPose.Knife : HandPose.Hold);
            Vm.Left.SetPose(HandPose.Fist);
            t = -1f;
            ModNet.Share(Def);
        }

        protected override void OnHolster()
        {
            Vm.Right.SetPose(HandPose.Relaxed);
            Vm.Left.SetPose(HandPose.Relaxed);
        }

        public override void Tick(InputFrame f, float dt)
        {
            Vm.ResetHands();
            var d = D;
            float speed = d != null ? d.meleeSpeed : 1f;
            bool usable = ModWeapons.Usable(Def);
            if (t < 0f && usable && !Player.Sprinting)
            {
                if (f.Fire) Begin(false);
                else if (f.AimPressed) Begin(true);
            }
            var idle = Def.HipPos + new Vector3(0, Mathf.Sin(Time.time * 1.7f) * 0.003f, 0);
            var idleRot = Quaternion.Euler(-8f, -12f, -12f);
            var pos = idle;
            var rot = idleRot;
            if (t >= 0f)
            {
                float dur = (Blade ? 0.5f : 0.75f) * (heavy ? 1.4f : 1f) / speed;
                t += dt / dur;
                Vector3 wind, through; Quaternion windRot, throughRot;
                if (Blade)
                {
                    wind = new Vector3(0.24f * side, -0.04f, 0.28f); through = new Vector3(-0.16f * side, -0.24f, 0.46f);
                    windRot = Quaternion.Euler(-25f, 35f * side, -70f * side); throughRot = Quaternion.Euler(35f, -55f * side, -40f * side);
                }
                else
                {
                    wind = new Vector3(0.1f, 0.12f, 0.15f); through = new Vector3(-0.02f, -0.28f, 0.42f);
                    windRot = Quaternion.Euler(-70f, 10f, -20f); throughRot = Quaternion.Euler(60f, -10f, -10f);
                }
                if (t < 0.3f) { float k = Ease(t / 0.3f); pos = Vector3.Lerp(idle, wind, k); rot = Quaternion.Slerp(idleRot, windRot, k); }
                else if (t < 0.55f) { float k = Ease((t - 0.3f) / 0.25f); pos = Vector3.Lerp(wind, through, k); rot = Quaternion.Slerp(windRot, throughRot, k); if (!hitDone && k > 0.45f) { hitDone = true; Hit(); } }
                else { float k = Ease((t - 0.55f) / 0.45f); pos = Vector3.Lerp(through, idle, k); rot = Quaternion.Slerp(throughRot, idleRot, k); }
                if (t >= 1f) t = -1f;
            }
            else TickInspect(f, dt, ref pos, ref rot, 1.8f);
            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
            Vm.LeftOnAnchor = false;
            Vm.LeftPos = new Vector3(-0.17f, -0.2f, 0.33f);
            Vm.LeftRot = Quaternion.Euler(-12f, 10f, 72f);
        }

        void Begin(bool isHeavy)
        {
            heavy = isHeavy;
            t = 0f;
            hitDone = false;
            inspectT = -1f;
            side = -side;
            Swing(Blade ? "whoosh_small" : "sledge_swing", heavy ? 0.8f : 0.6f);
        }

        void Hit()
        {
            var d = D;
            float reach = d != null ? d.meleeReach : 1.6f;
            if (!MeleeTrace(Aim, reach, 0.08f, out var hit)) return;
            float dmg = Def.Damage * (heavy ? 1.5f : 1f);
            var dir = Aim;
            var type = Blade ? DamageType.Slash : DamageType.Blunt;
            var part = hit.collider.GetComponent<BodyPart>();
            if (part != null)
            {
                part.TakeDamage(new DamageInfo { Amount = dmg, Force = dir * Def.Force * (heavy ? 1.5f : 1f), Point = hit.point, Normal = hit.normal, Direction = dir, Type = type, Source = Player, Weapon = Def.Name, Penetration = Blade ? 0.4f : 0.1f });
                AudioManager.Play(Blade ? "slice" : "sledge_flesh", hit.point, 0.9f, Random.Range(0.92f, 1.08f));
                if (Blade && part.CanSever && Settings.Dismemberment && part.Doll != null
                    && (Rules.AlwaysDismember || part.Health <= part.MaxHealth * 0.4f || Random.value < (heavy ? 0.35f : 0.18f)))
                {
                    part.Doll.Dismember.Slice(part, hit.point, Vector3.Cross(CamT.right * side, dir).normalized);
                    AudioManager.Play("dismember", hit.point, 0.9f);
                }
                else if (!Blade && heavy && part.IsHead) AudioManager.Play("bone_crack", hit.point, 0.8f);
                CameraFX.Punch(0.3f, -side * 0.4f);
            }
            else
            {
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Amount = dmg * 0.6f, Force = dir * Def.Force, Point = hit.point, Normal = hit.normal, Direction = dir, Type = type, Source = Player, Weapon = Def.Name });
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic && NetState.Authority) hit.rigidbody.AddForceAtPosition(dir * Def.Force * 0.4f, hit.point, ForceMode.Impulse);
                AudioManager.Play(Blade ? "knife_hit_hard" : "sledge_world", hit.point, 0.7f, Random.Range(0.9f, 1.1f));
                FX.Sparks(hit.point, hit.normal, 5, 4f);
                t = Mathf.Max(t, 0.55f);
            }
            CameraFX.Shake(0.12f);
            GameEvents.EmitNoise(hit.point, 5f, 0.4f, Player);
        }
    }
}
