using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A built weapon model with named moving parts and anchor points.</summary>
    public sealed class WeaponModel : MonoBehaviour
    {
        public Transform GripR, SupportL, Muzzle, Eject, MagWell, Sight, Slide, Mag, Charge, Glow;
        /// <summary>Iron sight line: centre of the rear notch/aperture and the tip of the front post (model local).
        /// When aiming, the viewmodel puts both on the camera's forward axis.</summary>
        public Transform SightRear, SightFront;
        public Vector3 SlideRest, MagRest, ChargeRest;
        public Mesh MagMesh;
        public readonly List<Renderer> GlowRenderers = new List<Renderer>();

        public Transform Anchor(string name, Vector3 pos, Quaternion rot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            t.localPosition = pos;
            t.localRotation = rot;
            return t;
        }

        public Transform Piece(string name, MeshBuilder mb, Material mat, Vector3 pos, Transform parent = null)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = pos;
            var mesh = mb.ToMesh(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }

    /// <summary>Procedural weapon models at real-world scale. +Z forward, origin at the grip.</summary>
    public static class WeaponModels
    {
        static readonly Color Polymer = new Color(0.11f, 0.11f, 0.12f), Steel = new Color(0.22f, 0.22f, 0.24f), DarkSteel = new Color(0.12f, 0.12f, 0.13f),
            Brass = new Color(0.8f, 0.62f, 0.25f), Gold = new Color(0.95f, 0.75f, 0.25f), Wood = new Color(0.45f, 0.28f, 0.15f), Bronze = new Color(0.62f, 0.45f, 0.22f);

        static Quaternion RightGrip(float pitch = 10f) => Quaternion.Euler(pitch, 0, -90f);

        static readonly Color TritiumGreen = new Color(0.45f, 1f, 0.35f);

        /// <summary>A tiny glowing sight dot.</summary>
        internal static void SightDot(WeaponModel m, Vector3 pos, Color c, float radius = 0.00095f, float intensity = 3f, Transform parent = null)
        {
            var d = new MeshBuilder { Color = Color.white };
            d.Sphere(Vector3.zero, Vector3.one * radius, 4, 6);
            m.Piece("SightDot", d, Mats.Glow(c, intensity), pos, parent);
        }

        /// <summary>
        /// Adds iron sights: a rear notch (two ears) or aperture ring centred on <paramref name="rear"/> (sight-line height),
        /// a front post whose tip is <paramref name="front"/>, a glowing dot on the post, and the SightRear/SightFront anchors.
        /// rearBase/frontBase are the heights of the surfaces the sights stand on.
        /// </summary>
        internal static void IronSights(WeaponModel m, Vector3 rear, float rearBase, Vector3 front, float frontBase, Color metal, Color dot,
            bool aperture = false, bool frontWings = false)
        {
            var s = new MeshBuilder { Color = metal, Gloss = 0.35f };
            const float depth = 0.006f;
            if (aperture)
            {
                const float r = 0.0055f;
                float postTop = rear.y - r;
                s.Box(new Vector3(rear.x, (rearBase + postTop) * 0.5f, rear.z), new Vector3(0.012f, Mathf.Max(0.002f, postTop - rearBase + 0.002f), depth));
                s.Torus(rear, r, 0.0016f, 18, 6, Vector3.forward);
                for (int sx = -1; sx <= 1; sx += 2)
                    s.Box(new Vector3(rear.x + sx * 0.0095f, (rearBase + rear.y + 0.004f) * 0.5f, rear.z), new Vector3(0.0025f, rear.y + 0.004f - rearBase, depth + 0.004f));
            }
            else
            {
                const float notch = 0.0044f, earW = 0.0055f, notchDepth = 0.0035f;
                float h = Mathf.Max(rear.y - rearBase, notchDepth + 0.001f);
                s.Box(new Vector3(rear.x, rear.y - h + (h - notchDepth) * 0.5f, rear.z), new Vector3(notch + earW * 2f, h - notchDepth, depth));
                for (int sx = -1; sx <= 1; sx += 2)
                    s.Box(new Vector3(rear.x + sx * (notch + earW) * 0.5f, rear.y - h * 0.5f, rear.z), new Vector3(earW, h, depth));
            }
            float fh = Mathf.Max(front.y - frontBase, 0.004f);
            s.Box(new Vector3(front.x, front.y - fh * 0.5f, front.z), new Vector3(0.0028f, fh, 0.004f));
            s.Box(new Vector3(front.x, front.y - fh + 0.002f, front.z), new Vector3(frontWings ? 0.019f : 0.008f, 0.004f, 0.012f));
            if (frontWings)
                for (int sx = -1; sx <= 1; sx += 2)
                    s.Box(new Vector3(front.x + sx * 0.0085f, front.y - fh + (fh + 0.003f) * 0.5f, front.z), new Vector3(0.002f, fh + 0.003f, 0.006f));
            m.Piece("IronSights", s, Mats.LitMetal, Vector3.zero);
            // Dot on the shooter-facing side of the post, just below its tip.
            SightDot(m, new Vector3(front.x, front.y - 0.0016f, front.z - 0.0021f), dot);
            m.SightRear = m.Anchor("SightRear", rear, Quaternion.identity);
            m.SightFront = m.Anchor("SightFront", front, Quaternion.identity);
        }

        internal static WeaponModel Root(string name, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            return go.AddComponent<WeaponModel>();
        }

        public static WeaponModel Build(string id, int layer)
        {
            switch (id)
            {
                case "glock": return Glock(layer);
                case "uzi": return Uzi(layer);
                case "ar15": return Ar15(layer);
                case "ar17": return Ar17(layer);
                case "admin": return AdminGun(layer);
                case "knife": return Knife(layer);
                case "spawngun": return SpawnGun(layer);
                case "ropetool": return ToolModels.RopeTool(layer);
                case "gravitygun": return ToolModels.GravityGun(layer);
                case "shotgun": case "sniper": case "revolver": case "minigun": case "harpoon": return WeaponModels2.Build(id, layer);
                case "rpg": case "frag": case "c4": case "katana": case "sledgehammer": return WeaponModels3.Build(id, layer);
                case "mac11": case "katana_dual": return WeaponModels4.Build(id, layer);
                default: return Root("Fists", layer);
            }
        }

        // ------------------------------------------------------------------ Glock 19

        static WeaponModel Glock(int layer)
        {
            var m = Root("Glock 19", layer);
            var frame = new MeshBuilder { Color = Polymer, Gloss = 0.25f };
            frame.BevelBox(new Vector3(0, 0.03f, 0.07f), new Vector3(0.029f, 0.024f, 0.165f), 0.006f);
            frame.Push(new Vector3(0, -0.02f, -0.005f), Quaternion.Euler(-18, 0, 0));
            frame.BevelBox(Vector3.zero, new Vector3(0.03f, 0.105f, 0.05f), 0.008f);
            for (int i = 0; i < 3; i++) frame.Box(new Vector3(0, -0.02f + i * 0.02f, 0.026f), new Vector3(0.026f, 0.006f, 0.004f));
            frame.Pop();
            frame.Box(new Vector3(0, 0.004f, 0.05f), new Vector3(0.012f, 0.005f, 0.05f));
            frame.Box(new Vector3(0, 0.018f, 0.074f), new Vector3(0.012f, 0.03f, 0.006f));
            frame.SetColor(DarkSteel, 0.5f);
            frame.Box(new Vector3(0, 0.018f, 0.045f), new Vector3(0.006f, 0.022f, 0.006f));
            m.Piece("Frame", frame, Mats.Lit, Vector3.zero);

            var slide = new MeshBuilder { Color = Steel, Gloss = 0.55f };
            slide.BevelBox(Vector3.zero, new Vector3(0.0265f, 0.03f, 0.186f), 0.004f);
            slide.SetColor(DarkSteel, 0.4f);
            for (int i = 0; i < 6; i++) slide.Box(new Vector3(0, 0, -0.085f + i * 0.006f), new Vector3(0.0275f, 0.022f, 0.002f));
            slide.Box(new Vector3(0.0135f, 0.004f, 0.01f), new Vector3(0.002f, 0.012f, 0.05f));
            slide.Box(new Vector3(0, 0.0175f, 0.086f), new Vector3(0.004f, 0.006f, 0.006f));
            slide.Box(new Vector3(-0.007f, 0.0175f, -0.083f), new Vector3(0.006f, 0.006f, 0.006f));
            slide.Box(new Vector3(0.007f, 0.0175f, -0.083f), new Vector3(0.006f, 0.006f, 0.006f));
            slide.SetColor(Color.white, 0.2f);
            slide.Box(new Vector3(0, 0.0205f, 0.086f), new Vector3(0.002f, 0.0015f, 0.003f));
            slide.SetColor(Color.black, 0.3f);
            slide.Cylinder(new Vector3(0, -0.002f, 0.093f), new Vector3(0, -0.002f, 0.0935f), 0.0055f, 0.0055f, 10);
            m.Slide = m.Piece("Slide", slide, Mats.LitMetal, new Vector3(0, 0.057f, 0.077f));
            m.SlideRest = m.Slide.localPosition;

            var mag = new MeshBuilder { Color = Polymer, Gloss = 0.2f };
            mag.Box(new Vector3(0, -0.03f, 0), new Vector3(0.024f, 0.09f, 0.034f));
            mag.Box(new Vector3(0, -0.077f, 0.003f), new Vector3(0.03f, 0.008f, 0.042f));
            mag.SetColor(Brass, 0.7f);
            mag.Cylinder(new Vector3(0, 0.016f, -0.012f), new Vector3(0, 0.016f, 0.008f), 0.0045f, 0.0045f, 8);
            m.MagMesh = mag.ToMesh("GlockMag");
            m.Mag = m.Piece("Mag", mag, Mats.Lit, Vector3.zero, null);
            m.Mag.localRotation = Quaternion.Euler(-18, 0, 0);
            m.Mag.localPosition = new Vector3(0, -0.01f, -0.002f);
            m.MagRest = m.Mag.localPosition;

            m.GripR = m.Anchor("GripR", new Vector3(0.024f, -0.045f, -0.062f), RightGrip(14f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.03f, -0.07f, -0.02f), Quaternion.Euler(30f, 20f, 65f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.055f, 0.175f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(0.016f, 0.065f, 0.085f), Quaternion.identity);
            m.MagWell = m.Anchor("MagWell", new Vector3(0, -0.075f, -0.02f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.0805f, 0), Quaternion.identity);
            m.Charge = m.Anchor("ChargePoint", new Vector3(-0.005f, 0.06f, 0.0f), Quaternion.Euler(0, 0, 90));
            // Sights are modelled on the slide (rear ears at slide z -0.083, front post at +0.086, tops at y 0.0205).
            SightDot(m, new Vector3(0, 0.0189f, 0.0829f), TritiumGreen, 0.00095f, 3f, m.Slide);
            SightDot(m, new Vector3(-0.007f, 0.0178f, -0.0862f), TritiumGreen, 0.0008f, 1.4f, m.Slide);
            SightDot(m, new Vector3(0.007f, 0.0178f, -0.0862f), TritiumGreen, 0.0008f, 1.4f, m.Slide);
            m.SightRear = m.Anchor("SightRear", new Vector3(0, 0.057f + 0.0205f, 0.077f - 0.083f), Quaternion.identity);
            m.SightFront = m.Anchor("SightFront", new Vector3(0, 0.057f + 0.0205f, 0.077f + 0.086f), Quaternion.identity);
            return m;
        }

        // ------------------------------------------------------------------ UZI

        static WeaponModel Uzi(int layer)
        {
            var m = Root("UZI", layer);
            var body = new MeshBuilder { Color = DarkSteel, Gloss = 0.45f };
            body.BevelBox(new Vector3(0, 0.05f, 0.06f), new Vector3(0.042f, 0.055f, 0.25f), 0.005f);
            body.SetColor(Polymer, 0.2f);
            body.BevelBox(new Vector3(0, 0.012f, 0.14f), new Vector3(0.046f, 0.03f, 0.09f), 0.008f);
            body.Push(new Vector3(0, -0.035f, 0f), Quaternion.Euler(-6, 0, 0));
            body.BevelBox(Vector3.zero, new Vector3(0.034f, 0.11f, 0.045f), 0.008f);
            body.Pop();
            body.SetColor(DarkSteel, 0.5f);
            body.Box(new Vector3(0, 0.006f, 0.04f), new Vector3(0.012f, 0.005f, 0.05f));
            body.Box(new Vector3(0, 0.02f, 0.065f), new Vector3(0.012f, 0.032f, 0.005f));
            body.Cylinder(new Vector3(0, 0.05f, 0.185f), new Vector3(0, 0.05f, 0.24f), 0.011f, 0.011f, 12);
            body.Box(new Vector3(0, 0.086f, 0.16f), new Vector3(0.012f, 0.018f, 0.01f));
            body.Box(new Vector3(0, 0.085f, -0.05f), new Vector3(0.02f, 0.016f, 0.012f));
            // folding wire stock
            body.SetColor(Steel, 0.6f);
            body.Cylinder(new Vector3(0.016f, 0.045f, -0.065f), new Vector3(0.016f, 0.03f, -0.25f), 0.004f, 0.004f, 6);
            body.Cylinder(new Vector3(-0.016f, 0.045f, -0.065f), new Vector3(-0.016f, 0.03f, -0.25f), 0.004f, 0.004f, 6);
            body.Box(new Vector3(0, 0.02f, -0.255f), new Vector3(0.04f, 0.05f, 0.008f));
            m.Piece("Body", body, Mats.LitMetal, Vector3.zero);
            var knob = new MeshBuilder { Color = DarkSteel, Gloss = 0.4f };
            knob.BevelBox(Vector3.zero, new Vector3(0.012f, 0.012f, 0.02f), 0.003f);
            m.Slide = m.Piece("CockingKnob", knob, Mats.LitMetal, new Vector3(0, 0.083f, 0.08f));
            m.SlideRest = m.Slide.localPosition;
            var mag = new MeshBuilder { Color = DarkSteel, Gloss = 0.35f };
            mag.Box(new Vector3(0, -0.07f, 0), new Vector3(0.024f, 0.19f, 0.032f));
            mag.Box(new Vector3(0, -0.165f, 0), new Vector3(0.028f, 0.008f, 0.036f));
            m.MagMesh = mag.ToMesh("UziMag");
            m.Mag = m.Piece("Mag", mag, Mats.LitMetal, new Vector3(0, -0.005f, -0.002f));
            m.Mag.localRotation = Quaternion.Euler(-6, 0, 0);
            m.MagRest = m.Mag.localPosition;
            m.GripR = m.Anchor("GripR", new Vector3(0.026f, -0.055f, -0.06f), RightGrip(8f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.005f, -0.04f, 0.1f), Quaternion.Euler(-10f, 0, 170f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.05f, 0.245f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(0.023f, 0.06f, 0.05f), Quaternion.identity);
            m.MagWell = m.Anchor("MagWell", new Vector3(0, -0.17f, -0.01f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.097f, -0.05f), Quaternion.identity);
            m.Charge = m.Anchor("ChargePoint", new Vector3(0, 0.09f, 0.06f), Quaternion.Euler(0, 0, 180));
            IronSights(m, new Vector3(0, 0.0985f, -0.05f), 0.093f, new Vector3(0, 0.0985f, 0.16f), 0.095f, DarkSteel, TritiumGreen, false, true);
            return m;
        }

        // ------------------------------------------------------------------ AR-15

        static WeaponModel Ar15(int layer)
        {
            var m = Root("AR-15", layer);
            var b = new MeshBuilder { Color = DarkSteel, Gloss = 0.4f };
            b.BevelBox(new Vector3(0, 0.05f, 0.05f), new Vector3(0.026f, 0.06f, 0.21f), 0.004f);
            b.BevelBox(new Vector3(0, 0.085f, 0.05f), new Vector3(0.022f, 0.012f, 0.2f), 0.003f);
            for (int i = 0; i < 10; i++) b.Box(new Vector3(0, 0.093f, -0.04f + i * 0.019f), new Vector3(0.022f, 0.004f, 0.008f));
            b.SetColor(Polymer, 0.2f);
            b.Loft(2, 8, i => (new Vector3(0, 0.055f, 0.16f + i * 0.28f), 0.025f, 0.025f), Vector3.forward);
            for (int i = 0; i < 8; i++) b.Box(new Vector3(0, 0.055f, 0.18f + i * 0.032f), new Vector3(0.052f, 0.008f, 0.012f));
            b.SetColor(DarkSteel, 0.5f);
            b.Cylinder(new Vector3(0, 0.055f, 0.44f), new Vector3(0, 0.055f, 0.6f), 0.009f, 0.009f, 10);
            b.Cylinder(new Vector3(0, 0.055f, 0.6f), new Vector3(0, 0.055f, 0.64f), 0.012f, 0.012f, 10);
            b.Box(new Vector3(0, 0.085f, 0.45f), new Vector3(0.006f, 0.05f, 0.014f));
            b.Box(new Vector3(0, 0.105f, -0.05f), new Vector3(0.018f, 0.025f, 0.02f));
            b.SetColor(Polymer, 0.2f);
            b.Push(new Vector3(0, -0.025f, -0.02f), Quaternion.Euler(-20, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.028f, 0.09f, 0.04f), 0.008f);
            b.Pop();
            b.Box(new Vector3(0, 0.003f, 0.035f), new Vector3(0.012f, 0.005f, 0.06f));
            b.Cylinder(new Vector3(0, 0.055f, -0.06f), new Vector3(0, 0.055f, -0.2f), 0.016f, 0.016f, 10);
            b.BevelBox(new Vector3(0, 0.035f, -0.28f), new Vector3(0.042f, 0.085f, 0.18f), 0.01f);
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);
            var ch = new MeshBuilder { Color = DarkSteel, Gloss = 0.4f };
            ch.Box(Vector3.zero, new Vector3(0.03f, 0.01f, 0.015f));
            m.Charge = m.Piece("ChargingHandle", ch, Mats.LitMetal, new Vector3(0, 0.085f, -0.06f));
            m.ChargeRest = m.Charge.localPosition;
            var mag = new MeshBuilder { Color = DarkSteel, Gloss = 0.35f };
            mag.Push(Vector3.zero, Quaternion.Euler(10, 0, 0));
            mag.Box(new Vector3(0, -0.06f, 0), new Vector3(0.022f, 0.15f, 0.06f));
            mag.Box(new Vector3(0, -0.138f, 0.004f), new Vector3(0.026f, 0.008f, 0.066f));
            mag.Pop();
            m.MagMesh = mag.ToMesh("ArMag");
            m.Mag = m.Piece("Mag", mag, Mats.LitMetal, new Vector3(0, 0.012f, 0.08f));
            m.MagRest = m.Mag.localPosition;
            m.GripR = m.Anchor("GripR", new Vector3(0.024f, -0.045f, -0.08f), RightGrip(18f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.004f, 0.012f, 0.27f), Quaternion.Euler(-15f, 0, 165f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.055f, 0.645f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(0.016f, 0.06f, 0.06f), Quaternion.identity);
            m.MagWell = m.Anchor("MagWell", new Vector3(0, -0.13f, 0.1f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.118f, -0.05f), Quaternion.identity);
            IronSights(m, new Vector3(0, 0.122f, -0.05f), 0.1175f, new Vector3(0, 0.122f, 0.45f), 0.108f, DarkSteel, TritiumGreen, true, true);
            return m;
        }

        // ------------------------------------------------------------------ ArmaLite AR-17 (semi-auto shotgun)

        static WeaponModel Ar17(int layer)
        {
            var m = Root("AR-17", layer);
            var b = new MeshBuilder { Color = Bronze, Gloss = 0.6f };
            b.BevelBox(new Vector3(0, 0.045f, 0.06f), new Vector3(0.032f, 0.07f, 0.26f), 0.008f);
            b.SetColor(DarkSteel, 0.5f);
            b.Cylinder(new Vector3(0, 0.06f, 0.19f), new Vector3(0, 0.06f, 0.82f), 0.0125f, 0.0115f, 12);
            b.Sphere(new Vector3(0, 0.074f, 0.8f), Vector3.one * 0.003f, 4, 6);
            b.SetColor(Polymer, 0.25f);
            b.BevelBox(new Vector3(0, 0.035f, 0.36f), new Vector3(0.035f, 0.035f, 0.26f), 0.012f);
            b.Push(new Vector3(0, -0.03f, -0.02f), Quaternion.Euler(-22, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.03f, 0.09f, 0.045f), 0.01f);
            b.Pop();
            b.Loft(5, 12, i => (new Vector3(0, 0.03f - i * 0.012f, -0.07f - i * 0.065f), 0.022f, 0.026f + i * 0.008f), Vector3.back, true, true);
            b.Box(new Vector3(0, 0.003f, 0.04f), new Vector3(0.012f, 0.005f, 0.06f));
            b.SetColor(Bronze * 0.8f, 0.5f);
            b.Box(new Vector3(0.0165f, 0.05f, 0.08f), new Vector3(0.002f, 0.025f, 0.06f));
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);
            var bolt = new MeshBuilder { Color = new Color(0.7f, 0.7f, 0.72f), Gloss = 0.8f };
            bolt.Box(Vector3.zero, new Vector3(0.012f, 0.01f, 0.018f));
            m.Charge = m.Piece("BoltHandle", bolt, Mats.LitMetal, new Vector3(0.022f, 0.055f, 0.03f));
            m.ChargeRest = m.Charge.localPosition;
            var shell = new MeshBuilder { Color = new Color(0.75f, 0.12f, 0.1f), Gloss = 0.4f };
            shell.Cylinder(new Vector3(0, 0, -0.03f), new Vector3(0, 0, 0.02f), 0.0105f, 0.0105f, 10);
            shell.SetColor(Brass, 0.8f);
            shell.Cylinder(new Vector3(0, 0, -0.04f), new Vector3(0, 0, -0.03f), 0.011f, 0.011f, 10);
            m.MagMesh = shell.ToMesh("Shell");
            m.Mag = m.Piece("Shell", shell, Mats.Lit, new Vector3(0.03f, 0.05f, 0.08f));
            m.MagRest = m.Mag.localPosition;
            m.Mag.gameObject.SetActive(false);
            m.GripR = m.Anchor("GripR", new Vector3(0.024f, -0.05f, -0.085f), RightGrip(20f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.004f, 0.0f, 0.36f), Quaternion.Euler(-15f, 0, 165f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.06f, 0.825f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(0.02f, 0.055f, 0.08f), Quaternion.identity);
            m.MagWell = m.Anchor("LoadPort", new Vector3(0.02f, 0.05f, 0.08f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.08f, 0.0f), Quaternion.identity);
            IronSights(m, new Vector3(0, 0.089f, 0.0f), 0.08f, new Vector3(0, 0.089f, 0.79f), 0.071f, DarkSteel, new Color(1f, 0.55f, 0.15f));
            return m;
        }

        // ------------------------------------------------------------------ Admin Gun

        static WeaponModel AdminGun(int layer)
        {
            var m = Root("Admin Gun", layer);
            var b = new MeshBuilder { Color = Gold, Gloss = 0.9f };
            b.BevelBox(new Vector3(0, 0.05f, 0.07f), new Vector3(0.04f, 0.05f, 0.2f), 0.01f);
            b.Push(new Vector3(0, -0.025f, -0.01f), Quaternion.Euler(-15, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.032f, 0.1f, 0.05f), 0.01f);
            b.Pop();
            b.SetColor(new Color(0.15f, 0.12f, 0.1f), 0.6f);
            b.Cylinder(new Vector3(0, 0.05f, 0.17f), new Vector3(0, 0.05f, 0.24f), 0.016f, 0.012f, 14);
            b.SetColor(Gold, 0.9f);
            b.Torus(new Vector3(0, 0.05f, 0.24f), 0.022f, 0.005f, 16, 6, Vector3.forward);
            b.Box(new Vector3(0, 0.08f, 0.0f), new Vector3(0.01f, 0.014f, 0.08f));
            b.Box(new Vector3(0, 0.006f, 0.05f), new Vector3(0.012f, 0.005f, 0.05f));
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);
            var coil = new MeshBuilder { Color = Color.white };
            for (int i = 0; i < 4; i++) coil.Torus(new Vector3(0, 0.05f, 0.04f + i * 0.04f), 0.023f, 0.004f, 14, 5, Vector3.forward);
            m.Glow = m.Piece("Coils", coil, Mats.Glow(new Color(0.3f, 0.9f, 1f), 3f), Vector3.zero);
            m.GlowRenderers.Add(m.Glow.GetComponent<Renderer>());
            m.GripR = m.Anchor("GripR", new Vector3(0.026f, -0.05f, -0.07f), RightGrip(12f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.03f, -0.07f, -0.02f), Quaternion.Euler(30f, 20f, 65f));
            m.Muzzle = m.Anchor("Muzzle", new Vector3(0, 0.05f, 0.25f), Quaternion.identity);
            m.Eject = m.Anchor("Eject", new Vector3(0.02f, 0.07f, 0.06f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.092f, 0), Quaternion.identity);
            IronSights(m, new Vector3(0, 0.094f, -0.03f), 0.087f, new Vector3(0, 0.094f, 0.15f), 0.075f, new Color(0.15f, 0.12f, 0.1f), new Color(0.3f, 0.9f, 1f));
            return m;
        }

        // ------------------------------------------------------------------ Knife

        static WeaponModel Knife(int layer)
        {
            var m = Root("Knife", layer);
            var blade = new MeshBuilder { Color = new Color(0.78f, 0.79f, 0.82f), Gloss = 0.95f };
            blade.Push(new Vector3(0, 0.0f, 0.05f), Quaternion.Euler(0, -90, 0));
            blade.Prism(new[] { new Vector2(0, -0.012f), new Vector2(0.15f, -0.006f), new Vector2(0.175f, 0.012f), new Vector2(0.11f, 0.016f), new Vector2(0, 0.016f) }, 0.0035f, Vector3.zero);
            blade.Pop();
            blade.SetColor(new Color(0.55f, 0.56f, 0.6f), 0.6f);
            blade.Box(new Vector3(0, 0.004f, 0.11f), new Vector3(0.0045f, 0.003f, 0.11f));
            m.Piece("Blade", blade, Mats.LitMetal, Vector3.zero);
            var handle = new MeshBuilder { Color = new Color(0.1f, 0.1f, 0.1f), Gloss = 0.3f };
            handle.Box(new Vector3(0, 0.002f, 0.048f), new Vector3(0.012f, 0.05f, 0.008f));
            handle.Loft(6, 10, i => (new Vector3(0, 0.002f, 0.042f - i * 0.022f), 0.0105f + Mathf.Sin(i * 1.1f) * 0.0015f, 0.015f + Mathf.Sin(i * 1.1f) * 0.002f), Vector3.back);
            for (int i = 0; i < 5; i++) handle.Torus(new Vector3(0, 0.002f, 0.03f - i * 0.02f), 0.0125f, 0.0015f, 10, 3, Vector3.forward);
            handle.SetColor(Steel, 0.7f);
            handle.Sphere(new Vector3(0, 0.002f, -0.072f), new Vector3(0.012f, 0.014f, 0.008f), 5, 8);
            m.Piece("Handle", handle, Mats.Lit, Vector3.zero);
            m.GripR = m.Anchor("GripR", new Vector3(0.02f, -0.0f, -0.075f), Quaternion.Euler(0, 0, -90f));
            m.Muzzle = m.Anchor("Tip", new Vector3(0, 0.002f, 0.22f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.05f, 0), Quaternion.identity);
            return m;
        }

        // ------------------------------------------------------------------ Spawn Gun

        static WeaponModel SpawnGun(int layer)
        {
            var m = Root("Spawn Gun", layer);
            var b = new MeshBuilder { Color = new Color(0.85f, 0.86f, 0.9f), Gloss = 0.6f };
            b.BevelBox(new Vector3(0, 0.05f, 0.06f), new Vector3(0.06f, 0.07f, 0.22f), 0.015f);
            b.SetColor(new Color(0.15f, 0.15f, 0.18f), 0.3f);
            b.Push(new Vector3(0, -0.03f, -0.01f), Quaternion.Euler(-12, 0, 0));
            b.BevelBox(Vector3.zero, new Vector3(0.034f, 0.1f, 0.048f), 0.012f);
            b.Pop();
            b.Box(new Vector3(0, 0.006f, 0.05f), new Vector3(0.012f, 0.005f, 0.05f));
            b.SetColor(new Color(0.3f, 0.3f, 0.35f), 0.7f);
            b.Cylinder(new Vector3(0, 0.05f, 0.17f), new Vector3(0, 0.05f, 0.21f), 0.03f, 0.045f, 18);
            b.Cylinder(new Vector3(0.036f, 0.065f, -0.03f), new Vector3(0.036f, 0.065f, 0.12f), 0.012f, 0.012f, 10);
            b.Cylinder(new Vector3(-0.036f, 0.065f, -0.03f), new Vector3(-0.036f, 0.065f, 0.12f), 0.012f, 0.012f, 10);
            b.SetColor(new Color(0.08f, 0.08f, 0.1f), 0.9f);
            b.Box(new Vector3(0, 0.07f, -0.052f), new Vector3(0.045f, 0.032f, 0.004f));
            m.Piece("Body", b, Mats.LitMetal, Vector3.zero);
            var glow = new MeshBuilder { Color = Color.white };
            glow.Sphere(new Vector3(0, 0.05f, 0.205f), Vector3.one * 0.022f, 8, 12);
            glow.Cylinder(new Vector3(0.036f, 0.065f, -0.02f), new Vector3(0.036f, 0.065f, 0.11f), 0.0128f, 0.0128f, 10, false);
            glow.Cylinder(new Vector3(-0.036f, 0.065f, -0.02f), new Vector3(-0.036f, 0.065f, 0.11f), 0.0128f, 0.0128f, 10, false);
            m.Glow = m.Piece("Glow", glow, Mats.Glow(new Color(0.3f, 1f, 0.6f), 2.5f), Vector3.zero);
            m.GlowRenderers.Add(m.Glow.GetComponent<Renderer>());
            m.GripR = m.Anchor("GripR", new Vector3(0.025f, -0.05f, -0.07f), RightGrip(12f));
            m.SupportL = m.Anchor("SupportL", new Vector3(-0.01f, 0.0f, 0.1f), Quaternion.Euler(-10f, 0, 160f));
            m.Muzzle = m.Anchor("Emitter", new Vector3(0, 0.05f, 0.215f), Quaternion.identity);
            m.Sight = m.Anchor("Sight", new Vector3(0, 0.1f, 0), Quaternion.identity);
            return m;
        }
    }
}
