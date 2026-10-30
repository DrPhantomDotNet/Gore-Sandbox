using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Coloured badge chips (icon + name) for player lists, name tags and the profile screen.</summary>
    public static class BadgeChips
    {
        /// <summary>The badges to show for a mask: every verified badge, or just "Player" when there are none.</summary>
        public static int Shown(int mask) => (mask & ~1) != 0 ? mask & ~1 : 1;

        public static float ChipWidth(BadgeId b, float h, bool compact)
        {
            if (compact) return h;
            int size = Mathf.RoundToInt(h * 0.5f);
            return h * 0.9f + UI.TextWidth(Badges.Names[(int)b].ToUpperInvariant(), size, FontStyle.Bold) + h * 0.4f;
        }

        public static float Width(int mask, float h, bool compact = false, float gap = 6f)
        {
            mask = Shown(mask);
            float w = 0;
            foreach (var b in Order) if ((mask & (1 << (int)b)) != 0) w += ChipWidth(b, h, compact) + gap;
            return Mathf.Max(0, w - gap);
        }

        static readonly BadgeId[] Order = { BadgeId.Developer, BadgeId.Admin, BadgeId.Tester, BadgeId.EarlyAccess, BadgeId.Player };

        /// <summary>Draws the chips left to right from r.x (clipped to r.width). Returns the width used.</summary>
        public static float Draw(Rect r, int mask, bool compact = false, float alpha = 1f, float gap = 6f)
        {
            mask = Shown(mask);
            float x = r.x, h = r.height;
            foreach (var b in Order)
            {
                if ((mask & (1 << (int)b)) == 0) continue;
                float w = ChipWidth(b, h, compact);
                if (x + w > r.xMax + 0.5f) break;
                Chip(new Rect(x, r.y, w, h), b, compact, alpha);
                x += w + gap;
            }
            return Mathf.Max(0, x - gap - r.x);
        }

        public static void Chip(Rect r, BadgeId b, bool compact, float alpha = 1f)
        {
            var c = Badges.Colors[(int)b];
            UI.Rounded(r, c.WithAlpha(0.92f * alpha), true);
            float ih = r.height * 0.66f;
            if (compact)
            {
                UI.Tex(new Rect(r.x + (r.width - ih) * 0.5f, r.y + (r.height - ih) * 0.5f, ih, ih), IconFactory.Get(Badges.Icons[(int)b]), Color.white.WithAlpha(alpha), ScaleMode.ScaleToFit);
                return;
            }
            UI.Tex(new Rect(r.x + r.height * 0.22f, r.y + (r.height - ih) * 0.5f, ih, ih), IconFactory.Get(Badges.Icons[(int)b]), Color.white.WithAlpha(alpha), ScaleMode.ScaleToFit);
            UI.Label(new Rect(r.x + r.height * 0.9f, r.y, r.width - r.height * 0.9f, r.height), Badges.Names[(int)b].ToUpperInvariant(),
                Mathf.RoundToInt(r.height * 0.5f), Color.white.WithAlpha(alpha), TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        /// <summary>A player row element: profile icon, name and badge chips. Returns the right edge used.</summary>
        public static float PlayerLine(Rect r, ProfileData p, string name, Color nameColor, int nameSize = 19, float alpha = 1f)
        {
            float ih = r.height;
            PlayerProfile.DrawIcon(new Rect(r.x, r.y, ih, ih), p, alpha);
            float x = r.x + ih + 10;
            float nw = Mathf.Min(UI.TextWidth(name, nameSize, FontStyle.Bold) + 6, Mathf.Max(80, r.xMax - x - 60));
            UI.Label(new Rect(x, r.y, nw, ih), name, nameSize, nameColor.WithAlpha(alpha), TextAnchor.MiddleLeft, FontStyle.Bold);
            x += nw + 8;
            int mask = p != null ? p.BadgeMask : 0;
            float chipH = Mathf.Min(30, ih * 0.62f);
            bool compact = Width(mask, chipH) > r.xMax - x;
            x += Draw(new Rect(x, r.y + (ih - chipH) * 0.5f, r.xMax - x, chipH), mask, compact, alpha);
            return x;
        }
    }
}
