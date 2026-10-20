using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Turns <see cref="CustomMapObj"/> entries into GameObjects. The same code builds the editor preview and the
    /// playable map, so what you place is exactly what you play. Objects are built in list order, which keeps
    /// multiplayer entity ids (assigned in hierarchy order) identical on every machine.
    /// </summary>
    public static class CustomMapKit
    {
        public static readonly string[] MatNames = { "Concrete", "Grass", "Wood", "Metal", "Tiles", "Sand", "Brick", "Asphalt", "Plaster", "Dirt", "Carpet", "Glass", "Glow" };
        public const int MatGlass = 11, MatGlow = 12;

        public static readonly Color[] Swatches =
        {
            Color.white, new Color(0.75f, 0.75f, 0.75f), new Color(0.45f, 0.45f, 0.47f), new Color(0.15f, 0.15f, 0.17f),
            new Color(0.85f, 0.2f, 0.18f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.88f, 0.3f), new Color(0.35f, 0.75f, 0.3f),
            new Color(0.25f, 0.6f, 0.9f), new Color(0.3f, 0.3f, 0.8f), new Color(0.65f, 0.35f, 0.85f), new Color(0.6f, 0.42f, 0.28f),
        };

        public sealed class Entry
        {
            public string Kind, Id, Name, Glyph;
            public Vector3 Size = Vector3.one;
            public int Mat;
            public bool Resizable => Kind != "prop" && Kind != "item" && Kind != "light" && Kind != "spawn" && Kind != "npc";
        }

        static List<Entry> shapes, items, specials;

        public static List<Entry> Shapes => shapes ??= new List<Entry>
        {
            new Entry { Kind = "floor", Name = "Floor Tile", Glyph = "grid", Size = new Vector3(4, 0.2f, 4), Mat = 0 },
            new Entry { Kind = "wall", Name = "Wall", Glyph = "building", Size = new Vector3(4, 3, 0.25f), Mat = 6 },
            new Entry { Kind = "block", Name = "Block", Glyph = "box", Size = new Vector3(1, 1, 1), Mat = 0 },
            new Entry { Kind = "platform", Name = "Platform", Glyph = "menu", Size = new Vector3(4, 0.3f, 4), Mat = 3 },
            new Entry { Kind = "ramp", Name = "Ramp", Glyph = "arrow_up", Size = new Vector3(3, 2, 5), Mat = 2 },
            new Entry { Kind = "stairs", Name = "Stairs", Glyph = "list", Size = new Vector3(2, 2, 4), Mat = 0 },
            new Entry { Kind = "pillar", Name = "Pillar", Glyph = "pin", Size = new Vector3(0.6f, 3, 0.6f), Mat = 0 },
        };

        public static List<Entry> Items => items ??= new List<Entry>
        {
            new Entry { Kind = "item", Id = "jumppad", Name = "Jump Pad", Glyph = "jump" },
            new Entry { Kind = "item", Id = "door", Name = "Door", Glyph = "door" },
            new Entry { Kind = "item", Id = "target", Name = "Shooting Target", Glyph = "target" },
            new Entry { Kind = "item", Id = "spikes", Name = "Spike Pit", Glyph = "warning", Size = new Vector3(4, 1, 4) },
            new Entry { Kind = "item", Id = "bladefan", Name = "Blade Fan", Glyph = "saw" },
            new Entry { Kind = "item", Id = "press", Name = "Hydraulic Press", Glyph = "hammer" },
            new Entry { Kind = "item", Id = "vending", Name = "Vending Machine", Glyph = "can" },
            new Entry { Kind = "item", Id = "tv", Name = "TV", Glyph = "tv" },
            new Entry { Kind = "item", Id = "radio", Name = "Radio", Glyph = "radio" },
            new Entry { Kind = "item", Id = "bell", Name = "Bell", Glyph = "bell" },
        };

        public static List<Entry> Specials => specials ??= new List<Entry>
        {
            new Entry { Kind = "spawn", Name = "Player Spawn", Glyph = "person" },
            new Entry { Kind = "npc", Name = "Goredoll Spawn", Glyph = "ragdoll" },
            new Entry { Kind = "light", Name = "Point Light", Glyph = "light" },
        };

        public static List<Entry> PropEntries()
        {
            var l = new List<Entry>();
            foreach (var p in PropFactory.Props) l.Add(new Entry { Kind = "prop", Id = p.Id, Name = p.Name, Glyph = p.Glyph });
            return l;
        }

        public static string DisplayName(CustomMapObj o)
        {
            foreach (var e in Shapes) if (e.Kind == o.K) return e.Name;
            foreach (var e in Specials) if (e.Kind == o.K) return e.Name;
            if (o.K == "item") foreach (var e in Items) if (e.Id == o.Id) return e.Name;
            if (o.K == "prop") foreach (var p in PropFactory.Props) if (p.Id == o.Id) return p.Name;
            return o.K;
        }

        public static CustomMapObj FromEntry(Entry e)
        {
            var o = new CustomMapObj { K = e.Kind, Id = e.Id ?? "", Sx = e.Size.x, Sy = e.Size.y, Sz = e.Size.z, Mat = e.Mat };
            if (e.Kind == "light") { o.Cr = 1f; o.Cg = 0.85f; o.Cb = 0.6f; }
            if (e.Kind == "item" && e.Id == "vending") { o.Cr = 0.75f; o.Cg = 0.1f; o.Cb = 0.1f; }
            return o;
        }

        // ------------------------------------------------------------------ materials

        public static Texture2D TexOf(int m)
        {
            switch (m)
            {
                case 1: return TextureFactory.Grass;
                case 2: return TextureFactory.Wood;
                case 3: return TextureFactory.Metal;
                case 4: return TextureFactory.Tiles;
                case 5: return TextureFactory.Sand;
                case 6: return TextureFactory.Brick;
                case 7: return TextureFactory.Asphalt;
                case 8: return TextureFactory.Plaster;
                case 9: return TextureFactory.Dirt;
                case 10: return TextureFactory.Carpet;
                default: return TextureFactory.Concrete;
            }
        }

        public static SurfaceType SurfOf(int m)
        {
            switch (m)
            {
                case 1: return SurfaceType.Grass;
                case 2: return SurfaceType.Wood;
                case 3: return SurfaceType.Metal;
                case 5: case 9: return SurfaceType.Dirt;
                case 10: return SurfaceType.Carpet;
                case MatGlass: case MatGlow: return SurfaceType.Glass;
                default: return SurfaceType.Concrete;
            }
        }

        static Material MatOf(int m, Color tint)
        {
            if (m == MatGlass) return Mats.Glass;
            if (m == MatGlow) return Mats.Glow(tint, 1.6f);
            return Mats.LitTextured(TexOf(m));
        }

        static Color Col(CustomMapObj o) => new Color(o.Cr, o.Cg, o.Cb, 1f);
        static Vector3 Pos(CustomMapObj o) => new Vector3(o.Px, o.Py, o.Pz);
        static Quaternion Rot(CustomMapObj o) => Quaternion.Euler(o.Rx, o.Ry, o.Rz);

        // ------------------------------------------------------------------ building

        /// <summary>Builds the whole map into a MapBuilder (called from the map's MapDef.Build).</summary>
        public static void BuildMap(MapBuilder m, CustomMapData d)
        {
            var staticRoot = new GameObject("Custom Static").transform;
            staticRoot.SetParent(m.Root, false);
            var items = new GameObject("Custom Objects").transform;
            items.SetParent(m.Root, false);

            var area = new Bounds(Vector3.zero, Vector3.zero);
            bool hasArea = false;
            void Grow(Bounds b) { if (!hasArea) { area = b; hasArea = true; } else area.Encapsulate(b); }

            if (d.Ground)
            {
                float g = d.GroundSize * 0.5f;
                m.Block("Ground", new Vector3(-g, -1, -g), new Vector3(g, 0, g), MapBuilder.Tex(TexOf(d.GroundMat)), SurfOf(d.GroundMat), null, 0.25f);
                Grow(new Bounds(Vector3.zero, new Vector3(d.GroundSize, 2, d.GroundSize)));
            }

            bool spawnSet = false;
            foreach (var o in d.Objects)
            {
                var p = Pos(o);
                Grow(new Bounds(p + Vector3.up * o.Sy * 0.5f, new Vector3(Mathf.Max(o.Sx, o.Sz) * 1.5f + 1, o.Sy + 1, Mathf.Max(o.Sx, o.Sz) * 1.5f + 1)));
                switch (o.K)
                {
                    case "spawn":
                        if (!spawnSet) { m.PlayerSpawn = p + Vector3.up * 0.1f; m.PlayerYaw = o.Ry; spawnSet = true; }
                        break;
                    case "npc":
                        m.NpcSpawns.Add(p + Vector3.up * 0.1f);
                        break;
                    case "light":
                    {
                        var go = Build(o, items, false);
                        var l = go != null ? go.GetComponentInChildren<Light>() : null;
                        if (l != null) m.Lamps.Add(l);
                        break;
                    }
                    case "prop": case "item":
                        Build(o, items, false);
                        break;
                    default:
                        Build(o, staticRoot, false);
                        break;
                }
            }
            if (!spawnSet) m.PlayerSpawn = new Vector3(0, 0.1f, 0);

            if (!hasArea) area = new Bounds(Vector3.zero, new Vector3(60, 10, 60));
            // Clamp the playable area so the AI nav grid stays a sane size.
            var min = area.min; var max = area.max;
            min.x = Mathf.Max(min.x - 6, -130); min.z = Mathf.Max(min.z - 6, -130);
            max.x = Mathf.Min(max.x + 6, 130); max.z = Mathf.Min(max.z + 6, 130);
            min.y = Mathf.Min(min.y, 0) - 8f;
            max.y = Mathf.Max(max.y + 20f, min.y + 40f);
            var playable = new Bounds();
            playable.SetMinMax(min, max);
            if (d.Walls) m.Boundaries(playable, playable.size.y);
            else { m.Playable = playable; m.KillY = min.y - 3f; }

            ApplyFog(d);
            if (d.Npcs >= 0 && !NetState.IsClient && m.Options != null) m.Options.StartingNpcs = d.Npcs;
            StaticBatchingUtility.Combine(staticRoot.gameObject);
        }

        public static void ApplyFog(CustomMapData d)
        {
            switch (d.Fog)
            {
                case 1: RenderSettings.fog = false; break;
                case 2: RenderSettings.fogDensity *= 0.35f; break;
                case 3: RenderSettings.fog = true; RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity * 4f, 0.03f); break;
            }
        }

        /// <summary>
        /// Builds one object under <paramref name="parent"/>. In the editor, physics props are kinematic and spawn
        /// points get visible markers; in play they only set data on the MapBuilder (handled by BuildMap).
        /// </summary>
        public static GameObject Build(CustomMapObj o, Transform parent, bool editor)
        {
            var pos = Pos(o);
            var rot = Rot(o);
            var tint = Col(o);
            switch (o.K)
            {
                case "block": case "floor": case "wall": case "platform":
                {
                    var go = Root(o.K, parent, pos, rot);
                    var size = new Vector3(o.Sx, o.Sy, o.Sz);
                    var mb = Mb(o, tint);
                    mb.Box(Vector3.up * size.y * 0.5f, size, 0.5f);
                    Finish(go, mb, o);
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = Vector3.up * size.y * 0.5f; bc.size = size;
                    return go;
                }
                case "pillar":
                {
                    var go = Root(o.K, parent, pos, rot);
                    float r = Mathf.Max(o.Sx, o.Sz) * 0.5f;
                    var mb = Mb(o, tint);
                    mb.Cylinder(Vector3.zero, Vector3.up * o.Sy, r, r, 16);
                    Finish(go, mb, o);
                    if (o.Sy > r * 2f)
                    {
                        var cc = go.AddComponent<CapsuleCollider>();
                        cc.radius = r; cc.height = o.Sy; cc.center = Vector3.up * o.Sy * 0.5f;
                    }
                    else
                    {
                        var bc = go.AddComponent<BoxCollider>();
                        bc.center = Vector3.up * o.Sy * 0.5f; bc.size = new Vector3(r * 2, o.Sy, r * 2);
                    }
                    return go;
                }
                case "ramp":
                {
                    var go = Root(o.K, parent, pos, rot);
                    float h = o.Sy, l = o.Sz;
                    float t = Mathf.Min(0.3f, h * 0.5f);
                    float ang = Mathf.Atan2(h, l) * Mathf.Rad2Deg;
                    float len = Mathf.Sqrt(h * h + l * l);
                    var slabRot = Quaternion.Euler(-ang, 0, 0);
                    var center = new Vector3(0, h * 0.5f, 0) - slabRot * Vector3.up * (t * 0.5f);
                    var slab = new GameObject("Ramp Slab");
                    slab.transform.SetParent(go.transform, false);
                    slab.transform.localPosition = center;
                    slab.transform.localRotation = slabRot;
                    var size = new Vector3(o.Sx, t, len);
                    var mb = Mb(o, tint);
                    mb.Box(Vector3.zero, size, 0.5f);
                    Finish(slab, mb, o);
                    slab.AddComponent<BoxCollider>().size = size;
                    return go;
                }
                case "stairs":
                {
                    var go = Root(o.K, parent, pos, rot);
                    int n = Mathf.Clamp(Mathf.RoundToInt(o.Sy / 0.22f), 1, 64);
                    float run = o.Sz / n;
                    var mb = Mb(o, tint);
                    for (int i = 0; i < n; i++)
                    {
                        float h = (i + 1) * o.Sy / n;
                        var c = new Vector3(0, h * 0.5f, -o.Sz * 0.5f + (i + 0.5f) * run);
                        var s = new Vector3(o.Sx, h, run);
                        mb.Box(c, s, 0.5f);
                        var bc = go.AddComponent<BoxCollider>();
                        bc.center = c; bc.size = s;
                    }
                    Finish(go, mb, o);
                    return go;
                }
                case "light":
                {
                    var go = Root("Light", parent, pos, Quaternion.identity);
                    var bulbGo = new GameObject("Bulb");
                    bulbGo.transform.SetParent(go.transform, false);
                    bulbGo.transform.localPosition = Vector3.up * 0.15f;
                    var mb = new MeshBuilder { Color = Color.white, Gloss = 0.5f };
                    mb.Sphere(Vector3.zero, Vector3.one * 0.15f, 8, 10);
                    bulbGo.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Bulb");
                    var mr = bulbGo.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Mats.Glow(tint, 2.5f);
                    bulbGo.AddComponent<SphereCollider>().radius = 0.15f;
                    bulbGo.AddComponent<Surface>().Type = SurfaceType.Glass;
                    var lightGo = new GameObject("Light");
                    lightGo.transform.SetParent(bulbGo.transform, false);
                    var light = lightGo.AddComponent<Light>();
                    light.type = LightType.Point; light.color = tint; light.range = o.Range; light.intensity = o.Power; light.shadows = LightShadows.None;
                    if (!editor) bulbGo.AddComponent<ShootableLight>().Init(light, mr);
                    return go;
                }
                case "prop":
                {
                    GameObject go = null;
                    if (editor)
                    {
                        foreach (var p in PropFactory.Props)
                            if (p.Id == o.Id) { go = p.Spawn(pos, rot); break; }
                    }
                    else
                    {
                        var rb = PropFactory.Spawn(o.Id, pos, rot);
                        go = rb != null ? rb.gameObject : null;
                    }
                    if (go == null) return editor ? Missing(o, parent, pos) : null;
                    float sc = Mathf.Clamp(o.Sx, 0.2f, 5f);
                    if (!Mathf.Approximately(sc, 1f)) go.transform.localScale *= sc;
                    SitOn(go, pos.y);
                    if (editor) { go.transform.SetParent(parent, true); Freeze(go); }
                    return go;
                }
                case "item":
                {
                    var c = new GameObject("Item " + o.Id);
                    c.transform.SetParent(parent, false);
                    float yaw = o.Ry;
                    switch (o.Id)
                    {
                        case "jumppad": JumpPad.Create(c.transform, pos + Vector3.up * 0.02f); break;
                        case "door": HingeDoor.Create(c.transform, pos, yaw); break;
                        case "target": ShootingTarget.Create(c.transform, pos, yaw); break;
                        case "spikes": SpikeTrap.Create(c.transform, pos, new Vector2(Mathf.Clamp(o.Sx, 1, 20), Mathf.Clamp(o.Sz, 1, 20))); break;
                        case "bladefan": BladeFan.Create(c.transform, pos + rot * Vector3.up * 1.6f, rot); break;
                        case "press": HydraulicPress.Create(c.transform, pos); break;
                        case "vending": VendingMachine.Create(c.transform, pos, yaw, tint); break;
                        case "tv": TVScreen.Create(c.transform, pos, yaw); break;
                        case "radio": Radio.Create(c.transform, pos, yaw); break;
                        case "bell": Bell.Create(c.transform, pos); break;
                        default: Object.Destroy(c); return editor ? Missing(o, parent, pos) : null;
                    }
                    if (editor) Freeze(c);
                    return c;
                }
                case "spawn": case "npc":
                {
                    if (!editor) return null;
                    bool player = o.K == "spawn";
                    var go = Root(o.K, parent, pos, Quaternion.Euler(0, o.Ry, 0));
                    var mb = new MeshBuilder { Color = player ? new Color(0.25f, 0.9f, 0.35f) : new Color(0.95f, 0.2f, 0.2f), Gloss = 0.3f };
                    mb.Capsule(Vector3.up * 0.35f, Vector3.up * 1.45f, 0.3f, 0.3f, 10, 3);
                    mb.SetColor(Color.white, 0.3f);
                    mb.Box(new Vector3(0, 1.5f, 0.32f), new Vector3(0.18f, 0.12f, 0.3f)); // facing nose
                    mb.SetColor(player ? new Color(0.1f, 0.5f, 0.15f) : new Color(0.5f, 0.05f, 0.05f), 0.2f);
                    mb.Cylinder(Vector3.zero, Vector3.up * 0.04f, 0.55f, 0.55f, 18);
                    go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Marker");
                    go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = Vector3.up * 0.9f; bc.size = new Vector3(0.7f, 1.8f, 0.7f);
                    return go;
                }
            }
            return null;
        }

        static GameObject Missing(CustomMapObj o, Transform parent, Vector3 pos)
        {
            var go = Root("Missing " + o.Id, parent, pos, Quaternion.identity);
            var mb = new MeshBuilder { Color = new Color(1f, 0f, 1f), Gloss = 0.2f };
            mb.Box(Vector3.up * 0.25f, Vector3.one * 0.5f);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Missing");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var bc = go.AddComponent<BoxCollider>(); bc.center = Vector3.up * 0.25f; bc.size = Vector3.one * 0.5f;
            return go;
        }

        static GameObject Root(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            return go;
        }

        static MeshBuilder Mb(CustomMapObj o, Color tint)
        {
            bool special = o.Mat == MatGlow;
            return new MeshBuilder { Color = special ? Color.white : tint, Gloss = o.Mat == 3 ? 0.55f : o.Mat == MatGlass ? 0.9f : 0.15f };
        }

        static void Finish(GameObject go, MeshBuilder mb, CustomMapObj o)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(go.name);
            go.AddComponent<MeshRenderer>().sharedMaterial = MatOf(o.Mat, Col(o));
            go.AddComponent<Surface>().Type = SurfOf(o.Mat);
        }

        /// <summary>Moves an object up/down so the bottom of its renderers rests at <paramref name="y"/> (pivot = bottom centre).</summary>
        static void SitOn(GameObject go, float y)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return;
            go.transform.position += Vector3.up * (y - b.min.y + 0.01f);
        }

        static void Freeze(GameObject go)
        {
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }
    }
}
