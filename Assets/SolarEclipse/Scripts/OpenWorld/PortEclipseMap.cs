using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A thing in the city you can use (shop door, heist board, locker...). On the Trigger layer: E / INTERACT.</summary>
    public sealed class OWPoint : MonoBehaviour, IInteractable, ILocalInteractable
    {
        public string Kind, Label;
        public string Prompt => Label;
        public void Interact(GameObject user) => OWDirector.Use(this);

        public static OWPoint Make(Transform parent, string kind, string label, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("OW " + kind);
            go.layer = Layers.Trigger;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = size;
            var p = go.AddComponent<OWPoint>();
            p.Kind = kind; p.Label = label;
            OWMap.Points.Add(p);
            return p;
        }
    }

    /// <summary>Named places in Port Eclipse (map blips, jobs, heists).</summary>
    public static class OWMap
    {
        public const string MapId = "porteclipse";
        public static readonly List<OWPoint> Points = new List<OWPoint>();
        public static readonly Dictionary<string, Vector3> Places = new Dictionary<string, Vector3>();
        public static readonly List<Vector3> CarSpawns = new List<Vector3>(), Sidewalks = new List<Vector3>(), Hospital = new List<Vector3>();
        public static Vector3 ApartmentInside, ApartmentOutside, GarageSpot;
        public const float Half = 186f;

        /// <summary>World centre of city block (i, j), 0..5 each.</summary>
        public static Vector3 Block(int i, int j) => new Vector3(-150f + 60f * i, 0f, -150f + 60f * j);
        public static Vector3 Place(string n) => Places.TryGetValue(n, out var v) ? v : Vector3.zero;

        public static string District(Vector3 p)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt((p.x + 180f) / 60f), 0, 5), j = Mathf.Clamp(Mathf.FloorToInt((p.z + 180f) / 60f), 0, 5);
            if (i <= 1 && j >= 4) return "Uptown";
            if (i <= 1 && j <= 1) return "Southside";
            if (i >= 4 && j >= 4) return "Industrial";
            if (i >= 4 && j <= 1) return "The Docks";
            if (i >= 2 && i <= 3 && j >= 2 && j <= 3) return "Downtown";
            return "Midtown";
        }
    }

    /// <summary>
    /// "Port Eclipse": the Open World city. 7 x 7 streets (12 m wide, every 60 m) with 36 blocks in six districts:
    /// Downtown (bank, jewelry store, gun store, clothes), Uptown (the Meridian penthouses, the boutique, mansions),
    /// Southside (Big Marcus and the heist board, the gas station, a gang hideout), Midtown (hospital, police, shops,
    /// offices), Industrial (scrapyard, factory) and the Docks (containers, ships). The bank, jewelry store, gas
    /// station and your penthouse have interiors. Everything Open World runs in OWDirector (added here).
    /// </summary>
    public static class PortEclipseMap
    {
        public static MapDef CreateDef() => new MapDef
        {
            Id = OWMap.MapId, Name = "Port Eclipse", Size = "Huge - open world city", Glyph = "home", DefaultTime = TimeOfDay.Sunset, Ambience = "amb_city",
            Description = "The Open World city: heists with your crew, cars, shops, a penthouse, police, gangs and street jobs. Your cash is saved.",
            Features = new[] { "Open World", "Heists", "Cars", "Shops", "Police", "Saved cash" },
            Tags = new[] { "Open World" },
            ThemeTop = new Color(0.08f, 0.05f, 0.15f), ThemeBottom = new Color(0.9f, 0.45f, 0.2f), Accent = new Color(1f, 0.78f, 0.16f),
            Build = Build, Thumbnail = Thumb(), NavCell = 1f,
        };

        static readonly float[] Lines = { -180, -120, -60, 0, 60, 120, 180 };
        const float RoadHalf = 6f;
        static System.Random rnd;

        static void Build(MapBuilder m)
        {
            rnd = new System.Random(1337);
            OWMap.Points.Clear(); OWMap.Places.Clear(); OWMap.CarSpawns.Clear(); OWMap.Sidewalks.Clear(); OWMap.Hospital.Clear();
            m.Options.StartingNpcs = 0; m.Options.Nextbots = false;
            var asphalt = MapBuilder.Tex(TextureFactory.Asphalt);
            var conc = MapBuilder.Tex(TextureFactory.Concrete);
            var grass = MapBuilder.Tex(TextureFactory.Grass);

            // Ground: grass under everything, asphalt roads, concrete sidewalks around every block.
            m.Block("Ground", new Vector3(-200, -1, -200), new Vector3(200, 0, 200), grass, SurfaceType.Grass, new Color(0.45f, 0.55f, 0.35f), 0.2f);
            foreach (var l in Lines)
            {
                m.Block("Road", new Vector3(-OWMap.Half, 0, l - RoadHalf), new Vector3(OWMap.Half, 0.01f, l + RoadHalf), asphalt, SurfaceType.Concrete, new Color(0.4f, 0.4f, 0.42f), 0.2f, false);
                m.Block("Road", new Vector3(l - RoadHalf, 0, -OWMap.Half), new Vector3(l + RoadHalf, 0.012f, OWMap.Half), asphalt, SurfaceType.Concrete, new Color(0.4f, 0.4f, 0.42f), 0.2f, false);
                for (float t = -OWMap.Half + 4; t < OWMap.Half; t += 8f)
                {
                    if (Near(t)) continue;
                    m.Box("Lane", new Vector3(t, 0.016f, l), Quaternion.identity, new Vector3(3f, 0.01f, 0.18f), Mats.Lit, SurfaceType.Concrete, new Color(0.9f, 0.8f, 0.3f), 1f, false);
                    m.Box("Lane", new Vector3(l, 0.017f, t), Quaternion.identity, new Vector3(0.18f, 0.01f, 3f), Mats.Lit, SurfaceType.Concrete, new Color(0.9f, 0.8f, 0.3f), 1f, false);
                }
            }
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                {
                    var c = OWMap.Block(i, j);
                    m.Block("Sidewalk", new Vector3(c.x - 24, 0, c.z - 24), new Vector3(c.x + 24, 0.15f, c.z + 24), conc, SurfaceType.Concrete, new Color(0.7f, 0.7f, 0.68f), 0.5f);
                    OWMap.Sidewalks.Add(c + new Vector3(-22, 0.2f, -22)); OWMap.Sidewalks.Add(c + new Vector3(22, 0.2f, 22));
                    OWMap.Sidewalks.Add(c + new Vector3(22, 0.2f, -22)); OWMap.Sidewalks.Add(c + new Vector3(-22, 0.2f, 22));
                }
            // Street lights on the corners.
            for (int a = 0; a < Lines.Length; a++)
                for (int b = 0; b < Lines.Length; b++)
                    if ((a + b) % 2 == 0) m.StreetLamp(new Vector3(Lines[a] + 7f, 0.15f, Lines[b] + 7f), 225f, new Color(1f, 0.82f, 0.55f), 6.5f, 20f, 2f);

            // ---------------- blocks
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                    BuildBlock(m, i, j);

            // ---------------- edges: sea to the east, a wall elsewhere
            m.Box("Sea", new Vector3(260, -0.6f, 0), Quaternion.identity, new Vector3(150, 0.05f, 420), Mats.Lit, SurfaceType.Glass, new Color(0.04f, 0.12f, 0.2f), 1f, false);
            m.Boundaries(new Bounds(new Vector3(0, 30, 0), new Vector3(OWMap.Half * 2f, 60, OWMap.Half * 2f)));

            Apartment(m);

            m.PlayerSpawn = OWMap.Hospital.Count > 0 ? OWMap.Hospital[0] : new Vector3(0, 0.3f, -126);
            m.PlayerYaw = 0f;
            m.NpcSpawns.AddRange(OWMap.Sidewalks.GetRange(0, Mathf.Min(8, OWMap.Sidewalks.Count)));
            m.Playable = new Bounds(new Vector3(0, 3f, 0), new Vector3(OWMap.Half * 2f, 10f, OWMap.Half * 2f));
            m.KillY = -90f;
            m.Root.gameObject.AddComponent<OWDirector>();
        }

        static bool Near(float t) { foreach (var l in Lines) if (Mathf.Abs(t - l) < 8f) return true; return false; }

        // ------------------------------------------------------------------ buildings

        static readonly Color[] Tints = { new Color(0.75f, 0.72f, 0.66f), new Color(0.55f, 0.6f, 0.68f), new Color(0.7f, 0.55f, 0.45f), new Color(0.6f, 0.62f, 0.6f), new Color(0.45f, 0.5f, 0.58f), new Color(0.82f, 0.78f, 0.7f) };

        /// <summary>A solid building with glowing window strips (one object per floor and face, cheap on phones).</summary>
        static void Tower(MapBuilder m, Vector3 min, Vector3 max, Texture2D tex, Color tint, bool lit = true)
        {
            m.Block("Building", min, max, MapBuilder.Tex(tex), SurfaceType.Concrete, tint, 0.25f);
            m.Block("Roof", new Vector3(min.x - 0.25f, max.y, min.z - 0.25f), new Vector3(max.x + 0.25f, max.y + 0.5f, max.z + 0.25f), MapBuilder.Tex(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.5f, 0.5f, 0.52f), 0.5f, false);
            var win = Mats.Glow(new Color(1f, 0.85f, 0.55f), 1.1f);
            var sx = max.x - min.x; var sz = max.z - min.z;
            int floor = 0;
            for (float y = min.y + 4.5f; y < max.y - 1.5f; y += 3.6f, floor++)
            {
                bool on = lit && (floor + (int)min.x) % 3 != 0;
                var mat = on ? win : Mats.Lit;
                var col = on ? Color.white : new Color(0.12f, 0.16f, 0.22f);
                m.Box("Windows", new Vector3((min.x + max.x) / 2, y, min.z - 0.04f), Quaternion.identity, new Vector3(sx - 2f, 1.5f, 0.06f), mat, SurfaceType.Glass, col, 1f, false);
                m.Box("Windows", new Vector3((min.x + max.x) / 2, y, max.z + 0.04f), Quaternion.identity, new Vector3(sx - 2f, 1.5f, 0.06f), mat, SurfaceType.Glass, col, 1f, false);
                if (floor % 2 == 0)
                {
                    m.Box("Windows", new Vector3(min.x - 0.04f, y, (min.z + max.z) / 2), Quaternion.identity, new Vector3(0.06f, 1.5f, sz - 2f), mat, SurfaceType.Glass, col, 1f, false);
                    m.Box("Windows", new Vector3(max.x + 0.04f, y, (min.z + max.z) / 2), Quaternion.identity, new Vector3(0.06f, 1.5f, sz - 2f), mat, SurfaceType.Glass, col, 1f, false);
                }
            }
        }

        /// <summary>A shop front: a building with a lit sign and an interactable door on the street side (facing -Z unless flipped).</summary>
        static void Shop(MapBuilder m, Vector3 c, string kind, string sign, Color signCol, float height = 9f, bool facePlusZ = false)
        {
            var min = c + new Vector3(-16, 0.15f, -14); var max = c + new Vector3(16, height, 14);
            Tower(m, min, max, TextureFactory.Plaster, new Color(0.85f, 0.82f, 0.78f), false);
            float zf = facePlusZ ? max.z + 0.06f : min.z - 0.06f;
            float dir = facePlusZ ? 1f : -1f;
            m.Box("Shop Window", new Vector3(c.x, 2.2f, zf), Quaternion.identity, new Vector3(24f, 3.2f, 0.08f), Mats.Glow(new Color(0.95f, 0.9f, 0.75f), 0.8f), SurfaceType.Glass, Color.white, 1f, false);
            m.Box("Shop Sign", new Vector3(c.x, 5.2f, zf + dir * 0.1f), Quaternion.identity, new Vector3(16f, 1.4f, 0.12f), Mats.Glow(signCol, 2.2f), SurfaceType.Glass, Color.white, 1f, false);
            m.Box("Awning", new Vector3(c.x, 4.1f, zf + dir * 1.2f), Quaternion.identity, new Vector3(26f, 0.12f, 2.4f), Mats.Lit, SurfaceType.Metal, signCol * 0.6f, 1f, false);
            var door = new Vector3(c.x, 1.2f, zf + dir * 1.4f);
            OWPoint.Make(m.Root, kind, sign, door, new Vector3(4f, 2.6f, 2.2f));
            OWMap.Places[sign] = door;
            m.CeilingLight(new Vector3(c.x, 3.9f, zf + dir * 1.6f), signCol, 12f, 1.6f, new Vector3(1.2f, 0.1f, 0.4f));
        }

        /// <summary>A hollow building: floor, ceiling, walls, a doorway on the -Z side. Returns the inside bounds.</summary>
        static Bounds Room(MapBuilder m, Vector3 min, Vector3 max, Texture2D wallTex, Color tint, float doorX, float doorW = 3f)
        {
            var wall = MapBuilder.Tex(wallTex);
            const float t = 0.4f;
            m.Block("Floor", new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y + 0.15f, max.z), MapBuilder.Tex(TextureFactory.Tiles), SurfaceType.Concrete, new Color(0.8f, 0.8f, 0.78f), 0.5f);
            m.Block("Ceiling", new Vector3(min.x, max.y - 0.3f, min.z), new Vector3(max.x, max.y, max.z), MapBuilder.Tex(TextureFactory.Ceiling), SurfaceType.Concrete, Color.white, 0.5f);
            m.Block("Wall", new Vector3(min.x, min.y, max.z - t), new Vector3(max.x, max.y, max.z), wall, SurfaceType.Concrete, tint, 0.3f);
            m.Block("Wall", new Vector3(min.x, min.y, min.z), new Vector3(min.x + t, max.y, max.z), wall, SurfaceType.Concrete, tint, 0.3f);
            m.Block("Wall", new Vector3(max.x - t, min.y, min.z), new Vector3(max.x, max.y, max.z), wall, SurfaceType.Concrete, tint, 0.3f);
            m.Block("Wall", new Vector3(min.x, min.y, min.z), new Vector3(doorX - doorW / 2, max.y, min.z + t), wall, SurfaceType.Concrete, tint, 0.3f);
            m.Block("Wall", new Vector3(doorX + doorW / 2, min.y, min.z), new Vector3(max.x, max.y, min.z + t), wall, SurfaceType.Concrete, tint, 0.3f);
            m.Block("Wall", new Vector3(doorX - doorW / 2, min.y + 2.8f, min.z), new Vector3(doorX + doorW / 2, max.y, min.z + t), wall, SurfaceType.Concrete, tint, 0.3f);
            m.CeilingLight(new Vector3((min.x + max.x) / 2, max.y - 0.4f, (min.z + max.z) / 2), new Color(1f, 0.95f, 0.85f), 18f, 1.8f, new Vector3(2f, 0.1f, 0.5f));
            return new Bounds((min + max) / 2, max - min);
        }

        static void Park(MapBuilder m, Vector3 c)
        {
            m.Block("Park", c + new Vector3(-23, 0.15f, -23), c + new Vector3(23, 0.2f, 23), MapBuilder.Tex(TextureFactory.Grass), SurfaceType.Grass, new Color(0.4f, 0.6f, 0.3f), 0.3f);
            for (int k = 0; k < 7; k++) m.Tree(c + new Vector3((float)rnd.NextDouble() * 40 - 20, 0.2f, (float)rnd.NextDouble() * 40 - 20), 0.9f + (float)rnd.NextDouble() * 0.6f, rnd.NextDouble() < 0.4);
            m.Cylinder("Fountain", c + Vector3.up * 0.2f, 3f, 0.6f, MapBuilder.Tex(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.75f, 0.75f, 0.75f), 20);
            m.Box("Fountain Water", c + Vector3.up * 0.78f, Quaternion.identity, new Vector3(5f, 0.05f, 5f), Mats.Lit, SurfaceType.Glass, new Color(0.2f, 0.45f, 0.65f), 1f, false);
        }

        static void Generic(MapBuilder m, Vector3 c, int i, int j, float hMin, float hMax)
        {
            // 2-4 buildings per block with a gap in the middle (alleys).
            int kind = rnd.Next(3);
            Texture2D[] texs = { TextureFactory.Concrete, TextureFactory.Brick, TextureFactory.Plaster, TextureFactory.Metal };
            if (kind == 0)
                Tower(m, c + new Vector3(-20, 0.15f, -20), c + new Vector3(20, Mathf.Lerp(hMin, hMax, (float)rnd.NextDouble()), 20), texs[rnd.Next(texs.Length)], Tints[rnd.Next(Tints.Length)]);
            else
            {
                Tower(m, c + new Vector3(-21, 0.15f, -21), c + new Vector3(-2, Mathf.Lerp(hMin, hMax, (float)rnd.NextDouble()), 21), texs[rnd.Next(texs.Length)], Tints[rnd.Next(Tints.Length)]);
                Tower(m, c + new Vector3(2, 0.15f, -21), c + new Vector3(21, Mathf.Lerp(hMin, hMax, (float)rnd.NextDouble()), kind == 1 ? 21 : -2), texs[rnd.Next(texs.Length)], Tints[rnd.Next(Tints.Length)]);
                if (kind == 2) Tower(m, c + new Vector3(2, 0.15f, 2), c + new Vector3(21, Mathf.Lerp(hMin, hMax, (float)rnd.NextDouble()), 21), texs[rnd.Next(texs.Length)], Tints[rnd.Next(Tints.Length)]);
            }
            if (rnd.NextDouble() < 0.6) Parked(m, c + new Vector3(-27f, 0, (float)rnd.NextDouble() * 30f - 15f), 0f);
        }

        static void Parked(MapBuilder m, Vector3 pos, float yaw)
        {
            if (rnd.NextDouble() < 0.5) OWMap.CarSpawns.Add(pos + Vector3.up * 0.4f); // a real, drivable car spawns here (OWDirector)
            else m.Car(pos, yaw, Tints[rnd.Next(Tints.Length)] * 0.8f);
        }

        static void BuildBlock(MapBuilder m, int i, int j)
        {
            var c = OWMap.Block(i, j);
            switch (i * 10 + j)
            {
                // ---------------- Downtown
                case 23: Bank(m, c); break;
                case 33: Jewelry(m, c); break;
                case 22: Shop(m, c, "guns", "BULLET BROS.", new Color(1f, 0.3f, 0.15f)); Tower(m, c + new Vector3(-20, 9f, -14), c + new Vector3(20, 34f, 20), TextureFactory.Concrete, Tints[1]); break;
                case 32: Shop(m, c + new Vector3(0, 0, -6), "clothes", "THREADS", new Color(0.3f, 0.8f, 1f)); Shop(m, c + new Vector3(0, 0, 10), "masks", "MASK MART", new Color(0.7f, 0.3f, 1f), 7f, true); break;
                // ---------------- Midtown west: cars
                case 12: Shop(m, c, "cars", "ECLIPSE MOTORS", new Color(1f, 0.78f, 0.16f), 8f);
                    for (int k = 0; k < 3; k++) m.Car(c + new Vector3(-10 + k * 10, 0.15f, -20.5f), 90f, new[] { new Color(0.9f, 0.1f, 0.1f), new Color(0.95f, 0.8f, 0.1f), new Color(0.5f, 0.2f, 0.7f) }[k]);
                    break;
                case 13: Shop(m, c, "customs", "ECLIPSE CUSTOMS", new Color(0.2f, 1f, 0.5f), 8f); break;
                // ---------------- Uptown
                case 14: Meridian(m, c); break;
                case 04: Shop(m, c, "boutique", "ECLIPSE BOUTIQUE", new Color(1f, 0.5f, 0.85f), 10f); break;
                case 05: case 15: Mansion(m, c); break;
                // ---------------- Southside
                case 11: GasStation(m, c); break;
                case 10: Hangout(m, c); break;
                case 00: Hideout(m, c); break;
                case 01: for (int k = 0; k < 4; k++) Tower(m, c + new Vector3(-20 + (k % 2) * 22, 0.15f, -20 + (k / 2) * 22), c + new Vector3(-2 + (k % 2) * 22, 6f + k, -2 + (k / 2) * 22), TextureFactory.Brick, Tints[k % Tints.Length], true); break;
                // ---------------- Midtown
                case 20: Hospital(m, c); break;
                case 30: Police(m, c); break;
                case 21: Park(m, c); break;
                case 31: Park(m, c); break;
                // ---------------- Industrial
                case 55: Scrapyard(m, c); break;
                case 45: Tower(m, c + new Vector3(-22, 0.15f, -18), c + new Vector3(22, 14f, 18), TextureFactory.Metal, new Color(0.5f, 0.52f, 0.55f), false);
                    m.Cylinder("Chimney", c + new Vector3(16, 14f, 10), 1.6f, 22f, MapBuilder.Tex(TextureFactory.Brick), SurfaceType.Concrete, new Color(0.5f, 0.3f, 0.25f), 12); OWMap.Places["Factory"] = c + new Vector3(0, 0.2f, -22); break;
                case 54: Lot(m, c, "Race Start"); break;
                case 44: Tower(m, c + new Vector3(-20, 0.15f, -20), c + new Vector3(20, 11f, 20), TextureFactory.Metal, new Color(0.45f, 0.5f, 0.55f), false); break;
                // ---------------- Docks
                case 50: DocksYard(m, c, true); break;
                case 51: DocksYard(m, c, false); break;
                case 40: case 41: Tower(m, c + new Vector3(-20, 0.15f, -20), c + new Vector3(20, 10f, 20), TextureFactory.Metal, new Color(0.4f, 0.45f, 0.5f), false); Parked(m, c + new Vector3(-27f, 0, 5f), 0f); break;
                // ---------------- tall Downtown / Midtown edges
                case 24: case 34: case 25: case 35: Generic(m, c, i, j, 30f, 70f); break;
                default: Generic(m, c, i, j, 10f, 28f); break;
            }
        }

        // ------------------------------------------------------------------ landmarks

        static void Bank(MapBuilder m, Vector3 c)
        {
            var b = Room(m, c + new Vector3(-18, 0.15f, -16), c + new Vector3(18, 7f, 16), TextureFactory.Plaster, new Color(0.85f, 0.83f, 0.78f), c.x, 4f);
            Tower(m, c + new Vector3(-18, 7f, -16), c + new Vector3(18, 40f, 16), TextureFactory.Concrete, new Color(0.8f, 0.78f, 0.72f));
            m.Box("Bank Sign", c + new Vector3(0, 6.2f, -16.4f), Quaternion.identity, new Vector3(18f, 1.4f, 0.15f), Mats.Glow(new Color(0.3f, 0.9f, 0.5f), 2f), SurfaceType.Glass, Color.white, 1f, false);
            for (int k = -2; k <= 2; k += 2) m.Cylinder("Pillar", c + new Vector3(k * 3f, 0.15f, -18f), 0.6f, 6.6f, MapBuilder.Tex(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.9f, 0.9f, 0.88f), 12);
            m.Block("Teller Counter", c + new Vector3(-12, 0.15f, 2), c + new Vector3(12, 1.2f, 3.2f), MapBuilder.Tex(TextureFactory.Wood), SurfaceType.Wood, new Color(0.5f, 0.32f, 0.2f));
            // Vault at the back
            m.Block("Vault Wall", c + new Vector3(-18, 0.15f, 8), c + new Vector3(-3, 7f, 8.6f), MapBuilder.Tex(TextureFactory.Metal), SurfaceType.Metal, new Color(0.5f, 0.52f, 0.55f));
            m.Block("Vault Wall", c + new Vector3(3, 0.15f, 8), c + new Vector3(18, 7f, 8.6f), MapBuilder.Tex(TextureFactory.Metal), SurfaceType.Metal, new Color(0.5f, 0.52f, 0.55f));
            m.Cylinder("Vault Door", c + new Vector3(0, 0.15f, 8.3f), 2.8f, 0.3f, Mats.LitMetal, SurfaceType.Metal, new Color(0.6f, 0.6f, 0.62f), 24, false);
            OWMap.Places["Bank"] = c + new Vector3(0, 0.2f, -20);
            OWMap.Places["Bank Vault"] = c + new Vector3(0, 0.2f, 7f);
            OWMap.Places["Bank Loot"] = c + new Vector3(0, 0.2f, 12.5f);
            OWMap.Places["Bank Lobby"] = c + new Vector3(0, 0.2f, -6f);
            for (int k = 0; k < 6; k++) m.Box("Gold", c + new Vector3(-6 + k * 2.4f, 0.6f, 14.5f), Quaternion.identity, new Vector3(1.6f, 0.9f, 0.9f), Mats.Glow(new Color(1f, 0.8f, 0.2f), 0.5f), SurfaceType.Metal, Color.white, 1f, false);
        }

        static void Jewelry(MapBuilder m, Vector3 c)
        {
            Room(m, c + new Vector3(-16, 0.15f, -14), c + new Vector3(16, 6f, 14), TextureFactory.Wallpaper, new Color(0.9f, 0.85f, 0.95f), c.x, 4f);
            Tower(m, c + new Vector3(-16, 6f, -14), c + new Vector3(16, 30f, 14), TextureFactory.Concrete, Tints[4]);
            m.Box("Sign", c + new Vector3(0, 5.2f, -14.4f), Quaternion.identity, new Vector3(14f, 1.2f, 0.15f), Mats.Glow(new Color(0.7f, 0.9f, 1f), 2.2f), SurfaceType.Glass, Color.white, 1f, false);
            for (int k = 0; k < 6; k++)
            {
                var p = c + new Vector3(-10 + (k % 3) * 10, 0.15f, -4 + (k / 3) * 8);
                m.Box("Display Case", p + Vector3.up * 0.5f, Quaternion.identity, new Vector3(3f, 1f, 1.2f), Mats.Lit, SurfaceType.Wood, new Color(0.25f, 0.2f, 0.3f));
                m.Box("Case Glass", p + Vector3.up * 1.25f, Quaternion.identity, new Vector3(3f, 0.5f, 1.2f), Mats.Glass, SurfaceType.Glass, Color.white, 1f, false);
                OWMap.Places["Jewel " + k] = p + Vector3.up * 0.2f;
            }
            OWMap.Places["Jewelry"] = c + new Vector3(0, 0.2f, -18);
        }

        static void GasStation(MapBuilder m, Vector3 c)
        {
            Room(m, c + new Vector3(4, 0.15f, 6), c + new Vector3(20, 4.2f, 20), TextureFactory.Plaster, new Color(0.9f, 0.9f, 0.86f), c.x + 12f, 3f);
            m.Block("Canopy", c + new Vector3(-20, 5f, -16), c + new Vector3(4, 5.4f, 4), Mats.LitMetal, SurfaceType.Metal, new Color(0.85f, 0.15f, 0.1f));
            for (int k = 0; k < 4; k++)
            {
                m.Box("Pump", c + new Vector3(-14 + (k % 2) * 12, 0.9f, -10 + (k / 2) * 10), Quaternion.identity, new Vector3(0.8f, 1.6f, 0.6f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.9f, 0.9f));
                m.Cylinder("Pump Pillar", c + new Vector3(-20 + (k % 2) * 24, 0.15f, -16 + (k / 2) * 20), 0.25f, 5f, Mats.LitMetal, SurfaceType.Metal, new Color(0.7f, 0.7f, 0.7f), 8);
            }
            m.Box("Counter", c + new Vector3(12, 0.7f, 15), Quaternion.identity, new Vector3(5f, 1.1f, 1f), Mats.Lit, SurfaceType.Wood, new Color(0.5f, 0.35f, 0.2f));
            m.Box("Sign", c + new Vector3(-8, 5.7f, -16.3f), Quaternion.identity, new Vector3(10f, 0.8f, 0.1f), Mats.Glow(new Color(1f, 0.85f, 0.2f), 2f), SurfaceType.Glass, Color.white, 1f, false);
            OWMap.Places["Gas Station"] = c + new Vector3(-6, 0.2f, -6);
            OWMap.Places["Gas Register"] = c + new Vector3(12, 0.2f, 13.5f);
            PropFactory.Spawn("explosive_barrel", c + new Vector3(-18, 0.6f, 6), Quaternion.identity);
            PropFactory.Spawn("propane", c + new Vector3(-17, 0.6f, 7.5f), Quaternion.identity);
        }

        static void Hangout(MapBuilder m, Vector3 c)
        {
            Tower(m, c + new Vector3(-20, 0.15f, 4), c + new Vector3(20, 9f, 20), TextureFactory.Brick, new Color(0.55f, 0.35f, 0.3f));
            m.Block("Court", c + new Vector3(-20, 0.15f, -20), c + new Vector3(20, 0.18f, 2), MapBuilder.Tex(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.3f, 0.32f, 0.4f), 0.3f, false);
            m.Box("Hoop", c + new Vector3(0, 3f, -19.5f), Quaternion.identity, new Vector3(1.8f, 1.2f, 0.1f), Mats.Lit, SurfaceType.Wood, Color.white);
            m.Box("Heist Board", c + new Vector3(6, 1.8f, 3.8f), Quaternion.identity, new Vector3(4f, 2.4f, 0.12f), Mats.Lit, SurfaceType.Wood, new Color(0.35f, 0.25f, 0.15f));
            m.Box("Heist Board Glow", c + new Vector3(6, 1.8f, 3.72f), Quaternion.identity, new Vector3(3.6f, 2f, 0.02f), Mats.Glow(new Color(1f, 0.78f, 0.16f), 0.6f), SurfaceType.Glass, Color.white, 1f, false);
            OWPoint.Make(m.Root, "heists", "Heist board", c + new Vector3(6, 1.4f, 2.6f), new Vector3(4f, 2.8f, 2f));
            OWMap.Places["Heist Board"] = c + new Vector3(6, 0.2f, 2f);
            OWMap.Places["Marcus"] = c + new Vector3(2, 0.17f, 1.5f);
            m.CeilingLight(c + new Vector3(6, 3.6f, 3.6f), new Color(1f, 0.78f, 0.4f), 10f, 1.6f, new Vector3(1f, 0.1f, 0.3f));
        }

        static void Hideout(MapBuilder m, Vector3 c)
        {
            Tower(m, c + new Vector3(-20, 0.15f, -20), c + new Vector3(-4, 6f, 20), TextureFactory.Brick, new Color(0.4f, 0.3f, 0.28f));
            Tower(m, c + new Vector3(4, 0.15f, 4), c + new Vector3(20, 6f, 20), TextureFactory.Brick, new Color(0.45f, 0.35f, 0.3f));
            for (int k = 0; k < 6; k++) PropFactory.Spawn(k % 3 == 0 ? "barrel" : "crate", c + new Vector3(6 + (k % 3) * 3, 0.6f, -12 + (k / 3) * 3), Quaternion.Euler(0, k * 30, 0));
            m.Fence(c + new Vector3(4, 0.15f, -22), c + new Vector3(22, 0.15f, -22), 2.2f);
            OWMap.Places["Gang Hideout"] = c + new Vector3(10, 0.2f, -6);
            OWMap.Places["Escape Southside"] = c + new Vector3(0, 0.2f, -6);
        }

        static void Hospital(MapBuilder m, Vector3 c)
        {
            Tower(m, c + new Vector3(-20, 0.15f, -10), c + new Vector3(20, 22f, 20), TextureFactory.Plaster, new Color(0.92f, 0.92f, 0.95f));
            m.Box("Hospital Sign", c + new Vector3(0, 8f, -10.4f), Quaternion.identity, new Vector3(4f, 4f, 0.15f), Mats.Glow(new Color(1f, 0.15f, 0.15f), 2f), SurfaceType.Glass, Color.white, 1f, false);
            m.Box("Hospital Sign", c + new Vector3(0, 8f, -10.45f), Quaternion.identity, new Vector3(1.2f, 3f, 0.1f), Mats.Glow(Color.white, 2f), SurfaceType.Glass, Color.white, 1f, false);
            m.Box("Hospital Sign", c + new Vector3(0, 8f, -10.45f), Quaternion.identity, new Vector3(3f, 1.2f, 0.1f), Mats.Glow(Color.white, 2f), SurfaceType.Glass, Color.white, 1f, false);
            for (int k = 0; k < 4; k++) OWMap.Hospital.Add(c + new Vector3(-6 + k * 4, 0.3f, -16));
            OWMap.Places["Hospital"] = c + new Vector3(0, 0.2f, -16);
            m.Car(c + new Vector3(-14, 0.15f, -16), 90f, Color.white);
        }

        static void Police(MapBuilder m, Vector3 c)
        {
            Tower(m, c + new Vector3(-20, 0.15f, -6), c + new Vector3(20, 14f, 20), TextureFactory.Concrete, new Color(0.35f, 0.4f, 0.55f));
            m.Box("Police Sign", c + new Vector3(0, 9f, -6.4f), Quaternion.identity, new Vector3(14f, 1.4f, 0.15f), Mats.Glow(new Color(0.2f, 0.45f, 1f), 2.2f), SurfaceType.Glass, Color.white, 1f, false);
            for (int k = 0; k < 3; k++) m.Car(c + new Vector3(-12 + k * 9, 0.15f, -14), 0f, new Color(0.92f, 0.92f, 0.95f));
            OWMap.Places["Police Station"] = c + new Vector3(0, 0.2f, -12);
        }

        static void Mansion(MapBuilder m, Vector3 c)
        {
            m.Block("Lawn", c + new Vector3(-23, 0.15f, -23), c + new Vector3(23, 0.2f, 23), MapBuilder.Tex(TextureFactory.Grass), SurfaceType.Grass, new Color(0.35f, 0.6f, 0.3f), 0.3f);
            Tower(m, c + new Vector3(-14, 0.2f, -2), c + new Vector3(14, 10f, 16), TextureFactory.Plaster, new Color(0.95f, 0.92f, 0.86f));
            m.Box("Pool", c + new Vector3(0, 0.22f, -12), Quaternion.identity, new Vector3(12f, 0.05f, 6f), Mats.Lit, SurfaceType.Glass, new Color(0.2f, 0.6f, 0.9f), 1f, false);
            for (int k = 0; k < 4; k++) m.Tree(c + new Vector3(-20 + k * 13, 0.2f, -20), 1.2f, false);
            Parked(m, c + new Vector3(18, 0.2f, -12), 0f);
        }

        static void Meridian(MapBuilder m, Vector3 c)
        {
            Tower(m, c + new Vector3(-16, 0.15f, -10), c + new Vector3(16, 64f, 20), TextureFactory.Concrete, new Color(0.25f, 0.28f, 0.35f));
            m.Box("Meridian Sign", c + new Vector3(0, 6f, -10.4f), Quaternion.identity, new Vector3(16f, 1.6f, 0.15f), Mats.Glow(new Color(1f, 0.85f, 0.5f), 2f), SurfaceType.Glass, Color.white, 1f, false);
            m.Box("Lobby Glass", c + new Vector3(0, 2.2f, -10.08f), Quaternion.identity, new Vector3(10f, 3.6f, 0.08f), Mats.Glow(new Color(0.9f, 0.85f, 0.7f), 0.9f), SurfaceType.Glass, Color.white, 1f, false);
            OWMap.ApartmentOutside = c + new Vector3(0, 0.3f, -14f);
            OWPoint.Make(m.Root, "apartment", "THE MERIDIAN (your penthouse)", c + new Vector3(0, 1.2f, -11.5f), new Vector3(5f, 2.6f, 2f));
            OWMap.Places["The Meridian"] = OWMap.ApartmentOutside;
            OWMap.GarageSpot = c + new Vector3(-28f, 0.5f, -14f);
        }

        static void Scrapyard(MapBuilder m, Vector3 c)
        {
            m.Block("Dirt", c + new Vector3(-23, 0.15f, -23), c + new Vector3(23, 0.2f, 23), MapBuilder.Tex(TextureFactory.Dirt), SurfaceType.Dirt, new Color(0.5f, 0.45f, 0.38f), 0.3f);
            for (int k = 0; k < 10; k++)
                m.Box("Wreck", c + new Vector3((float)rnd.NextDouble() * 36 - 18, 0.8f + (k % 3) * 1.1f, (float)rnd.NextDouble() * 36 - 18), Quaternion.Euler((float)rnd.NextDouble() * 30, (float)rnd.NextDouble() * 360, (float)rnd.NextDouble() * 30), new Vector3(1.8f, 1.1f, 4f), Mats.LitMetal, SurfaceType.Metal, Tints[k % Tints.Length] * 0.6f);
            m.Fence(c + new Vector3(-23, 0.2f, 23), c + new Vector3(23, 0.2f, 23), 2.4f);
            OWMap.Places["Scrapyard"] = c + new Vector3(0, 0.25f, -20);
        }

        static void Lot(MapBuilder m, Vector3 c, string name)
        {
            m.Block("Lot", c + new Vector3(-23, 0.15f, -23), c + new Vector3(23, 0.18f, 23), MapBuilder.Tex(TextureFactory.Asphalt), SurfaceType.Concrete, new Color(0.35f, 0.35f, 0.38f), 0.3f, false);
            for (int k = 0; k < 8; k++) m.Box("Checker", c + new Vector3(-7 + k * 2, 0.19f, -20), Quaternion.identity, new Vector3(1f, 0.01f, 1f), Mats.Lit, SurfaceType.Concrete, k % 2 == 0 ? Color.white : Color.black, 1f, false);
            OWMap.Places[name] = c + new Vector3(0, 0.25f, -18);
            OWPoint.Make(m.Root, "race", "Street race (needs a car)", c + new Vector3(12f, 1.3f, -18), new Vector3(2f, 2.2f, 2f)); // beside the line, not on it (wheels)
            OWMap.CarSpawns.Add(c + new Vector3(-12, 0.5f, -10));
        }

        static void DocksYard(MapBuilder m, Vector3 c, bool heist)
        {
            m.Block("Quay", c + new Vector3(-23, 0.15f, -23), c + new Vector3(30, 0.2f, 23), MapBuilder.Tex(TextureFactory.Concrete), SurfaceType.Concrete, new Color(0.5f, 0.5f, 0.5f), 0.3f);
            Color[] paint = { new Color(0.62f, 0.16f, 0.12f), new Color(0.15f, 0.4f, 0.22f), new Color(0.75f, 0.5f, 0.12f), new Color(0.2f, 0.25f, 0.45f) };
            for (int k = 0; k < 8; k++)
            {
                var p = c + new Vector3(-16 + (k % 4) * 9, 0.2f, -12 + (k / 4) * 16);
                int h = 1 + rnd.Next(2);
                for (int s = 0; s < h; s++) m.Box("Container", p + Vector3.up * (1.3f + s * 2.6f), Quaternion.identity, new Vector3(2.44f, 2.6f, 6.06f), Mats.LitMetal, SurfaceType.Metal, paint[(k + s) % paint.Length]);
            }
            if (heist)
            {
                var target = c + new Vector3(20, 0.2f, 0);
                m.Box("Shipment Container", target + Vector3.up * 1.3f, Quaternion.identity, new Vector3(2.44f, 2.6f, 6.06f), Mats.LitMetal, SurfaceType.Metal, new Color(0.12f, 0.3f, 0.7f));
                OWMap.Places["Docks"] = c + new Vector3(-20, 0.25f, -20);
                OWMap.Places["Docks Shipment"] = target + new Vector3(0, 0, -4f);
                OWMap.Places["Docks Boat"] = c + new Vector3(29, 0.25f, 18);
                m.Box("Boat", c + new Vector3(36, 0.4f, 18), Quaternion.identity, new Vector3(4f, 1.4f, 9f), Mats.LitMetal, SurfaceType.Metal, new Color(0.9f, 0.9f, 0.92f), 1f, false);
            }
            m.StreetLamp(c + new Vector3(0, 0.2f, 0), 0f, new Color(0.85f, 0.9f, 1f), 9f, 26f, 2.4f);
        }

        // ------------------------------------------------------------------ the penthouse (underneath the city, reached by its door)

        static void Apartment(MapBuilder m)
        {
            var o = new Vector3(0, -60f, 0);
            var b = Room(m, o + new Vector3(-12, 0, -8), o + new Vector3(12, 5f, 12), TextureFactory.Wallpaper, new Color(0.25f, 0.22f, 0.3f), o.x, 3f);
            m.Block("Carpet", o + new Vector3(-11.6f, 0.15f, -7.6f), o + new Vector3(11.6f, 0.17f, 11.6f), MapBuilder.Tex(TextureFactory.Carpet), SurfaceType.Carpet, new Color(0.35f, 0.15f, 0.2f), 0.5f, false);
            m.Block("Door Plug", o + new Vector3(-2, 0, -9f), o + new Vector3(2, 5f, -8f), MapBuilder.Tex(TextureFactory.Wallpaper), SurfaceType.Concrete, new Color(0.25f, 0.22f, 0.3f));
            m.Box("Window View", o + new Vector3(0, 2.6f, 11.55f), Quaternion.identity, new Vector3(18f, 3.4f, 0.05f), Mats.Glow(new Color(0.95f, 0.55f, 0.3f), 0.9f), SurfaceType.Glass, Color.white, 1f, false);
            OWMap.ApartmentInside = o + new Vector3(0, 0.3f, -5.5f);
            OWPoint.Make(m.Root, "exit", "Leave the penthouse", o + new Vector3(0, 1.2f, -7.4f), new Vector3(3f, 2.6f, 1.2f));
            OWPoint.Make(m.Root, "locker", "Locker (outfits, masks, skins)", o + new Vector3(-10.8f, 1.2f, 4f), new Vector3(1.4f, 2.4f, 3f));
            m.Box("Locker", o + new Vector3(-11.3f, 1.2f, 4f), Quaternion.identity, new Vector3(0.6f, 2.4f, 2.6f), Mats.LitMetal, SurfaceType.Metal, new Color(0.3f, 0.32f, 0.36f));
            OWPoint.Make(m.Root, "garage", "Garage (pick and call your car)", o + new Vector3(10.8f, 1.2f, 4f), new Vector3(1.4f, 2.4f, 3f));
            m.Box("Garage Door", o + new Vector3(11.4f, 1.6f, 4f), Quaternion.identity, new Vector3(0.2f, 3f, 3f), Mats.LitMetal, SurfaceType.Metal, new Color(0.55f, 0.5f, 0.4f));
            OWMap.Places["Penthouse"] = OWMap.ApartmentInside;
            // Furniture slots (shown by OWDirector when you own them): positions only.
            OWApartment.Slots = new[]
            {
                o + new Vector3(0, 0, 0),            // 0 apartment (nothing)
                o + new Vector3(-11.6f, 2.2f, -3f),  // 1 gun wall
                o + new Vector3(-6f, 0.15f, 10.5f),  // 2 trophy case
                o + new Vector3(6f, 3.4f, 11.5f),    // 3 neon sign
                o + new Vector3(0f, 0.15f, 4f),      // 4 pool table
                o + new Vector3(11.5f, 2.2f, -3f),   // 5 TV
                o + new Vector3(-8f, 0.15f, -5f),    // 6 statue
                o + new Vector3(8f, 0.15f, 9.5f),    // 7 aquarium
                o + new Vector3(6f, 0.15f, -2f),     // 8 sofa
                o + new Vector3(-3f, 0.15f, 9f),     // 9 cash pile
            };
        }

        // ------------------------------------------------------------------ thumbnail

        static Texture2D Thumb()
        {
            const int W = 384, H = 216;
            var c = new PixelCanvas(W, H, Color.black);
            for (int y = 0; y < H; y++)
            {
                var col = Color.Lerp(new Color(0.95f, 0.5f, 0.2f), new Color(0.15f, 0.08f, 0.3f), y / (float)H);
                for (int x = 0; x < W; x++) c.Set(x, y, col);
            }
            var r = new System.Random(7);
            for (int i = 0; i < 26; i++)
            {
                int w = 10 + r.Next(22), h = 30 + r.Next(130), x = r.Next(W);
                var col = new Color(0.08f, 0.06f, 0.12f) * (0.8f + (float)r.NextDouble() * 0.4f);
                c.Rect(x, 0, w, h, new Color(col.r, col.g, col.b, 1f));
                for (int wy = 6; wy < h - 4; wy += 7) for (int wx = 2; wx < w - 2; wx += 4) if (r.NextDouble() < 0.4) c.Rect(x + wx, wy, 2, 3, new Color(1f, 0.82f, 0.5f));
            }
            c.Rect(0, 0, W, 14, new Color(0.15f, 0.15f, 0.17f));
            return c.ToTexture("Thumb_" + OWMap.MapId);
        }
    }
}
