using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Content Update weapons (16): crossbow, flamethrower, nail gun, tranquilizer, laser cutter, freeze ray, harpoon
    /// chain gun, sentry turret, throwing knives, shuriken, boomerang blade, flail, scythe, nail board, giant hammer, spear.
    /// Appended to WeaponCatalog after the Arsenal pack (append only: the index travels over the network).
    /// </summary>
    public static class ContentPack
    {
        static readonly List<WeaponDef> defs = new List<WeaponDef>();
        static readonly Dictionary<string, MeleeSpec> melee = new Dictionary<string, MeleeSpec>();
        static readonly Dictionary<string, GunSpec> guns = new Dictionary<string, GunSpec>();
        static bool defined;

        public static readonly string[] GunIds = { "crossbow", "flamer", "nailgun", "tranq", "laser", "freezeray", "harpoon_cg" };

        static WeaponDef Long(string id, string name, string glyph, string desc) => new WeaponDef
        {
            Id = id, Name = name, Glyph = glyph, Description = desc, Kind = WeaponKind.Firearm,
            HipPos = new Vector3(0.12f, -0.16f, 0.2f), AimFov = 0.75f, AimDistance = 0.17f, MagOut = "mag_out_rifle", MagIn = "mag_in_rifle", Charge = "charging_handle",
        };

        static void Define()
        {
            if (defined) return;
            defined = true;
            WeaponDef d;
            // ---------------- guns
            d = Long("crossbow", "Crossbow", "rifle", "Fires heavy bolts that go through bodies and pin them to walls. One bolt, quick reload.");
            d.Damage = 110; d.Rpm = 60; d.Mag = 1; d.HipSpread = 1.2f; d.AimSpread = 0.05f; d.Recoil = 2f; d.ReloadTime = 1.2f; d.ShellReload = true; d.FireSound = "crossbow"; d.MagIn = "shell_in"; d.MagOut = null; d.NoiseRadius = 20f; d.WeightKg = 3f;
            Add(d, new GunSpec { Bore = 0.05f, RecBack = -0.1f, RecFront = 0.3f, RecH = 0.05f, RecW = 0.04f, Muzzle = 0.36f, BarrelR = 0.006f, Guard = GunGuard.Wood, GuardStart = 0.05f, GuardEnd = 0.3f,
                Stock = GunStock.Wood, StockLen = 0.3f, Mag = GunMag.Shell, Body = GunKit.DarkWood, Furniture = GunKit.Wood, Metal = GunKit.Gunmetal });

            d = Long("flamer", "Flamethrower", "fire", "Hold LMB: a jet of fire that sets Goredolls alight. They burn, panic and char black. 200 fuel.");
            d.Damage = 4; d.Rpm = 900; d.Auto = true; d.Mag = 200; d.HipSpread = 0f; d.AimSpread = 0f; d.Recoil = 0.05f; d.ReloadTime = 2.6f; d.FireSound = "flamer"; d.Range = 9f; d.NoiseRadius = 40f; d.WeightKg = 9f;
            d.HipPos = new Vector3(0.15f, -0.22f, 0.26f);
            Add(d, new GunSpec { Bore = 0.06f, RecBack = -0.12f, RecFront = 0.25f, RecH = 0.08f, RecW = 0.05f, Muzzle = 0.55f, BarrelR = 0.016f, Guard = GunGuard.Shroud, GuardStart = 0.25f, GuardEnd = 0.45f, GuardR = 0.028f,
                Stock = GunStock.Heavy, StockLen = 0.2f, Mag = GunMag.Box, MagZ = 0.05f, MagLen = 0.12f, MagD = 0.1f, Body = GunKit.Olive, Furniture = GunKit.Polymer, Metal = GunKit.Gunmetal });

            d = Long("nailgun", "Nail Gun", "smg", "Construction nail gun. Fast nails that bleed and nail limbs to walls. 50 nails.");
            d.Damage = 16; d.Rpm = 480; d.Auto = true; d.Mag = 50; d.HipSpread = 1.6f; d.AimSpread = 0.6f; d.Recoil = 0.5f; d.ReloadTime = 1.7f; d.FireSound = "nailgun"; d.Range = 40f; d.NoiseRadius = 30f; d.WeightKg = 2.4f; d.Glyph = "smg";
            d.HipPos = new Vector3(0.12f, -0.15f, 0.3f);
            Add(d, new GunSpec { Frame = GunFrame.Pistol, RecW = 0.05f, SlideLen = 0.26f, SlideH = 0.07f, SlideZ = 0.1f, GripAngle = 6f, BarrelR = 0.006f, MagLen = 0.1f,
                Body = new Color(0.95f, 0.65f, 0.08f), Metal = GunKit.DarkSteel, Furniture = GunKit.Polymer });

            d = Long("tranq", "Tranquilizer", "gun", "Fires sedative darts: Goredolls fall asleep for 25 seconds. 5 darts. Quiet.");
            d.Damage = 6; d.Rpm = 90; d.Mag = 5; d.HipSpread = 1.2f; d.AimSpread = 0.05f; d.Recoil = 0.8f; d.ReloadTime = 1.8f; d.FireSound = "tranq"; d.Range = 120f; d.NoiseRadius = 8f; d.WeightKg = 2.2f;
            Add(d, new GunSpec { Bore = 0.05f, RecBack = -0.06f, RecFront = 0.18f, RecH = 0.05f, RecW = 0.028f, Muzzle = 0.55f, BarrelR = 0.007f, Guard = GunGuard.Wood, GuardStart = 0.18f, GuardEnd = 0.4f,
                Stock = GunStock.Wood, StockLen = 0.28f, Mag = GunMag.Tube, Scope = true, Body = GunKit.Gunmetal, Furniture = GunKit.Wood, MetalBody = true });

            d = Long("laser", "Laser Cutter", "bolt", "Hold LMB: a cutting beam. Keep it on a limb and it slices clean through. Burns everything it touches. Battery 100.");
            d.Damage = 9; d.Rpm = 1200; d.Auto = true; d.Mag = 100; d.HipSpread = 0f; d.AimSpread = 0f; d.Recoil = 0.02f; d.ReloadTime = 2f; d.FireSound = "laser_zap"; d.Range = 30f; d.NoiseRadius = 20f; d.WeightKg = 3.5f;
            Add(d, new GunSpec { Bore = 0.06f, RecBack = -0.1f, RecFront = 0.22f, RecH = 0.08f, RecW = 0.045f, Muzzle = 0.42f, BarrelR = 0.012f, Guard = GunGuard.Shroud, GuardStart = 0.22f, GuardEnd = 0.38f, GuardR = 0.03f,
                Stock = GunStock.Skeleton, StockLen = 0.22f, Mag = GunMag.Box, MagLen = 0.08f, MagD = 0.06f, Body = new Color(0.9f, 0.92f, 0.95f), Furniture = new Color(0.15f, 0.2f, 0.3f), Metal = new Color(0.2f, 0.4f, 0.9f) });

            d = Long("freezeray", "Freeze Ray", "snow", "Hold LMB: freezes Goredolls solid. Frozen bodies fall like statues and shatter when hit. 60 cells.");
            d.Damage = 2; d.Rpm = 600; d.Auto = true; d.Mag = 60; d.HipSpread = 0f; d.AimSpread = 0f; d.Recoil = 0.02f; d.ReloadTime = 2.2f; d.FireSound = "freeze_ray"; d.Range = 25f; d.NoiseRadius = 20f; d.WeightKg = 4f;
            Add(d, new GunSpec { Bore = 0.06f, RecBack = -0.1f, RecFront = 0.24f, RecH = 0.09f, RecW = 0.05f, Muzzle = 0.48f, BarrelR = 0.014f, Guard = GunGuard.Round, GuardStart = 0.24f, GuardEnd = 0.4f, GuardR = 0.03f,
                Stock = GunStock.Heavy, StockLen = 0.2f, Mag = GunMag.Drum, MagZ = 0.06f, Body = GunKit.Silver, Furniture = new Color(0.1f, 0.25f, 0.4f), Metal = new Color(0.3f, 0.8f, 1f), MetalBody = true, BodyGloss = 0.7f });

            d = Long("harpoon_cg", "Harpoon Chain Gun", "rifle", "Full-auto harpoons. 12 barbed spears per drum: impale, pin and string up whole crowds.");
            d.Damage = 80; d.Rpm = 260; d.Auto = true; d.Mag = 12; d.HipSpread = 1.6f; d.AimSpread = 0.4f; d.Recoil = 2.2f; d.ReloadTime = 3.2f; d.FireSound = "gun_harpoon"; d.Range = 120f; d.NoiseRadius = 60f; d.WeightKg = 9f;
            d.HipPos = new Vector3(0.15f, -0.22f, 0.26f);
            Add(d, new GunSpec { Bore = 0.065f, RecBack = -0.12f, RecFront = 0.28f, RecH = 0.1f, RecW = 0.06f, Muzzle = 0.62f, BarrelR = 0.02f, Guard = GunGuard.Shroud, GuardStart = 0.28f, GuardEnd = 0.5f, GuardR = 0.032f,
                Stock = GunStock.Heavy, StockLen = 0.22f, Mag = GunMag.Drum, MagZ = 0.09f, Bipod = true, Body = GunKit.DarkSteel, Furniture = GunKit.Polymer, MetalBody = true });

            // ---------------- tools / throwables
            defs.Add(new WeaponDef { Id = "turret", Name = "Sentry Turret", Kind = WeaponKind.Explosive, Glyph = "target", Description = "LMB places an automatic minigun turret that shreds every Goredoll it can see. Place up to 4.", Mag = 4, HipPos = new Vector3(0.15f, -0.2f, 0.3f), WeightKg = 2f });
            defs.Add(new WeaponDef { Id = "tknife", Name = "Throwing Knives", Kind = WeaponKind.Explosive, Glyph = "knife", Description = "LMB throws a knife that sticks into whatever it hits. Carry 8. Pick them back up by walking over them.", Damage = 48, Mag = 8, HipPos = new Vector3(0.15f, -0.17f, 0.32f), WeightKg = 0.2f });
            defs.Add(new WeaponDef { Id = "shuriken", Name = "Shuriken", Kind = WeaponKind.Explosive, Glyph = "star", Description = "Throwing stars: fast, spinning, every hit bleeds and some cut fingers and limbs. Carry 12.", Damage = 24, Mag = 12, HipPos = new Vector3(0.15f, -0.17f, 0.32f), WeightKg = 0.1f });
            defs.Add(new WeaponDef { Id = "boomerang", Name = "Boomerang Blade", Kind = WeaponKind.Explosive, Glyph = "refresh", Description = "Throw it: it curves out, slices through everything in its path and comes back to your hand.", Damage = 40, Mag = 1, HipPos = new Vector3(0.15f, -0.17f, 0.32f), WeightKg = 0.8f });

            // ---------------- melee (MeleeWeapon + MeleeKit heads)
            AddMelee("flail", "Flail", "hammer", "Spiked ball on a chain. Huge swings that tear flesh and send bodies flying. RMB = overhead.", 52, 50, 3f, true,
                new MeleeSpec { Head = MeleeHead.Flail, TwoHanded = false, SwingTime = 0.55f, Reach = 2.2f, Launch = 11f, CrushChance = 0.35f, Bleeds = true, HandleLen = 0.35f, HeadLen = 0.45f,
                    HeadColor = new Color(0.35f, 0.35f, 0.38f), HandleColor = GunKit.DarkWood, SwingSound = "melee_swing_heavy", FleshSound = "blunt_flesh", WorldSound = "blunt_world" });
            AddMelee("scythe", "Scythe", "cut", "A reaper's scythe. Wide, long sweeps that cut through several bodies at once.", 70, 18, 3.4f, true,
                new MeleeSpec { Head = MeleeHead.Scythe, Blade = true, TwoHanded = true, SwingTime = 0.6f, Reach = 2.7f, Pierce = 4, CutChance = 0.6f, HandleLen = 1.3f, HeadLen = 0.6f,
                    HeadColor = new Color(0.75f, 0.76f, 0.78f), HandleColor = GunKit.DarkWood, SwingSound = "melee_swing_heavy", FleshSound = "slice", WorldSound = "knife_hit_hard" });
            AddMelee("nailboard", "Nail Board", "bat", "A plank with rusty nails hammered through it. Every hit bleeds. RMB = big overhead.", 40, 32, 1.5f, true,
                new MeleeSpec { Head = MeleeHead.NailBoard, TwoHanded = true, SwingTime = 0.5f, Reach = 2.0f, Launch = 5f, CrushChance = 0.15f, Bleeds = true, HandleLen = 0.35f, HeadLen = 0.6f,
                    HeadColor = new Color(0.55f, 0.42f, 0.28f), HandleColor = new Color(0.4f, 0.3f, 0.2f), SwingSound = "melee_swing_heavy", FleshSound = "blunt_flesh", WorldSound = "wood_thock" });
            AddMelee("bighammer", "Giant Hammer", "hammer", "A ridiculous two-handed war hammer. Hold LMB to wind up: crushes skulls flat and launches bodies across the map.", 160, 120, 14f, true,
                new MeleeSpec { Head = MeleeHead.GiantHammer, TwoHanded = true, ChargeSwing = true, SwingTime = 0.8f, Reach = 2.4f, Launch = 22f, CrushChance = 0.85f, HandleLen = 1.0f, HeadLen = 0.4f,
                    HeadColor = new Color(0.3f, 0.3f, 0.33f), HandleColor = GunKit.DarkWood, SwingSound = "melee_swing_heavy", FleshSound = "sledge_flesh", WorldSound = "sledge_world" });
            AddMelee("spear", "Spear", "knife", "Long steel-tipped spear. LMB = fast jabs that go deep, RMB = full thrust that skewers through.", 55, 16, 2.2f, true,
                new MeleeSpec { Head = MeleeHead.Spear, Blade = true, TwoHanded = true, RmbStab = true, SwingTime = 0.42f, Reach = 2.6f, Pierce = 2, CutChance = 0.2f, HandleLen = 1.2f, HeadLen = 0.28f,
                    HeadColor = new Color(0.72f, 0.73f, 0.76f), HandleColor = new Color(0.45f, 0.32f, 0.2f), SwingSound = "melee_swing", FleshSound = "stab", WorldSound = "knife_hit_hard" });
        }

        static void Add(WeaponDef d, GunSpec s) { defs.Add(d); guns[d.Id] = s; }

        static void AddMelee(string id, string name, string glyph, string desc, float dmg, float force, float kg, bool two, MeleeSpec s)
        {
            defs.Add(new WeaponDef
            {
                Id = id, Name = name, Glyph = glyph, Description = desc, Damage = dmg, Force = force, WeightKg = kg, Kind = WeaponKind.Melee,
                HipPos = two ? new Vector3(0.14f, -0.2f, 0.3f) : new Vector3(0.15f, -0.17f, 0.32f),
            });
            melee[id] = s;
        }

        public static void AppendDefs(List<WeaponDef> all)
        {
            Define();
            foreach (var d in defs) if (!all.Exists(w => w.Id == d.Id)) all.Add(d);
        }

        public static bool Has(string id) { Define(); return defs.Exists(d => d.Id == id); }

        public static string Category(WeaponDef d)
        {
            Define();
            if (melee.ContainsKey(d.Id)) return "Melee";
            switch (d.Id)
            {
                case "nailgun": return "SMGs";
                case "tranq": case "crossbow": return "Rifles";
                case "flamer": case "laser": case "freezeray": case "harpoon_cg": return "Heavy";
                case "turret": return "Tools";
                case "tknife": case "shuriken": case "boomerang": return "Explosives";
            }
            return null;
        }

        public static Weapon Create(WeaponDef def)
        {
            if (def == null) return null;
            Define();
            if (!defs.Contains(def)) return null;
            switch (def.Id)
            {
                case "crossbow": return new CrossbowGun { Def = def };
                case "flamer": return new Flamethrower { Def = def };
                case "nailgun": return new NailGun { Def = def };
                case "tranq": return new TranqGun { Def = def };
                case "laser": return new BeamGun { Def = def, Freeze = false };
                case "freezeray": return new BeamGun { Def = def, Freeze = true };
                case "harpoon_cg": return new HarpoonChainGun { Def = def };
                case "turret": return new TurretTool { Def = def };
                case "tknife": return new ThrowWeapon { Def = def, Kind = ThrownBlade.K.Knife };
                case "shuriken": return new ThrowWeapon { Def = def, Kind = ThrownBlade.K.Shuriken };
                case "boomerang": return new ThrowWeapon { Def = def, Kind = ThrownBlade.K.Boomerang };
            }
            if (melee.TryGetValue(def.Id, out var ms)) return new MeleeWeapon { Def = def, Spec = ms };
            return null;
        }

        public static WeaponModel BuildModel(string id, int layer)
        {
            Define();
            var def = defs.Find(d => d.Id == id);
            if (def == null) return null;
            if (melee.TryGetValue(id, out var ms)) return MeleeKit.Build(def.Name, ms, layer);
            if (guns.TryGetValue(id, out var gs))
            {
                var m = GunKit.Build(def.Name, gs, layer);
                ContentModels.Decorate(id, m, gs);
                return m;
            }
            switch (id)
            {
                case "turret": return ContentModels.TurretTool(layer);
                case "tknife": return ContentModels.ThrowingKnife(layer);
                case "shuriken": return ContentModels.Shuriken(layer);
                case "boomerang": return ContentModels.Boomerang(layer);
            }
            return null;
        }
    }

    // ====================================================================== effects on Goredolls

    public static class ContentFx
    {
        public static void Sedate(Goredoll d)
        {
            if (d == null || d.IsDead) return;
            if (NetState.IsClient) { NetManager.I.RequestDollCommand(d, 20); return; }
            d.Vitals.Sedate(25f);
            if (d.Ragdoll != null) d.Ragdoll.Fall(3f);
            if (d.Voice != null) d.Voice.Say("v_moan", 0.5f);
        }

        public static void Freeze(Goredoll d)
        {
            if (d == null) return;
            if (NetState.IsClient) { NetManager.I.RequestDollCommand(d, 21); return; }
            Frozen.Apply(d);
        }

        public static void Ignite(Goredoll d, object src)
        {
            if (d == null) return;
            if (NetState.IsClient) { NetManager.I.RequestDollCommand(d, 22); return; }
            Burning.Apply(d, 9f, src);
        }

        /// <summary>Nails / pins a body part to the world at its current position.</summary>
        public static bool PinToWorld(BodyPart part)
        {
            if (part == null || part.Body == null || part.Body.isKinematic || part.Severed) return false;
            if (part.GetComponents<FixedJoint>().Length >= 1) return false;
            var j = part.gameObject.AddComponent<FixedJoint>();
            j.breakForce = 9000f; j.breakTorque = 9000f;
            return true;
        }
    }

    /// <summary>A Goredoll on fire: flames on its body, burns and chars parts, pain and panic.</summary>
    public sealed class Burning : MonoBehaviour
    {
        Goredoll d; float until, next; object src;

        public static void Apply(Goredoll d, float seconds, object src)
        {
            var b = d.GetComponent<Burning>();
            if (b == null) { b = d.gameObject.AddComponent<Burning>(); b.d = d; }
            b.until = Mathf.Max(b.until, Time.time + seconds);
            b.src = src;
        }

        void Update()
        {
            if (d == null || Time.time > until) { Destroy(this); return; }
            int k = 0;
            foreach (var part in d.Parts.Values)
            {
                if (part == null || part.Gibbed || (k++ + Time.frameCount) % 4 != 0) continue;
                if (Random.value < 0.35f) FX.Fire(part.transform.position + Random.insideUnitSphere * 0.08f, Vector3.up * 1.4f + Random.insideUnitSphere * 0.3f, 0.35f);
            }
            if (Time.time < next) return;
            next = Time.time + 0.3f;
            var list = new List<BodyPart>(d.Parts.Values);
            var p = list[Random.Range(0, list.Count)];
            if (p == null || p.Gibbed) return;
            GoreKit.Burn(p, 0.14f);
            if (!d.IsDead)
            {
                p.TakeDamage(new DamageInfo { Amount = 5f, Type = DamageType.Generic, Point = p.transform.position, Direction = Vector3.up, Normal = Vector3.up, Source = src, Weapon = "Fire" });
                d.Vitals.AddPain(9f);
                if (d.Brain != null) d.Brain.Fear = 1f;
                if (Random.value < 0.2f && d.Voice != null) d.Voice.Say("v_scream", 1f);
            }
        }
    }

    /// <summary>Frozen solid: an icy statue (joints locked) that shatters into pieces when hit hard.</summary>
    public sealed class Frozen : MonoBehaviour
    {
        Goredoll d; float since, lastHealth;
        public static readonly Color Ice = new Color(0.62f, 0.85f, 1f);

        public static void Apply(Goredoll d)
        {
            if (d == null || d.GetComponent<Frozen>() != null) return;
            var f = d.gameObject.AddComponent<Frozen>();
            f.d = d; f.since = Time.time;
            foreach (var p in d.Parts.Values)
            {
                if (p == null) continue;
                if (p.Renderer != null) p.Renderer.material.color = Ice;
                if (p.Joint != null) { p.Joint.angularXMotion = p.Joint.angularYMotion = p.Joint.angularZMotion = ConfigurableJointMotion.Locked; }
                if (p.Body != null) { p.Body.angularDrag = 4f; p.Body.drag = 0.2f; }
            }
            if (d.Brain != null) d.Brain.enabled = false;
            if (d.Ragdoll != null) d.Ragdoll.State = RagdollState.Limp;
            if (d.Face != null) d.Face.Expression = Expression.Afraid;
            if (!d.IsDead) { d.Vitals.Undead = false; d.Vitals.Die("Frozen solid"); }
            FX.Glass(d.Position + Vector3.up * 0.5f, Vector3.up, 12);
            AudioManager.Play("freeze_ray", d.Position, 1f, 0.7f);
            f.lastHealth = f.Health();
        }

        float Health() { float s = 0; foreach (var p in d.Parts.Values) if (p != null && !p.Gibbed) s += p.Health; return s; }

        void Update()
        {
            if (d == null) { Destroy(this); return; }
            if (Time.time - since < 0.4f) { lastHealth = Health(); return; }
            float h = Health();
            var pel = d.Parts.TryGetValue(PartType.Pelvis, out var pp) && pp != null ? pp.Body : null;
            if (lastHealth - h > 6f || (pel != null && !pel.isKinematic && pel.velocity.magnitude > 8.5f)) { Shatter(); return; }
            lastHealth = h;
        }

        public void Shatter()
        {
            if (d == null) return;
            var center = d.Position;
            foreach (var p in new List<BodyPart>(d.Parts.Values))
                if (p != null && !p.Gibbed && p.Gibbable && d.Dismember != null) d.Dismember.Gib(p, (p.transform.position - center).normalized + Vector3.up * 0.3f);
            for (int i = 0; i < 4; i++) FX.Glass(center + Random.insideUnitSphere * 0.4f + Vector3.up * 0.4f, Random.onUnitSphere, 25);
            AudioManager.Play("glass_break_big", center, 1f, 0.9f);
            AudioManager.Play("glass_break", center, 1f, 1.2f);
            Destroy(this);
        }
    }

    // ====================================================================== guns

    public sealed class CrossbowGun : Firearm
    {
        protected override bool EjectOnFire => false;
        public override string ModeText => "BOLT";
        protected override void FireRound(Vector3 origin, Vector3 dir, Vector3 muzzle, int index, bool authority)
            => Harpoon.Launch(origin + dir * 0.35f - CamT.up * 0.05f, dir, Def, Player, authority);
    }

    public sealed class HarpoonChainGun : Firearm
    {
        protected override bool EjectOnFire => false;
        public override string ModeText => "AUTO HARPOON";
        protected override void FireRound(Vector3 origin, Vector3 dir, Vector3 muzzle, int index, bool authority)
            => Harpoon.Launch(origin + dir * 0.4f - CamT.up * 0.07f, dir, Def, Player, authority);
    }

    /// <summary>Shared raycast for the special guns (ignores your own hitbox).</summary>
    public static class ContentRay
    {
        public static bool Cast(Vector3 origin, Vector3 dir, float range, out RaycastHit hit)
            => Physics.Raycast(origin, dir, out hit, range, Layers.ShootMask, QueryTriggerInteraction.Ignore);
    }

    public sealed class Flamethrower : Firearm
    {
        protected override bool EjectOnFire => false;
        public override string ModeText => "FUEL";
        protected override bool PlaysFireSound => false;
        readonly Dictionary<Goredoll, float> lit = new Dictionary<Goredoll, float>();
        int shot;
        Light pilot;

        protected override void OnAttach()
        {
            base.OnAttach();
            if (Model != null && Model.Muzzle != null)
            {
                pilot = new GameObject("Pilot").AddComponent<Light>();
                pilot.transform.SetParent(Model.Muzzle, false);
                pilot.type = LightType.Point; pilot.color = new Color(0.3f, 0.6f, 1f); pilot.range = 0.6f; pilot.intensity = 1.5f;
            }
        }

        protected override void FireRound(Vector3 origin, Vector3 dir, Vector3 muzzle, int index, bool authority)
        {
            shot++;
            if (shot % 5 == 0) AudioManager.Play("flamer", muzzle, 0.9f, Random.Range(0.9f, 1.1f), 0f, 2f, 40f);
            dir = (dir + Random.insideUnitSphere * 0.06f).normalized;
            for (int i = 0; i < 2; i++) FX.Fire(muzzle + dir * 0.1f, dir * Random.Range(9f, 13f) + Random.insideUnitSphere * 1.2f, Random.Range(0.25f, 0.45f));
            if (shot % 3 == 0) FX.Flash(muzzle + dir * 1.5f, new Color(1f, 0.55f, 0.15f), 8f, 2.5f, 0.08f);
            float range = Def.Range;
            if (ContentRay.Cast(origin, dir, range, out var wall)) range = wall.distance;
            var hits = Physics.OverlapCapsule(origin + dir * 0.8f, origin + dir * range, 0.7f, Layers.ShootMask, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                var part = c.GetComponent<BodyPart>();
                if (part == null || part.Doll == null) continue;
                if (lit.TryGetValue(part.Doll, out var t) && Time.time - t < 0.4f) continue;
                lit[part.Doll] = Time.time;
                ContentFx.Ignite(part.Doll, Player);
                part.TakeDamage(new DamageInfo { Amount = 6f, Type = DamageType.Generic, Point = part.transform.position, Direction = dir, Normal = -dir, Source = Player, Weapon = Def.Name });
            }
            if (shot % 4 == 0 && wall.collider != null) Decals.Scorch(wall.point, wall.normal, 0.7f, wall.collider);
        }

        public override void Tick(InputFrame f, float dt)
        {
            base.Tick(f, dt);
            if (pilot != null) pilot.intensity = 1.2f + Mathf.PerlinNoise(Time.time * 9f, 0f) * 0.8f;
        }
    }

    public sealed class NailGun : Firearm
    {
        protected override bool EjectOnFire => false;
        public override string ModeText => "NAILS";
        int pins;

        protected override void FireRound(Vector3 origin, Vector3 dir, Vector3 muzzle, int index, bool authority)
        {
            Ballistics.Fire(origin, dir, Def, Player, muzzle, DamageType.Stab, false, true);
            if (!ContentRay.Cast(origin, dir, Def.Range, out var hit)) return;
            var nail = ContentModels.Nail();
            nail.transform.SetPositionAndRotation(hit.point - dir * 0.02f, Quaternion.LookRotation(dir));
            nail.transform.SetParent(hit.collider.transform, true);
            Debris.Register(nail, 40f);
            var part = hit.collider.GetComponent<BodyPart>();
            if (part != null && authority && pins < 30 && Physics.Raycast(hit.point + dir * 0.05f, dir, out var behind, 0.55f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                if (ContentFx.PinToWorld(part)) pins++;
        }
    }

    public sealed class TranqGun : Firearm
    {
        protected override bool EjectOnFire => false;
        public override string ModeText => "DART";

        protected override void FireRound(Vector3 origin, Vector3 dir, Vector3 muzzle, int index, bool authority)
        {
            if (!ContentRay.Cast(origin, dir, Def.Range, out var hit)) return;
            var dart = ContentModels.Dart();
            dart.transform.SetPositionAndRotation(hit.point - dir * 0.05f, Quaternion.LookRotation(dir));
            dart.transform.SetParent(hit.collider.transform, true);
            Debris.Register(dart, 40f);
            var part = hit.collider.GetComponent<BodyPart>();
            if (part != null && part.Doll != null)
            {
                part.TakeDamage(new DamageInfo { Amount = Def.Damage, Type = DamageType.Stab, Point = hit.point, Direction = dir, Normal = hit.normal, Source = Player, Weapon = Def.Name, Caliber = 2f });
                ContentFx.Sedate(part.Doll);
                GameEvents.Kill("Tranquilized: " + part.Doll.DisplayName, new Color(0.5f, 0.8f, 1f));
            }
        }
    }

    /// <summary>Laser cutter (slices and burns) or freeze ray (freezes solid). A visible beam while firing.</summary>
    public sealed class BeamGun : Firearm
    {
        public bool Freeze;
        protected override bool EjectOnFire => false;
        public override string ModeText => Freeze ? "FREEZE" : "CUT";
        protected override bool PlaysFireSound => false;
        int shots;
        Transform beam;
        float lastFire = -1f;
        Vector3 beamEnd;
        BodyPart cutting; float cutTime;
        readonly Dictionary<Goredoll, float> chill = new Dictionary<Goredoll, float>();

        protected override void OnAttach()
        {
            base.OnAttach();
            var go = new GameObject("Beam");
            var mb = new MeshBuilder { Color = Color.white };
            mb.Box(new Vector3(0, 0, 0.5f), new Vector3(1f, 1f, 1f));
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Beam");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Glow(Freeze ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.15f, 0.1f), 5f);
            beam = go.transform;
            if (Model != null) go.layer = Model.gameObject.layer;
            go.SetActive(false);
        }

        public override void Destroy() { base.Destroy(); if (beam != null) Object.Destroy(beam.gameObject); }

        protected override void FireRound(Vector3 origin, Vector3 dir, Vector3 muzzle, int index, bool authority)
        {
            lastFire = Time.time;
            if (shots++ % (Freeze ? 4 : 6) == 0) AudioManager.Play(Def.FireSound, muzzle, 0.7f, Random.Range(0.95f, 1.05f), 0f, 2f, 40f);
            float range = Def.Range;
            beamEnd = origin + dir * range;
            if (!ContentRay.Cast(origin, dir, range, out var hit)) { cutting = null; return; }
            beamEnd = hit.point;
            var part = hit.collider.GetComponent<BodyPart>();
            if (Freeze)
            {
                FX.Glass(hit.point, hit.normal, 2);
                if (part != null && part.Doll != null)
                {
                    chill.TryGetValue(part.Doll, out var c);
                    c += 1f / Mathf.Max(1f, Def.Rpm / 60f);
                    chill[part.Doll] = c;
                    if (c > 0.9f) { ContentFx.Freeze(part.Doll); chill.Remove(part.Doll); }
                }
                return;
            }
            if (Random.value < 0.5f) FX.Sparks(hit.point, hit.normal, 3, 4f);
            if (Random.value < 0.3f) FX.Smoke(hit.point, hit.normal * 0.4f, 0.25f, new Color(0.25f, 0.25f, 0.25f, 0.6f), 1f);
            if (part == null || part.Doll == null)
            {
                cutting = null;
                if (Random.value < 0.2f && hit.collider != null) Decals.Scorch(hit.point, hit.normal, 0.12f, hit.collider);
                var rb = hit.rigidbody;
                if (rb != null && !rb.isKinematic && authority) rb.AddForceAtPosition(dir * 2f, hit.point, ForceMode.Force);
                return;
            }
            part.TakeDamage(new DamageInfo { Amount = Def.Damage, Type = DamageType.Slash, Point = hit.point, Direction = dir, Normal = hit.normal, Source = Player, Weapon = Def.Name });
            if (Random.value < 0.25f) GoreKit.Burn(part, 0.08f);
            if (cutting != part) { cutting = part; cutTime = 0f; }
            cutTime += 1f / Mathf.Max(1f, Def.Rpm / 60f);
            if (cutTime > 0.35f && part.IsLimb && part.CanSever && part.Doll.Dismember != null)
            {
                part.Doll.Dismember.Slice(part, hit.point, Vector3.Cross(dir, CamT.right).normalized);
                AudioManager.Play("slice", hit.point, 1f);
                cutting = null;
            }
        }

        public override void Tick(InputFrame f, float dt)
        {
            base.Tick(f, dt);
            if (beam == null || Model == null || Model.Muzzle == null) return;
            bool on = Time.time - lastFire < 0.07f && Equipped;
            if (beam.gameObject.activeSelf != on) beam.gameObject.SetActive(on);
            if (!on) return;
            var from = Model.Muzzle.position;
            var d = beamEnd - from;
            float len = d.magnitude;
            if (len < 0.01f) return;
            float w = (Freeze ? 0.03f : 0.012f) * (1f + Mathf.Sin(Time.time * 60f) * 0.25f);
            beam.SetPositionAndRotation(from, Quaternion.LookRotation(d));
            beam.localScale = new Vector3(w, w, len);
        }

        protected override void OnHolster() { base.OnHolster(); if (beam != null) beam.gameObject.SetActive(false); }
    }

    // ====================================================================== throwing weapons

    public sealed class ThrowWeapon : Weapon, IRefillable
    {
        public ThrownBlade.K Kind;
        int count = -1;
        float t = -1f, cooldown;
        public int InFlight;

        int Max => Mathf.Max(1, Def.Mag);
        public override string AmmoText => Rules.InfiniteAmmo ? "INF" : (count + InFlight > 0 && Kind == ThrownBlade.K.Boomerang ? (count > 0 ? "READY" : "IN FLIGHT") : count.ToString());
        public override string ModeText => "LMB THROW";
        public override bool Busy => t >= 0f;
        public void Refill() { count = Max; ShowModel(true); }
        protected override void OnAttach() { if (count < 0) count = Max; }
        protected override void OnEquip() { Vm.Right.SetPose(HandPose.Knife); Vm.Left.SetPose(HandPose.Relaxed); ShowModel(count > 0 || Rules.InfiniteAmmo); }
        void ShowModel(bool on) { if (Model != null) foreach (var r in Model.GetComponentsInChildren<Renderer>(true)) r.enabled = on; }

        public void Caught() { InFlight = Mathf.Max(0, InFlight - 1); count = Mathf.Min(Max, count + 1); ShowModel(true); AudioManager.Play("equip_knife", Eye, 0.6f, 1.1f, 0f); }

        public override void Tick(InputFrame f, float dt)
        {
            var idle = Def.HipPos + new Vector3(0, Mathf.Sin(Time.time * 1.7f) * 0.003f, 0);
            var pos = idle; var rot = Quaternion.Euler(-12f, -8f, Kind == ThrownBlade.K.Shuriken ? 90f : 0f);
            cooldown -= dt;
            bool have = count > 0 || Rules.InfiniteAmmo;
            if (t < 0f && have && cooldown <= 0f && (f.Fire || (Kind == ThrownBlade.K.Shuriken && f.FireHeld && cooldown <= -0.05f)))
            {
                t = 0f;
                Swing(Kind == ThrownBlade.K.Boomerang ? "whoosh_big" : "whoosh_small", 0.6f);
            }
            if (t >= 0f)
            {
                t += dt / (Kind == ThrownBlade.K.Shuriken ? 0.22f : 0.35f);
                float wind = Ease(Ramp(t, 0f, 0.4f)), fling = Ease(Ramp(t, 0.4f, 0.7f)), back = Ease(Ramp(t, 0.7f, 1f));
                var windPos = new Vector3(0.2f, 0.02f, 0.1f); var windRot = Quaternion.Euler(-50f, -20f, -10f);
                var outPos = new Vector3(0.05f, -0.1f, 0.55f); var outRot = Quaternion.Euler(40f, -5f, 0f);
                pos = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(idle, windPos, wind), outPos, fling), idle, back);
                rot = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Slerp(rot, windRot, wind), outRot, fling), rot, back);
                if (t >= 0.55f && t - dt / 0.3f < 0.55f) Throw();
                if (t >= 1f) { t = -1f; cooldown = Kind == ThrownBlade.K.Shuriken ? 0.05f : 0.15f; }
            }
            Vm.LeftPos = new Vector3(-0.2f, -0.24f, 0.3f); Vm.LeftRot = Quaternion.Euler(-10f, 10f, 70f);
            Vm.TargetPos = pos; Vm.TargetRot = rot;
        }

        void Throw()
        {
            if (count <= 0 && !Rules.InfiniteAmmo) return;
            if (!Rules.InfiniteAmmo) count--;
            var origin = Eye + Aim * 0.5f + CamT.right * 0.12f - CamT.up * 0.05f;
            if (Physics.Linecast(Eye, origin, out var block, Layers.WorldMask, QueryTriggerInteraction.Ignore)) origin = block.point + block.normal * 0.06f;
            ThrownBlade.Launch(Kind, origin, Aim, Def, Player, this);
            if (Kind == ThrownBlade.K.Boomerang) InFlight++;
            if (count <= 0 && !Rules.InfiniteAmmo) ShowModel(false);
        }
    }

    /// <summary>A thrown knife / shuriken (sticks in) or boomerang blade (cuts through, comes back).</summary>
    public sealed class ThrownBlade : MonoBehaviour
    {
        public enum K { Knife, Shuriken, Boomerang }
        K kind; Vector3 vel; float life; WeaponDef def; PlayerController owner; ThrowWeapon home;
        bool stuck, returning;
        Transform vis;
        readonly Dictionary<Goredoll, float> recent = new Dictionary<Goredoll, float>();

        public static void Launch(K kind, Vector3 pos, Vector3 dir, WeaponDef def, PlayerController owner, ThrowWeapon home)
        {
            var go = new GameObject("Thrown " + kind);
            go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            var b = go.AddComponent<ThrownBlade>();
            b.kind = kind; b.def = def; b.owner = owner; b.home = home;
            b.vel = dir * (kind == K.Shuriken ? 34f : kind == K.Knife ? 26f : 22f);
            var m = kind == K.Knife ? ContentModels.ThrowingKnife(0) : kind == K.Shuriken ? ContentModels.Shuriken(0) : ContentModels.Boomerang(0);
            m.transform.SetParent(go.transform, false);
            m.transform.localRotation = kind == K.Knife ? Quaternion.identity : Quaternion.Euler(90, 0, 0);
            b.vis = m.transform;
            Debris.Register(go, 60f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (stuck) return;
            life += dt;
            if (kind == K.Boomerang)
            {
                if (!returning && life > 0.6f) returning = true;
                if (returning && owner != null)
                {
                    var target = owner.EyePosition + Vector3.down * 0.3f;
                    vel = Vector3.Lerp(vel, (target - transform.position).normalized * 24f, dt * 3.5f);
                    if (Vector3.Distance(transform.position, target) < 1.3f) { if (home != null) home.Caught(); Destroy(gameObject); return; }
                }
                else vel = Quaternion.Euler(0, 60f * dt, 0) * vel;
                if (life > 5f) { if (home != null) home.Caught(); Destroy(gameObject); return; }
                vis.Rotate(0, 0, 1400f * dt, Space.Self);
            }
            else
            {
                vel += Physics.gravity * (kind == K.Knife ? 0.45f : 0.2f) * dt;
                if (kind == K.Shuriken) vis.Rotate(0, 0, 1800f * dt, Space.Self);
                else vis.localRotation = Quaternion.Euler(life * 900f, 0, 0);
                if (life > 6f) { Destroy(gameObject); return; }
            }
            var from = transform.position;
            var to = from + vel * dt;
            if (Physics.Linecast(from, to, out var hit, Layers.ShootMask, QueryTriggerInteraction.Ignore) && !IsOwner(hit.collider))
            {
                var part = hit.collider.GetComponent<BodyPart>();
                if (part != null && part.Doll != null)
                {
                    if (!(recent.TryGetValue(part.Doll, out var tt) && Time.time - tt < 0.4f))
                    {
                        recent[part.Doll] = Time.time;
                        HitPart(part, hit);
                    }
                    if (kind != K.Boomerang) { Stick(hit, hit.collider.transform); return; }
                }
                else
                {
                    AudioManager.Play("knife_hit_hard", hit.point, 0.7f, Random.Range(0.9f, 1.15f));
                    FX.Sparks(hit.point, hit.normal, 5, 4f);
                    if (kind == K.Boomerang) { returning = true; vel = Vector3.Reflect(vel, hit.normal) * 0.7f; }
                    else { Stick(hit, hit.collider.transform); return; }
                }
            }
            transform.position = to;
            if (kind == K.Knife || kind == K.Shuriken) transform.rotation = Quaternion.LookRotation(vel);
        }

        bool IsOwner(Collider c) => owner != null && c.transform.IsChildOf(owner.transform);

        void HitPart(BodyPart part, RaycastHit hit)
        {
            var dir = vel.normalized;
            using (NetState.Local())
            {
                part.TakeDamage(new DamageInfo
                {
                    Amount = def.Damage, Type = kind == K.Knife ? DamageType.Stab : DamageType.Slash, Point = hit.point, Direction = dir, Normal = hit.normal,
                    Force = dir * 6f, Source = owner, Weapon = def.Name, Caliber = 5f, Penetration = 0.4f,
                });
                bool cut = kind == K.Boomerang ? Random.value < 0.5f : kind == K.Shuriken && Random.value < 0.12f;
                if (cut && part.IsLimb && part.CanSever && part.Doll.Dismember != null) part.Doll.Dismember.Slice(part, hit.point, Vector3.Cross(dir, Vector3.up).normalized);
            }
            AudioManager.Play(kind == K.Knife ? "stab" : "slice", hit.point, 0.8f);
        }

        void Stick(RaycastHit hit, Transform into)
        {
            stuck = true;
            transform.position = hit.point - vel.normalized * (kind == K.Knife ? 0.12f : 0.03f);
            transform.SetParent(into, true);
            var pickup = gameObject.AddComponent<SphereCollider>();
            pickup.isTrigger = true; pickup.radius = 0.6f;
            gameObject.layer = Layers.Trigger;
            gameObject.AddComponent<ThrownPickup>().Home = home;
        }
    }

    /// <summary>Walk over a stuck knife / shuriken to take it back.</summary>
    public sealed class ThrownPickup : MonoBehaviour
    {
        public ThrowWeapon Home;
        void Update()
        {
            var p = PlayerController.Instance;
            if (p == null || Home == null) return;
            if (Vector3.Distance(p.transform.position + Vector3.up * 0.9f, transform.position) < 1.3f) { Home.Caught(); Destroy(gameObject); }
        }
    }

    // ====================================================================== sentry turret

    public sealed class TurretTool : Weapon
    {
        float cd;
        public override string ModeText => "LMB PLACE";
        public override string AmmoText => Turret.All.Count + " / 4";
        protected override void OnEquip() { Vm.Right.SetPose(HandPose.Hold); Vm.Left.SetPose(HandPose.Hold); }

        public override void Tick(InputFrame f, float dt)
        {
            cd -= dt;
            Vm.TargetPos = Def.HipPos + new Vector3(0, Mathf.Sin(Time.time * 1.5f) * 0.004f, 0);
            Vm.TargetRot = Quaternion.Euler(-8f, -10f, 0f);
            Vm.LeftPos = new Vector3(-0.15f, -0.2f, 0.3f); Vm.LeftRot = Quaternion.Euler(-10f, 20f, 70f);
            if (!f.Fire || cd > 0f) return;
            cd = 0.5f;
            if (!Physics.Raycast(Eye, Aim, out var hit, 8f, Layers.WorldMask | (1 << Layers.Props), QueryTriggerInteraction.Ignore) || hit.normal.y < 0.5f) { GameEvents.ShowToast("Aim at the floor to place the turret."); return; }
            var def = SpawnCatalog.Find("turret");
            var flat = MathX.Flat(Aim);
            SpawnCatalog.Spawn(def, hit.point + Vector3.up * 0.02f, Quaternion.LookRotation(flat.sqrMagnitude > 0.01f ? flat : Vector3.forward));
            AudioManager.Play("metal_impact", hit.point, 0.8f);
        }
    }

    /// <summary>The placed sentry turret: finds the nearest living Goredoll in sight and shreds it (host / singleplayer).</summary>
    public sealed class Turret : MonoBehaviour, IDamageable
    {
        public static readonly List<Turret> All = new List<Turret>();
        Transform head, barrels, muzzle;
        Goredoll target;
        float nextScan, nextShot, spin, health = 300f;
        static WeaponDef shot;

        public bool IsAlive => health > 0f;

        public static GameObject Create(Vector3 pos, Quaternion rot)
        {
            var go = ContentModels.TurretBody(out var head, out var barrels, out var muzzle);
            go.transform.SetPositionAndRotation(pos, rot);
            var t = go.AddComponent<Turret>();
            t.head = head; t.barrels = barrels; t.muzzle = muzzle;
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 60f; rb.isKinematic = false; rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            while (All.Count >= 4) { var old = All[0]; All.RemoveAt(0); if (old != null) Destroy(old.gameObject); }
            All.Add(t);
            return go;
        }

        void OnDestroy() => All.Remove(this);

        public void TakeDamage(DamageInfo info)
        {
            if (NetState.IsClient) return;
            health -= info.Amount * (info.Type == DamageType.Explosion ? 3f : 1f);
            if (health <= 0f) { Explosion.Create(transform.position + Vector3.up * 0.6f, 3f, 80f, this, 1f); Destroy(gameObject); }
        }

        void Update()
        {
            spin = Mathf.MoveTowards(spin, target != null ? 1f : 0f, Time.deltaTime * 2f);
            if (barrels != null) barrels.Rotate(0, 0, spin * 1500f * Time.deltaTime, Space.Self);
            if (NetState.IsClient) return;
            if (Time.time > nextScan) { nextScan = Time.time + 0.4f; Scan(); }
            if (target == null || target.IsDead && Random.value < 0.02f) { target = null; return; }
            var aimAt = target.Parts.TryGetValue(PartType.Chest, out var ch) && ch != null ? ch.transform.position : target.Position;
            var dir = aimAt - head.position;
            var want = Quaternion.LookRotation(dir);
            head.rotation = Quaternion.RotateTowards(head.rotation, want, 240f * Time.deltaTime);
            if (spin > 0.8f && Quaternion.Angle(head.rotation, want) < 6f && Time.time > nextShot)
            {
                nextShot = Time.time + 0.045f;
                if (shot == null) shot = new WeaponDef { Id = "turret_round", Name = "Sentry Turret", Kind = WeaponKind.Firearm, Damage = 26, Penetration = 1.2f, Force = 12, Caliber = 7.62f, Range = 60f };
                var m = muzzle != null ? muzzle.position : head.position + head.forward * 0.8f;
                var d = (head.forward + Random.insideUnitSphere * 0.03f).normalized;
                Ballistics.Fire(m, d, shot, this, m, DamageType.Bullet, Random.value < 0.4f, true);
                if (Random.value < 0.5f) FX.Flash(m, new Color(1f, 0.75f, 0.4f), 5f, 3f, 0.04f);
                if (Random.value < 0.35f) AudioManager.Play("gun_m249", m, 0.6f, Random.Range(0.95f, 1.1f), 1f, 3f, 80f);
            }
        }

        void Scan()
        {
            Goredoll best = null; float bd = 35f;
            foreach (var g in Goredoll.All)
            {
                if (g == null || g.IsDead) continue;
                var p = g.Parts.TryGetValue(PartType.Chest, out var ch) && ch != null ? ch.transform.position : g.Position;
                float d = Vector3.Distance(head.position, p);
                if (d >= bd) continue;
                if (Physics.Linecast(head.position + head.forward * 0.6f, p, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                bd = d; best = g;
            }
            target = best;
        }
    }

    // ====================================================================== models

    public static class ContentModels
    {
        static void Piece(Transform parent, string name, MeshBuilder mb, Material mat, Vector3 pos, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Extra parts on top of the GunKit body.</summary>
        public static void Decorate(string id, WeaponModel m, GunSpec s)
        {
            int layer = m.gameObject.layer;
            var t = m.transform;
            switch (id)
            {
                case "crossbow":
                {
                    var b = new MeshBuilder { Color = GunKit.DarkWood, Gloss = 0.3f };
                    float z = s.Muzzle - 0.04f;
                    b.Cylinder(new Vector3(0, s.Bore, z), new Vector3(0.26f, s.Bore + 0.02f, z - 0.1f), 0.01f, 0.006f, 6);
                    b.Cylinder(new Vector3(0, s.Bore, z), new Vector3(-0.26f, s.Bore + 0.02f, z - 0.1f), 0.01f, 0.006f, 6);
                    b.SetColor(new Color(0.85f, 0.85f, 0.8f), 0.1f);
                    b.Cylinder(new Vector3(0.26f, s.Bore + 0.02f, z - 0.1f), new Vector3(0, s.Bore + 0.01f, 0.05f), 0.0015f, 0.0015f, 4);
                    b.Cylinder(new Vector3(-0.26f, s.Bore + 0.02f, z - 0.1f), new Vector3(0, s.Bore + 0.01f, 0.05f), 0.0015f, 0.0015f, 4);
                    b.SetColor(new Color(0.55f, 0.55f, 0.58f), 0.7f);
                    b.Cylinder(new Vector3(0, s.Bore + 0.012f, 0.05f), new Vector3(0, s.Bore + 0.012f, z + 0.08f), 0.004f, 0.004f, 6);
                    Piece(t, "Limbs", b, Mats.Lit, Vector3.zero, layer);
                    break;
                }
                case "flamer":
                {
                    var b = new MeshBuilder { Color = new Color(0.75f, 0.15f, 0.1f), Gloss = 0.6f };
                    b.Capsule(new Vector3(0, s.Bore - 0.11f, 0.02f), new Vector3(0, s.Bore - 0.11f, 0.32f), 0.045f, 0.045f, 12, 3);
                    b.SetColor(GunKit.Gunmetal, 0.6f);
                    b.Torus(new Vector3(0, s.Bore, s.Muzzle), s.BarrelR * 1.6f, 0.005f, 12, 4, Vector3.forward);
                    Piece(t, "Tank", b, Mats.LitMetal, Vector3.zero, layer);
                    break;
                }
                case "laser":
                case "freezeray":
                {
                    var col = id == "laser" ? new Color(1f, 0.2f, 0.15f) : new Color(0.4f, 0.85f, 1f);
                    var b = new MeshBuilder { Color = Color.white };
                    for (int i = 0; i < 4; i++) b.Torus(new Vector3(0, s.Bore, s.GuardStart + 0.03f + i * 0.04f), s.GuardR * 1.15f, 0.005f, 14, 4, Vector3.forward);
                    b.Cylinder(new Vector3(0, s.Bore, s.Muzzle - 0.02f), new Vector3(0, s.Bore, s.Muzzle + 0.01f), s.BarrelR * 0.8f, s.BarrelR * 0.8f, 10);
                    Piece(t, "Coils", b, Mats.Glow(col, 2.5f), Vector3.zero, layer);
                    if (m.Muzzle != null) m.Muzzle.localPosition = new Vector3(0, s.Bore, s.Muzzle + 0.02f);
                    break;
                }
                case "harpoon_cg":
                {
                    var b = new MeshBuilder { Color = new Color(0.7f, 0.7f, 0.72f), Gloss = 0.8f };
                    b.Cylinder(new Vector3(0, s.Bore, s.Muzzle), new Vector3(0, s.Bore, s.Muzzle + 0.12f), 0.006f, 0.006f, 6);
                    b.Cylinder(new Vector3(0, s.Bore, s.Muzzle + 0.12f), new Vector3(0, s.Bore, s.Muzzle + 0.2f), 0.02f, 0.001f, 6);
                    Piece(t, "Harpoon Tip", b, Mats.LitMetal, Vector3.zero, layer);
                    break;
                }
                case "nailgun":
                {
                    var b = new MeshBuilder { Color = new Color(0.7f, 0.7f, 0.72f), Gloss = 0.7f };
                    b.Box(new Vector3(0, s.Bore - 0.06f, 0.17f), new Vector3(0.01f, 0.08f, 0.14f));
                    Piece(t, "Nail Strip", b, Mats.LitMetal, Vector3.zero, layer);
                    break;
                }
            }
        }

        public static GameObject Nail()
        {
            var go = new GameObject("Nail");
            var mb = new MeshBuilder { Color = new Color(0.7f, 0.7f, 0.72f), Gloss = 0.7f };
            mb.Cylinder(new Vector3(0, 0, -0.01f), new Vector3(0, 0, 0.07f), 0.002f, 0.0015f, 5);
            mb.Cylinder(new Vector3(0, 0, -0.012f), new Vector3(0, 0, -0.01f), 0.006f, 0.006f, 8);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Nail");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            return go;
        }

        public static GameObject Dart()
        {
            var go = new GameObject("Dart");
            var mb = new MeshBuilder { Color = new Color(0.75f, 0.75f, 0.78f), Gloss = 0.8f };
            mb.Cylinder(new Vector3(0, 0, -0.04f), new Vector3(0, 0, 0.06f), 0.003f, 0.002f, 6);
            mb.SetColor(new Color(1f, 0.3f, 0.4f), 0.3f);
            mb.Cylinder(new Vector3(0, 0, -0.08f), new Vector3(0, 0, -0.04f), 0.012f, 0.003f, 8);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Dart");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            return go;
        }

        public static WeaponModel ThrowingKnife(int layer)
        {
            var m = WeaponModels.Root("Throwing Knife", layer);
            var b = new MeshBuilder { Color = new Color(0.72f, 0.73f, 0.76f), Gloss = 0.9f };
            b.Push(Vector3.zero, Quaternion.Euler(0, -90, 0));
            b.Prism(new[] { new Vector2(0, 0.012f), new Vector2(0.17f, 0.004f), new Vector2(0.2f, 0f), new Vector2(0.17f, -0.004f), new Vector2(0, -0.012f) }, 0.003f, Vector3.zero);
            b.Pop();
            m.Piece("Blade", b, Mats.LitMetal, Vector3.zero);
            var h = new MeshBuilder { Color = new Color(0.08f, 0.08f, 0.09f) };
            h.Box(new Vector3(0, 0, -0.05f), new Vector3(0.012f, 0.02f, 0.1f));
            m.Piece("Grip", h, Mats.Lit, Vector3.zero);
            m.GripR = m.Anchor("GripR", new Vector3(0.02f, 0f, -0.05f), Quaternion.Euler(0, 0, -90f));
            m.Muzzle = m.Anchor("Tip", new Vector3(0, 0, 0.2f), Quaternion.identity);
            return m;
        }

        public static WeaponModel Shuriken(int layer)
        {
            var m = WeaponModels.Root("Shuriken", layer);
            var b = new MeshBuilder { Color = new Color(0.3f, 0.31f, 0.34f), Gloss = 0.9f };
            var pts = new List<Vector2>();
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f, r = i % 2 == 0 ? 0.07f : 0.022f; pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
            b.Prism(pts.ToArray(), 0.004f, Vector3.zero);
            m.Piece("Star", b, Mats.LitMetal, Vector3.zero);
            m.GripR = m.Anchor("GripR", new Vector3(0.02f, 0f, 0f), Quaternion.Euler(0, 0, -90f));
            m.Muzzle = m.Anchor("Tip", new Vector3(0, 0, 0.07f), Quaternion.identity);
            return m;
        }

        public static WeaponModel Boomerang(int layer)
        {
            var m = WeaponModels.Root("Boomerang Blade", layer);
            var b = new MeshBuilder { Color = new Color(0.75f, 0.76f, 0.8f), Gloss = 0.9f };
            b.Prism(new[] { new Vector2(-0.2f, 0.12f), new Vector2(-0.15f, 0.14f), new Vector2(0f, 0.02f), new Vector2(0.15f, 0.14f), new Vector2(0.2f, 0.12f), new Vector2(0f, -0.04f) }, 0.006f, Vector3.zero);
            m.Piece("Blade", b, Mats.LitMetal, Vector3.zero);
            m.GripR = m.Anchor("GripR", new Vector3(0.02f, 0f, 0f), Quaternion.Euler(0, 0, -90f));
            m.Muzzle = m.Anchor("Tip", new Vector3(0, 0, 0.2f), Quaternion.identity);
            return m;
        }

        public static WeaponModel TurretTool(int layer)
        {
            var m = WeaponModels.Root("Sentry Turret", layer);
            var b = new MeshBuilder { Color = new Color(0.3f, 0.32f, 0.3f), Gloss = 0.5f };
            b.BevelBox(new Vector3(0, -0.02f, 0.05f), new Vector3(0.18f, 0.14f, 0.2f), 0.02f);
            b.SetColor(new Color(1f, 0.6f, 0.1f), 0.4f);
            b.Box(new Vector3(0, 0.055f, 0.05f), new Vector3(0.1f, 0.01f, 0.12f));
            m.Piece("Case", b, Mats.LitMetal, Vector3.zero);
            m.GripR = m.Anchor("GripR", new Vector3(0.08f, 0f, 0.05f), Quaternion.Euler(0, 0, -90f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.08f, 0f, 0.05f), Quaternion.Euler(0, 0, 90f));
            m.Muzzle = m.Anchor("Tip", new Vector3(0, 0, 0.16f), Quaternion.identity);
            return m;
        }

        public static GameObject TurretBody(out Transform head, out Transform barrels, out Transform muzzle)
        {
            var go = new GameObject("Sentry Turret");
            go.layer = Layers.Props;
            var b = new MeshBuilder { Color = new Color(0.3f, 0.32f, 0.3f), Gloss = 0.5f };
            for (int i = 0; i < 3; i++) { float a = i * 120f * Mathf.Deg2Rad; b.Cylinder(new Vector3(0, 0.55f, 0), new Vector3(Mathf.Cos(a) * 0.5f, 0.02f, Mathf.Sin(a) * 0.5f), 0.03f, 0.025f, 6); }
            b.Cylinder(new Vector3(0, 0.5f, 0), new Vector3(0, 0.75f, 0), 0.08f, 0.08f, 10);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Turret Base");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.5f, 0); col.size = new Vector3(0.8f, 1f, 0.8f);
            var h = new GameObject("Head"); h.layer = Layers.Props; h.transform.SetParent(go.transform, false); h.transform.localPosition = new Vector3(0, 0.85f, 0);
            var hb = new MeshBuilder { Color = new Color(0.26f, 0.28f, 0.26f), Gloss = 0.5f };
            hb.BevelBox(new Vector3(0, 0, -0.05f), new Vector3(0.3f, 0.24f, 0.4f), 0.04f);
            hb.SetColor(new Color(1f, 0.6f, 0.1f), 0.4f);
            hb.Box(new Vector3(0, 0.125f, -0.05f), new Vector3(0.2f, 0.01f, 0.3f));
            hb.SetColor(new Color(0.9f, 0.1f, 0.1f), 0.9f);
            hb.Sphere(new Vector3(0.1f, 0.05f, 0.15f), new Vector3(0.02f, 0.02f, 0.02f), 6, 8);
            h.AddComponent<MeshFilter>().sharedMesh = hb.ToMesh("Turret Head");
            h.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var br = new GameObject("Barrels"); br.layer = Layers.Props; br.transform.SetParent(h.transform, false); br.transform.localPosition = new Vector3(0, 0, 0.15f);
            var bb = new MeshBuilder { Color = new Color(0.1f, 0.1f, 0.11f), Gloss = 0.6f };
            for (int i = 0; i < 6; i++) { float a = i * 60f * Mathf.Deg2Rad; var o = new Vector3(Mathf.Cos(a) * 0.035f, Mathf.Sin(a) * 0.035f, 0); bb.Cylinder(o, o + Vector3.forward * 0.5f, 0.012f, 0.012f, 6); }
            bb.Cylinder(Vector3.zero, Vector3.forward * 0.06f, 0.06f, 0.06f, 10);
            br.AddComponent<MeshFilter>().sharedMesh = bb.ToMesh("Turret Barrels");
            br.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var mz = new GameObject("Muzzle").transform; mz.SetParent(br.transform, false); mz.localPosition = new Vector3(0, 0, 0.55f);
            head = h.transform; barrels = br.transform; muzzle = mz;
            return go;
        }

        // ---------------- melee heads (called from MeleeKit.Build); return the tip z
        public static float Flail(WeaponModel m, MeleeSpec s)
        {
            var b = new MeshBuilder { Color = s.HandleColor, Gloss = 0.3f };
            b.Cylinder(new Vector3(0, 0, -0.1f), new Vector3(0, 0, 0.25f), 0.015f, 0.015f, 8);
            b.SetColor(new Color(0.5f, 0.5f, 0.52f), 0.7f);
            for (int i = 0; i < 6; i++) b.Torus(new Vector3(0, -i * 0.03f * 0.3f, 0.27f + i * 0.035f), 0.012f, 0.003f, 8, 3, i % 2 == 0 ? Vector3.up : Vector3.right);
            var ball = new Vector3(0, -0.02f, 0.5f);
            b.SetColor(s.HeadColor, 0.6f);
            b.Sphere(ball, new Vector3(0.06f, 0.06f, 0.06f), 8, 10);
            for (int i = 0; i < 14; i++) { var dir = Random.onUnitSphere; b.Cylinder(ball + dir * 0.055f, ball + dir * 0.095f, 0.008f, 0.001f, 4); }
            m.Piece("Flail", b, Mats.LitMetal, Vector3.zero);
            return 0.55f;
        }

        public static float Scythe(WeaponModel m, MeleeSpec s)
        {
            var b = new MeshBuilder { Color = s.HandleColor, Gloss = 0.3f };
            float z1 = -0.3f + s.HandleLen;
            b.Cylinder(new Vector3(0, 0, -0.3f), new Vector3(0, 0, z1), 0.016f, 0.014f, 8);
            b.SetColor(s.HeadColor, 0.95f);
            // The blade hangs off the top of the shaft toward the striking side (-Y), curving forward.
            for (int i = 0; i < 7; i++)
                b.Box(new Vector3(0, -0.04f - i * 0.075f, z1 + 0.01f + i * i * 0.006f), new Vector3(0.004f, 0.085f, Mathf.Max(0.015f, 0.07f - i * 0.008f)));
            m.Piece("Scythe", b, Mats.LitMetal, Vector3.zero);
            return z1;
        }

        public static float NailBoard(WeaponModel m, MeleeSpec s)
        {
            var b = new MeshBuilder { Color = s.HeadColor, Gloss = 0.2f };
            b.Box(new Vector3(0, 0, 0.25f), new Vector3(0.025f, 0.09f, 0.75f));
            b.SetColor(s.HandleColor, 0.2f);
            b.Box(new Vector3(0, 0, -0.08f), new Vector3(0.03f, 0.05f, 0.14f));
            b.SetColor(new Color(0.5f, 0.4f, 0.32f), 0.6f);
            for (int i = 0; i < 10; i++) { float z = 0.2f + i * 0.04f; float y = (i % 3 - 1) * 0.025f; b.Cylinder(new Vector3(-0.012f, y, z), new Vector3(-0.07f, y, z), 0.002f, 0.001f, 4); }
            m.Piece("Board", b, Mats.Lit, Vector3.zero);
            return 0.62f;
        }

        public static float GiantHammer(WeaponModel m, MeleeSpec s)
        {
            var b = new MeshBuilder { Color = s.HandleColor, Gloss = 0.3f };
            float z1 = -0.25f + s.HandleLen;
            b.Cylinder(new Vector3(0, 0, -0.25f), new Vector3(0, 0, z1), 0.024f, 0.024f, 10);
            b.SetColor(s.HeadColor, 0.7f);
            b.BevelBox(new Vector3(0, 0, z1 + 0.12f), new Vector3(0.24f, 0.42f, 0.24f), 0.03f);
            b.SetColor(new Color(0.55f, 0.45f, 0.2f), 0.8f);
            b.Box(new Vector3(0, 0.21f, z1 + 0.12f), new Vector3(0.26f, 0.02f, 0.26f));
            b.Box(new Vector3(0, -0.21f, z1 + 0.12f), new Vector3(0.26f, 0.02f, 0.26f));
            m.Piece("Hammer", b, Mats.LitMetal, Vector3.zero);
            return z1 + 0.24f;
        }

        public static float Spear(WeaponModel m, MeleeSpec s)
        {
            var b = new MeshBuilder { Color = s.HandleColor, Gloss = 0.3f };
            float z1 = -0.35f + s.HandleLen;
            b.Cylinder(new Vector3(0, 0, -0.35f), new Vector3(0, 0, z1), 0.014f, 0.013f, 8);
            b.SetColor(s.HeadColor, 0.95f);
            b.Cylinder(new Vector3(0, 0, z1), new Vector3(0, 0, z1 + 0.24f), 0.026f, 0.001f, 4);
            b.SetColor(new Color(0.2f, 0.2f, 0.2f), 0.4f);
            b.Cylinder(new Vector3(0, 0, z1 - 0.03f), new Vector3(0, 0, z1 + 0.01f), 0.018f, 0.018f, 8);
            m.Piece("Spear", b, Mats.LitMetal, Vector3.zero);
            return z1 + 0.24f;
        }
    }
}
