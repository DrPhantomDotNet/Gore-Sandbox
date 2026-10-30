using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// "The Docks": a harbour at night. A quay (x -60..60, z -6..70) along black water, a moored cargo ship and a gantry
    /// crane at the edge, a container yard (east), the dock office (west), a big shed (north-west), floodlights,
    /// trucks and junk. Container 7 (blue) stands alone at the far east end. Story chapter 1, mission 2 is set here
    /// (Story/Chapter1.cs); it's also a normal sandbox map.
    /// </summary>
    public static class DocksMap
    {
        public const string MapId = "docks";
        public static readonly Vector3 Container7 = new Vector3(40f, 0f, 42f);

        public static MapDef CreateDef() => new MapDef
        {
            Id = MapId, Name = "The Docks", Size = "Large - harbour", Glyph = "box", DefaultTime = TimeOfDay.Night, Ambience = "amb_wind",
            Description = "A container port at night: stacked shipping containers, a gantry crane, a cargo ship at the quay, floodlights and the dock office. Story chapter 1, mission 2 takes place here.",
            Features = new[] { "Night harbour", "Container yard", "Gantry crane", "Cargo ship", "Floodlights", "Story mission 2" },
            Tags = new[] { "Story" },
            ThemeTop = new Color(0.02f, 0.04f, 0.08f), ThemeBottom = new Color(0.12f, 0.2f, 0.28f), Accent = new Color(0.3f, 0.6f, 1f),
            Build = Build, Thumbnail = Thumb(),
        };

        static readonly Color[] Paint =
        {
            new Color(0.62f, 0.16f, 0.12f), new Color(0.15f, 0.4f, 0.22f), new Color(0.75f, 0.5f, 0.12f), new Color(0.35f, 0.36f, 0.38f),
            new Color(0.55f, 0.28f, 0.1f), new Color(0.2f, 0.25f, 0.45f), new Color(0.65f, 0.62f, 0.55f), new Color(0.4f, 0.12f, 0.2f),
        };

        static void Build(MapBuilder m)
        {
            var conc = MapBuilder.Tex(TextureFactory.Concrete);
            var asphalt = MapBuilder.Tex(TextureFactory.Asphalt);
            var metal = MapBuilder.Tex(TextureFactory.Metal);

            // Quay and water.
            m.Block("Quay", new Vector3(-60, -2, -6), new Vector3(60, 0, 70), asphalt, SurfaceType.Concrete, new Color(0.42f, 0.42f, 0.44f), 0.25f);
            m.Block("Quay Edge", new Vector3(-60, -3, -6.6f), new Vector3(60, 0.05f, -6), conc, SurfaceType.Concrete, new Color(0.55f, 0.55f, 0.55f), 0.5f);
            m.Block("Sea Bed", new Vector3(-120, -9, -90), new Vector3(120, -8, -6.6f), conc, SurfaceType.Concrete, new Color(0.05f, 0.06f, 0.07f), 0.1f);
            m.Box("Water", new Vector3(0, -1.2f, -48), Quaternion.identity, new Vector3(240, 0.05f, 84), Mats.Lit, SurfaceType.Glass, new Color(0.02f, 0.05f, 0.08f), 1f, false);
            for (float x = -56; x <= 56; x += 8)
                m.Cylinder("Bollard", new Vector3(x, 0, -5.2f), 0.25f, 0.6f, Mats.LitMetal, SurfaceType.Metal, new Color(0.15f, 0.15f, 0.16f), 10);
            for (float x = -58; x < 60; x += 4.2f)
                m.Block("Lane Paint", new Vector3(x, 0, 4.9f), new Vector3(x + 2.4f, 0.012f, 5.1f), Mats.Lit, SurfaceType.Concrete, new Color(0.85f, 0.75f, 0.25f), 1f, false);

            Ship(m);
            Crane(m, -14f);
            Crane(m, 22f);

            // Container yard (east): rows of 2-3 high stacks with aisles between them.
            var rnd = new System.Random(4242);
            for (int col = 0; col < 4; col++)
                for (int row = 0; row < 4; row++)
                {
                    float x = 4f + col * 6.5f, z = 18f + row * 11f;
                    int h = 1 + rnd.Next(3);
                    for (int k = 0; k < h; k++)
                        Container(m, new Vector3(x, k * 2.6f, z), 0f, Paint[rnd.Next(Paint.Length)], false);
                }
            // A couple of lone containers near the office and the quay.
            Container(m, new Vector3(-8f, 0, 30f), 90f, Paint[1], false);
            Container(m, new Vector3(-8f, 0, 38f), 90f, Paint[4], false);
            Container(m, new Vector3(-12f, 2.6f, 34f), 0f, Paint[0], false);
            Container(m, new Vector3(-12f, 0, 34f), 0f, Paint[6], false);
            // Container 7: blue, alone at the east end, doors facing west (-z side lit).
            Container(m, Container7, 0f, new Color(0.12f, 0.3f, 0.7f), true);

            // Dock office (west) and the shed (north-west).
            m.Block("Dock Office", new Vector3(-36, 0, 6), new Vector3(-24, 3.6f, 15), MapBuilder.Tex(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.5f, 0.45f, 0.42f), 0.3f);
            m.Block("Office Roof", new Vector3(-36.4f, 3.6f, 5.6f), new Vector3(-23.6f, 3.9f, 15.4f), metal, SurfaceType.Metal, new Color(0.3f, 0.32f, 0.34f), 0.4f);
            m.Box("Office Window", new Vector3(-23.97f, 1.8f, 9f), Quaternion.identity, new Vector3(0.05f, 1.2f, 2.6f), Mats.Glow(new Color(1f, 0.85f, 0.55f), 1.2f), SurfaceType.Glass, Color.white, 1f, false);
            m.Box("Office Door", new Vector3(-23.97f, 1.1f, 12.6f), Quaternion.identity, new Vector3(0.06f, 2.2f, 1.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.2f, 0.32f, 0.3f), 0.5f, false);
            m.CeilingLight(new Vector3(-23.7f, 3.2f, 11f), new Color(1f, 0.8f, 0.5f), 12f, 1.6f, new Vector3(0.3f, 0.2f, 0.5f));
            m.Block("Shed", new Vector3(-58, 0, 46), new Vector3(-22, 12, 70), metal, SurfaceType.Metal, new Color(0.4f, 0.45f, 0.5f), 0.2f);
            m.Box("Shed Door", new Vector3(-40f, 3f, 45.95f), Quaternion.identity, new Vector3(8f, 6f, 0.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.55f, 0.4f, 0.15f), 0.3f, false);
            m.Box("Shed Sign", new Vector3(-40f, 9.5f, 45.9f), Quaternion.identity, new Vector3(10f, 1f, 0.08f), Mats.Glow(new Color(0.3f, 0.6f, 1f), 1.6f), SurfaceType.Glass, Color.white, 1f, false);

            // Perimeter fence (north and west) with a gate gap at the west road.
            m.Fence(new Vector3(-60, 0, 70), new Vector3(60, 0, 70), 2.4f);
            m.Fence(new Vector3(-60, 0, -5), new Vector3(-60, 0, 24), 2.4f);
            m.Fence(new Vector3(-60, 0, 36), new Vector3(-60, 0, 70), 2.4f);
            m.Fence(new Vector3(60, 0, -5), new Vector3(60, 0, 70), 2.4f);

            // Floodlights.
            var flood = new Color(0.85f, 0.9f, 1f);
            foreach (var p in new[] { new Vector3(-30, 0, 2), new Vector3(-2, 0, 2), new Vector3(30, 0, 2), new Vector3(-2, 0, 40), new Vector3(30, 0, 62), new Vector3(48, 0, 28), new Vector3(-30, 0, 30) })
                m.StreetLamp(p, 0f, flood, 9f, 26f, 2.4f);
            m.StreetLamp(new Vector3(-50, 0, 24), 90f, new Color(1f, 0.75f, 0.45f), 6f, 18f, 2f);

            // Trucks, pallets, barrels, crates.
            m.Car(new Vector3(-44f, 0, 12f), 0f, new Color(0.2f, 0.22f, 0.25f));
            m.Car(new Vector3(-16f, 0, 60f), 90f, new Color(0.7f, 0.68f, 0.6f));
            m.Car(new Vector3(52f, 0, 54f), 200f, new Color(0.12f, 0.12f, 0.13f));
            PropFactory.Spawn("pallet", new Vector3(-18f, 0.1f, 8f), Quaternion.Euler(0, 15, 0));
            PropFactory.Spawn("crate", new Vector3(-18f, 0.6f, 8f), Quaternion.Euler(0, 10, 0));
            PropFactory.Spawn("crate", new Vector3(-17f, 0.41f, 6.6f), Quaternion.Euler(0, -8, 0));
            PropFactory.Spawn("barrel", new Vector3(34f, 0.46f, 36f), Quaternion.identity);
            PropFactory.Spawn("barrel", new Vector3(35f, 0.46f, 35.2f), Quaternion.identity);
            PropFactory.Spawn("explosive_barrel", new Vector3(45.5f, 0.46f, 37f), Quaternion.identity);
            PropFactory.Spawn("explosive_barrel", new Vector3(30f, 0.46f, 50f), Quaternion.identity);
            PropFactory.Spawn("small_crate", new Vector3(2f, 0.3f, 8f), Quaternion.identity);
            PropFactory.Spawn("tire", new Vector3(-26f, 0.5f, 22f), Quaternion.identity);
            PropFactory.Spawn("cone", new Vector3(-52f, 0.02f, 27f), Quaternion.identity);
            PropFactory.Spawn("cone", new Vector3(-52f, 0.02f, 33f), Quaternion.identity);

            m.PlayerSpawn = new Vector3(-54f, 0.1f, 30f);
            m.PlayerYaw = 110f;
            m.NpcSpawns.Add(new Vector3(-20, 0.1f, 25));
            m.NpcSpawns.Add(new Vector3(0, 0.1f, 10));
            m.NpcSpawns.Add(new Vector3(30, 0.1f, 12));
            m.NpcSpawns.Add(new Vector3(-30, 0.1f, 38));
            m.Boundaries(new Bounds(new Vector3(0, 15, 32), new Vector3(120, 30, 76)));
            m.Playable = new Bounds(new Vector3(0, 5, 32), new Vector3(122, 20, 78));
            m.KillY = -20f;
        }

        // ------------------------------------------------------------------ pieces

        static void Container(MapBuilder m, Vector3 basePos, float yaw, Color paint, bool seven)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var size = new Vector3(2.44f, 2.6f, 6.06f);
            m.Box("Container", basePos + Vector3.up * 1.3f, rot, size, Mats.LitMetal, SurfaceType.Metal, paint, 0.6f);
            // Corrugation ribs on the long sides + door bars on the -Z end.
            for (float z = -2.4f; z <= 2.41f; z += 1.2f)
                for (int s = -1; s <= 1; s += 2)
                    m.Box("Rib", basePos + rot * new Vector3(s * 1.235f, 1.3f, z), rot, new Vector3(0.04f, 2.4f, 0.12f), Mats.LitMetal, SurfaceType.Metal, paint * 0.8f, 1f, false);
            for (int b = 0; b < 4; b++)
                m.Box("Door Bar", basePos + rot * new Vector3(-0.9f + b * 0.6f, 1.3f, -3.05f), rot, new Vector3(0.05f, 2.4f, 0.05f), Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.6f, 0.6f), 1f, false);
            if (!seven) return;
            // A big white "7" on both sides and a lamp over the doors.
            var white = Mats.Glow(new Color(0.95f, 0.95f, 0.95f), 0.6f);
            for (int s = -1; s <= 1; s += 2)
            {
                var o = new Vector3(s * 1.28f, 0, 0);
                m.Box("Seven", basePos + rot * (o + new Vector3(0, 2.05f, 0.2f)), rot, new Vector3(0.03f, 0.22f, 1.2f), white, SurfaceType.Metal, Color.white, 1f, false);
                m.Box("Seven", basePos + rot * (o + new Vector3(0, 1.35f, 0.05f)), rot * Quaternion.Euler(-58f, 0, 0), new Vector3(0.03f, 0.22f, 1.55f), white, SurfaceType.Metal, Color.white, 1f, false);
            }
            m.CeilingLight(basePos + rot * new Vector3(0, 2.9f, -3.3f), new Color(0.6f, 0.75f, 1f), 10f, 1.6f, new Vector3(0.4f, 0.12f, 0.2f));
        }

        static void Ship(MapBuilder m)
        {
            var hull = new Color(0.32f, 0.08f, 0.07f);
            m.Box("Ship Hull", new Vector3(-6, 1.5f, -17), Quaternion.identity, new Vector3(76, 9, 16), Mats.LitMetal, SurfaceType.Metal, hull, 0.2f);
            m.Box("Ship Deck", new Vector3(-6, 6.05f, -17), Quaternion.identity, new Vector3(75, 0.1f, 15), Mats.LitMetal, SurfaceType.Metal, new Color(0.2f, 0.25f, 0.22f), 0.2f);
            m.Box("Ship Bow", new Vector3(34, 1.5f, -17), Quaternion.Euler(0, 45, 0), new Vector3(11.3f, 9, 11.3f), Mats.LitMetal, SurfaceType.Metal, hull, 0.2f);
            m.Box("Bridge", new Vector3(-34, 11f, -17), Quaternion.identity, new Vector3(10, 10, 13), Mats.LitMetal, SurfaceType.Metal, new Color(0.85f, 0.85f, 0.82f), 0.2f);
            m.Box("Bridge Windows", new Vector3(-28.95f, 14.2f, -17), Quaternion.identity, new Vector3(0.05f, 1.2f, 11f), Mats.Glow(new Color(1f, 0.85f, 0.55f), 1f), SurfaceType.Glass, Color.white, 1f, false);
            m.Box("Funnel", new Vector3(-36, 18.5f, -17), Quaternion.identity, new Vector3(4, 5, 4), Mats.LitMetal, SurfaceType.Metal, new Color(0.15f, 0.15f, 0.16f), 0.3f);
            var rnd = new System.Random(77);
            for (int i = 0; i < 9; i++)
                for (int k = 0; k < 2; k++)
                    m.Box("Deck Container", new Vector3(-22 + i * 6.4f, 7.4f + k * 2.6f, -17 + (k == 0 ? -3 : 2)), Quaternion.Euler(0, 90, 0), new Vector3(2.44f, 2.6f, 6.06f), Mats.LitMetal, SurfaceType.Metal, Paint[rnd.Next(Paint.Length)], 0.6f, false);
            m.Box("Gangway", new Vector3(-20, 3f, -7.5f), Quaternion.Euler(-26f, 0, 0), new Vector3(1.4f, 0.1f, 6.8f), Mats.LitMetal, SurfaceType.Metal, new Color(0.45f, 0.45f, 0.47f), 1f, false);
        }

        static void Crane(MapBuilder m, float x)
        {
            var yellow = new Color(0.85f, 0.6f, 0.1f);
            foreach (var z in new[] { -4.5f, 10f })
                foreach (var dx in new[] { -6f, 6f })
                    m.Box("Crane Leg", new Vector3(x + dx, 11f, z), Quaternion.identity, new Vector3(0.9f, 22f, 0.9f), Mats.LitMetal, SurfaceType.Metal, yellow, 0.3f);
            m.Box("Crane Beam", new Vector3(x - 6f, 22.5f, -10f), Quaternion.identity, new Vector3(1.2f, 1.6f, 44f), Mats.LitMetal, SurfaceType.Metal, yellow, 0.3f, false);
            m.Box("Crane Beam", new Vector3(x + 6f, 22.5f, -10f), Quaternion.identity, new Vector3(1.2f, 1.6f, 44f), Mats.LitMetal, SurfaceType.Metal, yellow, 0.3f, false);
            m.Box("Crane Top", new Vector3(x, 23.6f, 2.5f), Quaternion.identity, new Vector3(13f, 1f, 16f), Mats.LitMetal, SurfaceType.Metal, yellow * 0.9f, 0.3f, false);
            m.Box("Crane Cab", new Vector3(x, 21.2f, -14f), Quaternion.identity, new Vector3(3f, 2.4f, 3f), Mats.LitMetal, SurfaceType.Metal, new Color(0.25f, 0.25f, 0.27f), 0.5f, false);
            m.Box("Crane Cable", new Vector3(x, 15f, -14f), Quaternion.identity, new Vector3(0.05f, 12f, 0.05f), Mats.LitMetal, SurfaceType.Metal, new Color(0.1f, 0.1f, 0.1f), 1f, false);
            m.Box("Crane Light", new Vector3(x, 25f, 2.5f), Quaternion.identity, new Vector3(0.3f, 0.3f, 0.3f), Mats.Glow(new Color(1f, 0.2f, 0.15f), 3f), SurfaceType.Glass, Color.white, 1f, false);
        }

        static Texture2D Thumb()
        {
            const int W = 384, H = 216;
            var c = new PixelCanvas(W, H, Color.black);
            for (int y = 0; y < H; y++)
            {
                var col = Color.Lerp(new Color(0.02f, 0.05f, 0.09f), new Color(0.05f, 0.1f, 0.18f), y / (float)H);
                for (int x = 0; x < W; x++) c.Set(x, y, col);
            }
            c.Rect(0, 0, W, 60, new Color(0.02f, 0.04f, 0.06f));   // water (bottom)
            c.Rect(0, 60, W, 18, new Color(0.25f, 0.25f, 0.27f));  // quay
            var paints = new[] { new Color(0.62f, 0.16f, 0.12f), new Color(0.15f, 0.4f, 0.22f), new Color(0.75f, 0.5f, 0.12f), new Color(0.12f, 0.3f, 0.7f) };
            for (int i = 0; i < 9; i++)
            {
                int h = 1 + (i * 7) % 3;
                for (int k = 0; k < h; k++) c.Rect(170 + i * 22, 78 + k * 18, 20, 16, paints[(i + k) % paints.Length]);
            }
            var yellow = new Color(0.85f, 0.6f, 0.1f);
            c.Rect(60, 60, 6, 120, yellow); c.Rect(110, 60, 6, 120, yellow); c.Rect(40, 170, 110, 8, yellow);
            c.Circle(300, 185, 6, new Color(0.9f, 0.95f, 1f), 4);
            c.Circle(300, 185, 30, new Color(0.8f, 0.9f, 1f, 0.2f), 25);
            for (int x = 0; x < W; x += 9) c.Rect(x, 20 + (x * 13) % 30, 6, 1, new Color(0.9f, 0.85f, 0.6f, 0.4f)); // reflections
            return c.ToTexture("Thumb_" + MapId);
        }
    }
}
