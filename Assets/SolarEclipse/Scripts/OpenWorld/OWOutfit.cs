using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Puts an Open World outfit (OWSave.OutfitCode: top;pants;shoes;jewelry;glasses;mask;hat item ids) on a body:
    /// clothing colours on the Appearance before the meshes are built, then masks, chains, watches, grills, glasses
    /// and hats as extra meshes on the head / chest / arm. Used by other players' avatars and the locker preview.
    /// </summary>
    public static class OWOutfit
    {
        public static string[] Split(string code) => (code ?? "").Split(';');

        static OWItem Slot(string[] s, int i) => i < s.Length ? OWItems.Get(s[i]) : null;

        public static bool HasHat(string code) => Slot(Split(code), 6) != null;

        /// <summary>Clothing colours (call before building the body meshes).</summary>
        public static void Apply(Appearance a, string code)
        {
            if (a == null || string.IsNullOrEmpty(code)) return;
            var s = Split(code);
            var top = Slot(s, 0); var pants = Slot(s, 1); var shoes = Slot(s, 2);
            if (top != null) { a.Shirt = top.Color; a.LongSleeves = top.LongSleeves; }
            if (pants != null) a.Pants = pants.Color;
            if (shoes != null) a.Shoes = shoes.Color;
        }

        /// <summary>Accessory meshes. Any of the transforms may be null.</summary>
        public static void Attach(Transform head, Transform chest, Transform forearm, string code, int layer)
        {
            if (string.IsNullOrEmpty(code)) return;
            var s = Split(code);
            var jewel = Slot(s, 3); var glasses = Slot(s, 4); var mask = Slot(s, 5); var hat = Slot(s, 6);
            if (head != null)
            {
                if (mask != null) Piece(head, "Mask", MaskMesh(mask), layer);
                if (glasses != null && (mask == null || mask.Variant == 6)) Piece(head, "Glasses", GlassesMesh(glasses), layer);
                if (jewel != null && jewel.Variant == 4 && (mask == null || mask.Variant == 6 || mask.Variant == 4)) Piece(head, "Grill", GrillMesh(jewel), layer);
                if (hat != null) Hats.Attach(head, hat.Variant, hat.Color, layer);
            }
            if (chest != null && jewel != null && jewel.Variant <= 1) Piece(chest, "Chain", ChainMesh(jewel), layer);
            if (forearm != null && jewel != null && (jewel.Variant == 2 || jewel.Variant == 3)) Piece(forearm, "Watch", WatchMesh(jewel), layer);
        }

        static void Piece(Transform parent, string name, Mesh mesh, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
        }

        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        static Mesh Cached(string key, System.Action<MeshBuilder> build)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder();
            build(mb);
            m = mb.ToMesh(key);
            cache[key] = m;
            return m;
        }

        static float Y => HumanModel.HeadCenterY;

        static Mesh MaskMesh(OWItem it) => Cached("mask" + it.Id, mb =>
        {
            var a = it.Color; var b = it.Color2;
            var c = new Vector3(0, Y + 0.005f, 0.012f);
            switch (it.Variant)
            {
                case 0: // skull
                    mb.SetColor(a, 0.3f); mb.Sphere(c, new Vector3(0.112f, 0.13f, 0.12f), 10, 14);
                    mb.SetColor(b, 0.1f);
                    mb.Sphere(c + new Vector3(-0.04f, 0.02f, 0.105f), new Vector3(0.026f, 0.03f, 0.012f), 6, 8);
                    mb.Sphere(c + new Vector3(0.04f, 0.02f, 0.105f), new Vector3(0.026f, 0.03f, 0.012f), 6, 8);
                    mb.Box(c + new Vector3(0, -0.025f, 0.118f), new Vector3(0.015f, 0.02f, 0.006f));
                    for (int i = -3; i <= 3; i++) mb.Box(c + new Vector3(i * 0.011f, -0.065f, 0.112f), new Vector3(0.004f, 0.022f, 0.006f));
                    break;
                case 1: // clown
                    mb.SetColor(a, 0.4f); mb.Sphere(c, new Vector3(0.112f, 0.13f, 0.12f), 10, 14);
                    mb.SetColor(b, 0.6f); mb.Sphere(c + new Vector3(0, -0.01f, 0.125f), Vector3.one * 0.022f, 6, 8);
                    mb.Box(c + new Vector3(0, -0.06f, 0.112f), new Vector3(0.06f, 0.012f, 0.008f));
                    mb.SetColor(new Color(0.1f, 0.3f, 0.9f), 0.3f);
                    mb.Box(c + new Vector3(-0.04f, 0.03f, 0.112f), new Vector3(0.022f, 0.03f, 0.006f));
                    mb.Box(c + new Vector3(0.04f, 0.03f, 0.112f), new Vector3(0.022f, 0.03f, 0.006f));
                    mb.SetColor(new Color(1f, 0.5f, 0.1f), 0.1f); // wild hair
                    mb.Sphere(c + new Vector3(-0.09f, 0.06f, -0.01f), Vector3.one * 0.05f, 6, 8);
                    mb.Sphere(c + new Vector3(0.09f, 0.06f, -0.01f), Vector3.one * 0.05f, 6, 8);
                    break;
                case 2: // hockey
                    mb.SetColor(a, 0.5f);
                    mb.Sphere(c + new Vector3(0, 0, 0.04f), new Vector3(0.1f, 0.125f, 0.09f), 9, 12);
                    mb.SetColor(new Color(0.08f, 0.08f, 0.08f), 0.2f);
                    for (int i = 0; i < 6; i++) mb.Sphere(c + new Vector3((i % 3 - 1) * 0.035f, -0.04f - (i / 3) * 0.022f, 0.125f), Vector3.one * 0.006f, 4, 5);
                    mb.Sphere(c + new Vector3(-0.038f, 0.025f, 0.122f), new Vector3(0.02f, 0.014f, 0.006f), 5, 6);
                    mb.Sphere(c + new Vector3(0.038f, 0.025f, 0.122f), new Vector3(0.02f, 0.014f, 0.006f), 5, 6);
                    mb.SetColor(b, 0.3f);
                    mb.Box(c + new Vector3(-0.07f, 0.05f, 0.1f), new Vector3(0.02f, 0.006f, 0.004f));
                    mb.Box(c + new Vector3(0.07f, 0.05f, 0.1f), new Vector3(0.02f, 0.006f, 0.004f));
                    break;
                case 3: // pig
                    mb.SetColor(a, 0.3f); mb.Sphere(c, new Vector3(0.115f, 0.13f, 0.12f), 10, 14);
                    mb.SetColor(b, 0.3f); mb.Cylinder(c + new Vector3(0, -0.02f, 0.11f), c + new Vector3(0, -0.02f, 0.15f), 0.035f, 0.032f, 10);
                    mb.SetColor(new Color(0.3f, 0.1f, 0.15f), 0.2f);
                    mb.Sphere(c + new Vector3(-0.012f, -0.02f, 0.151f), Vector3.one * 0.007f, 4, 5);
                    mb.Sphere(c + new Vector3(0.012f, -0.02f, 0.151f), Vector3.one * 0.007f, 4, 5);
                    mb.SetColor(a, 0.3f);
                    mb.Sphere(c + new Vector3(-0.08f, 0.11f, 0f), new Vector3(0.03f, 0.04f, 0.01f), 5, 6);
                    mb.Sphere(c + new Vector3(0.08f, 0.11f, 0f), new Vector3(0.03f, 0.04f, 0.01f), 5, 6);
                    break;
                case 4: // ski mask
                    mb.SetColor(a, 0.05f); mb.Sphere(c + new Vector3(0, 0.01f, -0.005f), new Vector3(0.116f, 0.14f, 0.124f), 10, 14);
                    mb.SetColor(b, 0.05f); mb.Box(c + new Vector3(0, 0.07f, 0.06f), new Vector3(0.2f, 0.012f, 0.12f));
                    mb.SetColor(new Color(0.92f, 0.75f, 0.62f), 0.3f); mb.Box(c + new Vector3(0, 0.025f, 0.112f), new Vector3(0.09f, 0.025f, 0.008f)); // eye slot
                    break;
                case 5: // gas mask
                    mb.SetColor(a, 0.3f); mb.Sphere(c + new Vector3(0, -0.01f, 0.05f), new Vector3(0.1f, 0.11f, 0.09f), 9, 12);
                    mb.SetColor(b, 0.9f);
                    mb.Cylinder(c + new Vector3(-0.04f, 0.025f, 0.11f), c + new Vector3(-0.04f, 0.025f, 0.13f), 0.024f, 0.024f, 10);
                    mb.Cylinder(c + new Vector3(0.04f, 0.025f, 0.11f), c + new Vector3(0.04f, 0.025f, 0.13f), 0.024f, 0.024f, 10);
                    mb.SetColor(new Color(0.2f, 0.2f, 0.2f), 0.4f);
                    mb.Cylinder(c + new Vector3(0, -0.06f, 0.11f), c + new Vector3(0, -0.07f, 0.17f), 0.03f, 0.034f, 10);
                    break;
                case 6: // bandana
                    mb.SetColor(a, 0.1f); mb.Sphere(c + new Vector3(0, -0.06f, 0.035f), new Vector3(0.106f, 0.06f, 0.1f), 7, 12);
                    mb.SetColor(b, 0.1f); for (int i = 0; i < 5; i++) mb.Sphere(c + new Vector3((i - 2) * 0.03f, -0.06f, 0.12f), Vector3.one * 0.006f, 4, 5);
                    break;
                default: // robot
                    mb.SetColor(a, 0.8f); mb.BevelBox(c + new Vector3(0, 0.005f, 0.01f), new Vector3(0.2f, 0.24f, 0.22f), 0.03f);
                    mb.SetColor(b, 0.9f); mb.Box(c + new Vector3(0, 0.03f, 0.12f), new Vector3(0.16f, 0.04f, 0.01f));
                    mb.SetColor(new Color(0.3f, 0.3f, 0.32f), 0.6f);
                    mb.Cylinder(c + new Vector3(0, 0.12f, 0), c + new Vector3(0, 0.18f, 0), 0.006f, 0.006f, 6);
                    mb.SetColor(b, 0.9f); mb.Sphere(c + new Vector3(0, 0.185f, 0), Vector3.one * 0.012f, 5, 6);
                    break;
            }
        });

        static Mesh GlassesMesh(OWItem it) => Cached("glasses" + it.Id, mb =>
        {
            var c = new Vector3(0, Y + 0.025f, 0.108f);
            mb.SetColor(it.Color, 0.8f);
            mb.Box(c, new Vector3(0.12f, 0.008f, 0.006f));
            mb.Box(c + new Vector3(-0.062f, 0, -0.05f), new Vector3(0.006f, 0.006f, 0.1f));
            mb.Box(c + new Vector3(0.062f, 0, -0.05f), new Vector3(0.006f, 0.006f, 0.1f));
            mb.SetColor(it.Color2, 0.95f);
            mb.Box(c + new Vector3(-0.032f, -0.012f, 0.002f), new Vector3(0.046f, 0.03f, 0.004f));
            mb.Box(c + new Vector3(0.032f, -0.012f, 0.002f), new Vector3(0.046f, 0.03f, 0.004f));
        });

        static Mesh GrillMesh(OWItem it) => Cached("grill", mb =>
        {
            mb.SetColor(it.Color, 0.95f);
            mb.Box(new Vector3(0, Y - 0.055f, 0.104f), new Vector3(0.05f, 0.012f, 0.006f));
        });

        static Mesh ChainMesh(OWItem it) => Cached("chain" + it.Id, mb =>
        {
            mb.SetColor(it.Color, 0.95f);
            const int links = 22;
            for (int i = 0; i < links; i++)
            {
                float a = i / (float)links * Mathf.PI * 2f;
                float x = Mathf.Sin(a) * 0.105f, z = Mathf.Cos(a) * 0.085f;
                float y = 0.27f - (z > 0f ? z * 1.6f : 0f);
                mb.Sphere(new Vector3(x, y, z + 0.015f), Vector3.one * (it.Variant == 1 ? 0.014f : 0.012f), 4, 6);
            }
            mb.SetColor(it.Color2, 0.98f);
            mb.Box(new Vector3(0, 0.11f, 0.118f), new Vector3(0.05f, 0.055f, 0.014f)); // pendant
        });

        static Mesh WatchMesh(OWItem it) => Cached("watch" + it.Id, mb =>
        {
            mb.SetColor(it.Color, 0.95f);
            mb.Cylinder(new Vector3(0, -0.2f, 0), new Vector3(0, -0.225f, 0), 0.04f, 0.04f, 10);
            mb.SetColor(new Color(0.05f, 0.05f, 0.08f), 0.9f);
            mb.Box(new Vector3(0, -0.212f, 0.038f), new Vector3(0.022f, 0.02f, 0.006f));
        });
    }
}
