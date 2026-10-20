using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SolarEclipse.Net
{
    /// <summary>
    /// Tiny MQTT 3.1.1 client (QoS 0) for one broker. Used for the free online server list and, as a relay, for
    /// game traffic when a direct connection isn't possible (school/work networks). Only makes outbound
    /// connections. Tries the broker's endpoints in order (e.g. TCP 1883, then secure WebSocket on 443) and
    /// reconnects automatically. A reader thread receives; the connection thread sends.
    /// </summary>
    public sealed class MqttLite : IDisposable
    {
        public readonly ConcurrentQueue<(string topic, byte[] payload)> Inbox = new ConcurrentQueue<(string, byte[])>();
        public volatile bool Connected;
        public volatile string Status = "Not connected";
        public readonly string Name;
        public readonly BrokerEndpoint[] Endpoints;
        public BrokerEndpoint Current { get; private set; }
        /// <summary>Increments on every successful (re)connect, so owners can re-publish retained state.</summary>
        public int ConnectCount => connectCount;
        public long BytesIn, BytesOut;
        public Action<string> Log;

        const int MaxQueued = 1024;
        readonly ConcurrentQueue<byte[]> outbox = new ConcurrentQueue<byte[]>();
        readonly ConcurrentQueue<byte[]> control = new ConcurrentQueue<byte[]>();
        readonly List<string> allSubs = new List<string>();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Thread thread;
        readonly string clientId;
        volatile bool running = true;
        volatile bool readerFailed;
        volatile string readerError;
        long lastRxTicks;
        int queued, connectCount;
        int packetId = 1;
        TcpClient activeTcp;

        public MqttLite(string name, BrokerEndpoint[] endpoints)
        {
            Name = name;
            Endpoints = endpoints;
            clientId = "gsb-" + Guid.NewGuid().ToString("N").Substring(0, 14);
            thread = new Thread(Run) { IsBackground = true, Name = "GSB-MQTT-" + name };
            thread.Start();
        }

        public void Subscribe(string filter)
        {
            lock (allSubs) { if (allSubs.Contains(filter)) return; allSubs.Add(filter); }
            control.Enqueue(SubscribePacket(filter));
            wake.Set();
        }

        public void Unsubscribe(string filter)
        {
            lock (allSubs) { if (!allSubs.Remove(filter)) return; }
            var w = new MemoryStream();
            int id = NextId();
            w.WriteByte((byte)(id >> 8)); w.WriteByte((byte)id);
            WriteString(w, filter);
            control.Enqueue(Packet(0xA2, w.ToArray()));
            wake.Set();
        }

        public void Publish(string topic, byte[] payload, bool retain)
        {
            if (Interlocked.Increment(ref queued) > MaxQueued)
            {
                // Falling behind (or offline): drop the oldest packet. The game layer resends what matters.
                if (outbox.TryDequeue(out _)) Interlocked.Decrement(ref queued);
            }
            var w = new MemoryStream(topic.Length + 4 + (payload?.Length ?? 0));
            WriteString(w, topic);
            if (payload != null) w.Write(payload, 0, payload.Length);
            outbox.Enqueue(Packet((byte)(0x30 | (retain ? 1 : 0)), w.ToArray()));
            wake.Set();
        }

        void Run()
        {
            int opt = 0, fails = 0;
            while (running)
            {
                var ep = Endpoints[opt % Endpoints.Length];
                Current = ep;
                TcpClient tcp = null;
                Stream s = null;
                bool wasConnected = false;
                try
                {
                    Status = "Connecting (" + ep.Scheme + ")...";
                    (tcp, s) = WebTransport.Open(ep, 7000, Log);
                    activeTcp = tcp;
                    var connect = ConnectPacket(ep);
                    s.Write(connect, 0, connect.Length);
                    s.Flush();
                    var (t0, body0) = ReadPacket(s);
                    if (t0 != 0x20 || body0.Length < 2 || body0[1] != 0) throw new IOException("broker refused connection (code " + (body0.Length > 1 ? body0[1] : -1) + ")");
                    tcp.ReceiveTimeout = 0; // the reader blocks; liveness comes from pings
                    try { s.ReadTimeout = Timeout.Infinite; } catch { }
                    Connected = true;
                    wasConnected = true;
                    fails = 0;
                    Interlocked.Increment(ref connectCount);
                    Status = "Online (" + ep.Scheme + ")";
                    Log?.Invoke("[Net] relay " + Name + " connected via " + ep);
                    while (control.TryDequeue(out _)) { }
                    lock (allSubs) foreach (var f in allSubs) Send(s, SubscribePacket(f));
                    Interlocked.Exchange(ref lastRxTicks, DateTime.UtcNow.Ticks);
                    readerFailed = false;
                    var reader = new Thread(() => ReadLoop(s)) { IsBackground = true, Name = "GSB-MQTT-R-" + Name };
                    reader.Start();
                    var lastPing = DateTime.UtcNow;
                    while (running && !readerFailed)
                    {
                        wake.WaitOne(20);
                        while (control.TryDequeue(out var c)) Send(s, c);
                        while (outbox.TryDequeue(out var pkt)) { Interlocked.Decrement(ref queued); Send(s, pkt); }
                        var now = DateTime.UtcNow;
                        if ((now - lastPing).TotalSeconds > 20) { Send(s, new byte[] { 0xC0, 0x00 }); lastPing = now; }
                        if ((now.Ticks - Interlocked.Read(ref lastRxTicks)) / TimeSpan.TicksPerSecond > 50) throw new IOException("no answer from broker");
                    }
                    if (readerFailed) throw new IOException(readerError ?? "connection lost");
                    while (outbox.TryDequeue(out var last)) { Interlocked.Decrement(ref queued); Send(s, last); } // flush before closing
                    try { Send(s, new byte[] { 0xE0, 0x00 }); } catch { }
                }
                catch (Exception e)
                {
                    Status = (wasConnected ? "Reconnecting" : "Can't reach") + " (" + Short(e.Message) + ")";
                    Log?.Invoke("[Net] relay " + Name + " " + ep + ": " + e.Message);
                }
                finally
                {
                    Connected = false;
                    activeTcp = null;
                    try { s?.Dispose(); } catch { }
                    try { tcp?.Close(); } catch { }
                }
                if (!running) break;
                if (!wasConnected) { opt++; fails++; }
                // Back off: quick retry after a drop, slower after repeated failures (offline / blocked).
                int waitMs = wasConnected ? 1000 : Math.Min(2000 + 1500 * (fails / Math.Max(1, Endpoints.Length)), 20000);
                for (int t = 0; t < waitMs && running; t += 100) Thread.Sleep(100);
            }
            Connected = false;
        }

        static string Short(string m) => m == null ? "" : m.Length > 60 ? m.Substring(0, 60) + "..." : m;

        void Send(Stream s, byte[] pkt)
        {
            s.Write(pkt, 0, pkt.Length);
            s.Flush();
            Interlocked.Add(ref BytesOut, pkt.Length);
        }

        void ReadLoop(Stream s)
        {
            try
            {
                while (running)
                {
                    var (type, body) = ReadPacket(s);
                    Interlocked.Exchange(ref lastRxTicks, DateTime.UtcNow.Ticks);
                    Interlocked.Add(ref BytesIn, body.Length + 2);
                    if ((type & 0xF0) == 0x30) HandlePublish(type, body);
                }
            }
            catch (Exception e) { readerError = e.Message; }
            readerFailed = true;
            wake.Set();
        }

        void HandlePublish(byte header, byte[] body)
        {
            if (body.Length < 2) return;
            int tlen = (body[0] << 8) | body[1];
            if (2 + tlen > body.Length) return;
            string topic = Encoding.UTF8.GetString(body, 2, tlen);
            int pos = 2 + tlen;
            int qos = (header >> 1) & 3;
            if (qos > 0) pos += 2;
            var payload = new byte[Math.Max(0, body.Length - pos)];
            if (payload.Length > 0) Buffer.BlockCopy(body, pos, payload, 0, payload.Length);
            Inbox.Enqueue((topic, payload));
        }

        byte[] ConnectPacket(BrokerEndpoint ep)
        {
            var cw = new MemoryStream();
            WriteString(cw, "MQTT");
            cw.WriteByte(4); // protocol level 3.1.1
            byte flags = 0x02; // clean session
            if (!string.IsNullOrEmpty(ep.User)) flags |= 0x80;
            if (!string.IsNullOrEmpty(ep.User) && ep.Pass != null) flags |= 0x40;
            cw.WriteByte(flags);
            cw.WriteByte(0); cw.WriteByte(60); // keep alive 60 s
            WriteString(cw, clientId);
            if ((flags & 0x80) != 0) WriteString(cw, ep.User);
            if ((flags & 0x40) != 0) WriteString(cw, ep.Pass);
            return Packet(0x10, cw.ToArray());
        }

        int NextId()
        {
            int id = Interlocked.Increment(ref packetId) & 0xFFFF;
            return id == 0 ? 1 : id;
        }

        byte[] SubscribePacket(string filter)
        {
            var w = new MemoryStream();
            int id = NextId();
            w.WriteByte((byte)(id >> 8)); w.WriteByte((byte)id);
            WriteString(w, filter);
            w.WriteByte(0); // QoS 0
            return Packet(0x82, w.ToArray());
        }

        static byte[] Packet(byte header, byte[] body)
        {
            var w = new MemoryStream(body.Length + 5);
            w.WriteByte(header);
            int len = body.Length;
            do { byte b = (byte)(len % 128); len /= 128; if (len > 0) b |= 128; w.WriteByte(b); } while (len > 0);
            w.Write(body, 0, body.Length);
            return w.ToArray();
        }

        static void WriteString(Stream w, string s)
        {
            var b = Encoding.UTF8.GetBytes(s);
            w.WriteByte((byte)(b.Length >> 8)); w.WriteByte((byte)b.Length);
            w.Write(b, 0, b.Length);
        }

        static (byte type, byte[] body) ReadPacket(Stream s)
        {
            int header = ReadByte(s);
            int mult = 1, len = 0;
            for (int i = 0; i < 4; i++)
            {
                int b = ReadByte(s);
                len += (b & 127) * mult;
                mult *= 128;
                if ((b & 128) == 0) break;
            }
            if (len > 4 << 20) throw new IOException("packet too large");
            var body = new byte[len];
            int got = 0;
            while (got < len)
            {
                int n = s.Read(body, got, len - got);
                if (n <= 0) throw new IOException("connection closed");
                got += n;
            }
            return ((byte)header, body);
        }

        static int ReadByte(Stream s)
        {
            int b = s.ReadByte();
            if (b < 0) throw new IOException("connection closed");
            return b;
        }

        public void Dispose()
        {
            if (!running) return;
            running = false;
            wake.Set();
            // Let the connection thread flush what's queued (e.g. clearing our server card), then close.
            var t = activeTcp;
            System.Threading.Tasks.Task.Run(() => { Thread.Sleep(600); try { t?.Close(); } catch { } });
        }
    }
}
