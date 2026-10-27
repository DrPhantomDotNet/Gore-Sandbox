using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Procedural models for the second gun pack: pump shotgun, bolt-action sniper, .357 revolver, minigun and
    /// harpoon gun. Same conventions as <see cref="WeaponModels"/>: real-world scale, +Z forward, origin at the grip.
    /// Moving parts are found by name from the weapon classes (Guns2.cs).
    /// </summary>
    public static class WeaponModels2
    {
        static readonly Color Polymer = new Color(0.11f, 0.11f, 0.12f), Steel = new Color(0.22f, 0.22f, 0.24f), DarkSteel = new Color(0.12f, 0.12f, 0.13f),
            Brass = new Color(0.8f, 0.62f, 0.25f), Wood = new Color(0.45f, 0.28f, 0.15f), DarkWood = new Color(0.3f, 0.17f, 0.09f),
            Chrome = new Color(0.62f, 0.63f, 0.66f), Olive = new Color(0.24f, 0.27f, 0.17f), Yellow = new Color(0.85f, 0.65f, 0.1f);
        static readonly Color TritiumGreen = new Color(0.45f, 1f, 0.35f);

        static Quaternion RightGrip(float pitch = 10f) => Quaternion.Euler(pitch, 0, -90f);

        public static WeaponModel Build(string id, int layer)
        {
            switch (id)
            {
                case "shotgun": return Shotgun(layer);
                case "sniper": return Sniper(layer);
                case "revolver": return Revolver(layer);
                case "minigun": return Minigun(layer);
                case "harpoon": return HarpoonGun(layer);
                default: return null;
            }
        }

        static MeshBuilder ShellBuilder()
        {
            var shell = new MeshBuilder { Color = new Color(0.12f, 0.35f, 0.75f), Gloss = 0.4f };
            shell.Cylinder(new Vector3(0, 0, -0.03f), new Vector3(0, 0, 0.02f), 0.0105f, 0.0105f, 10);
            shell.SetColor(Brass, 0.8f);
            shell.Cylinder(new Vector3(0, 0, -0.04f), new Vector3(0, 0, -0.03f), 0.011f, 0.011f, 10);
            return shell;
        }

        // ------------------------------------------------------------------ Pump shotgun

        static WeaponModel Shotgun(int layer)
        {
            var m = WeaponModels.Root("Pump Shotgun", layer);
            var b = new MeshBuilder { Color = DarkSteel, Gloss = 0.5f };
            b.BevelBox(new Vector3(0, 0.045f, 0.07f), new Vector3(0.034f, 0.07f, 0.24f), 0.006f);           // receiver
            b.Cylinder(new Vector3(0, 0.07f, 0.19f), new Vector3(0, 0.07f, 0.74f), 0.0125f, 0.012f, 14);     // barrel
            b.Cylinder(new Vector3(0, 0.04f, 0.19f), new Vector3(0, 0.04f, 0.66f), 0.011f, 0.011f, 12);      // magazine tube
            b.Box(new Vector3(0, 0.055f, 0.645f), new Vector3(0.012f, 0.035f, 0.02f));                      // barrel clamp
            b.SetColor(Steel, 0.7f);
            b.Box(new Vector3(0.0175f, 0.055f, 0.08f), new Vector3(0.002f, 0.022f, 0.065f));                // ejection port
            b.Box(new Vector3(0, 0.003f, 0.045f), new Vector3(0.012f, 0.005f, 0.06f));                       // trigger guard
            b.SetColor(Wood, 0.35f);
            b.Push(new Vector3(0, -0.03f, -0.02f), Quaternion.Euler(-22, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.03f, 0.09f, 0.045f), 0.01f);
            b.Pop();
            b.Loft(5, 12, i => (new Vector3(0, 0.035f - i * 0.013f, -0.06f - i * 0.065f), 0.022f, 0.028f + i * 0.008f), Vector3.back, true, true);
            b.SetColor(Polymer, 0.2f);
            b.Box(new Vector3(0, -0.017f, -0.325f), new Vector3(0.046f, 0.11f, 0.012f));                    // recoil pad
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);

            // Pump (forend) slides back along the magazine tube.
            var pump = new MeshBuilder { Color = Wood, Gloss = 0.35f };
            pump.BevelBox(Vector3.zero, new Vector3(0.046f, 0.04f, 0.16f), 0.012f);
            pump.SetColor(DarkWood, 0.3f);
            for (int i = 0; i < 7; i++) pump.Box(new Vector3(0, -0.006f, -0.06f + i * 0.02f), new Vector3(0.048f, 0.026f, 0.005f));
            m.Charge = m.Piece("Pump", pump, Mats.Lit, new Vector3(0, 0.04f, 0.37f));
            m.ChargeRest = m.Charge.localPosition;

            var shellB = ShellBuilder();
            m.MagMesh = shellB.ToMesh("Shell12");
            m.Mag = m.Piece("Shell", shellB, Mats.Lit, new Vector3(0.03f, 0.05f, 0.08f));
            m.MagRest = m.Mag.localPosition;
            m.Mag.gameObject.SetActive(false);

            m.GripR = m.Anchor("GripR", new Vector3(0.024f, -0.05f, -0.085f), RightGrip(20f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.004f, 0.003f, 0.37f), Quaternion.Euler(-15f, 0, 165f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.07f, 0.745f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(0.02f, 0.055f, 0.08f), Quaternion.identity);
            m.MagWell = m.Anchor("LoadPort", new Vector3(0.0f, 0.0f, 0.12f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.08f, 0.0f), Quaternion.identity);
            WeaponModels.IronSights(m, new Vector3(0, 0.0885f, 0.02f), 0.08f, new Vector3(0, 0.0885f, 0.72f), 0.0815f, DarkSteel, new Color(1f, 0.55f, 0.15f));
            return m;
        }

        // ------------------------------------------------------------------ Bolt-action sniper

        static WeaponModel Sniper(int layer)
        {
            var m = WeaponModels.Root("Sniper Rifle", layer);
            var b = new MeshBuilder { Color = Olive, Gloss = 0.25f };
            // Stock: forestock, grip, butt.
            b.BevelBox(new Vector3(0, 0.03f, 0.28f), new Vector3(0.042f, 0.045f, 0.42f), 0.012f);
            b.BevelBox(new Vector3(0, 0.03f, 0.0f), new Vector3(0.044f, 0.05f, 0.16f), 0.012f);
            b.Push(new Vector3(0, -0.03f, -0.06f), Quaternion.Euler(-18, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.032f, 0.095f, 0.048f), 0.012f);
            b.Pop();
            b.Loft(5, 12, i => (new Vector3(0, 0.03f - i * 0.006f, -0.1f - i * 0.06f), 0.022f, 0.035f + i * 0.007f), Vector3.back, true, true);
            b.BevelBox(new Vector3(0, 0.072f, -0.24f), new Vector3(0.03f, 0.025f, 0.12f), 0.008f);           // cheek rest
            b.SetColor(Polymer, 0.2f);
            b.Box(new Vector3(0, 0.0f, -0.39f), new Vector3(0.05f, 0.13f, 0.014f));
            b.Box(new Vector3(0, 0.003f, 0.045f), new Vector3(0.012f, 0.005f, 0.06f));
            b.SetColor(DarkSteel, 0.55f);
            b.Cylinder(new Vector3(0, 0.06f, -0.06f), new Vector3(0, 0.06f, 0.2f), 0.017f, 0.017f, 14);        // receiver
            b.Cylinder(new Vector3(0, 0.06f, 0.2f), new Vector3(0, 0.06f, 0.94f), 0.013f, 0.0095f, 14);       // barrel
            b.Cylinder(new Vector3(0, 0.06f, 0.94f), new Vector3(0, 0.06f, 1.01f), 0.015f, 0.015f, 12);       // muzzle brake
            for (int i = 0; i < 3; i++) b.Box(new Vector3(0, 0.06f, 0.955f + i * 0.02f), new Vector3(0.034f, 0.008f, 0.008f));
            // Scope: rings, tube, bells, turrets.
            b.Box(new Vector3(0, 0.088f, -0.02f), new Vector3(0.012f, 0.02f, 0.016f));
            b.Box(new Vector3(0, 0.088f, 0.14f), new Vector3(0.012f, 0.02f, 0.016f));
            b.SetColor(Polymer, 0.4f);
            b.Cylinder(new Vector3(0, 0.115f, -0.06f), new Vector3(0, 0.115f, 0.2f), 0.0135f, 0.0135f, 16);
            b.Cylinder(new Vector3(0, 0.115f, -0.13f), new Vector3(0, 0.115f, -0.06f), 0.02f, 0.0135f, 16);
            b.Cylinder(new Vector3(0, 0.115f, 0.2f), new Vector3(0, 0.115f, 0.28f), 0.0135f, 0.025f, 16);
            b.Cylinder(new Vector3(0, 0.135f, 0.06f), new Vector3(0, 0.146f, 0.06f), 0.009f, 0.009f, 10);
            b.Cylinder(new Vector3(0.018f, 0.115f, 0.06f), new Vector3(0.03f, 0.115f, 0.06f), 0.009f, 0.009f, 10);
            b.SetColor(new Color(0.15f, 0.3f, 0.45f), 0.95f);
            b.Cylinder(new Vector3(0, 0.115f, 0.279f), new Vector3(0, 0.115f, 0.281f), 0.022f, 0.022f, 16);   // objective lens
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);

            // Bolt: body inside the receiver plus the handle sticking out to the right. Rotates up about the bore, then slides back.
            var bolt = new MeshBuilder { Color = Chrome, Gloss = 0.85f };
            bolt.Cylinder(new Vector3(0, 0, -0.05f), new Vector3(0, 0, 0.02f), 0.0105f, 0.0105f, 10);
            bolt.Cylinder(new Vector3(0, 0, 0f), new Vector3(0.038f, -0.012f, 0f), 0.004f, 0.004f, 8);
            bolt.SetColor(DarkSteel, 0.5f);
            bolt.Sphere(new Vector3(0.041f, -0.013f, 0f), Vector3.one * 0.008f, 6, 8);
            m.Charge = m.Piece("Bolt", bolt, Mats.LitMetal, new Vector3(0, 0.06f, -0.03f));
            m.ChargeRest = m.Charge.localPosition;

            var mag = new MeshBuilder { Color = DarkSteel, Gloss = 0.35f };
            mag.Box(new Vector3(0, -0.025f, 0), new Vector3(0.026f, 0.06f, 0.075f));
            mag.Box(new Vector3(0, -0.057f, 0), new Vector3(0.03f, 0.006f, 0.08f));
            m.MagMesh = mag.ToMesh("SniperMag");
            m.Mag = m.Piece("Mag", mag, Mats.LitMetal, new Vector3(0, 0.012f, 0.09f));
            m.MagRest = m.Mag.localPosition;

            m.GripR = m.Anchor("GripR", new Vector3(0.024f, -0.05f, -0.12f), RightGrip(18f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.004f, -0.002f, 0.3f), Quaternion.Euler(-15f, 0, 165f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.06f, 1.015f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(0.02f, 0.07f, 0.03f), Quaternion.identity);
            m.MagWell = m.Anchor("MagWell", new Vector3(0, -0.06f, 0.09f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.115f, -0.13f), Quaternion.identity);
            // The scope's optical axis (the gun is hidden behind the scope overlay once fully aimed).
            m.SightRear = m.Anchor("SightRear", new Vector3(0, 0.115f, -0.13f), Quaternion.identity);
            m.SightFront = m.Anchor("SightFront", new Vector3(0, 0.115f, 0.28f), Quaternion.identity);
            return m;
        }

        // ------------------------------------------------------------------ .357 revolver

        static WeaponModel Revolver(int layer)
        {
            var m = WeaponModels.Root(".357 Revolver", layer);
            var b = new MeshBuilder { Color = Chrome, Gloss = 0.85f };
            b.BevelBox(new Vector3(0, 0.058f, 0.04f), new Vector3(0.024f, 0.05f, 0.075f), 0.005f);          // frame window
            b.BevelBox(new Vector3(0, 0.03f, -0.005f), new Vector3(0.022f, 0.03f, 0.05f), 0.005f);           // lower frame
            b.Cylinder(new Vector3(0, 0.07f, 0.075f), new Vector3(0, 0.07f, 0.23f), 0.0085f, 0.0085f, 12);  // barrel
            b.Box(new Vector3(0, 0.081f, 0.15f), new Vector3(0.008f, 0.006f, 0.155f));                       // top rib
            b.Box(new Vector3(0, 0.052f, 0.14f), new Vector3(0.014f, 0.016f, 0.13f));                        // ejector shroud
            b.Box(new Vector3(0, 0.0845f, 0.0f), new Vector3(0.016f, 0.004f, 0.08f));                       // top strap
            b.Box(new Vector3(0, 0.006f, 0.035f), new Vector3(0.01f, 0.005f, 0.045f));                       // trigger guard
            b.SetColor(DarkSteel, 0.5f);
            b.Box(new Vector3(0, 0.018f, 0.03f), new Vector3(0.005f, 0.022f, 0.006f));                       // trigger
            b.SetColor(DarkWood, 0.45f);
            b.Push(new Vector3(0, -0.025f, -0.04f), Quaternion.Euler(-20, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.03f, 0.1f, 0.042f), 0.012f);
            b.Pop();
            m.Piece("Frame", b, Mats.LitMetal, Vector3.zero);

            // Cylinder (rotates one chamber per shot, swings out to the left for reloads).
            var cyl = new MeshBuilder { Color = Chrome, Gloss = 0.8f };
            cyl.Cylinder(new Vector3(0, 0, -0.022f), new Vector3(0, 0, 0.022f), 0.0195f, 0.0195f, 18);
            cyl.SetColor(DarkSteel, 0.4f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.0115f;
                cyl.Cylinder(c + new Vector3(0, 0, 0.0215f), c + new Vector3(0, 0, 0.0225f), 0.0045f, 0.0045f, 8);
                cyl.SetColor(Brass, 0.7f);
                cyl.Cylinder(c + new Vector3(0, 0, -0.0225f), c + new Vector3(0, 0, -0.0215f), 0.0046f, 0.0046f, 8);
                cyl.SetColor(DarkSteel, 0.4f);
                var f = new Vector3(Mathf.Cos(a + Mathf.PI / 6f), Mathf.Sin(a + Mathf.PI / 6f), 0) * 0.0195f;
                cyl.Box(f, new Vector3(0.004f, 0.004f, 0.03f));                                          // flutes
            }
            m.Piece("Cylinder", cyl, Mats.LitMetal, new Vector3(0, 0.058f, 0.04f));

            // Hammer pivots at its base; cocking rotates it back.
            var ham = new MeshBuilder { Color = DarkSteel, Gloss = 0.6f };
            ham.Box(new Vector3(0, 0.008f, 0f), new Vector3(0.006f, 0.018f, 0.008f));
            ham.Box(new Vector3(0, 0.017f, -0.007f), new Vector3(0.008f, 0.005f, 0.012f));
            m.Piece("Hammer", ham, Mats.LitMetal, new Vector3(0, 0.07f, -0.002f));

            var loader = new MeshBuilder { Color = DarkSteel, Gloss = 0.3f };
            loader.Cylinder(new Vector3(0, 0, -0.01f), new Vector3(0, 0, 0.002f), 0.017f, 0.017f, 14);
            loader.SetColor(Brass, 0.7f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.0115f;
                loader.Cylinder(c + new Vector3(0, 0, 0.002f), c + new Vector3(0, 0, 0.034f), 0.0044f, 0.0038f, 8);
            }
            m.MagMesh = loader.ToMesh("SpeedLoader");

            m.GripR = m.Anchor("GripR", new Vector3(0.024f, -0.05f, -0.1f), RightGrip(14f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.03f, -0.075f, -0.06f), Quaternion.Euler(30f, 20f, 65f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.07f, 0.235f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(-0.03f, 0.058f, 0.04f), Quaternion.identity);
            m.MagWell = m.Anchor("MagWell", new Vector3(-0.04f, 0.04f, -0.02f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.092f, 0), Quaternion.identity);
            WeaponModels.IronSights(m, new Vector3(0, 0.0915f, -0.025f), 0.0865f, new Vector3(0, 0.0915f, 0.215f), 0.084f, DarkSteel, new Color(1f, 0.3f, 0.2f));
            return m;
        }

        // ------------------------------------------------------------------ Minigun

        static WeaponModel Minigun(int layer)
        {
            var m = WeaponModels.Root("Minigun", layer);
            var b = new MeshBuilder { Color = DarkSteel, Gloss = 0.5f };
            b.BevelBox(new Vector3(0, 0.02f, 0.02f), new Vector3(0.11f, 0.11f, 0.24f), 0.015f);              // motor housing
            b.Cylinder(new Vector3(0, 0.02f, 0.14f), new Vector3(0, 0.02f, 0.2f), 0.055f, 0.05f, 18);
            b.SetColor(Olive, 0.3f);
            b.BevelBox(new Vector3(-0.1f, -0.01f, 0.02f), new Vector3(0.09f, 0.13f, 0.17f), 0.01f);           // ammo box
            b.SetColor(Yellow, 0.3f);
            b.Box(new Vector3(-0.146f, 0.0f, 0.02f), new Vector3(0.002f, 0.03f, 0.1f));
            b.SetColor(Brass, 0.8f);
            for (int i = 0; i < 8; i++) b.Cylinder(new Vector3(-0.055f, 0.065f, -0.035f + i * 0.012f), new Vector3(-0.025f, 0.065f, -0.035f + i * 0.012f), 0.004f, 0.004f, 6); // belt
            b.SetColor(Polymer, 0.25f);
            b.Push(new Vector3(0, -0.07f, -0.11f), Quaternion.Euler(-12, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.032f, 0.1f, 0.045f), 0.01f);                              // rear grip
            b.Pop();
            b.Cylinder(new Vector3(0, 0.1f, -0.06f), new Vector3(0, 0.1f, 0.12f), 0.012f, 0.012f, 10);        // carry handle
            b.Box(new Vector3(0, 0.085f, -0.06f), new Vector3(0.012f, 0.03f, 0.012f));
            b.Box(new Vector3(0, 0.085f, 0.12f), new Vector3(0.012f, 0.03f, 0.012f));
            b.Push(new Vector3(0, -0.06f, 0.14f), Quaternion.Euler(-8, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.03f, 0.09f, 0.04f), 0.01f);                              // front grip
            b.Pop();
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);

            var br = new MeshBuilder { Color = Steel, Gloss = 0.6f };
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.026f;
                br.Cylinder(c, c + new Vector3(0, 0, 0.56f), 0.009f, 0.0085f, 8);
                br.SetColor(Color.black, 0.2f);
                br.Cylinder(c + new Vector3(0, 0, 0.5605f), c + new Vector3(0, 0, 0.561f), 0.005f, 0.005f, 6);
                br.SetColor(Steel, 0.6f);
            }
            br.SetColor(DarkSteel, 0.5f);
            br.Cylinder(new Vector3(0, 0, 0.0f), new Vector3(0, 0, 0.04f), 0.042f, 0.042f, 18);
            br.Cylinder(new Vector3(0, 0, 0.28f), new Vector3(0, 0, 0.3f), 0.04f, 0.04f, 18);
            br.Cylinder(new Vector3(0, 0, 0.52f), new Vector3(0, 0, 0.545f), 0.04f, 0.04f, 18);
            m.Piece("Barrels", br, Mats.LitMetal, new Vector3(0, 0.02f, 0.2f));

            m.GripR = m.Anchor("GripR", new Vector3(0.024f, -0.1f, -0.13f), RightGrip(10f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.022f, -0.09f, 0.135f), Quaternion.Euler(10f, 20f, 95f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.02f, 0.77f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(0.06f, 0.0f, 0.1f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.13f, 0f), Quaternion.identity);
            return m;
        }

        // ------------------------------------------------------------------ Harpoon gun

        /// <summary>The harpoon itself (shaft + barbed head); +Z forward, tip at z = length. Also used by the projectile.</summary>
        public static Mesh HarpoonMesh(float length = 0.8f)
        {
            var h = new MeshBuilder { Color = Steel, Gloss = 0.7f };
            h.Cylinder(new Vector3(0, 0, 0), new Vector3(0, 0, length - 0.08f), 0.0055f, 0.0055f, 8);
            h.SetColor(Chrome, 0.9f);
            h.Cylinder(new Vector3(0, 0, length - 0.09f), new Vector3(0, 0, length), 0.011f, 0f, 10);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                h.Cylinder(new Vector3(0, 0, length - 0.06f) + dir * 0.006f, new Vector3(0, 0, length - 0.11f) + dir * 0.02f, 0.0025f, 0.0012f, 5);
            }
            h.SetColor(new Color(0.75f, 0.12f, 0.08f), 0.3f);
            h.Cylinder(new Vector3(0, 0, 0.0f), new Vector3(0, 0, 0.05f), 0.008f, 0.008f, 8);                 // red tail
            return h.ToMesh("Harpoon");
        }

        static WeaponModel HarpoonGun(int layer)
        {
            var m = WeaponModels.Root("Harpoon Gun", layer);
            var b = new MeshBuilder { Color = Yellow, Gloss = 0.45f };
            b.Cylinder(new Vector3(0, 0.05f, -0.16f), new Vector3(0, 0.05f, 0.42f), 0.022f, 0.022f, 16);      // air tank / barrel
            b.Sphere(new Vector3(0, 0.05f, -0.16f), Vector3.one * 0.022f, 6, 12);
            b.SetColor(DarkSteel, 0.5f);
            b.Cylinder(new Vector3(0, 0.05f, 0.42f), new Vector3(0, 0.05f, 0.47f), 0.026f, 0.024f, 16);       // muzzle collar
            b.Box(new Vector3(0, 0.077f, 0.15f), new Vector3(0.016f, 0.008f, 0.62f));                         // rail
            b.Box(new Vector3(0, 0.003f, 0.035f), new Vector3(0.012f, 0.005f, 0.06f));
            b.SetColor(Polymer, 0.25f);
            b.Push(new Vector3(0, -0.025f, -0.02f), Quaternion.Euler(-16, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.032f, 0.1f, 0.048f), 0.012f);
            b.Pop();
            b.BevelBox(new Vector3(0, 0.02f, 0.25f), new Vector3(0.036f, 0.03f, 0.14f), 0.01f);                // forend
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);

            var loaded = new GameObject("LoadedHarpoon");
            loaded.layer = layer;
            loaded.transform.SetParent(m.transform, false);
            loaded.transform.localPosition = new Vector3(0, 0.087f, -0.08f);
            loaded.AddComponent<MeshFilter>().sharedMesh = HarpoonMesh();
            var lr = loaded.AddComponent<MeshRenderer>();
            lr.sharedMaterial = Mats.LitMetal;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            m.MagMesh = HarpoonMesh(0.3f);
            m.GripR = m.Anchor("GripR", new Vector3(0.024f, -0.05f, -0.08f), RightGrip(16f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.004f, -0.005f, 0.25f), Quaternion.Euler(-15f, 0, 165f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.087f, 0.72f), Quaternion.identity);
            m.Eject = m.Anchor("LoadPoint", new Vector3(0.0f, 0.06f, 0.42f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.12f, -0.05f), Quaternion.identity);
            WeaponModels.IronSights(m, new Vector3(0, 0.122f, -0.06f), 0.081f, new Vector3(0, 0.122f, 0.4f), 0.081f, DarkSteel, TritiumGreen, true, true);
            return m;
        }
    }
}
