using System;
using System.Collections.Generic;
using System.Text;

namespace SolarEclipse.Net
{
    /// <summary>A joiner's request, posted to the host's lobby topic so the host can punch a hole toward it.</summary>
    public sealed class JoinIntent
    {
        public string Name = "", PublicEndpoint = "", LanEndpoint = "", Nonce = "";

        public byte[] Serialize()
        {
            var w = new NetWriter(128);
            w.String(Name); w.String(PublicEndpoint); w.String(LanEndpoint); w.String(Nonce);
            return w.ToArray();
        }

        public static JoinIntent Deserialize(byte[] d)
        {
            var r = new NetReader(d, 0, d.Length);
            var j = new JoinIntent { Name = r.String(), PublicEndpoint = r.String(), LanEndpoint = r.String(), Nonce = r.String() };
            return r.Overflow ? null : j;
        }
    }

    /// <summary>Free public MQTT brokers. Each is reachable several ways; school mode only uses web ports.</summary>
    public static class BrokerCatalog
    {
        public sealed class Broker
        {
            public string Name;
            public BrokerEndpoint[] Normal, Restricted;
        }

        public static readonly Broker[] All =
        {
            // Eclipse's broker also answers secure WebSockets on 443 — the port every network allows (like a website).
            new Broker { Name = "eclipse",
                Normal = new[] { BrokerEndpoint.Tcp("mqtt.eclipseprojects.io"), BrokerEndpoint.Wss("mqtt.eclipseprojects.io", 443) },
                Restricted = new[] { BrokerEndpoint.Wss("mqtt.eclipseprojects.io", 443), BrokerEndpoint.Ws("mqtt.eclipseprojects.io", 80) } },
            new Broker { Name = "emqx",
                Normal = new[] { BrokerEndpoint.Tcp("broker.emqx.io"), BrokerEndpoint.Wss("broker.emqx.io", 8084) },
                Restricted = new[] { BrokerEndpoint.Wss("broker.emqx.io", 8084), BrokerEndpoint.Ws("broker.emqx.io", 8083) } },
            new Broker { Name = "hivemq",
                Normal = new[] { BrokerEndpoint.Tcp("broker.hivemq.com"), BrokerEndpoint.Wss("broker.hivemq.com", 8884) },
                Restricted = new[] { BrokerEndpoint.Wss("broker.hivemq.com", 8884), BrokerEndpoint.Ws("broker.hivemq.com", 8000) } },
            new Broker { Name = "mosquitto",
                Normal = new[] { BrokerEndpoint.Tcp("test.mosquitto.org"), BrokerEndpoint.Wss("test.mosquitto.org", 8081, "/") },
                Restricted = new[] { BrokerEndpoint.Wss("test.mosquitto.org", 8081, "/"), BrokerEndpoint.Ws("test.mosquitto.org", 8080, "/") } },
        };
    }

    /// <summary>
    /// Online server list + relay over free public MQTT brokers, connected to several brokers at once so one
    /// being down (or blocked) doesn't matter. Hosts keep a retained "server card" on root/servers/&lt;id&gt;
    /// (refreshed every 15 s, cleared on shutdown); browsers subscribe to root/servers/+; joiners publish to
    /// root/join/&lt;id&gt;; relayed game packets go to root/r/&lt;peer&gt;.
    /// </summary>
    public sealed class Lobby : IDisposable
    {
        public readonly List<MqttLite> Brokers = new List<MqttLite>();
        public readonly bool Restricted;
        public readonly string Custom;
        readonly string root;
        public readonly Dictionary<string, ServerInfo> Servers = new Dictionary<string, ServerInfo>();
        public readonly Queue<JoinIntent> JoinRequests = new Queue<JoinIntent>();
        /// <summary>Relayed packet arrived: (broker index, payload).</summary>
        public Action<int, byte[]> OnRelay;
        string hostedId;
        ServerInfo hostedInfo;
        int[] seenConnects;

        /// <param name="custom">Optional broker URL (e.g. wss://host/mqtt). When set, only that broker is used.</param>
        public Lobby(int protocol, bool restricted, string custom, Action<string> log)
        {
            root = "solareclipse/goresandbox/p" + protocol;
            Restricted = restricted;
            Custom = custom ?? "";
            var customEp = BrokerEndpoint.Parse(Custom);
            if (customEp != null) Brokers.Add(new MqttLite("custom", new[] { customEp }) { Log = log });
            else foreach (var b in BrokerCatalog.All) Brokers.Add(new MqttLite(b.Name, restricted ? b.Restricted : b.Normal) { Log = log });
            seenConnects = new int[Brokers.Count];
        }

