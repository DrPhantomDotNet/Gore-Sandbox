using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Procedural art for the "????" boss realm: Orbie, the boss face and the sanity icons. Plus glitch-text drawing.</summary>
    public static class BossArt
    {
        static Texture2D orbie, orbieAngry, bossFace, sane, scream, uncanny, glow;

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

        /// <summary>A glowing white orb (soft halo baked in). angry = black slanted eyes with a red glow.</summary>
        public static Texture2D Orbie(bool angry = false)
        {
            if (angry ? orbieAngry != null : orbie != null) return angry ? orbieAngry : orbie;
            const int S = 256;
            var c = new PixelCanvas(S, S, new Color(0, 0, 0, 0));
            float cx = S / 2f, cy = S / 2f, R = 70f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float halo = Mathf.Exp(-Mathf.Max(0, d - R) / 22f) * 0.75f * (1f - Smooth(R + 50, S / 2f, d));
                    float core = 1f - Smooth(R - 2, R + 2, d);
                    // Slight inner shading: brighter top-left.
                    float shade = 1f - 0.12f * Mathf.Clamp01((dx - dy) / (R * 2f) + 0.5f);
                    var col = angry ? new Color(1f, 0.85f, 0.85f) : new Color(0.92f, 0.97f, 1f);
                    float a = Mathf.Max(halo, core);
                    var px = Color.Lerp(col * 0.95f, Color.white * shade, core);
                    px.a = a;
                    c.Set(x, y, px);
                }
            // Face (y up).
            var eye = angry ? new Color(0.05f, 0f, 0f) : new Color(0.25f, 0.32f, 0.45f);
            if (!angry)
            {
                c.Circle(cx - 22, cy + 10, 9, eye); c.Circle(cx + 22, cy + 10, 9, eye);
                c.Circle(cx - 19, cy + 14, 3, Color.white); c.Circle(cx + 25, cy + 14, 3, Color.white);
                c.Line(cx - 12, cy - 18, cx, cy - 23, 3, eye); c.Line(cx, cy - 23, cx + 12, cy - 18, 3, eye);
            }
            else
            {
                c.Polygon(new[] { new Vector2(cx - 44, cy + 26), new Vector2(cx - 8, cy + 8), new Vector2(cx - 14, cy - 4), new Vector2(cx - 40, cy + 6) }, eye);
                c.Polygon(new[] { new Vector2(cx + 44, cy + 26), new Vector2(cx + 8, cy + 8), new Vector2(cx + 14, cy - 4), new Vector2(cx + 40, cy + 6) }, eye);
                c.Circle(cx - 24, cy + 8, 3, new Color(1f, 0.1f, 0.1f)); c.Circle(cx + 24, cy + 8, 3, new Color(1f, 0.1f, 0.1f));
                c.Circle(cx, cy - 34, 20, eye);
                c.Rect(cx - 24, cy - 26, 48, 14, new Color(1f, 0.9f, 0.9f));
            }
            var tex = c.ToTexture(angry ? "orbie_angry" : "orbie");
            if (angry) orbieAngry = tex; else orbie = tex;
            return tex;
        }

        /// <summary>The boss nextbot face: sickly bright skin, hollow eyes, a smile that is far too wide.</summary>
        public static Texture2D BossFace()
        {
            if (bossFace != null) return bossFace;
            const int S = 512;
            var rng = new System.Random(666);
            var c = new PixelCanvas(S, S, new Color(0, 0, 0, 0));
            float cx = S / 2f, cy = S / 2f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - cx) / 230f, dy = (y - cy) / 250f;
                    // Lumpy, slightly asymmetric head.
                    float ang = Mathf.Atan2(dy, dx);
                    float r = 1f + 0.04f * Mathf.Sin(ang * 5f + 1f) + 0.03f * Mathf.Sin(ang * 11f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (d > 1.06f) continue;
                    float n = (float)rng.NextDouble();
                    var skin = Color.Lerp(new Color(0.98f, 0.96f, 0.7f), new Color(0.85f, 1f, 0.55f), 0.5f + 0.5f * Mathf.Sin(x * 0.03f + y * 0.02f));
                    skin *= 0.9f + n * 0.12f;
                    // Neon magenta rim so it reads in the dark.
                    float rim = Smooth(0.86f, 1f, d);
                    var px = Color.Lerp(skin, new Color(1f, 0.1f, 0.75f), rim);
                    px.a = 1f - Smooth(1f, 1.06f, d);
                    // Veins.
                    if (Mathf.Abs(Mathf.Sin(x * 0.05f + Mathf.Sin(y * 0.031f) * 3f)) < 0.025f && d < 0.85f) px = Color.Lerp(px, new Color(0.5f, 0.1f, 0.25f), 0.6f);
                    c.Set(x, y, px);
                }
            // Hollow eyes: black pits, one larger and lower, tiny white pin-pupils.
            var pit = new Color(0.01f, 0f, 0f);
            c.Circle(cx - 92, cy + 70, 62, new Color(0.3f, 0.05f, 0.1f));
            c.Circle(cx + 96, cy + 58, 74, new Color(0.3f, 0.05f, 0.1f));
            c.Circle(cx - 92, cy + 70, 52, pit);
            c.Circle(cx + 96, cy + 58, 64, pit);
            c.Circle(cx - 84, cy + 74, 6, Color.white);
            c.Circle(cx + 104, cy + 54, 5, Color.white);
            // Tears of blood from the eyes.
            for (int i = 0; i < 6; i++)
            {
                float ex = i < 3 ? cx - 120 + i * 26 : cx + 66 + (i - 3) * 30;
                float ey = i < 3 ? cy + 22 : cy;
                c.Line(ex, ey, ex + (float)rng.NextDouble() * 6 - 3, ey - 60 - (float)rng.NextDouble() * 120, 6, new Color(0.45f, 0f, 0.02f));
            }
            // Smile: ear to ear, open, rows of thin teeth, gums.
            var mouth = new Color(0.08f, 0f, 0.01f);
            for (int x = -190; x <= 190; x++)
            {
                float u = x / 190f;
                float top = cy - 70 - 60 * (1 - u * u) + 30 * u * u * u * u;
                float bottom = cy - 70 - 150 * (1 - u * u);
                top += 50 * u * u;
                for (float y = bottom; y <= top; y += 1f) c.Set((int)(cx + x), (int)y, mouth);
                c.Set((int)(cx + x), (int)top + 1, new Color(0.7f, 0.1f, 0.2f));
                c.Set((int)(cx + x), (int)top + 2, new Color(0.7f, 0.1f, 0.2f));
            }
            for (int i = -17; i <= 17; i++)
            {
                float u = i / 18f;
                float x = cx + u * 180;
                float top = cy - 70 - 60 * (1 - u * u) + 30 * u * u * u * u + 50 * u * u;
                float bottom = cy - 70 - 150 * (1 - u * u);
                float tl = 18 + (float)rng.NextDouble() * 18 * (1 - Mathf.Abs(u));
                c.Polygon(new[] { new Vector2(x - 5, top), new Vector2(x + 5, top), new Vector2(x, top - tl) }, new Color(0.95f, 0.93f, 0.8f));
                if (bottom < top - 30) c.Polygon(new[] { new Vector2(x - 5, bottom), new Vector2(x + 5, bottom), new Vector2(x, bottom + tl * 0.8f) }, new Color(0.9f, 0.88f, 0.75f));
            }
            // No nose, just two slits.
            c.Line(cx - 10, cy + 2, cx - 6, cy - 22, 4, new Color(0.2f, 0.05f, 0.05f));
            c.Line(cx + 10, cy + 2, cx + 6, cy - 22, 4, new Color(0.2f, 0.05f, 0.05f));
            bossFace = c.ToTexture("boss_face");
            return bossFace;
        }

        /// <summary>Goredoll head icon for the sanity meter. stage 0 = calm, 1 = screaming, 2 = uncanny.</summary>
        public static Texture2D SanityIcon(int stage)
        {
            if (stage == 0 && sane != null) return sane;
            if (stage == 1 && scream != null) return scream;
            if (stage >= 2 && uncanny != null) return uncanny;
            const int S = 128;
            var c = new PixelCanvas(S, S, new Color(0, 0, 0, 0));
            var skin = stage >= 2 ? new Color(0.78f, 0.75f, 0.7f) : new Color(0.93f, 0.76f, 0.62f);
            var dark = new Color(0.12f, 0.05f, 0.05f);
            if (stage < 2)
            {
                c.Circle(64, 66, 50, skin);
                c.Circle(14, 64, 9, skin); c.Circle(114, 64, 9, skin);
            }
            else
            {
                // Stretched, warped skull.
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = (x - 64 - Mathf.Sin(y * 0.15f) * 5f) / 44f, dy = (y - 60) / 62f;
                        if (dx * dx + dy * dy < 1f) c.Set(x, y, skin * (0.85f + 0.15f * Mathf.Sin(x * 0.4f)));
                    }
            }
            if (stage == 0)
            {
                c.Circle(46, 76, 6, dark); c.Circle(82, 76, 6, dark);
                c.Line(48, 44, 80, 44, 4, dark);
                c.Line(36, 90, 54, 88, 3, dark); c.Line(74, 88, 92, 90, 3, dark);
            }
            else if (stage == 1)
            {
                c.Circle(46, 80, 11, Color.white); c.Circle(82, 80, 11, Color.white);
                c.Circle(46, 80, 4, dark); c.Circle(82, 80, 4, dark);
                c.Line(30, 98, 56, 92, 4, dark); c.Line(72, 92, 98, 98, 4, dark);
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float dx = (x - 64) / 16f, dy = (y - 38) / 24f; if (dx * dx + dy * dy < 1f) c.Set(x, y, new Color(0.25f, 0f, 0.02f)); }
                c.Rect(52, 56, 24, 5, Color.white);
            }
            else
            {
                // Misaligned hollow eyes, dripping; a vertical stretched mouth; red glitch tint.
                c.Circle(42, 86, 14, Color.black); c.Circle(88, 74, 9, Color.black);
                c.Circle(44, 88, 2, Color.red); c.Circle(86, 75, 2, Color.red);
                c.Line(42, 72, 40, 40, 4, new Color(0.5f, 0, 0)); c.Line(88, 66, 90, 30, 3, new Color(0.5f, 0, 0));
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float dx = (x - 66) / 8f, dy = (y - 34) / 30f; if (dx * dx + dy * dy < 1f) c.Set(x, y, Color.black); }
                var rng = new System.Random(3);
                for (int k = 0; k < 7; k++)
                {
                    int y0 = rng.Next(0, S - 8), h = rng.Next(2, 6), off = rng.Next(-10, 10);
                    for (int y = y0; y < y0 + h; y++)
                        for (int x = S - 1; x >= 0; x--)
                        {
                            var src = c.Get(x - off, y);
                            c.Set(x, y, new Color(src.r, src.g * 0.4f, src.b * 0.4f, src.a));
                        }
                }
            }
            var tex = c.ToTexture("sanity_" + stage);
            if (stage == 0) sane = tex; else if (stage == 1) scream = tex; else uncanny = tex;
            return tex;
        }

        static Texture2D thumb;

        /// <summary>Map card thumbnail: darkness, static, and something looking back.</summary>
        public static Texture2D Thumbnail()
        {
            if (thumb != null) return thumb;
            const int W = 384, H = 216;
            var c = new PixelCanvas(W, H, Color.black);
            var rng = new System.Random(13);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - W / 2f) / W, dy = (y - H / 2f) / H;
                    float v = Mathf.Clamp01(0.12f - (dx * dx + dy * dy) * 0.3f) + (float)rng.NextDouble() * 0.035f;
                    c.Set(x, y, new Color(v * 1.6f, v * 0.2f, v * 0.25f));
                }
            for (int k = 0; k < 9; k++)
            {
                int y = rng.Next(0, H), h = rng.Next(1, 3);
                c.Rect(0, y, W, h, new Color(0.25f, 0.02f, 0.04f, 0.6f));
            }
            // Two hollow eyes and a too-wide grin, barely visible.
            c.Circle(W / 2f - 46, H / 2f + 22, 22, new Color(0.35f, 0.05f, 0.12f));
            c.Circle(W / 2f + 48, H / 2f + 18, 26, new Color(0.35f, 0.05f, 0.12f));
            c.Circle(W / 2f - 46, H / 2f + 22, 17, Color.black);
            c.Circle(W / 2f + 48, H / 2f + 18, 21, Color.black);
            c.Circle(W / 2f - 43, H / 2f + 24, 2, Color.white);
            c.Circle(W / 2f + 51, H / 2f + 16, 2, Color.white);
            for (int x = -90; x <= 90; x += 2)
            {
                float u = x / 90f;
                c.Circle(W / 2f + x, H / 2f - 30 - 26 * (1 - u * u) + 14 * u * u, 2.2f, new Color(0.55f, 0.5f, 0.45f));
            }
            thumb = c.ToTexture("thumb_realm");
            return thumb;
        }

        /// <summary>Soft round glow sprite.</summary>
        public static Texture2D Glow()
        {
            if (glow != null) return glow;
            const int S = 128;
            var c = new PixelCanvas(S, S, new Color(0, 0, 0, 0));
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - 64) / 64f, dy = (y - 64) / 64f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    c.Set(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f)));
                }
            glow = c.ToTexture("boss_glow");
            return glow;
        }

        // ------------------------------------------------------------------ glitch text

        const string GlitchChars = "#%&@$!?/\\|<>*+=~^0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <summary>Draws text with jittering letters, random character swaps and red/cyan chromatic copies.</summary>
        public static void GlitchText(Rect r, string text, int size, Color color, float intensity, Font font = null, TextAnchor align = TextAnchor.MiddleCenter)
        {
            if (Event.current.type != EventType.Repaint) return;
            font = font ?? UI.HeavyFont;
            float t = Time.unscaledTime;
            // Per-letter layout so each letter can jitter on its own.
            float total = 0f;
            var widths = new float[text.Length];
            for (int i = 0; i < text.Length; i++) { widths[i] = UI.TextWidth(text[i].ToString(), size, FontStyle.Normal, font) + size * 0.02f; total += widths[i]; }
            float x = align == TextAnchor.MiddleLeft ? r.x : r.center.x - total / 2f;
            int frame = Mathf.FloorToInt(t * 24f);
            float bigShift = (Hash(frame, 999) < 0.12f * intensity) ? (Hash(frame, 998) - 0.5f) * size * 0.6f * intensity : 0f;
            for (int i = 0; i < text.Length; i++)
            {
                string ch = text[i].ToString();
                if (text[i] != ' ' && Hash(frame, i) < 0.08f * intensity) ch = GlitchChars[(int)(Hash(frame, i + 77) * GlitchChars.Length) % GlitchChars.Length].ToString();
                float jx = (Hash(frame, i * 3 + 1) - 0.5f) * size * 0.12f * intensity + bigShift;
                float jy = (Hash(frame, i * 3 + 2) - 0.5f) * size * 0.16f * intensity;
                var lr = new Rect(x + jx, r.y + jy, widths[i] + size, r.height);
                float off = size * 0.05f * intensity * (0.6f + Hash(frame, i + 31));
                UI.Label(new Rect(lr.x - off, lr.y, lr.width, lr.height), ch, size, new Color(1f, 0f, 0.1f, 0.65f * color.a), TextAnchor.MiddleLeft, FontStyle.Normal, false, false, font);
                UI.Label(new Rect(lr.x + off, lr.y + off * 0.3f, lr.width, lr.height), ch, size, new Color(0f, 1f, 1f, 0.55f * color.a), TextAnchor.MiddleLeft, FontStyle.Normal, false, false, font);
                UI.Label(lr, ch, size, color, TextAnchor.MiddleLeft, FontStyle.Normal, false, false, font);
                x += widths[i];
            }
            // Scanline slices.
            int slices = Mathf.RoundToInt(3 * intensity);
            for (int k = 0; k < slices; k++)
            {
                if (Hash(frame, 500 + k) > 0.5f) continue;
                float sy = r.y + Hash(frame, 600 + k) * r.height;
                UI.Fill(new Rect(r.x - 40, sy, r.width + 80, 2 + Hash(frame, 700 + k) * 6), new Color(color.r, color.g, color.b, 0.25f * color.a));
            }
        }

        public static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
