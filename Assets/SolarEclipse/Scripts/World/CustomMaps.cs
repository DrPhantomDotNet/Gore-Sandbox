using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Player-made maps: files in persistentDataPath/maps/*.json, registered into <see cref="MapCatalog.All"/> (tagged
    /// "Custom") so map select, hosting and joining treat them like built-in maps. Maps received from a multiplayer host
    /// are registered in memory only (tag "Downloaded") and dropped on the next refresh.
    /// </summary>
    public static class CustomMaps
    {
        public const string IdPrefix = "custom_";
        public const string Tag = "Custom";
        const int MaxFiles = 300;
        const long MaxFileBytes = 6 * 1024 * 1024;

        public sealed class Entry
        {
            public string Path, Json;
            public CustomMapData Data;
            public MapDef Def;
            public DateTime Modified;
        }

        public static readonly List<Entry> Local = new List<Entry>();
        static readonly Dictionary<MapDef, string> jsonOf = new Dictionary<MapDef, string>();
        static readonly HashSet<MapDef> registered = new HashSet<MapDef>();

        static string dir;
        public static string Dir
        {
            get
            {
                if (dir != null) return dir;
                var d = System.IO.Path.Combine(Application.persistentDataPath, "maps");
                try { Directory.CreateDirectory(d); dir = d; } catch (Exception e) { Debug.LogWarning("[Maps] " + e.Message); }
                return d;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            try { Refresh(); } catch (Exception e) { Debug.LogException(e); }
        }

        public static bool IsCustom(MapDef d) => d != null && d.Id != null && d.Id.StartsWith(IdPrefix, StringComparison.Ordinal);

        /// <summary>The exact JSON a custom map was built from (sent to multiplayer clients). Null for built-in maps.</summary>
        public static string JsonFor(MapDef d) => d != null && jsonOf.TryGetValue(d, out var j) ? j : null;

        /// <summary>Re-reads the maps folder and re-registers every custom map in the catalog.</summary>
        public static void Refresh()
        {
            Local.Clear();
            string[] files;
            try { files = Directory.GetFiles(Dir, "*.json"); }
            catch (Exception e) { Debug.LogWarning("[Maps] can't list maps: " + e.Message); files = new string[0]; }
            var seen = new HashSet<string>();
            foreach (var f in files)
            {
                if (Local.Count >= MaxFiles) break;
                try
                {
                    var info = new FileInfo(f);
                    if (info.Length > MaxFileBytes) { Debug.LogWarning("[Maps] skipping huge file " + f); continue; }
                    var json = File.ReadAllText(f);
                    var data = CustomMapData.FromJson(json, out var err);
                    if (data == null) { Debug.LogWarning("[Maps] skipping " + Path.GetFileName(f) + ": " + err); continue; }
                    if (!seen.Add(data.Id)) { data.Id = CustomMapData.NewId(); json = data.ToJson(); } // duplicated file: give it its own id
                    var e = new Entry { Path = f, Json = json, Data = data, Modified = info.LastWriteTimeUtc };
                    e.Def = CreateDef(data, json, false);
                    Local.Add(e);
                }
                catch (Exception ex) { Debug.LogWarning("[Maps] couldn't read " + f + ": " + ex.Message); }
            }
            Local.Sort((a, b) => b.Modified.CompareTo(a.Modified));

            var all = MapCatalog.All;
            all.RemoveAll(d => registered.Contains(d));
            registered.Clear();
            jsonOf.Clear();
            foreach (var e in Local) Register(e.Def, e.Json);
        }

        static void Register(MapDef def, string json)
        {
            var all = MapCatalog.All;
            for (int i = all.Count - 1; i >= 0; i--)
                if (all[i].Id == def.Id && registered.Contains(all[i])) { registered.Remove(all[i]); jsonOf.Remove(all[i]); all.RemoveAt(i); }
            all.Add(def);
            registered.Add(def);
            jsonOf[def] = json;
        }

        public static MapDef CreateDef(CustomMapData data, string json, bool downloaded)
        {
            var time = (TimeOfDay)Mathf.Clamp(data.Time, 0, 3);
            Color top, bottom;
            switch (time)
            {
                case TimeOfDay.Sunset: top = new Color(0.25f, 0.25f, 0.5f); bottom = new Color(0.95f, 0.55f, 0.3f); break;
                case TimeOfDay.Night: top = new Color(0.02f, 0.03f, 0.08f); bottom = new Color(0.15f, 0.17f, 0.25f); break;
                case TimeOfDay.BloodMoon: top = new Color(0.2f, 0.0f, 0.03f); bottom = new Color(0.55f, 0.08f, 0.07f); break;
                default: top = new Color(0.25f, 0.5f, 0.9f); bottom = new Color(0.55f, 0.7f, 0.45f); break;
            }
            int props = 0, items = 0, npcs = 0;
            foreach (var o in data.Objects) { if (o.K == "prop") props++; else if (o.K == "item") items++; else if (o.K == "npc") npcs++; }
            var features = new List<string> { data.Objects.Count + " objects" };
            if (props > 0) features.Add(props + " props");
            if (items > 0) features.Add(items + " interactables");
            if (npcs > 0) features.Add(npcs + " Goredoll spawns");
            if (!string.IsNullOrEmpty(data.Author)) features.Add("By " + data.Author);
            var copy = data;
            var def = new MapDef
            {
                Id = IdPrefix + data.Id,
                Name = data.Name,
                Description = string.IsNullOrEmpty(data.Description) ? "A custom map made in the map editor." : data.Description,
                Size = (downloaded ? "Downloaded" : "Custom") + " - " + (data.Indoor ? "indoor" : "outdoor"),
                Glyph = "wrench",
                DefaultTime = time,
                Indoor = data.Indoor,
                Ambience = data.Indoor ? "amb_hum" : "amb_wind",
                Features = features.ToArray(),
                Tags = downloaded ? new[] { Tag, "Downloaded" } : new[] { Tag },
                ThemeTop = top, ThemeBottom = bottom, Accent = new Color(1f, 0.6f, 0.2f),
                Build = m => CustomMapKit.BuildMap(m, copy),
            };
            return def;
        }

        /// <summary>Temporary map (test play from the editor). Not listed in the catalog.</summary>
        public static MapDef CreateTemp(CustomMapData data)
        {
            var json = data.ToJson();
            var parsed = CustomMapData.FromJson(json, out _) ?? data;
            var def = CreateDef(parsed, json, false);
            def.Id = IdPrefix + "test_" + parsed.Id;
            return def;
        }

        // ------------------------------------------------------------------ files

        public static string PathFor(CustomMapData d) => System.IO.Path.Combine(Dir, CustomMapData.SanitizeId(d.Id) + ".json");

        /// <summary>Saves (validates first). Returns null on success or an error message.</summary>
        public static string Save(CustomMapData d)
        {
            try
            {
                d.Validate();
                var path = PathFor(d);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, d.ToJson());
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                Refresh();
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Maps] save failed: " + e);
                return e.Message;
            }
        }

        public static void Delete(Entry e)
        {
            try { if (File.Exists(e.Path)) File.Delete(e.Path); }
            catch (Exception ex) { Debug.LogWarning("[Maps] delete failed: " + ex.Message); }
            Refresh();
        }

        public static CustomMapData NewMap(string name)
        {
            var d = new CustomMapData { Id = CustomMapData.NewId(), Name = name, Author = Settings.Data != null ? CustomMapData.CleanText(Settings.Data.playerName, 24) : "" };
            d.Objects.Add(new CustomMapObj { K = "spawn", Px = 0, Py = 0, Pz = -8 });
            d.Objects.Add(new CustomMapObj { K = "npc", Px = -3, Py = 0, Pz = 6 });
            d.Objects.Add(new CustomMapObj { K = "npc", Px = 3, Py = 0, Pz = 6 });
            d.Validate();
            return d;
        }

        public static string UniqueName(string baseName)
        {
            string n = baseName;
            int i = 2;
            while (Local.Exists(e => string.Equals(e.Data.Name, n, StringComparison.OrdinalIgnoreCase))) n = baseName + " " + i++;
            return n;
        }

        // ------------------------------------------------------------------ multiplayer transfer

        public static byte[] Compress(string json)
        {
            var raw = Encoding.UTF8.GetBytes(json);
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) gz.Write(raw, 0, raw.Length);
                return ms.ToArray();
            }
        }

        public static string Decompress(byte[] data, int maxBytes = 8 * 1024 * 1024)
        {
            using (var ms = new MemoryStream(data))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                var buf = new byte[8192];
                int n;
                while ((n = gz.Read(buf, 0, buf.Length)) > 0)
                {
                    outMs.Write(buf, 0, n);
                    if (outMs.Length > maxBytes) throw new InvalidDataException("map too large");
                }
                return Encoding.UTF8.GetString(outMs.ToArray());
            }
        }

        /// <summary>Client: registers a map the host sent (in memory only) so the following Welcome finds it by id.</summary>
        public static MapDef RegisterDownloaded(string json)
        {
            var data = CustomMapData.FromJson(json, out var err);
            if (data == null) { Debug.LogWarning("[Maps] host sent a bad map: " + err); return null; }
            var def = CreateDef(data, json, true);
            Register(def, json);
            Debug.Log("[Maps] received custom map '" + data.Name + "' (" + data.Objects.Count + " objects) from the host");
            return def;
        }
    }
}
