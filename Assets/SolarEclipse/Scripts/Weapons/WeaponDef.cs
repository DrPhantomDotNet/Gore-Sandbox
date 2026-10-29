using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum WeaponKind { Fists, Knife, Firearm, Admin, SpawnGun, Explosive, Melee, RopeTool, GravityGun }

    /// <summary>Data for one weapon (add new guns here without touching weapon code).</summary>
    public sealed class WeaponDef
    {
        public string Id, Name, Description, Glyph;
        public WeaponKind Kind;
        public float Damage, Rpm = 400f, HipSpread = 1f, AimSpread = 0.1f, Recoil = 1f, Range = 400f, Penetration = 0.4f, Force = 10f, Caliber = 9f;
        public int Mag = 1, Pellets = 1, CasingType;
        public bool Auto, ShellReload;
        public float ReloadTime = 1.8f, EmptyExtra = 0.4f, AimFov = 0.78f, AimDistance = 0.18f, NoiseRadius = 80f;   // AimDistance: eye → rear sight (m) when aiming
        public string FireSound, MagOut, MagIn, Charge;
        public Vector3 HipPos = new Vector3(0.13f, -0.15f, 0.32f);
        public float WeightKg = 1f;
    }

    public static class WeaponCatalog
    {
        public static readonly List<WeaponDef> All = new List<WeaponDef>
        {
            new WeaponDef { Id = "fists", Name = "Fists", Kind = WeaponKind.Fists, Glyph = "fist", Description = "Left and right punches. Hold RMB to block.", Damage = 12, Force = 30 },
            new WeaponDef { Id = "spawngun", Name = "Spawn Gun", Kind = WeaponKind.SpawnGun, Glyph = "spawngun", Description = "Spawn, grab, move, rotate, freeze, scale, throw and delete anything." },
            new WeaponDef
            {
                Id = "glock", Name = "Glock 19", Kind = WeaponKind.Firearm, Glyph = "gun", Description = "Semi-automatic 9mm pistol. 15+1 rounds.",
                Damage = 32, Rpm = 450, HipSpread = 1.2f, AimSpread = 0.12f, Recoil = 1.8f, Penetration = 0.45f, Force = 9, Caliber = 9f, Mag = 15,
                ReloadTime = 1.55f, EmptyExtra = 0.35f, FireSound = "gun_glock", MagOut = "mag_out_pistol", MagIn = "mag_in_pistol", Charge = "slide_back",
                HipPos = new Vector3(0.12f, -0.15f, 0.33f), AimFov = 0.82f, AimDistance = 0.2f, CasingType = 0, WeightKg = 0.85f,
            },
            new WeaponDef { Id = "knife", Name = "Knife", Kind = WeaponKind.Knife, Glyph = "knife", Description = "LMB slash (slices limbs), RMB stab (deep wounds).", Damage = 30, Force = 15 },
            new WeaponDef
            {
                Id = "uzi", Name = "UZI", Kind = WeaponKind.Firearm, Glyph = "smg", Description = "Fully automatic 9mm submachine gun. 32 rounds. B toggles fire mode.",
                Damage = 25, Rpm = 620, Auto = true, HipSpread = 2.6f, AimSpread = 0.9f, Recoil = 1.1f, Penetration = 0.35f, Force = 7, Caliber = 9f, Mag = 32,
                ReloadTime = 1.9f, EmptyExtra = 0.4f, FireSound = "gun_uzi", MagOut = "mag_out_smg", MagIn = "mag_in_smg", Charge = "charging_handle",
                HipPos = new Vector3(0.13f, -0.15f, 0.3f), AimFov = 0.8f, AimDistance = 0.2f, CasingType = 0, WeightKg = 3.5f,
            },
            new WeaponDef
            {
                Id = "ar15", Name = "AR-15", Kind = WeaponKind.Firearm, Glyph = "rifle", Description = "Semi-automatic 5.56mm rifle. 30 rounds. Punches through bodies and severs limbs.",
                Damage = 55, Rpm = 560, HipSpread = 1.5f, AimSpread = 0.05f, Recoil = 2.3f, Penetration = 1.4f, Force = 22, Caliber = 5.56f, Mag = 30,
                ReloadTime = 2.2f, EmptyExtra = 0.45f, FireSound = "gun_ar15", MagOut = "mag_out_rifle", MagIn = "mag_in_rifle", Charge = "charging_handle",
                HipPos = new Vector3(0.12f, -0.16f, 0.2f), AimFov = 0.62f, AimDistance = 0.16f, CasingType = 1, WeightKg = 3.2f, NoiseRadius = 120f,
            },
            new WeaponDef
            {
                Id = "ar17", Name = "AR-17", Kind = WeaponKind.Firearm, Glyph = "shotgun", Description = "ArmaLite AR-17: semi-automatic 12 gauge shotgun. 2 shells, loaded one at a time.",
                Damage = 16, Pellets = 9, Rpm = 260, HipSpread = 4.2f, AimSpread = 3.2f, Recoil = 5f, Penetration = 0.25f, Force = 14, Caliber = 8.4f, Mag = 2,
                ReloadTime = 0.55f, EmptyExtra = 0.45f, ShellReload = true, FireSound = "gun_ar17", MagIn = "shell_in", Charge = "charging_handle",
                HipPos = new Vector3(0.12f, -0.16f, 0.16f), AimFov = 0.8f, AimDistance = 0.17f, CasingType = 2, WeightKg = 2.9f, NoiseRadius = 110f,
            },
            new WeaponDef
            {
                Id = "admin", Name = "Admin Gun", Kind = WeaponKind.Admin, Glyph = "admin", Description = "Developer tool. Modes (B): Smite, Gib, Launch, Revive, Delete. Infinite ammo.",
                Damage = 500, Rpm = 300, HipSpread = 0, AimSpread = 0, Recoil = 2f, Penetration = 6f, Force = 60, Caliber = 12f, Mag = 1,
                FireSound = "gun_admin", HipPos = new Vector3(0.12f, -0.15f, 0.3f), AimFov = 0.8f, AimDistance = 0.2f, NoiseRadius = 60f,
            },
            // ---- explosives & heavy melee (Weapons/Explosives.cs, RocketLauncher.cs, Grenade.cs, C4.cs, Katana.cs, Sledgehammer.cs)
            new WeaponDef
            {
                Id = "rpg", Name = "Rocket Launcher", Kind = WeaponKind.Explosive, Glyph = "rocket", Description = "Fires a rocket that explodes on impact. Single shot, R reloads. RMB aims through the optic.",
                Damage = 260, Force = 6, Mag = 1, Recoil = 6f, ReloadTime = 2.4f, FireSound = "rocket_fire", HipPos = new Vector3(0.16f, -0.11f, 0.05f),
                AimFov = 0.7f, AimDistance = 0.12f, WeightKg = 7f, NoiseRadius = 150f,
            },
            new WeaponDef
            {
                Id = "frag", Name = "Frag Grenade", Kind = WeaponKind.Explosive, Glyph = "grenade", Description = "Hold LMB to pull the pin and cook (3.5 s fuse), release to throw. RMB = short underhand toss. Carry 5.",
                Damage = 200, Force = 5, Mag = 5, HipPos = new Vector3(0.15f, -0.17f, 0.3f), WeightKg = 0.4f, NoiseRadius = 140f,
            },
            new WeaponDef
            {
                Id = "c4", Name = "C4", Kind = WeaponKind.Explosive, Glyph = "c4", Description = "LMB throws a sticky charge (sticks to walls, props and Goredolls, up to 8). RMB detonates them all.",
                Damage = 300, Force = 6, Mag = 8, HipPos = new Vector3(0.15f, -0.17f, 0.3f), WeightKg = 0.6f, NoiseRadius = 150f,
            },
            new WeaponDef
            {
                Id = "katana", Name = "Katana", Kind = WeaponKind.Melee, Glyph = "katana", Description = "Fast alternating slashes that cut limbs clean off. Keep clicking for combos. RMB = overhead cleave.",
                Damage = 70, Force = 18, HipPos = new Vector3(0.14f, -0.2f, 0.3f), WeightKg = 1.2f,
            },
            new WeaponDef
            {
                Id = "sledgehammer", Name = "Sledgehammer", Kind = WeaponKind.Melee, Glyph = "hammer", Description = "Hold LMB to wind up, release to swing. Crushes skulls, breaks bones and launches bodies.",
                Damage = 90, Force = 60, HipPos = new Vector3(0.16f, -0.24f, 0.25f), WeightKg = 5f,
            },
            // ---- Gun pack 2 (Weapons/Guns2.cs, WeaponModels2.cs, Audio/RecipesGuns2.cs)
            new WeaponDef
            {
                Id = "shotgun", Name = "Shotgun", Kind = WeaponKind.Firearm, Glyph = "shotgun", Description = "Pump-action 12 gauge. 10 pellets, 6 shells loaded one at a time. Devastating up close.",
                Damage = 15, Pellets = 10, Rpm = 75, HipSpread = 5.2f, AimSpread = 4f, Recoil = 6f, Penetration = 0.3f, Force = 26, Caliber = 8.4f, Mag = 6,
                ReloadTime = 0.5f, EmptyExtra = 0.5f, ShellReload = true, FireSound = "gun_shotgun", MagIn = "shell_in", Charge = "pump_action",
                HipPos = new Vector3(0.12f, -0.16f, 0.15f), AimFov = 0.8f, AimDistance = 0.17f, CasingType = 2, WeightKg = 3.6f, NoiseRadius = 120f,
            },
            new WeaponDef
            {
                Id = "sniper", Name = "Sniper", Kind = WeaponKind.Firearm, Glyph = "rifle", Description = "Bolt-action .338 rifle with a 5x scope (hold RMB). 5 rounds. One-shot headshots.",
                Damage = 160, Rpm = 50, HipSpread = 4f, AimSpread = 0f, Recoil = 5f, Penetration = 3.5f, Force = 90, Caliber = 8.6f, Mag = 5, Range = 900f,
                ReloadTime = 2.6f, EmptyExtra = 0.6f, FireSound = "gun_sniper", MagOut = "mag_out_rifle", MagIn = "mag_in_rifle", Charge = "bolt_cycle",
                HipPos = new Vector3(0.12f, -0.16f, 0.12f), AimFov = 0.2f, AimDistance = 0.09f, CasingType = 1, WeightKg = 6.5f, NoiseRadius = 200f,
            },
            new WeaponDef
            {
                Id = "revolver", Name = "Revolver", Kind = WeaponKind.Firearm, Glyph = "gun", Description = ".357 Magnum revolver. 6 heavy rounds, big recoil, speedloader reload.",
                Damage = 70, Rpm = 160, HipSpread = 1.4f, AimSpread = 0.08f, Recoil = 4f, Penetration = 0.9f, Force = 30, Caliber = 9.1f, Mag = 6,
                ReloadTime = 2.3f, EmptyExtra = 0f, FireSound = "gun_revolver", MagOut = "revolver_open", MagIn = "speedloader", Charge = "hammer_cock",
                HipPos = new Vector3(0.12f, -0.15f, 0.33f), AimFov = 0.8f, AimDistance = 0.22f, CasingType = 0, WeightKg = 1.2f, NoiseRadius = 110f,
            },
            new WeaponDef
            {
                Id = "minigun", Name = "Minigun", Kind = WeaponKind.Firearm, Glyph = "rifle", Description = "Six-barrel 7.62mm rotary gun. Hold LMB to spin up and shred. 300 round box. Slows you down.",
                Damage = 30, Rpm = 2400, Auto = true, HipSpread = 3.2f, AimSpread = 3.2f, Recoil = 0.45f, Penetration = 1.1f, Force = 14, Caliber = 7.62f, Mag = 300,
                ReloadTime = 4.2f, EmptyExtra = 0f, FireSound = "gun_minigun", MagOut = "mag_out_rifle", MagIn = "mag_in_rifle", Charge = "charging_handle",
                HipPos = new Vector3(0.17f, -0.27f, 0.36f), AimFov = 0.95f, AimDistance = 0.3f, CasingType = 1, WeightKg = 18f, NoiseRadius = 160f,
            },
            new WeaponDef
            {
                Id = "harpoon", Name = "Harpoon Gun", Kind = WeaponKind.Firearm, Glyph = "rifle", Description = "Fires a barbed harpoon that impales bodies and pins them to walls and props.",
                Damage = 90, Rpm = 60, HipSpread = 1.5f, AimSpread = 0.1f, Recoil = 3f, Penetration = 0f, Force = 0, Caliber = 12f, Mag = 1, Range = 120f,
                ReloadTime = 1.1f, EmptyExtra = 0.3f, ShellReload = true, FireSound = "gun_harpoon", MagIn = "harpoon_load", Charge = "harpoon_load",
                HipPos = new Vector3(0.12f, -0.16f, 0.12f), AimFov = 0.78f, AimDistance = 0.17f, CasingType = 0, WeightKg = 3f, NoiseRadius = 40f,
            },
            // Sandbox tools (Creative)
            new WeaponDef { Id = "ropetool", Name = "Rope Tool", Kind = WeaponKind.RopeTool, Glyph = "rope", Description = "LMB end A, LMB end B: tie props, Goredolls and the world together (hang them from ceilings). RMB cuts a rope, R: tight / slack / long.", HipPos = new Vector3(0.13f, -0.15f, 0.3f) },
            new WeaponDef { Id = "gravitygun", Name = "Gravity Gun", Kind = WeaponKind.GravityGun, Glyph = "gravgun", Description = "RMB pulls and holds objects and Goredolls, LMB punts them (or anything in front of you).", HipPos = new Vector3(0.13f, -0.16f, 0.3f) },
            // ---- Gun pack 3 (Weapons/Mac11.cs, DualKatanas.cs, WeaponModels4.cs, Audio/RecipesGuns3.cs). Append only: the index is sent over the network.
            new WeaponDef
            {
                Id = "mac11", Name = "MAC-11", Kind = WeaponKind.Firearm, Glyph = "smg", Description = "Tiny .380 machine pistol. 1150 rounds per minute, 32-round mag in the grip. Climbs hard, sprays wide at range.",
                Damage = 21, Rpm = 1150, Auto = true, HipSpread = 3.2f, AimSpread = 1.25f, Recoil = 0.95f, Penetration = 0.3f, Force = 6, Caliber = 9f, Mag = 32, Range = 140f,
                ReloadTime = 1.65f, EmptyExtra = 0.4f, FireSound = "gun_mac11", MagOut = "mag_out_smg", MagIn = "mag_in_smg", Charge = "charging_handle",
                HipPos = new Vector3(0.12f, -0.14f, 0.32f), AimFov = 0.82f, AimDistance = 0.21f, CasingType = 0, WeightKg = 1.6f, NoiseRadius = 90f,
            },
            new WeaponDef
            {
                Id = "katana_dual", Name = "Dual Katanas", Kind = WeaponKind.Melee, Glyph = "katana", Description = "A katana in each hand. LMB = fast alternating left/right slashes (combo). RMB = both blades cross-slash: huge damage, cuts almost always.",
                Damage = 58, Force = 16, HipPos = new Vector3(0.14f, -0.2f, 0.3f), WeightKg = 2.4f,
            },
        };

        public static WeaponDef Get(string id) => All.Find(w => w.Id == id);
    }
}
