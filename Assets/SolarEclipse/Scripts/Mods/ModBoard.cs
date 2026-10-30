using System;
using System.Collections.Generic;
using System.Text;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>
    /// The online Mod board, with no server of our own: every uploaded mod is a retained message on the same free public
    /// MQTT brokers the multiplayer lobby uses, at root/mods/&lt;id&gt; (payload = share code, gzip + base64 JSON, max
    /// 48 KB compressed). Browsing subscribes to root/mods/+. Anyone can technically publish to a public broker, so
    /// every download is checked here: size, data-only validation with the anti-cheat caps, the topic must match the
    /// mod id, and an update must keep the original author's id (first one seen wins; remembered locally).
    /// The developer (logged into the dev panel) can remove any mod by publishing an empty retained message.
    /// </summary>
    public static class ModBoard
    {
        public sealed class Entry
        {
            public ModPackage Pkg;
            public int Bytes;
            public double SeenAt;
        }

        public static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        public static int Rejected { get; private set; }
        static readonly List<MqttLite> brokers = new List<MqttLite>();
        static Dictionary<string, string> owners;
        static string Root => "solareclipse/goresandbox/p" + NetManager.Protocol + "/mods/";

        public static bool Started => brokers.Count > 0;
        public static bool Online { get { foreach (var b in brokers) if (b.Connected) return true; return false; } }

        public static string Status
        {
            get
            {
                if (!Started) return "Offline";
                int on = 0;
                foreach (var b in brokers) if (b.Connected) on++;
                return on > 0 ? "Online (" + on + "/" + brokers.Count + " servers)" : "Connecting...";
            }
        }

        public static void Connect()
        {
            if (Started) return;
            bool restricted = Settings.Data.restrictedNetwork;
            foreach (var b in BrokerCatalog.All)
            {
                var m = new MqttLite("mods-" + b.Name, restricted ? b.Restricted : b.Normal) { Log = s => Debug.Log("[ModBoard] " + s) };
                m.Subscribe(Root + "+");
                brokers.Add(m);
            }
        }

        public static void Shutdown()
        {
            foreach (var b in brokers) { try { b.Dispose(); } catch { } }
            brokers.Clear();
        }

        // ------------------------------------------------------------------ ownership memory

        static Dictionary<string, string> Owners
        {
            get
            {
                if (owners != null) return owners;
                owners = new Dictionary<string, string>();
                var s = PlayerPrefs.GetString("modboard_owners", "");
                foreach (var pair in s.Split(';'))
                {
                    var kv = pair.Split('=');
                    if (kv.Length == 2 && kv[0].Length > 0) owners[kv[0]] = kv[1];
                }
                return owners;
            }
        }

        static void RememberOwner(string id, string guid)
        {
            if (Owners.TryGetValue(id, out var g) && g == guid) return;
            if (Owners.Count > 3000) return;
            Owners[id] = guid;
            var sb = new StringBuilder();
            foreach (var kv in Owners) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
            PlayerPrefs.SetString("modboard_owners", sb.ToString());
        }

        /// <summary>The author id that owns a mod id (the first one seen), or null when unknown.</summary>
        public static string OwnerOf(string id)
        {
            if (Entries.TryGetValue(id, out var e)) return e.Pkg.authorGuid;
            return Owners.TryGetValue(id, out var g) ? g : null;
        }

        // ------------------------------------------------------------------ receive

        public static void Poll()
        {
            if (!Started) return;
            foreach (var b in brokers)
            {
                int budget = 40;
                while (budget-- > 0 && b.Inbox.TryDequeue(out var m))
                {
                    if (!m.topic.StartsWith(Root)) continue;
                    var id = m.topic.Substring(Root.Length);
                    if (ModText.Id(id) != id || id.Length < 6) { Rejected++; continue; }
                    if (m.payload == null || m.payload.Length == 0) { Entries.Remove(id); continue; }
                    if (m.payload.Length > ModCodec.MaxCompressed * 4 / 3 + 64) { Rejected++; continue; }
                    string code;
                    try { code = Encoding.ASCII.GetString(m.payload); } catch { Rejected++; continue; }
                    var p = ModCodec.FromCode(code, out _);
                    if (p == null || p.id != id) { Rejected++; continue; }
                    if (ModStore.Validate(p) != null) { Rejected++; continue; }
                    if (string.IsNullOrEmpty(p.authorGuid)) { Rejected++; continue; }
                    var owner = Owners.TryGetValue(id, out var g) ? g : null;
                    if (owner != null && owner != p.authorGuid) { Rejected++; continue; } // someone else tried to overwrite it
                    if (Entries.TryGetValue(id, out var old) && old.Pkg.version > p.version && old.Pkg.authorGuid == p.authorGuid) continue; // older copy from a slower broker
                    RememberOwner(id, p.authorGuid);
                    Entries[id] = new Entry { Pkg = p, Bytes = m.payload.Length, SeenAt = Time.realtimeSinceStartup };
                }
            }
        }

        // ------------------------------------------------------------------ publish

        /// <summary>Uploads (or updates) one of your mods. Returns null when sent, otherwise why not.</summary>
        public static string Upload(ModPackage src)
        {
            var p = ModStore.Copy(src);
            if (p == null) return "Empty mod.";
            if (!ModStore.IsMine(p)) return "You can only upload mods you made.";
            var owner = OwnerOf(p.id);
            if (owner != null && owner != p.authorGuid) return "Another player owns this mod id online.";
            var err = ModStore.Validate(p);
            if (err != null) return err;
            var code = ModCodec.ToCode(p);
            int compressed = (code.Length - ModCodec.Prefix.Length) * 3 / 4;
            if (compressed > ModCodec.MaxCompressed) return "The mod is too big to upload (" + compressed / 1024 + " KB, max " + ModCodec.MaxCompressed / 1024 + " KB compressed).";
            Connect();
            var bytes = Encoding.ASCII.GetBytes(code);
            foreach (var b in brokers) b.Publish(Root + p.id, bytes, true);
            RememberOwner(p.id, p.authorGuid);
            Entries[p.id] = new Entry { Pkg = p, Bytes = bytes.Length, SeenAt = Time.realtimeSinceStartup };
            return Online ? null : "Queued: not connected yet. It uploads as soon as a server answers (keep this screen open a moment).";
        }

        /// <summary>Removes a mod from the board: its author, or the developer (dev panel login), only.</summary>
        public static string Remove(string id)
        {
            var owner = OwnerOf(id);
            bool mine = owner != null && owner == ModText.Id(ModStore.LocalGuid);
            if (!mine && !DevPanel.LoggedIn) return "Only the author (or the developer) can remove this mod.";
            Connect();
            foreach (var b in brokers) b.Publish(Root + id, new byte[0], true);
            Entries.Remove(id);
            return null;
        }
    }
}
