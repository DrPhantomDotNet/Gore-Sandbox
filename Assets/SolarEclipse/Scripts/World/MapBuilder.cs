using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Builds static level geometry from code: textured blocks with colliders and surface tags, ramps, stairs,
    /// lamps, trees, buildings and the invisible "Boundaries" walls that even noclip cannot pass.
    /// </summary>
    public sealed class MapBuilder
    {
        public readonly Transform Root;
        public readonly MapDef Def;
        public readonly MapOptions Options;
        public Vector3 PlayerSpawn = new Vector3(0, 0.1f, -8);
        public float PlayerYaw;
        public readonly List<Vector3> NpcSpawns = new List<Vector3>();
        public readonly List<Light> Lamps = new List<Light>();
        public Bounds Playable = new Bounds(Vector3.zero, new Vector3(120, 40, 120));
        public float KillY = -40f;

        readonly Transform staticRoot;

        public MapBuilder(Transform root, MapDef def, MapOptions options)
        {
            Root = root;
            Def = def;
            Options = options;
            staticRoot = new GameObject("Static").transform;
            staticRoot.SetParent(root, false);
        }

        public static Material Tex(Texture2D t) => Mats.LitTextured(t);

        // ------------------------------------------------------------------ primitives

        public GameObject Block(string name, Vector3 min, Vector3 max, Material mat, SurfaceType surface = SurfaceType.Concrete, Color? tint = null, float uvPerMeter = 0.5f, bool collide = true)
        {
            var size = max - min;
            return Box(name, (min + max) * 0.5f, Quaternion.identity, size, mat, surface, tint, uvPerMeter, collide);
        }

        public GameObject Box(string name, Vector3 center, Quaternion rot, Vector3 size, Material mat, SurfaceType surface = SurfaceType.Concrete, Color? tint = null, float uvPerMeter = 0.5f, bool collide = true, bool isStatic = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(isStatic ? staticRoot : Root, false);
            go.transform.SetPositionAndRotation(center, rot);
            var mb = new MeshBuilder { Color = tint ?? Color.white, Gloss = surface == SurfaceType.Metal ? 0.55f : surface == SurfaceType.Glass ? 0.9f : 0.15f };
            mb.Box(Vector3.zero, size, uvPerMeter);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (collide)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = size;
                go.AddComponent<Surface>().Type = surface;
            }
            return go;
        }

        /// <summary>Ramp whose top surface starts at <paramref name="bottomEdge"/> and climbs toward <paramref name="yawDeg"/>.</summary>
        public GameObject Ramp(string name, Vector3 bottomEdge, float yawDeg, float width, float rise, float angleDeg, Material mat, SurfaceType surface = SurfaceType.Wood, float thickness = 0.4f)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            float length = rise / Mathf.Sin(a);
            var rot = Quaternion.Euler(-angleDeg, yawDeg, 0);
            var slope = rot * Vector3.forward;
            var normal = rot * Vector3.up;
            var center = bottomEdge + slope * (length * 0.5f) - normal * (thickness * 0.5f);
            return Box(name, center, rot, new Vector3(width, thickness, length), mat, surface);
        }

        /// <summary>Solid stairs climbing along the yaw direction from <paramref name="start"/> (centre of the first step's front edge).</summary>
        public void Stairs(string name, Vector3 start, float yawDeg, float width, int steps, float rise, float run, Material mat, SurfaceType surface = SurfaceType.Concrete, Color? tint = null)
        {
            var rot = Quaternion.Euler(0, yawDeg, 0);
            for (int i = 0; i < steps; i++)
            {
                float h = (i + 1) * rise;
                var c = start + rot * new Vector3(0, h * 0.5f, (i + 0.5f) * run);
                var col = tint ?? Color.white;
                if (i % 2 == 1) col *= 0.93f;
                col.a = 1;
                Box(name + " " + (i + 1), c, rot, new Vector3(width, h, run), mat, surface, col);
            }
        }

        public GameObject Cylinder(string name, Vector3 baseCenter, float radius, float height, Material mat, SurfaceType surface, Color? tint = null, int segments = 16, bool collide = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(staticRoot, false);
            go.transform.position = baseCenter;
            var mb = new MeshBuilder { Color = tint ?? Color.white, Gloss = 0.2f };
            mb.Cylinder(Vector3.zero, Vector3.up * height, radius, radius, segments);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collide)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = radius; cc.height = Mathf.Max(height, radius * 2); cc.center = Vector3.up * height * 0.5f;
                go.AddComponent<Surface>().Type = surface;
            }
            return go;
        }

        /// <summary>Invisible walls on the playable bounds, on the dedicated Boundaries layer (noclip can't pass them).</summary>
        public void Boundaries(Bounds area, float height = 60f)
        {
            Playable = area;
            var parent = new GameObject("Boundaries");
            parent.transform.SetParent(Root, false);
            parent.layer = Layers.Boundaries;
            float t = 2f;
            var c = area.center;
            var e = area.extents;
            void Wall(Vector3 center, Vector3 size)
            {
                var w = new GameObject("Boundaries");
                w.layer = Layers.Boundaries;
                w.transform.SetParent(parent.transform, false);
                w.transform.position = center;
                w.AddComponent<BoxCollider>().size = size;
            }
            float y = area.min.y + height * 0.5f;
            Wall(new Vector3(c.x, y, area.max.z + t / 2), new Vector3(area.size.x + t * 2, height, t));
            Wall(new Vector3(c.x, y, area.min.z - t / 2), new Vector3(area.size.x + t * 2, height, t));
            Wall(new Vector3(area.max.x + t / 2, y, c.z), new Vector3(t, height, area.size.z + t * 2));
            Wall(new Vector3(area.min.x - t / 2, y, c.z), new Vector3(t, height, area.size.z + t * 2));
            Wall(new Vector3(c.x, area.min.y + height + t / 2, c.z), new Vector3(area.size.x + t * 2, t, area.size.z + t * 2));
            Wall(new Vector3(c.x, area.min.y - 6f, c.z), new Vector3(area.size.x + t * 2, t, area.size.z + t * 2));
            KillY = area.min.y - 3f;
        }

        // ------------------------------------------------------------------ props & decoration

        public void Tree(Vector3 basePos, float scale, bool pine = true)
        {
            var go = new GameObject("Tree");
            go.transform.SetParent(staticRoot, false);
            go.transform.position = basePos;
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var mb = new MeshBuilder();
            float trunkH = 2.2f * scale, trunkR = 0.2f * scale;
            mb.SetColor(new Color(0.42f, 0.29f, 0.19f), 0.1f);
            mb.Cylinder(Vector3.zero, Vector3.up * trunkH, trunkR * 1.2f, trunkR * 0.8f, 7);
            if (pine)
            {
                for (int i = 0; i < 3; i++)
                {
                    float w = (2.4f - i * 0.55f) * scale, h = 2.1f * scale, y = trunkH * 0.6f + i * 1.25f * scale;
                    mb.SetColor(i % 2 == 0 ? new Color(0.17f, 0.4f, 0.22f) : new Color(0.14f, 0.34f, 0.19f), 0.08f);
                    mb.Cylinder(Vector3.up * y, Vector3.up * (y + h), w, 0.02f, 8, true);
                }
            }
            else
            {
                mb.SetColor(new Color(0.24f, 0.48f, 0.2f), 0.08f);
                mb.Sphere(Vector3.up * (trunkH + 1.1f * scale), new Vector3(1.8f, 1.5f, 1.8f) * scale, 7, 9, p => p * (0.9f + 0.2f * Mathf.PerlinNoise(p.x * 3 + basePos.x, p.z * 3 + p.y)));
            }
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Tree");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = trunkR * 1.2f; cc.height = trunkH * 2f; cc.center = Vector3.up * trunkH;
            go.AddComponent<Surface>().Type = SurfaceType.Wood;
        }

        public void Rock(Vector3 basePos, float scale)
        {
            var go = new GameObject("Rock");
            go.transform.SetParent(staticRoot, false);
            go.transform.position = basePos;
            var mb = new MeshBuilder { Color = new Color(0.5f, 0.5f, 0.48f), Gloss = 0.1f };
            float seed = Random.value * 100f;
            mb.Sphere(Vector3.up * 0.3f * scale, new Vector3(1.2f, 0.8f, 1f) * scale, 6, 8, p => p * (0.8f + 0.4f * Mathf.PerlinNoise(p.x * 2 + seed, p.z * 2 + p.y * 2)));
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Rock");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.85f * scale; sc.center = Vector3.up * 0.3f * scale;
            go.AddComponent<Surface>().Type = SurfaceType.Concrete;
        }

        /// <summary>Street lamp with a real point light. The lamp head can be shot out.</summary>
        public Light StreetLamp(Vector3 basePos, float yawDeg, Color color, float height = 5f, float range = 16f, float intensity = 2.2f)
        {
            var rot = Quaternion.Euler(0, yawDeg, 0);
            var pole = Cylinder("Lamp Pole", basePos, 0.08f, height, Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.26f, 0.28f), 8);
            Box("Lamp Arm", basePos + Vector3.up * (height - 0.05f) + rot * Vector3.forward * 0.6f, rot, new Vector3(0.1f, 0.1f, 1.3f), Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.26f, 0.28f));
            var headPos = basePos + Vector3.up * (height - 0.2f) + rot * Vector3.forward * 1.15f;
            var head = new GameObject("Lamp Head");
            head.transform.SetParent(Root, false);
            head.transform.SetPositionAndRotation(headPos, rot);
            var mb = new MeshBuilder { Color = Color.white, Gloss = 0.8f };
            mb.Box(Vector3.zero, new Vector3(0.45f, 0.18f, 0.7f));
            head.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("LampHead");
            head.AddComponent<MeshRenderer>().sharedMaterial = Mats.Glow(color, 3f);
            head.AddComponent<BoxCollider>().size = new Vector3(0.45f, 0.18f, 0.7f);
            head.AddComponent<Surface>().Type = SurfaceType.Glass;
            var lightGo = new GameObject("Lamp Light");
            lightGo.transform.SetParent(head.transform, false);
            lightGo.transform.localPosition = Vector3.down * 0.4f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            Lamps.Add(light);
            var shootable = head.AddComponent<ShootableLight>();
            shootable.Init(light, head.GetComponent<MeshRenderer>());
            return light;
        }

        public Light CeilingLight(Vector3 pos, Color color, float range = 14f, float intensity = 1.6f, Vector3? size = null)
        {
            var head = new GameObject("Ceiling Light");
            head.transform.SetParent(Root, false);
            head.transform.position = pos;
            var s = size ?? new Vector3(1.2f, 0.08f, 0.35f);
            var mb = new MeshBuilder { Color = Color.white, Gloss = 0.5f };
            mb.Box(Vector3.zero, s);
            head.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("CeilingLight");
            head.AddComponent<MeshRenderer>().sharedMaterial = Mats.Glow(color, 2.5f);
            head.AddComponent<BoxCollider>().size = s;
            head.AddComponent<Surface>().Type = SurfaceType.Glass;
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(head.transform, false);
            lightGo.transform.localPosition = Vector3.down * 0.3f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point; light.color = color; light.range = range; light.intensity = intensity; light.shadows = LightShadows.None;
            Lamps.Add(light);
            head.AddComponent<ShootableLight>().Init(light, head.GetComponent<MeshRenderer>());
            return light;
        }

        /// <summary>A simple building shell with window strips; optional rooftop access.</summary>
        public void Building(Vector3 min, Vector3 max, Texture2D wallTex, Color tint, bool litWindows)
        {
            Block("Building", min, max, Tex(wallTex), SurfaceType.Concrete, tint, 0.25f);
            Block("Roof Edge", new Vector3(min.x - 0.2f, max.y, min.z - 0.2f), new Vector3(max.x + 0.2f, max.y + 0.4f, max.z + 0.2f), Tex(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.6f, 0.6f, 0.62f));
            // Window rows on the two long faces.
            var winMat = litWindows ? Mats.Glow(new Color(1f, 0.85f, 0.55f), 1.3f) : Mats.Lit;
            var darkWin = Mats.Lit;
            for (float y = min.y + 3f; y < max.y - 1.5f; y += 3.2f)
            {
                for (float x = min.x + 1.5f; x < max.x - 1.2f; x += 2.6f)
                {
                    bool lit = litWindows && Random.value < 0.55f;
                    var m = lit ? winMat : darkWin;
                    var tint2 = lit ? Color.white : new Color(0.12f, 0.15f, 0.2f);
                    Box("Window", new Vector3(x + 0.6f, y + 0.8f, min.z - 0.03f), Quaternion.identity, new Vector3(1.3f, 1.6f, 0.06f), m, SurfaceType.Glass, tint2, 1, false);
                    Box("Window", new Vector3(x + 0.6f, y + 0.8f, max.z + 0.03f), Quaternion.identity, new Vector3(1.3f, 1.6f, 0.06f), m, SurfaceType.Glass, tint2, 1, false);
                }
            }
        }

        public GameObject Fence(Vector3 a, Vector3 b, float height = 1.2f)
        {
            var dir = b - a;
            var rot = Quaternion.LookRotation(dir.normalized);
            var parent = Box("Fence Rail", (a + b) / 2 + Vector3.up * height * 0.85f, rot, new Vector3(0.06f, 0.08f, dir.magnitude), Mats.LitMetal, SurfaceType.Metal, new Color(0.35f, 0.35f, 0.37f));
            Box("Fence Rail", (a + b) / 2 + Vector3.up * height * 0.4f, rot, new Vector3(0.06f, 0.08f, dir.magnitude), Mats.LitMetal, SurfaceType.Metal, new Color(0.35f, 0.35f, 0.37f));
            int posts = Mathf.Max(2, Mathf.CeilToInt(dir.magnitude / 2.5f) + 1);
            for (int i = 0; i < posts; i++)
                Cylinder("Fence Post", Vector3.Lerp(a, b, i / (float)(posts - 1)), 0.05f, height, Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f), 6);
            return parent;
        }

        /// <summary>Static parked car made of boxes.</summary>
        public void Car(Vector3 pos, float yaw, Color paint)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var body = Box("Car", pos + rot * new Vector3(0, 0.65f, 0), rot, new Vector3(1.8f, 0.7f, 4.2f), Mats.LitMetal, SurfaceType.Metal, paint);
            Box("Car Cabin", pos + rot * new Vector3(0, 1.25f, -0.2f), rot, new Vector3(1.6f, 0.55f, 2.2f), Mats.Glass, SurfaceType.Glass, Color.white);
            Box("Car Roof", pos + rot * new Vector3(0, 1.55f, -0.2f), rot, new Vector3(1.62f, 0.06f, 2.0f), Mats.LitMetal, SurfaceType.Metal, paint);
            for (int i = 0; i < 4; i++)
            {
                var off = new Vector3(i % 2 == 0 ? -0.85f : 0.85f, 0.35f, i < 2 ? 1.35f : -1.35f);
                var w = new GameObject("Wheel");
                w.transform.SetParent(staticRoot, false);
                w.transform.SetPositionAndRotation(pos + rot * off, rot * Quaternion.Euler(0, 0, 90));
                var mb = new MeshBuilder { Color = new Color(0.08f, 0.08f, 0.08f), Gloss = 0.2f };
                mb.Cylinder(Vector3.down * 0.12f, Vector3.up * 0.12f, 0.36f, 0.36f, 14);
                mb.SetColor(new Color(0.6f, 0.6f, 0.62f), 0.7f);
                mb.Cylinder(Vector3.down * 0.13f, Vector3.up * 0.13f, 0.18f, 0.18f, 10);
                w.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Wheel");
                w.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            }
            // Headlights / taillights
            Box("Headlight", pos + rot * new Vector3(-0.6f, 0.75f, 2.11f), rot, new Vector3(0.35f, 0.15f, 0.04f), Mats.Glow(new Color(1f, 0.95f, 0.8f), 1.5f), SurfaceType.Glass, Color.white, 1, false);
            Box("Headlight", pos + rot * new Vector3(0.6f, 0.75f, 2.11f), rot, new Vector3(0.35f, 0.15f, 0.04f), Mats.Glow(new Color(1f, 0.95f, 0.8f), 1.5f), SurfaceType.Glass, Color.white, 1, false);
            Box("Taillight", pos + rot * new Vector3(-0.6f, 0.75f, -2.11f), rot, new Vector3(0.35f, 0.12f, 0.04f), Mats.Glow(new Color(1f, 0.1f, 0.1f), 1.5f), SurfaceType.Glass, Color.white, 1, false);
            Box("Taillight", pos + rot * new Vector3(0.6f, 0.75f, -2.11f), rot, new Vector3(0.35f, 0.12f, 0.04f), Mats.Glow(new Color(1f, 0.1f, 0.1f), 1.5f), SurfaceType.Glass, Color.white, 1, false);
        }

        /// <summary>Merges static meshes that share a material to cut draw calls.</summary>
        public void Finish()
        {
            StaticBatchingUtility.Combine(staticRoot.gameObject);
        }
    }
}
