using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Procedural textures and the menu thumbnail for the Backrooms (Level 0 look).</summary>
    public static class BackroomsArt
    {
        static Texture2D wallpaper, carpet, ceiling;

        static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>Mono-yellow wallpaper, tile = 2 x 2 m: thin stripes, faint diamond motif, mottling and damp stains.</summary>
        public static Texture2D Wallpaper => wallpaper ??= Tile("BR_Wallpaper", (u, v) =>
        {
            float su = Frac(u * 16f);
            float stripe = su < 0.07f ? -0.045f : 0f;
            float cu = su - 0.5f, cv = Frac(v * 16f) - 0.5f;
            float dia = Mathf.Abs(cu) + Mathf.Abs(cv);
            float motif = dia > 0.2f && dia < 0.27f ? -0.03f : 0f;
            float mott = Noise.FbmTile(u * 8f, v * 8f, 8, 4, 41);
            float stain = Mathf.Clamp01((Noise.FbmTile(u * 3f, v * 3f, 3, 4, 42) - 0.58f) * 3.5f);
            float l = 0.95f + stripe + motif + (mott - 0.5f) * 0.09f;
            var baseCol = Color.Lerp(new Color(0.86f, 0.78f, 0.44f), new Color(0.66f, 0.56f, 0.3f), stain * 0.6f);
            return baseCol * l;
        });

        /// <summary>Damp, brownish-yellow office carpet, tile = 2 x 2 m.</summary>
        public static Texture2D Carpet => carpet ??= Tile("BR_Carpet", (u, v) =>
        {
            float fib = Noise.FbmTile(u * 64f, v * 64f, 64, 2, 51);
            float tuft = Noise.ValueTile(u * 128f, v * 128f, 128, 52);
            float damp = Mathf.Clamp01((Noise.FbmTile(u * 2f, v * 2f, 2, 4, 53) - 0.55f) * 3f);
            float l = 0.82f + fib * 0.16f + (tuft > 0.85f ? -0.06f : 0f);
            var col = Color.Lerp(new Color(0.6f, 0.52f, 0.33f), new Color(0.4f, 0.33f, 0.2f), damp * 0.8f);
            return col * l;
        });

        /// <summary>Drop-ceiling tiles, 2 x 2 tiles per texture (use 1 uv per metre for 0.5 m tiles).</summary>
        public static Texture2D Ceiling => ceiling ??= Tile("BR_Ceiling", (u, v) =>
        {
            float fu = Frac(u * 2f), fv = Frac(v * 2f);
            bool grid = fu < 0.025f || fv < 0.025f || fu > 0.985f || fv > 0.985f;
            if (grid) return new Color(0.62f, 0.6f, 0.52f);
            float speck = Noise.ValueTile(u * 96f, v * 96f, 96, 61);
            float stain = Mathf.Clamp01((Noise.FbmTile(u * 2f, v * 2f, 2, 4, 62) - 0.62f) * 4f);
            float l = 0.9f - (speck > 0.78f ? 0.07f : 0f);
            var col = Color.Lerp(new Color(0.9f, 0.88f, 0.76f), new Color(0.72f, 0.62f, 0.38f), stain * 0.7f);
            return col * l;
        });

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

        /// <summary>One-point-perspective corridor: yellow walls, carpet, ceiling with glowing panels, a dark doorway.</summary>
        public static void PaintThumb(PixelCanvas c, int W, int H)
        {
            float l = W * 0.36f, r = W * 0.64f, b = H * 0.3f, t = H * 0.64f;
            var ceil = new Color(0.86f, 0.83f, 0.64f);
            var floor = new Color(0.45f, 0.37f, 0.22f);
            var wallL = new Color(0.78f, 0.7f, 0.38f);
            var wallR = new Color(0.68f, 0.6f, 0.32f);
            var end = new Color(0.9f, 0.82f, 0.48f);
            c.Rect(0, 0, W, H, end);
            c.Polygon(new[] { new Vector2(0, H), new Vector2(W, H), new Vector2(r, t), new Vector2(l, t) }, ceil);
            c.Polygon(new[] { new Vector2(0, 0), new Vector2(W, 0), new Vector2(r, b), new Vector2(l, b) }, floor);
            c.Polygon(new[] { new Vector2(0, 0), new Vector2(l, b), new Vector2(l, t), new Vector2(0, H) }, wallL);
            c.Polygon(new[] { new Vector2(W, 0), new Vector2(W, H), new Vector2(r, t), new Vector2(r, b) }, wallR);
            // Wallpaper stripes on the side walls.
            for (int i = 1; i < 14; i++)
            {
                float k = i / 14f, x0 = Mathf.Lerp(0, l, k), x1 = Mathf.Lerp(W, r, k);
                c.Line(x0, Mathf.Lerp(0, b, k), x0, Mathf.Lerp(H, t, k), 1f, new Color(0, 0, 0, 0.06f));
                c.Line(x1, Mathf.Lerp(0, b, k), x1, Mathf.Lerp(H, t, k), 1f, new Color(0, 0, 0, 0.06f));
            }
            // Side doorway on the left wall and a dark opening at the end of the corridor.
            c.Polygon(new[] { new Vector2(l * 0.45f, b * 0.45f), new Vector2(l * 0.7f, b * 0.7f), new Vector2(l * 0.7f, t * 0.9f + H * 0.0f), new Vector2(l * 0.45f, H * 0.8f) }, new Color(0.3f, 0.25f, 0.12f));
            c.Rect(W * 0.47f, b, W * 0.07f, (t - b) * 0.75f, new Color(0.22f, 0.18f, 0.08f));
            // Ceiling panels receding into the distance (one dead).
            for (int i = 0; i < 5; i++)
            {
                float k = 1f - Mathf.Pow(0.62f, i + 0.4f);
                float y = Mathf.Lerp(H, t, k), half = Mathf.Lerp(W * 0.16f, (r - l) * 0.3f, k), th = Mathf.Lerp(16f, 3f, k);
                bool dead = i == 2;
                c.Circle(W * 0.5f, y - th, half * 1.3f, new Color(1f, 0.97f, 0.75f, dead ? 0f : 0.25f), half);
                c.Rect(W * 0.5f - half, y - th * 1.5f, half * 2f, th, dead ? new Color(0.55f, 0.53f, 0.45f) : new Color(1f, 0.99f, 0.9f));
            }
            // Baseboards.
            c.Line(0, H * 0.03f, l, b, 2f, new Color(0.35f, 0.27f, 0.14f));
            c.Line(W, H * 0.03f, r, b, 2f, new Color(0.35f, 0.27f, 0.14f));
            // Haze toward the vanishing point.
            c.Circle(W * 0.5f, (b + t) * 0.5f, W * 0.22f, new Color(1f, 0.92f, 0.6f, 0.18f), W * 0.2f);
        }
    }
}
