using UnityEngine;

namespace SolarEclipse
{
    /// <summary>All textures are generated procedurally on first use (no image assets in the project).</summary>
    public static class TextureFactory
    {
        static Texture2D softDot, smoke, spark, bloodAtlas, holeAtlas, woundAtlas, fleshCap, white;
        static Texture2D grass, concrete, asphalt, tiles, wood, metal, brick, carpet, wallpaper, dirt, ceiling, sand, plaster;
        static Texture2D roundRect, roundRectSmall, circle, ring, gradient, glow, vignette, grain, shadow, corona, star4, streak;

        public const int BloodCells = 4;   // 4x4 atlas
        public const int HoleCells = 2;    // 2x2 atlas
        public const int WoundCells = 4;   // 4x4 atlas

        public static Texture2D White
        {
            get
            {
                if (white == null) { white = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "White" }; var p = new Color[16]; for (int i = 0; i < 16; i++) p[i] = Color.white; white.SetPixels(p); white.Apply(); }
                return white;
            }
        }

        public static Texture2D SoftDot => softDot ??= Radial(64, "SoftDot", d => Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f));
        public static Texture2D Glow => glow ??= Radial(128, "Glow", d => Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f));
        public static Texture2D Circle => circle ??= Radial(128, "Circle", d => Mathf.Clamp01((1f - d) * 64f));
        public static Texture2D Ring => ring ??= Radial(128, "Ring", d => Mathf.Clamp01((0.06f - Mathf.Abs(d - 0.9f)) * 40f));

        static Texture2D Radial(int size, string name, System.Func<float, float> alpha)
        {
            var c = new PixelCanvas(size, size, new Color(1, 1, 1, 0));
            float h = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - h) * (x - h) + (y - h) * (y - h)) / h;
                    c.Set(x, y, new Color(1, 1, 1, alpha(d)));
                }
            return c.ToTexture(name);
        }

        public static Texture2D Smoke
        {
            get
            {
                if (smoke != null) return smoke;
                var c = new PixelCanvas(128, 128, new Color(1, 1, 1, 0));
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 128; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 63.5f;
                        float n = Noise.Fbm(x * 0.06f, y * 0.06f, 4, 7);
                        float a = Mathf.Clamp01((1f - d) * 1.6f) * Mathf.Clamp01(n * 1.6f - 0.3f);
                        c.Set(x, y, new Color(1, 1, 1, a));
                    }
                return smoke = c.ToTexture("Smoke");
            }
        }

        public static Texture2D Spark
        {
            get
            {
                if (spark != null) return spark;
                var c = new PixelCanvas(64, 16, new Color(1, 1, 1, 0));
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float fx = Mathf.Abs(x - 31.5f) / 31.5f, fy = Mathf.Abs(y - 7.5f) / 7.5f;
                        c.Set(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - fx) * Mathf.Clamp01(1f - fy * fy)));
                    }
                return spark = c.ToTexture("Spark");
            }
        }

        // ------------------------------------------------------------------ blood

        /// <summary>4x4 atlas: 0-5 splats, 6-9 drips/streaks, 10-12 pools, 13-15 droplets. White, tinted by vertex colour.</summary>
        public static Texture2D BloodAtlas
        {
            get
            {
                if (bloodAtlas != null) return bloodAtlas;
                const int cell = 256;
                var c = new PixelCanvas(cell * BloodCells, cell * BloodCells, new Color(1, 1, 1, 0));
                var rng = new System.Random(1337);
                for (int i = 0; i < BloodCells * BloodCells; i++)
                {
                    int ox = (i % BloodCells) * cell, oy = (i / BloodCells) * cell;
                    if (i <= 5) Splat(c, ox, oy, cell, rng, 0.32f + (float)rng.NextDouble() * 0.12f, 14 + rng.Next(18), i);
                    else if (i <= 9) DripStreak(c, ox, oy, cell, rng, i);
                    else if (i <= 12) Pool(c, ox, oy, cell, rng, i);
                    else Droplets(c, ox, oy, cell, rng);
                }
                ShadeBlood(c);
                return bloodAtlas = c.ToTexture("BloodAtlas");
            }
        }

        static void Splat(PixelCanvas c, int ox, int oy, int cell, System.Random rng, float radius, int droplets, int seed)
        {
            float cx = ox + cell * 0.5f, cy = oy + cell * 0.5f, R = cell * radius;
            for (int y = oy; y < oy + cell; y++)
                for (int x = ox; x < ox + cell; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float ang = Mathf.Atan2(dy, dx);
                    float edge = R * (0.75f + 0.45f * Noise.Fbm(Mathf.Cos(ang) * 2.2f + seed * 7, Mathf.Sin(ang) * 2.2f + seed * 3, 4, seed));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((edge - d) / 2.5f);
                    if (a > 0) c.Max(x, y, new Color(1, 1, 1, a));
                }
            for (int k = 0; k < droplets; k++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = R * (0.95f + (float)rng.NextDouble() * 0.75f);
                float r = R * (0.03f + (float)rng.NextDouble() * 0.09f);
                // Streak outward from the main blob: a row of shrinking dots.
                int steps = 3 + rng.Next(5);
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    float px = cx + Mathf.Cos(ang) * Mathf.Lerp(R * 0.6f, dist, t);
                    float py = cy + Mathf.Sin(ang) * Mathf.Lerp(R * 0.6f, dist, t);
                    if (px - r < ox || px + r > ox + cell || py - r < oy || py + r > oy + cell) continue;
                    c.Circle(px, py, Mathf.Lerp(r * 1.4f, r * 0.6f, t), Color.white, 2f, true);
                }
                float fx = cx + Mathf.Cos(ang) * dist, fy = cy + Mathf.Sin(ang) * dist;
                if (fx - r > ox && fx + r < ox + cell && fy - r > oy && fy + r < oy + cell) c.Circle(fx, fy, r, Color.white, 2f, true);
            }
        }

        static void DripStreak(PixelCanvas c, int ox, int oy, int cell, System.Random rng, int seed)
        {
            // Vertical drip streaks for walls (y up in texture = up on wall).
            float cx = ox + cell * 0.5f;
            float top = oy + cell * 0.85f;
            c.Circle(cx, top, cell * 0.14f, Color.white, 3f, true);
            int drips = 2 + rng.Next(4);
            for (int d = 0; d < drips; d++)
            {
                float x = cx + ((float)rng.NextDouble() - 0.5f) * cell * 0.22f;
                float len = cell * (0.35f + (float)rng.NextDouble() * 0.45f);
                float w = cell * (0.015f + (float)rng.NextDouble() * 0.025f);
                for (float y = top; y > top - len; y -= 1f)
                {
                    float t = (top - y) / len;
                    float wob = (Noise.Value(y * 0.05f, d * 3.1f, seed) - 0.5f) * 4f;
                    c.Circle(x + wob, y, w * (1f - t * 0.4f), Color.white, 1.5f, true);
                }
                c.Circle(x, top - len, w * 1.6f, Color.white, 2f, true);
            }
        }

        static void Pool(PixelCanvas c, int ox, int oy, int cell, System.Random rng, int seed)
        {
            float cx = ox + cell * 0.5f, cy = oy + cell * 0.5f, R = cell * 0.42f;
            for (int y = oy; y < oy + cell; y++)
                for (int x = ox; x < ox + cell; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float ang = Mathf.Atan2(dy, dx);
                    float edge = R * (0.8f + 0.2f * Noise.Fbm(Mathf.Cos(ang) * 1.5f + seed, Mathf.Sin(ang) * 1.5f, 3, seed));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((edge - d) / 4f);
                    if (a > 0) c.Max(x, y, new Color(1, 1, 1, a));
                }
        }

        static void Droplets(PixelCanvas c, int ox, int oy, int cell, System.Random rng)
        {
            int n = 18 + rng.Next(20);
            for (int k = 0; k < n; k++)
            {
                float r = cell * (0.01f + (float)rng.NextDouble() * 0.035f);
                float x = ox + r + 4 + (float)rng.NextDouble() * (cell - 2 * r - 8);
                float y = oy + r + 4 + (float)rng.NextDouble() * (cell - 2 * r - 8);
                c.Circle(x, y, r, Color.white, 1.5f, true);
            }
        }

        /// <summary>Turns white masks into wet-looking blood: darker dried rim, subtle lighter core.</summary>
        static void ShadeBlood(PixelCanvas c)
        {
            for (int y = 0; y < c.H; y++)
                for (int x = 0; x < c.W; x++)
                {
                    var p = c.Px[y * c.W + x];
                    if (p.a <= 0) continue;
                    float n = Noise.Fbm(x * 0.03f, y * 0.03f, 3, 99);
                    float edge = 1f - Mathf.Clamp01(p.a * 1.2f);
                    float lum = Mathf.Lerp(0.85f, 1.05f, n) * Mathf.Lerp(1f, 0.55f, edge);
                    c.Px[y * c.W + x] = new Color(lum, lum, lum, Mathf.Clamp01(p.a * 1.15f) * 0.96f);
                }
        }

        // ------------------------------------------------------------------ bullet holes

        /// <summary>2x2 atlas: concrete, metal, wood, generic scorch.</summary>
        public static Texture2D HoleAtlas
        {
            get
            {
                if (holeAtlas != null) return holeAtlas;
                const int cell = 128;
                var c = new PixelCanvas(cell * 2, cell * 2, new Color(0, 0, 0, 0));
                var rng = new System.Random(77);
                for (int i = 0; i < 4; i++)
                {
                    float cx = (i % 2) * cell + cell * 0.5f, cy = (i / 2) * cell + cell * 0.5f;
                    Color ringCol = i == 0 ? new Color(0.45f, 0.44f, 0.42f) : i == 1 ? new Color(0.75f, 0.75f, 0.78f) : i == 2 ? new Color(0.45f, 0.3f, 0.17f) : new Color(0.12f, 0.1f, 0.08f);
                    // chipped ring
                    for (int k = 0; k < 24; k++)
                    {
                        float ang = k / 24f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
                        float len = cell * (0.12f + (float)rng.NextDouble() * (i == 2 ? 0.3f : 0.14f));
                        c.Line(cx, cy, cx + Mathf.Cos(ang) * len, cy + Mathf.Sin(ang) * len, i == 2 ? 3f : 2f, ringCol.WithAlpha(0.7f), 2f);
                    }
                    c.Circle(cx, cy, cell * 0.2f, ringCol.WithAlpha(0.6f), 6f);
                    c.Circle(cx, cy, cell * 0.12f, new Color(0.06f, 0.05f, 0.05f, 0.95f), 3f);
                    c.Circle(cx, cy, cell * 0.06f, new Color(0, 0, 0, 1f), 2f);
                    if (i == 3) c.Circle(cx, cy, cell * 0.38f, new Color(0.05f, 0.04f, 0.03f, 0.35f), 30f);
                }
                return holeAtlas = c.ToTexture("HoleAtlas");
            }
        }

        // ------------------------------------------------------------------ wounds

        /// <summary>
        /// 4x4 atlas of skin wounds: 0-1 entry, 2-3 exit, 4-5 slash, 6 stab, 7 bruise, 8 burn, 9 abrasion,
        /// 10 buckshot cluster, 11 laceration, 12 graze, 13 neck/arterial, 14 crush, 15 small entry.
        /// </summary>
        public static Texture2D WoundAtlas
        {
            get
            {
                if (woundAtlas != null) return woundAtlas;
                const int cell = 256;
                var c = new PixelCanvas(cell * WoundCells, cell * WoundCells, new Color(0, 0, 0, 0));
                var rng = new System.Random(4242);
                var dark = new Color(0.12f, 0.0f, 0.01f, 1f);
                var blood = new Color(0.5f, 0.02f, 0.03f, 1f);
                var flesh = new Color(0.78f, 0.22f, 0.22f, 1f);
                var bruise = new Color(0.38f, 0.16f, 0.25f, 1f);
                for (int i = 0; i < 16; i++)
                {
                    float ox = (i % 4) * cell, oy = (i / 4) * cell;
                    float cx = ox + cell * 0.5f, cy = oy + cell * 0.5f;
                    switch (i)
                    {
                        case 0: case 1: case 15:
                        {
                            float s = i == 1 ? 1.3f : i == 15 ? 0.75f : 1f;
                            c.Circle(cx, cy, cell * 0.22f * s, bruise.WithAlpha(0.35f), cell * 0.18f);
                            c.Circle(cx, cy, cell * 0.11f * s, blood.WithAlpha(0.9f), 6f);
                            c.Ring(cx, cy, cell * 0.085f * s, cell * 0.035f, new Color(0.35f, 0.08f, 0.06f, 0.9f), 3f);
                            c.Circle(cx, cy, cell * 0.055f * s, dark, 3f);
                            Runs(c, cx, cy - cell * 0.06f, rng, 2, cell * 0.25f, blood.WithAlpha(0.8f));
                            break;
                        }
                        case 2: case 3:
                        {
                            float R = cell * (i == 3 ? 0.36f : 0.27f);
                            var pts = new Vector2[18];
                            for (int k = 0; k < pts.Length; k++)
                            {
                                float ang = k / (float)pts.Length * Mathf.PI * 2f;
                                float r = R * (0.45f + (float)rng.NextDouble() * 0.6f);
                                pts[k] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
                            }
                            c.Circle(cx, cy, R * 1.25f, bruise.WithAlpha(0.3f), R * 0.6f);
                            c.Polygon(pts, blood);
                            for (int k = 0; k < 30; k++)
                                c.Circle(cx + ((float)rng.NextDouble() - 0.5f) * R, cy + ((float)rng.NextDouble() - 0.5f) * R, cell * 0.02f, flesh.WithAlpha(0.8f), 3f);
                            c.Circle(cx, cy, R * 0.3f, dark, 6f);
                            Runs(c, cx, cy - R * 0.5f, rng, 3, cell * 0.3f, blood);
                            break;
                        }
                        case 4: case 5: case 11:
                        {
                            float len = cell * (i == 4 ? 0.42f : 0.3f);
                            float w = cell * (i == 11 ? 0.05f : 0.03f);
                            c.Line(cx - len, cy, cx + len, cy, w * 3.2f, bruise.WithAlpha(0.25f), w * 2f);
                            c.Line(cx - len, cy, cx + len, cy, w * 1.8f, flesh, 3f);
                            c.Line(cx - len * 0.9f, cy, cx + len * 0.9f, cy, w, dark, 2f);
                            Runs(c, cx, cy - w, rng, 4, cell * 0.22f, blood.WithAlpha(0.85f), len);
                            break;
                        }
                        case 6:
                            c.Circle(cx, cy, cell * 0.12f, bruise.WithAlpha(0.3f), cell * 0.1f);
                            c.Line(cx - cell * 0.08f, cy, cx + cell * 0.08f, cy, cell * 0.05f, flesh, 3f);
                            c.Line(cx - cell * 0.07f, cy, cx + cell * 0.07f, cy, cell * 0.022f, dark, 2f);
                            Runs(c, cx, cy - 6, rng, 2, cell * 0.3f, blood);
                            break;
                        case 7:
                            for (int k = 0; k < 6; k++)
                                c.Circle(cx + ((float)rng.NextDouble() - 0.5f) * cell * 0.25f, cy + ((float)rng.NextDouble() - 0.5f) * cell * 0.25f, cell * 0.16f, new Color(0.35f, 0.15f, 0.35f, 0.22f), cell * 0.12f);
                            c.Circle(cx, cy, cell * 0.1f, new Color(0.45f, 0.12f, 0.2f, 0.35f), cell * 0.1f);
                            break;
                        case 8:
                            c.Circle(cx, cy, cell * 0.38f, new Color(0.55f, 0.18f, 0.12f, 0.45f), cell * 0.2f);
                            c.Circle(cx, cy, cell * 0.28f, new Color(0.25f, 0.12f, 0.06f, 0.85f), cell * 0.12f);
                            c.Circle(cx, cy, cell * 0.16f, new Color(0.05f, 0.04f, 0.03f, 0.95f), cell * 0.08f);
                            break;
                        case 9:
                            for (int k = 0; k < 14; k++)
                            {
                                float y = cy + ((float)rng.NextDouble() - 0.5f) * cell * 0.3f;
                                c.Line(cx - cell * 0.3f, y, cx + cell * 0.3f, y + ((float)rng.NextDouble() - 0.5f) * 10f, 2.5f, flesh.WithAlpha(0.6f), 2f);
                            }
                            break;
                        case 10:
                            for (int k = 0; k < 9; k++)
                            {
                                float px = cx + ((float)rng.NextDouble() - 0.5f) * cell * 0.6f, py = cy + ((float)rng.NextDouble() - 0.5f) * cell * 0.6f;
                                c.Circle(px, py, cell * 0.045f, blood, 3f);
                                c.Circle(px, py, cell * 0.022f, dark, 2f);
                            }
                            break;
                        case 12:
                            c.Line(cx - cell * 0.35f, cy - 10, cx + cell * 0.35f, cy + 10, cell * 0.06f, flesh.WithAlpha(0.8f), 6f);
                            c.Line(cx - cell * 0.3f, cy - 8, cx + cell * 0.3f, cy + 8, cell * 0.02f, blood, 2f);
                            break;
                        case 13:
                            c.Circle(cx, cy, cell * 0.16f, blood, 5f);
                            c.Circle(cx, cy, cell * 0.08f, dark, 3f);
                            Runs(c, cx, cy - cell * 0.1f, rng, 6, cell * 0.4f, blood);
                            break;
                        case 14:
                            c.Circle(cx, cy, cell * 0.36f, bruise.WithAlpha(0.45f), cell * 0.2f);
                            for (int k = 0; k < 10; k++)
                                c.Line(cx, cy, cx + ((float)rng.NextDouble() - 0.5f) * cell * 0.6f, cy + ((float)rng.NextDouble() - 0.5f) * cell * 0.6f, 3f, blood.WithAlpha(0.8f), 2f);
                            c.Circle(cx, cy, cell * 0.12f, flesh, 4f);
                            break;
                    }
                }
                return woundAtlas = c.ToTexture("WoundAtlas");
            }
        }

        /// <summary>Little blood runs flowing "down" (towards -y in the texture) from a wound.</summary>
        static void Runs(PixelCanvas c, float x, float y, System.Random rng, int count, float maxLen, Color col, float spread = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                float sx = x + ((float)rng.NextDouble() - 0.5f) * (spread > 0 ? spread * 2f : 20f);
                float len = maxLen * (0.4f + (float)rng.NextDouble() * 0.6f);
                c.Line(sx, y, sx + ((float)rng.NextDouble() - 0.5f) * 6f, y - len, 3f + (float)rng.NextDouble() * 3f, col, 2f);
                c.Circle(sx, y - len, 4f, col, 2f);
            }
        }

        /// <summary>Limb cross-section: skin, fat, muscle and bone rings. Mapped radially onto stump caps.</summary>
        public static Texture2D FleshCap
        {
            get
            {
                if (fleshCap != null) return fleshCap;
                const int size = 256;
                var c = new PixelCanvas(size, size, new Color(0.6f, 0.1f, 0.1f, 1));
                float h = size * 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - h) / h, dy = (y - h) / h;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float ang = Mathf.Atan2(dy, dx);
                        float n = Noise.Fbm(x * 0.08f, y * 0.08f, 3, 5);
                        Color col;
                        if (d > 0.93f) col = new Color(0.86f, 0.66f, 0.55f);
                        else if (d > 0.82f) col = Color.Lerp(new Color(0.95f, 0.85f, 0.45f), new Color(0.9f, 0.75f, 0.4f), n);
                        else if (d > 0.27f)
                        {
                            float stri = Mathf.Abs(Mathf.Sin(ang * 22f + n * 6f));
                            col = Color.Lerp(new Color(0.55f, 0.06f, 0.07f), new Color(0.78f, 0.16f, 0.16f), stri * 0.6f + n * 0.4f);
                        }
                        else if (d > 0.13f) col = Color.Lerp(new Color(0.93f, 0.9f, 0.82f), new Color(0.85f, 0.8f, 0.7f), n);
                        else col = new Color(0.45f, 0.08f, 0.1f);
                        c.Set(x, y, col);
                    }
                return fleshCap = c.ToTexture("FleshCap", true, TextureWrapMode.Clamp);
            }
        }

        // ------------------------------------------------------------------ surfaces (tileable 256²)

        public static Texture2D Grass => grass ??= Tile("Grass", (x, y) =>
        {
            float n = Noise.FbmTile(x * 8, y * 8, 8, 5, 1);
            float blades = Noise.FbmTile(x * 64, y * 64, 64, 2, 2);
            return Color.Lerp(new Color(0.24f, 0.42f, 0.16f), new Color(0.42f, 0.62f, 0.24f), n * 0.7f + blades * 0.3f);
        });

        public static Texture2D Concrete => concrete ??= Tile("Concrete", (x, y) =>
        {
            float n = Noise.FbmTile(x * 8, y * 8, 8, 5, 3);
            float speck = Noise.ValueTile(x * 128, y * 128, 128, 4);
            float seam = (Frac(x * 2f) < 0.008f || Frac(y * 2f) < 0.008f) ? 0.8f : 1f;
            float l = (0.55f + n * 0.25f + (speck > 0.9f ? -0.1f : 0f)) * seam;
            return new Color(l, l * 0.99f, l * 0.96f);
        });

        public static Texture2D Asphalt => asphalt ??= Tile("Asphalt", (x, y) =>
        {
            float n = Noise.FbmTile(x * 16, y * 16, 16, 4, 5);
            float speck = Noise.ValueTile(x * 200, y * 200, 200, 6);
            float l = 0.16f + n * 0.12f + (speck > 0.85f ? 0.08f : 0f);
            return new Color(l, l, l * 1.05f);
        });

        public static Texture2D Tiles => tiles ??= Tile("Tiles", (x, y) =>
        {
            float fx = Frac(x * 4f), fy = Frac(y * 4f);
            bool grout = fx < 0.03f || fy < 0.03f;
            float n = Noise.FbmTile(x * 4, y * 4, 4, 3, 7);
            float l = grout ? 0.45f : 0.82f + n * 0.1f;
            return new Color(l, l, l * 0.98f);
        });

        public static Texture2D Wood => wood ??= Tile("Wood", (x, y) =>
        {
            float plank = Mathf.Floor(y * 6f);
            float fy = Frac(y * 6f);
            float grain = Noise.FbmTile(x * 4 + plank * 0.37f, y * 48, 4, 4, 8);
            float rings = Mathf.Sin((x * 40f + grain * 6f + plank * 3f)) * 0.5f + 0.5f;
            float l = 0.5f + grain * 0.25f + rings * 0.08f + Noise.Hash((int)plank, 0, 9) * 0.1f;
            if (fy < 0.03f) l *= 0.5f;
            return new Color(0.62f * l + 0.1f, 0.42f * l + 0.05f, 0.25f * l);
        });

        public static Texture2D Metal => metal ??= Tile("Metal", (x, y) =>
        {
            float fx = Frac(x * 16f + y * 16f), fy = Frac(x * 16f - y * 16f);
            float diamond = (fx > 0.45f && fx < 0.55f) || (fy > 0.45f && fy < 0.55f) ? 0.12f : 0f;
            float n = Noise.FbmTile(x * 8, y * 8, 8, 3, 10);
            float l = 0.45f + n * 0.15f + diamond;
            return new Color(l, l * 1.02f, l * 1.06f);
        });

        public static Texture2D Brick => brick ??= Tile("Brick", (x, y) =>
        {
            float row = Mathf.Floor(y * 8f);
            float bx = Frac(x * 4f + (row % 2f) * 0.5f), by = Frac(y * 8f);
            bool mortar = bx < 0.04f || by < 0.08f;
            float n = Noise.FbmTile(x * 16, y * 16, 16, 3, 11);
            float v = Noise.Hash(Mathf.FloorToInt(x * 4f + (row % 2f) * 0.5f), (int)row, 12) * 0.15f;
            return mortar ? new Color(0.62f, 0.6f, 0.56f) : new Color(0.55f + n * 0.15f + v, 0.24f + n * 0.08f, 0.18f + n * 0.05f);
        });

        public static Texture2D Carpet => carpet ??= Tile("Carpet", (x, y) =>
        {
            float n = Noise.FbmTile(x * 32, y * 32, 32, 3, 13);
            float stain = Noise.FbmTile(x * 3, y * 3, 3, 3, 14);
            float l = 0.8f + n * 0.15f - (stain > 0.62f ? 0.12f : 0f);
            return new Color(0.72f * l, 0.62f * l, 0.38f * l);
        });

        public static Texture2D Wallpaper => wallpaper ??= Tile("Wallpaper", (x, y) =>
        {
            float stripe = Mathf.Sin(x * Mathf.PI * 2f * 12f) * 0.5f + 0.5f;
            float pattern = Mathf.Sin(y * Mathf.PI * 2f * 24f) * Mathf.Sin(x * Mathf.PI * 2f * 24f) > 0.6f ? 0.05f : 0f;
            float stain = Noise.FbmTile(x * 4, y * 4, 4, 4, 15);
            float l = 0.85f + stripe * 0.06f + pattern - (stain > 0.6f ? 0.1f : 0f);
            return new Color(0.86f * l, 0.8f * l, 0.5f * l);
        });

        public static Texture2D Ceiling => ceiling ??= Tile("Ceiling", (x, y) =>
        {
            float fx = Frac(x * 2f), fy = Frac(y * 2f);
            bool grid = fx < 0.02f || fy < 0.02f;
            float n = Noise.ValueTile(x * 100, y * 100, 100, 16);
            float l = grid ? 0.55f : 0.86f + (n > 0.8f ? -0.06f : 0f);
            return new Color(l, l * 0.97f, l * 0.88f);
        });

        public static Texture2D Dirt => dirt ??= Tile("Dirt", (x, y) =>
        {
            float n = Noise.FbmTile(x * 8, y * 8, 8, 5, 17);
            float pebbles = Noise.ValueTile(x * 64, y * 64, 64, 18);
            float l = 0.35f + n * 0.25f + (pebbles > 0.85f ? 0.1f : 0f);
            return new Color(0.55f * l + 0.1f, 0.42f * l + 0.06f, 0.3f * l + 0.03f);
        });

        public static Texture2D Sand => sand ??= Tile("Sand", (x, y) =>
        {
            float n = Noise.FbmTile(x * 16, y * 16, 16, 4, 19);
            float l = 0.75f + n * 0.2f;
            return new Color(0.82f * l, 0.7f * l, 0.5f * l);
        });

        public static Texture2D Plaster => plaster ??= Tile("Plaster", (x, y) =>
        {
            float n = Noise.FbmTile(x * 16, y * 16, 16, 4, 20);
            float l = 0.8f + n * 0.12f;
            return new Color(l, l * 0.98f, l * 0.94f);
        });

        static float Frac(float v) => v - Mathf.Floor(v);

        static Texture2D Tile(string name, System.Func<float, float, Color> f)
        {
            const int size = 256;
            var c = new PixelCanvas(size, size, Color.white);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var col = f(x / (float)size, y / (float)size);
                    col.a = 1f;
                    c.Set(x, y, col);
                }
            return c.ToTexture(name, true, TextureWrapMode.Repeat, FilterMode.Trilinear);
        }

        // ------------------------------------------------------------------ UI

        public static Texture2D RoundRect => roundRect ??= RoundedRect(64, 16, "RoundRect");
        public static Texture2D RoundRectSmall => roundRectSmall ??= RoundedRect(32, 7, "RoundRectSmall");

        static Texture2D RoundedRect(int size, float radius, string name)
        {
            var c = new PixelCanvas(size, size, new Color(1, 1, 1, 0));
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Max(Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)), 0f);
                    float py = Mathf.Max(Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)), 0f);
                    float d = Mathf.Sqrt(px * px + py * py);
                    c.Set(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
                }
            return c.ToTexture(name, false);
        }

        public static Texture2D Gradient
        {
            get
            {
                if (gradient != null) return gradient;
                var c = new PixelCanvas(4, 128, Color.white);
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 4; x++)
                        c.Set(x, y, new Color(1, 1, 1, y / 127f));
                return gradient = c.ToTexture("Gradient", false);
            }
        }

        public static Texture2D Vignette => vignette ??= Radial(256, "Vignette", d => Mathf.Clamp01((d - 0.55f) * 1.4f));

        public static Texture2D Grain
        {
            get
            {
                if (grain != null) return grain;
                var c = new PixelCanvas(128, 128, Color.white);
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 128; x++)
                    {
                        float v = Noise.Hash(x, y, 3);
                        c.Set(x, y, new Color(v, v, v, 0.06f));
                    }
                return grain = c.ToTexture("Grain", false, TextureWrapMode.Repeat, FilterMode.Point);
            }
        }

        public static Texture2D Shadow
        {
            get
            {
                if (shadow != null) return shadow;
                const int size = 64;
                var c = new PixelCanvas(size, size, new Color(0, 0, 0, 0));
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(Mathf.Max(20 - x, x - 43), 0) / 20f;
                        float dy = Mathf.Max(Mathf.Max(20 - y, y - 43), 0) / 20f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        c.Set(x, y, new Color(0, 0, 0, Mathf.Pow(Mathf.Clamp01(1f - d), 2f) * 0.6f));
                    }
                return shadow = c.ToTexture("Shadow", false);
            }
        }

        // ------------------------------------------------------------------ splash screen

        public static Texture2D Corona
        {
            get
            {
                if (corona != null) return corona;
                const int size = 512;
                var c = new PixelCanvas(size, size, new Color(1, 1, 1, 0));
                float h = size * 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - h) / h, dy = (y - h) / h;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float ang = Mathf.Atan2(dy, dx);
                        float rays = Noise.Fbm(Mathf.Cos(ang) * 6f + 10f, Mathf.Sin(ang) * 6f + 10f, 4, 21);
                        float reach = 0.42f + rays * 0.55f;
                        float a = d < 0.38f ? 0f : Mathf.Pow(Mathf.Clamp01(1f - (d - 0.38f) / (reach - 0.38f + 0.0001f)), 1.8f);
                        a += Mathf.Clamp01(1f - Mathf.Abs(d - 0.385f) * 60f) * 0.9f;
                        c.Set(x, y, new Color(1, 1, 1, Mathf.Clamp01(a)));
                    }
                return corona = c.ToTexture("Corona");
            }
        }

        public static Texture2D Star4
        {
            get
            {
                if (star4 != null) return star4;
                const int size = 128;
                var c = new PixelCanvas(size, size, new Color(1, 1, 1, 0));
                float h = size * 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Abs(x - h) / h, dy = Mathf.Abs(y - h) / h;
                        float cross = Mathf.Clamp01(1f - dx * 6f) * Mathf.Clamp01(1f - dy) + Mathf.Clamp01(1f - dy * 6f) * Mathf.Clamp01(1f - dx);
                        float core = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f), 2f);
                        c.Set(x, y, new Color(1, 1, 1, Mathf.Clamp01(cross * 0.8f + core)));
                    }
                return star4 = c.ToTexture("Star4");
            }
        }

        public static Texture2D Streak
        {
            get
            {
                if (streak != null) return streak;
                var c = new PixelCanvas(256, 32, new Color(1, 1, 1, 0));
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 256; x++)
                    {
                        float fx = Mathf.Abs(x - 127.5f) / 127.5f, fy = Mathf.Abs(y - 15.5f) / 15.5f;
                        c.Set(x, y, new Color(1, 1, 1, Mathf.Pow(1f - fx, 2f) * Mathf.Pow(Mathf.Clamp01(1f - fy), 3f)));
                    }
                return streak = c.ToTexture("Streak");
            }
        }

        /// <summary>UV rectangle of one cell in an NxN atlas.</summary>
        public static Rect AtlasCell(int index, int cells)
        {
            float s = 1f / cells;
            return new Rect((index % cells) * s, (index / cells) * s, s, s);
        }
    }
}
