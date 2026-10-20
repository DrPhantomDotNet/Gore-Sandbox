using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Pure C# (no UnityEngine): the saved format of player-made maps, its compact JSON writer/reader and validation.
// Kept engine-free so it can be unit tested offline.
namespace SolarEclipse
{
    /// <summary>One placed object of a custom map. Pivot = bottom centre; rotation in degrees (Euler XYZ).</summary>
    public sealed class CustomMapObj
    {
        /// <summary>Kind: block, floor, wall, platform, ramp, stairs, pillar, prop, item, light, spawn, npc.</summary>
        public string K = "block";
        /// <summary>Prop id (PropFactory) or interactable id for kind prop / item.</summary>
        public string Id = "";
        public float Px, Py, Pz, Rx, Ry, Rz, Sx = 1, Sy = 1, Sz = 1;
        public int Mat;
        public float Cr = 1, Cg = 1, Cb = 1;
        /// <summary>Lights: range (m) and intensity.</summary>
        public float Range = 12f, Power = 1.6f;

        public CustomMapObj Clone() => (CustomMapObj)MemberwiseClone();
    }

    /// <summary>A player-made map as saved in persistentDataPath/maps/*.json (and sent to multiplayer clients).</summary>
    public sealed class CustomMapData
    {
        public const int MaxObjects = 2000;
        public const int FormatVersion = 1;
        public static readonly string[] Kinds = { "block", "floor", "wall", "platform", "ramp", "stairs", "pillar", "prop", "item", "light", "spawn", "npc" };

        public string Id = "", Name = "My Map", Description = "", Author = "";
        public int Time;            // TimeOfDay
        public bool Ground = true;
        public float GroundSize = 120f;
        public int GroundMat = 1;   // grass
        public int Npcs = -1;       // -1 = use the map select slider
        public int Fog;             // 0 default, 1 off, 2 thin, 3 thick
        public bool Indoor;
        public bool Walls = true;   // invisible boundary walls around the playable area
        public List<CustomMapObj> Objects = new List<CustomMapObj>();

        public CustomMapData Clone()
        {
            var c = (CustomMapData)MemberwiseClone();
            c.Objects = new List<CustomMapObj>(Objects.Count);
            foreach (var o in Objects) c.Objects.Add(o.Clone());
            return c;
        }

        public static string NewId()
        {
            var b = Guid.NewGuid().ToByteArray();
            var sb = new StringBuilder("m");
            for (int i = 0; i < 5; i++) sb.Append(b[i].ToString("x2"));
            return sb.ToString();
        }

