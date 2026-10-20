using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Deterministic value noise / fBm used by texture and sound generators.</summary>
    public static class Noise
    {
        public static float Hash(int x, int y, int seed = 0)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x00FFFFFF) / 16777215f;
            }
        }

        public static float Value(float x, float y, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Tileable value noise with integer period.</summary>
        public static float ValueTile(float x, float y, int period, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period), y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        public static float Fbm(float x, float y, int octaves, int seed = 0)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(x, y, seed + i * 31) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return sum / norm;
        }

        public static float FbmTile(float x, float y, int period, int octaves, int seed = 0)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int p = period;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueTile(x, y, p, seed + i * 31) * amp;
                norm += amp;
                x *= 2f; y *= 2f; p *= 2; amp *= 0.5f;
            }
            return sum / norm;
        }

        static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
    }
}
