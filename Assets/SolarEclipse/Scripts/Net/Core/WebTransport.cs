using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace SolarEclipse.Net
{
    /// <summary>Where to reach an MQTT broker: plain TCP, TLS, WebSocket (ws) or secure WebSocket (wss).</summary>
    public sealed class BrokerEndpoint
    {
        public string Host = "";
        public int Port;
        public bool Tls, WebSocket;
        public string Path = "/mqtt";
        public string User, Pass;

        public static BrokerEndpoint Tcp(string host, int port = 1883) => new BrokerEndpoint { Host = host, Port = port };
        public static BrokerEndpoint Wss(string host, int port, string path = "/mqtt") => new BrokerEndpoint { Host = host, Port = port, Tls = true, WebSocket = true, Path = path };
        public static BrokerEndpoint Ws(string host, int port, string path = "/mqtt") => new BrokerEndpoint { Host = host, Port = port, WebSocket = true, Path = path };

        public string Scheme => WebSocket ? (Tls ? "wss" : "ws") : (Tls ? "mqtts" : "mqtt");
        public override string ToString() => Scheme + "://" + Host + ":" + Port + (WebSocket ? Path : "");

        /// <summary>Parses "wss://user:pass@host:port/path", "ws://...", "mqtt://...", "mqtts://..." or "host:port".</summary>
        public static BrokerEndpoint Parse(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim();
            if (!url.Contains("://")) url = "mqtt://" + url;
            try
            {
                var u = new Uri(url);
                var ep = new BrokerEndpoint { Host = u.Host };
                switch (u.Scheme.ToLowerInvariant())
                {
                    case "wss": ep.WebSocket = true; ep.Tls = true; ep.Port = u.IsDefaultPort || u.Port <= 0 ? 443 : u.Port; break;
                    case "ws": ep.WebSocket = true; ep.Port = u.IsDefaultPort || u.Port <= 0 ? 80 : u.Port; break;
                    case "mqtts": case "ssl": case "tls": ep.Tls = true; ep.Port = u.Port > 0 ? u.Port : 8883; break;
                    case "mqtt": case "tcp": ep.Port = u.Port > 0 ? u.Port : 1883; break;
                    default: return null;
                }
                if (ep.WebSocket) ep.Path = string.IsNullOrEmpty(u.AbsolutePath) ? "/" : u.AbsolutePath;
                if (!string.IsNullOrEmpty(u.UserInfo))
                {
                    var parts = u.UserInfo.Split(new[] { ':' }, 2);
                    ep.User = Uri.UnescapeDataString(parts[0]);
                    ep.Pass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
                }
                return string.IsNullOrEmpty(ep.Host) ? null : ep;
            }
            catch { return null; }
        }
    }

    /// <summary>
    /// Opens outbound connections only (never listens), so it needs no firewall/admin permission. Supports
    /// TLS, WebSockets and HTTP proxies (CONNECT), which is how school/work networks usually let traffic out.
    /// </summary>
    public static class WebTransport
    {
        /// <summary>Optional "host:port" of an HTTP proxy the user typed in (overrides auto-detection).</summary>
        public static volatile string ManualProxy = "";
        /// <summary>For tests only: accept self-signed certificates.</summary>
        public static bool AcceptAnyCertificate;

        public static (TcpClient tcp, Stream stream) Open(BrokerEndpoint ep, int timeoutMs, Action<string> log = null)
        {
            var proxy = ProxyFor(ep);
            if (proxy != null)
            {
                try { return OpenVia(ep, proxy, timeoutMs); }
                catch (Exception e) { log?.Invoke("[Net] via proxy " + proxy.Host + ":" + proxy.Port + " failed (" + e.Message + "), trying direct"); }
            }
            return OpenVia(ep, null, timeoutMs);
        }

        static (TcpClient, Stream) OpenVia(BrokerEndpoint ep, DnsEndPoint proxy, int timeoutMs)
        {
            var tcp = new TcpClient();
            try
            {
                string host = proxy != null ? proxy.Host : ep.Host;
                int port = proxy != null ? proxy.Port : ep.Port;
                var ar = tcp.BeginConnect(host, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) throw new IOException("timeout connecting to " + host + ":" + port);
                tcp.EndConnect(ar);
                tcp.NoDelay = true;
                tcp.ReceiveTimeout = timeoutMs;
                tcp.SendTimeout = timeoutMs;
                Stream s = tcp.GetStream();
                if (proxy != null)
                {
                    string target = ep.Host + ":" + ep.Port;
                    WriteAscii(s, "CONNECT " + target + " HTTP/1.1\r\nHost: " + target + "\r\nUser-Agent: GoreSandbox\r\nProxy-Connection: keep-alive\r\n\r\n");
                    string head = ReadHttpHead(s);
                    if (!StatusIs(head, 200)) throw new IOException("proxy refused: " + FirstLine(head));
                }
                if (ep.Tls)
                {
                    var ssl = AcceptAnyCertificate ? new SslStream(s, false, (a, b, c, d) => true) : new SslStream(s, false);
                    ssl.AuthenticateAsClient(ep.Host);
                    s = ssl;
                }
                if (ep.WebSocket) s = WebSocketStream.Handshake(s, ep);
                return (tcp, s);
            }
            catch
            {
                try { tcp.Close(); } catch { }
                throw;
            }
        }

        /// <summary>Manual proxy, then HTTPS_PROXY/HTTP_PROXY, then the OS proxy settings. Null = connect directly.</summary>
        static DnsEndPoint ProxyFor(BrokerEndpoint ep)
        {
            var manual = ParseHostPort(ManualProxy);
            if (manual != null) return manual;
            if (!ep.WebSocket && !ep.Tls) return null; // plain MQTT (1883) is never allowed through web proxies
            try
            {
                foreach (var name in new[] { "HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy", "ALL_PROXY", "all_proxy" })
                {
                    var v = Environment.GetEnvironmentVariable(name);
                    if (string.IsNullOrWhiteSpace(v) || IsNoProxy(ep.Host)) continue;
                    if (Uri.TryCreate(v.Contains("://") ? v : "http://" + v, UriKind.Absolute, out var u) && u.Scheme.StartsWith("http")) return new DnsEndPoint(u.Host, u.Port > 0 ? u.Port : 8080);
                }
            }
            catch { }
            try
            {
                var target = new Uri((ep.Tls ? "https://" : "http://") + ep.Host + ":" + ep.Port + "/");
                var sys = WebRequest.GetSystemWebProxy();
                if (sys != null && !sys.IsBypassed(target))
                {
                    var p = sys.GetProxy(target);
                    if (p != null && !string.Equals(p.Host, target.Host, StringComparison.OrdinalIgnoreCase)) return new DnsEndPoint(p.Host, p.Port);
                }
            }
            catch { }
            return null;
        }

        static bool IsNoProxy(string host)
        {
            var np = Environment.GetEnvironmentVariable("NO_PROXY") ?? Environment.GetEnvironmentVariable("no_proxy");
            if (string.IsNullOrEmpty(np)) return false;
            foreach (var raw in np.Split(','))
            {
                var e = raw.Trim().TrimStart('*');
                if (e.Length > 0 && (host == e.TrimStart('.') || host.EndsWith(e.StartsWith(".") ? e : "." + e))) return true;
            }
            return false;
        }

        public static DnsEndPoint ParseHostPort(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            if (s.Contains("://") && Uri.TryCreate(s, UriKind.Absolute, out var u)) return new DnsEndPoint(u.Host, u.Port > 0 ? u.Port : 8080);
            int c = s.LastIndexOf(':');
            if (c > 0 && int.TryParse(s.Substring(c + 1), out var port) && port > 0 && port < 65536) return new DnsEndPoint(s.Substring(0, c), port);
            return new DnsEndPoint(s, 8080);
        }

        internal static void WriteAscii(Stream s, string text)
        {
            var b = Encoding.ASCII.GetBytes(text);
            s.Write(b, 0, b.Length);
            s.Flush();
        }

        /// <summary>Reads an HTTP response head byte by byte (so nothing after it is consumed).</summary>
        internal static string ReadHttpHead(Stream s)
        {
            var sb = new StringBuilder();
            while (sb.Length < 16384)
            {
                int b = s.ReadByte();
                if (b < 0) throw new IOException("connection closed during HTTP handshake");
                sb.Append((char)b);
                int n = sb.Length;
                if (n >= 4 && sb[n - 4] == '\r' && sb[n - 3] == '\n' && sb[n - 2] == '\r' && sb[n - 1] == '\n') return sb.ToString();
            }
            throw new IOException("HTTP response too long");
        }

        internal static string FirstLine(string head) { int i = head.IndexOf('\r'); return i > 0 ? head.Substring(0, i) : head; }

        internal static bool StatusIs(string head, int code)
        {
            var parts = FirstLine(head).Split(' ');
            return parts.Length >= 2 && parts[0].StartsWith("HTTP/") && parts[1] == code.ToString();
        }
    }

    /// <summary>
    /// Minimal RFC 6455 WebSocket client stream (binary frames). Reads return the payload bytes of incoming data
    /// frames as a continuous stream; writes send one masked binary frame each. Answers pings automatically.
    /// </summary>
    public sealed class WebSocketStream : Stream
    {
        readonly Stream inner;
        readonly object writeLock = new object();
        readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();
        readonly byte[] mask = new byte[4];
        byte[] frame = new byte[0];
        int framePos, frameLen;
        bool closed;

        WebSocketStream(Stream inner) { this.inner = inner; }

        public static WebSocketStream Handshake(Stream s, BrokerEndpoint ep)
        {
            var keyBytes = new byte[16];
            using (var r = RandomNumberGenerator.Create()) r.GetBytes(keyBytes);
            string key = Convert.ToBase64String(keyBytes);
            bool defaultPort = ep.Port == (ep.Tls ? 443 : 80);
            string hostHeader = ep.Host + (defaultPort ? "" : ":" + ep.Port);
            string path = string.IsNullOrEmpty(ep.Path) ? "/" : ep.Path;
            WebTransport.WriteAscii(s,
                "GET " + path + " HTTP/1.1\r\n" +
                "Host: " + hostHeader + "\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Key: " + key + "\r\n" +
                "Sec-WebSocket-Version: 13\r\n" +
                "Sec-WebSocket-Protocol: mqtt\r\n" +
                "Origin: " + (ep.Tls ? "https://" : "http://") + hostHeader + "\r\n" +
                "User-Agent: GoreSandbox\r\n\r\n");
            string head = WebTransport.ReadHttpHead(s);
            if (!WebTransport.StatusIs(head, 101)) throw new IOException("websocket refused: " + WebTransport.FirstLine(head));
            string expect;
            using (var sha = SHA1.Create())
                expect = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            bool ok = false;
            foreach (var line in head.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                int c = line.IndexOf(':');
                if (c > 0 && line.Substring(0, c).Trim().Equals("Sec-WebSocket-Accept", StringComparison.OrdinalIgnoreCase) && line.Substring(c + 1).Trim() == expect) ok = true;
            }
            if (!ok) throw new IOException("bad websocket handshake");
            return new WebSocketStream(s);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return 0;
            while (framePos >= frameLen)
            {
                if (closed) return 0;
                if (!ReadFrame()) return 0;
            }
            int n = Math.Min(count, frameLen - framePos);
            Buffer.BlockCopy(frame, framePos, buffer, offset, n);
            framePos += n;
            return n;
        }

        /// <summary>Reads one frame. Data frames fill the read buffer; control frames are handled here.</summary>
        bool ReadFrame()
        {
            int b0 = ReadByteOrThrow(), b1 = ReadByteOrThrow();
            int opcode = b0 & 0x0F;
            bool masked = (b1 & 0x80) != 0;
            long len = b1 & 0x7F;
            if (len == 126) len = (ReadByteOrThrow() << 8) | ReadByteOrThrow();
            else if (len == 127) { len = 0; for (int i = 0; i < 8; i++) len = (len << 8) | (uint)ReadByteOrThrow(); }
            if (len < 0 || len > 16 << 20) throw new IOException("websocket frame too large");
            var m = new byte[4];
            if (masked) ReadExact(m, 0, 4);
            var payload = new byte[len];
            ReadExact(payload, 0, (int)len);
            if (masked) for (int i = 0; i < payload.Length; i++) payload[i] ^= m[i & 3];
            switch (opcode)
            {
                case 0: case 1: case 2:
                    frame = payload; framePos = 0; frameLen = payload.Length;
                    return true;
                case 8:
                    closed = true;
                    try { SendFrame(0x8, new byte[0], 0, 0); } catch { }
                    return false;
                case 9:
                    SendFrame(0xA, payload, 0, payload.Length);
                    return true;
                default:
                    return true; // pong / unknown: ignore
            }
        }

        int ReadByteOrThrow()
        {
            int b = inner.ReadByte();
            if (b < 0) throw new IOException("connection closed");
            return b;
        }

        void ReadExact(byte[] buf, int off, int count)
        {
            while (count > 0)
            {
                int n = inner.Read(buf, off, count);
                if (n <= 0) throw new IOException("connection closed");
                off += n; count -= n;
            }
        }

        public override void Write(byte[] buffer, int offset, int count) => SendFrame(0x2, buffer, offset, count);

        void SendFrame(int opcode, byte[] data, int offset, int count)
        {
            lock (writeLock)
            {
                int headLen = 2 + (count >= 126 ? (count > 65535 ? 8 : 2) : 0) + 4;
                var f = new byte[headLen + count];
                f[0] = (byte)(0x80 | opcode);
                int p;
                if (count < 126) { f[1] = (byte)(0x80 | count); p = 2; }
                else if (count <= 65535) { f[1] = 0x80 | 126; f[2] = (byte)(count >> 8); f[3] = (byte)count; p = 4; }
                else { f[1] = 0x80 | 127; for (int i = 0; i < 8; i++) f[2 + i] = (byte)((long)count >> (56 - 8 * i)); p = 10; }
                rng.GetBytes(mask);
                Buffer.BlockCopy(mask, 0, f, p, 4); p += 4;
                for (int i = 0; i < count; i++) f[p + i] = (byte)(data[offset + i] ^ mask[i & 3]);
                inner.Write(f, 0, f.Length);
                inner.Flush();
            }
        }

        public override void Flush() { }
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override bool CanTimeout => inner.CanTimeout;
        public override int ReadTimeout { get => inner.ReadTimeout; set => inner.ReadTimeout = value; }
        public override int WriteTimeout { get => inner.WriteTimeout; set => inner.WriteTimeout = value; }
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!closed) { try { SendFrame(0x8, new byte[0], 0, 0); } catch { } }
                closed = true;
                try { inner.Dispose(); } catch { }
                rng.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