        /// <summary>Lowercase a-z 0-9 _ - only, max 32 chars (safe as a file name and map id).</summary>
        public static string SanitizeId(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in (s ?? "").ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '-') sb.Append(ch);
                if (sb.Length >= 32) break;
            }
            return sb.ToString();
        }

        /// <summary>Plain printable ASCII, trimmed and length-limited.</summary>
        public static string CleanText(string s, int max)
        {
            var sb = new StringBuilder();
            foreach (var ch in s ?? "")
            {
                if (ch == '\n' && max > 60) { sb.Append(' '); continue; }
                if (ch >= 32 && ch <= 126) sb.Append(ch);
                if (sb.Length >= max) break;
            }
            return sb.ToString().Trim();
        }

        static float F(float v, float min, float max, float def)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return def;
            v = Math.Max(min, Math.Min(max, v));
            return (float)Math.Round(v, 3);
        }

        /// <summary>Clamps every value into a sane range, drops unknown kinds and enforces the object limit.</summary>
        public void Validate()
        {
            Id = SanitizeId(Id);
            if (Id.Length == 0) Id = NewId();
            Name = CleanText(Name, 40);
            if (Name.Length == 0) Name = "Untitled Map";
            Description = CleanText(Description, 300);
            Author = CleanText(Author, 24);
            Time = Math.Max(0, Math.Min(3, Time));
            GroundSize = F(GroundSize, 10f, 600f, 120f);
            GroundMat = Math.Max(0, Math.Min(63, GroundMat));
            Npcs = Math.Max(-1, Math.Min(30, Npcs));
            Fog = Math.Max(0, Math.Min(3, Fog));
            if (Objects == null) Objects = new List<CustomMapObj>();
            Objects.RemoveAll(o => o == null || Array.IndexOf(Kinds, o.K) < 0);
            if (Objects.Count > MaxObjects) Objects.RemoveRange(MaxObjects, Objects.Count - MaxObjects);
            foreach (var o in Objects)
            {
                o.Id = SanitizeId(o.Id);
                o.Px = F(o.Px, -1500, 1500, 0); o.Py = F(o.Py, -500, 1000, 0); o.Pz = F(o.Pz, -1500, 1500, 0);
                o.Rx = F(o.Rx % 360f, -360, 360, 0); o.Ry = F(o.Ry % 360f, -360, 360, 0); o.Rz = F(o.Rz % 360f, -360, 360, 0);
                o.Sx = F(o.Sx, 0.05f, 200, 1); o.Sy = F(o.Sy, 0.05f, 200, 1); o.Sz = F(o.Sz, 0.05f, 200, 1);
                o.Mat = Math.Max(0, Math.Min(63, o.Mat));
                o.Cr = F(o.Cr, 0, 1, 1); o.Cg = F(o.Cg, 0, 1, 1); o.Cb = F(o.Cb, 0, 1, 1);
                o.Range = F(o.Range, 1, 60, 12); o.Power = F(o.Power, 0, 8, 1.6f);
            }
        }

        // ------------------------------------------------------------------ JSON

        static string N(float v) => ((double)v).ToString("0.###", CultureInfo.InvariantCulture);

        static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var ch in s ?? "")
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 32 || ch > 126) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
        }

        static void Vec(StringBuilder sb, string key, float a, float b, float c)
        {
            sb.Append(",\"").Append(key).Append("\":[").Append(N(a)).Append(',').Append(N(b)).Append(',').Append(N(c)).Append(']');
        }

        /// <summary>Compact JSON (one object per line so files stay diff/readable).</summary>
        public string ToJson()
        {
            var sb = new StringBuilder(256 + Objects.Count * 80);
            sb.Append("{\"v\":").Append(FormatVersion);
            sb.Append(",\"id\":"); Str(sb, Id);
            sb.Append(",\"name\":"); Str(sb, Name);
            sb.Append(",\"desc\":"); Str(sb, Description);
            sb.Append(",\"author\":"); Str(sb, Author);
            sb.Append(",\"time\":").Append(Time);
            sb.Append(",\"ground\":").Append(Ground ? "true" : "false");
            sb.Append(",\"groundSize\":").Append(N(GroundSize));
            sb.Append(",\"groundMat\":").Append(GroundMat);
            sb.Append(",\"npcs\":").Append(Npcs);
            sb.Append(",\"fog\":").Append(Fog);
            sb.Append(",\"indoor\":").Append(Indoor ? "true" : "false");
            sb.Append(",\"walls\":").Append(Walls ? "true" : "false");
            sb.Append(",\"objects\":[");
            for (int i = 0; i < Objects.Count; i++)
            {
                var o = Objects[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("{\"k\":"); Str(sb, o.K);
                if (!string.IsNullOrEmpty(o.Id)) { sb.Append(",\"id\":"); Str(sb, o.Id); }
                Vec(sb, "p", o.Px, o.Py, o.Pz);
                if (o.Rx != 0 || o.Ry != 0 || o.Rz != 0) Vec(sb, "r", o.Rx, o.Ry, o.Rz);
                if (o.Sx != 1 || o.Sy != 1 || o.Sz != 1) Vec(sb, "s", o.Sx, o.Sy, o.Sz);
                if (o.Mat != 0) sb.Append(",\"m\":").Append(o.Mat);
                if (o.Cr != 1 || o.Cg != 1 || o.Cb != 1) Vec(sb, "c", o.Cr, o.Cg, o.Cb);
                if (o.K == "light") sb.Append(",\"l\":[").Append(N(o.Range)).Append(',').Append(N(o.Power)).Append(']');
                sb.Append('}');
            }
            sb.Append("\n]}");
            return sb.ToString();
        }

        /// <summary>Parses and validates. Never throws: returns null with an error message for bad input.</summary>
        public static CustomMapData FromJson(string json, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrEmpty(json)) { error = "empty file"; return null; }
                if (json.Length > 8 * 1024 * 1024) { error = "file too large"; return null; }
                if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) { error = "not a JSON object"; return null; }
                var d = new CustomMapData
                {
                    Id = S(root, "id", ""), Name = S(root, "name", "Untitled Map"), Description = S(root, "desc", ""), Author = S(root, "author", ""),
                    Time = (int)D(root, "time", 0), Ground = B(root, "ground", true), GroundSize = (float)D(root, "groundSize", 120),
                    GroundMat = (int)D(root, "groundMat", 1), Npcs = (int)D(root, "npcs", -1), Fog = (int)D(root, "fog", 0),
                    Indoor = B(root, "indoor", false), Walls = B(root, "walls", true),
                };
                if (root.TryGetValue("objects", out var ol) && ol is List<object> list)
                {
                    foreach (var item in list)
                    {
                        if (d.Objects.Count >= MaxObjects) break;
                        if (!(item is Dictionary<string, object> od)) continue;
                        var o = new CustomMapObj { K = S(od, "k", ""), Id = S(od, "id", ""), Mat = (int)D(od, "m", 0) };
                        V(od, "p", ref o.Px, ref o.Py, ref o.Pz);
                        V(od, "r", ref o.Rx, ref o.Ry, ref o.Rz);
                        V(od, "s", ref o.Sx, ref o.Sy, ref o.Sz);
                        V(od, "c", ref o.Cr, ref o.Cg, ref o.Cb);
                        float unused = 0;
                        V(od, "l", ref o.Range, ref o.Power, ref unused);
                        d.Objects.Add(o);
                    }
                }
                d.Validate();
                return d;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        static string S(Dictionary<string, object> d, string k, string def) => d.TryGetValue(k, out var v) && v is string s ? s : def;
        static bool B(Dictionary<string, object> d, string k, bool def) => d.TryGetValue(k, out var v) && v is bool b ? b : def;
        static double D(Dictionary<string, object> d, string k, double def)
        {
            if (!d.TryGetValue(k, out var v) || !(v is double x) || double.IsNaN(x) || double.IsInfinity(x)) return def;
            return Math.Max(-1e6, Math.Min(1e6, x));
        }

        static void V(Dictionary<string, object> d, string k, ref float a, ref float b, ref float c)
        {
            if (!d.TryGetValue(k, out var v) || !(v is List<object> l)) return;
            if (l.Count > 0 && l[0] is double x) a = (float)x;
            if (l.Count > 1 && l[1] is double y) b = (float)y;
            if (l.Count > 2 && l[2] is double z) c = (float)z;
        }
    }

    /// <summary>Minimal JSON reader: objects -> Dictionary, arrays -> List, numbers -> double, plus string/bool/null.</summary>
    public static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Value(s, ref i, 0);
            Ws(s, ref i);
            if (i != s.Length) throw new FormatException("trailing characters at " + i);
            return v;
        }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i, int depth)
        {
            if (depth > 32) throw new FormatException("nested too deep");
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>();
                Ws(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    if (i >= s.Length || s[i] != '"') throw new FormatException("expected key at " + i);
                    var key = Str(s, ref i);
                    Ws(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException("expected : at " + i);
                    i++;
                    d[key] = Value(s, ref i, depth + 1);
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return d; }
                    throw new FormatException("expected , or } at " + i);
                }
            }
            if (c == '[')
            {
                i++;
                var l = new List<object>();
                Ws(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i, depth + 1));
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return l; }
                    throw new FormatException("expected , or ] at " + i);
                }
            }
            if (c == '"') return Str(s, ref i);
            if (Match(s, ref i, "true")) return true;
            if (Match(s, ref i, "false")) return false;
            if (Match(s, ref i, "null")) return null;
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException("unexpected character at " + i);
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static bool Match(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static string Str(string s, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw new FormatException("unterminated string");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw new FormatException("bad escape");
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("bad unicode escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
        }
    }
}
