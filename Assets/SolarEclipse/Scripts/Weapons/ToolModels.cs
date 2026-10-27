using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Procedural viewmodels for the sandbox tools (Rope Tool, Gravity Gun). +Z forward, origin at the grip.</summary>
    public static class ToolModels
    {
        static WeaponModel Root(string name, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            return go.AddComponent<WeaponModel>();
        }

        static Quaternion RightGrip(float pitch = 10f) => Quaternion.Euler(pitch, 0, -90f);

        public static WeaponModel RopeTool(int layer)
        {
            var m = Root("Rope Tool", layer);
            var b = new MeshBuilder { Color = new Color(0.9f, 0.55f, 0.12f), Gloss = 0.4f };
            b.BevelBox(new Vector3(0, 0.045f, 0.05f), new Vector3(0.05f, 0.06f, 0.18f), 0.012f);
            b.SetColor(new Color(0.14f, 0.14f, 0.16f), 0.3f);
            b.Push(new Vector3(0, -0.03f, -0.01f), Quaternion.Euler(-12, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.032f, 0.1f, 0.046f), 0.012f);
            b.Pop();
            b.Box(new Vector3(0, 0.006f, 0.05f), new Vector3(0.012f, 0.005f, 0.05f));
            // Spool on top with wound rope.
            b.SetColor(new Color(0.3f, 0.3f, 0.33f), 0.7f);
            b.Cylinder(new Vector3(-0.035f, 0.1f, 0.04f), new Vector3(0.035f, 0.1f, 0.04f), 0.035f, 0.035f, 16);
            b.SetColor(new Color(0.55f, 0.4f, 0.22f), 0.1f);
            b.Cylinder(new Vector3(-0.028f, 0.1f, 0.04f), new Vector3(0.028f, 0.1f, 0.04f), 0.039f, 0.039f, 16, false);
            // Barrel with a hook at the tip.
            b.SetColor(new Color(0.2f, 0.2f, 0.22f), 0.8f);
            b.Cylinder(new Vector3(0, 0.045f, 0.14f), new Vector3(0, 0.045f, 0.2f), 0.016f, 0.012f, 12);
            b.Torus(new Vector3(0, 0.03f, 0.215f), 0.014f, 0.004f, 12, 6, Vector3.right);
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);
            var glow = new MeshBuilder { Color = Color.white };
            glow.Box(new Vector3(0, 0.077f, 0.0f), new Vector3(0.03f, 0.004f, 0.06f));
            m.Glow = m.Piece("Glow", glow, Mats.Glow(new Color(1f, 0.7f, 0.2f), 2.2f), Vector3.zero);
            m.GlowRenderers.Add(m.Glow.GetComponent<Renderer>());
            m.GripR = m.Anchor("GripR", new Vector3(0.025f, -0.05f, -0.07f), RightGrip(12f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.01f, 0.0f, 0.1f), Quaternion.Euler(-10f, 0, 160f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.045f, 0.21f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.14f, 0), Quaternion.identity);
            return m;
        }

        public static WeaponModel GravityGun(int layer)
        {
            var m = Root("Gravity Gun", layer);
            var b = new MeshBuilder { Color = new Color(0.85f, 0.45f, 0.1f), Gloss = 0.5f };
            b.BevelBox(new Vector3(0, 0.05f, 0.02f), new Vector3(0.075f, 0.075f, 0.2f), 0.018f);
            b.SetColor(new Color(0.12f, 0.12f, 0.14f), 0.4f);
            b.Push(new Vector3(0, -0.03f, -0.03f), Quaternion.Euler(-12, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.034f, 0.1f, 0.048f), 0.012f);
            b.Pop();
            b.Box(new Vector3(0, 0.006f, 0.03f), new Vector3(0.012f, 0.005f, 0.05f));
            b.SetColor(new Color(0.25f, 0.25f, 0.28f), 0.8f);
            b.Cylinder(new Vector3(0, 0.05f, 0.12f), new Vector3(0, 0.05f, 0.16f), 0.04f, 0.03f, 18);
            // Three claw prongs around the emitter.
            for (int i = 0; i < 3; i++)
            {
                var rot = Quaternion.Euler(0, 0, i * 120f);
                var o = new Vector3(0, 0.05f, 0);
                var a = o + rot * new Vector3(0, 0.035f, 0.15f);
                var c = o + rot * new Vector3(0, 0.05f, 0.24f);
                var d = o + rot * new Vector3(0, 0.03f, 0.27f);
                b.Cylinder(a, c, 0.008f, 0.007f, 8);
                b.Cylinder(c, d, 0.007f, 0.004f, 8);
            }
            b.SetColor(new Color(0.1f, 0.1f, 0.12f), 0.9f);
            b.Cylinder(new Vector3(0, 0.1f, -0.06f), new Vector3(0, 0.1f, 0.08f), 0.012f, 0.012f, 10);
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);
            var glow = new MeshBuilder { Color = Color.white };
            glow.Sphere(new Vector3(0, 0.05f, 0.17f), Vector3.one * 0.022f, 8, 12);
            glow.Cylinder(new Vector3(0.0385f, 0.06f, -0.04f), new Vector3(0.0385f, 0.06f, 0.08f), 0.006f, 0.006f, 8, false);
            glow.Cylinder(new Vector3(-0.0385f, 0.06f, -0.04f), new Vector3(-0.0385f, 0.06f, 0.08f), 0.006f, 0.006f, 8, false);
            m.Glow = m.Piece("Glow", glow, Mats.Glow(new Color(1f, 0.6f, 0.15f), 3f), Vector3.zero);
            m.GlowRenderers.Add(m.Glow.GetComponent<Renderer>());
            m.GripR = m.Anchor("GripR", new Vector3(0.025f, -0.05f, -0.09f), RightGrip(12f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.01f, 0.0f, 0.08f), Quaternion.Euler(-10f, 0, 160f));
            m.Muzzle = m.Anchor("Emitter", new Vector3(0, 0.05f, 0.18f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.12f, 0), Quaternion.identity);
            return m;
        }
    }
}
