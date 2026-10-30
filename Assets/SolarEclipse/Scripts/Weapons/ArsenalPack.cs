using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Arsenal pack: 20 guns + 10 melee weapons, all data. Each gun is a <see cref="WeaponDef"/> (stats, sounds) plus a
    /// <see cref="GunSpec"/> (model, built by GunKit) and a few <see cref="GunExtra"/> mechanics; each melee weapon is a
    /// WeaponDef plus a <see cref="MeleeSpec"/> (model by MeleeKit, behaviour by MeleeWeapon). Sounds: Audio/RecipesArsenal.cs.
    /// The defs are appended to WeaponCatalog.All once, in this order (the catalog index is sent over the network:
    /// only ever append to the end of <see cref="Guns"/> / <see cref="Melee"/>... and keep Guns before Melee).
    /// </summary>
    public static class ArsenalPack
    {
        /// <summary>Extra gun mechanics beyond the plain <see cref="Firearm"/>.</summary>
        public sealed class GunExtra
        {
            /// <summary>Seconds of lever/bolt work after every shot (0 = self-loading).</summary>
            public float Cycle;
            /// <summary>Doll launch speed for point-blank/heavy hits (0 = none) and its range.</summary>
            public float Launch, LaunchRange = 6f;
            public bool OpenBolt;
            /// <summary>Built-in scope: aiming uses the full-screen scope picture.</summary>
            public bool Scoped;
            public string Mode;
            public string Category = "Rifles";
            public string Allowed = "";
        }

        sealed class GunEntry { public WeaponDef Def; public GunSpec Spec; public GunExtra Extra; }
        sealed class MeleeEntry { public WeaponDef Def; public MeleeSpec Spec; }

        static readonly Dictionary<string, GunEntry> guns = new Dictionary<string, GunEntry>();
        static readonly Dictionary<string, MeleeEntry> melee = new Dictionary<string, MeleeEntry>();
        static readonly List<string> order = new List<string>();

        public static readonly string[] Categories = { "All", "Pistols", "SMGs", "Rifles", "Shotguns", "Heavy", "Melee", "Explosives", "Tools" };

        // ------------------------------------------------------------------ registration

        static bool defined;
        static void Define()
        {
            if (defined) return;
            defined = true;
            DefineGuns();
            DefineMelee();
        }

        static void Gun(WeaponDef d, GunSpec s, GunExtra x)
        {
            d.Kind = WeaponKind.Firearm;
            guns[d.Id] = new GunEntry { Def = d, Spec = s, Extra = x };
            order.Add(d.Id);
        }

        static void Melee(WeaponDef d, MeleeSpec s)
        {
            d.Kind = WeaponKind.Melee;
            melee[d.Id] = new MeleeEntry { Def = d, Spec = s };
            order.Add(d.Id);
        }

        /// <summary>Called once by WeaponCatalog's static constructor.</summary>
        public static void AppendDefs(List<WeaponDef> all)
        {
            Define();
            foreach (var id in order)
            {
                if (all.Exists(w => w.Id == id)) continue;
                all.Add(guns.TryGetValue(id, out var g) ? g.Def : melee[id].Def);
            }
        }

        public static bool Has(string id) { Define(); return id != null && (guns.ContainsKey(id) || melee.ContainsKey(id)); }

        public static GunSpec SpecOf(string id) { Define(); return id != null && guns.TryGetValue(id, out var g) ? g.Spec : null; }

        public static Weapon Create(WeaponDef def)
        {
            if (def == null) return null;
            Define();
            // Only the catalog instances (a mod weapon reusing an id gets its own handling).
            if (guns.TryGetValue(def.Id, out var g) && ReferenceEquals(g.Def, def)) return new ArsenalGun { Def = def, Spec = g.Spec, Extra = g.Extra };
            if (melee.TryGetValue(def.Id, out var m) && ReferenceEquals(m.Def, def)) return new MeleeWeapon { Def = def, Spec = m.Spec };
            return null;
        }

        public static WeaponModel BuildModel(string id, int layer)
        {
            if (id == null) return null;
            Define();
            if (guns.TryGetValue(id, out var g)) return GunKit.Build(g.Def.Name, g.Spec, layer);
            if (melee.TryGetValue(id, out var m)) return MeleeKit.Build(m.Def.Name, m.Spec, layer);
            return null;
        }

        /// <summary>Called by the Attachments static constructor: (id, allowed, rail, under, laser, opticOnSlide).</summary>
        public static void RegisterMounts(System.Action<string, string, Vector3, Vector3, Vector3, bool> add)
        {
            Define();
            foreach (var g in guns.Values)
            {
                if (string.IsNullOrEmpty(g.Extra.Allowed)) continue;
                var s = g.Spec;
                var rail = s.RailPos;
                if (s.CarryHandle) rail.y += 0.039f;
                if (s.Frame == GunFrame.Long && s.Stock == GunStock.Bullpup) rail.z = s.RecBack + (s.RecFront - s.RecBack) * 0.45f;
                add(g.Def.Id, g.Extra.Allowed, rail, s.UnderPos, s.LaserPos, s.Frame == GunFrame.Pistol);
            }
        }

        /// <summary>Spawn menu category of any catalog weapon (pack, base game or modded).</summary>
        public static string Category(WeaponDef d)
        {
            if (d == null) return "Tools";
            Define();
            if (guns.TryGetValue(d.Id, out var g)) return g.Extra.Category;
            if (melee.ContainsKey(d.Id)) return "Melee";
            switch (d.Id)
            {
                case "glock": case "revolver": return "Pistols";
                case "uzi": case "mac11": return "SMGs";
                case "ar15": case "sniper": return "Rifles";
                case "ar17": case "shotgun": return "Shotguns";
                case "minigun": case "harpoon": return "Heavy";
            }
            switch (d.Kind)
            {
                case WeaponKind.Knife: case WeaponKind.Melee: return "Melee";
                case WeaponKind.Explosive: return "Explosives";
                case WeaponKind.Firearm:
                    if (d.Pellets > 1) return "Shotguns";
                    if (d.Glyph == "smg") return "SMGs";
                    if (d.Glyph == "gun") return "Pistols";
                    return d.WeightKg >= 7f ? "Heavy" : "Rifles";
                default: return "Tools";
            }
        }

        // ------------------------------------------------------------------ data: guns

        static readonly Vector3 PistolHip = new Vector3(0.12f, -0.15f, 0.33f), SmgHip = new Vector3(0.13f, -0.15f, 0.28f), RifleHip = new Vector3(0.12f, -0.16f, 0.2f);
        const string AllRifle = "reddot holo scope ext drum supp comp vgrip agrip laser", AllSmg = "reddot holo ext drum supp comp vgrip agrip laser";

        static WeaponDef PistolDef(string id, string name, string desc, string sound) => new WeaponDef
        {
            Id = id, Name = name, Description = desc, Glyph = "gun", FireSound = sound, MagOut = "mag_out_pistol", MagIn = "mag_in_pistol", Charge = "slide_back",
            HipPos = PistolHip, AimFov = 0.8f, AimDistance = 0.21f, CasingType = 0,
        };

        static WeaponDef SmgDef(string id, string name, string desc, string sound) => new WeaponDef
        {
            Id = id, Name = name, Description = desc, Glyph = "smg", FireSound = sound, MagOut = "mag_out_smg", MagIn = "mag_in_smg", Charge = "charging_handle",
            HipPos = SmgHip, AimFov = 0.78f, AimDistance = 0.18f, CasingType = 0, Auto = true,
        };

        static WeaponDef RifleDef(string id, string name, string desc, string sound) => new WeaponDef
        {
            Id = id, Name = name, Description = desc, Glyph = "rifle", FireSound = sound, MagOut = "mag_out_rifle", MagIn = "mag_in_rifle", Charge = "charging_handle",
            HipPos = RifleHip, AimFov = 0.64f, AimDistance = 0.16f, CasingType = 1, NoiseRadius = 130f,
        };

        static void DefineGuns()
        {
            // ---------------- pistols
            var d = PistolDef("deagle", "Desert Eagle", ".50 AE hand cannon. 7 rounds, savage recoil, blows limbs off.", "gun_deagle");
            d.Damage = 85; d.Rpm = 150; d.HipSpread = 1.6f; d.AimSpread = 0.1f; d.Recoil = 4.8f; d.Penetration = 1.1f; d.Force = 34; d.Caliber = 12.7f; d.Mag = 7;
            d.ReloadTime = 1.9f; d.EmptyExtra = 0.4f; d.WeightKg = 2f; d.NoiseRadius = 130f; d.AimDistance = 0.23f;
            Gun(d, new GunSpec { Frame = GunFrame.Pistol, Bore = 0.06f, RecW = 0.034f, SlideLen = 0.23f, SlideH = 0.036f, SlideZ = 0.09f, GripAngle = 16f, BarrelR = 0.014f,
                Body = GunKit.Silver, Metal = GunKit.Silver, Furniture = GunKit.Polymer, MetalBody = true, BodyGloss = 0.6f, RibbedSlide = true, MagLen = 0.09f },
                new GunExtra { Category = "Pistols", Allowed = "reddot comp laser ext" });

            d = PistolDef("m1911", "M1911", "Classic .45 ACP. 7+1 heavy rounds, wood grips.", "gun_m1911");
            d.Damage = 44; d.Rpm = 380; d.HipSpread = 1.2f; d.AimSpread = 0.1f; d.Recoil = 2.4f; d.Penetration = 0.55f; d.Force = 13; d.Caliber = 11.4f; d.Mag = 7;
            d.ReloadTime = 1.6f; d.EmptyExtra = 0.35f; d.WeightKg = 1.1f; d.NoiseRadius = 100f;
            Gun(d, new GunSpec { Frame = GunFrame.Pistol, RecW = 0.03f, SlideLen = 0.205f, SlideH = 0.029f, SlideZ = 0.085f, GripAngle = 16f, Hammer = true,
                Body = GunKit.Gunmetal, Metal = GunKit.Gunmetal, Furniture = GunKit.Wood, MetalBody = true, BodyGloss = 0.5f },
                new GunExtra { Category = "Pistols", Allowed = "reddot supp comp laser ext" });

            d = PistolDef("beretta", "Beretta 92", "Italian 9mm service pistol. 15+1 rounds, smooth and accurate.", "gun_beretta");
            d.Damage = 31; d.Rpm = 480; d.HipSpread = 1.1f; d.AimSpread = 0.1f; d.Recoil = 1.7f; d.Penetration = 0.45f; d.Force = 9; d.Caliber = 9f; d.Mag = 15;
            d.ReloadTime = 1.5f; d.EmptyExtra = 0.35f; d.WeightKg = 0.95f; d.NoiseRadius = 85f;
            Gun(d, new GunSpec { Frame = GunFrame.Pistol, RecW = 0.031f, SlideLen = 0.2f, SlideH = 0.028f, SlideZ = 0.085f, GripAngle = 14f, Hammer = true,
                Body = GunKit.DarkSteel, Metal = new Color(0.09f, 0.09f, 0.1f), Furniture = new Color(0.16f, 0.15f, 0.15f), MetalBody = true },
                new GunExtra { Category = "Pistols", Allowed = "reddot holo ext drum supp comp laser" });

            d = PistolDef("glock18", "Glock 18", "Full-auto 9mm machine pistol. 33-round extended mag, 1100 rpm. B toggles fire mode.", "gun_glock18");
            d.Damage = 27; d.Rpm = 1100; d.Auto = true; d.HipSpread = 2.4f; d.AimSpread = 0.9f; d.Recoil = 1.15f; d.Penetration = 0.4f; d.Force = 8; d.Caliber = 9f; d.Mag = 33; d.Range = 160f;
            d.ReloadTime = 1.6f; d.EmptyExtra = 0.35f; d.WeightKg = 0.95f; d.NoiseRadius = 90f;
            Gun(d, new GunSpec { Frame = GunFrame.Pistol, MagLen = 0.145f, Body = GunKit.Polymer, Metal = new Color(0.24f, 0.24f, 0.26f), Furniture = GunKit.Polymer },
                new GunExtra { Category = "Pistols", Allowed = "reddot holo ext drum supp comp laser", Mode = null });

            d = PistolDef("tec9", "TEC-9", "Cheap and nasty 9mm machine pistol. 32 rounds, sprays wild.", "gun_tec9");
            d.Damage = 25; d.Rpm = 820; d.Auto = true; d.HipSpread = 3f; d.AimSpread = 1.4f; d.Recoil = 1.1f; d.Penetration = 0.35f; d.Force = 7; d.Mag = 32; d.Range = 120f;
            d.ReloadTime = 1.8f; d.EmptyExtra = 0.4f; d.WeightKg = 1.4f; d.NoiseRadius = 90f; d.Charge = "charging_handle"; d.MagOut = "mag_out_smg"; d.MagIn = "mag_in_smg"; d.Glyph = "smg";
            d.HipPos = new Vector3(0.12f, -0.14f, 0.32f);
            Gun(d, new GunSpec { Bore = 0.05f, RecBack = -0.06f, RecFront = 0.13f, RecH = 0.05f, RecW = 0.03f, Muzzle = 0.27f, BarrelR = 0.007f,
                Guard = GunGuard.Shroud, GuardStart = 0.13f, GuardEnd = 0.25f, GuardR = 0.014f, Stock = GunStock.None,
                Mag = GunMag.Stick, MagZ = 0.06f, MagLen = 0.17f, MagW = 0.022f, MagD = 0.03f, MagAngle = 0f, GripAngle = 14f, Body = GunKit.Polymer, Furniture = GunKit.Polymer },
                new GunExtra { Category = "Pistols", Allowed = "reddot ext drum supp comp laser" });

            // ---------------- SMGs
            d = SmgDef("mp5", "MP5", "H&K 9mm submachine gun. 30 rounds, very controllable. B: auto / 3-round burst / semi.", "gun_mp5");
            d.Damage = 27; d.Rpm = 800; d.Burst = 3; d.HipSpread = 2.2f; d.AimSpread = 0.55f; d.Recoil = 0.9f; d.Penetration = 0.4f; d.Force = 8; d.Caliber = 9f; d.Mag = 30;
            d.ReloadTime = 2.1f; d.EmptyExtra = 0.5f; d.WeightKg = 2.5f; d.NoiseRadius = 90f;
            Gun(d, new GunSpec { Bore = 0.06f, RecBack = -0.08f, RecFront = 0.17f, RecH = 0.055f, RecW = 0.03f, Muzzle = 0.36f, BarrelR = 0.008f,
                Guard = GunGuard.Slim, GuardStart = 0.17f, GuardEnd = 0.31f, GuardR = 0.02f, Stock = GunStock.Fixed, StockLen = 0.22f,
                Mag = GunMag.Curved, MagZ = 0.09f, MagLen = 0.15f, MagW = 0.02f, MagD = 0.03f, MagAngle = 14f, Body = GunKit.DarkSteel, MetalBody = true, BodyGloss = 0.45f },
                new GunExtra { Category = "SMGs", Allowed = AllSmg.Replace("ext drum ", "") });

            d = SmgDef("p90", "P90", "Bullpup PDW. 50-round top magazine of armour-piercing 5.7mm.", "gun_p90");
            d.Damage = 24; d.Rpm = 900; d.HipSpread = 2f; d.AimSpread = 0.5f; d.Recoil = 0.75f; d.Penetration = 0.9f; d.Force = 7; d.Caliber = 5.7f; d.Mag = 50;
            d.ReloadTime = 2.6f; d.EmptyExtra = 0.45f; d.WeightKg = 2.6f; d.NoiseRadius = 90f; d.HipPos = new Vector3(0.12f, -0.15f, 0.3f);
            Gun(d, new GunSpec { Bore = 0.06f, RecBack = -0.33f, RecFront = 0.15f, RecH = 0.1f, RecW = 0.05f, Muzzle = 0.21f, BarrelR = 0.008f,
                Guard = GunGuard.None, Stock = GunStock.Bullpup, Mag = GunMag.Top, MagZ = -0.09f, MagLen = 0.24f, MagW = 0.04f, SightLift = 0.032f,
                SupportPos = new Vector3(-0.01f, -0.03f, 0.1f), SupportRot = new Vector3(-10f, 0f, 170f), Body = GunKit.Polymer, Furniture = GunKit.Polymer },
                new GunExtra { Category = "SMGs", Allowed = "reddot holo supp comp laser" });

            d = SmgDef("vector", "KRISS Vector", ".45 ACP at 1200 rpm with almost no recoil. 25 rounds. B: auto / 2-round burst / semi.", "gun_vector");
            d.Damage = 26; d.Rpm = 1200; d.Burst = 2; d.HipSpread = 2.2f; d.AimSpread = 0.65f; d.Recoil = 0.6f; d.Penetration = 0.5f; d.Force = 9; d.Caliber = 11.4f; d.Mag = 25;
            d.ReloadTime = 2f; d.EmptyExtra = 0.45f; d.WeightKg = 2.7f; d.NoiseRadius = 90f;
            Gun(d, new GunSpec { Bore = 0.065f, RecBack = -0.09f, RecFront = 0.16f, RecH = 0.09f, RecW = 0.034f, Muzzle = 0.3f, BarrelR = 0.008f, Rail = true,
                Guard = GunGuard.Slim, GuardStart = 0.16f, GuardEnd = 0.26f, GuardR = 0.018f, Stock = GunStock.Skeleton, StockLen = 0.22f,
                Mag = GunMag.Stick, MagZ = 0.105f, MagLen = 0.13f, MagW = 0.024f, MagD = 0.035f, MagAngle = 0f, Body = GunKit.Tan, Furniture = GunKit.Polymer },
                new GunExtra { Category = "SMGs", Allowed = AllSmg });

            d = SmgDef("ump45", "UMP-45", "Polymer .45 SMG. 25 heavy rounds, slow and punchy. B: auto / 2-round burst / semi.", "gun_ump");
            d.Damage = 33; d.Rpm = 600; d.Burst = 2; d.HipSpread = 2f; d.AimSpread = 0.5f; d.Recoil = 1.1f; d.Penetration = 0.5f; d.Force = 11; d.Caliber = 11.4f; d.Mag = 25;
            d.ReloadTime = 2.1f; d.EmptyExtra = 0.45f; d.WeightKg = 2.3f; d.NoiseRadius = 95f;
            Gun(d, new GunSpec { Bore = 0.06f, RecBack = -0.09f, RecFront = 0.2f, RecH = 0.07f, RecW = 0.034f, Muzzle = 0.33f, BarrelR = 0.008f, Rail = true,
                Guard = GunGuard.Slim, GuardStart = 0.2f, GuardEnd = 0.3f, GuardR = 0.02f, Stock = GunStock.Skeleton, StockLen = 0.23f,
                Mag = GunMag.Stick, MagZ = 0.1f, MagLen = 0.16f, MagW = 0.024f, MagD = 0.04f, MagAngle = 3f, Body = GunKit.Polymer, Furniture = GunKit.Polymer },
                new GunExtra { Category = "SMGs", Allowed = AllSmg });

            d = SmgDef("thompson", "Thompson", "Tommy gun with a 50-round drum. .45 ACP, wood furniture, open bolt.", "gun_thompson");
            d.Damage = 36; d.Rpm = 700; d.HipSpread = 2.6f; d.AimSpread = 0.8f; d.Recoil = 1.25f; d.Penetration = 0.55f; d.Force = 12; d.Caliber = 11.4f; d.Mag = 50;
            d.ReloadTime = 3f; d.EmptyExtra = 0.5f; d.WeightKg = 4.9f; d.NoiseRadius = 110f; d.HipPos = new Vector3(0.13f, -0.16f, 0.24f);
            Gun(d, new GunSpec { Bore = 0.06f, RecBack = -0.1f, RecFront = 0.18f, RecH = 0.065f, RecW = 0.032f, Muzzle = 0.5f, BarrelR = 0.011f, FlashHider = true,
                Guard = GunGuard.None, GuardStart = 0.2f, GuardEnd = 0.32f, GuardR = 0.012f, Foregrip = true, Stock = GunStock.Wood, StockLen = 0.3f, StockDrop = 0.03f,
                Mag = GunMag.Drum, MagZ = 0.1f, MagLen = 0.1f, MagW = 0.022f, MagD = 0.03f, TopCharge = true,
                Body = GunKit.Gunmetal, Furniture = GunKit.Wood, MetalBody = true, BodyGloss = 0.5f },
                new GunExtra { Category = "SMGs", Allowed = "reddot comp laser", OpenBolt = true });

            // ---------------- rifles
            d = RifleDef("ak47", "AK-47", "7.62mm assault rifle. 30 rounds, hits hard, kicks harder. B toggles fire mode.", "gun_ak47");
            d.Damage = 58; d.Rpm = 600; d.Auto = true; d.HipSpread = 2f; d.AimSpread = 0.12f; d.Recoil = 2.6f; d.Penetration = 1.5f; d.Force = 24; d.Caliber = 7.62f; d.Mag = 30;
            d.ReloadTime = 2.5f; d.EmptyExtra = 0.5f; d.WeightKg = 3.6f;
            Gun(d, new GunSpec { Bore = 0.06f, RecBack = -0.09f, RecFront = 0.2f, RecH = 0.06f, RecW = 0.03f, Muzzle = 0.62f, BarrelR = 0.009f, GasTube = true, FlashHider = true,
                Guard = GunGuard.Wood, GuardStart = 0.2f, GuardEnd = 0.4f, GuardR = 0.022f, Stock = GunStock.Wood, StockLen = 0.3f, StockDrop = 0.03f,
                Mag = GunMag.Curved, MagZ = 0.1f, MagLen = 0.2f, MagW = 0.024f, MagD = 0.055f, MagAngle = 18f, Body = GunKit.DarkSteel, Furniture = GunKit.Wood, MetalBody = true, BodyGloss = 0.45f },
                new GunExtra { Category = "Rifles", Allowed = AllRifle });

            d = RifleDef("m4a1", "M4A1", "5.56mm carbine. 30 rounds, light and accurate. B: auto / 3-round burst / semi.", "gun_m4a1");
            d.Damage = 50; d.Rpm = 800; d.Auto = true; d.Burst = 3; d.HipSpread = 1.5f; d.AimSpread = 0.05f; d.Recoil = 2f; d.Penetration = 1.3f; d.Force = 20; d.Caliber = 5.56f; d.Mag = 30;
            d.ReloadTime = 2.1f; d.EmptyExtra = 0.45f; d.WeightKg = 3.1f;
            Gun(d, new GunSpec { Bore = 0.055f, RecBack = -0.06f, RecFront = 0.17f, RecH = 0.07f, RecW = 0.026f, Muzzle = 0.52f, BarrelR = 0.0085f, FlashHider = true, Rail = true, TopCharge = true,
                Guard = GunGuard.Rail, GuardStart = 0.17f, GuardEnd = 0.37f, GuardR = 0.022f, Stock = GunStock.Fixed, StockLen = 0.24f,
                Mag = GunMag.Stick, MagZ = 0.08f, MagLen = 0.15f, MagW = 0.022f, MagD = 0.06f, MagAngle = 10f, Body = GunKit.DarkSteel, Furniture = GunKit.Polymer, MetalBody = true, BodyGloss = 0.4f },
                new GunExtra { Category = "Rifles", Allowed = AllRifle });

            d = RifleDef("scarh", "SCAR-H", "7.62mm battle rifle. 20 rounds that go through walls and bodies.", "gun_scarh");
            d.Damage = 68; d.Rpm = 600; d.Auto = true; d.HipSpread = 1.8f; d.AimSpread = 0.06f; d.Recoil = 3f; d.Penetration = 1.9f; d.Force = 28; d.Caliber = 7.62f; d.Mag = 20;
            d.ReloadTime = 2.4f; d.EmptyExtra = 0.45f; d.WeightKg = 3.6f; d.NoiseRadius = 140f;
            Gun(d, new GunSpec { Bore = 0.06f, RecBack = -0.08f, RecFront = 0.27f, RecH = 0.075f, RecW = 0.034f, Muzzle = 0.58f, BarrelR = 0.0095f, FlashHider = true, Rail = true,
                Guard = GunGuard.Slim, GuardStart = 0.2f, GuardEnd = 0.36f, GuardR = 0.022f, Stock = GunStock.Heavy, StockLen = 0.24f,
                Mag = GunMag.Stick, MagZ = 0.09f, MagLen = 0.12f, MagW = 0.024f, MagD = 0.065f, MagAngle = 4f, Body = GunKit.Tan, Furniture = GunKit.Polymer },
                new GunExtra { Category = "Rifles", Allowed = AllRifle });

            d = RifleDef("famas", "FAMAS", "French bullpup. 25 rounds at 1000 rpm in 3-round bursts. B toggles burst / semi.", "gun_famas");
            d.Damage = 46; d.Rpm = 1000; d.Burst = 3; d.HipSpread = 1.6f; d.AimSpread = 0.06f; d.Recoil = 1.7f; d.Penetration = 1.2f; d.Force = 19; d.Caliber = 5.56f; d.Mag = 25;
            d.ReloadTime = 2.4f; d.EmptyExtra = 0.45f; d.WeightKg = 3.6f; d.HipPos = new Vector3(0.12f, -0.16f, 0.26f);
            Gun(d, new GunSpec { Bore = 0.055f, RecBack = -0.42f, RecFront = 0.12f, RecH = 0.08f, RecW = 0.034f, Muzzle = 0.43f, BarrelR = 0.0085f, CarryHandle = true, Bipod = true, FlashHider = true,
                Guard = GunGuard.Slim, GuardStart = 0.12f, GuardEnd = 0.25f, GuardR = 0.022f, Stock = GunStock.Bullpup,
                Mag = GunMag.Stick, MagZ = -0.13f, MagLen = 0.14f, MagW = 0.022f, MagD = 0.055f, MagAngle = 6f, Body = GunKit.Polymer, Furniture = GunKit.Polymer },
                new GunExtra { Category = "Rifles", Allowed = "reddot holo scope ext supp comp laser" });

            d = RifleDef("g36", "G36", "German 5.56mm rifle with a carry handle. 30 rounds. B: auto / 2-round burst / semi.", "gun_g36");
            d.Damage = 48; d.Rpm = 750; d.Auto = true; d.Burst = 2; d.HipSpread = 1.6f; d.AimSpread = 0.05f; d.Recoil = 1.8f; d.Penetration = 1.3f; d.Force = 19; d.Caliber = 5.56f; d.Mag = 30;
            d.ReloadTime = 2.2f; d.EmptyExtra = 0.45f; d.WeightKg = 3.6f;
            Gun(d, new GunSpec { Bore = 0.055f, RecBack = -0.07f, RecFront = 0.18f, RecH = 0.07f, RecW = 0.032f, Muzzle = 0.5f, BarrelR = 0.0085f, CarryHandle = true, FlashHider = true,
                Guard = GunGuard.Slim, GuardStart = 0.18f, GuardEnd = 0.36f, GuardR = 0.024f, Stock = GunStock.Skeleton, StockLen = 0.25f,
                Mag = GunMag.Stick, MagZ = 0.085f, MagLen = 0.15f, MagW = 0.022f, MagD = 0.055f, MagAngle = 12f, Body = new Color(0.14f, 0.15f, 0.13f), Furniture = new Color(0.14f, 0.15f, 0.13f) },
                new GunExtra { Category = "Rifles", Allowed = AllRifle });

            d = RifleDef("lever", "Lever-Action", ".44 lever-action rifle. 8 rounds loaded one at a time; work the lever between shots.", "gun_lever");
            d.Damage = 78; d.Rpm = 400; d.HipSpread = 1.6f; d.AimSpread = 0.05f; d.Recoil = 3.2f; d.Penetration = 1.2f; d.Force = 30; d.Caliber = 11f; d.Mag = 8;
            d.ShellReload = true; d.ReloadTime = 0.42f; d.EmptyExtra = 0.45f; d.MagIn = "shell_in"; d.MagOut = null; d.Charge = "lever_action"; d.WeightKg = 3.3f; d.NoiseRadius = 130f;
            Gun(d, new GunSpec { Bore = 0.05f, RecBack = -0.05f, RecFront = 0.11f, RecH = 0.055f, RecW = 0.026f, Muzzle = 0.64f, BarrelR = 0.009f, Lever = true, Hammer = true,
                Guard = GunGuard.Wood, GuardStart = 0.12f, GuardEnd = 0.33f, GuardR = 0.018f, Stock = GunStock.Wood, StockLen = 0.32f, StockDrop = 0.04f, GripAngle = 32f,
                Mag = GunMag.Tube, Body = GunKit.Gunmetal, Furniture = GunKit.Wood, MetalBody = true, BodyGloss = 0.55f },
                new GunExtra { Category = "Rifles", Allowed = "reddot scope comp laser", Cycle = 0.55f, Mode = "LEVER .44" });

            // ---------------- shotguns
            d = RifleDef("dbarrel", "Double Barrel", "Side-by-side 12 gauge. Two barrels, 12 pellets each, throws bodies across the room.", "gun_dbarrel");
            d.Glyph = "shotgun"; d.Damage = 16; d.Pellets = 12; d.Rpm = 400; d.HipSpread = 5.5f; d.AimSpread = 4.4f; d.Recoil = 6.5f; d.Penetration = 0.3f; d.Force = 30; d.Caliber = 8.4f; d.Mag = 2;
            d.ShellReload = true; d.ReloadTime = 0.45f; d.EmptyExtra = 0.2f; d.MagIn = "shell_in"; d.MagOut = null; d.Charge = "break_close"; d.CasingType = 2; d.WeightKg = 3.2f;
            d.HipPos = new Vector3(0.12f, -0.16f, 0.15f); d.AimFov = 0.8f; d.AimDistance = 0.17f; d.NoiseRadius = 140f;
            Gun(d, new GunSpec { Bore = 0.05f, RecBack = -0.04f, RecFront = 0.07f, RecH = 0.05f, RecW = 0.034f, Muzzle = 0.74f, BarrelR = 0.011f, DoubleBarrel = true, BreakAction = true,
                Guard = GunGuard.Wood, GuardStart = 0.09f, GuardEnd = 0.3f, GuardR = 0.018f, Stock = GunStock.Wood, StockLen = 0.34f, StockDrop = 0.04f, GripAngle = 32f,
                Mag = GunMag.Shell, Body = GunKit.Gunmetal, Furniture = GunKit.DarkWood, Metal = GunKit.DarkSteel, MetalBody = true, BodyGloss = 0.55f },
                new GunExtra { Category = "Shotguns", Allowed = "reddot laser", Launch = 10f, LaunchRange = 5f, Mode = "2x 12GA" });

            d = RifleDef("aa12", "AA-12", "Full-auto 12 gauge with a 20-shell drum and almost no recoil.", "gun_aa12");
            d.Glyph = "shotgun"; d.Damage = 13; d.Pellets = 8; d.Rpm = 300; d.Auto = true; d.HipSpread = 4.5f; d.AimSpread = 3.6f; d.Recoil = 2.2f; d.Penetration = 0.25f; d.Force = 16; d.Caliber = 8.4f; d.Mag = 20;
            d.ReloadTime = 3f; d.EmptyExtra = 0.5f; d.CasingType = 2; d.WeightKg = 5.2f; d.HipPos = new Vector3(0.12f, -0.16f, 0.18f); d.AimFov = 0.8f; d.NoiseRadius = 140f;
            Gun(d, new GunSpec { Bore = 0.065f, RecBack = -0.12f, RecFront = 0.3f, RecH = 0.09f, RecW = 0.04f, Muzzle = 0.5f, BarrelR = 0.012f, Rail = true, CarryHandle = true,
                Guard = GunGuard.Slim, GuardStart = 0.3f, GuardEnd = 0.42f, GuardR = 0.024f, Stock = GunStock.Heavy, StockLen = 0.22f,
                Mag = GunMag.Drum, MagZ = 0.12f, MagLen = 0.12f, MagW = 0.03f, MagD = 0.04f, Body = GunKit.Polymer, Furniture = GunKit.Polymer },
                new GunExtra { Category = "Shotguns", Allowed = "reddot holo comp vgrip agrip laser", Launch = 4f, LaunchRange = 3f });

            // ---------------- heavy
            d = RifleDef("m249", "M249 SAW", "5.56mm light machine gun. 100-round box, bipod, open bolt. Slows you down.", "gun_m249");
            d.Damage = 40; d.Rpm = 800; d.Auto = true; d.HipSpread = 3f; d.AimSpread = 0.6f; d.Recoil = 1.3f; d.Penetration = 1.4f; d.Force = 18; d.Caliber = 5.56f; d.Mag = 100;
            d.ReloadTime = 5f; d.EmptyExtra = 0.6f; d.WeightKg = 7.5f; d.NoiseRadius = 150f; d.HipPos = new Vector3(0.15f, -0.21f, 0.28f); d.AimFov = 0.7f; d.AimDistance = 0.18f;
            Gun(d, new GunSpec { Bore = 0.06f, RecBack = -0.1f, RecFront = 0.2f, RecH = 0.08f, RecW = 0.04f, Muzzle = 0.64f, BarrelR = 0.01f, Rail = true, Bipod = true, FlashHider = true, GasTube = true,
                Guard = GunGuard.Rail, GuardStart = 0.2f, GuardEnd = 0.34f, GuardR = 0.022f, Stock = GunStock.Heavy, StockLen = 0.26f,
                Mag = GunMag.Box, MagZ = 0.06f, MagLen = 0.11f, MagD = 0.09f, Body = GunKit.DarkSteel, Furniture = GunKit.Polymer, MetalBody = true },
                new GunExtra { Category = "Heavy", Allowed = "reddot holo scope supp comp laser", OpenBolt = true });

            d = RifleDef("barrett", "Barrett .50", "Semi-auto .50 BMG anti-materiel rifle with a scope (hold RMB). Goes through everything and sends bodies flying.", "gun_barrett");
            d.Damage = 240; d.Rpm = 110; d.HipSpread = 5f; d.AimSpread = 0f; d.Recoil = 7.5f; d.Penetration = 6f; d.Force = 160; d.Range = 1200f; d.Caliber = 12.7f; d.Mag = 10;
            d.ReloadTime = 3.2f; d.EmptyExtra = 0.6f; d.WeightKg = 13f; d.NoiseRadius = 260f; d.HipPos = new Vector3(0.13f, -0.18f, 0.1f); d.AimFov = 0.17f; d.AimDistance = 0.09f;
            Gun(d, new GunSpec { Bore = 0.065f, RecBack = -0.12f, RecFront = 0.32f, RecH = 0.09f, RecW = 0.045f, Muzzle = 0.98f, BarrelR = 0.013f, Rail = true, Scope = true, Brake = true, Bipod = true,
                Guard = GunGuard.Shroud, GuardStart = 0.32f, GuardEnd = 0.56f, GuardR = 0.026f, Stock = GunStock.Heavy, StockLen = 0.3f,
                Mag = GunMag.Stick, MagZ = 0.1f, MagLen = 0.1f, MagW = 0.034f, MagD = 0.09f, MagAngle = 0f, Body = GunKit.Gunmetal, Furniture = GunKit.Polymer, MetalBody = true },
                new GunExtra { Category = "Heavy", Allowed = "ext supp comp laser", Scoped = true, Launch = 14f, LaunchRange = 300f, Mode = "SEMI .50 BMG" });
        }

        // ------------------------------------------------------------------ data: melee

        static WeaponDef MeleeDef(string id, string name, string glyph, string desc, float dmg, float force, float kg, bool twoHanded) => new WeaponDef
        {
            Id = id, Name = name, Glyph = glyph, Description = desc, Damage = dmg, Force = force, WeightKg = kg,
            HipPos = twoHanded ? new Vector3(0.14f, -0.2f, 0.3f) : new Vector3(0.15f, -0.17f, 0.32f),
        };

        static readonly Color Tape = new Color(0.08f, 0.08f, 0.09f), Ash = new Color(0.78f, 0.6f, 0.38f), Steel = new Color(0.7f, 0.71f, 0.74f), RedPaint = new Color(0.62f, 0.07f, 0.05f);

        static void DefineMelee()
        {
            Melee(MeleeDef("bat", "Baseball Bat", "bat", "Two-handed swings that break bones and knock bodies flat. Click again to combo, RMB = home run swing.", 38, 45, 1f, true),
                new MeleeSpec { Head = MeleeHead.Bat, TwoHanded = true, SwingTime = 0.48f, Reach = 2f, Launch = 7f, CrushChance = 0.25f, HandleLen = 0.32f, HeadLen = 0.52f,
                    HeadColor = Ash, HandleColor = Tape, SwingSound = "melee_swing_heavy", FleshSound = "blunt_flesh", WorldSound = "wood_thock" });
            Melee(MeleeDef("spikedbat", "Spiked Bat", "bat", "A bat full of nails and barbed wire. Every hit bleeds. RMB = big overhead.", 46, 40, 1.3f, true),
                new MeleeSpec { Head = MeleeHead.SpikedBat, TwoHanded = true, SwingTime = 0.52f, Reach = 2f, Launch = 6f, CrushChance = 0.3f, Bleeds = true, HandleLen = 0.32f, HeadLen = 0.52f,
                    HeadColor = new Color(0.55f, 0.4f, 0.25f), HandleColor = Tape, SwingSound = "melee_swing_heavy", FleshSound = "blunt_flesh", WorldSound = "wood_thock" });
            Melee(MeleeDef("crowbar", "Crowbar", "wrench", "Fast one-handed steel. Cracks skulls. RMB = overhead smash.", 34, 30, 1.5f, false),
                new MeleeSpec { Head = MeleeHead.Crowbar, SwingTime = 0.38f, Reach = 1.75f, Launch = 4f, CrushChance = 0.15f, HandleLen = 0.3f, HeadLen = 0.25f,
                    HeadColor = RedPaint, HandleColor = Tape, SwingSound = "melee_swing", FleshSound = "blunt_flesh", WorldSound = "blunt_world" });
            Melee(MeleeDef("pipe", "Lead Pipe", "hammer", "Heavy pipe with a taped grip. Breaks bones. RMB = overhead smash.", 36, 34, 2f, false),
                new MeleeSpec { Head = MeleeHead.Pipe, SwingTime = 0.42f, Reach = 1.75f, Launch = 5f, CrushChance = 0.2f, HandleLen = 0.3f, HeadLen = 0.2f,
                    HeadColor = new Color(0.42f, 0.43f, 0.45f), HandleColor = new Color(0.15f, 0.15f, 0.16f), SwingSound = "melee_swing", FleshSound = "blunt_flesh", WorldSound = "blunt_world" });
            Melee(MeleeDef("pan", "Frying Pan", "hammer", "BONK. Sends Goredolls flying. RMB = full-power smack.", 30, 40, 1.6f, false),
                new MeleeSpec { Head = MeleeHead.Pan, SwingTime = 0.4f, Reach = 1.75f, Launch = 9f, CrushChance = 0.05f,
                    HeadColor = new Color(0.16f, 0.16f, 0.17f), HandleColor = new Color(0.12f, 0.1f, 0.09f), SwingSound = "melee_swing", FleshSound = "pan_bonk", WorldSound = "pan_bonk" });
            Melee(MeleeDef("machete", "Machete", "knife", "Long jungle blade. Fast slashes that hack limbs off. RMB = overhead chop.", 46, 14, 0.7f, false),
                new MeleeSpec { Head = MeleeHead.Machete, Blade = true, SwingTime = 0.36f, Reach = 1.85f, Pierce = 2, CutChance = 0.4f, HandleLen = 0.13f, HeadLen = 0.42f,
                    HeadColor = Steel, HandleColor = Tape, SwingSound = "melee_swing", FleshSound = "slice", WorldSound = "knife_hit_hard" });
            Melee(MeleeDef("cleaver", "Meat Cleaver", "knife", "Butcher's cleaver. Short reach, chops through bone. RMB = heavy chop.", 40, 12, 0.6f, false),
                new MeleeSpec { Head = MeleeHead.Cleaver, Blade = true, SwingTime = 0.34f, Reach = 1.55f, CutChance = 0.38f, HandleLen = 0.12f, HeadLen = 0.19f,
                    HeadColor = Steel, HandleColor = new Color(0.4f, 0.25f, 0.13f), SwingSound = "melee_swing", FleshSound = "blade_chop", WorldSound = "knife_hit_hard" });
            Melee(MeleeDef("cknife", "Combat Knife", "knife", "Serrated combat knife. Very fast slashes that bleed, RMB = deep stab.", 26, 10, 0.3f, false),
                new MeleeSpec { Head = MeleeHead.CombatKnife, Blade = true, SwingTime = 0.28f, Reach = 1.55f, CutChance = 0.2f, RmbStab = true, Bleeds = true, HandleLen = 0.12f, HeadLen = 0.17f,
                    HeadColor = new Color(0.3f, 0.31f, 0.33f), HandleColor = Tape, SwingSound = "whoosh_small", FleshSound = "slice", WorldSound = "knife_hit_hard" });
            Melee(MeleeDef("fireaxe", "Fire Axe", "axe", "Hold LMB to wind up, release to chop: lops limbs and heads off. RMB = quick swing.", 75, 30, 2.7f, true),
                new MeleeSpec { Head = MeleeHead.FireAxe, Blade = true, TwoHanded = true, ChargeSwing = true, SwingTime = 0.5f, Reach = 2.1f, CutChance = 0.55f, HandleLen = 0.78f,
                    HeadColor = RedPaint, HandleColor = new Color(0.62f, 0.44f, 0.26f), SwingSound = "melee_swing_heavy", FleshSound = "blade_chop", WorldSound = "blunt_world" });
            Melee(MeleeDef("chainsaw", "Chainsaw", "katana", "Hold LMB to rev and cut: grinds through anything it touches and saws limbs off.", 170, 10, 6f, true),
                new MeleeSpec { Head = MeleeHead.Chainsaw, Blade = true, TwoHanded = true, Continuous = true, Reach = 1.65f, HeadLen = 0.4f,
                    HeadColor = new Color(0.95f, 0.45f, 0.08f), HandleColor = Tape });
            // Chainsaw sits low and forward like the minigun.
            melee["chainsaw"].Def.HipPos = new Vector3(0.12f, -0.25f, 0.22f);
        }
    }

    // ====================================================================== the gun class

    /// <summary>
    /// An arsenal-pack firearm: a <see cref="Firearm"/> plus lever cycling, break-action barrels, open bolt,
    /// a built-in scope and point-blank body launching, all switched by <see cref="ArsenalPack.GunExtra"/>.
    /// </summary>
    public sealed class ArsenalGun : Firearm
    {
        public GunSpec Spec;
        public ArsenalPack.GunExtra Extra;
        float cycleT = -1f;
        bool cycleSound, hingeOpen;
        Transform hinge;

        public override bool OpenBolt => Extra != null && Extra.OpenBolt;
        protected override bool CanShoot => cycleT < 0f;
        public override string ModeText => Extra != null && !string.IsNullOrEmpty(Extra.Mode) ? Extra.Mode : base.ModeText;
        public override float MoveSpeedMul => Def.WeightKg >= 7f ? Mathf.Lerp(0.9f, 0.75f, Mathf.InverseLerp(7f, 14f, Def.WeightKg)) : 1f;

        protected override void OnAttach()
        {
            base.OnAttach();
            hinge = Model.transform.Find("Hinge");
            if (Extra != null && Extra.Scoped && Mods.Optic == 0)
            {
                // Built-in scope: same full-screen picture as the 4x attachment, at the gun's own zoom.
                Mods.Optic = 3;
                Mods.AimFov = Def.AimFov;
                Mods.AimDistance = Def.AimDistance;
            }
        }

        protected override void OnHolster()
        {
            base.OnHolster();
            cycleT = -1f;
            if (hinge != null) hinge.localRotation = Quaternion.identity;
            if (Model.Charge != null && Spec != null && Spec.Lever) Model.Charge.localRotation = Quaternion.identity;
        }

        protected override void OnShot()
        {
            if (Extra == null) return;
            if (Extra.Cycle > 0f) { cycleT = 0f; cycleSound = false; }
            if (Extra.Launch > 0f && NetState.Authority &&
                Physics.SphereCast(Eye, 0.1f, Aim, out var hit, Extra.LaunchRange, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var part = hit.collider.GetComponent<BodyPart>();
                if (part != null && part.Doll != null)
                {
                    float k = Extra.LaunchRange > 20f ? 1f : 1f - hit.distance / Extra.LaunchRange;
                    part.Doll.Launch(Aim * Extra.Launch * Mathf.Lerp(0.35f, 1f, k) + Vector3.up * 1.5f * k);
                    BloodFX.Burst(hit.point, Aim, 1.5f + 2f * k, new Color(0.45f, 0.02f, 0.02f));
                    BloodFX.Mist(hit.point, Aim, 1f + k);
                }
            }
            if (Def.Caliber >= 12f && Def.Damage > 150f) CameraFX.Shake(0.35f);
        }

        public override void Tick(InputFrame f, float dt)
        {
            base.Tick(f, dt);
            // Lever: throw the loop down and back after every shot.
            if (cycleT >= 0f)
            {
                cycleT += dt / Extra.Cycle;
                if (!cycleSound && cycleT > 0.15f) { cycleSound = true; AudioManager.Play(Def.Charge, Eye, 0.7f, Random.Range(0.96f, 1.04f), 0f); }
                float open = Bell(cycleT, 0.1f, 0.9f);
                if (Model.Charge != null && Spec.Lever) Model.Charge.localRotation = Quaternion.Euler(-55f * open, 0f, 0f);
                Vm.TargetRot *= Quaternion.Euler(-4f * open, 0f, -5f * open);
                if (cycleT >= 1f) { cycleT = -1f; if (Model.Charge != null) Model.Charge.localRotation = Quaternion.identity; }
            }
            // Break action: barrels tip down while shells go in.
            if (hinge != null)
            {
                float p = ReloadProgress;
                float open = p < 0f ? 0f : Ease(Ramp(p, 0f, 0.1f)) * (1f - Ease(Ramp(p, 0.9f, 1f)));
                hinge.localRotation = Quaternion.Euler(32f * open, 0f, 0f);
                if (open > 0.6f && !hingeOpen) { hingeOpen = true; AudioManager.Play("break_open", Eye, 0.65f, 1f, 0f); }
                else if (open < 0.3f && hingeOpen) { hingeOpen = false; AudioManager.Play("break_close", Eye, 0.7f, 1f, 0f); }
            }
        }
    }
}
