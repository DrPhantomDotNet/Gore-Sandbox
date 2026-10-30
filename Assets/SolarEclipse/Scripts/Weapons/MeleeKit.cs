using UnityEngine;

namespace SolarEclipse
{
    public enum MeleeHead { Bat, SpikedBat, Crowbar, Pipe, Pan, Machete, Cleaver, CombatKnife, FireAxe, Chainsaw }

    /// <summary>
    /// Data for one melee weapon: the model (handle + head shape) and how <see cref="MeleeWeapon"/> swings it.
    /// Model space: +Z along the weapon away from the hand, edge/face toward -Y, origin at the right hand.
    /// </summary>
    public sealed class MeleeSpec
    {
        public MeleeHead Head;
        // ---- model
        public float HandleLen = 0.3f, HandleR = 0.014f, HeadLen = 0.3f;
        public Color HandleColor = new Color(0.1f, 0.1f, 0.1f), HeadColor = new Color(0.78f, 0.79f, 0.82f), Accent = new Color(0.55f, 0.08f, 0.08f);
        // ---- behaviour
        /// <summary>Blades cut (Dismemberment.Slice); blunt weapons break bones and launch.</summary>
        public bool Blade;
        public bool TwoHanded;
        /// <summary>Seconds per light swing.</summary>
        public float SwingTime = 0.4f;
        public float Reach = 1.8f;
        /// <summary>Bodies a light swing can pass through (blunt = 1).</summary>
        public int Pierce = 1;
        public float CutChance = 0.3f;
        /// <summary>Doll launch speed at full power (blunt).</summary>
        public float Launch = 6f;
        /// <summary>Head gib chance on a full-power blunt head hit.</summary>
        public float CrushChance;
        /// <summary>Extra bleeding (nails, serrations): adds a stab wound on flesh hits.</summary>
        public bool Bleeds;
        /// <summary>LMB hold = wind up, release = swing (like the sledgehammer).</summary>
        public bool ChargeSwing;
        /// <summary>RMB = forward stab (knives) instead of the heavy overhead swing.</summary>
        public bool RmbStab;
        /// <summary>Hold LMB to run a continuous cutter (chainsaw).</summary>
        public bool Continuous;
        public string SwingSound = "melee_swing", FleshSound = "blunt_flesh", WorldSound = "blunt_world";
    }

    /// <summary>Parametric melee model builder: a handle (wrap, guard, pommel) plus one of the head shapes.</summary>
    public static class MeleeKit
    {
        static readonly Color Steel = new Color(0.62f, 0.63f, 0.66f), Dark = new Color(0.12f, 0.12f, 0.13f), Wood = new Color(0.62f, 0.44f, 0.26f),
            Rust = new Color(0.42f, 0.24f, 0.14f), Red = new Color(0.72f, 0.08f, 0.06f), Orange = new Color(0.95f, 0.45f, 0.08f);

        public static WeaponModel Build(string name, MeleeSpec s, int layer)
        {
            var m = WeaponModels.Root(name, layer);
            float tipZ;
            switch (s.Head)
            {
                case MeleeHead.Bat: case MeleeHead.SpikedBat: tipZ = Bat(m, s); break;
                case MeleeHead.Crowbar: tipZ = Crowbar(m, s); break;
                case MeleeHead.Pipe: tipZ = Pipe(m, s); break;
                case MeleeHead.Pan: tipZ = Pan(m, s); break;
                case MeleeHead.FireAxe: tipZ = Axe(m, s); break;
                case MeleeHead.Chainsaw: tipZ = Chainsaw(m, s); break;
                default: tipZ = Blade(m, s); break;
            }
            m.GripR = m.Anchor("GripR", new Vector3(0.02f, 0f, 0f), Quaternion.Euler(0, 0, -90f));
            if (s.TwoHanded && s.Head != MeleeHead.Chainsaw) m.SupportL = m.Anchor("SupportL", new Vector3(-0.02f, 0f, -0.11f), Quaternion.Euler(0, 0, 90f));
            m.Muzzle = m.Anchor("Tip", new Vector3(0, 0, tipZ), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.05f, 0), Quaternion.identity);
            return m;
        }

