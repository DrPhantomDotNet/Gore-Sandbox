using UnityEngine;

namespace SolarEclipse
{
    /// <summary>CPU pixel buffer with simple drawing ops, turned into a Texture2D at the end.</summary>
    public sealed class PixelCanvas
    {
        public readonly int W, H;
        public readonly Color[] Px;

        public PixelCanvas(int w, int h, Color fill)
        {
            W = w; H = h;
            Px = new Color[w * h];
            for (int i = 0; i < Px.Length; i++) Px[i] = fill;
        }

        public Color Get(int x, int y) => Px[Mathf.Clamp(y, 0, H - 1) * W + Mathf.Clamp(x, 0, W - 1)];
        public void Set(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) Px[y * W + x] = c; }

        /// <summary>Alpha-over blend.</summary>
        public void Blend(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || c.a <= 0f) return;
            int i = y * W + x;
            var d = Px[i];
            float a = c.a + d.a * (1f - c.a);
            if (a <= 0.0001f) { Px[i] = new Color(0, 0, 0, 0); return; }
            Px[i] = new Color(
                (c.r * c.a + d.r * d.a * (1f - c.a)) / a,
                (c.g * c.a + d.g * d.a * (1f - c.a)) / a,
                (c.b * c.a + d.b * d.a * (1f - c.a)) / a, a);
        }

        /// <summary>Takes the max alpha, keeps the brighter colour (for building masks).</summary>
        public void Max(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            int i = y * W + x;
            var d = Px[i];
            Px[i] = new Color(Mathf.Max(d.r, c.r), Mathf.Max(d.g, c.g), Mathf.Max(d.b, c.b), Mathf.Max(d.a, c.a));
        }

        public void Circle(float cx, float cy, float r, Color c, float soft = 1f, bool max = false)
        {
            int x0 = Mathf.FloorToInt(cx - r - soft), x1 = Mathf.CeilToInt(cx + r + soft);
            int y0 = Mathf.FloorToInt(cy - r - soft), y1 = Mathf.CeilToInt(cy + r + soft);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01((r + soft * 0.5f - d) / Mathf.Max(soft, 0.001f));
                    if (a <= 0) continue;
                    var col = c; col.a *= a;
                    if (max) Max(x, y, col); else Blend(x, y, col);
                }
        }

        public void Ring(float cx, float cy, float r, float thickness, Color c, float soft = 1f)
        {
            int x0 = Mathf.FloorToInt(cx - r - thickness - soft), x1 = Mathf.CeilToInt(cx + r + thickness + soft);
            int y0 = Mathf.FloorToInt(cy - r - thickness - soft), y1 = Mathf.CeilToInt(cy + r + thickness + soft);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);
                    float a = Mathf.Clamp01((thickness * 0.5f + soft * 0.5f - d) / Mathf.Max(soft, 0.001f));
                    if (a <= 0) continue;
                    var col = c; col.a *= a;
                    Blend(x, y, col);
                }
        }

        public void Line(float ax, float ay, float bx, float by, float width, Color c, float soft = 1f)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(ax, bx) - width - soft), x1 = Mathf.CeilToInt(Mathf.Max(ax, bx) + width + soft);
            int y0 = Mathf.FloorToInt(Mathf.Min(ay, by) - width - soft), y1 = Mathf.CeilToInt(Mathf.Max(ay, by) + width + soft);
            var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
            var ab = b - a;
            float len2 = Mathf.Max(ab.sqrMagnitude, 0.0001f);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    float al = Mathf.Clamp01((width * 0.5f + soft * 0.5f - d) / Mathf.Max(soft, 0.001f));
                    if (al <= 0) continue;
                    var col = c; col.a *= al;
                    Blend(x, y, col);
                }
        }

        public void Rect(float x, float y, float w, float h, Color c)
        {
            for (int yy = Mathf.FloorToInt(y); yy < Mathf.CeilToInt(y + h); yy++)
                for (int xx = Mathf.FloorToInt(x); xx < Mathf.CeilToInt(x + w); xx++)
                    Blend(xx, yy, c);
        }

        /// <summary>Filled convex/concave polygon via even-odd scanline test (small shapes only).</summary>
        public void Polygon(Vector2[] pts, Color c)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
                for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
                {
                    // 4x supersampling for smooth edges
                    int inside = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        float sx = x + 0.25f + 0.5f * (s & 1), sy = y + 0.25f + 0.5f * (s >> 1);
                        if (PointInPolygon(pts, sx, sy)) inside++;
                    }
                    if (inside == 0) continue;
                    var col = c; col.a *= inside / 4f;
                    Blend(x, y, col);
                }
        }

        static bool PointInPolygon(Vector2[] pts, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if ((pts[i].y > y) != (pts[j].y > y) && x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y + 1e-6f) + pts[i].x)
                    inside = !inside;
            }
            return inside;
        }

        public Texture2D ToTexture(string name, bool mips = true, TextureWrapMode wrap = TextureWrapMode.Clamp, FilterMode filter = FilterMode.Bilinear, bool linear = false)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, mips, linear) { name = name, wrapMode = wrap, filterMode = filter, anisoLevel = mips ? 4 : 0 };
            t.SetPixels(Px);
            t.Apply(mips, false);
            return t;
        }
    }
}
