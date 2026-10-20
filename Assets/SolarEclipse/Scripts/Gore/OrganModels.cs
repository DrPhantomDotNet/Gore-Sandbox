using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Low-poly organs: hidden inside the torso (visible with X-ray) and spilled as physics gibs.</summary>
    public static class OrganModels
    {
        static readonly Color HeartC = new Color(0.55f, 0.08f, 0.1f), LungC = new Color(0.85f, 0.45f, 0.5f), LiverC = new Color(0.4f, 0.08f, 0.07f),
            StomachC = new Color(0.85f, 0.55f, 0.5f), GutC = new Color(0.88f, 0.6f, 0.55f), KidneyC = new Color(0.5f, 0.12f, 0.12f),
            BrainC = new Color(0.9f, 0.7f, 0.7f), BoneC = new Color(0.92f, 0.88f, 0.78f), MeatC = new Color(0.6f, 0.1f, 0.1f);

        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static Mesh Heart() => Get("heart", mb =>
        {
            mb.SetColor(HeartC, 0.7f);
            mb.Sphere(new Vector3(-0.012f, 0.01f, 0), new Vector3(0.03f, 0.034f, 0.03f), 8, 10);
            mb.Sphere(new Vector3(0.014f, 0.012f, 0), new Vector3(0.028f, 0.03f, 0.028f), 8, 10);
            mb.Cylinder(new Vector3(0, -0.005f, 0), new Vector3(0.006f, -0.055f, 0.004f), 0.034f, 0.006f, 10);
            mb.SetColor(new Color(0.75f, 0.6f, 0.5f), 0.6f);
            mb.Cylinder(new Vector3(-0.005f, 0.03f, 0), new Vector3(-0.008f, 0.07f, -0.01f), 0.012f, 0.011f, 8);
        });

        public static Mesh Lung() => Get("lung", mb =>
        {
            mb.SetColor(LungC, 0.5f);
            mb.Sphere(Vector3.zero, new Vector3(0.05f, 0.09f, 0.06f), 8, 10, p => p.y > 0 ? new Vector3(p.x * (1f - p.y * 4f), p.y, p.z * (1f - p.y * 3f)) : p);
        });

        public static Mesh Liver() => Get("liver", mb =>
        {
            mb.SetColor(LiverC, 0.75f);
            mb.Sphere(Vector3.zero, new Vector3(0.09f, 0.04f, 0.06f), 7, 12, p => new Vector3(p.x, p.y * (1f - p.x * 4f), p.z));
        });

        public static Mesh Stomach() => Get("stomach", mb =>
        {
            mb.SetColor(StomachC, 0.6f);
            mb.Loft(7, 10, i => (new Vector3(Mathf.Sin(i * 0.5f) * 0.03f, -i * 0.016f, 0), 0.02f + Mathf.Sin(i / 6f * Mathf.PI) * 0.03f, 0.02f + Mathf.Sin(i / 6f * Mathf.PI) * 0.025f), Vector3.down);
        });

        public static Mesh Kidney() => Get("kidney", mb =>
        {
            mb.SetColor(KidneyC, 0.7f);
            mb.Sphere(Vector3.zero, new Vector3(0.025f, 0.05f, 0.02f), 7, 10, p => p.x > 0.01f ? new Vector3(p.x * 0.4f, p.y, p.z) : p);
        });

        public static Mesh Brain() => Get("brain", mb =>
        {
            mb.SetColor(BrainC, 0.6f);
            mb.Sphere(Vector3.zero, new Vector3(0.065f, 0.05f, 0.08f), 12, 16, p => p * (1f + 0.06f * Mathf.Sin(p.x * 180f) * Mathf.Sin(p.z * 150f + p.y * 90f)));
        });

        public static Mesh GutSegment() => Get("gut", mb =>
        {
            mb.SetColor(GutC, 0.65f);
            mb.Capsule(new Vector3(0, -0.035f, 0), new Vector3(0, 0.035f, 0), 0.016f, 0.016f, 8, 2);
        });

        public static Mesh Meat(int seed) => Get("meat" + seed, mb =>
        {
            var r = new System.Random(seed);
            mb.SetColor(MeatC, 0.6f);
            float s = 0.03f + (float)r.NextDouble() * 0.04f;
            mb.Sphere(Vector3.zero, new Vector3(s, s * 0.7f, s * 0.9f), 5, 7, p => p * (0.7f + 0.6f * Mathf.PerlinNoise(p.x * 40f + seed, p.y * 40f + p.z * 30f)));
            if (seed % 2 == 0) { mb.SetColor(new Color(0.95f, 0.85f, 0.5f), 0.3f); mb.Sphere(new Vector3(s * 0.5f, 0, 0), Vector3.one * s * 0.45f, 4, 6); }
        });

        public static Mesh BoneShard(int seed) => Get("bone" + seed, mb =>
        {
            var r = new System.Random(seed);
            mb.SetColor(BoneC, 0.4f);
            float len = 0.04f + (float)r.NextDouble() * 0.08f;
            mb.Cylinder(Vector3.zero, new Vector3(0, len, 0), 0.012f, 0.006f + (float)r.NextDouble() * 0.006f, 6);
            mb.SetColor(new Color(0.6f, 0.1f, 0.1f), 0.6f);
            mb.Sphere(Vector3.zero, Vector3.one * 0.013f, 4, 6);
        });

        static Mesh Get(string key, System.Action<MeshBuilder> build)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            var mb = new MeshBuilder();
            build(mb);
            m = mb.ToMesh(key);
            cache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ inside the body (X-ray / opened)

        public static GameObject BuildInside(BodyPart part)
        {
            var mb = new MeshBuilder();
            switch (part.Type)
            {
                case PartType.Chest:
                    Place(mb, Heart(), new Vector3(-0.03f, 0.12f, 0.035f), Quaternion.Euler(0, 0, 15));
                    Place(mb, Lung(), new Vector3(-0.08f, 0.15f, 0f), Quaternion.identity);
                    Place(mb, Lung(), new Vector3(0.08f, 0.15f, 0f), Quaternion.identity);
                    Spine(mb, 0.02f, 0.28f, -0.09f);
                    mb.SetColor(BoneC, 0.4f);
                    for (int i = 0; i < 6; i++) mb.Torus(new Vector3(0, 0.05f + i * 0.04f, 0.0f), 0.13f - Mathf.Abs(i - 3) * 0.008f, 0.006f, 16, 4);
                    break;
                case PartType.Abdomen:
                    Place(mb, Liver(), new Vector3(0.06f, 0.12f, 0.02f), Quaternion.identity);
                    Place(mb, Stomach(), new Vector3(-0.05f, 0.14f, 0.045f), Quaternion.identity);
                    Place(mb, Kidney(), new Vector3(-0.065f, 0.07f, -0.065f), Quaternion.identity);
                    Place(mb, Kidney(), new Vector3(0.065f, 0.07f, -0.065f), Quaternion.Euler(0, 180, 0));
                    mb.SetColor(GutC, 0.65f);
                    for (int i = 0; i < 3; i++) mb.Torus(new Vector3(0, 0.02f + i * 0.025f, 0.04f), 0.05f - i * 0.008f, 0.014f, 14, 6);
                    Spine(mb, 0f, 0.17f, -0.085f);
                    break;
                case PartType.Pelvis:
                    mb.SetColor(GutC, 0.65f);
                    mb.Torus(new Vector3(0, 0.05f, 0.03f), 0.06f, 0.018f, 14, 6);
                    mb.SetColor(BoneC, 0.4f);
                    mb.Torus(new Vector3(0, 0f, 0), 0.11f, 0.02f, 16, 6);
                    break;
                case PartType.Head:
                    Place(mb, Brain(), new Vector3(0, 0.15f, -0.005f), Quaternion.identity);
                    break;
                case PartType.Neck:
                    Spine(mb, -0.01f, 0.1f, -0.025f);
                    mb.SetColor(new Color(0.85f, 0.7f, 0.7f), 0.5f);
                    mb.Cylinder(new Vector3(0, -0.01f, 0.03f), new Vector3(0, 0.1f, 0.03f), 0.012f, 0.012f, 8);
                    break;
                default:
                    if (!part.IsLimb) return null;
                    mb.SetColor(BoneC, 0.4f);
                    float len = part.Spec.Length;
                    var dir = Vector3.down;
                    mb.Capsule(Vector3.zero + dir * 0.02f, dir * (len - 0.02f), 0.015f, 0.012f, 8, 2);
                    break;
            }
            var go = new GameObject("Organs");
            go.layer = Layers.Ragdoll;
            go.transform.SetParent(part.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Organs_" + part.Type);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Xray;
            mr.enabled = Rules.Xray;
            go.AddComponent<XrayToggle>();
            return go;
        }

        static void Spine(MeshBuilder mb, float y0, float y1, float z)
        {
            mb.SetColor(BoneC, 0.4f);
            for (float y = y0; y < y1; y += 0.03f) mb.BevelBox(new Vector3(0, y + 0.012f, z), new Vector3(0.035f, 0.022f, 0.03f), 0.005f);
        }

        static void Place(MeshBuilder mb, Mesh m, Vector3 pos, Quaternion rot)
        {
            var src = new MeshBuilder();
            src.V.AddRange(m.vertices);
            src.C.AddRange(m.colors);
            src.UV.AddRange(m.uv.Length == m.vertexCount ? m.uv : new Vector2[m.vertexCount]);
            src.T.AddRange(m.triangles);
            mb.Push(pos, rot);
            mb.Append(src);
            mb.Pop();
        }

        // ------------------------------------------------------------------ spilled gibs

        /// <summary>Organs and meat that fly out when a torso part or head is destroyed.</summary>
        public static void Spill(BodyPart part, Vector3 dir, Vector3 inheritVelocity)
        {
            var t = part.transform;
            void Spawn(string name, Mesh mesh, Vector3 local, float mass, float radius)
            {
                var go = new GameObject(name);
                go.layer = Layers.Debris;
                go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
                go.transform.SetPositionAndRotation(t.TransformPoint(local), Random.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = radius;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                RB.SetDamping(rb, 0.2f, 0.6f);
                RB.SetVel(rb, inheritVelocity + (Random.onUnitSphere + dir * 0.6f) * Random.Range(2f, 6f));
                rb.angularVelocity = Random.insideUnitSphere * 10f;
                go.AddComponent<Gib>();
                go.AddComponent<Surface>().Type = SurfaceType.Flesh;
                var s = go.AddComponent<Spawned>(); s.DisplayName = name;
                Debris.Register(go, 60f, Debris.GroupGib);
            }

            bool organs = Settings.Organs;
            switch (part.Type)
            {
                case PartType.Chest:
                    if (organs) { Spawn("Heart", Heart(), new Vector3(-0.03f, 0.12f, 0.03f), 0.3f, 0.035f); Spawn("Lung", Lung(), new Vector3(-0.08f, 0.15f, 0), 0.5f, 0.05f); Spawn("Lung", Lung(), new Vector3(0.08f, 0.15f, 0), 0.5f, 0.05f); }
                    break;
                case PartType.Abdomen:
                    if (organs) { Spawn("Liver", Liver(), new Vector3(0.06f, 0.1f, 0.02f), 1.5f, 0.05f); Spawn("Stomach", Stomach(), new Vector3(-0.05f, 0.1f, 0.04f), 0.4f, 0.04f); Spawn("Kidney", Kidney(), new Vector3(0.06f, 0.07f, -0.06f), 0.15f, 0.025f); Intestines(part, dir, inheritVelocity); }
                    break;
                case PartType.Pelvis:
                    if (organs) Intestines(part, dir, inheritVelocity);
                    break;
                case PartType.Head:
                    if (organs) Spawn("Brain", Brain(), new Vector3(0, 0.15f, 0), 1.3f, 0.06f);
                    break;
            }
            int meat = part.IsLimb ? 4 : 7;
            for (int i = 0; i < meat; i++) Spawn("Flesh", Meat(Random.Range(0, 6)), Random.insideUnitSphere * 0.05f + part.Spec.ColliderCenter, 0.2f, 0.03f);
            for (int i = 0; i < (part.IsLimb ? 2 : 4); i++) Spawn("Bone fragment", BoneShard(Random.Range(0, 4)), Random.insideUnitSphere * 0.05f + part.Spec.ColliderCenter, 0.08f, 0.015f);
        }

        /// <summary>A dangling jointed chain of intestine segments.</summary>
        public static void Intestines(BodyPart part, Vector3 dir, Vector3 inheritVelocity)
        {
            Rigidbody prev = null;
            var start = part.transform.TransformPoint(new Vector3(0, 0.05f, 0.08f));
            for (int i = 0; i < 9; i++)
            {
                var go = new GameObject("Intestine");
                go.layer = Layers.Debris;
                go.transform.SetParent(WorldRoot.Current != null ? WorldRoot.Current.DynamicRoot : null, false);
                go.transform.SetPositionAndRotation(start + dir * (i * 0.06f), Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0));
                go.AddComponent<MeshFilter>().sharedMesh = GutSegment();
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = 0.016f; cc.height = 0.1f; cc.direction = 1;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.08f;
                RB.SetDamping(rb, 0.4f, 1f);
                RB.SetVel(rb, inheritVelocity + dir * Random.Range(0.5f, 2f));
                if (prev == null && part.Body != null && !part.Gibbed) prev = part.Body;
                if (prev != null)
                {
                    var j = go.AddComponent<CharacterJoint>();
                    j.connectedBody = prev;
                    j.anchor = new Vector3(0, -0.035f, 0);
                    j.swing1Limit = new SoftJointLimit { limit = 60 };
                    j.swing2Limit = new SoftJointLimit { limit = 60 };
                    j.breakForce = 400f;
                }
                go.AddComponent<Gib>();
                go.AddComponent<Spawned>().DisplayName = "Intestine";
                Debris.Register(go, 70f, Debris.GroupGib);
                prev = rb;
            }
        }
    }

    /// <summary>Bloody chunk: drips for a few seconds and splats when it lands.</summary>
    public sealed class Gib : MonoBehaviour
    {
        float born, lastSplat;
        void Awake() { born = Time.time; }

        void Update()
        {
            if (Time.time - born < 4f && Random.value < 0.08f) BloodFX.Drip(transform.position, Vector3.zero);
        }

        void OnCollisionEnter(Collision c)
        {
            if (Time.time - lastSplat < 0.3f || c.relativeVelocity.magnitude < 2f) return;
            lastSplat = Time.time;
            var cp = c.GetContact(0);
            AudioManager.Play("splat", cp.point, Mathf.Clamp01(c.relativeVelocity.magnitude / 8f), Random.Range(0.9f, 1.2f), 1f, 1f, 20f);
            if (c.collider.gameObject.layer != Layers.Ragdoll) Decals.Blood(cp.point, cp.normal, Random.Range(0.15f, 0.4f), 0, c.collider);
        }
    }

    /// <summary>Shows organs when the X-ray cheat is on.</summary>
    public sealed class XrayToggle : MonoBehaviour
    {
        MeshRenderer mr;
        void Awake() => mr = GetComponent<MeshRenderer>();
        void Update() { if (mr != null && mr.enabled != Rules.Xray) mr.enabled = Rules.Xray; }
    }
}
