using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Procedural hats for player profiles. Meshes are built in head-part local space (the same space as
    /// HumanModel's head: centre at y = HeadCenterY, face towards +z) and cached per hat + colour.
    /// </summary>
    public static class Hats
    {
        public static readonly string[] Names =
        {
            "None", "Cap", "Backwards cap", "Beanie", "Cowboy hat", "Top hat", "Hard hat", "Army helmet",
            "Crown", "Party hat", "Headband", "Bucket hat", "Halo", "Devil horns", "Chef hat", "Viking helmet",
        };
        public static int Count => Names.Length;

        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        static readonly Color Gold = new Color(1f, 0.78f, 0.2f), White = new Color(0.95f, 0.95f, 0.93f), Horn = new Color(0.92f, 0.86f, 0.72f);

        const float CY = HumanModel.HeadCenterY;   // head centre height in head space
        const float Top = CY + 0.118f;             // top of the skull
        const float Brow = CY + 0.045f;            // just above the eyebrows

        /// <summary>Adds the hat under a head transform (removing any previous one). Returns null for "None".</summary>
        public static GameObject Attach(Transform head, int hat, Color color, int layer)
        {
            if (head == null) return null;
            var old = head.Find("Hat");
            if (old != null) Object.Destroy(old.gameObject);
            var mesh = Get(hat, color);
            if (mesh == null) return null;
            var go = new GameObject("Hat");
            go.layer = layer;
            go.transform.SetParent(head, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        public static Mesh Get(int hat, Color color)
        {
            if (hat <= 0 || hat >= Count) return null;
            string key = hat + ":" + ColorUtility.ToHtmlStringRGB(color);
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            if (cache.Count > 64) cache.Clear();
            var mb = new MeshBuilder { Color = color, Gloss = 0.25f };
            try { Build(mb, hat, color); }
            catch (System.Exception e) { Debug.LogWarning("[Hats] " + Names[hat] + ": " + e.Message); return null; }
            m = mb.ToMesh("Hat_" + Names[hat]);
            cache[key] = m;
            return m;
        }

        static Color Darker(Color c, float k = 0.6f) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        /// <summary>Skull-hugging dome clipped flat below <paramref name="cutY"/> (head space).</summary>
        static void Dome(MeshBuilder mb, Vector3 radii, float cutY, float cutBackDrop = 0f, float yOffset = 0f)
        {
            var c = new Vector3(0, CY + yOffset, -0.004f);
            mb.Sphere(c, radii, 12, 20, p =>
            {
                float back = Mathf.Clamp01(-p.z / radii.z);
                float cut = cutY - back * cutBackDrop - c.y;
                return p.y < cut ? new Vector3(p.x, cut, p.z) : p;
            });
        }

        /// <summary>Flat disc (elliptical through scale) centred at <paramref name="center"/>.</summary>
        static void Disc(MeshBuilder mb, Vector3 center, float radius, float thickness, Vector3 scale, Quaternion rot)
        {
            mb.Push(center, rot, scale);
            mb.Cylinder(new Vector3(0, -thickness * 0.5f, 0), new Vector3(0, thickness * 0.5f, 0), radius, radius, 20, true);
            mb.Pop();
        }

        static void Build(MeshBuilder mb, int hat, Color col)
        {
            switch (hat)
            {
                case 1: // cap
                case 2: // backwards cap
                {
                    if (hat == 2) mb.Push(new Vector3(0, 0, 0.002f), Quaternion.Euler(0, 180f, 0));
                    Dome(mb, new Vector3(0.099f, 0.125f, 0.114f), Brow + 0.006f, 0.02f);
                    mb.SetColor(Darker(col, 0.85f));
                    Disc(mb, new Vector3(0, Brow + 0.004f, 0.105f), 0.072f, 0.008f, new Vector3(1f, 1f, 0.78f), Quaternion.Euler(10f, 0, 0));
                    mb.SetColor(Darker(col, 0.7f));
                    mb.Sphere(new Vector3(0, Top + 0.008f, -0.004f), new Vector3(0.012f, 0.008f, 0.012f), 6, 8);
                    if (hat == 2) mb.Pop();
                    break;
                }
                case 3: // beanie
                    Dome(mb, new Vector3(0.102f, 0.135f, 0.117f), Brow + 0.01f, 0.02f, 0.006f);
                    mb.SetColor(Darker(col, 0.8f));
                    mb.Push(new Vector3(0, Brow + 0.018f, 0.0f), Quaternion.identity, new Vector3(1f, 1f, 1.14f));
                    mb.Torus(Vector3.zero, 0.098f, 0.014f, 24, 8);
                    mb.Pop();
                    mb.SetColor(White);
                    mb.Sphere(new Vector3(0, Top + 0.03f, -0.004f), new Vector3(0.03f, 0.028f, 0.03f), 8, 10);
                    break;
                case 4: // cowboy hat
                {
                    float by = Brow + 0.03f;
                    Disc(mb, new Vector3(0, by, -0.004f), 0.2f, 0.008f, new Vector3(1f, 1f, 0.86f), Quaternion.identity);
                    // upturned brim sides
                    for (int s = -1; s <= 1; s += 2)
                        Disc(mb, new Vector3(s * 0.17f, by + 0.022f, -0.004f), 0.07f, 0.008f, new Vector3(0.5f, 1f, 1.6f), Quaternion.Euler(0, 0, s * 38f));
                    mb.Cylinder(new Vector3(0, by, -0.004f), new Vector3(0, Top + 0.05f, -0.004f), 0.094f, 0.08f, 18, true);
                    mb.SetColor(Darker(col, 0.75f));
                    mb.Sphere(new Vector3(0, Top + 0.05f, -0.004f), new Vector3(0.08f, 0.02f, 0.07f), 6, 16);
                    mb.SetColor(Darker(col, 0.35f));
                    mb.Cylinder(new Vector3(0, by + 0.004f, -0.004f), new Vector3(0, by + 0.022f, -0.004f), 0.097f, 0.096f, 18, false);
                    break;
                }
                case 5: // top hat
                {
                    float by = Brow + 0.045f;
                    Disc(mb, new Vector3(0, by, -0.004f), 0.13f, 0.008f, new Vector3(1f, 1f, 0.9f), Quaternion.identity);
                    mb.Cylinder(new Vector3(0, by, -0.004f), new Vector3(0, by + 0.17f, -0.004f), 0.086f, 0.092f, 20, true);
                    mb.SetColor(new Color(0.75f, 0.1f, 0.12f));
                    mb.Cylinder(new Vector3(0, by + 0.006f, -0.004f), new Vector3(0, by + 0.03f, -0.004f), 0.089f, 0.089f, 20, false);
                    break;
                }
                case 6: // hard hat
                    Dome(mb, new Vector3(0.106f, 0.132f, 0.12f), Brow + 0.015f, 0f, 0.008f);
                    Disc(mb, new Vector3(0, Brow + 0.016f, 0.004f), 0.122f, 0.008f, new Vector3(1f, 1f, 1.12f), Quaternion.identity);
                    mb.SetColor(Darker(col, 0.85f));
                    mb.Push(new Vector3(0, Top + 0.012f, -0.004f), Quaternion.identity);
                    mb.Torus(Vector3.zero, 0.07f, 0.01f, 16, 6, Vector3.right);
                    mb.Pop();
                    break;
                case 7: // army helmet
                    Dome(mb, new Vector3(0.112f, 0.138f, 0.126f), Brow + 0.012f, 0.05f, 0.004f);
                    mb.SetColor(Darker(col, 0.7f));
                    mb.Push(new Vector3(0, Brow + 0.008f, -0.006f), Quaternion.Euler(-8f, 0, 0), new Vector3(1f, 1f, 1.1f));
                    mb.Torus(Vector3.zero, 0.108f, 0.008f, 24, 6);
                    mb.Pop();
                    break;
                case 8: // crown (gold, gems in the chosen colour)
                {
                    float y0 = Top - 0.035f, y1 = y0 + 0.045f;
                    mb.SetColor(Gold, 0.8f);
                    mb.Cylinder(new Vector3(0, y0, -0.004f), new Vector3(0, y1, -0.004f), 0.09f, 0.094f, 20, false);
                    mb.Cylinder(new Vector3(0, y1, -0.004f), new Vector3(0, y0, -0.004f), 0.086f, 0.082f, 20, false); // inner wall
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI * 2f / 6f;
                        var b = new Vector3(Mathf.Sin(a) * 0.088f, y1, Mathf.Cos(a) * 0.088f - 0.004f);
                        mb.SetColor(Gold, 0.8f);
                        mb.Cylinder(b, b + new Vector3(Mathf.Sin(a) * 0.006f, 0.04f, Mathf.Cos(a) * 0.006f), 0.02f, 0.002f, 6, true);
                        mb.SetColor(col, 0.9f);
                        mb.Sphere(new Vector3(Mathf.Sin(a) * 0.096f, (y0 + y1) * 0.5f, Mathf.Cos(a) * 0.096f - 0.004f), Vector3.one * 0.011f, 6, 8);
                    }
                    break;
                }
                case 9: // party hat
                {
                    var b = new Vector3(0.012f, Top - 0.03f, 0.0f);
                    var t = new Vector3(0.035f, Top + 0.14f, 0.01f);
                    mb.Cylinder(b, t, 0.07f, 0.004f, 16, true);
                    mb.SetColor(White);
                    for (int i = 1; i <= 2; i++)
                    {
                        float k = i / 3f;
                        var p = Vector3.Lerp(b, t, k);
                        mb.Push(p, Quaternion.FromToRotation(Vector3.up, t - b));
                        mb.Torus(Vector3.zero, Mathf.Lerp(0.07f, 0.004f, k) + 0.001f, 0.006f, 16, 5);
                        mb.Pop();
                    }
                    mb.Sphere(t, Vector3.one * 0.022f, 6, 8);
                    break;
                }
                case 10: // headband
                    mb.Push(new Vector3(0, Brow + 0.026f, 0.002f), Quaternion.Euler(-6f, 0, 0), new Vector3(1f, 1f, 1.13f));
                    mb.Torus(Vector3.zero, 0.098f, 0.013f, 24, 6);
                    mb.Pop();
                    for (int s = -1; s <= 1; s += 2)
                        mb.Cylinder(new Vector3(s * 0.01f, Brow + 0.03f, -0.112f), new Vector3(s * 0.045f, Brow - 0.05f, -0.13f), 0.012f, 0.008f, 6, true);
                    break;
                case 11: // bucket hat
                {
                    float by = Brow + 0.02f;
                    Dome(mb, new Vector3(0.102f, 0.13f, 0.116f), by, 0f, 0.008f);
                    mb.Cylinder(new Vector3(0, by + 0.005f, -0.004f), new Vector3(0, by - 0.03f, -0.004f), 0.1f, 0.155f, 20, true);
                    mb.SetColor(Darker(col, 0.7f));
                    mb.Cylinder(new Vector3(0, by + 0.006f, -0.004f), new Vector3(0, by + 0.02f, -0.004f), 0.103f, 0.102f, 20, false);
                    break;
                }
                case 12: // halo
                    mb.SetColor(Gold, 0.9f);
                    mb.Torus(new Vector3(0, Top + 0.08f, -0.004f), 0.085f, 0.011f, 28, 8);
                    break;
                case 13: // devil horns
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var a = new Vector3(s * 0.05f, Top - 0.03f, 0.03f);
                        var m = new Vector3(s * 0.07f, Top + 0.03f, 0.04f);
                        var e = new Vector3(s * 0.06f, Top + 0.08f, 0.01f);
                        mb.Cylinder(a, m, 0.022f, 0.013f, 10, true);
                        mb.Cylinder(m, e, 0.013f, 0.002f, 10, true);
                    }
                    break;
                case 14: // chef hat
                {
                    float by = Brow + 0.02f;
                    mb.SetColor(White, 0.1f);
                    mb.Cylinder(new Vector3(0, by, -0.004f), new Vector3(0, Top + 0.06f, -0.004f), 0.098f, 0.1f, 20, true);
                    mb.Sphere(new Vector3(0, Top + 0.09f, -0.004f), new Vector3(0.125f, 0.065f, 0.125f), 10, 20);
                    mb.SetColor(col);
                    mb.Cylinder(new Vector3(0, by, -0.004f), new Vector3(0, by + 0.018f, -0.004f), 0.1f, 0.1f, 20, false);
                    break;
                }
                case 15: // viking helmet
                    Dome(mb, new Vector3(0.108f, 0.134f, 0.122f), Brow + 0.01f, 0.03f, 0.005f);
                    mb.SetColor(Darker(col, 0.65f));
                    mb.Push(new Vector3(0, Brow + 0.012f, -0.004f), Quaternion.identity, new Vector3(1f, 1f, 1.12f));
                    mb.Torus(Vector3.zero, 0.106f, 0.01f, 24, 6);
                    mb.Pop();
                    mb.BevelBox(new Vector3(0, Brow - 0.005f, 0.118f), new Vector3(0.014f, 0.05f, 0.01f), 0.003f);
                    mb.SetColor(Horn, 0.4f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var a = new Vector3(s * 0.095f, CY + 0.07f, -0.004f);
                        var m = new Vector3(s * 0.155f, CY + 0.1f, 0.0f);
                        var e = new Vector3(s * 0.175f, CY + 0.17f, 0.01f);
                        mb.Cylinder(a, m, 0.026f, 0.017f, 10, true);
                        mb.Cylinder(m, e, 0.017f, 0.003f, 10, true);
                    }
                    break;
            }
        }
    }
}
