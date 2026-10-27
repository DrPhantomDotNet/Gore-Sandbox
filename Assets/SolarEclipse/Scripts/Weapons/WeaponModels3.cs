using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Procedural models for the explosives and heavy melee weapons (rocket launcher, frag grenade, C4, katana,
    /// sledgehammer) plus the meshes their thrown/fired projectiles use. +Z forward, origin at the grip.
    /// </summary>
    public static class WeaponModels3
    {
        static readonly Color Olive = new Color(0.25f, 0.29f, 0.17f), DarkOlive = new Color(0.17f, 0.2f, 0.12f), Steel = new Color(0.24f, 0.24f, 0.26f),
            DarkSteel = new Color(0.1f, 0.1f, 0.11f), Wood = new Color(0.42f, 0.27f, 0.15f), Putty = new Color(0.78f, 0.74f, 0.62f), Black = new Color(0.06f, 0.06f, 0.07f);

        /// <summary>Returns null for ids this file doesn't build.</summary>
        public static WeaponModel Build(string id, int layer)
        {
            switch (id)
            {
                case "rpg": return RocketLauncher(layer);
                case "frag": return Frag(layer);
                case "c4": return C4(layer);
                case "katana": return Katana(layer);
                case "sledgehammer": return Sledgehammer(layer);
                default: return null;
            }
        }

        static WeaponModel Root(string name, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            return go.AddComponent<WeaponModel>();
        }

        // ------------------------------------------------------------------ projectile meshes (shared with the viewmodels)

        static Mesh rocketMesh, grenadeMesh, c4Mesh;

        /// <summary>Rocket: warhead nose at +Z, motor + fins at the back. Origin at the warhead's base.</summary>
        public static Mesh RocketMesh
        {
            get
            {
                if (rocketMesh != null) return rocketMesh;
                var b = new MeshBuilder { Color = Olive, Gloss = 0.35f };
                b.Loft(9, 14, i =>
                {
                    float[] z = { 0f, 0.02f, 0.06f, 0.1f, 0.14f, 0.18f, 0.21f, 0.235f, 0.25f };
                    float[] r = { 0.03f, 0.042f, 0.044f, 0.043f, 0.038f, 0.028f, 0.018f, 0.009f, 0.003f };
                    return (new Vector3(0, 0, z[i]), r[i], r[i]);
                }, Vector3.forward);
                b.SetColor(new Color(0.75f, 0.62f, 0.2f), 0.6f);
                b.Cylinder(new Vector3(0, 0, 0.245f), new Vector3(0, 0, 0.27f), 0.004f, 0.002f, 8);
                b.SetColor(DarkSteel, 0.4f);
                b.Cylinder(new Vector3(0, 0, -0.32f), new Vector3(0, 0, 0f), 0.018f, 0.022f, 12);
                b.Cylinder(new Vector3(0, 0, -0.36f), new Vector3(0, 0, -0.32f), 0.022f, 0.016f, 12);
                for (int i = 0; i < 4; i++)
                {
                    b.Push(new Vector3(0, 0, -0.29f), Quaternion.Euler(0, 0, 45f + i * 90f));
                    b.Box(new Vector3(0, 0.03f, 0), new Vector3(0.002f, 0.025f, 0.06f));
                    b.Pop();
                }
                rocketMesh = b.ToMesh("Rocket");
                return rocketMesh;
            }
        }

        /// <summary>Frag grenade body with fuse, spoon and pin ring; origin at the body centre.</summary>
        public static Mesh GrenadeMesh
        {
            get
            {
                if (grenadeMesh != null) return grenadeMesh;
                var b = new MeshBuilder { Color = Olive, Gloss = 0.3f };
                b.Sphere(Vector3.zero, new Vector3(0.031f, 0.036f, 0.031f), 10, 14);
                b.SetColor(DarkOlive, 0.2f);
                b.Torus(new Vector3(0, -0.005f, 0), 0.031f, 0.002f, 16, 4, Vector3.up);
                b.SetColor(Steel, 0.6f);
                b.Cylinder(new Vector3(0, 0.03f, 0), new Vector3(0, 0.05f, 0), 0.012f, 0.011f, 10);
                b.Box(new Vector3(0, 0.054f, 0.006f), new Vector3(0.012f, 0.004f, 0.024f));
                b.Push(new Vector3(0, 0.03f, 0.03f), Quaternion.Euler(-12f, 0, 0));
                b.Box(new Vector3(0, -0.02f, 0), new Vector3(0.011f, 0.05f, 0.003f));     // spoon down the side
                b.Pop();
                grenadeMesh = b.ToMesh("Grenade");
                return grenadeMesh;
            }
        }

        static void PinRing(MeshBuilder b, Vector3 at)
        {
            b.SetColor(new Color(0.7f, 0.7f, 0.72f), 0.8f);
            b.Torus(at, 0.011f, 0.0015f, 14, 4, Vector3.right);
        }

        /// <summary>C4 block wrapped in tape with a detonator box; origin at the block's base centre (sticks with -Y).</summary>
        public static Mesh C4Mesh
        {
            get
            {
                if (c4Mesh != null) return c4Mesh;
                var b = new MeshBuilder { Color = Putty, Gloss = 0.1f };
                b.BevelBox(new Vector3(0, 0.022f, 0), new Vector3(0.1f, 0.044f, 0.15f), 0.006f);
                b.SetColor(new Color(0.15f, 0.15f, 0.15f), 0.25f);
                b.Box(new Vector3(0, 0.022f, 0.045f), new Vector3(0.104f, 0.046f, 0.018f));
                b.Box(new Vector3(0, 0.022f, -0.045f), new Vector3(0.104f, 0.046f, 0.018f));
                b.SetColor(new Color(0.12f, 0.2f, 0.12f), 0.3f);
                b.BevelBox(new Vector3(0, 0.05f, 0), new Vector3(0.05f, 0.016f, 0.06f), 0.003f);
                b.SetColor(new Color(0.8f, 0.1f, 0.1f), 0.3f);
                b.Cylinder(new Vector3(0.02f, 0.05f, 0.02f), new Vector3(0.045f, 0.03f, 0.075f), 0.0025f, 0.0025f, 6, false);
                b.SetColor(new Color(0.1f, 0.2f, 0.7f), 0.3f);
                b.Cylinder(new Vector3(-0.02f, 0.05f, 0.02f), new Vector3(-0.045f, 0.03f, 0.07f), 0.0025f, 0.0025f, 6, false);
                c4Mesh = b.ToMesh("C4");
                return c4Mesh;
            }
        }

        /// <summary>A small glowing LED sphere (blinks by toggling its renderer).</summary>
        public static Renderer Led(Transform parent, Vector3 localPos, Color c, float radius = 0.005f)
        {
            var go = new GameObject("LED");
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var mb = new MeshBuilder { Color = Color.white };
            mb.Sphere(Vector3.zero, Vector3.one * radius, 4, 6);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("LED");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Glow(c, 4f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mr;
        }

        static Transform MeshPiece(WeaponModel m, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.layer = m.gameObject.layer;
            go.transform.SetParent(m.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        // ------------------------------------------------------------------ Rocket launcher (RPG-7 style)

        static WeaponModel RocketLauncher(int layer)
        {
            var m = Root("Rocket Launcher", layer);
            const float tubeY = 0.075f;
            var t = new MeshBuilder { Color = DarkSteel, Gloss = 0.45f };
            t.Cylinder(new Vector3(0, tubeY, -0.55f), new Vector3(0, tubeY, 0.42f), 0.026f, 0.026f, 16);
            // Flared rear venturi.
            t.Loft(4, 16, i => (new Vector3(0, tubeY, -0.55f - i * 0.04f), 0.028f + i * i * 0.006f, 0.028f + i * i * 0.006f), Vector3.back, false, false);
            t.Cylinder(new Vector3(0, tubeY, 0.4f), new Vector3(0, tubeY, 0.44f), 0.03f, 0.03f, 16, false);
            // Wooden heat guards.
            t.SetColor(Wood, 0.3f);
            t.Cylinder(new Vector3(0, tubeY, -0.08f), new Vector3(0, tubeY, 0.18f), 0.034f, 0.034f, 16);
            // Grips + trigger guard.
            t.SetColor(Black, 0.25f);
            t.Push(new Vector3(0, -0.005f, -0.01f), Quaternion.Euler(-14f, 0, 0)); t.BevelBox(Vector3.zero, new Vector3(0.028f, 0.11f, 0.04f), 0.008f); t.Pop();
            t.Push(new Vector3(0, -0.005f, -0.22f), Quaternion.Euler(-14f, 0, 0)); t.BevelBox(Vector3.zero, new Vector3(0.028f, 0.11f, 0.04f), 0.008f); t.Pop();
            t.Box(new Vector3(0, 0.045f, -0.11f), new Vector3(0.022f, 0.02f, 0.26f));
            t.SetColor(Steel, 0.6f);
            t.Box(new Vector3(0, -0.012f, 0.045f), new Vector3(0.006f, 0.004f, 0.06f));
            m.Piece("Tube", t, Mats.LitMetal, Vector3.zero);
            // Optic on the left side.
            var o = new MeshBuilder { Color = Black, Gloss = 0.4f };
            o.Box(new Vector3(-0.035f, tubeY + 0.035f, -0.05f), new Vector3(0.012f, 0.03f, 0.05f));
            o.Cylinder(new Vector3(-0.045f, tubeY + 0.055f, -0.13f), new Vector3(-0.045f, tubeY + 0.055f, 0.02f), 0.014f, 0.014f, 12);
            m.Piece("Optic", o, Mats.Lit, Vector3.zero);
            // Loaded rocket: warhead sticks out of the muzzle.
            var rocket = MeshPiece(m, "Rocket", RocketMesh, Mats.Lit, new Vector3(0, tubeY, 0.44f), Quaternion.identity);
            m.Mag = rocket;
            m.MagRest = rocket.localPosition;
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, tubeY, 0.5f), Quaternion.identity);
            m.Eject = m.Anchor("Backblast", new Vector3(0, tubeY, -0.72f), Quaternion.Euler(0, 180f, 0));
            m.Sight = m.Anchor("Sight", new Vector3(-0.045f, tubeY + 0.055f, -0.13f), Quaternion.identity);
            m.GripR = m.Anchor("GripR", new Vector3(0.022f, -0.05f, -0.065f), Quaternion.Euler(4f, 0, -90f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.022f, -0.05f, -0.275f), Quaternion.Euler(4f, 0, 90f));
            return m;
        }

        // ------------------------------------------------------------------ Frag grenade

        static WeaponModel Frag(int layer)
        {
            var m = Root("Frag Grenade", layer);
            var body = MeshPiece(m, "Grenade", GrenadeMesh, Mats.Lit, new Vector3(0, 0.0f, 0.045f), Quaternion.identity);
            var pin = new MeshBuilder { Color = Color.white };
            PinRing(pin, Vector3.zero);
            pin.Box(new Vector3(0.0f, 0.0f, -0.012f), new Vector3(0.002f, 0.002f, 0.014f));
            var pinT = m.Piece("Pin", pin, Mats.LitMetal, new Vector3(-0.012f, 0.052f, 0.045f - 0.01f));
            pinT.localRotation = Quaternion.Euler(0, -90f, 0);
            m.Charge = pinT;                  // pin (animated / hidden after pulling)
            m.ChargeRest = pinT.localPosition;
            m.Mag = body;                     // the grenade itself
            m.MagRest = body.localPosition;
            m.Muzzle = m.Anchor("Release", new Vector3(0, 0, 0.045f), Quaternion.identity);
            m.GripR = m.Anchor("GripR", new Vector3(0.03f, -0.01f, -0.03f), Quaternion.Euler(-20f, 0, -90f));
            return m;
        }

        // ------------------------------------------------------------------ C4 + detonator

        static WeaponModel C4(int layer)
        {
            var m = Root("C4", layer);
            var brick = MeshPiece(m, "Charge", C4Mesh, Mats.Lit, new Vector3(0, 0.0f, 0.06f), Quaternion.Euler(-70f, 0, 0));
            Led(brick, new Vector3(0.012f, 0.06f, -0.015f), new Color(1f, 0.1f, 0.05f), 0.004f);
            m.Mag = brick;
            m.MagRest = brick.localPosition;
            // Detonator in the left hand.
            var d = new MeshBuilder { Color = new Color(0.2f, 0.21f, 0.2f), Gloss = 0.3f };
            d.BevelBox(new Vector3(-0.24f, 0.0f, 0.04f), new Vector3(0.04f, 0.1f, 0.03f), 0.008f);
            d.SetColor(Black, 0.4f);
            d.Cylinder(new Vector3(-0.255f, 0.05f, 0.04f), new Vector3(-0.255f, 0.13f, 0.04f), 0.003f, 0.002f, 6);
            m.Piece("Detonator", d, Mats.Lit, Vector3.zero);
            var btn = new MeshBuilder { Color = new Color(0.85f, 0.1f, 0.08f), Gloss = 0.5f };
            btn.Cylinder(Vector3.zero, new Vector3(0, 0.012f, 0), 0.009f, 0.009f, 10);
            m.Charge = m.Piece("Button", btn, Mats.Lit, new Vector3(-0.235f, 0.05f, 0.04f));
            m.ChargeRest = m.Charge.localPosition;
            var led = Led(m.transform, new Vector3(-0.225f, 0.035f, 0.024f), new Color(0.2f, 1f, 0.2f), 0.003f);
            m.Glow = led.transform;
            m.GripR = m.Anchor("GripR", new Vector3(0.03f, -0.03f, 0.0f), Quaternion.Euler(-30f, 0, -90f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.215f, -0.06f, 0.02f), Quaternion.Euler(-80f, 0, 80f));
            m.Muzzle = m.Anchor("Throw", new Vector3(0, 0, 0.1f), Quaternion.identity);
            return m;
        }

        // ------------------------------------------------------------------ Katana

        static WeaponModel Katana(int layer)
        {
            var m = Root("Katana", layer);
            // Slightly curved blade (sori): edge faces -Y, spine +Y, tip at +Z.
            var blade = new MeshBuilder { Color = new Color(0.82f, 0.83f, 0.86f), Gloss = 0.97f };
            const int segs = 12;
            const float len = 0.7f;
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs, b2 = (i + 1) / (float)segs;
                float za = 0.03f + a * len, zb = 0.03f + b2 * len;
                float ya = a * a * 0.04f, yb = b2 * b2 * 0.04f;
                float wa = Mathf.Lerp(0.032f, 0.026f, a), wb = Mathf.Lerp(0.032f, 0.026f, b2);
                if (i == segs - 1) wb = 0.004f;
                var c = new Vector3(0, (ya + yb) * 0.5f, (za + zb) * 0.5f);
                var dir = new Vector3(0, yb - ya, zb - za);
                blade.Push(c, Quaternion.LookRotation(dir.normalized, Vector3.up));
                blade.Box(new Vector3(0, ((wa + wb) * 0.5f) * 0.5f - 0.004f, 0), new Vector3(0.0045f, (wa + wb) * 0.5f, dir.magnitude + 0.001f));
                blade.Pop();
            }
            m.Piece("Blade", blade, Mats.LitMetal, Vector3.zero);
            var h = new MeshBuilder { Color = new Color(0.08f, 0.08f, 0.1f), Gloss = 0.2f };
            h.Cylinder(new Vector3(0, 0.008f, -0.25f), new Vector3(0, 0.008f, 0.0f), 0.0155f, 0.0165f, 10);
            h.SetColor(new Color(0.6f, 0.08f, 0.08f), 0.3f);
            for (int i = 0; i < 9; i++) h.Torus(new Vector3(0, 0.008f, -0.235f + i * 0.027f), 0.0168f, 0.002f, 10, 3, Vector3.forward);
            h.SetColor(new Color(0.5f, 0.42f, 0.2f), 0.7f);
            h.Cylinder(new Vector3(0, 0.008f, 0.0f), new Vector3(0, 0.008f, 0.008f), 0.04f, 0.04f, 18);   // tsuba
            h.Cylinder(new Vector3(0, 0.008f, 0.008f), new Vector3(0, 0.008f, 0.032f), 0.017f, 0.015f, 10); // habaki
            h.Sphere(new Vector3(0, 0.008f, -0.255f), new Vector3(0.017f, 0.017f, 0.01f), 5, 8);
            m.Piece("Handle", h, Mats.LitMetal, Vector3.zero);
            m.GripR = m.Anchor("GripR", new Vector3(0.02f, 0.008f, -0.085f), Quaternion.Euler(0, 0, -90f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.02f, 0.008f, -0.2f), Quaternion.Euler(0, 0, 90f));
            m.Muzzle = m.Anchor("Tip", new Vector3(0, 0.04f, 0.73f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.05f, 0), Quaternion.identity);
            return m;
        }

        // ------------------------------------------------------------------ Sledgehammer

        static WeaponModel Sledgehammer(int layer)
        {
            var m = Root("Sledgehammer", layer);
            var h = new MeshBuilder { Color = Wood, Gloss = 0.25f };
            h.Cylinder(new Vector3(0, 0, -0.12f), new Vector3(0, 0, 0.7f), 0.017f, 0.015f, 10);
            h.SetColor(Black, 0.2f);
            h.Cylinder(new Vector3(0, 0, -0.13f), new Vector3(0, 0, 0.06f), 0.0195f, 0.0195f, 10);
            m.Piece("Handle", h, Mats.Lit, Vector3.zero);
            var head = new MeshBuilder { Color = new Color(0.2f, 0.2f, 0.22f), Gloss = 0.5f };
            head.BevelBox(new Vector3(0, 0, 0.72f), new Vector3(0.07f, 0.2f, 0.07f), 0.01f);
            head.SetColor(new Color(0.45f, 0.45f, 0.47f), 0.8f);
            head.Box(new Vector3(0, 0.101f, 0.72f), new Vector3(0.064f, 0.004f, 0.064f));
            head.Box(new Vector3(0, -0.101f, 0.72f), new Vector3(0.064f, 0.004f, 0.064f));
            m.Piece("Head", head, Mats.LitMetal, Vector3.zero);
            m.GripR = m.Anchor("GripR", new Vector3(0.02f, 0, 0.12f), Quaternion.Euler(0, 0, -90f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.02f, 0, -0.08f), Quaternion.Euler(0, 0, 90f));
            m.Muzzle = m.Anchor("Face", new Vector3(0, 0.1f, 0.72f), Quaternion.identity);
            return m;
        }
    }
}
