using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>One primitive part of a modded weapon's model (model space: +Z forward, metres).</summary>
    [Serializable]
    public sealed class WPart
    {
        public string name = "Part";
        public int shape;               // WeaponDesign.Shapes
        public Vector3 pos;
        public Vector3 rot;             // euler degrees
        public Vector3 size = new Vector3(0.03f, 0.04f, 0.12f);
        public Color color = new Color(0.2f, 0.2f, 0.22f, 1f);
        public int mat;                 // WeaponDesign.Materials

        public WPart Clone() => new WPart { name = name, shape = shape, pos = pos, rot = rot, size = size, color = color, mat = mat };
    }

    /// <summary>
    /// Everything the Weapon Editor makes: the parts list, the special points (bullet source, hold points, sights,
    /// shell eject), the trigger and all stats. Stored as JSON in <see cref="ModPackage.data"/>. Pure data.
    /// </summary>
    [Serializable]
    public sealed class WeaponDesign
    {
        public static readonly string[] Shapes = { "Box", "Cylinder", "Sphere", "Capsule", "Wedge", "Tube" };
        public static readonly string[] Materials = { "Metal", "Polymer", "Wood", "Glow" };
        public static readonly string[] Triggers = { "Semi-auto", "Full auto", "Burst", "Charge (hold, release)", "Pump / bolt action" };
        public static readonly string[] Projectiles = { "Hitscan bullet", "Visible bolt", "Explosive round" };
        public static readonly string[] ReloadStyles = { "Magazine swap", "One round at a time" };
        public static readonly string[] MeleeTypes = { "Blade (cuts limbs)", "Blunt (smashes, knocks back)" };
        public static readonly string[] Sounds = { "gun_glock", "gun_uzi", "gun_ar15", "gun_ar17", "gun_shotgun", "gun_sniper", "gun_revolver", "gun_minigun", "gun_mac11", "gun_harpoon", "gun_suppressed", "gun_admin", "rocket_fire" };
        public static readonly string[] SoundNames = { "Pistol", "SMG", "Rifle", "Light shotgun", "Shotgun", "Sniper", "Revolver", "Minigun", "Machine pistol", "Harpoon", "Suppressed", "Laser", "Rocket" };
        public static readonly string[] Icons = { "gun", "smg", "rifle", "shotgun", "rocket", "knife", "katana", "hammer", "admin" };

        public int formatVersion = 1;
        public List<WPart> parts = new List<WPart>();
        public int magPart = -1, slidePart = -1;

        // Special points (model space)
        public Vector3 muzzlePos = new Vector3(0f, 0.05f, 0.2f);
        public Vector3 muzzleRot;                       // euler: direction the flash / bolts leave
        public Vector3 gripPos = new Vector3(0f, -0.04f, -0.06f);
        public bool useSupport = true;
        public Vector3 supportPos = new Vector3(-0.02f, -0.05f, 0f);
        public Vector3 sightRear = new Vector3(0f, 0.075f, -0.05f), sightFront = new Vector3(0f, 0.075f, 0.17f);
        public Vector3 ejectPos = new Vector3(0.015f, 0.06f, 0.05f);

        // Trigger
        public int trigger;                             // Triggers
        public int burstCount = 3;
        public float chargeTime = 1f;

        // Fire
        public float rpm = 450f, damage = 30f;
        public int pellets = 1;
        public float hipSpread = 1.5f, aimSpread = 0.2f, range = 300f, force = 10f, penetration = 0.5f;
        public int projectile;                          // Projectiles
        public float projectileSpeed = 60f, explosionRadius = 3f;
        public bool alwaysTracers;
        public Color tracer = new Color(1f, 0.85f, 0.5f, 1f);

        // Ammo
        public int magSize = 15;
        public float reloadTime = 1.8f;
        public int reloadStyle;                         // ReloadStyles

        // Recoil + handling
        public float recoilV = 1.5f, recoilH = 0.3f, kick = 1f;
        public float aimFov = 0.8f, aimSpeed = 1f, moveSpeed = 1f;
        public float weight = 2f;

        // Sound
        public int sound;
        public float pitch = 1f;

        // Melee
        public bool melee;
        public int meleeType;
        public float meleeReach = 1.6f, meleeDamage = 40f, meleeSpeed = 1f;

        public int icon;

        public string FireSound => Sounds[Mathf.Clamp(sound, 0, Sounds.Length - 1)];
        public string Glyph => Icons[Mathf.Clamp(icon, 0, Icons.Length - 1)];

        public WeaponDesign Clone() => JsonUtility.FromJson<WeaponDesign>(JsonUtility.ToJson(this));

        // ------------------------------------------------------------------ templates

        static WPart P(string n, int shape, Vector3 pos, Vector3 size, Color c, int mat = 0, Vector3 rot = default) =>
            new WPart { name = n, shape = shape, pos = pos, size = size, color = c, mat = mat, rot = rot };

        static readonly Color Black = new Color(0.11f, 0.11f, 0.12f), Steel = new Color(0.24f, 0.24f, 0.26f), WoodC = new Color(0.45f, 0.28f, 0.15f), Silver = new Color(0.7f, 0.72f, 0.75f);

        public static readonly string[] Templates = { "Pistol", "Rifle", "Shotgun", "Blaster", "Knife", "Bat", "Blank" };

        public static WeaponDesign Template(int t)
        {
            var d = new WeaponDesign();
            switch (t)
            {
                case 0: // pistol
                    d.parts.Add(P("Slide", 0, new Vector3(0, 0.03f, 0.07f), new Vector3(0.028f, 0.03f, 0.17f), Steel));
                    d.parts.Add(P("Frame", 0, new Vector3(0, 0.005f, 0.06f), new Vector3(0.026f, 0.02f, 0.15f), Black, 1));
                    d.parts.Add(P("Grip", 0, new Vector3(0, -0.04f, -0.01f), new Vector3(0.028f, 0.1f, 0.045f), Black, 1, new Vector3(-15, 0, 0)));
                    d.parts.Add(P("Magazine", 0, new Vector3(0, -0.05f, -0.005f), new Vector3(0.02f, 0.09f, 0.03f), Steel, 0, new Vector3(-15, 0, 0)));
                    d.slidePart = 0; d.magPart = 3;
                    d.muzzlePos = new Vector3(0, 0.03f, 0.16f); d.gripPos = new Vector3(0, -0.04f, -0.02f); d.supportPos = new Vector3(-0.03f, -0.06f, 0f);
                    d.sightRear = new Vector3(0, 0.05f, -0.005f); d.sightFront = new Vector3(0, 0.05f, 0.15f); d.ejectPos = new Vector3(0.015f, 0.045f, 0.08f);
                    d.icon = 0; d.sound = 0;
                    break;
                case 1: // rifle
                    d.parts.Add(P("Receiver", 0, new Vector3(0, 0.03f, 0.08f), new Vector3(0.032f, 0.05f, 0.26f), Black));
                    d.parts.Add(P("Barrel", 1, new Vector3(0, 0.04f, 0.38f), new Vector3(0.016f, 0.016f, 0.36f), Steel));
                    d.parts.Add(P("Handguard", 1, new Vector3(0, 0.04f, 0.32f), new Vector3(0.04f, 0.04f, 0.24f), Black, 1));
                    d.parts.Add(P("Stock", 4, new Vector3(0, 0.0f, -0.17f), new Vector3(0.03f, 0.08f, 0.2f), Black, 1, new Vector3(0, 180, 0)));
                    d.parts.Add(P("Grip", 0, new Vector3(0, -0.04f, -0.04f), new Vector3(0.026f, 0.09f, 0.035f), Black, 1, new Vector3(-18, 0, 0)));
                    d.parts.Add(P("Magazine", 0, new Vector3(0, -0.05f, 0.08f), new Vector3(0.022f, 0.12f, 0.05f), Steel, 0, new Vector3(10, 0, 0)));
                    d.parts.Add(P("Bolt", 0, new Vector3(0.017f, 0.04f, 0.05f), new Vector3(0.006f, 0.012f, 0.05f), Silver));
                    d.magPart = 5; d.slidePart = 6;
                    d.muzzlePos = new Vector3(0, 0.04f, 0.56f); d.gripPos = new Vector3(0, -0.04f, -0.05f); d.supportPos = new Vector3(-0.005f, 0.01f, 0.3f);
                    d.sightRear = new Vector3(0, 0.075f, -0.02f); d.sightFront = new Vector3(0, 0.075f, 0.42f); d.ejectPos = new Vector3(0.017f, 0.045f, 0.06f);
                    d.trigger = 1; d.rpm = 650; d.damage = 40; d.magSize = 30; d.reloadTime = 2.2f; d.recoilV = 1.6f; d.aimFov = 0.65f; d.icon = 2; d.sound = 2; d.range = 400; d.penetration = 1.2f; d.weight = 3.4f;
                    break;
                case 2: // shotgun
                    d.parts.Add(P("Receiver", 0, new Vector3(0, 0.03f, 0.08f), new Vector3(0.036f, 0.05f, 0.22f), Black));
                    d.parts.Add(P("Barrel", 1, new Vector3(0, 0.045f, 0.4f), new Vector3(0.024f, 0.024f, 0.44f), Steel));
                    d.parts.Add(P("Pump", 1, new Vector3(0, 0.015f, 0.32f), new Vector3(0.04f, 0.036f, 0.16f), WoodC, 2));
                    d.parts.Add(P("Stock", 4, new Vector3(0, -0.01f, -0.18f), new Vector3(0.034f, 0.09f, 0.24f), WoodC, 2, new Vector3(0, 180, 0)));
                    d.parts.Add(P("Grip", 0, new Vector3(0, -0.04f, -0.04f), new Vector3(0.028f, 0.09f, 0.04f), WoodC, 2, new Vector3(-20, 0, 0)));
                    d.slidePart = 2;
                    d.muzzlePos = new Vector3(0, 0.045f, 0.62f); d.gripPos = new Vector3(0, -0.04f, -0.05f); d.supportPos = new Vector3(-0.005f, -0.005f, 0.32f);
                    d.sightRear = new Vector3(0, 0.07f, 0.02f); d.sightFront = new Vector3(0, 0.07f, 0.6f); d.ejectPos = new Vector3(0.019f, 0.04f, 0.08f);
                    d.trigger = 4; d.rpm = 80; d.damage = 15; d.pellets = 9; d.hipSpread = 5; d.aimSpread = 4; d.magSize = 6; d.reloadStyle = 1; d.reloadTime = 0.5f; d.recoilV = 5; d.kick = 2; d.icon = 3; d.sound = 4; d.range = 120; d.weight = 3.6f;
                    break;
                case 3: // blaster
                    d.parts.Add(P("Body", 3, new Vector3(0, 0.035f, 0.08f), new Vector3(0.05f, 0.06f, 0.24f), new Color(0.85f, 0.85f, 0.9f), 1));
                    d.parts.Add(P("Emitter", 5, new Vector3(0, 0.035f, 0.23f), new Vector3(0.04f, 0.04f, 0.06f), Steel));
                    d.parts.Add(P("Core", 2, new Vector3(0, 0.035f, 0.23f), new Vector3(0.025f, 0.025f, 0.025f), new Color(0.2f, 0.9f, 1f), 3));
                    d.parts.Add(P("Grip", 0, new Vector3(0, -0.03f, 0f), new Vector3(0.026f, 0.09f, 0.04f), Black, 1, new Vector3(-15, 0, 0)));
                    d.parts.Add(P("Cell", 0, new Vector3(0, -0.04f, 0.07f), new Vector3(0.024f, 0.06f, 0.03f), new Color(0.2f, 0.9f, 1f), 3));
                    d.magPart = 4;
                    d.muzzlePos = new Vector3(0, 0.035f, 0.26f); d.gripPos = new Vector3(0, -0.03f, -0.01f); d.supportPos = new Vector3(-0.02f, -0.03f, 0.08f);
                    d.sightRear = new Vector3(0, 0.08f, 0.0f); d.sightFront = new Vector3(0, 0.08f, 0.18f);
                    d.projectile = 1; d.projectileSpeed = 45; d.damage = 45; d.rpm = 300; d.magSize = 20; d.tracer = new Color(0.2f, 0.9f, 1f); d.alwaysTracers = true; d.sound = 11; d.pitch = 1.3f; d.icon = 8;
                    break;
                case 4: // knife
                    d.melee = true; d.meleeType = 0; d.meleeDamage = 35; d.meleeReach = 1.5f; d.meleeSpeed = 1.3f; d.icon = 5;
                    d.parts.Add(P("Handle", 1, new Vector3(0, 0, 0), new Vector3(0.025f, 0.03f, 0.11f), Black, 1));
                    d.parts.Add(P("Guard", 0, new Vector3(0, 0, 0.06f), new Vector3(0.012f, 0.05f, 0.01f), Steel));
                    d.parts.Add(P("Blade", 4, new Vector3(0, 0.005f, 0.13f), new Vector3(0.004f, 0.03f, 0.14f), Silver, 0, new Vector3(0, -90, 0)));
                    d.gripPos = new Vector3(0, 0, 0); d.muzzlePos = new Vector3(0, 0, 0.2f); d.useSupport = false;
                    break;
                case 5: // bat
                    d.melee = true; d.meleeType = 1; d.meleeDamage = 55; d.meleeReach = 2f; d.meleeSpeed = 0.8f; d.icon = 7; d.weight = 1f;
                    d.parts.Add(P("Handle", 1, new Vector3(0, 0, 0.05f), new Vector3(0.03f, 0.03f, 0.3f), WoodC, 2));
                    d.parts.Add(P("Barrel", 3, new Vector3(0, 0, 0.42f), new Vector3(0.065f, 0.065f, 0.5f), WoodC, 2));
                    d.parts.Add(P("Tape", 1, new Vector3(0, 0, -0.03f), new Vector3(0.034f, 0.034f, 0.12f), Black, 1));
                    d.gripPos = new Vector3(0, 0, -0.03f); d.muzzlePos = new Vector3(0, 0, 0.65f); d.useSupport = false;
                    break;
                default:
                    d.parts.Add(P("Body", 0, new Vector3(0, 0.02f, 0.05f), new Vector3(0.03f, 0.05f, 0.2f), Black));
                    break;
            }
            return d;
        }
    }

    /// <summary>
    /// Anti-cheat limits for modded weapons. The same rules run when a mod is saved, uploaded, installed, downloaded,
    /// and on the multiplayer host when a client equips one, so nobody can bring a weapon stronger than these caps.
    /// </summary>
    public static class WeaponRules
    {
        public const int MaxParts = 64, MaxJson = 96 * 1024;
        public const float MaxDamage = 250f, MaxRpm = 1500f, MaxRange = 1000f, MaxForce = 120f, MaxPen = 4f, MaxExplosion = 6f;
        public const int MaxPellets = 16, MaxMag = 200, MaxBurst = 8;
        public const float MaxPartSize = 1.5f, MaxOffset = 1.5f;

        /// <summary>Parses + clamps a weapon design. Returns null and fills <paramref name="design"/> when usable,
        /// otherwise the reason it was rejected. Every value that had to change is described in <paramref name="notes"/>.</summary>
        public static string Parse(string json, out WeaponDesign design, List<string> notes)
        {
            design = null;
            if (string.IsNullOrEmpty(json)) return "The weapon has no data.";
            if (json.Length > MaxJson) return "The weapon data is too big (" + json.Length / 1024 + " KB, max " + MaxJson / 1024 + " KB).";
            try { design = JsonUtility.FromJson<WeaponDesign>(json); }
            catch (Exception) { design = null; }
            if (design == null) return "The weapon data is damaged.";
            return Sanitize(design, notes);
        }

        static float F(float v, float min, float max, string what, List<string> notes, float fallback)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) { notes?.Add(what + ": invalid number, reset to " + fallback); return fallback; }
            if (v < min) { notes?.Add(what + ": " + Fmt(v) + " raised to the minimum " + Fmt(min)); return min; }
            if (v > max) { notes?.Add(what + ": " + Fmt(v) + " lowered to the maximum " + Fmt(max)); return max; }
            return v;
        }

        static int I(int v, int min, int max, string what, List<string> notes)
        {
            if (v < min) { notes?.Add(what + ": " + v + " raised to " + min); return min; }
            if (v > max) { notes?.Add(what + ": " + v + " lowered to the maximum " + max); return max; }
            return v;
        }

        static string Fmt(float v) => Math.Abs(v) >= 100f ? v.ToString("0") : v.ToString("0.###");

        static bool Bad(Vector3 v) => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);

        static Vector3 V(Vector3 v, float lim, string what, List<string> notes, Vector3 fallback)
        {
            if (Bad(v)) { notes?.Add(what + ": invalid position, reset"); return fallback; }
            var c = new Vector3(Mathf.Clamp(v.x, -lim, lim), Mathf.Clamp(v.y, -lim, lim), Mathf.Clamp(v.z, -lim, lim));
            if (c != v) notes?.Add(what + ": moved back inside the " + lim + " m build area");
            return c;
        }

        static Vector3 Euler(Vector3 v, string what, List<string> notes)
        {
            if (Bad(v)) { notes?.Add(what + ": invalid rotation, reset"); return Vector3.zero; }
            return new Vector3(Mathf.Repeat(v.x + 180f, 360f) - 180f, Mathf.Repeat(v.y + 180f, 360f) - 180f, Mathf.Repeat(v.z + 180f, 360f) - 180f);
        }

        static Color Col(Color c, string what, List<string> notes)
        {
            if (float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b) || float.IsInfinity(c.r) || float.IsInfinity(c.g) || float.IsInfinity(c.b)) { notes?.Add(what + ": invalid colour, reset"); return Color.gray; }
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
        }

        public static string Sanitize(WeaponDesign d, List<string> notes)
        {
            if (d == null) return "The weapon data is damaged.";
            if (d.parts == null) d.parts = new List<WPart>();
            if (d.parts.Count > MaxParts) return "Too many parts (" + d.parts.Count + ", max " + MaxParts + ").";
            if (d.parts.Count == 0) return "The weapon has no parts.";
            for (int i = 0; i < d.parts.Count; i++)
            {
                var p = d.parts[i];
                if (p == null) return "Part " + (i + 1) + " is damaged.";
                p.name = ModText.Clean(p.name, 24);
                if (p.name.Length == 0) p.name = "Part " + (i + 1);
                string w = "Part '" + p.name + "'";
                p.shape = I(p.shape, 0, WeaponDesign.Shapes.Length - 1, w + " shape", notes);
                p.mat = I(p.mat, 0, WeaponDesign.Materials.Length - 1, w + " material", notes);
                p.pos = V(p.pos, MaxOffset, w, notes, Vector3.zero);
                p.rot = Euler(p.rot, w, notes);
                if (Bad(p.size)) { notes?.Add(w + ": invalid size, reset"); p.size = Vector3.one * 0.05f; }
                var s = new Vector3(Mathf.Clamp(p.size.x, 0.002f, MaxPartSize), Mathf.Clamp(p.size.y, 0.002f, MaxPartSize), Mathf.Clamp(p.size.z, 0.002f, MaxPartSize));
                if (s != p.size) notes?.Add(w + ": size kept between 0.002 and " + MaxPartSize + " m");
                p.size = s;
                p.color = Col(p.color, w, notes);
            }
            if (d.magPart < -1 || d.magPart >= d.parts.Count) d.magPart = -1;
            if (d.slidePart < -1 || d.slidePart >= d.parts.Count) d.slidePart = -1;

            d.muzzlePos = V(d.muzzlePos, MaxOffset, "Bullet source", notes, new Vector3(0, 0.05f, 0.2f));
            d.muzzleRot = Euler(d.muzzleRot, "Bullet source", notes);
            d.gripPos = V(d.gripPos, MaxOffset, "Hold point", notes, Vector3.zero);
            d.supportPos = V(d.supportPos, MaxOffset, "Support hand point", notes, Vector3.zero);
            d.sightRear = V(d.sightRear, MaxOffset, "Rear sight", notes, new Vector3(0, 0.07f, 0f));
            d.sightFront = V(d.sightFront, MaxOffset, "Front sight", notes, new Vector3(0, 0.07f, 0.2f));
            d.ejectPos = V(d.ejectPos, MaxOffset, "Shell eject", notes, Vector3.zero);

            d.trigger = I(d.trigger, 0, WeaponDesign.Triggers.Length - 1, "Trigger", notes);
            d.burstCount = I(d.burstCount, 2, MaxBurst, "Burst count", notes);
            d.chargeTime = F(d.chargeTime, 0.2f, 4f, "Charge time", notes, 1f);
            d.rpm = F(d.rpm, 20f, MaxRpm, "Fire rate (RPM)", notes, 400f);
            d.damage = F(d.damage, 1f, MaxDamage, "Damage per bullet", notes, 30f);
            d.pellets = I(d.pellets, 1, MaxPellets, "Pellets", notes);
            // Total damage per trigger pull is capped too, so 16 pellets can't each do the max.
            float maxPellet = MaxDamage * 1.6f / d.pellets;
            if (d.pellets > 1 && d.damage > maxPellet) { notes?.Add("Damage per pellet: " + Fmt(d.damage) + " lowered to " + Fmt(maxPellet) + " (all pellets together max " + Fmt(MaxDamage * 1.6f) + ")"); d.damage = maxPellet; }
            d.hipSpread = F(d.hipSpread, 0f, 15f, "Hip spread", notes, 1.5f);
            d.aimSpread = F(d.aimSpread, 0f, 15f, "Aim spread", notes, 0.2f);
            d.range = F(d.range, 5f, MaxRange, "Range", notes, 300f);
            d.force = F(d.force, 0f, MaxForce, "Bullet force", notes, 10f);
            d.penetration = F(d.penetration, 0f, MaxPen, "Penetration", notes, 0.5f);
            d.projectile = I(d.projectile, 0, WeaponDesign.Projectiles.Length - 1, "Projectile", notes);
            d.projectileSpeed = F(d.projectileSpeed, 5f, 200f, "Projectile speed", notes, 60f);
            d.explosionRadius = F(d.explosionRadius, 0.5f, MaxExplosion, "Explosion radius", notes, 3f);
            if (d.projectile == 2)
            {
                // Explosive rounds: lower fire rate and damage caps (an explosion hits everything around).
                if (d.rpm > 240f) { notes?.Add("Explosive rounds: fire rate lowered to 240 RPM"); d.rpm = 240f; }
                if (d.pellets > 3) { notes?.Add("Explosive rounds: max 3 per shot"); d.pellets = 3; }
            }
            d.tracer = Col(d.tracer, "Tracer colour", notes);
            d.magSize = I(d.magSize, 1, MaxMag, "Magazine size", notes);
            d.reloadTime = F(d.reloadTime, d.reloadStyle == 1 ? 0.2f : 0.4f, 8f, "Reload time", notes, 1.8f);
            d.reloadStyle = I(d.reloadStyle, 0, WeaponDesign.ReloadStyles.Length - 1, "Reload style", notes);
            d.recoilV = F(d.recoilV, 0f, 8f, "Vertical recoil", notes, 1.5f);
            d.recoilH = F(d.recoilH, 0f, 5f, "Horizontal recoil", notes, 0.3f);
            d.kick = F(d.kick, 0f, 5f, "Kick", notes, 1f);
            d.aimFov = F(d.aimFov, 0.15f, 1f, "Aim zoom (FOV)", notes, 0.8f);
            d.aimSpeed = F(d.aimSpeed, 0.3f, 2.5f, "Aim speed", notes, 1f);
            d.moveSpeed = F(d.moveSpeed, 0.5f, 1.15f, "Move speed", notes, 1f);
            d.weight = F(d.weight, 0.1f, 25f, "Weight", notes, 2f);
            d.sound = I(d.sound, 0, WeaponDesign.Sounds.Length - 1, "Sound", notes);
            d.pitch = F(d.pitch, 0.5f, 2f, "Sound pitch", notes, 1f);
            d.meleeType = I(d.meleeType, 0, WeaponDesign.MeleeTypes.Length - 1, "Melee type", notes);
            d.meleeReach = F(d.meleeReach, 0.8f, 3f, "Melee reach", notes, 1.6f);
            d.meleeDamage = F(d.meleeDamage, 1f, 150f, "Melee damage", notes, 40f);
            d.meleeSpeed = F(d.meleeSpeed, 0.4f, 2f, "Melee speed", notes, 1f);
            d.icon = I(d.icon, 0, WeaponDesign.Icons.Length - 1, "Icon", notes);
            d.formatVersion = 1;
            return null;
        }
    }
}
