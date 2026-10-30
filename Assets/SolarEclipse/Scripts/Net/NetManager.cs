using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using SolarEclipse.Net;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace SolarEclipse
{
    /// <summary>Message ids (one byte). Connectionless ones travel outside any connection.</summary>
    public enum Msg : byte
    {
        // connectionless
        Hello = 1, Punch = 2, Reject = 3, Bye = 4,
        // session
        Welcome = 10, PlayerInfo = 11, PlayerLeft = 12, Chat = 13, Permission = 14, Kicked = 15, Toast = 16, Rules = 17,
        ClientReady = 18, PlayerDied = 19, HitMarker = 20, Ping = 21, Pong = 22, PlayerList = 23, Disconnect = 24,
        // state
        PlayerState = 30, Snapshot = 31, Voice = 32,
        // world events (host → clients)
        SpawnDoll = 40, SpawnProp = 41, SpawnNextbot = 42, Despawn = 43, MapRemoved = 44, EntityScale = 45, EntityFreeze = 46,
        DollDamage = 50, DollDismember = 51, DollSay = 52, DollDeath = 53, DollRevive = 54, EntityDamage = 55, ExplosionFx = 56,
        FireFx = 57, HurtPlayer = 58, DollState = 59,
        // requests (client → host)
        ReqDamagePart = 70, ReqDamageEntity = 71, ReqImpulse = 72, ReqSpawn = 73, ReqDelete = 74, ReqGrab = 75, ReqGrabMove = 76,
        ReqGrabEnd = 77, ReqFreeze = 78, ReqScale = 79, ReqLaunch = 80, ReqDismember = 81, ReqExplosion = 82, ReqInteract = 83,
        ReqFire = 84, ReqAdmin = 85, ReqHurtPlayer = 86, ReqDollCommand = 87,
        // map editor: host -> joining client, custom map JSON (gzip) sent right before Welcome
        CustomMap = 121,
        // explosives (Net/NetManager.Explosives.cs): client -> host requests, host -> client effects
        ReqThrowable = 140, ThrowableFx = 141, ReqDetonate = 142, C4Detonated = 143,
        // player profiles + badges (Net/NetManager.Profile.cs)
        ProfileUpdate = 220, ProfileData = 221, BadgeGrant = 222, ReqBadgeGrant = 223,
    }

    public sealed class NetPlayer
    {
        public byte Id;
        public string Name = "", Guid = "", Address = "";
        public bool Creative, IsHost, Ready;
        public int PingMs;
        public NetConnection Conn;
        public RemotePlayer Avatar;
        public double JoinedAt;
        // Latest movement state
        public Vector3 Pos;
        public float Yaw, Pitch;
        public byte Flags, Weapon, Health = 100;
        // Host-side spawn gun grab driven by this player
        public Rigidbody GrabBody;
        public Vector3 GrabLocal, GrabTarget;
        public Quaternion GrabRot = Quaternion.identity;
        public bool GrabRotating, GrabIsDoll;
        public float LastGrabMsg;

        public const byte FCrouch = 1, FAim = 2, FNoclip = 4, FDead = 8, FSprint = 16, FGrounded = 32, FFiring = 64;
    }

    /// <summary>Host-chosen rules for a multiplayer session.</summary>
    public sealed class ServerSettings
    {
        public string Name = "Gore Sandbox Server";
        public int MaxPlayers = 8;
        public string Password = "";
        public bool Public = true;      // listed online (otherwise LAN + direct IP only)
        public bool PvP = true;
        public bool CreativeForAll;     // new players start with Creative
        public int Loadout = 1;         // 0 fists only, 1 fists + guns
    }

    /// <summary>
    /// Peer-to-peer multiplayer with a host-authoritative simulation. The host runs physics, AI, gore and damage;
    /// clients run their own player movement and send requests for everything that affects the world.
    /// No paid server: hosts are found on the LAN (broadcast), online (a free public MQTT broker used only as a
    /// listing board), or by IP; NATs are crossed with STUN + UDP hole punching.
    /// </summary>
    public sealed partial class NetManager : MonoBehaviour
    {
        public static NetManager I { get; private set; }
        public const int Protocol = 2;
        public const int DefaultPort = 27015;

        public readonly Dictionary<byte, NetPlayer> Players = new Dictionary<byte, NetPlayer>();
        public byte LocalId;
        public bool LocalCreative;
        public string LocalName => string.IsNullOrWhiteSpace(Settings.Data.playerName) ? "Player" : Settings.Data.playerName.Trim();
        public string LocalGuid { get; private set; }
        public ServerSettings Server = new ServerSettings();
        public ServerInfo Info;
        public string ServerName = "";
        public string Status = "";
        public IPEndPoint PublicEndpoint { get; private set; }
        public int Port => socket != null ? socket.Port : 0;
        public long BytesIn => (socket != null ? socket.BytesIn : 0) + (relay != null ? relay.BytesIn : 0);
        public long BytesOut => (socket != null ? socket.BytesOut : 0) + (relay != null ? relay.BytesOut : 0);
        /// <summary>School/work network mode: never open a port (no firewall/admin prompt); everything goes through the relay.</summary>
        public static bool Restricted => Settings.Data.restrictedNetwork;
        /// <summary>The code friends type to join this server (hosts), or the code of the server we're on.</summary>
        public string ServerCode => Info != null ? Info.Id : joinCode ?? "";
        /// <summary>True when this machine's game traffic goes through the relay instead of UDP.</summary>
        public bool ViaRelay => NetState.IsClient ? hostConn != null && Relay.IsRelay(hostConn.EndPoint) : NetState.IsHost && socket == null;
        public bool HasUdp => socket != null;
        public bool InGame { get; private set; }
        public Lobby Lobby => lobby;
        public readonly List<(string text, Color color, float time)> ChatLog = new List<(string, Color, float)>();

        NetSocket socket;
        Lobby lobby;
        Relay relay;
        readonly List<IPEndPoint> relayCandidates = new List<IPEndPoint>();
        double relayAfter;
        bool relayTried;
        string joinCode;
        LanDiscovery lanBroadcaster, lanListener;
        readonly Dictionary<string, NetConnection> conns = new Dictionary<string, NetConnection>();
        NetConnection hostConn;
        readonly List<IPEndPoint> joinCandidates = new List<IPEndPoint>();
        string joinPassword = "";
        double joinStarted, lastHello, lastLobby, lastLan, lastPing, lastPlayerList, lastRules, lastSnapshot, lastStateSend, lastStun;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly List<NetMessage> msgs = new List<NetMessage>();
        readonly System.Random rng = new System.Random();
        readonly NetWriter w = new NetWriter(2048);
        readonly NetReader r = new NetReader();
        readonly Dictionary<string, ServerInfo> lanServers = new Dictionary<string, ServerInfo>();
        Action onJoined;
        Action<string> onJoinFailed;
        int stunAttempts;
        IPEndPoint[] stunServers;

        public double Now => clock.Elapsed.TotalSeconds;

        public static void Create(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("NetManager");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<NetManager>();
        }

        void Awake()
        {
            LocalGuid = PlayerPrefs.GetString("mp_guid", "");
            if (string.IsNullOrEmpty(LocalGuid)) { LocalGuid = System.Guid.NewGuid().ToString("N"); PlayerPrefs.SetString("mp_guid", LocalGuid); }
            Bans.Load();
        }

        void Log(string s) => Debug.Log("[Net] " + s);

        // ================================================================== browsing

        /// <summary>Starts listening for LAN servers and the online list (used by the server browser).</summary>
        public void StartBrowsing()
        {
            if (Restricted) { lanListener?.Dispose(); lanListener = null; lanServers.Clear(); }
            else if (lanListener == null) lanListener = LanDiscovery.Listener();
            EnsureLobby();
            lobby.BrowseServers();
        }

        /// <summary>Creates (or re-creates after a settings change) the broker connections.</summary>
        void EnsureLobby()
        {
            WebTransport.ManualProxy = Settings.Data.netProxy ?? "";
            string custom = (Settings.Data.customRelay ?? "").Trim();
            if (lobby != null && (lobby.Restricted != Restricted || lobby.Custom != custom) && !NetState.Active) { lobby.Dispose(); lobby = null; }
            if (lobby == null) lobby = new Lobby(Protocol, Restricted, custom, s => Debug.Log(s));
        }

        /// <summary>School mode or relay settings changed in the menu.</summary>
        public void NetworkSettingsChanged()
        {
            if (NetState.Active) return;
            StopBrowsing();
            StartBrowsing();
        }

        static string NewServerCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I
            var b = new byte[8];
            using (var r = System.Security.Cryptography.RandomNumberGenerator.Create()) r.GetBytes(b);
            var c = new char[8];
            for (int i = 0; i < 8; i++) c[i] = alphabet[b[i] % alphabet.Length];
            return new string(c);
        }

        /// <summary>"k7p2-qx9m" → "K7P2QX9M"; null if it doesn't look like a server code.</summary>
        public static string NormalizeCode(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s.Trim().ToUpperInvariant())
            {
                if (ch == '-' || ch == ' ') continue;
                if (!((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))) return null;
                sb.Append(ch);
            }
            return sb.Length == 8 ? sb.ToString() : null;
        }

        public void StopBrowsing()
        {
            if (NetState.Active) return;
            lanListener?.Dispose(); lanListener = null;
            lobby?.Dispose(); lobby = null;
            lanServers.Clear();
        }

        public string LobbyStatus => lobby == null ? "Offline" : lobby.Status;

        /// <summary>All known servers (LAN + online), merged by id.</summary>
        public List<ServerInfo> Servers()
        {
            var map = new Dictionary<string, ServerInfo>();
            if (lobby != null) foreach (var kv in lobby.Servers) map[kv.Key] = kv.Value;
            foreach (var kv in lanServers)
            {
                if (Now - kv.Value.LastSeen > 6) continue;
                if (map.TryGetValue(kv.Key, out var online)) { online.FoundOnLan = true; online.LanEndpoint = kv.Value.LanEndpoint; }
                else map[kv.Key] = kv.Value;
            }
            var list = new List<ServerInfo>(map.Values);
            list.Sort((a, b) => b.Players.CompareTo(a.Players));
            return list;
        }

        // ================================================================== hosting

        public bool StartHost(ServerSettings settings, MapDef map, MapOptions options)
        {
            Shutdown("restart");
            if (!Restricted)
            {
                try { socket = new NetSocket(DefaultPort) { Log = Log }; }
                catch (Exception e) { socket = null; Log("No UDP port (" + e.Message + "), hosting through the relay only"); }
            }
            EnsureLobby();
            Server = settings;
            NetState.Role = NetRole.Host;
            LocalId = 1;
            LocalCreative = true;
            ServerName = settings.Name;
            Players.Clear();
            Players[1] = new NetPlayer { Id = 1, Name = LocalName, Guid = LocalGuid, Creative = true, IsHost = true, Ready = true, JoinedAt = Now };
            Info = new ServerInfo
            {
                Id = NewServerCode(), Name = settings.Name, HostName = LocalName, MapId = map.Id, MapName = map.Name,
                GameVersion = GameInfo.Version, MaxPlayers = settings.MaxPlayers, Players = 1, Password = !string.IsNullOrEmpty(settings.Password), Protocol = Protocol,
                LanEndpoint = socket != null ? AddressUtil.BestLanAddress() + ":" + socket.Port : "",
                Relay = true, DirectOk = socket != null,
            };
            relay = new Relay(lobby, Info.Id);
            if (socket != null)
            {
                BeginStun();
                lanBroadcaster = LanDiscovery.Broadcaster();
                Status = "Hosting on port " + socket.Port + " (code " + Info.Id + ")";
                Log($"Hosting '{settings.Name}' on UDP {socket.Port} (LAN {Info.LanEndpoint}), relay code {Info.Id}");
            }
            else
            {
                Status = "Hosting through the relay (code " + Info.Id + ")";
                Log($"Hosting '{settings.Name}' through the relay only, code {Info.Id}");
            }
            int seed = Random.Range(1, int.MaxValue);
            GameManager.I.StartMap(map, options, seed);
            return true;
        }

        // ================================================================== joining

        public void Join(ServerInfo info, string password, Action joined, Action<string> failed)
        {
            var eps = new List<IPEndPoint>();
            if (!Restricted && info.DirectOk)
            {
                var lan = AddressUtil.Parse(info.LanEndpoint);
                var pub = AddressUtil.Parse(info.PublicEndpoint);
                if (lan != null) eps.Add(lan);
                if (pub != null && (lan == null || !pub.Equals(lan))) eps.Add(pub);
            }
            if (eps.Count == 0 && !info.Relay)
            {
                failed?.Invoke(Restricted ? "This server can't be reached in school network mode (it's LAN-only)." : "That server didn't publish an address.");
                return;
            }
            BeginJoin(eps, info.Relay ? info.Id : null, info.BrokerMask, password, joined, failed);
            if (info.FoundOnline && lobby != null && socket != null)
            {
                // Ask the host (through the lobby) to punch a hole toward us once our public address is known.
                pendingIntentServer = info.Id;
            }
        }

        string pendingIntentServer;

        /// <summary>Joins by server code (works everywhere, through the relay) or by IP:port.</summary>
        public void JoinDirect(string address, string password, Action joined, Action<string> failed)
        {
            var code = NormalizeCode(address);
            bool looksLikeIp = (address ?? "").Contains(".") || (address ?? "").Contains(":");
            if (code != null && !looksLikeIp) { BeginJoin(new List<IPEndPoint>(), code, 0, password, joined, failed); return; }
            if (Restricted) { failed?.Invoke("School network mode can't connect by IP address. Ask the host for their 8-letter server code (shown in their Esc > Players menu)."); return; }
            var ep = AddressUtil.Parse(address);
            if (ep == null) { failed?.Invoke("Couldn't understand that. Type the host's 8-letter server code, or an IP:port like 192.168.1.20:27015"); return; }
            BeginJoin(new List<IPEndPoint> { ep }, null, 0, password, joined, failed);
        }

        void BeginJoin(List<IPEndPoint> endpoints, string relayServer, int brokerMask, string password, Action joined, Action<string> failed)
        {
            var keepLobby = lobby;
            lobby = null; // don't let Shutdown dispose the browsing lobby; we may need it for the join request
            Shutdown("rejoin");
            lobby = keepLobby;
            if (endpoints.Count == 0 && relayServer == null) { failed?.Invoke("That server didn't publish an address."); return; }
            if (endpoints.Count > 0 && !Restricted)
            {
                try { socket = new NetSocket(0) { Log = Log }; }
                catch (Exception e) { socket = null; if (relayServer == null) { failed?.Invoke("Network error: " + e.Message); return; } }
            }
            if (socket == null && relayServer == null) { failed?.Invoke(Restricted ? "School network mode can only join by server code." : "Couldn't open a network connection."); return; }
            relayCandidates.Clear();
            relayTried = false;
            joinCode = relayServer;
            if (relayServer != null)
            {
                EnsureLobby();
                relay = new Relay(lobby, Relay.NewId());
                relayCandidates.AddRange(relay.EndpointsFor(relayServer, brokerMask));
                // Direct UDP is faster, so give it a head start; go straight to the relay when there's no UDP.
                relayAfter = socket == null ? 0 : 3.0;
            }
            NetState.Role = NetRole.Client;
            joinCandidates.Clear();
            if (socket != null) joinCandidates.AddRange(endpoints);
            joinPassword = password ?? "";
            joinStarted = Now;
            lastHello = -1;
            onJoined = joined;
            onJoinFailed = failed;
            Players.Clear();
            LocalCreative = false;
            Status = socket != null ? "Contacting " + string.Join(" / ", endpoints) : "Connecting through the relay...";
            if (socket != null) BeginStun();
            Log("Joining " + (socket != null ? string.Join(", ", endpoints) : "") + (relayServer != null ? " relay code " + relayServer : ""));
        }

        void FailJoin(string reason)
        {
            var cb = onJoinFailed;
            onJoinFailed = null; onJoined = null;
            Shutdown(reason);
            cb?.Invoke(reason);
        }

        // ================================================================== STUN

        void BeginStun()
        {
            stunAttempts = 0;
            lastStun = -10;
            PublicEndpoint = null;
            System.Threading.Tasks.Task.Run(() =>
            {
                var list = new List<IPEndPoint>();
                foreach (var (host, port) in Stun.Servers)
                {
                    try
                    {
                        foreach (var a in Dns.GetHostAddresses(host))
                            if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { list.Add(new IPEndPoint(a, port)); break; }
                    }
                    catch { }
                }
                stunServers = list.ToArray();
            });
        }

        void UpdateStun()
        {
            if (PublicEndpoint != null || socket == null || stunServers == null || stunServers.Length == 0 || stunAttempts > 12) return;
            if (Now - lastStun < 0.5) return;
            lastStun = Now;
            var server = stunServers[stunAttempts % stunServers.Length];
            stunAttempts++;
            var req = Stun.BuildRequest(rng);
            socket.Send(req, req.Length, server);
        }

        // ================================================================== shutdown

        public void Shutdown(string reason)
        {
            if (NetState.IsHost)
            {
                foreach (var p in Players.Values) if (p.Conn != null) SendBye(p.Conn.EndPoint, "The host closed the server.");
                lobby?.StopHosting();
            }
            else if (NetState.IsClient && hostConn != null) SendBye(hostConn.EndPoint, "left");
            foreach (var p in Players.Values) if (p.Avatar != null) Destroy(p.Avatar.gameObject);
            Players.Clear();
            conns.Clear();
            hostConn = null;
            socket?.Dispose(); socket = null;
            if (relay != null) { relay.Flush(Now, true); relay.Dispose(); relay = null; } // flush sends the Bye messages
            relayCandidates.Clear();
            joinCode = null;
            lanBroadcaster?.Dispose(); lanBroadcaster = null;
            if (reason != "rejoin" && reason != "restart") { lobby?.Dispose(); lobby = null; lanListener?.Dispose(); lanListener = null; }
            NetState.Role = NetRole.None;
            InGame = false;
            Info = null;
            LocalCreative = false;
            ChatLog.Clear();
            pendingIntentServer = null;
            if (reason != "rejoin" && reason != "restart") Log("Session ended: " + reason);
        }

        void SendBye(IPEndPoint ep, string reason)
        {
            w.Reset(); w.String(reason);
            var p = Wire.Connectionless((byte)Msg.Bye, w.Buffer, w.Length);
            for (int i = 0; i < 3; i++) SendTo(p, p.Length, ep);
        }

        /// <summary>Routes a packet to UDP or the relay depending on the address.</summary>
        void SendTo(byte[] data, int length, IPEndPoint to)
        {
            if (Relay.IsRelay(to)) relay?.Send(data, length, to);
            else socket?.Send(data, length, to);
        }

        void OnApplicationQuit() => Shutdown("quit");

        // ================================================================== main loop

        void Update()
        {
            double now = Now;
            if (lanListener != null)
                while (lanListener.Found.TryDequeue(out var f))
                {
                    if (f.info.Protocol != Protocol) continue;
                    f.info.FoundOnLan = true;
                    f.info.LastSeen = now;
                    // Prefer the address we actually heard the broadcast from.
                    var ep = AddressUtil.Parse(f.info.LanEndpoint);
                    if (ep != null && !f.from.Address.Equals(IPAddress.Loopback)) f.info.LanEndpoint = f.from.Address + ":" + ep.Port;
                    lanServers[f.info.Id] = f.info;
                }
            lobby?.Poll(now);
            if (socket == null && relay == null) return;

            UpdateStun();
            while (socket != null && socket.Inbox.TryDequeue(out var d)) HandleDatagram(d, now);
            while (relay != null && relay.Inbox.Count > 0) HandleDatagram(relay.Inbox.Dequeue(), now);
            if (socket == null && relay == null) return;

            if (NetState.IsHost) HostUpdate(now);
            else if (NetState.IsClient) ClientUpdate(now);
            relay?.Flush(now);
        }

        void HandleDatagram(Datagram d, double now)
        {
            if (Stun.IsResponse(d.Data, d.Length))
            {
                var ep = Stun.Parse(d.Data, d.Length);
                if (ep != null && PublicEndpoint == null)
                {
                    PublicEndpoint = ep;
                    Log("Public address (STUN): " + ep);
                    if (Info != null) Info.PublicEndpoint = ep.ToString();
                    if (NetState.IsClient && pendingIntentServer != null && lobby != null) SendJoinIntent();
                }
                return;
            }
            if (!Wire.IsGame(d.Data, d.Length)) return;
            if (d.Data[2] == Wire.KConnectionless) { HandleConnectionless((Msg)d.Data[3], d.Data, d.Length, d.From, now); return; }

            NetConnection conn = null;
            if (NetState.IsHost) conns.TryGetValue(d.From.ToString(), out conn);
            else if (NetState.IsClient)
            {
                // Accept the host's first reliable reply even if its NAT changed the port we expected.
                if (hostConn == null && onJoined != null && (joinCandidates.Exists(c => c.Equals(d.From)) || d.Data[2] == Wire.KReliable))
                {
                    hostConn = new NetConnection(d.From, SendTo, now);
                    Log("Connected to host " + (Relay.IsRelay(d.From) ? "through the relay" : "at " + d.From));
                }
                if (hostConn != null && hostConn.EndPoint.Equals(d.From)) conn = hostConn;
            }
            if (conn == null) return;
            msgs.Clear();
            conn.Receive(d.Data, d.Length, now, msgs);
            for (int i = 0; i < msgs.Count; i++)
            {
                try { Dispatch(conn, msgs[i]); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        void SendJoinIntent()
        {
            var intent = new JoinIntent
            {
                Name = LocalName, Nonce = LocalGuid.Substring(0, 8),
                PublicEndpoint = PublicEndpoint != null ? PublicEndpoint.ToString() : "",
                LanEndpoint = socket != null ? AddressUtil.BestLanAddress() + ":" + socket.Port : "",
            };
            lobby.RequestJoin(pendingIntentServer, intent);
            Log("Sent join request through the lobby (" + intent.PublicEndpoint + ")");
            pendingIntentServer = null;
        }

        // ------------------------------------------------------------------ connectionless (handshake, punch)

        void HandleConnectionless(Msg type, byte[] data, int len, IPEndPoint from, double now)
        {
            r.Set(data, 4, len - 4);
            switch (type)
            {
                case Msg.Punch:
                    break; // only opens NAT mappings
                case Msg.Hello when NetState.IsHost:
                    HandleHello(from, now);
                    break;
                case Msg.Reject when NetState.IsClient && hostConn == null:
                    FailJoin(r.String());
                    break;
                case Msg.Bye:
                {
                    string reason = r.String();
                    if (NetState.IsClient && hostConn != null && hostConn.EndPoint.Equals(from)) LostConnection(reason == "left" ? "The host left." : reason);
                    else if (NetState.IsHost && conns.TryGetValue(from.ToString(), out var c) && c.Tag is NetPlayer p) DropPlayer(p, "left the game");
                    break;
                }
            }
        }

        void HandleHello(IPEndPoint from, double now)
        {
            if (conns.ContainsKey(from.ToString())) return; // already connected (duplicate hello)
            int proto = r.Int();
            string version = r.String(), guid = r.String(), name = r.String(), password = r.String();
            name = CleanName(name);
            string reject = null;
            if (proto != Protocol || version != GameInfo.Version) reject = $"Version mismatch: server is {GameInfo.Version}, you have {version}.";
            else if (!InGame) reject = "The server is still loading. Try again in a few seconds.";
            else if (Bans.IsBanned(guid, Relay.IsRelay(from) ? "" : from.Address.ToString())) reject = "You are banned from this server.";
            else if (Players.Count >= Server.MaxPlayers) reject = "The server is full.";
            else if (!string.IsNullOrEmpty(Server.Password) && password != Server.Password) reject = "Wrong password.";
            else foreach (var pl in Players.Values) if (pl.Guid == guid) return; // same player reaching us via a second address: ignore
            if (reject != null)
            {
                w.Reset(); w.String(reject);
                var pkt = Wire.Connectionless((byte)Msg.Reject, w.Buffer, w.Length);
                for (int i = 0; i < 3; i++) SendTo(pkt, pkt.Length, from);
                Log($"Rejected {name} ({from}): {reject}");
                return;
            }
            byte id = 2;
            while (Players.ContainsKey(id) && id < 250) id++;
            var conn = new NetConnection(from, SendTo, now);
            var p = new NetPlayer { Id = id, Name = UniqueName(name), Guid = guid, Address = Relay.IsRelay(from) ? "relay" : from.Address.ToString(), Conn = conn, Creative = Server.CreativeForAll, JoinedAt = now };
            conn.Tag = p;
            conn.Id = id;
            conns[from.ToString()] = conn;
            Players[id] = p;
            SendWelcome(p);
            Log($"{p.Name} connected {(Relay.IsRelay(from) ? "through the relay" : "from " + from)} as player {id}");
        }

        string CleanName(string n)
        {
            n = (n ?? "").Trim();
            if (n.Length > 20) n = n.Substring(0, 20);
            return n.Length == 0 ? "Player" : n;
        }

        string UniqueName(string n)
        {
            string name = n;
            int k = 2;
            bool taken = true;
            while (taken)
            {
                taken = false;
                foreach (var p in Players.Values) if (p.Name == name) { taken = true; name = n + " (" + k++ + ")"; break; }
            }
            return name;
        }

        // ------------------------------------------------------------------ host loop

        void HostUpdate(double now)
        {
            if (lobby != null && Server.Public && Info != null && InGame && now - lastLobby > 15)
            {
                lastLobby = now;
                Info.Players = Players.Count;
                lobby.Host(Info);
            }
            if (lobby != null && socket != null)
                while (lobby.JoinRequests.Count > 0)
                {
                    var j = lobby.JoinRequests.Dequeue();
                    Log("Join request from " + j.Name + " " + j.PublicEndpoint);
                    foreach (var s in new[] { j.PublicEndpoint, j.LanEndpoint })
                    {
                        var ep = AddressUtil.Parse(s);
                        if (ep == null) continue;
                        var pkt = Wire.Connectionless((byte)Msg.Punch, new byte[0], 0);
                        for (int i = 0; i < 6; i++) socket.Send(pkt, pkt.Length, ep);
                    }
                }
            if (lanBroadcaster != null && Info != null && InGame && now - lastLan > 1.5)
            {
                lastLan = now;
                Info.Players = Players.Count;
                Info.UnixTime = ServerInfo.Now;
                lanBroadcaster.Broadcast(Info);
            }

            var dead = new List<NetPlayer>();
            foreach (var p in Players.Values)
            {
                if (p.Conn == null) continue;
                p.Conn.Update(now);
                if (p.Conn.Failed || now - p.Conn.LastHeard > 12) dead.Add(p);
            }
            foreach (var p in dead) DropPlayer(p, "timed out");

            if (now - lastPing > 1)
            {
                lastPing = now;
                foreach (var p in Players.Values) if (p.Conn != null) { w.Reset(); w.Float((float)now); p.Conn.SendUnreliable((byte)Msg.Ping, w.Buffer, w.Length); }
            }
            if (now - lastPlayerList > 2) { lastPlayerList = now; BroadcastPlayerList(); }
            if (now - lastRules > 1) { lastRules = now; BroadcastRules(); }
            if (InGame && now - lastSnapshot > 1.0 / 20.0) { lastSnapshot = now; SendSnapshots(); }
            UpdateRemoteGrabs();
        }

        // ------------------------------------------------------------------ client loop

        void ClientUpdate(double now)
        {
            if (hostConn == null || onJoined != null)
            {
                // Still connecting: keep knocking on every address we know.
                if (now - lastHello > 0.25)
                {
                    lastHello = now;
                    w.Reset();
                    w.Int(Protocol); w.String(GameInfo.Version); w.String(LocalGuid); w.String(LocalName); w.String(joinPassword);
                    if (!relayTried && relay != null && now - joinStarted >= relayAfter)
                    {
                        relayTried = true;
                        joinCandidates.AddRange(relayCandidates);
                        Log("Trying the relay (code " + joinCode + ")");
                    }
                    var pkt = Wire.Connectionless((byte)Msg.Hello, w.Buffer, w.Length);
                    foreach (var ep in joinCandidates) SendTo(pkt, pkt.Length, ep);
                    double t = now - joinStarted;
                    double limit = relay != null ? 25 : 15;
                    if (socket == null)
                        Status = lobby != null && !lobby.Online ? "Connecting to the relay servers..." : t < 6 ? "Connecting through the relay..." : "Still trying... (" + (int)(limit - t) + "s)";
                    else
                        Status = t < 3 ? "Connecting..." : relayTried ? "Trying the relay..." : t < 8 ? "Punching through NAT..." : "Still trying... (" + (int)(limit - t) + "s)";
                }
                // No STUN answer (blocked network)? Ask the host to punch toward our LAN address anyway.
                if (pendingIntentServer != null && lobby != null && socket != null && now - joinStarted > 2.5) SendJoinIntent();
                if (now - joinStarted > (relay != null ? 25 : 15) && onJoined != null)
                {
                    if (relay != null)
                        FailJoin(lobby != null && !lobby.Online
                            ? "Couldn't reach any relay server. Your network may block them, or need a proxy (Multiplayer > Join by code > Network options)."
                            : "The server didn't answer. Check the code, and make sure the host is still in the game.");
                    else
                        FailJoin("Couldn't reach the server. If it's on the internet, the host may need to forward UDP port " + (joinCandidates.Count > 0 ? joinCandidates[0].Port : DefaultPort) + ", or both of you may be behind strict NATs. LAN and direct IP always work on the same network.");
                    return;
                }
            }
            if (hostConn == null) return;
            hostConn.Update(now);
            if (hostConn.Failed || (now - hostConn.LastHeard > 12 && onJoined == null)) { LostConnection("Lost connection to the host."); return; }
            if (InGame && now - lastStateSend > 1.0 / 20.0) { lastStateSend = now; SendLocalPlayerState(); }
            UpdateClientGrab(now);
        }

        void LostConnection(string reason)
        {
            Log("Disconnected: " + reason);
            Shutdown(reason);
            if (GameManager.I != null) GameManager.I.ReturnToMenu();
            UIRoot.Push(new MessageDialog("DISCONNECTED", reason));
        }

        // ================================================================== sending helpers

        public NetWriter Writer() => w.Reset();

        /// <summary>Sends the voice packet currently in <see cref="Writer"/> (client → host, host → everyone).</summary>
        public void SendVoice(NetWriter payload)
        {
            if (NetState.IsClient) { ToHost(Msg.Voice, false); return; }
            if (!NetState.IsHost || !HasClients) return;
            var data = payload.ToArray();
            w.Reset(); w.Byte(LocalId); w.Bytes(data, 0, data.Length);
            ToClients(Msg.Voice, false);
        }

        public void ToHost(Msg type, bool reliable = true)
        {
            if (hostConn == null) return;
            if (reliable) hostConn.SendReliable((byte)type, w.Buffer, w.Length, Now);
            else hostConn.SendUnreliable((byte)type, w.Buffer, w.Length);
        }

        public void ToPlayer(NetPlayer p, Msg type, bool reliable = true)
        {
            if (p?.Conn == null) return;
            if (reliable) p.Conn.SendReliable((byte)type, w.Buffer, w.Length, Now);
            else p.Conn.SendUnreliable((byte)type, w.Buffer, w.Length);
        }

        /// <summary>Host → every ready client (optionally except one).</summary>
        public void ToClients(Msg type, bool reliable = true, NetPlayer except = null, bool includeLoading = false)
        {
            foreach (var p in Players.Values)
            {
                if (p.Conn == null || p == except || (!p.Ready && !includeLoading)) continue;
                if (reliable) p.Conn.SendReliable((byte)type, w.Buffer, w.Length, Now);
                else p.Conn.SendUnreliable((byte)type, w.Buffer, w.Length);
            }
        }

        public NetPlayer Local => Players.TryGetValue(LocalId, out var p) ? p : null;
        public NetPlayer Get(byte id) => Players.TryGetValue(id, out var p) ? p : null;
        public bool HasClients { get { foreach (var p in Players.Values) if (p.Conn != null && p.Ready) return true; return false; } }

        // ================================================================== session messages

        void SendWelcome(NetPlayer p)
        {
            SendCustomMap(p); // custom maps: the client needs the map data before it builds (NetManager.CustomMaps.cs)
            var gm = GameManager.I;
            var o = gm.CurrentOptions;
            w.Reset();
            w.Byte(p.Id); w.String(Server.Name); w.String(LocalName);
            w.String(gm.CurrentMap.Id); w.Int(gm.CurrentSeed);
            w.Byte((byte)o.Time); w.Byte((byte)o.Loadout); w.Bool(o.Nextbots); w.Bool(o.AiIgnore);
            w.Bool(p.Creative); w.Bool(Server.PvP); w.Byte((byte)Server.Loadout);
            w.Byte((byte)o.Weather); // appended last (older clients ignore it)
            ToPlayer(p, Msg.Welcome);
        }

        void BroadcastPlayerList()
        {
            w.Reset();
            w.Byte((byte)Players.Count);
            foreach (var p in Players.Values)
            {
                w.Byte(p.Id); w.String(p.Name); w.Bool(p.Creative); w.Bool(p.IsHost);
                w.Short((short)Mathf.Clamp(p.IsHost ? 0 : (p.Conn != null ? (int)(p.Conn.Rtt * 1000) : 0), 0, 9999));
            }
            ToClients(Msg.PlayerList, false);
        }

        void BroadcastRules()
        {
            w.Reset();
            w.Float(Rules.TimeScale); w.Float(Rules.GravityScale);
            w.Bool(Rules.NoFallDamage); w.Bool(Rules.Invincible); w.Bool(Rules.InfiniteAmmo); w.Bool(Rules.NoReload);
            w.Bool(Rules.BulletTracers); w.Bool(Rules.AiIgnorePlayer); w.Float(Rules.PlayerSpeedMultiplier); w.Float(Rules.JumpMultiplier);
            w.Bool(Server.PvP); w.Bool(Rules.Xray);
            ToClients(Msg.Rules, false);
        }

        public void SendChat(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;
            if (text.Length > 160) text = text.Substring(0, 160);
            if (NetState.IsHost) { BroadcastChat(LocalId, text); }
            else { w.Reset(); w.String(text); ToHost(Msg.Chat); }
        }

        void BroadcastChat(byte from, string text)
        {
            var p = Get(from);
            AddChat((p != null ? p.Name : "?") + ": " + text, p != null && p.IsHost ? UI.Accent2 : Color.white);
            w.Reset(); w.Byte(from); w.String(text);
            ToClients(Msg.Chat);
        }

        public void SystemMessage(string text, bool broadcast = true)
        {
            AddChat(text, UI.Warn);
            if (broadcast && NetState.IsHost) { w.Reset(); w.Byte(0); w.String(text); ToClients(Msg.Toast); }
        }

        public void AddChat(string text, Color c)
        {
            ChatLog.Add((text, c, Time.unscaledTime));
            if (ChatLog.Count > 60) ChatLog.RemoveAt(0);
            AudioManager.Ui("ui_notify", 0.3f);
        }

        // ================================================================== host powers

        public void SetCreative(NetPlayer p, bool on)
        {
            if (!NetState.IsHost || p == null || p.IsHost) return;
            p.Creative = on;
            w.Reset(); w.Bool(on);
            ToPlayer(p, Msg.Permission);
            SystemMessage(p.Name + (on ? " was given Creative mode." : " is no longer in Creative mode."));
            BroadcastPlayerList();
        }

        public void Kick(NetPlayer p, string reason = "Kicked by the host.")
        {
            if (!NetState.IsHost || p == null || p.IsHost) return;
            w.Reset(); w.String(reason);
            ToPlayer(p, Msg.Kicked);
            if (p.Conn != null) SendBye(p.Conn.EndPoint, reason);
            DropPlayer(p, "was kicked");
        }

        public void Ban(NetPlayer p)
        {
            if (!NetState.IsHost || p == null || p.IsHost) return;
            Bans.Add(p.Guid, p.Name, p.Address);
            Kick(p, "You were banned from this server.");
        }

        void DropPlayer(NetPlayer p, string why)
        {
            if (!Players.ContainsKey(p.Id)) return;
            Players.Remove(p.Id);
            if (p.Conn != null) conns.Remove(p.Conn.EndPoint.ToString());
            if (p.Avatar != null) Destroy(p.Avatar.gameObject);
            ReleaseRemoteGrab(p, Vector3.zero);
            w.Reset(); w.Byte(p.Id); w.String(why);
            ToClients(Msg.PlayerLeft);
            SystemMessage(p.Name + " " + why + ".", false);
            Log(p.Name + " " + why);
        }

        // ================================================================== map lifecycle (called by GameManager)

        /// <summary>The (seeded) map finished building on this machine.</summary>
        public void OnMapLoaded()
        {
            InGame = true;
            if (NetState.IsHost)
            {
                lastLobby = -100; lastLan = -100;
                if (socket != null) SystemMessage("Server started. " + (Server.Public ? "Listed online and on your LAN." : "Visible on your LAN.") + " Port " + Port + ". Server code: " + Info.Id, false);
                else SystemMessage("Server started in school network mode. " + (Server.Public ? "Listed online. " : "") + "Friends join with the code " + Info.Id + " (Multiplayer > Join by code).", false);
            }
            else if (NetState.IsClient)
            {
                w.Reset();
                ToHost(Msg.ClientReady);
                SendMyProfile(); // NetManager.Profile.cs
                foreach (var p in Players.Values) EnsureAvatar(p);
                ApplyLoadout();
            }
        }

        /// <summary>Gives the local player the server's loadout and Creative tools (or takes them away).</summary>
        public void ApplyLoadout()
        {
            var pc = PlayerController.Instance;
            if (pc == null || pc.Inventory == null) return;
            var inv = pc.Inventory;
            bool creative = NetState.LocalCreative;
            if (!creative)
            {
                for (int i = 0; i < Inventory.SlotCount; i++)
                    if (inv.Slots[i] != null && (inv.Slots[i].Def.Kind == WeaponKind.SpawnGun || inv.Slots[i].Def.Kind == WeaponKind.Admin
                        || inv.Slots[i].Def.Kind == WeaponKind.RopeTool || inv.Slots[i].Def.Kind == WeaponKind.GravityGun)) inv.Remove(i);
                if (pc.Noclip) pc.SetNoclip(false);
            }
            else if (!inv.Has("spawngun")) inv.Give("spawngun", inv.Slots[1] == null ? 1 : -1, false);
            if (NetState.IsClient && clientLoadout == 1 && !inv.Has("glock"))
                foreach (var id in new[] { "knife", "glock", "uzi", "ar15", "ar17", "shotgun", "sniper" }) inv.Give(id, -1, false);
        }

        int clientLoadout;

        void EnsureAvatar(NetPlayer p)
        {
            if (p.Id == LocalId || p.Avatar != null || !InGame) return;
            p.Avatar = RemotePlayer.Create(p, WorldRoot.Current != null ? WorldRoot.Current.transform : null);
        }
    }
}
