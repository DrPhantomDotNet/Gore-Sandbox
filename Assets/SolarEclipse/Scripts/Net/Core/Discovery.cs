using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace SolarEclipse.Net
{
    /// <summary>What a host advertises in the server list (LAN broadcast and online lobby).</summary>
    public sealed class ServerInfo
    {
        public string Id = "", Name = "", HostName = "", MapId = "", MapName = "", GameVersion = "";
        public int Players, MaxPlayers, Protocol;
        public bool Password;
        public string LanEndpoint = "", PublicEndpoint = "";
        public long UnixTime;
        public bool Relay;            // reachable through the relay (join by code / school network mode)
        public bool DirectOk = true;  // has an open UDP port (false = host is in school network mode)
        // Filled in by the browser, not sent:
        public bool FoundOnLan, FoundOnline;
        public int BrokerMask;        // which relays the card was seen on
        public double LastSeen;

        const uint Magic = 0x47534232; // "GSB2"

        public byte[] Serialize()
        {
            var w = new NetWriter(256);
            w.UInt(Magic); w.Int(Protocol);
            w.String(Id); w.String(Name); w.String(HostName); w.String(MapId); w.String(MapName); w.String(GameVersion);
            w.Byte((byte)Players); w.Byte((byte)MaxPlayers); w.Bool(Password);
            w.String(LanEndpoint); w.String(PublicEndpoint); w.Long(UnixTime);
            w.Bool(Relay); w.Bool(DirectOk);
            return w.ToArray();
        }

        public static ServerInfo Deserialize(byte[] d, int offset, int len)
        {
            var r = new NetReader(d, offset, len);
            if (r.UInt() != Magic) return null;
            var s = new ServerInfo
            {
                Protocol = r.Int(), Id = r.String(), Name = r.String(), HostName = r.String(), MapId = r.String(), MapName = r.String(), GameVersion = r.String(),
                Players = r.Byte(), MaxPlayers = r.Byte(), Password = r.Bool(), LanEndpoint = r.String(), PublicEndpoint = r.String(), UnixTime = r.Long(),
            };
            s.Relay = r.Bool(); s.DirectOk = r.Bool();
            return r.Overflow || string.IsNullOrEmpty(s.Id) ? null : s;
        }

        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static class AddressUtil
    {
        public static IPEndPoint Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            int port = 27015;
            string host = s;
            int colon = s.LastIndexOf(':');
            if (colon > 0 && s.IndexOf(':') == colon && int.TryParse(s.Substring(colon + 1), out var p)) { host = s.Substring(0, colon); port = p; }
            if (IPAddress.TryParse(host, out var ip)) return new IPEndPoint(ip, port);
            try
            {
                foreach (var a in Dns.GetHostAddresses(host))
                    if (a.AddressFamily == AddressFamily.InterNetwork) return new IPEndPoint(a, port);
            }
            catch { }
            return null;
        }

        /// <summary>IPv4 addresses of active, non-loopback interfaces with their broadcast addresses.</summary>
        public static List<(IPAddress ip, IPAddress broadcast)> LanAddresses()
        {
            var list = new List<(IPAddress, IPAddress)>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var ip = ua.Address.GetAddressBytes();
                        IPAddress bc = IPAddress.Broadcast;
                        try
                        {
                            var mask = ua.IPv4Mask?.GetAddressBytes();
                            if (mask != null && mask.Length == 4)
                            {
                                var b = new byte[4];
                                for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | ~mask[i]);
                                bc = new IPAddress(b);
                            }
                        }
                        catch { }
                        list.Add((ua.Address, bc));
                    }
                }
            }
            catch { }
            return list;
        }

        public static IPAddress BestLanAddress()
        {
            foreach (var (ip, _) in LanAddresses())
            {
                var b = ip.GetAddressBytes();
                if (b[0] == 169 && b[1] == 254) continue; // link-local
                return ip;
            }
            return IPAddress.Loopback;
        }
    }

    /// <summary>LAN server discovery: hosts broadcast their <see cref="ServerInfo"/>, browsers listen.</summary>
    public sealed class LanDiscovery : IDisposable
    {
        public const int Port = 27016;
        public readonly ConcurrentQueue<(ServerInfo info, IPEndPoint from)> Found = new ConcurrentQueue<(ServerInfo, IPEndPoint)>();
        Socket listen, sender;
        Thread thread;
        volatile bool running = true;

        public static LanDiscovery Listener()
        {
            var d = new LanDiscovery();
            try
            {
                d.listen = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                d.listen.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                d.listen.ExclusiveAddressUse = false;
                d.listen.Bind(new IPEndPoint(IPAddress.Any, Port));
                d.thread = new Thread(d.Loop) { IsBackground = true, Name = "GSB-LAN" };
                d.thread.Start();
            }
            catch { d.listen = null; }
            return d;
        }

        public static LanDiscovery Broadcaster()
        {
            var d = new LanDiscovery();
            try
            {
                d.sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { EnableBroadcast = true };
                d.sender.Bind(new IPEndPoint(IPAddress.Any, 0));
            }
            catch { d.sender = null; }
            return d;
        }

        public bool Listening => listen != null;

        void Loop()
        {
            var buf = new byte[4096];
            while (running)
            {
                try
                {
                    EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    int n = listen.ReceiveFrom(buf, ref ep);
                    var info = ServerInfo.Deserialize(buf, 0, n);
                    if (info != null) Found.Enqueue((info, (IPEndPoint)ep));
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (!running) break; }
                catch { }
            }
        }

        public void Broadcast(ServerInfo info)
        {
            if (sender == null) return;
            var data = info.Serialize();
            var targets = new HashSet<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
            foreach (var (_, bc) in AddressUtil.LanAddresses()) targets.Add(bc);
            foreach (var t in targets)
            {
                try { sender.SendTo(data, new IPEndPoint(t, Port)); } catch { }
            }
        }

        public void Dispose()
        {
            running = false;
            try { listen?.Close(); } catch { }
            try { sender?.Close(); } catch { }
        }
    }
}
