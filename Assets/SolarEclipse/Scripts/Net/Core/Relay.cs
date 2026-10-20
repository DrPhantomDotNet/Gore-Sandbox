using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace SolarEclipse.Net
{
    /// <summary>
    /// Carries game packets through the public MQTT brokers instead of UDP, for networks that block direct
    /// connections or where opening a port needs an admin password (school/work). Everything is outbound
    /// (secure WebSocket on 443 where available), so no firewall permission is ever needed.
    ///
    /// Each peer is given a fake address 240.&lt;broker&gt;.&lt;slot&gt;:1 (240/8 is reserved and never routed), so the
    /// reliable connection layer and all game code work unchanged. Packets for the same peer are batched into
    /// one broker message at most ~30 times per second.
    /// </summary>
    public sealed class Relay : IDisposable
    {
        public readonly string Id;
        readonly Lobby lobby;
        readonly Dictionary<string, IPEndPoint> byPeer = new Dictionary<string, IPEndPoint>();
        readonly Dictionary<IPEndPoint, (int broker, string peer)> byEp = new Dictionary<IPEndPoint, (int, string)>();
        readonly Dictionary<IPEndPoint, Batch> batches = new Dictionary<IPEndPoint, Batch>();
        public readonly Queue<Datagram> Inbox = new Queue<Datagram>();
        public long BytesIn, BytesOut, PacketsIn, PacketsOut;
        int nextSlot = 1;
        const byte Version = 1;
        const int MaxBatch = 24 * 1024;
        public double MinInterval = 1.0 / 30.0;

        sealed class Batch { public readonly MemoryStream Data = new MemoryStream(); public double LastFlush = -1; }

        public Relay(Lobby lobby, string id)
        {
            this.lobby = lobby;
            Id = id;
            lobby.OnRelay = Deliver;
            lobby.SubscribeRelay(id);
        }

        public static bool IsRelay(IPEndPoint ep)
        {
            if (ep == null || ep.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            return ep.Address.GetAddressBytes()[0] == 240;
        }

        /// <summary>A random relay id for a joiner (hosts use their server code).</summary>
        public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 16);

        public IPEndPoint Endpoint(int broker, string peer)
        {
            string key = broker + "|" + peer;
            if (byPeer.TryGetValue(key, out var ep)) return ep;
            int slot = nextSlot++ & 0xFFFF;
            ep = new IPEndPoint(new IPAddress(new byte[] { 240, (byte)broker, (byte)(slot >> 8), (byte)slot }), 1);
            byPeer[key] = ep;
            byEp[ep] = (broker, peer);
            return ep;
        }

        /// <summary>One fake address per broker for <paramref name="peer"/> (only brokers in <paramref name="mask"/>, if given).</summary>
        public List<IPEndPoint> EndpointsFor(string peer, int mask = 0)
        {
            var list = new List<IPEndPoint>();
            for (int i = 0; i < lobby.BrokerCount; i++)
                if (mask == 0 || (mask & (1 << i)) != 0) list.Add(Endpoint(i, peer));
            if (list.Count == 0) for (int i = 0; i < lobby.BrokerCount; i++) list.Add(Endpoint(i, peer));
            return list;
        }

        public bool BrokerOnline(IPEndPoint ep) => byEp.TryGetValue(ep, out var t) && lobby.IsConnected(t.broker);

        public void Send(byte[] data, int length, IPEndPoint to)
        {
            if (!byEp.ContainsKey(to) || length <= 0 || length > 65535) return;
            if (!batches.TryGetValue(to, out var b)) batches[to] = b = new Batch();
            if (b.Data.Length + length + 2 > MaxBatch) Publish(to, b);
            b.Data.WriteByte((byte)(length >> 8));
            b.Data.WriteByte((byte)length);
            b.Data.Write(data, 0, length);
            PacketsOut++;
        }

        /// <summary>Sends batched packets (rate-limited per peer unless <paramref name="force"/>). Call once per frame.</summary>
        public void Flush(double now, bool force = false)
        {
            foreach (var kv in batches)
            {
                var b = kv.Value;
                if (b.Data.Length == 0) continue;
                if (!force && now - b.LastFlush < MinInterval) continue;
                b.LastFlush = now;
                Publish(kv.Key, b);
            }
        }

        void Publish(IPEndPoint to, Batch b)
        {
            if (b.Data.Length == 0 || !byEp.TryGetValue(to, out var dest)) { b.Data.SetLength(0); return; }
            var idBytes = Encoding.ASCII.GetBytes(Id);
            var payload = new byte[2 + idBytes.Length + b.Data.Length];
            payload[0] = Version;
            payload[1] = (byte)idBytes.Length;
            Buffer.BlockCopy(idBytes, 0, payload, 2, idBytes.Length);
            Buffer.BlockCopy(b.Data.GetBuffer(), 0, payload, 2 + idBytes.Length, (int)b.Data.Length);
            b.Data.SetLength(0);
            BytesOut += payload.Length;
            lobby.PublishRelay(dest.broker, dest.peer, payload);
        }

        void Deliver(int broker, byte[] p)
        {
            if (p.Length < 2 || p[0] != Version) return;
            int idLen = p[1];
            if (idLen == 0 || 2 + idLen > p.Length) return;
            string sender = Encoding.ASCII.GetString(p, 2, idLen);
            if (sender == Id) return; // our own echo
            var from = Endpoint(broker, sender);
            BytesIn += p.Length;
            int pos = 2 + idLen;
            while (pos + 2 <= p.Length)
            {
                int len = (p[pos] << 8) | p[pos + 1];
                pos += 2;
                if (len <= 0 || pos + len > p.Length) break;
                var copy = new byte[len];
                Buffer.BlockCopy(p, pos, copy, 0, len);
                pos += len;
                PacketsIn++;
                Inbox.Enqueue(new Datagram { Data = copy, Length = len, From = from });
            }
        }

        public void Dispose()
        {
            if (lobby.OnRelay == (Action<int, byte[]>)Deliver) lobby.OnRelay = null;
            lobby.UnsubscribeRelay(Id);
        }
    }
}
