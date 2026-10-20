using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum WeaponKind { Fists, Knife, Firearm, Admin, SpawnGun }

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
        };

        public static WeaponDef Get(string id) => All.Find(w => w.Id == id);
    }
}
