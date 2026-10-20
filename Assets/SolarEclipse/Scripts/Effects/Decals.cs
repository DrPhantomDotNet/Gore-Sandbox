using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum DecalKind { Blood, Hole, Wound, Scorch }

    /// <summary>
    /// Capped, recycled decals: blood splats/pools/trails, bullet holes, skin wounds and scorch marks.
    /// Decals stick to moving objects by parenting. Pools grow over time; trails fade.
    /// </summary>
    public static class Decals
    {
        sealed class Decal
        {
            public GameObject Go;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public DecalKind Kind;
            public Color Color;
            public float Born, GrowTime, TargetSize, StartSize, FadeAfter;
            public bool Active;
        }

        static readonly List<Decal> blood = new List<Decal>();
        static readonly List<Decal> other = new List<Decal>();
        static readonly List<Decal> growing = new List<Decal>();
        static int bloodNext, otherNext;
        static Transform root;
        public static int ActiveCount { get; private set; }

        static Transform Root
        {
            get
            {
                if (root == null)
                {
                    root = new GameObject("Decals").transform;
                    root.gameObject.AddComponent<DecalUpdater>();
                }
                return root;
            }
        }

        public static void ClearAll()
        {
            foreach (var d in blood) if (d.Go != null) Object.Destroy(d.Go);
            foreach (var d in other) if (d.Go != null) Object.Destroy(d.Go);
            blood.Clear(); other.Clear(); growing.Clear();
            bloodNext = otherNext = 0;
            ActiveCount = 0;
        }

        static Decal Acquire(bool isBlood)
        {
            var list = isBlood ? blood : other;
            int cap = isBlood ? Mathf.Max(20, Settings.Data.maxDecals) : Mathf.Max(40, Settings.Data.maxDecals / 2);
            Decal d;
            if (list.Count < cap)
            {
                d = new Decal();
                list.Add(d);
            }
            else
            {
                ref int next = ref (isBlood ? ref bloodNext : ref otherNext);
                if (next >= list.Count) next = 0;
                d = list[next++];
            }
            if (d.Go == null)
            {
                d.Go = new GameObject("Decal");
                d.Go.layer = Layers.Effects;
                d.Mesh = new Mesh { name = "DecalQuad" };
                d.Mesh.MarkDynamic();
                d.Go.AddComponent<MeshFilter>().sharedMesh = d.Mesh;
                d.Renderer = d.Go.AddComponent<MeshRenderer>();
                d.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.Renderer.receiveShadows = false;
                ActiveCount++;
            }
            growing.Remove(d);
            d.Active = true;
            return d;
        }

        static void Place(Decal d, Vector3 pos, Vector3 normal, float size, float rotationDeg, Rect uv, Color color, Collider attachTo, float offset)
        {
            var go = d.Go;
            Transform parent = Root;
            if (attachTo != null && (attachTo.attachedRigidbody != null || attachTo.gameObject.layer == Layers.Ragdoll)) parent = attachTo.transform;
            go.transform.SetParent(parent, true);
            go.transform.position = pos + normal * offset;
            go.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, rotationDeg);
            var ls = parent.lossyScale;
            float s = size / Mathf.Max(0.0001f, Mathf.Abs((ls.x + ls.y + ls.z) / 3f));
            go.transform.localScale = new Vector3(s, s, s);
            d.Color = color;
            BuildQuad(d.Mesh, uv, color);
            go.SetActive(true);
        }

        static void BuildQuad(Mesh m, Rect uv, Color c)
        {
            m.Clear();
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) };
            var lc = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
            lc.a = c.a;
            m.colors = new[] { lc, lc, lc, lc };
            // Facing -Z (towards the surface normal after LookRotation(-normal)): clockwise as seen from the front.
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.RecalculateBounds();
        }

        static void SetColor(Decal d, Color c)
        {
            var lc = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
            lc.a = c.a;
            d.Mesh.colors = new[] { lc, lc, lc, lc };
        }

        /// <summary>Shrinks the size until all four corners sit on a surface (avoids decals hanging in the air).</summary>
        static float FitSize(Vector3 pos, Vector3 normal, float size)
        {
            var right = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
            var up = Vector3.Cross(right, normal);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float h = size * 0.4f;
                bool ok = true;
                for (int i = 0; i < 4 && ok; i++)
                {
                    var corner = pos + (i % 2 == 0 ? right : -right) * h + (i < 2 ? up : -up) * h + normal * 0.1f;
                    if (!Physics.Raycast(corner, -normal, 0.25f, Layers.DecalMask, QueryTriggerInteraction.Ignore)) ok = false;
                }
                if (ok) return size;
                size *= 0.6f;
            }
            return size;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Blood splat. kind 0 splat, 1 streak (walls), 2 pool, 3 droplets.</summary>
        public static void Blood(Vector3 pos, Vector3 normal, float size, int kind, Collider surface, float growTime = 0f, float fadeAfter = 0f)
        {
            if (!Settings.GoreEnabled) return;
            int cell = kind == 0 ? Random.Range(0, 6) : kind == 1 ? Random.Range(6, 10) : kind == 2 ? Random.Range(10, 13) : Random.Range(13, 16);
            var col = Settings.BloodColor;
            col = new Color(col.r * Random.Range(0.85f, 1.1f), col.g, col.b, Random.Range(0.85f, 1f));
            if (kind != 1 && surface != null && surface.attachedRigidbody == null) size = FitSize(pos, normal, size);
            float rot = kind == 1 ? 0f : Random.Range(0f, 360f);
            if (kind == 1)
            {
                // Streaks run downhill along the wall.
                var down = Vector3.ProjectOnPlane(Vector3.down, normal);
                if (down.sqrMagnitude > 0.01f)
                {
                    var look = Quaternion.LookRotation(-normal);
                    var localDown = Quaternion.Inverse(look) * down.normalized;
                    rot = Mathf.Atan2(localDown.x, -localDown.y) * Mathf.Rad2Deg;
                }
            }
            var d = Acquire(true);
            d.Kind = DecalKind.Blood;
            d.Born = Time.time;
            d.FadeAfter = fadeAfter;
            float start = growTime > 0 ? size * 0.15f : size;
            Place(d, pos, normal, start, rot, TextureFactory.AtlasCell(cell, TextureFactory.BloodCells), col, surface, 0.004f + Random.value * 0.003f);
            d.Renderer.sharedMaterial = Mats.DecalBlood;
            if (growTime > 0)
            {
                d.StartSize = start; d.TargetSize = size; d.GrowTime = growTime;
                growing.Add(d);
            }
            else if (fadeAfter > 0) growing.Add(d);
        }

        public static void BulletHole(Vector3 pos, Vector3 normal, SurfaceType surface, Collider col, float size = 0.12f)
        {
            int cell = surface == SurfaceType.Metal ? 1 : surface == SurfaceType.Wood ? 2 : surface == SurfaceType.Dirt || surface == SurfaceType.Grass ? 3 : 0;
            var d = Acquire(false);
            d.Kind = DecalKind.Hole;
            Place(d, pos, normal, size * Random.Range(0.85f, 1.2f), Random.Range(0, 360f), TextureFactory.AtlasCell(cell, TextureFactory.HoleCells), Color.white, col, 0.003f);
            d.Renderer.sharedMaterial = Mats.DecalHoles;
        }

        public static void Scorch(Vector3 pos, Vector3 normal, float size, Collider col)
        {
            var d = Acquire(false);
            d.Kind = DecalKind.Scorch;
            Place(d, pos, normal, size * 2.2f, Random.Range(0, 360f), TextureFactory.AtlasCell(3, TextureFactory.HoleCells), new Color(1, 1, 1, 0.9f), col, 0.006f);
            d.Renderer.sharedMaterial = Mats.DecalHoles;
        }

        /// <summary>Skin wound on a body part (parented to it). Cells: see TextureFactory.WoundAtlas.</summary>
        public static GameObject Wound(Vector3 pos, Vector3 normal, int cell, float size, Collider bodyPart, float rotation = -1f)
        {
            var d = Acquire(false);
            d.Kind = DecalKind.Wound;
            float rot = rotation >= 0 ? rotation : Random.Range(0, 360f);
            var tint = Color.Lerp(Color.white, Settings.BloodColor * 1.8f, Settings.Data.bloodColor == 0 ? 0f : 0.6f);
            tint.a = 1;
            Place(d, pos, normal, size, rot, TextureFactory.AtlasCell(cell, TextureFactory.WoundCells), tint, bodyPart, 0.002f);
            d.Renderer.sharedMaterial = Mats.DecalWounds;
            // Wound decals always follow the limb, even static colliders.
            if (bodyPart != null && d.Go.transform.parent != bodyPart.transform) d.Go.transform.SetParent(bodyPart.transform, true);
            return d.Go;
        }

        internal static void Tick()
        {
            float now = Time.time;
            for (int i = growing.Count - 1; i >= 0; i--)
            {
                var d = growing[i];
                if (d.Go == null) { growing.RemoveAt(i); continue; }
                if (d.GrowTime > 0)
                {
                    float t = Mathf.Clamp01((now - d.Born) / d.GrowTime);
                    float s = Mathf.Lerp(d.StartSize, d.TargetSize, 1f - (1f - t) * (1f - t));
                    var ls = d.Go.transform.parent != null ? d.Go.transform.parent.lossyScale : Vector3.one;
                    s /= Mathf.Max(0.0001f, (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f);
                    d.Go.transform.localScale = new Vector3(s, s, s);
                    if (t >= 1f) d.GrowTime = 0;
                }
                if (d.FadeAfter > 0 && now - d.Born > d.FadeAfter)
                {
                    float f = Mathf.Clamp01(1f - (now - d.Born - d.FadeAfter) / 20f);
                    var c = d.Color; c.a *= f;
                    if ((Time.frameCount + i) % 10 == 0) SetColor(d, c);
                    if (f <= 0f) { d.Go.SetActive(false); d.FadeAfter = 0; }
                }
                if (d.GrowTime <= 0 && d.FadeAfter <= 0) growing.RemoveAt(i);
            }
        }

        sealed class DecalUpdater : MonoBehaviour
        {
            void Update() => Tick();
        }
    }
}
