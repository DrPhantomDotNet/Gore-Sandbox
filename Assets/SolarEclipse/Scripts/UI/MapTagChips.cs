using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Draws a map's <see cref="MapDef.Tags"/> as small chips (map cards, details hero, loading screen).</summary>
    public static class MapTagChips
    {
        /// <summary>Draws the tags left to right starting at (x, y). Returns the total width used.</summary>
        public static float Draw(float x, float y, string[] tags, int fontSize = 14, float alpha = 1f, float maxWidth = 10000f)
        {
            if (tags == null || tags.Length == 0) return 0f;
            float h = fontSize + 12f, start = x;
            foreach (var tag in tags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                var text = tag.ToUpperInvariant();
                float w = UI.TextWidth(text, fontSize, FontStyle.Bold) + 18f;
                if (x + w - start > maxWidth) break;
                var r = new Rect(x, y, w, h);
                UI.Rounded(r, new Color(0.04f, 0.04f, 0.05f, 0.82f * alpha), true);
                UI.Rounded(new Rect(r.x + 4, r.y + 4, 3, h - 8), UI.Accent2.WithAlpha(alpha), true);
                UI.Label(new Rect(r.x + 11, r.y, w - 11, h), text, fontSize, UI.Accent2.WithAlpha(alpha), TextAnchor.MiddleLeft, FontStyle.Bold, false);
                x += w + 6f;
            }
            return x - start;
        }
    }
}
