using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// GS2 cloud-free profile backup: your lifetime stats and unlocked achievements as one text code (GSPRO1:...).
    /// Copy it, keep it or paste it on another device. Importing merges: each stat keeps the higher value.
    /// </summary>
    public static class ProfileCode
    {
        const string Prefix = "GSPRO1:";

        static IEnumerable<string> StatNames()
        {
            var set = new HashSet<string>();
            foreach (var a in Stats.Achievements) if (!string.IsNullOrEmpty(a.Stat)) set.Add(a.Stat);
            foreach (var f in typeof(Stats).GetFields()) if (f.IsLiteral && f.FieldType == typeof(string)) set.Add((string)f.GetRawConstantValue());
            foreach (var s in new[] { "xp", "level", "punches", "fight_landed" }) set.Add(s);
            return set;
        }

        public static string Export()
        {
            var sb = new StringBuilder();
            foreach (var s in StatNames()) { float v = Stats.Get(s); if (v > 0f) sb.Append(s).Append('=').Append(v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(';'); }
            sb.Append('|');
            foreach (var a in Stats.Achievements) if (Stats.IsUnlocked(a)) sb.Append(a.Id).Append(',');
            return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        /// <summary>Returns a message for the player.</summary>
        public static string Import(string code)
        {
            code = (code ?? "").Trim();
            if (!code.StartsWith(Prefix)) return "That's not a Gore Sandbox profile code.";
            string body;
            try { body = Encoding.UTF8.GetString(Convert.FromBase64String(code.Substring(Prefix.Length))); }
            catch { return "The code is damaged (copy all of it)."; }
            var parts = body.Split('|');
            int stats = 0, achs = 0;
            foreach (var kv in parts[0].Split(';'))
            {
                int eq = kv.IndexOf('=');
                if (eq <= 0) continue;
                string k = kv.Substring(0, eq);
                if (k.Length > 64 || !float.TryParse(kv.Substring(eq + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) || float.IsNaN(v) || v < 0f) continue;
                if (v > Stats.Get(k)) { PlayerPrefs.SetFloat("stat_" + k, v); stats++; }
            }
            if (parts.Length > 1)
                foreach (var id in parts[1].Split(','))
                {
                    if (id.Length == 0 || id.Length > 64) continue;
                    var a = Stats.Achievements.Find(x => x.Id == id);
                    if (a != null && !Stats.IsUnlocked(a)) { PlayerPrefs.SetInt("ach_" + id, 1); achs++; }
                }
            PlayerPrefs.Save();
            Stats.ClearCache();
            return "Profile imported: " + stats + " stats raised, " + achs + " achievements unlocked.";
        }
    }
}
