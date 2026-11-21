using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 melee pack: 20 more melee weapons on the shared MeleeWeapon behaviour (Weapons/MeleeKit.cs),
    /// each with its own procedural model. Model space: the handle runs along +Z from about -0.1, the striking
    /// side is -Y (blades, hammer faces and spikes point that way).
    /// </summary>
    public static class MeleePack2
    {
        static readonly List<WeaponDef> defs = new List<WeaponDef>();
        static readonly Dictionary<string, MeleeSpec> specs = new Dictionary<string, MeleeSpec>();
        static bool defined;

        static readonly Color Steel = new Color(0.68f, 0.69f, 0.72f), Dark = new Color(0.12f, 0.12f, 0.13f), Wood = new Color(0.55f, 0.38f, 0.22f),
            Leather = new Color(0.3f, 0.18f, 0.1f), Gold = new Color(0.85f, 0.68f, 0.25f), Rust = new Color(0.45f, 0.26f, 0.15f);

        static void Add(string id, string name, string glyph, string desc, float dmg, float force, float kg, bool two, MeleeSpec s)
        {
            defs.Add(new WeaponDef
            {
                Id = id, Name = name, Glyph = glyph, Description = desc, Damage = dmg, Force = force, WeightKg = kg, Kind = WeaponKind.Melee,
                HipPos = two ? new Vector3(0.14f, -0.2f, 0.3f) : new Vector3(0.15f, -0.17f, 0.32f),
            });
            specs[id] = s;
        }

        static void Define()
        {
            if (defined) return;
            defined = true;
            const string Heavy = "melee_swing_heavy", Swing = "melee_swing";
            Add("golfclub", "Golf Club", "bat", "Fore! Long, whippy swings that launch Goredolls. RMB = full drive.", 32, 55, 0.4f, true,
                new MeleeSpec { Head = MeleeHead.GolfClub, TwoHanded = true, SwingTime = 0.44f, Reach = 2.1f, Launch = 11f, CrushChance = 0.08f, HandleLen = 0.85f, HeadColor = Steel, HandleColor = Dark, SwingSound = Heavy, FleshSound = "blunt_flesh", WorldSound = "blunt_world" });
            Add("wrench", "Pipe Wrench", "wrench", "Heavy cast-iron wrench. Breaks hands and teeth. RMB = overhead.", 36, 32, 2f, false,
                new MeleeSpec { Head = MeleeHead.Wrench, SwingTime = 0.42f, Reach = 1.7f, Launch = 5f, CrushChance = 0.18f, HandleLen = 0.3f, HeadColor = new Color(0.55f, 0.15f, 0.1f), HandleColor = new Color(0.55f, 0.15f, 0.1f), SwingSound = Swing, FleshSound = "blunt_flesh", WorldSound = "blunt_world" });
            Add("shovel", "Shovel", "hammer", "Flat steel blade on a long handle. Bonks and chops. RMB = overhead slam.", 40, 40, 2.2f, true,
                new MeleeSpec { Head = MeleeHead.Shovel, TwoHanded = true, SwingTime = 0.5f, Reach = 2.0f, Launch = 7f, CutChance = 0.12f, CrushChance = 0.15f, HandleLen = 0.8f, HeadColor = new Color(0.4f, 0.42f, 0.45f), HandleColor = Wood, SwingSound = Heavy, FleshSound = "pan_bonk", WorldSound = "pan_bonk" });
            Add("pickaxe", "Pickaxe", "axe", "A miner's pick. Punches deep holes in skulls. Hold LMB to wind up.", 70, 20, 2.8f, true,
                new MeleeSpec { Head = MeleeHead.Pickaxe, TwoHanded = true, ChargeSwing = true, Blade = true, SwingTime = 0.55f, Reach = 2.0f, Pierce = 1, CutChance = 0.05f, Bleeds = true, HandleLen = 0.75f, HeadColor = new Color(0.35f, 0.36f, 0.38f), HandleColor = Wood, SwingSound = Heavy, FleshSound = "stab", WorldSound = "blunt_world" });
            Add("hatchet", "Hatchet", "axe", "Small one-handed axe. Quick chops that take fingers and hands. RMB = heavy chop.", 46, 16, 0.9f, false,
                new MeleeSpec { Head = MeleeHead.Hatchet, Blade = true, SwingTime = 0.36f, Reach = 1.65f, CutChance = 0.35f, HandleLen = 0.32f, HeadColor = Steel, HandleColor = Wood, SwingSound = Swing, FleshSound = "blade_chop", WorldSound = "knife_hit_hard" });
            Add("kukri", "Kukri", "knife", "Forward-curved Gurkha blade. Built for chopping limbs. RMB = heavy chop.", 50, 14, 0.7f, false,
                new MeleeSpec { Head = MeleeHead.Kukri, Blade = true, SwingTime = 0.34f, Reach = 1.7f, CutChance = 0.45f, HandleLen = 0.12f, HeadLen = 0.32f, HeadColor = Steel, HandleColor = Leather, SwingSound = Swing, FleshSound = "blade_chop", WorldSound = "knife_hit_hard" });
            Add("rapier", "Rapier", "knife", "Long thin duelling sword. Lightning-fast thrusts that go straight through. RMB = lunge.", 34, 8, 1.1f, false,
                new MeleeSpec { Head = MeleeHead.Sword, Blade = true, RmbStab = true, SwingTime = 0.26f, Reach = 2.2f, Pierce = 2, CutChance = 0.12f, Bleeds = true, HandleLen = 0.14f, HeadLen = 0.9f, HandleR = 0.008f, HeadColor = Steel, HandleColor = Gold, Accent = Gold, SwingSound = "whoosh_small", FleshSound = "stab", WorldSound = "knife_hit_hard" });
            Add("longsword", "Longsword", "knife", "A knight's sword. Clean cuts through limbs and necks. RMB = overhead.", 58, 14, 1.5f, true,
                new MeleeSpec { Head = MeleeHead.Sword, Blade = true, TwoHanded = true, SwingTime = 0.42f, Reach = 2.2f, Pierce = 2, CutChance = 0.5f, HandleLen = 0.22f, HeadLen = 0.85f, HandleR = 0.022f, HeadColor = Steel, HandleColor = Leather, Accent = Steel, SwingSound = Heavy, FleshSound = "slice", WorldSound = "knife_hit_hard" });
            Add("claymore", "Claymore", "knife", "A huge two-handed greatsword. Hold LMB to wind up: cleaves bodies in half.", 95, 30, 2.8f, true,
                new MeleeSpec { Head = MeleeHead.Sword, Blade = true, TwoHanded = true, ChargeSwing = true, SwingTime = 0.6f, Reach = 2.5f, Pierce = 3, CutChance = 0.7f, HandleLen = 0.32f, HeadLen = 1.15f, HandleR = 0.032f, HeadColor = Steel, HandleColor = Leather, Accent = Steel, SwingSound = Heavy, FleshSound = "blade_chop", WorldSound = "knife_hit_hard" });
            Add("mace", "Flanged Mace", "hammer", "Steel flanges that cave in armour and skulls. RMB = overhead.", 48, 40, 2f, false,
                new MeleeSpec { Head = MeleeHead.Mace, SwingTime = 0.44f, Reach = 1.75f, Launch = 6f, CrushChance = 0.3f, HandleLen = 0.4f, HeadColor = Steel, HandleColor = Leather, SwingSound = Swing, FleshSound = "blunt_flesh", WorldSound = "blunt_world" });
            Add("morningstar", "Morning Star", "hammer", "A spiked ball on a pole. Every hit bleeds. RMB = overhead.", 54, 38, 2.4f, true,
                new MeleeSpec { Head = MeleeHead.MorningStar, TwoHanded = true, SwingTime = 0.5f, Reach = 2.0f, Launch = 6f, CrushChance = 0.25f, Bleeds = true, HandleLen = 0.7f, HeadColor = new Color(0.4f, 0.4f, 0.42f), HandleColor = Wood, SwingSound = Heavy, FleshSound = "blunt_flesh", WorldSound = "blunt_world" });
            Add("warhammer", "War Hammer", "hammer", "Hammer face on one side, spike on the other. Hold LMB to wind up.", 80, 70, 3.5f, true,
                new MeleeSpec { Head = MeleeHead.WarHammer, TwoHanded = true, ChargeSwing = true, SwingTime = 0.55f, Reach = 2.1f, Launch = 9f, CrushChance = 0.45f, HandleLen = 0.8f, HeadColor = new Color(0.45f, 0.46f, 0.5f), HandleColor = Wood, SwingSound = Heavy, FleshSound = "blunt_flesh", WorldSound = "blunt_world" });
            Add("halberd", "Halberd", "axe", "Axe blade, spike and hook on a long pole. Huge reach. RMB = thrust.", 72, 24, 3.2f, true,
                new MeleeSpec { Head = MeleeHead.Halberd, Blade = true, TwoHanded = true, RmbStab = true, SwingTime = 0.55f, Reach = 2.8f, Pierce = 2, CutChance = 0.55f, HandleLen = 1.4f, HeadColor = Steel, HandleColor = Wood, SwingSound = Heavy, FleshSound = "blade_chop", WorldSound = "knife_hit_hard" });
            Add("trident", "Trident", "knife", "Three barbed prongs on a long shaft. Jabs that skewer. RMB = thrust.", 50, 14, 2.4f, true,
                new MeleeSpec { Head = MeleeHead.Trident, Blade = true, TwoHanded = true, RmbStab = true, SwingTime = 0.4f, Reach = 2.5f, Pierce = 2, CutChance = 0.08f, Bleeds = true, HandleLen = 1.2f, HeadColor = new Color(0.75f, 0.6f, 0.3f), HandleColor = Wood, SwingSound = Swing, FleshSound = "stab", WorldSound = "knife_hit_hard" });
            Add("screwdriver", "Screwdriver", "wrench", "A flat-head screwdriver. Short, fast, nasty stabs. RMB = stab.", 22, 6, 0.2f, false,
                new MeleeSpec { Head = MeleeHead.Screwdriver, Blade = true, RmbStab = true, SwingTime = 0.26f, Reach = 1.45f, CutChance = 0f, Bleeds = true, HandleLen = 0.1f, HeadColor = Steel, HandleColor = new Color(0.9f, 0.75f, 0.1f), SwingSound = "whoosh_small", FleshSound = "stab", WorldSound = "knife_hit_hard" });
            Add("scalpel", "Scalpel", "knife", "A surgeon's blade. Tiny, razor sharp, deep bleeding cuts. RMB = precise stab.", 20, 4, 0.05f, false,
                new MeleeSpec { Head = MeleeHead.Scalpel, Blade = true, RmbStab = true, SwingTime = 0.22f, Reach = 1.4f, CutChance = 0.15f, Bleeds = true, HandleLen = 0.09f, HeadColor = new Color(0.85f, 0.86f, 0.9f), HandleColor = new Color(0.8f, 0.82f, 0.85f), SwingSound = "whoosh_small", FleshSound = "slice", WorldSound = "knife_hit_hard" });
            Add("bonesaw", "Bone Saw", "cut", "A surgical saw. Ragged cuts through bone. RMB = heavy saw.", 42, 10, 0.8f, false,
                new MeleeSpec { Head = MeleeHead.BoneSaw, Blade = true, SwingTime = 0.4f, Reach = 1.6f, CutChance = 0.4f, Bleeds = true, HandleLen = 0.13f, HeadLen = 0.3f, HeadColor = Steel, HandleColor = Dark, SwingSound = Swing, FleshSound = "blade_chop", WorldSound = "knife_hit_hard" });
            Add("bottle", "Broken Bottle", "pill", "A smashed bottle neck. Jagged glass that rips skin open. RMB = stab.", 24, 6, 0.3f, false,
                new MeleeSpec { Head = MeleeHead.Bottle, Blade = true, RmbStab = true, SwingTime = 0.3f, Reach = 1.45f, CutChance = 0.1f, Bleeds = true, HandleLen = 0.1f, HeadColor = new Color(0.25f, 0.55f, 0.3f), HandleColor = new Color(0.25f, 0.55f, 0.3f), SwingSound = "whoosh_small", FleshSound = "slice", WorldSound = "glass" });
            Add("stunbaton", "Stun Baton", "bolt", "A police baton with a shock tip. Zaps on every hit. RMB = overhead.", 30, 25, 0.8f, false,
                new MeleeSpec { Head = MeleeHead.Baton, SwingTime = 0.36f, Reach = 1.75f, Launch = 4f, CrushChance = 0.05f, HandleLen = 0.25f, HeadLen = 0.35f, HeadColor = Dark, HandleColor = Dark, SwingSound = Swing, FleshSound = "zap", WorldSound = "blunt_world" });
            Add("extinguisher", "Fire Extinguisher", "hammer", "A heavy red cylinder. Clumsy, slow, devastating. RMB = overhead.", 55, 60, 6f, true,
                new MeleeSpec { Head = MeleeHead.Extinguisher, TwoHanded = true, SwingTime = 0.6f, Reach = 1.7f, Launch = 8f, CrushChance = 0.35f, HeadColor = new Color(0.78f, 0.08f, 0.06f), HandleColor = Dark, SwingSound = Heavy, FleshSound = "pan_bonk", WorldSound = "pan_bonk" });
            Add("butterfly", "Butterfly Knife", "knife", "Flip it open, slash fast. Tiny blade, lots of cuts. RMB = stab.", 22, 6, 0.2f, false,
                new MeleeSpec { Head = MeleeHead.CombatKnife, Blade = true, RmbStab = true, SwingTime = 0.2f, Reach = 1.45f, CutChance = 0.15f, Bleeds = true, HandleLen = 0.13f, HeadLen = 0.11f, HeadColor = Steel, HandleColor = Gold, SwingSound = "whoosh_small", FleshSound = "slice", WorldSound = "knife_hit_hard" });
            Add("bayonet", "Bayonet", "knife", "A long military blade made for thrusting. Deep stabs that go through. RMB = lunge.", 38, 10, 0.5f, false,
                new MeleeSpec { Head = MeleeHead.CombatKnife, Blade = true, RmbStab = true, SwingTime = 0.3f, Reach = 1.8f, Pierce = 2, CutChance = 0.15f, Bleeds = true, HandleLen = 0.13f, HeadLen = 0.3f, HeadColor = new Color(0.4f, 0.42f, 0.4f), HandleColor = Wood, SwingSound = "whoosh_small", FleshSound = "stab", WorldSound = "knife_hit_hard" });
            Add("nunchucks", "Nunchucks", "hammer", "Two sticks and a chain. Very fast, very loud, very painful. RMB = overhead.", 20, 22, 0.5f, false,
                new MeleeSpec { Head = MeleeHead.Flail, SwingTime = 0.22f, Reach = 1.6f, Launch = 4f, CrushChance = 0.08f, HandleLen = 0.3f, HeadLen = 0.3f, HeadColor = new Color(0.12f, 0.1f, 0.08f), HandleColor = new Color(0.12f, 0.1f, 0.08f), SwingSound = "whoosh_small", FleshSound = "blunt_flesh", WorldSound = "blunt_world" });
            Add("whip", "Bullwhip", "cut", "Long leather whip with a cracking tip. Huge reach, cuts skin open. RMB = overhead crack.", 18, 6, 0.6f, false,
                new MeleeSpec { Head = MeleeHead.Flail, SwingTime = 0.45f, Reach = 3.4f, Launch = 2f, CutChance = 0.05f, Bleeds = true, HandleLen = 0.25f, HeadLen = 0.9f, HeadColor = Leather, HandleColor = Leather, SwingSound = "whoosh_small", FleshSound = "slice", WorldSound = "blunt_world" });
        }

        public static void AppendDefs(List<WeaponDef> all) { Define(); foreach (var d in defs) if (!all.Exists(w => w.Id == d.Id)) all.Add(d); }
        public static bool Has(string id) { Define(); return specs.ContainsKey(id); }

        public static Weapon Create(WeaponDef def)
        {
            if (def == null) return null;
            Define();
            return specs.TryGetValue(def.Id, out var s) && defs.Contains(def) ? new MeleeWeapon { Def = def, Spec = s } : null;
        }

        public static WeaponModel BuildModel(string id, int layer)
        {
            Define();
            if (!specs.TryGetValue(id, out var s)) return null;
            var d = defs.Find(x => x.Id == id);
            return MeleeKit.Build(d != null ? d.Name : id, s, layer);
        }

        // ------------------------------------------------------------------ models (called from MeleeKit.Build)

        static void Grip(MeshBuilder b, MeleeSpec s, float z0, float z1, float r)
        {
            b.SetColor(s.HandleColor, 0.25f);
            b.Cylinder(new Vector3(0, 0, z0), new Vector3(0, 0, z1), r, r, 10);
        }

        public static float Model(WeaponModel m, MeleeSpec s)
        {
            var b = new MeshBuilder { Color = s.HandleColor, Gloss = 0.3f };
            float z0 = -0.12f, top = z0 + s.HandleLen;
            switch (s.Head)
            {
                case MeleeHead.GolfClub:
                    Grip(b, s, z0, z0 + 0.25f, 0.013f);
                    b.SetColor(Steel, 0.8f);
                    b.Cylinder(new Vector3(0, 0, z0 + 0.25f), new Vector3(0, 0, top), 0.007f, 0.005f, 8);
                    b.BevelBox(new Vector3(0, -0.03f, top + 0.015f), new Vector3(0.022f, 0.08f, 0.04f), 0.008f);
                    m.Piece("Golf Club", b, Mats.LitMetal, Vector3.zero);
                    return top + 0.03f;
                case MeleeHead.Wrench:
                    b.Color = s.HeadColor;
                    b.BevelBox(new Vector3(0, 0, (z0 + top) / 2f), new Vector3(0.024f, 0.014f, top - z0), 0.004f);
                    b.SetColor(new Color(0.5f, 0.5f, 0.52f), 0.7f);
                    b.BevelBox(new Vector3(0, -0.018f, top + 0.02f), new Vector3(0.03f, 0.06f, 0.05f), 0.006f);
                    b.Box(new Vector3(0, -0.055f, top + 0.045f), new Vector3(0.028f, 0.018f, 0.03f));
                    m.Piece("Wrench", b, Mats.LitMetal, Vector3.zero);
                    return top + 0.06f;
                case MeleeHead.Shovel:
                    Grip(b, s, z0, top, 0.015f);
                    b.SetColor(new Color(0.2f, 0.2f, 0.22f), 0.3f);
                    b.Box(new Vector3(0, 0, z0 - 0.02f), new Vector3(0.11f, 0.02f, 0.03f));
                    b.SetColor(s.HeadColor, 0.5f);
                    b.BevelBox(new Vector3(0, 0, top + 0.15f), new Vector3(0.22f, 0.008f, 0.3f), 0.003f);
                    m.Piece("Shovel", b, Mats.LitMetal, Vector3.zero);
                    return top + 0.3f;
                case MeleeHead.Pickaxe:
                    Grip(b, s, z0, top, 0.016f);
                    b.SetColor(s.HeadColor, 0.5f);
                    b.Cylinder(new Vector3(0, 0.22f, top), new Vector3(0, 0, top), 0.012f, 0.022f, 8);
                    b.Cylinder(new Vector3(0, 0, top), new Vector3(0, -0.26f, top - 0.02f), 0.022f, 0.004f, 8);
                    m.Piece("Pickaxe", b, Mats.LitMetal, Vector3.zero);
                    return top;
                case MeleeHead.Hatchet:
                    Grip(b, s, z0, top, 0.014f);
                    b.SetColor(s.HeadColor, 0.75f);
                    b.Prism(new[] { new Vector2(0.02f, 0.02f), new Vector2(-0.085f, 0.04f), new Vector2(-0.095f, -0.05f), new Vector2(0.02f, -0.02f) }, 0.012f, new Vector3(0, 0, top - 0.02f));
                    m.Piece("Hatchet", b, Mats.LitMetal, Vector3.zero);
                    return top;
                case MeleeHead.Kukri:
                {
                    Grip(b, s, z0, z0 + s.HandleLen, 0.014f);
                    b.SetColor(s.HeadColor, 0.85f);
                    float z = z0 + s.HandleLen;
                    for (int i = 0; i < 8; i++)
                    {
                        float t0 = i / 8f, t1 = (i + 1) / 8f;
                        float bend0 = -Mathf.Sin(t0 * 1.6f) * 0.06f, bend1 = -Mathf.Sin(t1 * 1.6f) * 0.06f;
                        float w = 0.018f + Mathf.Sin(t0 * Mathf.PI) * 0.022f;
                        b.Box(new Vector3(0, (bend0 + bend1) / 2f, z + (t0 + t1) / 2f * s.HeadLen), new Vector3(0.004f, w * 2f, s.HeadLen / 8f + 0.002f));
                    }
                    m.Piece("Kukri", b, Mats.LitMetal, Vector3.zero);
                    return z + s.HeadLen;
                }
                case MeleeHead.Sword:
                {
                    float w = s.HandleR; // blade half-width
                    Grip(b, s, z0, z0 + s.HandleLen, 0.014f);
                    b.SetColor(s.Accent, 0.8f);
                    b.Sphere(new Vector3(0, 0, z0 - 0.015f), new Vector3(0.02f, 0.02f, 0.02f), 6, 8);
                    float z = z0 + s.HandleLen;
                    b.BevelBox(new Vector3(0, 0, z + 0.01f), new Vector3(0.025f, w * 2f + 0.12f, 0.022f), 0.006f); // cross-guard
                    b.SetColor(s.HeadColor, 0.95f);
                    b.Box(new Vector3(0, 0, z + 0.02f + s.HeadLen * 0.47f), new Vector3(0.006f, w * 2f, s.HeadLen * 0.94f));
                    b.Prism(new[] { new Vector2(w, 0), new Vector2(0, 0.06f), new Vector2(-w, 0) }, 0.006f, new Vector3(0, 0, z + 0.02f + s.HeadLen * 0.94f));
                    m.Piece("Sword", b, Mats.LitMetal, Vector3.zero);
                    return z + s.HeadLen;
                }
                case MeleeHead.Mace:
                    Grip(b, s, z0, top, 0.015f);
                    b.SetColor(s.HeadColor, 0.75f);
                    b.Cylinder(new Vector3(0, 0, top), new Vector3(0, 0, top + 0.1f), 0.025f, 0.025f, 8);
                    for (int i = 0; i < 6; i++)
                    {
                        var d = Quaternion.Euler(0, 0, i * 60f) * Vector3.up;
                        b.BevelBox(new Vector3(0, 0, top + 0.05f) + d * 0.035f, new Vector3(0.008f + Mathf.Abs(d.x) * 0.03f, 0.008f + Mathf.Abs(d.y) * 0.03f, 0.1f), 0.003f);
                    }
                    m.Piece("Mace", b, Mats.LitMetal, Vector3.zero);
                    return top + 0.1f;
                case MeleeHead.MorningStar:
                {
                    Grip(b, s, z0, top, 0.017f);
                    var c = new Vector3(0, 0, top + 0.06f);
                    b.SetColor(s.HeadColor, 0.6f);
                    b.Sphere(c, new Vector3(0.06f, 0.06f, 0.07f), 8, 10);
                    var rng = new System.Random(3);
                    for (int i = 0; i < 16; i++)
                    {
                        var d = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).normalized;
                        b.Cylinder(c + d * 0.055f, c + d * 0.1f, 0.008f, 0.001f, 4);
                    }
                    m.Piece("Morning Star", b, Mats.LitMetal, Vector3.zero);
                    return top + 0.12f;
                }
                case MeleeHead.WarHammer:
                    Grip(b, s, z0, top, 0.017f);
                    b.SetColor(s.HeadColor, 0.6f);
                    b.BevelBox(new Vector3(0, -0.04f, top), new Vector3(0.05f, 0.09f, 0.06f), 0.008f);
                    b.Cylinder(new Vector3(0, 0.005f, top), new Vector3(0, 0.13f, top), 0.018f, 0.002f, 6);
                    m.Piece("War Hammer", b, Mats.LitMetal, Vector3.zero);
                    return top + 0.03f;
                case MeleeHead.Halberd:
                    Grip(b, s, z0, top, 0.016f);
                    b.SetColor(s.HeadColor, 0.85f);
                    b.Prism(new[] { new Vector2(0.01f, 0.05f), new Vector2(-0.12f, 0.1f), new Vector2(-0.13f, -0.08f), new Vector2(0.01f, -0.04f) }, 0.008f, new Vector3(0, 0, top - 0.06f));
                    b.Cylinder(new Vector3(0, 0, top), new Vector3(0, 0, top + 0.22f), 0.012f, 0.001f, 6);
                    b.Cylinder(new Vector3(0, 0.01f, top - 0.04f), new Vector3(0, 0.09f, top - 0.02f), 0.01f, 0.002f, 5);
                    m.Piece("Halberd", b, Mats.LitMetal, Vector3.zero);
                    return top + 0.22f;
                case MeleeHead.Trident:
                    Grip(b, s, z0, top, 0.014f);
                    b.SetColor(s.HeadColor, 0.8f);
                    b.Box(new Vector3(0, 0, top + 0.01f), new Vector3(0.012f, 0.14f, 0.02f));
                    foreach (var y in new[] { -0.065f, 0f, 0.065f })
                    {
                        b.Cylinder(new Vector3(0, y, top + 0.01f), new Vector3(0, y, top + 0.2f), 0.007f, 0.004f, 5);
                        b.Cylinder(new Vector3(0, y, top + 0.2f), new Vector3(0, y, top + 0.25f), 0.009f, 0.0005f, 5);
                    }
                    m.Piece("Trident", b, Mats.LitMetal, Vector3.zero);
                    return top + 0.25f;
                case MeleeHead.Screwdriver:
                    b.Color = s.HandleColor;
                    b.Cylinder(new Vector3(0, 0, z0), new Vector3(0, 0, top), 0.016f, 0.014f, 8);
                    b.SetColor(s.HeadColor, 0.85f);
                    b.Cylinder(new Vector3(0, 0, top), new Vector3(0, 0, top + 0.13f), 0.004f, 0.004f, 6);
                    b.Box(new Vector3(0, 0, top + 0.14f), new Vector3(0.002f, 0.008f, 0.02f));
                    m.Piece("Screwdriver", b, Mats.LitMetal, Vector3.zero);
                    return top + 0.15f;
                case MeleeHead.Scalpel:
                    b.Color = s.HandleColor;
                    b.BevelBox(new Vector3(0, 0, (z0 + top) / 2f), new Vector3(0.006f, 0.01f, top - z0), 0.002f);
                    b.SetColor(s.HeadColor, 1f);
                    b.Prism(new[] { new Vector2(0.004f, 0f), new Vector2(0.002f, 0.04f), new Vector2(-0.006f, 0.0f) }, 0.0015f, new Vector3(0, 0, top));
                    m.Piece("Scalpel", b, Mats.LitMetal, Vector3.zero);
                    return top + 0.04f;
                case MeleeHead.BoneSaw:
                {
                    Grip(b, s, z0, z0 + s.HandleLen, 0.015f);
                    float z = z0 + s.HandleLen;
                    b.SetColor(s.HeadColor, 0.8f);
                    b.Box(new Vector3(0, -0.02f, z + s.HeadLen / 2f), new Vector3(0.003f, 0.05f, s.HeadLen));
                    b.Box(new Vector3(0, 0.02f, z + s.HeadLen / 2f), new Vector3(0.008f, 0.012f, s.HeadLen)); // spine
                    for (int i = 0; i < 18; i++) b.Prism(new[] { new Vector2(0, 0), new Vector2(0.008f, -0.01f), new Vector2(0.016f, 0) }, 0.003f, new Vector3(0, -0.045f, z + i * s.HeadLen / 18f));
                    m.Piece("Bone Saw", b, Mats.LitMetal, Vector3.zero);
                    return z + s.HeadLen;
                }
                case MeleeHead.Bottle:
                {
                    b.Color = s.HeadColor; b.Gloss = 0.95f;
                    b.Cylinder(new Vector3(0, 0, z0), new Vector3(0, 0, top), 0.013f, 0.013f, 10);
                    b.Cylinder(new Vector3(0, 0, top), new Vector3(0, 0, top + 0.05f), 0.013f, 0.03f, 10);
                    var rng = new System.Random(5);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i / 7f * Mathf.PI * 2f;
                        var p = new Vector3(Mathf.Cos(a) * 0.028f, Mathf.Sin(a) * 0.028f, top + 0.05f);
                        b.Cylinder(p, p + new Vector3(0, 0, 0.02f + (float)rng.NextDouble() * 0.05f), 0.006f, 0.0005f, 3);
                    }
                    m.Piece("Broken Bottle", b, Mats.Glass, Vector3.zero);
                    return top + 0.08f;
                }
                case MeleeHead.Baton:
                {
                    float z = z0 + s.HandleLen;
                    Grip(b, s, z0, z, 0.016f);
                    b.SetColor(s.HeadColor, 0.4f);
                    b.Cylinder(new Vector3(0, 0, z), new Vector3(0, 0, z + s.HeadLen), 0.013f, 0.013f, 8);
                    m.Piece("Baton", b, Mats.LitMetal, Vector3.zero);
                    var tip = new MeshBuilder { Color = Color.white, Gloss = 0f };
                    tip.Sphere(new Vector3(0, 0, z + s.HeadLen + 0.01f), new Vector3(0.014f, 0.014f, 0.014f), 6, 8);
                    m.Piece("Shock Tip", tip, Mats.Glow(new Color(0.4f, 0.7f, 1f), 3f), Vector3.zero);
                    return z + s.HeadLen + 0.02f;
                }
                case MeleeHead.Extinguisher:
                default:
                    b.Color = s.HeadColor; b.Gloss = 0.6f;
                    b.Cylinder(new Vector3(0, -0.06f, -0.05f), new Vector3(0, -0.06f, 0.35f), 0.065f, 0.065f, 14);
                    b.Sphere(new Vector3(0, -0.06f, 0.35f), new Vector3(0.065f, 0.065f, 0.05f), 8, 12);
                    b.SetColor(Dark, 0.3f);
                    b.Box(new Vector3(0, 0, 0.0f), new Vector3(0.02f, 0.03f, 0.16f));
                    b.Cylinder(new Vector3(0, -0.06f, 0.4f), new Vector3(0, 0.02f, 0.48f), 0.008f, 0.008f, 6);
                    m.Piece("Extinguisher", b, Mats.LitMetal, Vector3.zero);
                    return 0.42f;
            }
        }
    }
}
