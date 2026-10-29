using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// "Alleyway": a narrow city alley between brick buildings at night. The player starts on the street; the alley
    /// runs north 56 m to a dead end, and 30 m in it opens to the right into a back courtyard (26 x 26 m, room for a
    /// fight). Dumpsters, trash bags, fire escapes, graffiti, puddles, wall lamps. Playable in sandbox; started with
    /// <see cref="StoryMode.Begin"/> it hosts story chapter 1 (Big Marcus in the alley, the barrier before the corner,
    /// Z in the courtyard). Buildings are solid blocks, so the NavGrid samples only the street, alley and courtyard
    /// (the probe starts at 9 m, inside the buildings, which therefore don't count as walkable roofs).
    /// </summary>
    public static class AlleywayMap
    {
        public const string MapId = "alleyway";

        // Layout (metres). Alley: x -3.5..3.5, z 0..56. Courtyard: x 3.5..30, z 30..56. Street: z -16..0.
        const float AlleyHalf = 3.5f, AlleyEnd = 56f, CornerZ = 30f, CourtX = 30f;

        public static MapDef CreateDef()
        {
            EnsureAchievement();
            return new MapDef
            {
                Id = MapId, Name = "Alleyway", Size = "Small - city alley", Glyph = "home", DefaultTime = TimeOfDay.Night, Ambience = "amb_city",
                Description = "A narrow alley between brick buildings at night: dumpsters, fire escapes, graffiti, puddles, and a back courtyard round the corner. Story mode chapter 1 takes place here.",
                Features = new[] { "Night alley", "Back courtyard", "Dumpsters & trash", "Fire escapes", "Story chapter 1" },
                Tags = new[] { "Story" },
                ThemeTop = new Color(0.03f, 0.03f, 0.08f), ThemeBottom = new Color(0.3f, 0.16f, 0.14f), Accent = new Color(1f, 0.78f, 0.16f),
                Build = Build, Thumbnail = Thumb(),
            };
        }

        public static void EnsureAchievement()
        {
            if (!Stats.Achievements.Exists(a => a.Id == "story_ch1"))
                Stats.Achievements.Add(new Achievement("story_ch1", "Chapter 1 Complete", "Finish story chapter 1: take out Z.", "story_ch1", 1));
        }

        static Material M(Texture2D t) => MapBuilder.Tex(t);

        static void Build(MapBuilder m)
        {
            bool story = StoryMode.ConsumePending();
            m.Options.StartingNpcs = story ? 0 : m.Options.StartingNpcs;
            if (story) m.Options.Nextbots = false;

            var asphalt = M(TextureFactory.Asphalt);
            var brick = M(TextureFactory.Brick);
            var conc = M(TextureFactory.Concrete);
            var brickTint = new Color(0.62f, 0.38f, 0.3f);
            var brickTint2 = new Color(0.5f, 0.33f, 0.28f);
            var brickTint3 = new Color(0.45f, 0.4f, 0.38f);

            // Ground
            m.Block("Ground", new Vector3(-40, -1, -30), new Vector3(50, 0, 70), asphalt, SurfaceType.Concrete, new Color(0.45f, 0.45f, 0.48f), 0.25f);
            m.Block("Sidewalk", new Vector3(-26, 0, -2.2f), new Vector3(32, 0.02f, 0), conc, SurfaceType.Concrete, new Color(0.6f, 0.6f, 0.62f), 0.5f, false);
            m.Block("Sidewalk Far", new Vector3(-26, 0, -16), new Vector3(32, 0.02f, -13.8f), conc, SurfaceType.Concrete, new Color(0.6f, 0.6f, 0.62f), 0.5f, false);
            for (float x = -24f; x < 31f; x += 5f)
                m.Block("Lane Mark", new Vector3(x, 0, -8.1f), new Vector3(x + 2.4f, 0.015f, -7.9f), Mats.Lit, SurfaceType.Concrete, new Color(0.85f, 0.75f, 0.3f), 1f, false);
            m.Block("Alley Floor", new Vector3(-AlleyHalf, 0, 0), new Vector3(CourtX, 0.012f, AlleyEnd), conc, SurfaceType.Concrete, new Color(0.38f, 0.37f, 0.36f), 0.4f, false);

            // Buildings
            m.Block("Building Across", new Vector3(-40, 0, -30), new Vector3(50, 14, -16), brick, SurfaceType.Concrete, brickTint2, 0.25f);
            m.Block("Street End W", new Vector3(-40, 0, -16), new Vector3(-26, 14, 0), brick, SurfaceType.Concrete, brickTint3, 0.25f);
            m.Block("Street End E", new Vector3(32, 0, -16), new Vector3(50, 14, 0), brick, SurfaceType.Concrete, brickTint3, 0.25f);
            m.Block("Building West", new Vector3(-26, 0, 0), new Vector3(-AlleyHalf, 15, AlleyEnd), brick, SurfaceType.Concrete, brickTint, 0.25f);
            m.Block("Building East", new Vector3(AlleyHalf, 0, 0), new Vector3(32, 12, CornerZ), brick, SurfaceType.Concrete, brickTint2, 0.25f);
            m.Block("Building Courtyard E", new Vector3(CourtX, 0, CornerZ), new Vector3(50, 13, 60), brick, SurfaceType.Concrete, brickTint3, 0.25f);
            m.Block("Building North", new Vector3(-26, 0, AlleyEnd), new Vector3(50, 16, 70), brick, SurfaceType.Concrete, brickTint, 0.25f);
            // Roof lips (silhouettes against the sky).
            m.Block("Roof Lip", new Vector3(-AlleyHalf - 0.3f, 15, 0), new Vector3(-AlleyHalf, 15.5f, AlleyEnd), conc, SurfaceType.Concrete, new Color(0.35f, 0.35f, 0.37f), 0.5f, false);
            m.Block("Roof Lip", new Vector3(AlleyHalf, 12, 0), new Vector3(AlleyHalf + 0.3f, 12.5f, CornerZ), conc, SurfaceType.Concrete, new Color(0.35f, 0.35f, 0.37f), 0.5f, false);

            Windows(m);
            FireEscape(m, -AlleyHalf, 1f, 4f, 9f);     // west wall, facing east
            FireEscape(m, AlleyHalf, -1f, 14f, 9f);    // east wall, facing west
            FireEscape(m, CourtX, -1f, 38f, 8f);       // courtyard east wall

            // Doors (decorative) and a neon strip at the alley mouth.
            m.Box("Back Door", new Vector3(-AlleyHalf + 0.03f, 1.1f, 12f), Quaternion.identity, new Vector3(0.08f, 2.2f, 1.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.3f, 0.28f), 0.5f, false);
            m.Box("Back Door", new Vector3(AlleyHalf - 0.03f, 1.1f, 21f), Quaternion.identity, new Vector3(0.08f, 2.2f, 1.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.35f, 0.18f, 0.15f), 0.5f, false);
            m.Box("Back Door", new Vector3(16f, 1.1f, CornerZ + 0.03f), Quaternion.identity, new Vector3(1.2f, 2.2f, 0.08f), Mats.LitMetal, SurfaceType.Metal, new Color(0.2f, 0.22f, 0.3f), 0.5f, false);
            m.Box("Neon", new Vector3(-AlleyHalf - 0.6f, 4.2f, -0.05f), Quaternion.identity, new Vector3(3.2f, 0.18f, 0.1f), Mats.Glow(new Color(1f, 0.2f, 0.6f), 2.5f), SurfaceType.Glass, Color.white, 1f, false);
            m.Box("Neon", new Vector3(AlleyHalf + 1.2f, 3.6f, -0.05f), Quaternion.identity, new Vector3(2.2f, 0.18f, 0.1f), Mats.Glow(new Color(0.2f, 0.85f, 1f), 2.5f), SurfaceType.Glass, Color.white, 1f, false);

            // Lights: street lamps, wall lamps in the alley and courtyard.
            m.StreetLamp(new Vector3(-12, 0, -1.2f), 180f, new Color(1f, 0.75f, 0.45f), 5.5f, 18f, 2.2f);
            m.StreetLamp(new Vector3(14, 0, -1.2f), 180f, new Color(1f, 0.75f, 0.45f), 5.5f, 18f, 2.2f);
            m.StreetLamp(new Vector3(2, 0, -14.6f), 0f, new Color(1f, 0.75f, 0.45f), 5.5f, 18f, 2f);
            var warm = new Color(1f, 0.72f, 0.42f);
            m.CeilingLight(new Vector3(-AlleyHalf + 0.25f, 3.6f, 6f), warm, 10f, 1.5f, new Vector3(0.3f, 0.25f, 0.45f));
            m.CeilingLight(new Vector3(AlleyHalf - 0.25f, 3.8f, 15f), new Color(0.7f, 0.85f, 1f), 10f, 1.3f, new Vector3(0.3f, 0.25f, 0.45f));
            m.CeilingLight(new Vector3(-AlleyHalf + 0.25f, 3.6f, 24f), warm, 10f, 1.5f, new Vector3(0.3f, 0.25f, 0.45f));
            m.CeilingLight(new Vector3(-AlleyHalf + 0.25f, 4f, 40f), warm, 12f, 1.6f, new Vector3(0.3f, 0.25f, 0.45f));
            m.CeilingLight(new Vector3(16f, 4.2f, CornerZ + 0.25f), warm, 14f, 1.8f, new Vector3(0.45f, 0.25f, 0.3f));
            m.CeilingLight(new Vector3(CourtX - 0.25f, 4.2f, 47f), new Color(0.75f, 0.85f, 1f), 14f, 1.5f, new Vector3(0.3f, 0.25f, 0.45f));
            m.CeilingLight(new Vector3(10f, 4.4f, AlleyEnd - 0.25f), warm, 14f, 1.6f, new Vector3(0.45f, 0.25f, 0.3f));

            // Dumpsters, trash, crates.
            Dumpster(m, new Vector3(-AlleyHalf + 0.75f, 0, 8f), 0f, new Color(0.15f, 0.35f, 0.2f));
            Dumpster(m, new Vector3(AlleyHalf - 0.75f, 0, 10.5f), 0f, new Color(0.2f, 0.25f, 0.4f));
            Dumpster(m, new Vector3(CourtX - 0.8f, 0, 34f), 0f, new Color(0.15f, 0.35f, 0.2f));
            Dumpster(m, new Vector3(8f, 0, AlleyEnd - 0.8f), 90f, new Color(0.45f, 0.2f, 0.12f));
            var rnd = new System.Random(5150);
            void Bags(Vector3 c, int n) { for (int i = 0; i < n; i++) TrashBag(m, c + new Vector3((float)rnd.NextDouble() * 1.4f - 0.7f, 0, (float)rnd.NextDouble() * 1.6f - 0.8f), rnd); }
            Bags(new Vector3(-AlleyHalf + 0.6f, 0, 10.3f), 4);
            Bags(new Vector3(AlleyHalf - 0.6f, 0, 6.5f), 3);
            Bags(new Vector3(-AlleyHalf + 0.7f, 0, 28f), 3);
            Bags(new Vector3(CourtX - 0.8f, 0, 37f), 4);
            Bags(new Vector3(12f, 0, AlleyEnd - 0.7f), 3);
            Bags(new Vector3(-AlleyHalf + 0.7f, 0, 50f), 3);

            PropFactory.Spawn("crate", new Vector3(22f, 0.41f, 54.6f), Quaternion.Euler(0, 10, 0));
            PropFactory.Spawn("crate", new Vector3(23f, 0.41f, 54.5f), Quaternion.Euler(0, -5, 0));
            PropFactory.Spawn("small_crate", new Vector3(22.5f, 1.1f, 54.6f), Quaternion.identity);
            PropFactory.Spawn("pallet", new Vector3(6f, 0.1f, 32f), Quaternion.Euler(0, 20, 0));
            PropFactory.Spawn("barrel", new Vector3(28.8f, 0.46f, 52f), Quaternion.identity);
            PropFactory.Spawn("barrel", new Vector3(28.6f, 0.46f, 50.9f), Quaternion.identity);
            PropFactory.Spawn("explosive_barrel", new Vector3(5.2f, 0.46f, 54.6f), Quaternion.identity);
            PropFactory.Spawn("tire", new Vector3(-2.6f, 0.5f, 45f), Quaternion.identity);
            PropFactory.Spawn("cone", new Vector3(2.4f, 0.02f, -0.8f), Quaternion.identity);

            // Puddles and graffiti.
            var puddleCol = new Color(0.05f, 0.06f, 0.09f);
            foreach (var p in new[] { new Vector4(0.8f, 0, 4f, 1.6f), new Vector4(-1.2f, 0, 13f, 2.2f), new Vector4(1.5f, 0, 21f, 1.3f), new Vector4(12f, 0, 41f, 2.6f), new Vector4(20f, 0, 48f, 1.8f), new Vector4(-6f, 0, -6f, 2f), new Vector4(9f, 0, -11f, 1.6f) })
                m.Box("Puddle", new Vector3(p.x, 0.018f, p.z), Quaternion.Euler(0, p.x * 37f % 90f, 0), new Vector3(p.w, 0.01f, p.w * 0.65f), Mats.Lit, SurfaceType.Glass, puddleCol, 1f, false);
            Graffiti(m, new Vector3(-AlleyHalf + 0.02f, 1.6f, 17f), 90f, 4.2f, 2.1f, 1);
            Graffiti(m, new Vector3(AlleyHalf - 0.02f, 1.5f, 25f), -90f, 3.2f, 1.6f, 2);
            Graffiti(m, new Vector3(20f, 1.7f, AlleyEnd - 0.02f), 180f, 5f, 2.4f, 3);
            Graffiti(m, new Vector3(-AlleyHalf + 0.02f, 1.5f, 44f), 90f, 3f, 1.5f, 4);

            // Street: parked cars.
            m.Car(new Vector3(-14f, 0, -4.2f), 90f, new Color(0.5f, 0.08f, 0.08f));
            m.Car(new Vector3(19f, 0, -4.2f), -90f, new Color(0.15f, 0.18f, 0.22f));
            m.Car(new Vector3(-2f, 0, -12.2f), -90f, new Color(0.75f, 0.72f, 0.65f));

            m.PlayerSpawn = new Vector3(0, 0.1f, -7f);
            m.PlayerYaw = 0f;
            m.NpcSpawns.Add(new Vector3(-8f, 0.1f, -8f));
            m.NpcSpawns.Add(new Vector3(0f, 0.1f, 12f));
            m.NpcSpawns.Add(new Vector3(16f, 0.1f, 44f));
            m.NpcSpawns.Add(new Vector3(10f, 0.1f, -10f));
            m.Boundaries(new Bounds(new Vector3(3f, 15f, 20f), new Vector3(58f, 30f, 72f)));
            // NavGrid probes from 9 m down: inside the buildings (so they don't count as walkable roofs).
            m.Playable = new Bounds(new Vector3(3f, 4f, 20f), new Vector3(58f, 10f, 72f));
            m.KillY = -20f;

            if (story)
            {
                var s = m.Root.gameObject.AddComponent<StoryMode>();
                s.MarcusPos = new Vector3(-1.4f, 0f, 17f);
                s.MarcusYaw = 180f;
                s.BarrierPos = new Vector3(0f, 0f, 26.5f);
                s.BarrierYaw = 0f;
                s.BarrierWidth = AlleyHalf * 2f + 0.2f;
                s.ZPos = new Vector3(17f, 0.05f, 45f);
                s.ZYaw = -110f;
                s.ZTrigger = new Vector3(2f, 0f, 34f);
                s.ThugSpawns = new[] { new Vector3(26f, 0.05f, 53f), new Vector3(-1f, 0.05f, 53f) };
                s.SpawnCast(m.Root);
            }
        }

        // ------------------------------------------------------------------ pieces

        static void Windows(MapBuilder m)
        {
            var lit = Mats.Glow(new Color(1f, 0.82f, 0.5f), 1.2f);
            var rnd = new System.Random(77);
            void Row(Vector3 start, Vector3 step, int count, float faceX, bool alongZ)
            {
                for (float y = 4.5f; y < 12f; y += 3.3f)
                    for (int i = 0; i < count; i++)
                    {
                        var p = start + step * i + Vector3.up * y;
                        bool on = rnd.NextDouble() < 0.3;
                        var size = alongZ ? new Vector3(0.06f, 1.5f, 1.1f) : new Vector3(1.1f, 1.5f, 0.06f);
                        m.Box("Window", p, Quaternion.identity, size, on ? lit : Mats.Lit, SurfaceType.Glass, on ? Color.white : new Color(0.08f, 0.1f, 0.14f), 1f, false);
                    }
            }
            Row(new Vector3(-AlleyHalf + 0.02f, 0, 3f), new Vector3(0, 0, 4.5f), 12, -AlleyHalf, true);
            Row(new Vector3(AlleyHalf - 0.02f, 0, 3f), new Vector3(0, 0, 4.5f), 6, AlleyHalf, true);
            Row(new Vector3(CourtX - 0.02f, 0, 33f), new Vector3(0, 0, 4.5f), 5, CourtX, true);
            Row(new Vector3(6f, 0, CornerZ + 0.02f), new Vector3(4.5f, 0, 0), 5, 0, false);
            Row(new Vector3(-1f, 0, AlleyEnd - 0.02f), new Vector3(4.5f, 0, 0), 7, 0, false);
            Row(new Vector3(-22f, 0, -0.02f), new Vector3(4.5f, 0, 0), 4, 0, false);
            Row(new Vector3(6f, 0, -0.02f), new Vector3(4.5f, 0, 0), 6, 0, false);
            Row(new Vector3(-22f, 0, -15.98f), new Vector3(4.5f, 0, 0), 12, 0, false);
        }

        /// <summary>Decorative fire escape (no colliders, so it doesn't confuse the NavGrid): landings, rails, ladders.</summary>
        static void FireEscape(MapBuilder m, float wallX, float outward, float z0, float len)
        {
            var metal = Mats.LitMetal;
            var col = new Color(0.16f, 0.17f, 0.18f);
            float depth = 1.1f;
            float x = wallX + outward * depth * 0.5f;
            for (int level = 0; level < 3; level++)
            {
                float y = 3.6f + level * 3.3f;
                m.Box("Fire Escape", new Vector3(x, y, z0 + len * 0.5f), Quaternion.identity, new Vector3(depth, 0.06f, len), metal, SurfaceType.Metal, col, 1f, false);
                m.Box("Fire Escape Rail", new Vector3(wallX + outward * depth, y + 0.9f, z0 + len * 0.5f), Quaternion.identity, new Vector3(0.05f, 0.05f, len), metal, SurfaceType.Metal, col, 1f, false);
                for (float zz = z0; zz <= z0 + len + 0.01f; zz += 1.5f)
                    m.Box("Fire Escape Post", new Vector3(wallX + outward * depth, y + 0.45f, zz), Quaternion.identity, new Vector3(0.04f, 0.9f, 0.04f), metal, SurfaceType.Metal, col, 1f, false);
                // Stair down to the level below (diagonal).
                float rise = 3.3f;
                var a = new Vector3(x, y - rise * 0.5f + 0.05f, z0 + len * 0.62f);
                float ang = Mathf.Atan2(rise, len * 0.45f) * Mathf.Rad2Deg;
                if (level > 0) m.Box("Fire Escape Stair", a, Quaternion.Euler(-ang, 0, 0), new Vector3(depth * 0.7f, 0.06f, Mathf.Sqrt(rise * rise + len * 0.45f * len * 0.45f)), metal, SurfaceType.Metal, col, 1f, false);
            }
            // Drop ladder (raised).
            m.Box("Fire Escape Ladder", new Vector3(wallX + outward * (depth - 0.2f), 2.3f, z0 + 1f), Quaternion.identity, new Vector3(0.05f, 2.6f, 0.05f), metal, SurfaceType.Metal, col, 1f, false);
            m.Box("Fire Escape Ladder", new Vector3(wallX + outward * (depth - 0.2f), 2.3f, z0 + 1.45f), Quaternion.identity, new Vector3(0.05f, 2.6f, 0.05f), metal, SurfaceType.Metal, col, 1f, false);
            for (float y = 1.2f; y < 3.6f; y += 0.35f)
                m.Box("Fire Escape Rung", new Vector3(wallX + outward * (depth - 0.2f), y, z0 + 1.225f), Quaternion.identity, new Vector3(0.03f, 0.03f, 0.45f), metal, SurfaceType.Metal, col, 1f, false);
        }

        static void Dumpster(MapBuilder m, Vector3 basePos, float yaw, Color paint)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            m.Box("Dumpster", basePos + Vector3.up * 0.65f, rot, new Vector3(1.3f, 1.2f, 2f), Mats.LitMetal, SurfaceType.Metal, paint);
            m.Box("Dumpster Lid", basePos + rot * new Vector3(0.05f, 1.3f, 0), rot * Quaternion.Euler(0, 0, -6f), new Vector3(1.45f, 0.06f, 2.05f), Mats.LitMetal, SurfaceType.Metal, paint * 0.7f, 1f, false);
            m.Box("Dumpster Rim", basePos + rot * new Vector3(0, 1.2f, 1.02f), rot, new Vector3(1.32f, 0.12f, 0.06f), Mats.LitMetal, SurfaceType.Metal, paint * 0.8f, 1f, false);
            for (int i = -1; i <= 1; i += 2)
                for (int k = -1; k <= 1; k += 2)
                    m.Box("Dumpster Wheel", basePos + rot * new Vector3(i * 0.5f, 0.05f, k * 0.8f), rot, new Vector3(0.1f, 0.1f, 0.1f), Mats.Lit, SurfaceType.Metal, new Color(0.05f, 0.05f, 0.05f), 1f, false);
        }

        static readonly Mesh[] bagMeshes = new Mesh[2];

        static void TrashBag(MapBuilder m, Vector3 pos, System.Random rnd)
        {
            int kind = rnd.NextDouble() < 0.7 ? 0 : 1;
            if (bagMeshes[kind] == null)
            {
                var mb = new MeshBuilder { Color = kind == 0 ? new Color(0.07f, 0.07f, 0.08f) : new Color(0.1f, 0.18f, 0.1f), Gloss = 0.7f };
                mb.Sphere(new Vector3(0, 0.26f, 0), new Vector3(0.32f, 0.27f, 0.28f), 7, 10, v => v + new Vector3(Mathf.Sin(v.y * 19f) * 0.02f, 0, Mathf.Cos(v.x * 23f) * 0.02f));
                mb.Cylinder(new Vector3(0, 0.5f, 0), new Vector3(0, 0.62f, 0), 0.06f, 0.025f, 6);
                bagMeshes[kind] = mb.ToMesh("TrashBag");
            }
            var go = new GameObject("Trash Bag");
            go.transform.SetParent(m.Root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 14f - 7f));
            go.transform.localScale = Vector3.one * (0.8f + (float)rnd.NextDouble() * 0.45f);
            go.AddComponent<MeshFilter>().sharedMesh = bagMeshes[kind];
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
        }

        // ------------------------------------------------------------------ graffiti

        static Material[] graffitiMats = new Material[5];

        static void Graffiti(MapBuilder m, Vector3 center, float yaw, float w, float h, int seed)
        {
            if (graffitiMats[seed] == null)
            {
                var tex = GraffitiTex(seed);
                var shader = Mats.Find("SolarEclipse/Decal", "Sprites/Default");
                graffitiMats[seed] = new Material(shader) { name = "Graffiti" + seed, mainTexture = tex, renderQueue = 2500 };
            }
            var go = new GameObject("Graffiti");
            go.transform.SetParent(m.Root, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0, yaw, 0));
            var mb = new MeshBuilder { Color = Color.white };
            mb.Face(Vector3.zero, Vector3.back, Vector3.up, w * 0.5f, h * 0.5f);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("GraffitiQuad");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = graffitiMats[seed];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Texture2D GraffitiTex(int seed)
        {
            const int W = 256, H = 128;
            var c = new PixelCanvas(W, H, new Color(0, 0, 0, 0));
            var rnd = new System.Random(seed * 991);
            Color[] palette = { new Color(0.95f, 0.2f, 0.5f), new Color(0.2f, 0.8f, 1f), new Color(1f, 0.85f, 0.2f), new Color(0.4f, 1f, 0.4f), new Color(1f, 0.45f, 0.1f), new Color(0.75f, 0.4f, 1f) };
            var fill = palette[rnd.Next(palette.Length)];
            var fill2 = palette[rnd.Next(palette.Length)];
            var outline = new Color(0.05f, 0.05f, 0.06f, 1f);
            // Bubble "letters": 3-5 blobby strokes with a thick dark outline, a highlight and drips.
            int letters = 3 + rnd.Next(3);
            float lw = (W - 40f) / letters;
            for (int pass = 0; pass < 2; pass++)
            {
                var r2 = new System.Random(seed * 131);
                for (int i = 0; i < letters; i++)
                {
                    float cx = 20 + lw * (i + 0.5f), cy = H * 0.5f + (float)(r2.NextDouble() * 16 - 8);
                    int strokes = 2 + r2.Next(3);
                    for (int s = 0; s < strokes; s++)
                    {
                        float ax = cx + (float)(r2.NextDouble() - 0.5) * lw * 0.7f, ay = cy + (float)(r2.NextDouble() - 0.5) * 70f;
                        float bx = cx + (float)(r2.NextDouble() - 0.5) * lw * 0.7f, by = cy + (float)(r2.NextDouble() - 0.5) * 70f;
                        float width = pass == 0 ? 24f : 14f;
                        var col = pass == 0 ? outline : Color.Lerp(fill, fill2, i / (float)Mathf.Max(1, letters - 1));
                        c.Line(ax, ay, bx, by, width, col, 1.5f);
                    }
                    if (pass == 1)
                    {
                        c.Circle(cx - lw * 0.15f, cy + 18f, 3f, new Color(1f, 1f, 1f, 0.85f), 1f);
                        if (r2.NextDouble() < 0.6) c.Line(cx, cy - 20f, cx, cy - 20f - 10f - (float)r2.NextDouble() * 25f, 3f, fill, 1f);
                    }
                }
            }
            // Spray haze: soften alpha edges a little.
            for (int i = 0; i < c.Px.Length; i++) { var p = c.Px[i]; p.a *= 0.92f; c.Px[i] = p; }
            return c.ToTexture("Graffiti" + seed, true, TextureWrapMode.Clamp);
        }

        // ------------------------------------------------------------------ thumbnail

        static Texture2D Thumb()
        {
            const int W = 384, H = 216;
            var c = new PixelCanvas(W, H, Color.black);
            for (int y = 0; y < H; y++)
            {
                var col = Color.Lerp(new Color(0.12f, 0.08f, 0.1f), new Color(0.03f, 0.03f, 0.09f), y / (float)H);
                for (int x = 0; x < W; x++) c.Set(x, y, col);
            }
            // Perspective alley: two brick walls converging, wet floor, a lamp.
            var wallL = new Color(0.32f, 0.17f, 0.13f);
            var wallR = new Color(0.26f, 0.15f, 0.13f);
            c.Polygon(new[] { new Vector2(0, 0), new Vector2(150, 70), new Vector2(150, 200), new Vector2(0, H) }, wallL);
            c.Polygon(new[] { new Vector2(W, 0), new Vector2(234, 70), new Vector2(234, 200), new Vector2(W, H) }, wallR);
            c.Polygon(new[] { new Vector2(0, 0), new Vector2(W, 0), new Vector2(234, 70), new Vector2(150, 70) }, new Color(0.13f, 0.13f, 0.15f));
            c.Rect(150, 70, 84, 110, new Color(0.08f, 0.06f, 0.08f));
            for (int i = 0; i < 9; i++) { float y = 20 + i * 20; c.Line(0, y, 150, 70 + (y - 0) * 0.62f, 1.2f, new Color(0, 0, 0, 0.35f), 1f); }
            c.Circle(60, 120, 10, new Color(1f, 0.75f, 0.45f), 8);
            c.Circle(60, 120, 40, new Color(1f, 0.7f, 0.4f, 0.25f), 30);
            c.Rect(160, 26, 70, 6, new Color(0.35f, 0.4f, 0.55f, 0.6f)); // puddle
            // Figure at the end of the alley.
            var sil = new Color(0.02f, 0.02f, 0.03f);
            c.Rect(183, 76, 18, 46, sil);
            c.Circle(192, 128, 8, sil, 1);
            // Graffiti dabs and the dumpster.
            c.Line(20, 90, 60, 105, 6, new Color(0.95f, 0.2f, 0.5f), 1.5f);
            c.Line(30, 80, 70, 95, 5, new Color(0.2f, 0.8f, 1f), 1.5f);
            c.Rect(255, 18, 70, 34, new Color(0.12f, 0.26f, 0.16f));
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float vx = (x - W / 2f) / (W / 2f), vy = (y - H / 2f) / (H / 2f);
                    float v = Mathf.Clamp01(1f - (vx * vx + vy * vy) * 0.4f);
                    var p = c.Px[y * W + x]; p.r *= v; p.g *= v; p.b *= v; c.Px[y * W + x] = p;
                }
            return c.ToTexture("Thumb_" + MapId);
        }
    }
}
