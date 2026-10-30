using System;
using System.Collections.Generic;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// What other players see of you: name, profile icon (glyph on a coloured circle), hat, body look (a CustomLook
    /// code) and badge certificates. Sent to the host in multiplayer (Net/NetManager.Profile.cs).
    /// </summary>
    public sealed class ProfileData
    {
        public const byte WireVersion = 1;
        public const int MaxCerts = 8;

        public string Name = "Player";
        public int Icon;
        public Color IconColor = new Color(0.86f, 0.11f, 0.13f);
        public int Hat;
        public Color HatColor = new Color(0.15f, 0.15f, 0.17f);
        public string Look = "";
        public byte[] Subject = new byte[32];
        public List<byte[]> Certs = new List<byte[]>();
        /// <summary>Bumped whenever the profile changes (avatars rebuild on change).</summary>
        public int Version;

        int mask = -1;
        string lookCode;
        CustomLook look;

        /// <summary>Verified badges (bitmask of 1 &lt;&lt; BadgeId).</summary>
        public int BadgeMask { get { if (mask < 0) mask = Badges.MaskFrom(Subject, Certs); return mask; } }
        public void Changed() { mask = -1; Version++; }

        /// <summary>The decoded body look, or null for the default (seeded from the name).</summary>
        public CustomLook BodyLook
        {
            get
            {
                if (lookCode != Look) { lookCode = Look; look = string.IsNullOrEmpty(Look) ? null : CustomLook.Decode(Look); }
                return look;
            }
        }

        public void Write(NetWriter w)
        {
            w.Byte(WireVersion);
            w.String(Name ?? "");
            w.Byte((byte)Mathf.Clamp(Icon, 0, 255));
            Col(w, IconColor);
            w.Byte((byte)Mathf.Clamp(Hat, 0, 255));
            Col(w, HatColor);
            w.String(Look ?? "");
            w.BlockWithLength(Subject ?? new byte[32], 0, 32);
            int n = Mathf.Min(Certs.Count, MaxCerts);
            w.Byte((byte)n);
            for (int i = 0; i < n; i++) w.BlockWithLength(Certs[i], 0, Certs[i].Length);
        }

        public static ProfileData Read(NetReader r)
        {
            if (r.Remaining <= 0 || r.Byte() != WireVersion) return null;
            var p = new ProfileData
            {
                Name = r.String(), Icon = r.Byte(), IconColor = Col(r), Hat = r.Byte(), HatColor = Col(r), Look = r.String(),
            };
            var s = r.BlockWithLength();
            if (s.Length == 32) p.Subject = s;
            int n = r.Byte();
            for (int i = 0; i < n && i < MaxCerts && r.Remaining > 0; i++)
            {
                var c = r.BlockWithLength();
                if (c.Length > 0 && c.Length <= 1024) p.Certs.Add(c);
            }
            if (r.Overflow) return null;
            p.Sanitize();
            return p;
        }

        public void Sanitize()
        {
            Name = (Name ?? "").Trim();
            if (Name.Length > 20) Name = Name.Substring(0, 20);
            if (Name.Length == 0) Name = "Player";
            Icon = Mathf.Clamp(Icon, 0, PlayerProfile.IconGlyphs.Length - 1);
            Hat = Mathf.Clamp(Hat, 0, Hats.Count - 1);
            if (Look != null && (Look.Length > 120 || CustomLook.Decode(Look) == null)) Look = "";
            IconColor.a = 1f; HatColor.a = 1f;
            Changed();
        }

        static void Col(NetWriter w, Color c) { w.Byte(Q(c.r)); w.Byte(Q(c.g)); w.Byte(Q(c.b)); }
        static Color Col(NetReader r) => new Color(r.Byte() / 255f, r.Byte() / 255f, r.Byte() / 255f, 1f);
        static byte Q(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(v) * 255f), 0, 255);
    }

    /// <summary>The local player's profile, saved in PlayerPrefs, plus icon drawing helpers.</summary>
    public static class PlayerProfile
    {
        /// <summary>Profile icon glyphs (IconFactory names).</summary>
        public static readonly string[] IconGlyphs =
        {
            "skull", "heart", "star", "crown", "fire", "lightning", "ghost", "duck", "diamond", "moon", "sun", "eclipse",
            "rocket", "katana", "gun", "knife", "brain", "eyeball", "clown", "dice", "music", "nextbot", "bones", "joystick",
        };

        /// <summary>Colours for icon backgrounds and hats.</summary>
        public static readonly Color[] Palette =
        {
            new Color(0.86f, 0.11f, 0.13f), new Color(1f, 0.55f, 0.16f), new Color(0.95f, 0.8f, 0.2f), new Color(0.35f, 0.75f, 0.3f),
            new Color(0.15f, 0.65f, 0.6f), new Color(0.2f, 0.45f, 0.9f), new Color(0.5f, 0.3f, 0.85f), new Color(0.9f, 0.4f, 0.7f),
            new Color(0.55f, 0.35f, 0.2f), new Color(0.9f, 0.9f, 0.88f), new Color(0.45f, 0.45f, 0.5f), new Color(0.12f, 0.12f, 0.14f),
        };

        static ProfileData local;
        const string KIcon = "profile_icon", KIconCol = "profile_icon_color", KHat = "profile_hat", KHatCol = "profile_hat_color",
            KLook = "profile_look", KCerts = "profile_badges";

        public static string Guid
        {
            get
            {
                if (NetManager.I != null && !string.IsNullOrEmpty(NetManager.I.LocalGuid)) return NetManager.I.LocalGuid;
                var g = PlayerPrefs.GetString("mp_guid", "");
                if (string.IsNullOrEmpty(g)) { g = System.Guid.NewGuid().ToString("N"); PlayerPrefs.SetString("mp_guid", g); PlayerPrefs.Save(); }
                return g;
            }
        }

        public static string LocalName => string.IsNullOrWhiteSpace(Settings.Data.playerName) ? "Player" : Settings.Data.playerName.Trim();

        public static ProfileData Local
        {
            get
            {
                if (local == null) Load();
                if (local.Name != LocalName) { local.Name = LocalName; local.Version++; }
                return local;
            }
        }

        static void Load()
        {
            local = new ProfileData();
            try
            {
                local.Icon = Mathf.Clamp(PlayerPrefs.GetInt(KIcon, UnityEngine.Random.Range(0, IconGlyphs.Length)), 0, IconGlyphs.Length - 1);
                local.IconColor = Hex(PlayerPrefs.GetString(KIconCol, ""), Palette[UnityEngine.Random.Range(0, Palette.Length)]);
                local.Hat = Mathf.Clamp(PlayerPrefs.GetInt(KHat, 0), 0, Hats.Count - 1);
                local.HatColor = Hex(PlayerPrefs.GetString(KHatCol, ""), Palette[11]);
                local.Look = PlayerPrefs.GetString(KLook, "");
                if (CustomLook.Decode(local.Look) == null) local.Look = "";
                foreach (var s in PlayerPrefs.GetString(KCerts, "").Split(','))
                {
                    if (s.Length == 0) continue;
                    try { local.Certs.Add(Convert.FromBase64String(s)); } catch { }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Profile] load failed: " + e.Message); }
            local.Name = LocalName;
            local.Subject = Badges.SubjectFor(Guid);
            local.Changed();
            if (!PlayerPrefs.HasKey(KIcon)) Save(false);
        }

        /// <summary>Saves the local profile and tells the server about it.</summary>
        public static void Save(bool announce = true)
        {
            var p = local ?? Local;
            p.Name = LocalName;
            try
            {
                PlayerPrefs.SetInt(KIcon, p.Icon);
                PlayerPrefs.SetString(KIconCol, ColorUtility.ToHtmlStringRGB(p.IconColor));
                PlayerPrefs.SetInt(KHat, p.Hat);
                PlayerPrefs.SetString(KHatCol, ColorUtility.ToHtmlStringRGB(p.HatColor));
                PlayerPrefs.SetString(KLook, p.Look ?? "");
                var parts = new List<string>();
                foreach (var c in p.Certs) parts.Add(Convert.ToBase64String(c));
                PlayerPrefs.SetString(KCerts, string.Join(",", parts));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("[Profile] save failed: " + e.Message); }
            p.Changed();
            if (announce) NetManager.I?.OnLocalProfileChanged();
        }

        /// <summary>Stores a badge certificate issued to this device. Returns the badge when it changed something.</summary>
        public static bool AddCert(byte[] raw, out Badges.Cert cert)
        {
            cert = Badges.Parse(raw);
            var p = Local;
            if (cert == null || !BadgeCrypto.Same(cert.Subject, p.Subject) || !Badges.Verify(raw)) { cert = null; return false; }
            int before = p.BadgeMask;
            long newest = long.MinValue;
            for (int i = p.Certs.Count - 1; i >= 0; i--)
            {
                var o = Badges.Parse(p.Certs[i]);
                if (o == null || o.Badge != cert.Badge) continue;
                if (o.Issued > cert.Issued) newest = Math.Max(newest, o.Issued);
                else p.Certs.RemoveAt(i);
            }
            if (newest != long.MinValue) return false; // we already hold a newer certificate for this badge
            if (cert.Grant) p.Certs.Add(raw);
            while (p.Certs.Count > ProfileData.MaxCerts) p.Certs.RemoveAt(0);
            p.Changed();
            Save();
            return p.BadgeMask != before;
        }

        public static CustomLook DefaultLook(string name)
        {
            var st = UnityEngine.Random.state;
            UnityEngine.Random.InitState((name ?? "p").GetHashCode() ^ 0x5EED);
            var a = Appearance.Random(-1);
            UnityEngine.Random.state = st;
            return CustomLook.FromAppearance(a);
        }

        static Color Hex(string s, Color fallback)
        {
            if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString("#" + s, out var c)) { c.a = 1f; return c; }
            return fallback;
        }

        // ------------------------------------------------------------------ drawing

        public static void DrawIcon(Rect r, ProfileData p, float alpha = 1f)
        {
            if (p == null) { DrawIcon(r, -1, Palette[10], alpha); return; }
            DrawIcon(r, p.Icon, p.IconColor, alpha);
        }

        /// <summary>Glyph on a coloured circle. index &lt; 0 draws a plain person.</summary>
        public static void DrawIcon(Rect r, int index, Color bg, float alpha = 1f)
        {
            bg.a = alpha;
            UI.Tex(new Rect(r.x - 1, r.y + 1, r.width + 2, r.height + 2), TextureFactory.Circle, new Color(0, 0, 0, 0.45f * alpha));
            UI.Tex(r, TextureFactory.Circle, bg);
            string glyph = index >= 0 && index < IconGlyphs.Length ? IconGlyphs[index] : "person";
            float pad = r.width * 0.2f;
            bool light = bg.r * 0.3f + bg.g * 0.59f + bg.b * 0.11f > 0.72f;
            UI.Tex(new Rect(r.x + pad, r.y + pad, r.width - pad * 2, r.height - pad * 2), IconFactory.Get(glyph), (light ? new Color(0.1f, 0.1f, 0.12f) : Color.white).WithAlpha(alpha), ScaleMode.ScaleToFit);
        }
    }
}
