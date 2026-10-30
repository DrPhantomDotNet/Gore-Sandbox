using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>String sanitising for everything that comes out of a mod (printable ASCII only, length limits).</summary>
    public static class ModText
    {
        public static string Clean(string s, int max, bool multiline = false)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(Math.Min(s.Length, max));
            foreach (char c in s)
            {
                if (sb.Length >= max) break;
                if (c >= 32 && c <= 126) sb.Append(c);
                else if (multiline && c == '\n') sb.Append(c);
                else if (c == '\t' || c == '\r' || c == '\n') sb.Append(' ');
            }
            return sb.ToString().Trim();
        }

        /// <summary>Cleaned + profanity filtered (names and descriptions other players see).</summary>
        public static string Public(string s, int max, bool multiline = false) => ChatFilter.Censor(Clean(s, max, multiline));

        /// <summary>Mod ids are lowercase letters/digits only: safe to use as a file name and an MQTT topic level.</summary>
        public static string Id(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(24);
            foreach (char c in s.ToLowerInvariant())
            {
                if (sb.Length >= 24) break;
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>Share codes and Mod board payloads: "GSMOD1:" + base64(gzip(JSON)). Size limited both ways.</summary>
    public static class ModCodec
    {
        public const string Prefix = "GSMOD1:";
        public const int MaxCompressed = 48 * 1024;
        public const int MaxJson = 200 * 1024;

        public static byte[] Gzip(string text)
        {
            var raw = Encoding.UTF8.GetBytes(text);
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) gz.Write(raw, 0, raw.Length);
                return ms.ToArray();
            }
        }

        /// <summary>Decompresses at most <see cref="MaxJson"/> bytes (zip bombs stop there). Null when too big or broken.</summary>
        public static string Gunzip(byte[] data)
        {
            if (data == null || data.Length == 0 || data.Length > MaxCompressed) return null;
            try
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
                        if (outMs.Length > MaxJson) return null;
                    }
                    return Encoding.UTF8.GetString(outMs.ToArray());
                }
            }
            catch (Exception) { return null; }
        }

        public static string ToJson(ModPackage p) => JsonUtility.ToJson(p);

        public static ModPackage FromJson(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > MaxJson) return null;
            try { return JsonUtility.FromJson<ModPackage>(json); }
            catch (Exception) { return null; }
        }

        public static byte[] ToBytes(ModPackage p) => Gzip(ToJson(p));

        public static ModPackage FromBytes(byte[] gz) => FromJson(Gunzip(gz));

        public static string ToCode(ModPackage p) => Prefix + Convert.ToBase64String(ToBytes(p));

        /// <summary>Reads a share code. Returns null and an error when it isn't a valid mod.</summary>
        public static ModPackage FromCode(string code, out string error)
        {
            error = null;
            code = (code ?? "").Trim();
            int at = code.IndexOf(Prefix, StringComparison.Ordinal);
            if (at < 0) { error = "That isn't a Gore Sandbox mod code (they start with " + Prefix + ")."; return null; }
            var b64 = new StringBuilder(code.Length);
            for (int i = at + Prefix.Length; i < code.Length; i++)
            {
                char c = code[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=') b64.Append(c);
                else if (!char.IsWhiteSpace(c)) break;
            }
            if (b64.Length > MaxCompressed * 4 / 3 + 8) { error = "The mod is too big (max " + MaxCompressed / 1024 + " KB compressed)."; return null; }
            byte[] gz;
            try { gz = Convert.FromBase64String(b64.ToString()); }
            catch (Exception) { error = "The code is incomplete or damaged (copy all of it)."; return null; }
            var p = FromBytes(gz);
            if (p == null) error = "The code is damaged or too big.";
            return p;
        }

        /// <summary>
        /// Generic checks every mod passes before its kind's own validator: data only, clean strings, sane sizes.
        /// Also normalises the strings that other players see (ASCII, profanity filter).
        /// </summary>
        public static string Check(ModPackage p)
        {
            if (p == null) return "Empty mod.";
            if (p.formatVersion < 1 || p.formatVersion > 1) return "This mod was made with a newer version of the game.";
            p.id = ModText.Id(p.id);
            if (p.id.Length < 6) return "The mod has no valid id.";
            p.name = ModText.Public(p.name, 32);
            if (p.name.Length == 0) p.name = "Unnamed mod";
            p.description = ModText.Public(p.description, 300, true);
            p.author = ModText.Public(p.author, 24);
            if (p.author.Length == 0) p.author = "Unknown";
            p.authorGuid = ModText.Id(p.authorGuid);
            if (p.version < 1) p.version = 1;
            if (p.version > 99999) p.version = 99999;
            if (p.data == null) p.data = "";
            if (p.script == null) p.script = "";
            if (p.data.Length > WeaponRules.MaxJson) return "The mod data is too big.";
            if (p.script.Length > 64 * 1024) return "The mod script is too big (max 64 KB).";
            if (!Enum.IsDefined(typeof(ModKind), p.kind)) return "This mod needs a newer version of the game.";
            return null;
        }
    }

    /// <summary>
    /// Local storage: persistentDataPath/mods/installed/&lt;id&gt;.json (mods you play with) and
    /// persistentDataPath/mods/created/&lt;id&gt;.json (mods you made). Nothing is ever written anywhere else, and file
    /// names are only ever the sanitised mod id.
    /// </summary>
    public static class ModStore
    {
        public static readonly List<ModPackage> Installed = new List<ModPackage>();
        public static readonly List<ModPackage> Created = new List<ModPackage>();
        public static bool Booted { get; private set; }

        static string Root => Path.Combine(Application.persistentDataPath, "mods");
        static string Dir(bool created) => Path.Combine(Root, created ? "created" : "installed");
        static string FileFor(bool created, string id) => Path.Combine(Dir(created), ModText.Id(id) + ".json");

        public static string LocalGuid
        {
            get
            {
                if (NetManager.I != null && !string.IsNullOrEmpty(NetManager.I.LocalGuid)) return NetManager.I.LocalGuid;
                var g = PlayerPrefs.GetString("mp_guid", "");
                if (string.IsNullOrEmpty(g)) { g = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString("mp_guid", g); }
                return g;
            }
        }

        public static string LocalName => string.IsNullOrWhiteSpace(Settings.Data.playerName) ? "Player" : Settings.Data.playerName.Trim();

        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static ModPackage NewPackage(ModKind kind, string name)
        {
            return new ModPackage
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 16), name = name, author = LocalName, authorGuid = ModText.Id(LocalGuid),
                kind = (int)kind, version = 1, created = Now, updated = Now,
            };
        }

        public static ModPackage Copy(ModPackage p) => p == null ? null : ModCodec.FromJson(ModCodec.ToJson(p));

        public static bool IsMine(ModPackage p) => p != null && p.authorGuid == ModText.Id(LocalGuid);

        /// <summary>Loads everything from disk and installs the installed mods (weapons sorted by id so catalog order is stable).</summary>
        public static void Boot()
        {
            if (Booted) return;
            Booted = true;
            Installed.Clear(); Created.Clear();
            Installed.AddRange(LoadDir(false));
            Created.AddRange(LoadDir(true));
            Installed.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            var bad = new List<ModPackage>();
            foreach (var p in Installed)
            {
                var err = Validate(p);
                if (err != null) { Debug.LogWarning("[Mods] not loading '" + p.name + "': " + err); bad.Add(p); continue; }
                try { ModHub.Handler(p.Kind)?.Install?.Invoke(p); }
                catch (Exception e) { Debug.LogWarning("[Mods] install failed for '" + p.name + "': " + e.Message); }
            }
            foreach (var p in bad) Installed.Remove(p);
            Debug.Log("[Mods] " + Installed.Count + " installed, " + Created.Count + " created");
        }

        static List<ModPackage> LoadDir(bool created)
        {
            var list = new List<ModPackage>();
            try
            {
                var dir = Dir(created);
                if (!Directory.Exists(dir)) return list;
                foreach (var f in Directory.GetFiles(dir, "*.json"))
                {
                    try
                    {
                        if (new FileInfo(f).Length > ModCodec.MaxJson) continue;
                        var p = ModCodec.FromJson(File.ReadAllText(f));
                        if (p == null || ModCodec.Check(p) != null) continue;
                        if (list.Exists(x => x.id == p.id)) continue;
                        list.Add(p);
                    }
                    catch (Exception e) { Debug.LogWarning("[Mods] couldn't read " + Path.GetFileName(f) + ": " + e.Message); }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Mods] couldn't list mods: " + e.Message); }
            return list;
        }

        static void Write(bool created, ModPackage p)
        {
            try
            {
                Directory.CreateDirectory(Dir(created));
                File.WriteAllText(FileFor(created, p.id), ModCodec.ToJson(p));
            }
            catch (Exception e) { Debug.LogWarning("[Mods] couldn't save: " + e.Message); Toasts.Show("MODS", "Couldn't save the mod: " + e.Message, UI.Bad); }
        }

        static void DeleteFile(bool created, string id)
        {
            try { var f = FileFor(created, id); if (File.Exists(f)) File.Delete(f); }
            catch (Exception e) { Debug.LogWarning("[Mods] couldn't delete: " + e.Message); }
        }

        /// <summary>Generic checks, then the kind's validator (the anti-cheat limits). Null = OK.</summary>
        public static string Validate(ModPackage p)
        {
            var err = ModCodec.Check(p);
            return err ?? ModHub.Validate(p);
        }

        public static ModPackage FindInstalled(string id) => Installed.Find(p => p.id == id);
        public static ModPackage FindCreated(string id) => Created.Find(p => p.id == id);

        /// <summary>Installs (or updates) a mod. Returns null on success, otherwise why it was refused.</summary>
        public static string Install(ModPackage src)
        {
            var p = Copy(src);
            if (p == null) return "Empty mod.";
            var err = Validate(p);
            if (err != null) return err;
            var h = ModHub.Handler(p.Kind);
            var old = FindInstalled(p.id);
            if (old != null && !string.IsNullOrEmpty(old.authorGuid) && old.authorGuid != p.authorGuid)
                return "A different author's mod already uses this id. Uninstall it first.";
            if (old != null) Installed.Remove(old);
            Installed.Add(p);
            Write(false, p);
            try { h?.Install?.Invoke(p); }
            catch (Exception e) { Debug.LogWarning("[Mods] install failed: " + e.Message); return "Install failed: " + e.Message; }
            return null;
        }

        public static void Uninstall(string id)
        {
            var p = FindInstalled(id);
            if (p == null) return;
            Installed.Remove(p);
            DeleteFile(false, id);
            try { ModHub.Handler(p.Kind)?.Uninstall?.Invoke(p); }
            catch (Exception e) { Debug.LogWarning("[Mods] uninstall failed: " + e.Message); }
        }

        /// <summary>Saves a mod you made (MY MODS). Returns null on success.</summary>
        public static string SaveCreated(ModPackage src)
        {
            var p = Copy(src);
            if (p == null) return "Empty mod.";
            if (string.IsNullOrEmpty(p.authorGuid)) p.authorGuid = ModText.Id(LocalGuid);
            p.updated = Now;
            var err = Validate(p);
            if (err != null) return err;
            Created.RemoveAll(x => x.id == p.id);
            Created.Insert(0, p);
            Write(true, p);
            // Keep an installed copy of your own mod in sync.
            if (FindInstalled(p.id) != null) Install(p);
            return null;
        }

        public static void DeleteCreated(string id)
        {
            Created.RemoveAll(x => x.id == id);
            DeleteFile(true, id);
        }

        /// <summary>Imports a share code: your own mods go to MY MODS, everyone else's get installed.</summary>
        public static string Import(string code, out ModPackage result)
        {
            result = ModCodec.FromCode(code, out var err);
            if (result == null) return err;
            err = Validate(result);
            if (err != null) return err;
            if (IsMine(result)) return SaveCreated(result);
            return Install(result);
        }
    }

    /// <summary>Boots the mod system after every handler registered itself, polls the Mod board, renders icons.</summary>
    public sealed class ModHubRunner : MonoBehaviour
    {
        public static ModHubRunner I { get; private set; }
        int frames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (I != null) return;
            var go = new GameObject("ModHub");
            DontDestroyOnLoad(go);
            I = go.AddComponent<ModHubRunner>();
            ModWeapons.Register();
        }

        void Update()
        {
            // Wait a couple of frames so every mod kind (scripted weapons, NPCs) has registered its handler.
            if (!ModStore.Booted && ++frames > 3)
            {
                try { ModStore.Boot(); }
                catch (Exception e) { Debug.LogWarning("[Mods] boot failed: " + e.Message); }
            }
            try { ModBoard.Poll(); } catch (Exception e) { Debug.LogWarning("[Mods] board: " + e.Message); }
            try { ModWeapons.Tick(); } catch (Exception e) { Debug.LogWarning("[Mods] weapons: " + e.Message); }
            try { ModNet.Tick(); } catch (Exception e) { Debug.LogWarning("[Mods] net: " + e.Message); }
        }

        void OnApplicationQuit() => ModBoard.Shutdown();
    }
}
