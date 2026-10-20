using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>All maps. Each Build method creates geometry, interactables, props and spawn points.</summary>
    public static class MapCatalog
    {
        static List<MapDef> all;

        public static List<MapDef> All
        {
            get
            {
                if (all != null) return all;
                all = new List<MapDef>
                {
                    new MapDef
                    {
                        Id = "flatgrass", Name = "Flatgrass Lab", Size = "Large · outdoor", Glyph = "grid", DefaultTime = TimeOfDay.Day,
                        Description = "The testing ground. Ramps, stairs, a 10 m tower with a diving board, a crawl tunnel, a shooting range and a playground of deadly machines.",
                        Features = new[] { "Ramps & stairs", "Tower + diving board", "Blade fan", "Hydraulic press", "Spike pit", "Jump pads", "Shooting range", "Explosives" },
                        ThemeTop = new Color(0.25f, 0.5f, 0.9f), ThemeBottom = new Color(0.4f, 0.65f, 0.3f), Build = FlatgrassLab,
                    },
                    new MapDef
                    {
                        Id = "arena", Name = "Crimson Arena", Size = "Medium · walled", Glyph = "skull", DefaultTime = TimeOfDay.BloodMoon, Ambience = "amb_drone",
                        Description = "A blood-red arena under a blood moon. Street lamps, cages, a meat grinder and a spike pit in the middle of the sand.",
                        Features = new[] { "Blood moon", "Meat grinder", "Spike pit", "Cages", "Bleachers", "Lamps you can shoot out" },
                        ThemeTop = new Color(0.25f, 0.0f, 0.03f), ThemeBottom = new Color(0.6f, 0.1f, 0.08f), Build = CrimsonArena,
                    },
                    new MapDef
                    {
                        Id = "downtown", Name = "Downtown", Size = "Large · urban", Glyph = "home", DefaultTime = TimeOfDay.Night, Ambience = "amb_city",
                        Description = "A city block at night. Streets, alleys, parked cars, a bus stop, vending machines and a rooftop you can reach by the fire stairs.",
                        Features = new[] { "Night city", "Rooftop access", "Cars", "Bus stop glass", "Vending machines", "Street lights" },
                        ThemeTop = new Color(0.02f, 0.03f, 0.08f), ThemeBottom = new Color(0.25f, 0.2f, 0.25f), Build = Downtown,
                    },
                    new MapDef
                    {
                        Id = "warehouse", Name = "Warehouse 13", Size = "Medium · indoor", Glyph = "box", DefaultTime = TimeOfDay.Day, Indoor = true, Ambience = "amb_hum",
                        Description = "An indoor storage hall. Shelving, catwalks, an office with a door and windows, a hydraulic press and plenty of crates to break.",
                        Features = new[] { "Indoor", "Catwalks", "Office", "Shelving", "Hydraulic press", "Doors" },
                        ThemeTop = new Color(0.3f, 0.32f, 0.35f), ThemeBottom = new Color(0.55f, 0.5f, 0.42f), Build = Warehouse,
                    },
                    BackroomsMap.CreateDef(), // procedurally generated (World/BackroomsMap.cs)
                    ArenaMap.CreateDef(), // endless horde mode (World/ArenaMap.cs + World/HordeMode.cs)
                    BossRealm.CreateDef(), // "????" boss fight (singleplayer only, gated by Orbie in MapSelectScreen)
                };
                return all;
            }
        }

        public static MapDef Get(string id) => All.Find(m => m.Id == id) ?? All[0];

        static Material M(Texture2D t) => MapBuilder.Tex(t);

        static void Ring(MapBuilder m, float minR, float maxR, int count, int seed, bool pine = true)
        {
            var state = Random.state;
            Random.InitState(seed);
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float d = Random.Range(minR, maxR);
                var p = new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                if (Random.value < 0.85f) m.Tree(p, Random.Range(0.8f, 1.5f), pine || Random.value < 0.5f);
                else m.Rock(p, Random.Range(0.6f, 1.8f));
            }
            Random.state = state;
        }

        static void Spawn(string id, Vector3 p, float yaw = 0) => PropFactory.Spawn(id, p, Quaternion.Euler(0, yaw, 0));

        // ================================================================== Flatgrass Lab

        static void FlatgrassLab(MapBuilder m)
        {
            m.Block("Ground", new Vector3(-100, -1, -100), new Vector3(100, 0, 100), M(TextureFactory.Grass), SurfaceType.Grass, null, 0.25f);
            m.Block("Plaza", new Vector3(-16, 0, -16), new Vector3(16, 0.02f, 16), M(TextureFactory.Concrete), SurfaceType.Concrete, null, 0.5f, false);
            m.PlayerSpawn = new Vector3(0, 0.1f, -10); m.PlayerYaw = 0;

            var wood = M(TextureFactory.Wood);
            var stone = M(TextureFactory.Concrete);
            m.Ramp("Gentle ramp", new Vector3(10.5f, 0, -4), 0, 3, 2.4f, 15, wood);
            m.Block("Gentle ramp top", new Vector3(9, 0, 4.96f), new Vector3(12, 2.4f, 8.96f), stone);
            m.Ramp("Steep ramp", new Vector3(14.5f, 0, -4), 0, 3, 2.4f, 30, wood);
            m.Block("Steep ramp top", new Vector3(13, 0, 0.15f), new Vector3(16, 2.4f, 4.15f), stone);
            m.Ramp("Too steep ramp", new Vector3(18.5f, 0, -4), 0, 3, 2.4f, 55, M(TextureFactory.Metal), SurfaceType.Metal);
            m.Block("Too steep top", new Vector3(17, 0, -2.32f), new Vector3(20, 2.4f, 1.68f), stone);
            m.Stairs("Stairs", new Vector3(-10.5f, 0, -4), 0, 3, 12, 0.2f, 0.35f, stone);
            m.Block("Stairs top", new Vector3(-12, 0, 0.2f), new Vector3(-9, 2.4f, 4.2f), stone);
            m.Block("Tunnel wall L", new Vector3(-10.4f, 0, 9), new Vector3(-10f, 1.3f, 15), M(TextureFactory.Brick), SurfaceType.Concrete);
            m.Block("Tunnel wall R", new Vector3(-8.4f, 0, 9), new Vector3(-8f, 1.3f, 15), M(TextureFactory.Brick), SurfaceType.Concrete);
            m.Block("Tunnel roof", new Vector3(-10.4f, 1.3f, 9), new Vector3(-8f, 1.6f, 15), stone);
            m.Block("Low wall", new Vector3(-5, 0, 8), new Vector3(-1.5f, 1f, 8.4f), M(TextureFactory.Brick));
            m.Block("High wall", new Vector3(1.5f, 0, 8), new Vector3(5, 2.5f, 8.4f), M(TextureFactory.Brick));
            m.Block("Ledge", new Vector3(-5, 0, 11), new Vector3(-2, 1.2f, 13), stone);
            // Tower with diving board
            m.Block("Tower", new Vector3(-3, 0, 22), new Vector3(3, 10, 28), M(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.7f, 0.74f, 0.8f));
            float run = 10f / Mathf.Tan(20f * Mathf.Deg2Rad);
            m.Ramp("Tower ramp", new Vector3(3 + run, 0, 25), -90, 3, 10, 20, wood);
            m.Block("Diving board", new Vector3(-0.6f, 9.75f, 28), new Vector3(0.6f, 10f, 31.5f), M(TextureFactory.Metal), SurfaceType.Metal, new Color(0.85f, 0.3f, 0.25f));
            m.Fence(new Vector3(-3, 10, 22), new Vector3(3, 10, 22));
            m.NpcSpawns.Add(new Vector3(0, 10.1f, 25));
            // Shooting range
            m.Block("Range bench", new Vector3(-22, 0, -6), new Vector3(-14, 1f, -5.4f), wood, SurfaceType.Wood);
            for (int i = 0; i < 5; i++) ShootingTarget.Create(m.Root, new Vector3(-21 + i * 1.6f, 0, -22 - i * 2.5f), 0);
            m.Block("Range backstop", new Vector3(-25, 0, -36), new Vector3(-12, 5, -35), M(TextureFactory.Concrete));
            // Machines of doom
            BladeFan.Create(m.Root, new Vector3(24, 2.6f, 12), Quaternion.Euler(0, -90, 0));
            HydraulicPress.Create(m.Root, new Vector3(24, 0, 20));
            SpikeTrap.Create(m.Root, new Vector3(24, 0, 28), new Vector2(4, 4));
            m.Block("Spike drop", new Vector3(21.5f, 0, 31), new Vector3(26.5f, 6f, 33), stone);
            m.Stairs("Spike stairs", new Vector3(24, 0, 41.4f), 180, 2.2f, 15, 0.4f, 0.55f, stone);
            JumpPad.Create(m.Root, new Vector3(6, 0.02f, -6));
            JumpPad.Create(m.Root, new Vector3(-6, 0.02f, -6), 22f);
            PushButton.Create(m.Root, new Vector3(2.5f, 0, -12), Quaternion.identity, "Spawn a Goredoll", () => GameManager.I.SpawnGoredollAt(new Vector3(0, 0.1f, -2)));
            PushButton.Create(m.Root, new Vector3(4f, 0, -12), Quaternion.identity, "Drop explosive barrels", () => { for (int i = 0; i < 3; i++) Spawn("explosive_barrel", new Vector3(Random.Range(-3, 3), 8 + i, Random.Range(2, 5))); });
            PushButton.Create(m.Root, new Vector3(5.5f, 0, -12), Quaternion.identity, "Rain watermelons", () => { for (int i = 0; i < 8; i++) Spawn("watermelon", new Vector3(Random.Range(-4, 4), 10 + i * 0.5f, Random.Range(-1, 5))); });
            Bell.Create(m.Root, new Vector3(-14, 0, 14));
            TVScreen.Create(m.Root, new Vector3(-6, 1.2f, 12), 180);
            Radio.Create(m.Root, new Vector3(-3.5f, 1.2f, 12), 180);
            VendingMachine.Create(m.Root, new Vector3(9, 0, -12), 0, new Color(0.75f, 0.1f, 0.1f));
            RubberDuck.Create(m.Root, new Vector3(-9.2f, 0.02f, 12f), "flatgrass_duck");
            // Props
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 3 - row; i++)
                    Spawn("crate", new Vector3(5 + (i + row * 0.5f) * 0.82f, 0.41f + row * 0.8f, 2));
            Spawn("barrel", new Vector3(-4, 0.46f, 2)); Spawn("explosive_barrel", new Vector3(-4.7f, 0.46f, 2.6f)); Spawn("barrel", new Vector3(-3.3f, 0.46f, 2.8f));
            Spawn("propane", new Vector3(-6, 0.4f, 3));
            for (int i = 0; i < 10; i++) Spawn("bowling", new Vector3(-12 + (i % 4) * 0.3f - (i / 4) * 0.15f, 0.02f, -14 - (i / 4) * 0.3f));
            Spawn("ball", new Vector3(2, 0.4f, -4)); Spawn("watermelon", new Vector3(0, 1.3f, 8.2f)); Spawn("tire", new Vector3(8, 0.5f, -2)); Spawn("table", new Vector3(-5, 0, 12), 180); Spawn("chair", new Vector3(-5, 0, 11));
            m.NpcSpawns.Add(new Vector3(0, 0.1f, -4)); m.NpcSpawns.Add(new Vector3(-6, 0.1f, 6)); m.NpcSpawns.Add(new Vector3(8, 0.1f, 6)); m.NpcSpawns.Add(new Vector3(-12, 2.5f, 2));
            Ring(m, 40, 85, 110, 11);
            m.Boundaries(new Bounds(new Vector3(0, 30, 0), new Vector3(180, 80, 180)));
        }

        // ================================================================== Crimson Arena

        static void CrimsonArena(MapBuilder m)
        {
            var sand = M(TextureFactory.Sand);
            var brick = M(TextureFactory.Brick);
            var stone = M(TextureFactory.Concrete);
            m.Block("Ground", new Vector3(-70, -1, -70), new Vector3(70, 0, 70), M(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.6f, 0.45f, 0.4f), 0.25f);
            m.Block("Arena floor", new Vector3(-30, 0, -30), new Vector3(30, 0.05f, 30), sand, SurfaceType.Dirt, new Color(0.85f, 0.65f, 0.6f), 0.3f);
            m.PlayerSpawn = new Vector3(0, 0.2f, -22); m.PlayerYaw = 0;
            // Walls with bleachers
            float h = 6f;
            m.Block("Wall N", new Vector3(-31, 0, 30), new Vector3(31, h, 31), brick, SurfaceType.Concrete, new Color(0.75f, 0.55f, 0.5f), 0.3f);
            m.Block("Wall S", new Vector3(-31, 0, -31), new Vector3(31, h, -30), brick, SurfaceType.Concrete, new Color(0.75f, 0.55f, 0.5f), 0.3f);
            m.Block("Wall E", new Vector3(30, 0, -30), new Vector3(31, h, 30), brick, SurfaceType.Concrete, new Color(0.75f, 0.55f, 0.5f), 0.3f);
            m.Block("Wall W", new Vector3(-31, 0, -30), new Vector3(-30, h, 30), brick, SurfaceType.Concrete, new Color(0.75f, 0.55f, 0.5f), 0.3f);
            for (int i = 0; i < 6; i++)
            {
                m.Block("Bleacher", new Vector3(-24, h + i * 0.6f, 31 + i * 1f), new Vector3(24, h + (i + 1) * 0.6f, 32 + i * 1f), stone, SurfaceType.Concrete, new Color(0.45f, 0.42f, 0.42f));
                m.Block("Bleacher", new Vector3(-24, h + i * 0.6f, -32 - i * 1f), new Vector3(24, h + (i + 1) * 0.6f, -31 - i * 1f), stone, SurfaceType.Concrete, new Color(0.45f, 0.42f, 0.42f));
            }
            m.Stairs("Gate stairs", new Vector3(-27, 0, -14), 0, 2.5f, 15, 0.4f, 0.5f, stone, SurfaceType.Concrete, new Color(0.6f, 0.5f, 0.48f));
            // Centre: raised stage with a spike pit
            m.Block("Stage", new Vector3(-6, 0, -6), new Vector3(6, 1.2f, 6), stone, SurfaceType.Concrete, new Color(0.5f, 0.3f, 0.3f));
            m.Ramp("Stage ramp", new Vector3(0, 0, -10.5f), 0, 3, 1.2f, 16, stone);
            SpikeTrap.Create(m.Root, new Vector3(0, 0, 14), new Vector2(6, 4));
            m.Block("Pit rim N", new Vector3(-3.5f, 0, 16), new Vector3(3.5f, 0.6f, 16.5f), stone);
            BladeFan.Create(m.Root, new Vector3(-26, 2.6f, 10), Quaternion.Euler(0, 90, 0), 1.6f);
            HydraulicPress.Create(m.Root, new Vector3(22, 0, 18), 5f);
            // Cages
            for (int c = 0; c < 3; c++)
            {
                var cpos = new Vector3(-18 + c * 8, 0, 22);
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * Mathf.PI * 2f;
                    m.Cylinder("Cage bar", cpos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.3f, 0.04f, 3f, Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.3f), 6);
                }
                m.Cylinder("Cage top", cpos + Vector3.up * 3f, 1.4f, 0.1f, Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.3f), 16);
                m.NpcSpawns.Add(cpos + Vector3.up * 0.1f);
            }
            // Red lamps like the menu backdrop
            foreach (var p in new[] { new Vector3(-28, 0, -28), new Vector3(28, 0, -28), new Vector3(-28, 0, 28), new Vector3(28, 0, 28), new Vector3(0, 0, -28), new Vector3(0, 0, 28) })
                m.StreetLamp(p, Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg, new Color(1f, 0.55f, 0.45f), 7f, 22f, 2.6f);
            for (int i = 0; i < 6; i++) Spawn(i % 2 == 0 ? "crate" : "explosive_barrel", new Vector3(Random.Range(-20, 20), 0.5f, Random.Range(-20, 10)));
            Spawn("watermelon", new Vector3(0, 1.5f, 0)); Spawn("dumbbell", new Vector3(2, 1.4f, 1));
            RubberDuck.Create(m.Root, new Vector3(20, h + 3.6f + 0.02f, 36.5f), "arena_duck");
            m.NpcSpawns.Add(new Vector3(0, 1.3f, 0)); m.NpcSpawns.Add(new Vector3(-10, 0.1f, -10)); m.NpcSpawns.Add(new Vector3(10, 0.1f, -8));
            Ring(m, 45, 65, 40, 22, false);
            m.Boundaries(new Bounds(new Vector3(0, 25, 0), new Vector3(110, 60, 110)));
        }

        // ================================================================== Downtown

        static void Downtown(MapBuilder m)
        {
            var asphalt = M(TextureFactory.Asphalt);
            var walk = M(TextureFactory.Tiles);
            m.Block("Ground", new Vector3(-90, -1, -90), new Vector3(90, 0, 90), asphalt, SurfaceType.Concrete, null, 0.2f);
            m.PlayerSpawn = new Vector3(2, 0.2f, -30); m.PlayerYaw = 0;
            // Road markings
            for (float z = -80; z < 80; z += 6) m.Block("Lane mark", new Vector3(-0.1f, 0, z), new Vector3(0.1f, 0.01f, z + 3), Mats.Glow(new Color(0.9f, 0.85f, 0.6f), 0.2f), SurfaceType.Concrete, null, 1, false);
            for (float x = -80; x < 80; x += 6) m.Block("Lane mark", new Vector3(x, 0, -0.1f), new Vector3(x + 3, 0.01f, 0.1f), Mats.Glow(new Color(0.9f, 0.85f, 0.6f), 0.2f), SurfaceType.Concrete, null, 1, false);
            // Four blocks of buildings with sidewalks
            var rnd = new System.Random(5);
            Texture2D[] walls = { TextureFactory.Brick, TextureFactory.Plaster, TextureFactory.Concrete };
            for (int qx = -1; qx <= 1; qx += 2)
                for (int qz = -1; qz <= 1; qz += 2)
                {
                    var min = new Vector3(qx < 0 ? -70 : 7, 0, qz < 0 ? -70 : 7);
                    var max = new Vector3(qx < 0 ? -7 : 70, 0.18f, qz < 0 ? -7 : 70);
                    m.Block("Sidewalk", min, max, walk, SurfaceType.Concrete, new Color(0.75f, 0.75f, 0.75f), 0.5f);
                    for (int b = 0; b < 3; b++)
                    {
                        float bx = qx < 0 ? -12 - b * 19 : 12 + b * 19;
                        float bz = qz < 0 ? -14 : 14;
                        float hgt = 9 + (float)rnd.NextDouble() * 16;
                        var bmin = new Vector3(Mathf.Min(bx, bx + qx * 15), 0.18f, Mathf.Min(bz, bz + qz * 22));
                        var bmax = new Vector3(Mathf.Max(bx, bx + qx * 15), hgt, Mathf.Max(bz, bz + qz * 22));
                        if (qx > 0 && qz < 0 && b == 0) continue; // empty lot with the fire stairs building next to it
                        m.Building(bmin, bmax, walls[rnd.Next(walls.Length)], Color.Lerp(new Color(0.55f, 0.5f, 0.48f), new Color(0.85f, 0.8f, 0.72f), (float)rnd.NextDouble()), true);
                    }
                }
            // Rooftop access: fire stairs up the side of a building
            var roofMin = new Vector3(31, 0.18f, -36); var roofMax = new Vector3(46, 12f, -14);
            m.Building(roofMin, roofMax, TextureFactory.Brick, new Color(0.7f, 0.5f, 0.45f), true);
            for (int f = 0; f < 3; f++)
            {
                float y0 = 0.18f + f * 4f;
                m.Stairs("Fire stairs", new Vector3(29.6f, y0, -34 + (f % 2) * 0f), 0, 1.6f, 10, 0.4f, 0.5f, Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f));
                m.Block("Fire landing", new Vector3(28.8f, y0 + 3.9f, -29), new Vector3(30.8f, y0 + 4f, -27), Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.3f, 0.32f));
            }
            m.NpcSpawns.Add(new Vector3(38, 12.1f, -25));
            // Street furniture
            float[] lampsZ = { -60, -40, -20, 20, 40, 60 };
            foreach (var z in lampsZ) { m.StreetLamp(new Vector3(-6.2f, 0.18f, z), 90, new Color(1f, 0.82f, 0.55f)); m.StreetLamp(new Vector3(6.2f, 0.18f, z + 10), -90, new Color(1f, 0.82f, 0.55f)); }
            foreach (var x in lampsZ) m.StreetLamp(new Vector3(x, 0.18f, -6.2f), 0, new Color(1f, 0.82f, 0.55f));
            var paints = new[] { new Color(0.6f, 0.05f, 0.05f), new Color(0.1f, 0.2f, 0.5f), new Color(0.85f, 0.85f, 0.85f), new Color(0.12f, 0.12f, 0.12f), new Color(0.75f, 0.6f, 0.1f) };
            for (int i = 0; i < 8; i++) m.Car(new Vector3(i % 2 == 0 ? -4.2f : 4.2f, 0, -60 + i * 15), i % 2 == 0 ? 0 : 180, paints[i % paints.Length]);
            m.Car(new Vector3(-30, 0, 4.2f), 90, paints[2]); m.Car(new Vector3(25, 0, -4.2f), -90, paints[0]);
            // Bus stop with glass panes
            m.Block("Bus stop roof", new Vector3(-12, 3f, -9.2f), new Vector3(-6.8f, 3.15f, -7.6f), Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.3f, 0.35f));
            for (int i = 0; i < 3; i++)
            {
                var pane = m.Box("Bus stop glass", new Vector3(-11 + i * 1.8f, 1.6f, -9.1f), Quaternion.identity, new Vector3(1.7f, 2.6f, 0.04f), Mats.Glass, SurfaceType.Glass, Color.white, 1, true, false);
                var br = pane.AddComponent<BreakableProp>(); br.Health = 8; br.Material = SurfaceType.Glass;
            }
            VendingMachine.Create(m.Root, new Vector3(-13, 0.18f, 8), 90, new Color(0.1f, 0.35f, 0.75f));
            VendingMachine.Create(m.Root, new Vector3(-13, 0.18f, 9.2f), 90, new Color(0.85f, 0.15f, 0.1f));
            for (int i = 0; i < 6; i++) Spawn("cone", new Vector3(-2 + i * 0.8f, 0.05f, 12));
            for (int i = 0; i < 5; i++) Spawn(Rng.Pick(new[] { "crate", "barrel", "pallet", "explosive_barrel" }), new Vector3(Random.Range(8, 20), 0.5f, Random.Range(-8, -12)));
            HingeDoor.Create(m.Root, new Vector3(-12.5f, 0.18f, 13.9f), 0);
            RubberDuck.Create(m.Root, new Vector3(44, 12.02f, -15), "downtown_duck");
            m.NpcSpawns.Add(new Vector3(0, 0.2f, -10)); m.NpcSpawns.Add(new Vector3(-9, 0.3f, 10)); m.NpcSpawns.Add(new Vector3(9, 0.3f, 30)); m.NpcSpawns.Add(new Vector3(-30, 0.2f, 2));
            m.Boundaries(new Bounds(new Vector3(0, 30, 0), new Vector3(150, 80, 150)));
        }

        // ================================================================== Warehouse 13

        static void Warehouse(MapBuilder m)
        {
            var metal = M(TextureFactory.Metal);
            var floor = M(TextureFactory.Concrete);
            float W = 30, L = 50, H = 10;
            m.Block("Floor", new Vector3(-W, -1, -L), new Vector3(W, 0, L), floor, SurfaceType.Concrete, new Color(0.75f, 0.73f, 0.7f), 0.25f);
            m.Block("Wall N", new Vector3(-W, 0, L), new Vector3(W, H, L + 0.5f), metal, SurfaceType.Metal, new Color(0.55f, 0.6f, 0.65f), 0.3f);
            m.Block("Wall S", new Vector3(-W, 0, -L - 0.5f), new Vector3(W, H, -L), metal, SurfaceType.Metal, new Color(0.55f, 0.6f, 0.65f), 0.3f);
            m.Block("Wall E", new Vector3(W, 0, -L), new Vector3(W + 0.5f, H, L), metal, SurfaceType.Metal, new Color(0.55f, 0.6f, 0.65f), 0.3f);
            m.Block("Wall W", new Vector3(-W - 0.5f, 0, -L), new Vector3(-W, H, L), metal, SurfaceType.Metal, new Color(0.55f, 0.6f, 0.65f), 0.3f);
            m.Block("Roof", new Vector3(-W - 0.5f, H, -L - 0.5f), new Vector3(W + 0.5f, H + 0.4f, L + 0.5f), metal, SurfaceType.Metal, new Color(0.4f, 0.42f, 0.45f), 0.3f);
            m.PlayerSpawn = new Vector3(0, 0.1f, -44); m.PlayerYaw = 0;
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 3; j++)
                    m.CeilingLight(new Vector3(-20 + j * 20, H - 0.3f, -40 + i * 16), new Color(1f, 0.97f, 0.9f), 22f, 1.8f, new Vector3(2.5f, 0.1f, 0.4f));
            // Shelving rows
            for (int row = 0; row < 4; row++)
            {
                float x = -22 + row * 9;
                for (int lvl = 0; lvl < 3; lvl++)
                    m.Block("Shelf", new Vector3(x - 1, 0.1f + lvl * 2f, -20), new Vector3(x + 1, 0.2f + lvl * 2f, 20), Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.35f, 0.65f));
                for (int p = -20; p <= 20; p += 5)
                {
                    m.Block("Shelf post", new Vector3(x - 1, 0, p - 0.05f), new Vector3(x - 0.9f, 6.2f, p + 0.05f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.5f, 0.1f));
                    m.Block("Shelf post", new Vector3(x + 0.9f, 0, p - 0.05f), new Vector3(x + 1, 6.2f, p + 0.05f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.5f, 0.1f));
                }
                for (int k = 0; k < 9; k++) Spawn(Rng.Pick(new[] { "crate", "small_crate", "barrel", "pallet" }), new Vector3(x, 0.3f + (k % 3) * 2f + 0.5f, -18 + k * 4.2f));
            }
            // Catwalk along the east wall with stairs
            m.Block("Catwalk", new Vector3(24, 4, -40), new Vector3(29.5f, 4.2f, 40), metal, SurfaceType.Metal, new Color(0.5f, 0.5f, 0.52f));
            m.Fence(new Vector3(24, 4.2f, -40), new Vector3(24, 4.2f, 40));
            m.Stairs("Catwalk stairs", new Vector3(26.8f, 0, -50f + 1.5f), 0, 2.2f, 10, 0.4f, 0.5f, metal, SurfaceType.Metal, new Color(0.5f, 0.5f, 0.52f));
            m.NpcSpawns.Add(new Vector3(27, 4.3f, 0));
            // Office with door and windows
            var plaster = M(TextureFactory.Plaster);
            m.Block("Office wall", new Vector3(-29, 0, 30), new Vector3(-12, 3.2f, 30.3f), plaster, SurfaceType.Concrete);
            m.Block("Office wall", new Vector3(-12.3f, 0, 30), new Vector3(-12, 3.2f, 38), plaster, SurfaceType.Concrete);
            m.Block("Office wall", new Vector3(-12.3f, 0, 39.2f), new Vector3(-12, 3.2f, 49), plaster, SurfaceType.Concrete);
            m.Block("Office ceiling", new Vector3(-30, 3.2f, 30), new Vector3(-12, 3.4f, 50), plaster, SurfaceType.Concrete);
            HingeDoor.Create(m.Root, new Vector3(-12.15f, 0, 38), 90, 1.15f, 2.2f);
            for (int i = 0; i < 3; i++)
            {
                var pane = m.Box("Office window", new Vector3(-26 + i * 4, 1.8f, 30.15f), Quaternion.identity, new Vector3(3f, 1.4f, 0.04f), Mats.Glass, SurfaceType.Glass, Color.white, 1, true, false);
                var br = pane.AddComponent<BreakableProp>(); br.Health = 6; br.Material = SurfaceType.Glass;
            }
            m.CeilingLight(new Vector3(-21, 3.1f, 40), new Color(1f, 0.95f, 0.85f), 12f, 1.4f);
            Spawn("table", new Vector3(-22, 0, 42)); Spawn("chair", new Vector3(-22, 0, 41)); Spawn("chair", new Vector3(-20.5f, 0, 43), 200);
            TVScreen.Create(m.Root, new Vector3(-22, 0.78f, 42.2f), 180);
            Radio.Create(m.Root, new Vector3(-21.2f, 0.78f, 41.8f), 160);
            RubberDuck.Create(m.Root, new Vector3(-28.5f, 0.02f, 49), "warehouse_duck");
            HydraulicPress.Create(m.Root, new Vector3(10, 0, 32), 5f);
            BladeFan.Create(m.Root, new Vector3(-29.4f, 3.5f, -30), Quaternion.Euler(0, 90, 0), 1.8f);
            for (int i = 0; i < 4; i++) Spawn("explosive_barrel", new Vector3(14 + i * 0.7f, 0.5f, -38));
            Spawn("propane", new Vector3(16, 0.4f, -36)); Spawn("mattress", new Vector3(20, 0.2f, 0)); Spawn("dumbbell", new Vector3(5, 0.2f, -30));
            m.NpcSpawns.Add(new Vector3(0, 0.1f, -30)); m.NpcSpawns.Add(new Vector3(-6, 0.1f, 0)); m.NpcSpawns.Add(new Vector3(12, 0.1f, 10)); m.NpcSpawns.Add(new Vector3(-20, 0.1f, 40));
            m.Boundaries(new Bounds(new Vector3(0, 6, 0), new Vector3(W * 2 + 1, 13, L * 2 + 1)));
        }
    }
}
