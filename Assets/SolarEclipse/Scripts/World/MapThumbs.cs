using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Paints a stylised preview image for each map (sky gradient, silhouettes, glyph).</summary>
    public static class MapThumbs
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(MapDef def)
        {
            if (def.Thumbnail != null) return def.Thumbnail;
            if (cache.TryGetValue(def.Id, out var t)) return t;
            const int W = 384, H = 216;
            var c = new PixelCanvas(W, H, Color.black);
            var rng = new System.Random(def.Id.GetHashCode());
            for (int y = 0; y < H; y++)
            {
                var col = Color.Lerp(def.ThemeBottom, def.ThemeTop, y / (float)H);
                for (int x = 0; x < W; x++) c.Set(x, y, col);
            }
            var sil = Color.Lerp(def.ThemeBottom, Color.black, 0.75f);
            switch (def.Id)
            {
                case "flatgrass":
                    c.Circle(300, 170, 18, new Color(1f, 0.95f, 0.8f), 3);
                    for (int i = 0; i < 18; i++) { float x = rng.Next(W); float h = 30 + rng.Next(40); c.Polygon(new[] { new Vector2(x - 12, 40), new Vector2(x + 12, 40), new Vector2(x, 40 + h) }, sil); }
                    c.Rect(170, 40, 30, 90, sil); c.Polygon(new[] { new Vector2(200, 40), new Vector2(300, 40), new Vector2(200, 128) }, sil);
                    c.Rect(0, 0, W, 42, Color.Lerp(def.ThemeBottom, Color.black, 0.3f));
                    break;
                case "arena":
                    c.Circle(80, 160, 26, new Color(1f, 0.3f, 0.25f), 4);
                    c.Rect(0, 0, W, 70, sil);
                    for (int i = 0; i < 6; i++) { int x = 30 + i * 65; c.Rect(x, 70, 4, 70, sil); c.Rect(x, 138, 18, 5, sil); c.Circle(x + 16, 134, 10, new Color(1f, 0.5f, 0.4f, 0.5f), 10); }
                    break;
                case "downtown":
                    for (int i = 0; i < 40; i++) c.Circle(rng.Next(W), 130 + rng.Next(80), 1, new Color(1, 1, 1, 0.7f), 1);
                    for (int x = 0; x < W;)
                    {
                        int bw = 30 + rng.Next(40), bh = 50 + rng.Next(110);
                        c.Rect(x, 0, bw, bh, sil);
                        for (int wy = 12; wy < bh - 8; wy += 14) for (int wx = 5; wx < bw - 6; wx += 10) if (rng.NextDouble() < 0.4) c.Rect(x + wx, wy, 5, 7, new Color(1f, 0.85f, 0.5f, 0.85f));
                        x += bw + 4;
                    }
                    break;
                case "warehouse":
                    for (int i = 0; i < 5; i++) c.Rect(20 + i * 75, 0, 10, 140, sil);
                    for (int r = 0; r < 3; r++) c.Rect(0, 30 + r * 40, W, 6, sil);
                    for (int i = 0; i < 4; i++) c.Rect(40 + i * 90, 190, 50, 6, new Color(1, 1, 0.9f, 0.9f));
                    break;
                case BackroomsMap.MapId:
                    BackroomsArt.PaintThumb(c, W, H);
                    break;
                case RooftopsMap.MapId:
                    RooftopsMap.PaintThumb(c, W, H);
                    break;
            }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float vx = (x - W / 2f) / (W / 2f), vy = (y - H / 2f) / (H / 2f);
                    float v = Mathf.Clamp01(1f - (vx * vx + vy * vy) * 0.35f);
                    var p = c.Px[y * W + x]; p.r *= v; p.g *= v; p.b *= v; c.Px[y * W + x] = p;
                }
            t = c.ToTexture("Thumb_" + def.Id);
            cache[def.Id] = t;
            return t;
        }
    }
}