        static float Bat(WeaponModel m, MeleeSpec s)
        {
            // Tapered wooden (or aluminium) bat: thin handle swelling to a fat barrel.
            var b = new MeshBuilder { Color = s.HeadColor, Gloss = 0.35f };
            float z0 = -0.12f, z1 = z0 + s.HandleLen + s.HeadLen;
            b.Loft(10, 14, i =>
            {
                float t = i / 9f;
                float r = Mathf.Lerp(0.0135f, 0.032f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.85f, t)));
                if (i == 9) r *= 0.85f;
                return (new Vector3(0, 0, Mathf.Lerp(z0, z1, t)), r, r);
            }, Vector3.forward);
            b.SetColor(s.HandleColor, 0.2f);
            b.Cylinder(new Vector3(0, 0, z0 - 0.012f), new Vector3(0, 0, z0 + 0.002f), 0.022f, 0.016f, 12);
            for (int i = 0; i < 5; i++) b.Torus(new Vector3(0, 0, z0 + 0.03f + i * 0.025f), 0.0145f, 0.0018f, 12, 3, Vector3.forward);
            m.Piece("Bat", b, Mats.Lit, Vector3.zero);
            if (s.Head == MeleeHead.SpikedBat)
            {
                var n = new MeshBuilder { Color = new Color(0.55f, 0.55f, 0.57f), Gloss = 0.7f };
                var rng = new System.Random(7);
                for (int i = 0; i < 16; i++)
                {
                    float z = z1 - 0.03f - i * 0.011f;
                    float a = (float)rng.NextDouble() * 360f;
                    var d = Quaternion.Euler(0, 0, a) * Vector3.up;
                    n.Cylinder(new Vector3(0, 0, z) + d * 0.026f, new Vector3(0, 0, z) + d * 0.058f, 0.0018f, 0.0009f, 5);
                }
                // Barbed wire wrap.
                n.SetColor(new Color(0.4f, 0.38f, 0.36f), 0.5f);
                for (int i = 0; i < 4; i++) n.Torus(new Vector3(0, 0, z1 - 0.08f - i * 0.03f), 0.031f, 0.0015f, 14, 3, Vector3.forward);
                m.Piece("Nails", n, Mats.LitMetal, Vector3.zero);
            }
            return z1;
        }

        static float Crowbar(WeaponModel m, MeleeSpec s)
        {
            var b = new MeshBuilder { Color = s.HeadColor, Gloss = 0.5f };
            float z0 = -0.1f, z1 = z0 + s.HandleLen + s.HeadLen;
            b.Cylinder(new Vector3(0, 0, z0), new Vector3(0, 0, z1), 0.011f, 0.011f, 8);
            // Flat pry end at the bottom, curved claw at the top (bends toward -Y = the striking side).
            b.Box(new Vector3(0, 0.004f, z0 - 0.02f), new Vector3(0.02f, 0.008f, 0.04f));
            var p = new Vector3(0, 0, z1);
            float ang = 0f;
            for (int i = 0; i < 6; i++)
            {
                ang += 28f;
                var dir = Quaternion.Euler(ang, 0, 0) * Vector3.forward;
                b.Cylinder(p, p + dir * 0.022f, 0.011f, i == 5 ? 0.006f : 0.0105f, 8);
                p += dir * 0.022f;
            }
            b.SetColor(Dark, 0.3f);
            b.Box(p + new Vector3(0, 0.002f, 0), new Vector3(0.022f, 0.004f, 0.012f));
            m.Piece("Crowbar", b, Mats.LitMetal, Vector3.zero);
            var g = new MeshBuilder { Color = s.HandleColor, Gloss = 0.2f };
            g.Cylinder(new Vector3(0, 0, z0 + 0.03f), new Vector3(0, 0, z0 + 0.16f), 0.0135f, 0.0135f, 10);
            m.Piece("Grip", g, Mats.Lit, Vector3.zero);
            return z1 + 0.05f;
        }

        static float Pipe(WeaponModel m, MeleeSpec s)
        {
            var b = new MeshBuilder { Color = s.HeadColor, Gloss = 0.45f };
            float z0 = -0.1f, z1 = z0 + s.HandleLen + s.HeadLen;
            b.Cylinder(new Vector3(0, 0, z0), new Vector3(0, 0, z1), 0.016f, 0.016f, 12);
            b.SetColor(Color.black, 0.1f);
            b.Cylinder(new Vector3(0, 0, z1), new Vector3(0, 0, z1 + 0.0008f), 0.011f, 0.011f, 10);
            b.SetColor(s.HeadColor * 0.8f, 0.6f);
            // Threaded elbow joint on the end.
            b.Cylinder(new Vector3(0, 0, z1 - 0.05f), new Vector3(0, 0, z1 + 0.012f), 0.022f, 0.022f, 12);
            for (int i = 0; i < 3; i++) b.Torus(new Vector3(0, 0, z0 + 0.01f + i * 0.006f), 0.0165f, 0.0012f, 12, 3, Vector3.forward);
            m.Piece("Pipe", b, Mats.LitMetal, Vector3.zero);
            var t = new MeshBuilder { Color = s.HandleColor, Gloss = 0.1f };
            t.Cylinder(new Vector3(0, 0, z0 + 0.03f), new Vector3(0, 0, z0 + 0.15f), 0.0172f, 0.0172f, 12);
            m.Piece("Tape", t, Mats.Lit, Vector3.zero);
            return z1;
        }

        static float Pan(WeaponModel m, MeleeSpec s)
        {
            var h = new MeshBuilder { Color = s.HandleColor, Gloss = 0.3f };
            h.Push(Vector3.zero, Quaternion.identity);
            h.BevelBox(new Vector3(0, 0, 0.03f), new Vector3(0.022f, 0.012f, 0.2f), 0.005f);
            h.Pop();
            h.SetColor(new Color(0.5f, 0.5f, 0.52f), 0.7f);
            h.Torus(new Vector3(0, 0, -0.068f), 0.008f, 0.0025f, 10, 4, Vector3.up);
            h.Box(new Vector3(0, 0, 0.14f), new Vector3(0.018f, 0.01f, 0.03f));
            m.Piece("Handle", h, Mats.Lit, Vector3.zero);
            // The pan itself: a shallow dish facing -Y (the hitting side is the flat bottom).
            var p = new MeshBuilder { Color = s.HeadColor, Gloss = 0.55f };
            float r = 0.12f, cz = 0.15f + r;
            p.Cylinder(new Vector3(0, -0.006f, cz), new Vector3(0, 0.0f, cz), r, r, 24);
            p.Loft(3, 24, i => (new Vector3(0, i * 0.018f, cz), r + i * 0.008f, r + i * 0.008f), Vector3.up, false, false);
            p.SetColor(new Color(0.05f, 0.05f, 0.05f), 0.3f);
            p.Cylinder(new Vector3(0, 0.0f, cz), new Vector3(0, 0.0015f, cz), r - 0.003f, r - 0.003f, 24);
            m.Piece("Pan", p, Mats.LitMetal, Vector3.zero);
            return cz + r;
        }

        static float Blade(WeaponModel m, MeleeSpec s)
        {
            // Machete / cleaver / combat knife: flat blade with the edge toward -Y.
            var b = new MeshBuilder { Color = s.HeadColor, Gloss = 0.9f };
            float L = s.HeadLen, z0 = s.HandleLen * 0.5f;
            Vector2[] outline;
            float th = 0.004f;
            switch (s.Head)
            {
                case MeleeHead.Cleaver:
                    outline = new[] { new Vector2(0, -0.06f), new Vector2(L, -0.068f), new Vector2(L, 0.012f), new Vector2(0, 0.012f) };
                    th = 0.0045f;
                    break;
                case MeleeHead.Machete:
                    outline = new[] { new Vector2(0, -0.022f), new Vector2(L * 0.8f, -0.03f), new Vector2(L, -0.012f), new Vector2(L * 0.95f, 0.012f), new Vector2(0, 0.012f) };
                    th = 0.003f;
                    break;
                default: // combat knife (clip point)
                    outline = new[] { new Vector2(0, -0.014f), new Vector2(L * 0.82f, -0.01f), new Vector2(L, 0.008f), new Vector2(L * 0.68f, 0.016f), new Vector2(0, 0.016f) };
                    th = 0.004f;
                    break;
            }
            b.Push(new Vector3(0, 0, z0), Quaternion.Euler(0, -90, 0));
            b.Prism(outline, th, Vector3.zero);
            b.Pop();
            b.SetColor(s.HeadColor * 0.7f, 0.6f);
            float spineY = s.Head == MeleeHead.Cleaver ? 0.012f : s.Head == MeleeHead.CombatKnife ? 0.016f : 0.012f;
            b.Box(new Vector3(0, spineY - 0.002f, z0 + L * 0.35f), new Vector3(th + 0.001f, 0.004f, L * 0.6f));
            if (s.Head == MeleeHead.CombatKnife)
                for (int i = 0; i < 6; i++) b.Box(new Vector3(0, spineY + 0.001f, z0 + 0.02f + i * 0.009f), new Vector3(th + 0.0005f, 0.004f, 0.004f)); // serrations
            if (s.Head == MeleeHead.Cleaver) { b.SetColor(Color.black, 0.2f); b.Cylinder(new Vector3(-0.003f, -0.0f, z0 + L - 0.025f), new Vector3(0.003f, 0f, z0 + L - 0.025f), 0.007f, 0.007f, 10); }
            m.Piece("Blade", b, Mats.LitMetal, Vector3.zero);
            var h = new MeshBuilder { Color = s.HandleColor, Gloss = 0.3f };
            float hz0 = -s.HandleLen * 0.5f;
            h.BevelBox(new Vector3(0, 0, (hz0 + z0) * 0.5f), new Vector3(0.022f, 0.03f, z0 - hz0), 0.008f);
            h.SetColor(new Color(0.7f, 0.7f, 0.72f), 0.8f);
            for (int i = 0; i < 3; i++) h.Cylinder(new Vector3(-0.012f, 0, hz0 + 0.02f + i * (z0 - hz0 - 0.04f) / 2f), new Vector3(0.012f, 0, hz0 + 0.02f + i * (z0 - hz0 - 0.04f) / 2f), 0.003f, 0.003f, 6);
            if (s.Head == MeleeHead.CombatKnife) { h.SetColor(Dark, 0.5f); h.Box(new Vector3(0, 0.002f, z0 + 0.002f), new Vector3(0.016f, 0.06f, 0.006f)); h.Sphere(new Vector3(0, 0, hz0 - 0.004f), new Vector3(0.013f, 0.016f, 0.01f), 5, 8); }
            m.Piece("Handle", h, Mats.Lit, Vector3.zero);
            return z0 + L;
        }

        static float Axe(WeaponModel m, MeleeSpec s)
        {
            var h = new MeshBuilder { Color = s.HandleColor, Gloss = 0.4f };
            float z0 = -0.14f, z1 = z0 + s.HandleLen;
            h.Loft(4, 10, i => (new Vector3(0, 0, Mathf.Lerp(z0, z1, i / 3f)), 0.016f, i == 0 ? 0.02f : 0.018f), Vector3.forward);
            m.Piece("Haft", h, Mats.Lit, Vector3.zero);
            // Red head: blade toward -Y, pick spike toward +Y.
            var a = new MeshBuilder { Color = s.HeadColor, Gloss = 0.55f };
            a.BevelBox(new Vector3(0, 0, z1 - 0.03f), new Vector3(0.03f, 0.05f, 0.07f), 0.006f);
            a.Push(new Vector3(0, -0.025f, z1 - 0.03f), Quaternion.Euler(0, -90, 0));
            a.Prism(new[] { new Vector2(-0.03f, 0.005f), new Vector2(0.03f, 0.005f), new Vector2(0.06f, -0.13f), new Vector2(-0.065f, -0.13f) }, 0.012f, Vector3.zero);
            a.Pop();
            a.Push(new Vector3(0, 0.025f, z1 - 0.03f), Quaternion.Euler(0, -90, 0));
            a.Prism(new[] { new Vector2(-0.022f, -0.004f), new Vector2(0.022f, -0.004f), new Vector2(0.004f, 0.11f), new Vector2(-0.004f, 0.11f) }, 0.014f, Vector3.zero);
            a.Pop();
            a.SetColor(Steel, 0.95f);
            a.Push(new Vector3(0, -0.15f, z1 - 0.03f), Quaternion.Euler(0, -90, 0));
            a.Prism(new[] { new Vector2(-0.066f, 0.03f), new Vector2(0.061f, 0.03f), new Vector2(0.064f, -0.002f), new Vector2(-0.07f, -0.002f) }, 0.006f, Vector3.zero);
            a.Pop();
            m.Piece("AxeHead", a, Mats.LitMetal, Vector3.zero);
            return z1;
        }

        static float Chainsaw(WeaponModel m, MeleeSpec s)
        {
            // Engine body around the hand, guide bar forward, rear + top handles.
            var e = new MeshBuilder { Color = s.HeadColor, Gloss = 0.35f };
            e.BevelBox(new Vector3(0.04f, -0.02f, 0.02f), new Vector3(0.1f, 0.15f, 0.24f), 0.02f);
            e.SetColor(Dark, 0.3f);
            e.BevelBox(new Vector3(0.04f, 0.04f, -0.005f), new Vector3(0.09f, 0.04f, 0.14f), 0.012f);
            for (int i = 0; i < 6; i++) e.Box(new Vector3(0.091f, -0.02f, -0.06f + i * 0.022f), new Vector3(0.004f, 0.09f, 0.008f)); // cooling fins
            e.SetColor(Steel, 0.6f);
            e.Cylinder(new Vector3(0.091f, 0.03f, 0.06f), new Vector3(0.1f, 0.03f, 0.06f), 0.02f, 0.02f, 12); // pull-start
            e.SetColor(s.HandleColor, 0.2f);
            // Rear handle (right hand) and wrap-around top handle (left hand).
            e.Box(new Vector3(0, 0.0f, -0.07f), new Vector3(0.026f, 0.032f, 0.14f));
            e.Box(new Vector3(0, -0.06f, -0.13f), new Vector3(0.026f, 0.1f, 0.022f));
            e.Torus(new Vector3(0.04f, 0.06f, 0.1f), 0.07f, 0.0105f, 18, 6, Vector3.forward);
            m.Piece("Engine", e, Mats.Lit, new Vector3(0, 0, 0.07f));
            var bar = new MeshBuilder { Color = new Color(0.55f, 0.56f, 0.58f), Gloss = 0.8f };
            float b0 = 0.2f, b1 = 0.2f + s.HeadLen;
            bar.BevelBox(new Vector3(0, -0.04f, (b0 + b1) * 0.5f), new Vector3(0.006f, 0.06f, b1 - b0), 0.003f);
            bar.Cylinder(new Vector3(-0.003f, -0.04f, b1), new Vector3(0.003f, -0.04f, b1), 0.03f, 0.03f, 16);
            m.Piece("Bar", bar, Mats.LitMetal, Vector3.zero);
            var chain = new MeshBuilder { Color = new Color(0.22f, 0.22f, 0.23f), Gloss = 0.6f };
            for (float z = b0; z < b1; z += 0.012f)
            {
                chain.Box(new Vector3(0, -0.0085f, z), new Vector3(0.008f, 0.005f, 0.007f));
                chain.Box(new Vector3(0, -0.0715f, z), new Vector3(0.008f, 0.005f, 0.007f));
            }
            m.Piece("Chain", chain, Mats.LitMetal, Vector3.zero); // moved by MeleeWeapon to fake chain travel
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.0f, 0.13f, 0.17f), Quaternion.Euler(0f, 0f, 180f));
            return b1 + 0.03f;
        }
    }
}
