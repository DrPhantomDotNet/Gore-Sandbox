using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// "Arena": a round gladiator arena with a sand floor, a stone wall, tiered stands, four gates where the horde
    /// enters and lamps around the rim. Builds a <see cref="HordeMode"/> on its root (endless waves: 1, 2, 4, 8 ...).
    /// </summary>
    public static class ArenaMap
    {
        public const string MapId = "horde_arena";
        public const float Radius = 22f;
        const int Gates = 4;

        public static MapDef CreateDef()
        {
            Zombies.EnsureAchievements();
            return new MapDef
            {
                Id = MapId, Name = "Arena", Size = "Medium · round arena", Glyph = "skull", DefaultTime = TimeOfDay.Sunset, Ambience = "amb_wind",
                Description = "Endless horde mode. Every round brings twice as many enemies as the last - zombies or hostile Goredolls, picked at random. You start with one. How long can you last?",
                Features = new[] { "Endless horde mode", "Zombies or Goredolls", "Rounds double", "Guns provided", "Best round saved" },
                Tags = new[] { "Horde Mode" },
                ThemeTop = new Color(0.55f, 0.32f, 0.16f), ThemeBottom = new Color(0.85f, 0.7f, 0.45f), Accent = new Color(1f, 0.6f, 0.25f),
                Build = Build, Thumbnail = Thumb(),
            };
        }

        static float GateAngle(int i) => i * (360f / Gates) + 45f;

        static bool NearGate(float angDeg, float halfWidthDeg)
        {
            for (int i = 0; i < Gates; i++)
                if (Mathf.Abs(Mathf.DeltaAngle(angDeg, GateAngle(i))) < halfWidthDeg) return true;
            return false;
        }

        static void Build(MapBuilder m)
        {
            m.Options.StartingNpcs = 0; // the horde is the only company here
            m.Options.Nextbots = false;
            var sandMat = MapBuilder.Tex(TextureFactory.Sand);
            var stone = MapBuilder.Tex(TextureFactory.Brick);
            var conc = MapBuilder.Tex(TextureFactory.Concrete);
            var wallTint = new Color(0.82f, 0.7f, 0.55f);
            var standTint = new Color(0.62f, 0.56f, 0.48f);

            m.Block("Arena Sand", new Vector3(-40, -1, -40), new Vector3(40, 0, 40), sandMat, SurfaceType.Dirt, new Color(0.95f, 0.85f, 0.65f), 0.35f);

            // Inner wall ring with 4 gate gaps.
            const int segs = 48;
            float segLen = 2f * Mathf.PI * (Radius + 0.4f) / segs + 0.1f;
            for (int i = 0; i < segs; i++)
            {
                float ang = (i + 0.5f) * 360f / segs;
                if (NearGate(ang, 5.5f)) continue;
                var dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                m.Box("Arena Wall", dir * (Radius + 0.4f) + Vector3.up * 2f, Quaternion.Euler(0, ang, 0), new Vector3(segLen, 4f, 0.8f), stone, SurfaceType.Concrete, wallTint);
                // Blood-stained skirting.
                m.Box("Wall Skirt", dir * (Radius - 0.05f) + Vector3.up * 0.3f, Quaternion.Euler(0, ang, 0), new Vector3(segLen, 0.6f, 0.12f), conc, SurfaceType.Concrete, new Color(0.45f, 0.2f, 0.16f), 0.5f, false);
            }

            // Tiered stands behind the wall (outside the playable fighting area, but solid).
            for (int tier = 0; tier < 5; tier++)
            {
                float r = Radius + 1.6f + tier * 1.4f;
                float h = 4.4f + tier * 1.2f;
                float len = 2f * Mathf.PI * r / segs + 0.15f;
                for (int i = 0; i < segs; i++)
                {
                    float ang = (i + 0.5f) * 360f / segs;
                    if (NearGate(ang, 6.5f)) continue;
                    var dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                    m.Box("Stand", dir * r + Vector3.up * (h * 0.5f), Quaternion.Euler(0, ang, 0), new Vector3(len, h, 1.45f), conc, SurfaceType.Concrete, standTint * (0.9f + 0.05f * (tier % 2)));
                }
            }
            // Outer wall.
            float outerR = Radius + 1.6f + 5 * 1.4f + 0.6f;
            for (int i = 0; i < segs; i++)
            {
                float ang = (i + 0.5f) * 360f / segs;
                var dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                float len = 2f * Mathf.PI * outerR / segs + 0.2f;
                m.Box("Outer Wall", dir * outerR + Vector3.up * 6f, Quaternion.Euler(0, ang, 0), new Vector3(len, 12f, 1f), stone, SurfaceType.Concrete, wallTint * 0.9f);
            }

            // Gates: a corridor through the stands, an arch on the arena wall and a torch-lit tunnel mouth.
            for (int g = 0; g < Gates; g++)
            {
                float ang = GateAngle(g);
                var rot = Quaternion.Euler(0, ang, 0);
                var dir = rot * Vector3.forward;
                var right = rot * Vector3.right;
                float inner = Radius - 0.2f, outer = outerR - 0.5f;
                float mid = (inner + outer) * 0.5f, len = outer - inner;
                m.Box("Gate Corridor L", dir * mid - right * 2.6f + Vector3.up * 3f, rot, new Vector3(0.6f, 6f, len), stone, SurfaceType.Concrete, wallTint * 0.8f);
                m.Box("Gate Corridor R", dir * mid + right * 2.6f + Vector3.up * 3f, rot, new Vector3(0.6f, 6f, len), stone, SurfaceType.Concrete, wallTint * 0.8f);
                m.Box("Gate Roof", dir * mid + Vector3.up * 6.2f, rot, new Vector3(5.8f, 0.5f, len), stone, SurfaceType.Concrete, wallTint * 0.7f);
                m.Box("Gate Arch", dir * (Radius + 0.4f) + Vector3.up * 5f, rot, new Vector3(6.4f, 2f, 1f), stone, SurfaceType.Concrete, new Color(0.5f, 0.3f, 0.22f));
                m.Box("Gate Pillar L", dir * (Radius + 0.1f) - right * 3f + Vector3.up * 2.5f, rot, new Vector3(0.9f, 5f, 1.2f), stone, SurfaceType.Concrete, new Color(0.55f, 0.35f, 0.25f));
                m.Box("Gate Pillar R", dir * (Radius + 0.1f) + right * 3f + Vector3.up * 2.5f, rot, new Vector3(0.9f, 5f, 1.2f), stone, SurfaceType.Concrete, new Color(0.55f, 0.35f, 0.25f));
                // Raised portcullis (decoration only).
                for (int b = -2; b <= 2; b++)
                    m.Box("Portcullis Bar", dir * (Radius + 0.4f) + right * (b * 1f) + Vector3.up * 5.4f, rot, new Vector3(0.1f, 1.2f, 0.1f), Mats.LitMetal, SurfaceType.Metal, new Color(0.2f, 0.2f, 0.22f), 0.5f, false);
                m.CeilingLight(dir * (Radius + 3f) + Vector3.up * 5.8f, new Color(1f, 0.55f, 0.25f), 9f, 1.4f);
                HordeSpawns.Add(dir * (outer - 1.5f) + Vector3.up * 0.05f);
                m.NpcSpawns.Add(dir * (Radius - 2f));
            }

            // Cover pillars and a central dais.
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f + 15f;
                var p = Quaternion.Euler(0, ang, 0) * Vector3.forward * 11f;
                m.Cylinder("Pillar", p, 0.7f, 3.2f, stone, SurfaceType.Concrete, wallTint, 12);
                if (i % 2 == 0) m.Box("Broken Pillar", p + Quaternion.Euler(0, ang + 90, 0) * Vector3.forward * 1.6f + Vector3.up * 0.35f, Quaternion.Euler(0, ang + 30f, 85f), new Vector3(0.8f, 2.4f, 0.8f), stone, SurfaceType.Concrete, wallTint * 0.9f);
            }
            m.Cylinder("Dais", new Vector3(0, 0, 0), 2.6f, 0.25f, conc, SurfaceType.Concrete, new Color(0.7f, 0.62f, 0.5f), 24);

            // Lamps around the rim (inside the wall), facing the centre.
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f;
                if (NearGate(ang, 8f)) continue;
                var p = Quaternion.Euler(0, ang, 0) * Vector3.forward * (Radius - 0.9f);
                m.StreetLamp(p, ang + 180f, new Color(1f, 0.82f, 0.6f), 6f, 22f, 2.4f);
            }

            m.PlayerSpawn = new Vector3(0, 0.35f, -4f);
            m.PlayerYaw = 0f;
            m.KillY = -30f;
            m.Playable = new Bounds(new Vector3(0, 6f, 0), new Vector3(outerR * 2f + 2f, 16f, outerR * 2f + 2f));
            m.Boundaries(new Bounds(new Vector3(0, 12f, 0), new Vector3(outerR * 2f + 1f, 30f, outerR * 2f + 1f)));

            var horde = m.Root.gameObject.AddComponent<HordeMode>();
            horde.Spawns.AddRange(HordeSpawns);
            HordeSpawns.Clear();
        }

        static readonly List<Vector3> HordeSpawns = new List<Vector3>();

        static Texture2D Thumb()
        {
            const int W = 384, H = 216;
            var c = new PixelCanvas(W, H, Color.black);
            for (int y = 0; y < H; y++)
            {
                var col = Color.Lerp(new Color(0.85f, 0.55f, 0.3f), new Color(0.35f, 0.18f, 0.2f), y / (float)H);
                for (int x = 0; x < W; x++) c.Set(x, y, col);
            }
            c.Circle(300, 160, 24, new Color(1f, 0.85f, 0.55f), 6);
            var sand = new Color(0.9f, 0.76f, 0.5f);
            var stone = new Color(0.45f, 0.33f, 0.24f);
            // Stands (back), sand floor ellipse, wall.
            c.Rect(0, 70, W, 70, stone * 0.8f);
            for (int i = 0; i < 5; i++) c.Rect(0, 80 + i * 12, W, 3, stone * 0.6f);
            for (int x = 10; x < W; x += 9) c.Circle(x, 136 - (x * 7 % 30) * 0.2f, 2.2f, new Color(0.2f, 0.15f, 0.12f), 1);
            c.Rect(0, 0, W, 72, sand);
            c.Rect(0, 66, W, 10, stone);
            // Gate arch
            c.Rect(170, 66, 44, 46, new Color(0.08f, 0.05f, 0.04f));
            c.Rect(164, 108, 56, 8, stone * 0.7f);
            // Horde silhouettes
            var sil = new Color(0.12f, 0.16f, 0.1f);
            for (int i = 0; i < 9; i++)
            {
                float x = 60 + i * 32 + (i % 2) * 8, h = 34 + (i * 13 % 10);
                c.Rect(x - 5, 20, 10, h, sil);
                c.Circle(x, 20 + h + 6, 6, sil, 1);
                c.Line(x, 20 + h - 6, x + 14, 20 + h - 4, 3, sil);
            }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float vx = (x - W / 2f) / (W / 2f), vy = (y - H / 2f) / (H / 2f);
                    float v = Mathf.Clamp01(1f - (vx * vx + vy * vy) * 0.35f);
                    var p = c.Px[y * W + x]; p.r *= v; p.g *= v; p.b *= v; c.Px[y * W + x] = p;
                }
            return c.ToTexture("Thumb_" + MapId);
        }
    }
}
