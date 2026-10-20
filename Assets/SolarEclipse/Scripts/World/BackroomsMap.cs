using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarEclipse
{
    /// <summary>
    /// "The Backrooms" (Level 0): a procedurally generated maze of yellow rooms, corridors, pillars and open halls.
    /// A new layout every time it is played. The layout comes from UnityEngine.Random, which GameManager seeds with
    /// the map seed right before building, so multiplayer clients build exactly the host's layout.
    /// Geometry is merged into one mesh per material per chunk; lighting is emissive ceiling panels plus a small
    /// pool of real point lights that follows the camera (BackroomsLights).
    /// </summary>
    public static class BackroomsMap
    {
        public const string MapId = "backrooms";
        public const int Cells = 30;          // 30 x 30 cells
        public const float CellSize = 4f;     // -> 120 x 120 m
        public const float Height = 3f;       // floor to drop ceiling
        const float Half = Cells * CellSize * 0.5f;
        const float T = 0.24f;                // wall thickness
        const int Chunk = 8;                  // cells per chunk side
        const float DoorW = 1.4f, DoorH = 2.25f;

        public static MapDef CreateDef() => new MapDef
        {
            Id = MapId, Name = "The Backrooms", Size = "Huge - indoor - new layout every time", Glyph = "nextbot",
            DefaultTime = TimeOfDay.Day, Indoor = true, Ambience = "amb_backrooms",
            Description = "Mono-yellow wallpaper, damp carpet and the endless buzz of fluorescent lights. The layout is generated fresh every time you noclip in. You are not alone in here.",
            Features = new[] { "Procedural maze", "New layout every time", "Flickering lights", "Pillar halls", "Indoor" },
            Tags = new[] { "Procedurally Generated" },
            ThemeTop = new Color(0.86f, 0.8f, 0.5f), ThemeBottom = new Color(0.5f, 0.42f, 0.22f), Accent = new Color(1f, 0.9f, 0.45f),
            Build = Build,
        };

        sealed class ChunkData
        {
            public readonly MeshBuilder Walls = new MeshBuilder { Gloss = 0.1f };
            public readonly MeshBuilder Lit = new MeshBuilder { Gloss = 0.5f };
            public readonly MeshBuilder Dead = new MeshBuilder { Color = new Color(0.5f, 0.49f, 0.44f), Gloss = 0.4f };
            public readonly MeshBuilder[] Flicker = { new MeshBuilder { Gloss = 0.5f }, new MeshBuilder { Gloss = 0.5f }, new MeshBuilder { Gloss = 0.5f } };
            public readonly List<Bounds> Colliders = new List<Bounds>();
        }

        static float X(int line) => -Half + line * CellSize;

        static void Build(MapBuilder m)
        {
            int seed = Random.Range(1, int.MaxValue); // deterministic: GameManager seeded Random with the map seed
            var maze = BackroomsMaze.Generate(Cells, seed);
            var rnd = new System.Random(seed ^ 0x5bd1e995);
            int nChunks = (Cells + Chunk - 1) / Chunk;
            var chunks = new ChunkData[nChunks * nChunks];
            for (int i = 0; i < chunks.Length; i++) chunks[i] = new ChunkData();
            ChunkData ChunkOf(int cx, int cz) => chunks[Mathf.Clamp(cz, 0, Cells - 1) / Chunk * nChunks + Mathf.Clamp(cx, 0, Cells - 1) / Chunk];

            // ---------------------------------------------------------------- floor, ceiling
            m.Block("Carpet", new Vector3(-Half, -1, -Half), new Vector3(Half, 0, Half), MapBuilder.Tex(BackroomsArt.Carpet), SurfaceType.Carpet, null, 0.5f);
            m.Block("Drop Ceiling", new Vector3(-Half, Height, -Half), new Vector3(Half, Height + 0.3f, Half), MapBuilder.Tex(BackroomsArt.Ceiling), SurfaceType.Concrete, null, 1f);

            // ---------------------------------------------------------------- walls (runs merged along each grid line)
            void WallBox(ChunkData c, Vector3 min, Vector3 max, bool baseboard)
            {
                var size = max - min;
                c.Walls.Box((min + max) * 0.5f, size, 0.5f);
                c.Colliders.Add(new Bounds((min + max) * 0.5f, size));
                if (baseboard && min.y <= 0.01f)
                {
                    c.Walls.SetColor(new Color(0.45f, 0.36f, 0.2f), 0.2f);
                    var bs = new Vector3(size.x + (size.x < size.z ? 0.04f : 0f), 0.12f, size.z + (size.z < size.x ? 0.04f : 0f));
                    c.Walls.Box(new Vector3((min.x + max.x) * 0.5f, 0.06f, (min.z + max.z) * 0.5f), bs, 0.5f);
                    c.Walls.SetColor(Color.white, 0.1f);
                }
            }

            // Vertical lines (constant x). Line 0 and line Cells are the outer walls.
            for (int lx = 0; lx <= Cells; lx++)
            {
                float x = X(lx);
                int runStart = -1;
                for (int z = 0; z <= Cells; z++)
                {
                    byte e = z == Cells ? (byte)255 : lx == 0 || lx == Cells ? BackroomsMaze.Solid : maze.EdgeE[lx - 1, z];
                    bool solid = e == BackroomsMaze.Solid;
                    bool breakHere = z % Chunk == 0 && runStart >= 0 && runStart != z;
                    if (runStart >= 0 && (!solid || breakHere))
                    {
                        WallBox(ChunkOf(lx, runStart), new Vector3(x - T / 2, 0, X(runStart) - T / 2), new Vector3(x + T / 2, Height, X(z) + T / 2), true);
                        runStart = -1;
                    }
                    if (solid && runStart < 0) runStart = z;
                    if (e == BackroomsMaze.Door)
                    {
                        var c = ChunkOf(lx, z);
                        float z0 = X(z), gc = z0 + 1.1f + (float)rnd.NextDouble() * (CellSize - 2.2f);
                        WallBox(c, new Vector3(x - T / 2, 0, z0 - T / 2), new Vector3(x + T / 2, Height, gc - DoorW / 2), true);
                        WallBox(c, new Vector3(x - T / 2, 0, gc + DoorW / 2), new Vector3(x + T / 2, Height, z0 + CellSize + T / 2), true);
                        WallBox(c, new Vector3(x - T / 2, DoorH, gc - DoorW / 2), new Vector3(x + T / 2, Height, gc + DoorW / 2), false);
                    }
                }
            }
            // Horizontal lines (constant z).
            for (int lz = 0; lz <= Cells; lz++)
            {
                float z = X(lz);
                int runStart = -1;
                for (int x = 0; x <= Cells; x++)
                {
                    byte e = x == Cells ? (byte)255 : lz == 0 || lz == Cells ? BackroomsMaze.Solid : maze.EdgeN[x, lz - 1];
                    bool solid = e == BackroomsMaze.Solid;
                    bool breakHere = x % Chunk == 0 && runStart >= 0 && runStart != x;
                    if (runStart >= 0 && (!solid || breakHere))
                    {
                        WallBox(ChunkOf(runStart, lz), new Vector3(X(runStart) - T / 2, 0, z - T / 2), new Vector3(X(x) + T / 2, Height, z + T / 2), true);
                        runStart = -1;
                    }
                    if (solid && runStart < 0) runStart = x;
                    if (e == BackroomsMaze.Door)
                    {
                        var c = ChunkOf(x, lz);
                        float x0 = X(x), gc = x0 + 1.1f + (float)rnd.NextDouble() * (CellSize - 2.2f);
                        WallBox(c, new Vector3(x0 - T / 2, 0, z - T / 2), new Vector3(gc - DoorW / 2, Height, z + T / 2), true);
                        WallBox(c, new Vector3(gc + DoorW / 2, 0, z - T / 2), new Vector3(x0 + CellSize + T / 2, Height, z + T / 2), true);
                        WallBox(c, new Vector3(gc - DoorW / 2, DoorH, z - T / 2), new Vector3(gc + DoorW / 2, Height, z + T / 2), false);
                    }
                }
            }
            // Pillars on open vertices.
            foreach (var (vx, vz) in maze.Pillars)
            {
                float px = X(vx), pz = X(vz), s = 0.32f;
                WallBox(ChunkOf(vx, vz), new Vector3(px - s, 0, pz - s), new Vector3(px + s, Height, pz + s), true);
            }

            // ---------------------------------------------------------------- ceiling light panels
            var panels = new List<BackroomsLights.Panel>();
            bool night = m.Options.Time == TimeOfDay.Night, blood = m.Options.Time == TimeOfDay.BloodMoon;
            for (int z = 0; z < Cells; z++)
                for (int x = 0; x < Cells; x++)
                {
                    bool spawnRoom = Mathf.Abs(x - maze.SpawnX) <= 1 && Mathf.Abs(z - maze.SpawnZ) <= 1;
                    double roll = rnd.NextDouble();
                    if (!spawnRoom && roll < (night ? 0.3 : 0.12)) continue; // no fixture: darker patches
                    var c = ChunkOf(x, z);
                    bool alongX = rnd.NextDouble() < 0.5;
                    var pos = new Vector3(X(x) + CellSize * 0.5f, Height - 0.015f, X(z) + CellSize * 0.5f);
                    var size = alongX ? new Vector3(1f, 0.03f, 0.5f) : new Vector3(0.5f, 0.03f, 1f);
                    double state = rnd.NextDouble();
                    if (!spawnRoom && state < (night ? 0.18 : 0.06)) { c.Dead.Box(pos, size, 1f); continue; }
                    int group = !spawnRoom && state < (night ? 0.32 : 0.17) ? rnd.Next(3) : -1;
                    (group >= 0 ? c.Flicker[group] : c.Lit).Box(pos, size, 1f);
                    // Frame around the panel.
                    c.Walls.SetColor(new Color(0.75f, 0.73f, 0.66f), 0.3f);
                    c.Walls.Box(pos + Vector3.up * 0.008f, size + new Vector3(0.08f, 0f, 0.08f), 1f);
                    c.Walls.SetColor(Color.white, 0.1f);
                    panels.Add(new BackroomsLights.Panel { Pos = pos, Group = group });
                }

            // ---------------------------------------------------------------- materials + chunk objects
            var lightCol = blood ? new Color(1f, 0.45f, 0.38f) : new Color(1f, 0.96f, 0.8f);
            var wallMat = MapBuilder.Tex(BackroomsArt.Wallpaper);
            var litMat = Mats.Glow(lightCol, night ? 1.6f : 2.2f);
            var deadMat = Mats.Lit;
            var flickerMats = new Material[3];
            for (int i = 0; i < 3; i++) flickerMats[i] = new Material(litMat) { name = "BR_Flicker" + i };
            var chunkRoot = new GameObject("Backrooms Chunks").transform;
            chunkRoot.SetParent(m.Root, false);
            for (int i = 0; i < chunks.Length; i++)
            {
                var c = chunks[i];
                var walls = Emit(chunkRoot, "Walls " + i, c.Walls, wallMat);
                if (walls != null)
                {
                    foreach (var b in c.Colliders) { var bc = walls.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size; }
                    walls.AddComponent<Surface>().Type = SurfaceType.Concrete;
                }
                Emit(chunkRoot, "Panels " + i, c.Lit, litMat);
                Emit(chunkRoot, "Dead Panels " + i, c.Dead, deadMat);
                for (int g = 0; g < 3; g++) Emit(chunkRoot, "Flicker Panels " + i + "_" + g, c.Flicker[g], flickerMats[g]);
            }

            // ---------------------------------------------------------------- spawn points
            Vector3 CellCenter(int x, int z) => new Vector3(X(x) + CellSize * 0.5f, 0.1f, X(z) + CellSize * 0.5f);
            m.PlayerSpawn = CellCenter(maze.SpawnX, maze.SpawnZ) + new Vector3(0, 0, -1f);
            m.PlayerYaw = rnd.Next(4) * 90f;
            var dist = maze.Distances(maze.SpawnX, maze.SpawnZ);
            var far = new List<(int x, int z)>();
            for (int z = 0; z < Cells; z++) for (int x = 0; x < Cells; x++) if (dist[x, z] >= 4) far.Add((x, z));
            for (int i = 0; i < 10 && far.Count > 0; i++)
            {
                int k = rnd.Next(far.Count);
                var (cx, cz) = far[k];
                far.RemoveAt(k);
                m.NpcSpawns.Add(CellCenter(cx, cz) + new Vector3(((float)rnd.NextDouble() - 0.5f) * 1.5f, 0, ((float)rnd.NextDouble() - 0.5f) * 1.5f));
            }
            // A few abandoned office chairs and tables; the duck hides in the furthest cell.
            for (int i = 0; i < 9 && far.Count > 0; i++)
            {
                int k = rnd.Next(far.Count);
                var (cx, cz) = far[k];
                far.RemoveAt(k);
                PropFactory.Spawn(i % 3 == 0 ? "table" : "chair", CellCenter(cx, cz) + Vector3.up * 0.05f, Quaternion.Euler(0, rnd.Next(360), 0));
            }
            int bestX = 0, bestZ = 0;
            for (int z = 0; z < Cells; z++) for (int x = 0; x < Cells; x++) if (dist[x, z] > dist[bestX, bestZ]) { bestX = x; bestZ = z; }
            RubberDuck.Create(m.Root, CellCenter(bestX, bestZ) + new Vector3(1.2f, -0.08f, 1.2f), "backrooms_duck");

            // ---------------------------------------------------------------- bounds, nav, atmosphere
            m.Boundaries(new Bounds(new Vector3(0, 1.5f, 0), new Vector3(Half * 2f + 0.5f, 5f, Half * 2f + 0.5f)), 7f);
            // NavGrid probes from Playable.max.y downward: keep it below the ceiling so only the carpet counts as ground.
            m.Playable = new Bounds(new Vector3(0, (Height - 0.2f - 1f) * 0.5f, 0), new Vector3(Half * 2f, Height - 0.2f + 1f, Half * 2f));

            float bright = Settings.Data.brightness;
            var amb = blood ? new Color(0.4f, 0.16f, 0.12f) : new Color(0.46f, 0.41f, 0.24f);
            if (night) amb *= 0.55f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = amb * bright;
            RenderSettings.ambientEquatorColor = amb * 0.85f * bright;
            RenderSettings.ambientGroundColor = amb * 0.45f * bright;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = blood ? new Color(0.3f, 0.12f, 0.08f) : night ? new Color(0.18f, 0.16f, 0.09f) : new Color(0.4f, 0.36f, 0.2f);
            RenderSettings.fogDensity = night ? 0.05f : 0.032f;
            RenderSettings.reflectionIntensity = 0.15f;
            if (MapLighting.Sun != null) MapLighting.Sun.intensity = 0f;
            QualitySettings.pixelLightCount = 8;

            var ctl = m.Root.gameObject.AddComponent<BackroomsLights>();
            ctl.Init(panels.ToArray(), flickerMats, lightCol * (night ? 1.6f : 2.2f), lightCol, night ? 0.9f : 1.25f);
        }

        static GameObject Emit(Transform parent, string name, MeshBuilder mb, Material mat)
        {
            if (mb.VertexCount == 0) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
            return go;
        }
    }

    /// <summary>
    /// Backrooms lighting at runtime: a small pool of real point lights snaps to the ceiling panels nearest the camera,
    /// three flicker groups drive the emission of the flickering panels (and the lights on them), and a nearby panel
    /// that cuts out buzzes.
    /// </summary>
    public sealed class BackroomsLights : MonoBehaviour
    {
        public struct Panel { public Vector3 Pos; public int Group; }

        const int PoolSize = 8;
        Panel[] panels;
        Material[] flickerMats;
        Color emission, lightColor;
        float baseIntensity;
        readonly Light[] pool = new Light[PoolSize];
        readonly int[] assigned = new int[PoolSize];
        readonly float[] fade = new float[PoolSize];
        readonly float[] groupValue = { 1f, 1f, 1f };
        readonly int[] nearestOfGroup = { -1, -1, -1 };
        float nextScan, nextBuzz;

        public void Init(Panel[] p, Material[] flicker, Color emissionColor, Color color, float intensity)
        {
            panels = p; flickerMats = flicker; emission = emissionColor; lightColor = color; baseIntensity = intensity;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Backrooms Light " + i);
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.color = lightColor; l.range = 9.5f; l.intensity = 0f; l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.Auto;
                l.enabled = false;
                pool[i] = l;
                assigned[i] = -1;
            }
        }

        static float Flicker(int g, float t)
        {
            float slow = Mathf.PerlinNoise(t * 0.33f, g * 7.31f + 1.7f);
            if (slow < 0.6f) return 1f;
            float fast = Mathf.PerlinNoise(t * 17f, g * 3.77f + 9.1f);
            return fast > 0.42f ? 1f : 0.06f;
        }

        void Update()
        {
            if (panels == null) return;
            float t = Time.time;
            for (int g = 0; g < 3; g++)
            {
                float v = Flicker(g, t);
                if (v < 0.5f && groupValue[g] >= 0.5f && t >= nextBuzz && nearestOfGroup[g] >= 0)
                {
                    var cam = Camera.main;
                    var p = panels[nearestOfGroup[g]].Pos;
                    if (cam != null && (cam.transform.position - p).sqrMagnitude < 14f * 14f)
                    {
                        AudioManager.Play("backrooms_flicker", p, 0.45f, Random.Range(0.9f, 1.1f));
                        nextBuzz = t + 0.35f;
                    }
                }
                groupValue[g] = v;
                if (flickerMats[g] != null) flickerMats[g].SetColor("_EmissionColor", emission * v);
            }
            if (t >= nextScan) { nextScan = t + 0.2f; Scan(); }
            float dt = Time.deltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                var l = pool[i];
                if (l == null) continue;
                if (assigned[i] < 0) { l.enabled = false; continue; }
                fade[i] = Mathf.Min(1f, fade[i] + dt * 4f);
                int g = panels[assigned[i]].Group;
                l.intensity = baseIntensity * fade[i] * (g >= 0 ? groupValue[g] : 1f);
                l.enabled = true;
            }
        }

        readonly int[] best = new int[PoolSize];
        readonly float[] bestD = new float[PoolSize];

        void Scan()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var c = cam.transform.position;
            int count = 0;
            float[] groupD = { float.MaxValue, float.MaxValue, float.MaxValue };
            for (int i = 0; i < panels.Length; i++)
            {
                var d3 = panels[i].Pos - c;
                float d = d3.x * d3.x + d3.z * d3.z + d3.y * d3.y * 4f;
                int g = panels[i].Group;
                if (g >= 0 && d < groupD[g]) { groupD[g] = d; nearestOfGroup[g] = i; }
                if (count < PoolSize) { best[count] = i; bestD[count] = d; count++; Bubble(count - 1); }
                else if (d < bestD[PoolSize - 1]) { best[PoolSize - 1] = i; bestD[PoolSize - 1] = d; Bubble(PoolSize - 1); }
            }
            // Keep lights already on a chosen panel; move the rest.
            for (int s = 0; s < PoolSize; s++)
            {
                if (assigned[s] < 0) continue;
                bool keep = false;
                for (int k = 0; k < count; k++) if (best[k] == assigned[s]) { keep = true; break; }
                if (!keep) assigned[s] = -1;
            }
            for (int k = 0; k < count; k++)
            {
                int p = best[k];
                bool taken = false;
                for (int s = 0; s < PoolSize; s++) if (assigned[s] == p) { taken = true; break; }
                if (taken) continue;
                for (int s = 0; s < PoolSize; s++)
                    if (assigned[s] < 0)
                    {
                        assigned[s] = p; fade[s] = 0f;
                        pool[s].transform.position = panels[p].Pos + Vector3.down * 0.35f;
                        break;
                    }
            }
        }

        void Bubble(int i)
        {
            while (i > 0 && bestD[i] < bestD[i - 1])
            {
                (bestD[i], bestD[i - 1]) = (bestD[i - 1], bestD[i]);
                (best[i], best[i - 1]) = (best[i - 1], best[i]);
                i--;
            }
        }
    }
}
