using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Gore Sandbox 2 maps, part 3: Suburban Street, Oil Rig, Shopping Mall, Space Station and Dream World.</summary>
    public static class Maps4
    {
        static Material T(Texture2D t) => MapBuilder.Tex(t);

        public static IEnumerable<MapDef> Defs()
        {
            yield return new MapDef { Id = "suburb", Name = "Suburban Street", Size = "Large - outdoor", Glyph = "home", DefaultTime = TimeOfDay.Day, Ambience = "amb_wind",
                Description = "A quiet cul-de-sac: houses with lawns and fences, parked cars, a mailbox on every drive. Nothing ever happens here. Until now.",
                Features = new[] { "8 houses", "Lawns and fences", "Parked cars", "Cul-de-sac" },
                ThemeTop = new Color(0.5f, 0.72f, 0.95f), ThemeBottom = new Color(0.4f, 0.6f, 0.3f), Accent = new Color(1f, 0.9f, 0.6f), Build = Suburb };
            yield return new MapDef { Id = "oilrig", Name = "Oil Rig", Size = "Medium - over the sea", Glyph = "anchor", DefaultTime = TimeOfDay.Sunset, Ambience = "amb_wind",
                Description = "A steel platform far out at sea on four huge legs: decks, a drilling tower, a helipad and a long way down to the water.",
                Features = new[] { "Three decks", "Drill tower", "Helipad", "Long drop into the sea" },
                ThemeTop = new Color(0.9f, 0.55f, 0.35f), ThemeBottom = new Color(0.1f, 0.18f, 0.25f), Accent = new Color(1f, 0.7f, 0.2f), Build = OilRig };
            yield return new MapDef { Id = "mall", Name = "Shopping Mall", Size = "Large - indoor", Glyph = "box", DefaultTime = TimeOfDay.Day, Indoor = true, Ambience = "amb_hum",
                Description = "A two-floor mall after closing: shop fronts, a food court, escalator ramps, a fountain and a glass roof.",
                Features = new[] { "Two floors", "Food court", "Shop fronts", "Fountain" },
                ThemeTop = new Color(0.85f, 0.88f, 0.92f), ThemeBottom = new Color(0.6f, 0.55f, 0.5f), Accent = new Color(1f, 0.6f, 0.8f), Build = Mall };
            yield return new MapDef { Id = "spacestation", Name = "Space Station", Size = "Medium - indoor", Glyph = "rocket", DefaultTime = TimeOfDay.Night, Indoor = true, Ambience = "amb_hum",
                Description = "An orbital station: white corridors, a control room, sleeping pods and an observation window looking down on the Earth.",
                Features = new[] { "Corridors", "Control room", "Observation deck", "Airlock" },
                ThemeTop = new Color(0.02f, 0.03f, 0.08f), ThemeBottom = new Color(0.8f, 0.82f, 0.86f), Accent = new Color(0.4f, 0.8f, 1f), Build = Station };
            yield return new MapDef { Id = "dream", Name = "Dream World", Size = "Medium - surreal", Glyph = "cloud", DefaultTime = TimeOfDay.Sunset, Ambience = "amb_drone",
                Description = "Floating islands in a pink sky, giant furniture, stairs to nowhere and doors standing on their own. None of it makes sense.",
                Features = new[] { "Floating islands", "Giant furniture", "Stairs to nowhere", "Pastel sky" },
                ThemeTop = new Color(0.95f, 0.6f, 0.8f), ThemeBottom = new Color(0.55f, 0.75f, 1f), Accent = new Color(1f, 0.85f, 1f), Build = Dream };
        }

        static void Suburb(MapBuilder m)
        {
            m.Block("Lawn", new Vector3(-70, -1, -70), new Vector3(70, 0, 70), T(TextureFactory.Grass), SurfaceType.Grass, new Color(0.4f, 0.62f, 0.3f), 0.1f);
            m.Block("Road", new Vector3(-5, 0, -70), new Vector3(5, 0.04f, 30), T(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.3f, 0.3f, 0.32f), 0.15f);
            m.Cylinder("Cul-de-sac", new Vector3(0, 0, 38), 14f, 0.04f, T(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.3f, 0.3f, 0.32f), 28);
            var walls = new[] { new Color(0.95f, 0.92f, 0.85f), new Color(0.75f, 0.85f, 0.95f), new Color(0.95f, 0.85f, 0.7f), new Color(0.8f, 0.9f, 0.78f) };
            for (int i = 0; i < 8; i++)
            {
                int side = i % 2 == 0 ? -1 : 1; float z = -50 + (i / 2) * 22f;
                var c = new Vector3(side * 20f, 0, z);
                m.Building(c - new Vector3(6, 0, 5), c + new Vector3(6, 6, 5), TextureFactory.Plaster, walls[i % 4], true);
                m.Fence(c + new Vector3(-side * 8f, 0, -9), c + new Vector3(-side * 8f, 0, 9), 1f);
                m.Box("Mailbox", new Vector3(side * 6.5f, 0.6f, z - 6f), Quaternion.identity, new Vector3(0.3f, 1.2f, 0.4f), Mats.LitMetal, SurfaceType.Metal, new Color(0.2f, 0.25f, 0.5f));
                if (i % 3 == 0) m.Car(new Vector3(side * 10f, 0, z), 0f, walls[(i + 1) % 4] * 0.7f);
            }
            for (int i = 0; i < 6; i++) m.StreetLamp(new Vector3(i % 2 == 0 ? -6 : 6, 0, -60 + i * 18), i % 2 == 0 ? 90f : -90f, new Color(1f, 0.85f, 0.6f), 5f, 18f, 2f);
            m.PlayerSpawn = new Vector3(0, 0.1f, -65f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-12, 0.1f, -40), new Vector3(12, 0.1f, -20), new Vector3(0, 0.1f, 38), new Vector3(-12, 0.1f, 10), new Vector3(12, 0.1f, 20) });
            m.Playable = new Bounds(new Vector3(0, 10, 0), new Vector3(136, 40, 136));
        }

        static void OilRig(MapBuilder m)
        {
            m.Block("Sea", new Vector3(-150, -32, -150), new Vector3(150, -30, 150), Mats.Glow(new Color(0.05f, 0.12f, 0.18f), 0.15f), SurfaceType.Water, null, 0.05f, false);
            m.Block("Seabed", new Vector3(-150, -45, -150), new Vector3(150, -44, 150), T(TextureFactory.Sand), SurfaceType.Dirt, new Color(0.3f, 0.3f, 0.28f));
            var steel = new Color(0.55f, 0.5f, 0.42f); var yellow = new Color(0.9f, 0.7f, 0.1f);
            for (int i = 0; i < 4; i++) m.Cylinder("Leg", new Vector3(i < 2 ? -18 : 18, -44, i % 2 == 0 ? -18 : 18), 2.2f, 44f, Mats.LitMetal, SurfaceType.Metal, steel, 16);
            float[] decks = { 0f, 6f, 12f };
            for (int d = 0; d < decks.Length; d++)
            {
                float y = decks[d], half = 22f - d * 4f;
                m.Block("Deck", new Vector3(-half, y - 0.4f, -half), new Vector3(half, y, half), T(TextureFactory.Metal), SurfaceType.Metal, steel);
                for (int s = 0; s < 4; s++)
                {
                    var dir = Quaternion.Euler(0, s * 90f, 0);
                    m.Box("Railing", dir * new Vector3(0, y + 0.55f, half), dir, new Vector3(half * 2f, 1.1f, 0.06f), Mats.LitMetal, SurfaceType.Metal, yellow);
                }
                if (d < decks.Length - 1) m.Stairs("Stairs", new Vector3(half - 6f, y, -half + 2f), 0f, 2f, 20, 0.3f, 0.5f, T(TextureFactory.Metal), SurfaceType.Metal, yellow);
            }
            for (int i = 0; i < 4; i++) m.Box("Derrick", new Vector3((i % 2) * 6 - 3, 22, (i / 2) * 6 - 3), Quaternion.identity, new Vector3(0.4f, 20f, 0.4f), Mats.LitMetal, SurfaceType.Metal, new Color(0.75f, 0.2f, 0.15f));
            m.Box("Derrick Top", new Vector3(0, 32, 0), Quaternion.identity, new Vector3(7f, 0.6f, 7f), Mats.LitMetal, SurfaceType.Metal, new Color(0.75f, 0.2f, 0.15f));
            m.Cylinder("Helipad", new Vector3(10, 12.02f, 8), 5f, 0.04f, Mats.Lit, SurfaceType.Concrete, new Color(0.15f, 0.35f, 0.2f), 24, false);
            for (int i = 0; i < 6; i++) PropFactory.Spawn(i % 2 == 0 ? "barrel" : "explosive_barrel", new Vector3(Random.Range(-15f, 15f), 0.6f, Random.Range(-15f, 15f)), Quaternion.identity);
            for (int i = 0; i < 4; i++) m.CeilingLight(new Vector3(i < 2 ? -12 : 12, 5.5f, i % 2 == 0 ? -12 : 12), new Color(1f, 0.85f, 0.6f), 16f, 1.8f);
            m.PlayerSpawn = new Vector3(-15, 0.1f, -15f);
            m.NpcSpawns.AddRange(new[] { new Vector3(5, 0.1f, 5), new Vector3(-10, 6.1f, 10), new Vector3(10, 6.1f, -10), new Vector3(0, 12.1f, -6), new Vector3(15, 0.1f, 15) });
            m.Playable = new Bounds(new Vector3(0, 0, 0), new Vector3(120, 100, 120));
            m.KillY = -40f;
        }

        static void Mall(MapBuilder m)
        {
            Maps2.Room(m, new Vector3(-30, 0, -20), new Vector3(30, 11, 20), TextureFactory.Tiles, new Color(0.85f, 0.84f, 0.8f));
            m.Block("Upper Floor L", new Vector3(-30, 5.4f, -20), new Vector3(-12, 5.8f, 20), T(TextureFactory.Tiles), SurfaceType.Concrete, new Color(0.8f, 0.78f, 0.75f));
            m.Block("Upper Floor R", new Vector3(12, 5.4f, -20), new Vector3(30, 5.8f, 20), T(TextureFactory.Tiles), SurfaceType.Concrete, new Color(0.8f, 0.78f, 0.75f));
            m.Ramp("Escalator", new Vector3(-10.8f, 0, -14), 0f, 2.4f, 5.8f, 28f, Mats.LitMetal, SurfaceType.Metal);
            m.Ramp("Escalator", new Vector3(10.8f, 0, 4), 180f, 2.4f, 5.8f, 28f, Mats.LitMetal, SurfaceType.Metal);
            var shop = new[] { new Color(0.9f, 0.3f, 0.35f), new Color(0.3f, 0.6f, 0.9f), new Color(0.95f, 0.75f, 0.2f), new Color(0.4f, 0.8f, 0.45f), new Color(0.75f, 0.4f, 0.9f) };
            for (int f = 0; f < 2; f++)
                for (int i = 0; i < 5; i++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float y = f * 5.8f, x = s * 26f, z = -16 + i * 8f;
                        m.Box("Shop Glass", new Vector3(s * 21.5f, y + 2.2f, z), Quaternion.identity, new Vector3(0.06f, 4f, 6f), Mats.Glass, SurfaceType.Glass, Color.white, 1f);
                        m.Box("Shop Sign", new Vector3(s * 21.4f, y + 4.6f, z), Quaternion.identity, new Vector3(0.1f, 0.7f, 5f), Mats.Glow(shop[(i + f) % shop.Length], 0.9f), SurfaceType.Metal, null, 1f, false);
                        m.Box("Shelf", new Vector3(x, y + 0.8f, z), Quaternion.identity, new Vector3(1f, 1.6f, 4f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.6f, 0.5f, 0.4f));
                    }
            m.Cylinder("Fountain", new Vector3(0, 0, 8), 3f, 0.7f, T(TextureFactory.Tiles), SurfaceType.Concrete, new Color(0.7f, 0.8f, 0.85f), 24);
            for (int i = 0; i < 6; i++) m.Box("Food Table", new Vector3(-6 + (i % 3) * 6, 0.4f, -6 + (i / 3) * 5), Quaternion.identity, new Vector3(1.6f, 0.8f, 1.6f), Mats.LitMetal, SurfaceType.Metal, new Color(0.85f, 0.85f, 0.88f));
            for (int i = 0; i < 6; i++) m.CeilingLight(new Vector3(-20 + i * 8, 10.7f, 0), new Color(1f, 0.97f, 0.9f), 20f, 1.6f, new Vector3(2f, 0.1f, 0.5f));
            m.PlayerSpawn = new Vector3(0, 0.1f, -18f);
            m.NpcSpawns.AddRange(new[] { new Vector3(-15, 0.1f, 0), new Vector3(15, 0.1f, 10), new Vector3(-20, 5.9f, 5), new Vector3(20, 5.9f, -5), new Vector3(0, 0.1f, 14) });
            m.Playable = new Bounds(new Vector3(0, 5, 0), new Vector3(60, 11, 40));
        }

        static void Station(MapBuilder m)
        {
            var white = new Color(0.88f, 0.9f, 0.93f);
            var ct = T(TextureFactory.Metal); // corridor, open at the north end into the hall
            m.Block("Corridor Floor", new Vector3(-4, -0.3f, -30), new Vector3(4, 0, 30), ct, SurfaceType.Metal, white * 0.8f);
            m.Block("Corridor Roof", new Vector3(-4, 3.4f, -30), new Vector3(4, 3.7f, 30), ct, SurfaceType.Metal, white * 0.6f);
            m.Block("Corridor Wall", new Vector3(-4.3f, 0, -30), new Vector3(-4, 3.4f, 30), ct, SurfaceType.Metal, white);
            m.Block("Corridor Wall", new Vector3(4, 0, -30), new Vector3(4.3f, 3.4f, 30), ct, SurfaceType.Metal, white);
            m.Block("Airlock Door", new Vector3(-4, 0, -30.3f), new Vector3(4, 3.4f, -30), ct, SurfaceType.Metal, new Color(0.6f, 0.62f, 0.66f));
            var mt = T(TextureFactory.Metal); var hw = white * 0.95f;
            m.Block("Hall Floor", new Vector3(-16, -0.3f, 30), new Vector3(16, 0, 44), mt, SurfaceType.Metal, hw * 0.8f);
            m.Block("Hall Roof", new Vector3(-16, 5, 30), new Vector3(16, 5.3f, 44), mt, SurfaceType.Metal, hw * 0.6f);
            m.Block("Hall Wall", new Vector3(-16.3f, 0, 30), new Vector3(-16, 5, 44), mt, SurfaceType.Metal, hw);
            m.Block("Hall Wall", new Vector3(16, 0, 30), new Vector3(16.3f, 5, 44), mt, SurfaceType.Metal, hw);
            m.Block("Hall Wall", new Vector3(-16, 0, 44), new Vector3(16, 5, 44.3f), mt, SurfaceType.Metal, hw);
            m.Block("Hall Wall", new Vector3(-16, 0, 29.7f), new Vector3(-4, 5, 30), mt, SurfaceType.Metal, hw);
            m.Block("Hall Wall", new Vector3(4, 0, 29.7f), new Vector3(16, 5, 30), mt, SurfaceType.Metal, hw);
            m.Block("Hall Wall", new Vector3(-4, 3.4f, 29.7f), new Vector3(4, 5, 30), mt, SurfaceType.Metal, hw);
            m.Box("Observation Window", new Vector3(0, 2.6f, 43.95f), Quaternion.identity, new Vector3(20f, 3f, 0.05f), Mats.Glow(new Color(0.05f, 0.1f, 0.25f), 0.5f), SurfaceType.Glass, null, 1f, false);
            m.Cylinder("Earth View", new Vector3(0, -6, 60), 14f, 0.1f, Mats.Glow(new Color(0.25f, 0.5f, 0.95f), 1f), SurfaceType.Glass, null, 32, false);
            for (int i = 0; i < 6; i++) m.Box("Console", new Vector3(-12 + i * 4.8f, 0.6f, 38f), Quaternion.identity, new Vector3(3f, 1.2f, 1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.27f, 0.32f));
            for (int i = 0; i < 6; i++) m.Box("Console Screen", new Vector3(-12 + i * 4.8f, 1.3f, 37.55f), Quaternion.Euler(-20, 0, 0), new Vector3(2.4f, 0.7f, 0.05f), Mats.Glow(new Color(0.3f, 0.9f, 1f), 0.9f), SurfaceType.Glass, null, 1f, false);
            for (int i = 0; i < 6; i++) m.Box("Sleep Pod", new Vector3(i % 2 == 0 ? -3.3f : 3.3f, 1f, -24 + (i / 2) * 5f), Quaternion.identity, new Vector3(1.2f, 0.9f, 2.4f), Mats.LitMetal, SurfaceType.Metal, white * 0.8f);
            for (int z = -26; z <= 26; z += 6) m.CeilingLight(new Vector3(0, 3.3f, z), new Color(0.85f, 0.95f, 1f), 9f, 1.3f, new Vector3(1.2f, 0.05f, 0.3f));
            m.CeilingLight(new Vector3(0, 4.8f, 37f), new Color(0.6f, 0.85f, 1f), 18f, 1.6f);
            m.PlayerSpawn = new Vector3(0, 0.1f, -28f);
            m.NpcSpawns.AddRange(new[] { new Vector3(0, 0.1f, -10), new Vector3(0, 0.1f, 10), new Vector3(-8, 0.1f, 34), new Vector3(8, 0.1f, 34), new Vector3(0, 0.1f, 40) });
            m.Playable = new Bounds(new Vector3(0, 2.5f, 7), new Vector3(32, 5, 74));
        }

        static void Dream(MapBuilder m)
        {
            var pastel = new[] { new Color(1f, 0.75f, 0.85f), new Color(0.7f, 0.85f, 1f), new Color(0.8f, 1f, 0.8f), new Color(1f, 0.95f, 0.7f), new Color(0.85f, 0.75f, 1f) };
            var islands = new[] { new Vector4(0, 0, 0, 12), new Vector4(26, 4, 10, 8), new Vector4(-22, 7, 18, 7), new Vector4(8, 11, 36, 9), new Vector4(-18, -4, -20, 8), new Vector4(30, 15, -14, 6) };
            for (int i = 0; i < islands.Length; i++)
            {
                var c = new Vector3(islands[i].x, islands[i].y, islands[i].z); float r = islands[i].w;
                m.Cylinder("Island", c - Vector3.up * 3f, r, 3f, T(TextureFactory.Grass), SurfaceType.Grass, pastel[i % pastel.Length], 20);
                m.Cylinder("Island Root", c - Vector3.up * 7f, r * 0.55f, 4f, T(TextureFactory.Dirt), SurfaceType.Dirt, pastel[(i + 2) % pastel.Length] * 0.7f, 12);
            }
            m.Box("Giant Chair Seat", new Vector3(4, 2.5f, 3), Quaternion.Euler(0, 20, 0), new Vector3(5f, 0.6f, 5f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.9f, 0.6f, 0.4f));
            for (int i = 0; i < 4; i++) m.Box("Giant Chair Leg", new Vector3(4, 0, 3) + Quaternion.Euler(0, 20, 0) * new Vector3(i < 2 ? -2.2f : 2.2f, 1.1f, i % 2 == 0 ? -2.2f : 2.2f), Quaternion.Euler(0, 20, 0), new Vector3(0.5f, 2.2f, 0.5f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.9f, 0.6f, 0.4f));
            m.Box("Giant Chair Back", new Vector3(4, 0, 3) + Quaternion.Euler(0, 20, 0) * new Vector3(0, 5.5f, 2.3f), Quaternion.Euler(0, 20, 0), new Vector3(5f, 5.5f, 0.5f), T(TextureFactory.Wood), SurfaceType.Wood, new Color(0.9f, 0.6f, 0.4f));
            m.Stairs("Stairs to Nowhere", new Vector3(-6, 0, -4), 45f, 2f, 18, 0.4f, 0.6f, T(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.95f, 0.95f, 1f));
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(Random.Range(-8f, 8f), 0, Random.Range(-8f, 8f));
                m.Box("Lone Door", p + Vector3.up * 1.1f, Quaternion.Euler(0, Random.Range(0f, 360f), 0), new Vector3(1f, 2.2f, 0.12f), T(TextureFactory.Wood), SurfaceType.Wood, pastel[i]);
            }
            for (int i = 0; i < 5; i++) m.Ramp("Bridge", new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f)), Random.Range(0f, 360f), 1.5f, 4f, 15f, T(TextureFactory.Wood), SurfaceType.Wood);
            m.PlayerSpawn = new Vector3(0, 0.1f, -6f);
            m.NpcSpawns.AddRange(new[] { new Vector3(2, 0.1f, 2), new Vector3(26, 4.1f, 10), new Vector3(-22, 7.1f, 18), new Vector3(8, 11.1f, 36), new Vector3(-18, -3.9f, -20) });
            m.Playable = new Bounds(new Vector3(0, 5, 8), new Vector3(110, 80, 110));
            m.KillY = -50f;
        }
    }
}
