using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Gun pack 3 models: MAC-11 and dual katanas. +Z forward, origin at the grip, real-world scale.</summary>
    public static class WeaponModels4
    {
        static readonly Color Parkerized = new Color(0.14f, 0.14f, 0.15f), DarkSteel = new Color(0.1f, 0.1f, 0.11f), Steel = new Color(0.3f, 0.3f, 0.32f),
            Polymer = new Color(0.09f, 0.09f, 0.1f);
        static readonly Color TritiumGreen = new Color(0.45f, 1f, 0.35f);

        static Quaternion RightGrip(float pitch = 10f) => Quaternion.Euler(pitch, 0, -90f);

        public static WeaponModel Build(string id, int layer)
        {
            switch (id)
            {
                case "mac11": return Mac11(layer);
                case "katana_dual": return DualKatanas(layer);
                default: return WeaponModels.Root(id, layer);
            }
        }

        // ------------------------------------------------------------------ MAC-11

        static WeaponModel Mac11(int layer)
        {
            var m = WeaponModels.Root("MAC-11", layer);
            var b = new MeshBuilder { Color = Parkerized, Gloss = 0.35f };
            // Boxy stamped receiver (upper + lower) running from behind the grip to the barrel nut.
            b.BevelBox(new Vector3(0, 0.05f, 0.045f), new Vector3(0.036f, 0.05f, 0.21f), 0.004f);
            b.BevelBox(new Vector3(0, 0.022f, 0.05f), new Vector3(0.034f, 0.012f, 0.19f), 0.003f);
            // Rib along the top and the rear sight / front sight ears.
            b.Box(new Vector3(0, 0.0765f, 0.045f), new Vector3(0.012f, 0.003f, 0.2f));
            // Grip (the magazine well) — almost vertical.
            b.Push(new Vector3(0, -0.035f, 0f), Quaternion.Euler(-4, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.032f, 0.1f, 0.044f), 0.006f);
            b.SetColor(Polymer, 0.15f);
            for (int i = 0; i < 4; i++) b.Box(new Vector3(0, -0.035f + i * 0.02f, -0.0225f), new Vector3(0.028f, 0.008f, 0.004f));
            b.Pop();
            // Trigger guard + trigger.
            b.SetColor(Parkerized, 0.35f);
            b.Box(new Vector3(0, -0.002f, 0.064f), new Vector3(0.012f, 0.004f, 0.06f));
            b.Box(new Vector3(0, 0.008f, 0.094f), new Vector3(0.012f, 0.024f, 0.004f));
            b.SetColor(DarkSteel, 0.5f);
            b.Box(new Vector3(0, 0.008f, 0.045f), new Vector3(0.005f, 0.02f, 0.005f));
            // Barrel nut + stubby threaded barrel.
            b.Cylinder(new Vector3(0, 0.055f, 0.15f), new Vector3(0, 0.055f, 0.162f), 0.013f, 0.013f, 12);
            b.Cylinder(new Vector3(0, 0.055f, 0.162f), new Vector3(0, 0.055f, 0.198f), 0.0085f, 0.0085f, 10);
            for (int i = 0; i < 4; i++) b.Torus(new Vector3(0, 0.055f, 0.168f + i * 0.007f), 0.0088f, 0.0012f, 10, 3, Vector3.forward);
            b.SetColor(Color.black, 0.2f);
            b.Cylinder(new Vector3(0, 0.055f, 0.198f), new Vector3(0, 0.055f, 0.1985f), 0.0045f, 0.0045f, 8);
            // Sling loop at the front of the lower.
            b.SetColor(Steel, 0.6f);
            b.Torus(new Vector3(0, 0.01f, 0.142f), 0.008f, 0.0018f, 10, 4, Vector3.right);
            // Folded wire stock: two rods down the sides plus the butt plate flipped up against the rear.
            b.Cylinder(new Vector3(0.0205f, 0.03f, -0.07f), new Vector3(0.0205f, 0.03f, 0.13f), 0.0032f, 0.0032f, 6);
            b.Cylinder(new Vector3(-0.0205f, 0.03f, -0.07f), new Vector3(-0.0205f, 0.03f, 0.13f), 0.0032f, 0.0032f, 6);
            b.Cylinder(new Vector3(0.0205f, 0.03f, 0.13f), new Vector3(-0.0205f, 0.03f, 0.13f), 0.0032f, 0.0032f, 6);
            b.BevelBox(new Vector3(0, 0.03f, -0.072f), new Vector3(0.046f, 0.06f, 0.007f), 0.002f);
            b.SetColor(Polymer, 0.2f);
            b.Box(new Vector3(0, 0.03f, -0.077f), new Vector3(0.044f, 0.054f, 0.004f));
            // Ejection port on the right.
            b.SetColor(Color.black, 0.2f);
            b.Box(new Vector3(0.0181f, 0.055f, 0.06f), new Vector3(0.001f, 0.014f, 0.035f));
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);

            // Cocking knob on top (rides with the bolt; doubles as the "slide" kick).
            var knob = new MeshBuilder { Color = DarkSteel, Gloss = 0.45f };
            // Kept low so it stays under the sight line (0.0905).
            knob.Cylinder(new Vector3(0, -0.002f, 0), new Vector3(0, 0.006f, 0), 0.006f, 0.0055f, 10);
            knob.Sphere(new Vector3(0, 0.006f, 0), new Vector3(0.0055f, 0.002f, 0.0055f), 4, 8);
            m.Slide = m.Piece("CockingKnob", knob, Mats.LitMetal, new Vector3(0, 0.078f, 0.07f));
            m.SlideRest = m.Slide.localPosition;

            // 32-round stick magazine inside the grip, sticking out below it.
            var mag = new MeshBuilder { Color = DarkSteel, Gloss = 0.35f };
            mag.Box(new Vector3(0, -0.075f, 0), new Vector3(0.022f, 0.19f, 0.03f));
            mag.Box(new Vector3(0, -0.172f, 0.001f), new Vector3(0.027f, 0.009f, 0.035f));
            mag.SetColor(new Color(0.8f, 0.62f, 0.25f), 0.7f);
            mag.Cylinder(new Vector3(0, 0.019f, -0.01f), new Vector3(0, 0.019f, 0.008f), 0.0042f, 0.0042f, 8);
            m.MagMesh = mag.ToMesh("Mac11Mag");
            m.Mag = m.Piece("Mag", mag, Mats.LitMetal, new Vector3(0, -0.004f, 0f));
            m.Mag.localRotation = Quaternion.Euler(-4, 0, 0);
            m.MagRest = m.Mag.localPosition;

            m.GripR = m.Anchor("GripR", new Vector3(0.026f, -0.055f, -0.06f), RightGrip(6f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.005f, -0.02f, 0.115f), Quaternion.Euler(-10f, 0, 170f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.055f, 0.2f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(0.02f, 0.058f, 0.06f), Quaternion.identity);
            m.MagWell = m.Anchor("MagWell", new Vector3(0, -0.175f, -0.01f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.09f, -0.045f), Quaternion.identity);
            m.Charge = m.Anchor("ChargePoint", new Vector3(0, 0.085f, 0.07f), Quaternion.Euler(0, 0, 180));
            // Rear aperture at the back of the receiver, front post just behind the barrel nut.
            WeaponModels.IronSights(m, new Vector3(0, 0.0905f, -0.045f), 0.078f, new Vector3(0, 0.0905f, 0.14f), 0.078f, DarkSteel, TritiumGreen, true, true);
            return m;
        }

        // ------------------------------------------------------------------ Dual katanas

        /// <summary>Grip of the left blade, relative to its own "LeftBlade" transform (mirror of the right GripR).</summary>
        public static readonly Vector3 LeftGripPos = new Vector3(-0.02f, 0.008f, -0.085f);

        static WeaponModel DualKatanas(int layer)
        {
            // Right blade = the regular katana model (keeps GripR/Tip anchors). Left blade = a second katana under
            // "LeftBlade", posed every frame by DualKatanas.cs; the left hand rides its SupportL anchor.
            var m = WeaponModels3.Build("katana", layer);
            m.gameObject.name = "Dual Katanas";
            if (m.SupportL != null) Object.DestroyImmediate(m.SupportL.gameObject);
            var second = WeaponModels3.Build("katana", layer);
            var wm = second.GetComponent<WeaponModel>();
            // Drop the second model's anchors, keep its meshes.
            foreach (var t in new[] { wm.GripR, wm.SupportL, wm.Muzzle, wm.Sight })
                if (t != null) Object.DestroyImmediate(t.gameObject);
            Object.DestroyImmediate(wm);
            second.gameObject.name = "LeftBlade";
            second.transform.SetParent(m.transform, false);
            // Rest pose (used by icons / previews): side by side, slightly splayed.
            second.transform.localPosition = new Vector3(-0.09f, 0f, 0f);
            second.transform.localRotation = Quaternion.Euler(0f, -6f, 0f);
            m.SupportL = new GameObject("SupportL").transform;
            m.SupportL.SetParent(second.transform, false);
            m.SupportL.localPosition = LeftGripPos;
            m.SupportL.localRotation = Quaternion.Euler(0, 0, 90f);
            return m;
        }
    }
}
