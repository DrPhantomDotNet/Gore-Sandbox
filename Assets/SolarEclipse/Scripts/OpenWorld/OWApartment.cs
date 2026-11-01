using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Your penthouse furniture: built from what you own (each player sees their own things).</summary>
    public static class OWApartment
    {
        public static Vector3[] Slots;
        static GameObject root;
        static string builtKey;

        public static void Clear() { if (root != null) Object.Destroy(root); root = null; builtKey = null; }
        public static void Refresh() { builtKey = null; Tick(); }

        public static void Tick()
        {
            if (Slots == null || WorldRoot.Current == null) return;
            var d = OWSave.D;
            string key = string.Join(",", d.owned) + "|" + d.heists;
            if (key == builtKey && root != null) return;
            builtKey = key;
            if (root != null) Object.Destroy(root);
            root = new GameObject("Penthouse Furniture");
            root.transform.SetParent(WorldRoot.Current.transform, false);
            foreach (var it in OWItems.In(OWCat.Apartment))
                if (it.Variant > 0 && it.Variant < Slots.Length && OWSave.Owns(it.Id))
                    try { Build(it, Slots[it.Variant]); } catch (System.Exception e) { Debug.LogWarning("[OpenWorld] furniture " + it.Id + ": " + e.Message); }
        }

        static GameObject Piece(string name, Vector3 pos, Color col, System.Action<MeshBuilder> shape, bool solid, Material mat = null)
        {
            var mb = new MeshBuilder { Color = col };
            shape(mb);
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
            var mesh = mb.ToMesh(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat != null ? mat : Mats.Lit;
            if (solid) { var bc = go.AddComponent<BoxCollider>(); bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
            return go;
        }

        static void Build(OWItem it, Vector3 p)
        {
            switch (it.Variant)
            {
                case 1: // gun wall: every gun you own
                {
                    Piece("Gun Wall", p, new Color(0.18f, 0.18f, 0.2f), m => m.Box(Vector3.zero, new Vector3(0.12f, 2.2f, 5.2f)), false);
                    var guns = OWItems.In(OWCat.Gun).FindAll(g => OWSave.Owns(g.Id));
                    for (int i = 0; i < guns.Count && i < 12; i++)
                    {
                        var wm = WeaponModels.Build(guns[i].Weapon, 0);
                        if (wm == null) continue;
                        wm.transform.SetParent(root.transform, false);
                        wm.transform.position = p + new Vector3(0.12f, 0.8f - (i / 3) * 0.5f, -1.8f + (i % 3) * 1.8f);
                        wm.transform.rotation = Quaternion.Euler(0, 0, 0);
                        foreach (var b in wm.GetComponentsInChildren<Behaviour>()) b.enabled = false;
                    }
                    break;
                }
                case 2: // trophy case: one cup per heist (up to 10)
                {
                    Piece("Trophy Case", p, new Color(0.3f, 0.2f, 0.12f), m => { m.Box(new Vector3(0, 0.45f, 0), new Vector3(3f, 0.9f, 0.8f)); m.Box(new Vector3(0, 2.3f, 0), new Vector3(3f, 0.1f, 0.8f)); }, true);
                    Piece("Trophy Glass", p, Color.white, m => m.Box(new Vector3(0, 1.6f, 0), new Vector3(3f, 1.3f, 0.8f)), false, Mats.Glass);
                    int cups = Mathf.Min(10, OWSave.D.heists);
                    for (int i = 0; i < cups; i++)
                        Piece("Trophy", p + new Vector3(-1.2f + (i % 5) * 0.6f, 0.95f + (i / 5) * 0.65f, 0), new Color(1f, 0.8f, 0.2f), m => { m.Cylinder(Vector3.zero, new Vector3(0, 0.18f, 0), 0.08f, 0.05f, 10); m.Cylinder(new Vector3(0, 0.18f, 0), new Vector3(0, 0.36f, 0), 0.06f, 0.13f, 10); }, false, Mats.Glow(new Color(1f, 0.78f, 0.2f), 0.4f));
                    break;
                }
                case 3: // neon sign
                    Piece("Neon Sign", p, Color.white, m => { m.Box(new Vector3(0, 0, 0), new Vector3(3.6f, 0.14f, 0.06f)); m.Box(new Vector3(0, -0.8f, 0), new Vector3(3.6f, 0.14f, 0.06f)); m.Box(new Vector3(-1.8f, -0.4f, 0), new Vector3(0.14f, 0.94f, 0.06f)); m.Box(new Vector3(0.4f, -0.4f, 0), new Vector3(0.14f, 0.94f, 0.06f)); }, false, Mats.Glow(it.Color, 3f));
                    break;
                case 4: // pool table
                    Piece("Pool Table", p, new Color(0.35f, 0.2f, 0.1f), m => { m.Box(new Vector3(0, 0.8f, 0), new Vector3(2.6f, 0.16f, 1.5f)); for (int k = 0; k < 4; k++) m.Box(new Vector3(k % 2 == 0 ? -1.1f : 1.1f, 0.38f, k < 2 ? -0.6f : 0.6f), new Vector3(0.16f, 0.76f, 0.16f)); }, true);
                    Piece("Felt", p, new Color(0.05f, 0.4f, 0.15f), m => m.Box(new Vector3(0, 0.89f, 0), new Vector3(2.4f, 0.03f, 1.3f)), false);
                    break;
                case 5: // TV
                    Piece("TV", p, new Color(0.05f, 0.05f, 0.06f), m => m.Box(Vector3.zero, new Vector3(0.1f, 1.4f, 2.4f)), false);
                    Piece("TV Screen", p + new Vector3(-0.06f, 0, 0), Color.white, m => m.Box(Vector3.zero, new Vector3(0.02f, 1.25f, 2.25f)), false, Mats.Glow(new Color(0.3f, 0.55f, 1f), 1.2f));
                    break;
                case 6: // statue
                    Piece("Statue", p, Color.white, m =>
                    {
                        m.Box(new Vector3(0, 0.4f, 0), new Vector3(1.2f, 0.8f, 1.2f));
                        m.Capsule(new Vector3(-0.15f, 0.9f, 0), new Vector3(-0.15f, 1.7f, 0), 0.12f, 0.12f);
                        m.Capsule(new Vector3(0.15f, 0.9f, 0), new Vector3(0.15f, 1.7f, 0), 0.12f, 0.12f);
                        m.Capsule(new Vector3(0, 1.8f, 0), new Vector3(0, 2.4f, 0), 0.3f, 0.26f);
                        m.Capsule(new Vector3(-0.32f, 2.35f, 0), new Vector3(-0.6f, 3f, 0), 0.08f, 0.07f);
                        m.Capsule(new Vector3(0.32f, 2.35f, 0), new Vector3(0.6f, 3f, 0), 0.08f, 0.07f);
                        m.Sphere(new Vector3(0, 2.75f, 0), new Vector3(0.18f, 0.22f, 0.18f));
                    }, true, Mats.Glow(new Color(1f, 0.78f, 0.2f), 0.5f));
                    break;
                case 7: // aquarium
                    Piece("Aquarium Base", p, new Color(0.15f, 0.15f, 0.18f), m => m.Box(new Vector3(0, 0.4f, 0), new Vector3(3.2f, 0.8f, 1f)), true);
                    Piece("Aquarium Water", p, Color.white, m => m.Box(new Vector3(0, 1.45f, 0), new Vector3(3.1f, 1.3f, 0.9f)), true, Mats.Glow(new Color(0.1f, 0.5f, 0.9f), 0.8f));
                    Piece("Shark", p + new Vector3(0, 1.5f, 0), new Color(0.5f, 0.55f, 0.6f), m => { m.Sphere(Vector3.zero, new Vector3(0.6f, 0.15f, 0.16f)); m.Box(new Vector3(0, 0.18f, 0), new Vector3(0.12f, 0.2f, 0.04f)); }, false);
                    break;
                case 8: // sofa
                    Piece("Sofa", p, it.Color, m => { m.Box(new Vector3(0, 0.25f, 0), new Vector3(3f, 0.5f, 1f)); m.Box(new Vector3(0, 0.7f, 0.4f), new Vector3(3f, 0.6f, 0.25f)); m.Box(new Vector3(-1.4f, 0.55f, 0), new Vector3(0.25f, 0.4f, 1f)); m.Box(new Vector3(1.4f, 0.55f, 0), new Vector3(0.25f, 0.4f, 1f)); }, true);
                    break;
                case 10: // home cinema
                    Piece("Screen", p, Color.white, m => m.Box(Vector3.zero, new Vector3(4.2f, 2.4f, 0.05f)), false, Mats.Glow(new Color(0.55f, 0.65f, 0.9f), 1f));
                    Piece("Seats", p + new Vector3(0, -2f, -3.5f), new Color(0.45f, 0.08f, 0.1f), m => { for (int k = 0; k < 4; k++) { m.Box(new Vector3(-1.5f + k, 0.25f, 0), new Vector3(0.8f, 0.5f, 0.8f)); m.Box(new Vector3(-1.5f + k, 0.7f, -0.35f), new Vector3(0.8f, 0.6f, 0.1f)); } }, true);
                    break;
                case 11: // home bar
                    Piece("Bar", p, new Color(0.35f, 0.22f, 0.13f), m => { m.Box(new Vector3(0, 0.55f, 0), new Vector3(3f, 1.1f, 0.6f)); m.SetColor(new Color(0.2f, 0.5f, 0.3f), 0.9f); for (int k = 0; k < 8; k++) m.Cylinder(new Vector3(-1.3f + k * 0.37f, 1.1f, 0), new Vector3(-1.3f + k * 0.37f, 1.45f, 0), 0.04f, 0.03f, 6); }, true);
                    Piece("Bar Neon", p + new Vector3(0, 2.1f, 0.25f), Color.white, m => m.Box(Vector3.zero, new Vector3(2.4f, 0.12f, 0.05f)), false, Mats.Glow(new Color(0.2f, 0.8f, 1f), 3f));
                    break;
                case 12: // gold bathtub
                    Piece("Gold Tub", p, Color.white, m => { m.Capsule(new Vector3(-0.7f, 0.4f, 0), new Vector3(0.7f, 0.4f, 0), 0.45f, 0.45f, 12, 4); }, true, Mats.Glow(new Color(1f, 0.78f, 0.2f), 0.5f));
                    break;
                case 13: // DJ booth
                    Piece("DJ Booth", p, new Color(0.12f, 0.12f, 0.15f), m => { m.Box(new Vector3(0, 0.55f, 0), new Vector3(2f, 1.1f, 0.8f)); m.SetColor(new Color(0.3f, 0.3f, 0.35f), 0.8f); m.Cylinder(new Vector3(-0.5f, 1.1f, 0), new Vector3(-0.5f, 1.14f, 0), 0.25f, 0.25f, 16); m.Cylinder(new Vector3(0.5f, 1.1f, 0), new Vector3(0.5f, 1.14f, 0), 0.25f, 0.25f, 16); m.SetColor(new Color(0.08f, 0.08f, 0.1f)); m.Box(new Vector3(-1.4f, 0.8f, 0), new Vector3(0.6f, 1.6f, 0.6f)); m.Box(new Vector3(1.4f, 0.8f, 0), new Vector3(0.6f, 1.6f, 0.6f)); }, true);
                    Piece("DJ Lights", p + new Vector3(0, 2.6f, 0), Color.white, m => { for (int k = 0; k < 5; k++) m.Sphere(new Vector3(-1f + k * 0.5f, 0, 0), Vector3.one * 0.1f, 6, 8); }, false, Mats.Glow(new Color(0.8f, 0.3f, 1f), 3f));
                    break;
                case 14: // gold safe
                    Piece("Safe", p, new Color(0.25f, 0.25f, 0.28f), m => { m.BevelBox(new Vector3(0, 0.9f, 0), new Vector3(1.6f, 1.8f, 1.2f), 0.05f); m.SetColor(new Color(0.7f, 0.7f, 0.72f), 0.9f); m.Cylinder(new Vector3(0.3f, 1f, 0.6f), new Vector3(0.3f, 1f, 0.66f), 0.15f, 0.15f, 12); }, true);
                    Piece("Safe Gold", p + new Vector3(0, 0, -1.1f), Color.white, m => { for (int k = 0; k < 9; k++) m.Box(new Vector3(-0.6f + (k % 3) * 0.6f, 0.12f + (k / 3) * 0.2f, 0), new Vector3(0.5f, 0.18f, 0.25f)); }, false, Mats.Glow(new Color(1f, 0.78f, 0.2f), 0.6f));
                    break;
                case 9: // pile of cash
                    Piece("Cash Pile", p, new Color(0.25f, 0.6f, 0.3f), m =>
                    {
                        for (int y = 0; y < 5; y++)
                            for (int k = 0; k < 6 - y; k++)
                                m.Box(new Vector3((k - (5 - y) / 2f) * 0.32f, 0.06f + y * 0.13f, (k % 2) * 0.2f), new Vector3(0.3f, 0.12f, 0.16f));
                    }, true);
                    break;
            }
        }
    }

    /// <summary>Emote poses for other players' avatars (RemotePlayer).</summary>
    public static class OWEmotes
    {
        public static void Pose(Dictionary<PartType, Transform> parts, int v, float t)
        {
            float s = Mathf.Sin(t * 8f), s2 = Mathf.Sin(t * 4f);
            void R(PartType p, float x, float y, float z) { if (parts.TryGetValue(p, out var tr) && tr != null) tr.localRotation = Quaternion.Euler(x, y, z); }
            switch (v)
            {
                case 0: // wave
                    R(PartType.UpperArmR, -165f, 0, -15f + s * 15f); R(PartType.ForearmR, -25f + s * 20f, 0, 0); break;
                case 1: // dance
                    R(PartType.UpperArmR, -90f + s * 50f, 0, -20f); R(PartType.UpperArmL, -90f - s * 50f, 0, 20f);
                    R(PartType.ForearmR, -60f, 0, 0); R(PartType.ForearmL, -60f, 0, 0);
                    R(PartType.Chest, 0, s2 * 20f, s * 8f); R(PartType.Abdomen, 0, -s2 * 10f, 0);
                    R(PartType.ThighL, -20f * Mathf.Max(0, s), 0, 0); R(PartType.ThighR, -20f * Mathf.Max(0, -s), 0, 0);
                    break;
                case 2: // salute
                    R(PartType.UpperArmR, -100f, 0, -45f); R(PartType.ForearmR, -125f, 0, 0); R(PartType.Chest, -5f, 0, 0); break;
                case 3: // flex
                    R(PartType.UpperArmR, -10f, 0, -85f); R(PartType.UpperArmL, -10f, 0, 85f);
                    R(PartType.ForearmR, -120f - s * 8f, 0, 0); R(PartType.ForearmL, -120f - s * 8f, 0, 0); R(PartType.Chest, -8f, 0, 0); break;
                case 4: // clap
                {
                    float c = Mathf.Abs(Mathf.Sin(t * 9f));
                    R(PartType.UpperArmR, -70f, 0, 18f * c); R(PartType.UpperArmL, -70f, 0, -18f * c);
                    R(PartType.ForearmR, -40f, 0, 0); R(PartType.ForearmL, -40f, 0, 0); break;
                }
                case 5: // point
                    R(PartType.UpperArmR, -88f, 0, -4f); R(PartType.ForearmR, -4f, 0, 0); break;
                case 6: // taunt
                    R(PartType.UpperArmR, -170f + s * 8f, 0, -20f); R(PartType.UpperArmL, -170f - s * 8f, 0, 20f); R(PartType.Head, -20f, 0, 0); break;
                case 8: R(PartType.Chest, 45f, 0, 0); R(PartType.Head, 20f, 0, 0); R(PartType.UpperArmR, -20f, 0, 30f); R(PartType.ForearmR, -90f, 0, 0); R(PartType.UpperArmL, 10f, 0, -10f); break; // bow
                case 9: R(PartType.UpperArmR, -170f, 0, 25f + s * 10f); R(PartType.UpperArmL, -170f, 0, -25f - s * 10f); R(PartType.ForearmR, -20f, 0, 0); R(PartType.ForearmL, -20f, 0, 0); R(PartType.Head, -15f, 0, 0); break; // cheer
                case 10: R(PartType.Head, 25f, 0, 0); R(PartType.UpperArmR, -70f, 0, -25f); R(PartType.ForearmR, -130f, 0, 0); R(PartType.HandR, -40f, 0, 0); break; // facepalm
                case 11: R(PartType.UpperArmR, -90f, 0, 0); R(PartType.ForearmR, s > 0 ? -90f : 0f, 0, 0); R(PartType.UpperArmL, -90f, 0, 0); R(PartType.ForearmL, s > 0 ? 0f : -90f, 0, 0); R(PartType.Head, 0, s > 0 ? 25f : -25f, 0); break; // robot
                case 7: // money rain
                    R(PartType.UpperArmR, -150f + s * 25f, 0, -20f); R(PartType.ForearmR, -30f + s * 30f, 0, 0); R(PartType.UpperArmL, -60f, 0, 10f); break;
            }
        }
    }
}