        public string Root => root;
        public int BrokerCount => Brokers.Count;
        public bool Online { get { foreach (var b in Brokers) if (b.Connected) return true; return false; } }
        public bool IsConnected(int i) => i >= 0 && i < Brokers.Count && Brokers[i].Connected;

        public string Status
        {
            get
            {
                int on = 0;
                foreach (var b in Brokers) if (b.Connected) on++;
                if (on > 0) return "Online (" + on + "/" + Brokers.Count + " relays" + (Restricted ? ", school mode" : "") + ")";
                return Brokers.Count == 1 ? Brokers[0].Status : "Connecting to " + Brokers.Count + " relays...";
            }
        }

        public long BytesIn { get { long n = 0; foreach (var b in Brokers) n += b.BytesIn; return n; } }
        public long BytesOut { get { long n = 0; foreach (var b in Brokers) n += b.BytesOut; return n; } }

        void All(Action<MqttLite> a) { foreach (var b in Brokers) a(b); }

        public void BrowseServers() => All(b => b.Subscribe(root + "/servers/+"));

        public void Host(ServerInfo info)
        {
            if (hostedId != info.Id) { hostedId = info.Id; All(b => b.Subscribe(root + "/join/" + info.Id)); }
            hostedInfo = info;
            info.UnixTime = ServerInfo.Now;
            var card = info.Serialize();
            All(b => b.Publish(root + "/servers/" + info.Id, card, true));
        }

        public void StopHosting()
        {
            if (hostedId == null) return;
            var id = hostedId;
            All(b => { b.Publish(root + "/servers/" + id, new byte[0], true); b.Unsubscribe(root + "/join/" + id); }); // clears the retained card
            hostedId = null;
            hostedInfo = null;
        }

        public void RequestJoin(string serverId, JoinIntent intent)
        {
            var data = intent.Serialize();
            All(b => b.Publish(root + "/join/" + serverId, data, false));
        }

        // ------------------------------------------------------------------ relay

        public string RelayTopic(string peer) => root + "/r/" + peer;
        public void SubscribeRelay(string id) => All(b => b.Subscribe(RelayTopic(id)));
        public void UnsubscribeRelay(string id) => All(b => b.Unsubscribe(RelayTopic(id)));

        public void PublishRelay(int broker, string peer, byte[] payload)
        {
            if (broker >= 0 && broker < Brokers.Count) Brokers[broker].Publish(RelayTopic(peer), payload, false);
        }

        /// <summary>Processes broker messages; call every frame on the main thread.</summary>
        public void Poll(double now)
        {
            string serversPrefix = root + "/servers/";
            string relayPrefix = root + "/r/";
            for (int i = 0; i < Brokers.Count; i++)
            {
                var b = Brokers[i];
                // Re-post our server card right after a (re)connect instead of waiting for the next refresh.
                if (b.ConnectCount != seenConnects[i])
                {
                    seenConnects[i] = b.ConnectCount;
                    if (hostedInfo != null) { hostedInfo.UnixTime = ServerInfo.Now; b.Publish(serversPrefix + hostedInfo.Id, hostedInfo.Serialize(), true); }
                }
                while (b.Inbox.TryDequeue(out var m))
                {
                    if (m.topic.StartsWith(relayPrefix)) { OnRelay?.Invoke(i, m.payload); continue; }
                    if (m.topic.StartsWith(serversPrefix))
                    {
                        var id = m.topic.Substring(serversPrefix.Length);
                        if (m.payload.Length == 0) { Servers.Remove(id); continue; }
                        var info = ServerInfo.Deserialize(m.payload, 0, m.payload.Length);
                        if (info == null || info.Id != id) continue;
                        if (ServerInfo.Now - info.UnixTime > 60) { Servers.Remove(id); continue; } // stale card from a crashed host
                        info.FoundOnline = true;
                        info.LastSeen = now;
                        if (Servers.TryGetValue(id, out var old)) info.BrokerMask = old.BrokerMask;
                        info.BrokerMask |= 1 << i;
                        Servers[id] = info;
                    }
                    else if (hostedId != null && m.topic == root + "/join/" + hostedId)
                    {
                        var j = JoinIntent.Deserialize(m.payload);
                        if (j != null) JoinRequests.Enqueue(j);
                    }
                }
            }
            // Drop cards that stopped refreshing.
            List<string> stale = null;
            foreach (var kv in Servers) if (ServerInfo.Now - kv.Value.UnixTime > 60) (stale ??= new List<string>()).Add(kv.Key);
            if (stale != null) foreach (var k in stale) Servers.Remove(k);
        }

        public void Dispose()
        {
            StopHosting();
            foreach (var b in Brokers) b.Dispose(); // each flushes its queue (the cleared card) before closing
        }
    }
}
