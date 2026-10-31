using System.Collections.Generic;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// "PLAY MULTIPLAYER": hosted servers (LAN + online), HOST a server, or DIRECT IP. No paid servers: hosts
    /// announce themselves on the LAN and on a free public lobby; players connect peer-to-peer.
    /// </summary>
    public sealed class MultiplayerScreen : UIScreen
    {
        static int tab;
        static readonly string[] Tabs = { "OPEN WORLD", "HOSTED SERVERS", "HOST A SERVER", "JOIN BY CODE / IP" };
        static readonly string[] Visibility = { "Public (in the server list)", "Private (join by code only)" };
        static readonly string[] Loadouts = { "Fists only", "Fists + guns" };
        static string direct = "";
        static string directPassword = "";
        static string proxyEdit, relayEdit;
        static readonly ServerSettings host = new ServerSettings();
        static int mapIndex, timeIndex;
        static float npcs = 3, maxPlayers = 8;
        static int visibility, loadout = 1;
        static bool nextbots;

        public override void OnOpen()
        {
            AudioManager.Ui("ui_open");
            NetManager.I.StartBrowsing();
            if (string.IsNullOrWhiteSpace(Settings.Data.playerName)) { Settings.Data.playerName = "Player" + Random.Range(1000, 9999); Settings.Save(); }
            if (string.IsNullOrEmpty(host.Name) || host.Name == "Gore Sandbox Server") host.Name = Settings.Data.playerName + "'s server";
        }

        public override bool OnBack()
        {
            AudioManager.Ui("ui_close");
            NetManager.I.StopBrowsing();
            Settings.Save();
            UIRoot.Pop();
            return true;
        }

        public override void Draw()
        {
            float a = Intro(0, 0.25f);
            UI.Dim(0.55f * a);
            var r = UI.Centered(Mathf.Min(1400, UI.VW - 80), Mathf.Min(900, UI.VH - 60));
            UI.Panel(r, "Multiplayer", a);
            var nm = NetManager.I;

            // Name + network status
            UI.Label(new Rect(r.x + 330, r.y + 20, 140, 40), "Your name", 17, UI.Muted, TextAnchor.MiddleRight);
            var name = UI.TextField(new Rect(r.x + 480, r.y + 16, 300, 48), Settings.Data.playerName, "mp_name", false, "Name");
            if (name != Settings.Data.playerName) Settings.Data.playerName = name.Length > 20 ? name.Substring(0, 20) : name;
            // Profile (icon, hat, look, badges): UI/Screens/ProfileScreen.cs
            PlayerProfile.DrawIcon(new Rect(r.x + 796, r.y + 16, 48, 48), PlayerProfile.Local);
            if (UI.Button(new Rect(r.x + 852, r.y + 16, 170, 48), "PROFILE", ButtonKind.Normal, null, 18, true, "Your icon, hat, look and badges (what other players see).")) UIRoot.Push(new ProfileScreen());
            UI.Label(new Rect(r.x + 1036, r.y + 16, r.width - 1066, 48), "Lobby: " + nm.LobbyStatus, 15, nm.Lobby != null && nm.Lobby.Online ? UI.Good : UI.Muted, TextAnchor.MiddleRight);

            int nt = UI.Tabs(new Rect(r.x + 24, r.y + 80, r.width - 48, 50), tab, Tabs, 19);
            if (nt != tab) { tab = nt; AudioManager.Ui("ui_tab"); }
            var body = new Rect(r.x + 24, r.y + 146, r.width - 48, r.height - 240);
            switch (tab)
            {
                case 0: DrawOpenWorld(body); break;
                case 1: DrawServers(body); break;
                case 2: DrawHost(body); break;
                case 3: DrawDirect(body); break;
            }
            if (UI.Button(new Rect(r.x + 24, r.yMax - 78, 220, 56), "BACK", ButtonKind.Normal, IconFactory.Get("arrow_left"))) OnBack();
            bool school = Settings.Data.restrictedNetwork;
            if (UI.Toggle(new Rect(r.x + 264, r.yMax - 78, 560, 56), "School / work network mode", ref school,
                "Turn this on if hosting or joining asks for an admin password (school, work or library networks). The game then never opens a network port, so there's no firewall prompt: everything goes through free relay servers over normal secure web traffic (port 443). A bit more lag, works almost anywhere."))
            {
                Settings.Data.restrictedNetwork = school;
                Settings.Save();
                nm.NetworkSettingsChanged();
            }
            UI.Label(new Rect(r.x + 844, r.yMax - 78, r.width - 874, 56), school ? "No ports, no admin prompt. Join with server codes." : "No paid servers: the host's game is the server. Voice: " + VoiceChat.KeyHint, 15, school ? UI.Good : UI.Muted, TextAnchor.MiddleRight, FontStyle.Normal, true, true);
        }

        // ------------------------------------------------------------------ server list

        void DrawServers(Rect body)
        {
            var list = NetManager.I.Servers();
            if (list.Count == 0)
            {
                UI.Rounded(new Rect(body.x, body.y, body.width, 200), UI.PanelLight, false);
                UI.Tex(new Rect(body.x + 40, body.y + 55, 90, 90), IconFactory.Get("wifi"), UI.Muted);
                float dots = Mathf.Repeat(Time.unscaledTime, 1.5f) / 0.5f;
                UI.Label(new Rect(body.x + 160, body.y + 30, body.width - 200, 140),
                    "Looking for servers" + new string('.', 1 + (int)dots) + "\n\nNobody is hosting right now. Click HOST A SERVER and it will pop up here for everyone. " +
                    (Settings.Data.restrictedNetwork ? "School network mode: servers appear once the relay is online (see top right). Private servers: use JOIN BY CODE." : "Players on your network see it instantly, online players in a few seconds."),
                    20, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                return;
            }
            const float rowH = 92f;
            float content = list.Count * (rowH + 10);
            float s = UI.BeginScroll("servers", body, content);
            for (int i = 0; i < list.Count; i++)
            {
                var sv = list[i];
                var rr = new Rect(0, i * (rowH + 10) - s, body.width - 16, rowH);
                if (rr.yMax < 0 || rr.y > body.height) continue;
                bool compatible = sv.GameVersion == GameInfo.Version;
                bool full = sv.Players >= sv.MaxPlayers;
                if (Settings.Data.restrictedNetwork && !sv.Relay) compatible = false;
                UI.Rounded(rr, UI.Hover(rr) ? new Color(1, 1, 1, 0.1f) : UI.PanelLight, false);
                UI.Tex(new Rect(rr.x + 16, rr.y + 16, 60, 60), IconFactory.Get(sv.Password ? "lock" : "globe"), sv.Password ? UI.Warn : UI.Accent2);
                UI.Label(new Rect(rr.x + 96, rr.y + 8, rr.width - 520, 40), sv.Name, 24, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                string where = (sv.FoundOnLan ? "LAN" : "") + (sv.FoundOnLan && sv.FoundOnline ? " + " : "") + (sv.FoundOnline ? "ONLINE" : "") + (sv.DirectOk ? "" : " (school mode)");
                UI.Label(new Rect(rr.x + 96, rr.y + 46, rr.width - 520, 34), "Host: " + sv.HostName + "  ·  Map: " + sv.MapName + "  ·  " + where + (compatible ? "" : "  ·  needs v" + sv.GameVersion), 16, compatible ? UI.Muted : UI.Bad);
                UI.Label(new Rect(rr.xMax - 410, rr.y, 180, rowH), sv.Players + " / " + sv.MaxPlayers, 26, full ? UI.Bad : UI.Good, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (UI.Button(new Rect(rr.xMax - 210, rr.y + 16, 190, 60), full ? "FULL" : "JOIN", ButtonKind.Primary, IconFactory.Get("play"), 22, compatible && !full))
                {
                    if (sv.Password) UIRoot.Push(new PasswordDialog(pw => Connect(sv, null, pw)));
                    else Connect(sv, null, "");
                }
            }
            UI.EndScroll("servers", body, content);
        }

        // ------------------------------------------------------------------ open world (OpenWorld/)

        static float owPlayers = 12;
        static int owVisibility, owTime = 1;

        void DrawOpenWorld(Rect body)
        {
            float colW = (body.width - 30) / 2f;
            var left = new Rect(body.x, body.y, colW, body.height);
            var right = new Rect(body.x + colW + 30, body.y, colW, body.height);

            UI.Gradient(new Rect(left.x, left.y, left.width, 150), new Color(0.35f, 0.12f, 0.25f), new Color(0.9f, 0.45f, 0.15f));
            UI.Label(new Rect(left.x + 20, left.y + 14, left.width - 40, 60), "PORT ECLIPSE", 44, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, true, false, UI.HeavyFont);
            UI.Label(new Rect(left.x + 20, left.y + 72, left.width - 40, 70), "A whole city in multiplayer. Do heists with your crew, earn cash (it's saved), buy cars, clothes, masks, guns and a penthouse.", 16, Color.white, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            float y = left.y + 164;
            UI.Label(new Rect(left.x, y, left.width, 110),
                "- 5 heists (2+ players), street races, deliveries, bounties\n- Police and wanted stars, armed guards, a gang\n- 12 cars, 100 things to buy, a phone with calls, texts, money and a map\n- You start with $1,500 and a free Glock with unlimited ammo",
                15, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            y += 120;
            host.Name = UI.TextField(new Rect(left.x, y, left.width, 52), host.Name, "mp_ow_name", false, "Server name"); y += 62;
            UI.Slider(new Rect(left.x, y, colW / 2f - 8, 56), "Max players", ref owPlayers, 2, 16, "0");
            UI.Selector(new Rect(left.x + colW / 2f + 8, y, colW / 2f - 8, 56), "Time", ref owTime, MapOptions.Times); y += 66;
            UI.Selector(new Rect(left.x, y, colW / 2f - 8, 56), "Visibility", ref owVisibility, Visibility);
            UI.Toggle(new Rect(left.x + colW / 2f + 8, y, colW / 2f - 8, 56), "PvP damage", ref host.PvP); y += 66;
            var def = MapCatalog.All.Find(m => m.Id == OWMap.MapId);
            float bw = (left.width - 16) / 2f;
            if (UI.Button(new Rect(left.x, left.yMax - 70, bw, 70), "HOST OPEN WORLD", ButtonKind.Primary, IconFactory.Get("globe"), 22, def != null && !string.IsNullOrWhiteSpace(host.Name)))
            {
                Settings.Save();
                host.MaxPlayers = Mathf.RoundToInt(owPlayers);
                host.Public = owVisibility == 0;
                host.Loadout = 0;
                host.CreativeForAll = false;
                var options = new MapOptions { Time = (TimeOfDay)owTime, StartingNpcs = 0, Loadout = 0, Nextbots = false, AiIgnore = false };
                if (!NetManager.I.StartHost(host, def, options)) UIRoot.Push(new MessageDialog("COULDN'T HOST", NetManager.I.Status));
            }
            if (UI.Button(new Rect(left.x + bw + 16, left.yMax - 70, bw, 70), "PLAY SOLO", ButtonKind.Normal, IconFactory.Get("person"), 22, def != null, "Singleplayer Port Eclipse. Same saved cash; heists pay 60% when you go alone."))
            {
                NetManager.I.StopBrowsing();
                GameManager.I.StartMap(def, new MapOptions { Time = (TimeOfDay)owTime, StartingNpcs = 0, Loadout = 0 });
            }

            // Right: Port Eclipse servers
            UI.Label(new Rect(right.x, right.y, right.width, 40), "OPEN WORLD SERVERS", 22, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Label(new Rect(right.x, right.y + 40, right.width, 26), "Your cash: $" + OWSave.Cash.ToString("N0") + "  ·  it goes with you to every Open World server", 15, new Color(0.45f, 1f, 0.5f));
            var list = NetManager.I.Servers().FindAll(sv => sv.MapId == OWMap.MapId || sv.MapName == "Port Eclipse");
            var view = new Rect(right.x, right.y + 76, right.width, right.height - 76);
            if (list.Count == 0)
            {
                UI.Rounded(new Rect(view.x, view.y, view.width, 160), UI.PanelLight, false);
                UI.Label(new Rect(view.x + 24, view.y + 16, view.width - 48, 130), "No Open World servers right now. Host one and it shows up here for everyone (friends can also join with your server code from JOIN BY CODE / IP).", 18, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
                return;
            }
            const float rowH = 86f;
            float content = list.Count * (rowH + 10);
            float sc = UI.BeginScroll("ow_servers", view, content);
            for (int i = 0; i < list.Count; i++)
            {
                var sv = list[i];
                var rr = new Rect(0, i * (rowH + 10) - sc, view.width - 16, rowH);
                if (rr.yMax < 0 || rr.y > view.height) continue;
                bool compatible = sv.GameVersion == GameInfo.Version && (!Settings.Data.restrictedNetwork || sv.Relay);
                bool full = sv.Players >= sv.MaxPlayers;
                UI.Rounded(rr, UI.Hover(rr) ? new Color(1, 1, 1, 0.1f) : UI.PanelLight, false);
                UI.Label(new Rect(rr.x + 16, rr.y + 6, rr.width - 230, 40), sv.Name, 22, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UI.Label(new Rect(rr.x + 16, rr.y + 44, rr.width - 230, 32), sv.Players + " / " + sv.MaxPlayers + " players  ·  host " + sv.HostName + (compatible ? "" : "  ·  needs v" + sv.GameVersion), 15, compatible ? UI.Muted : UI.Bad);
                if (UI.Button(new Rect(rr.xMax - 190, rr.y + 14, 174, 58), full ? "FULL" : "JOIN", ButtonKind.Primary, IconFactory.Get("play"), 21, compatible && !full))
                {
                    if (sv.Password) UIRoot.Push(new PasswordDialog(pw => Connect(sv, null, pw)));
                    else Connect(sv, null, "");
                }
            }
            UI.EndScroll("ow_servers", view, content);
        }

        // ------------------------------------------------------------------ host

        void DrawHost(Rect body)
        {
            var maps = MapCatalog.All.FindAll(m => m.IsUnlocked() && m.Id != BossRealm.MapId);
            var mapNames = maps.ConvertAll(m => m.Name).ToArray();
            mapIndex = Mathf.Clamp(mapIndex, 0, mapNames.Length - 1);
            float colW = (body.width - 30) / 2f, y = body.y;
            float x2 = body.x + colW + 30;
            host.Name = UI.TextField(new Rect(body.x, y, colW, 52), host.Name, "mp_server", false, "Server name");
            UI.Selector(new Rect(x2, y, colW, 56), "Map", ref mapIndex, mapNames); y += 70;
            UI.Selector(new Rect(body.x, y, colW, 56), "Time of day", ref timeIndex, MapOptions.Times);
            UI.Slider(new Rect(x2, y, colW, 56), "Goredolls", ref npcs, 0, 20, "0"); y += 66;
            UI.Slider(new Rect(body.x, y, colW, 56), "Max players", ref maxPlayers, 2, 16, "0");
            UI.Selector(new Rect(x2, y, colW, 56), "Visibility", ref visibility, Visibility, "Public servers are also listed online (your public IP is shared with players who join)."); y += 66;
            UI.Selector(new Rect(body.x, y, colW, 56), "Loadout (non-Creative players)", ref loadout, Loadouts);
            UI.Toggle(new Rect(x2, y, colW, 56), "Player vs player damage", ref host.PvP); y += 66;
            UI.Toggle(new Rect(body.x, y, colW, 56), "Everyone starts in Creative mode", ref host.CreativeForAll, "Creative = noclip, Spawn Gun, spawn menu, Admin Gun. You can grant or remove it per player from Esc > Players.");
            if (Stats.HasEasterEgg("Nextbots")) UI.Toggle(new Rect(x2, y, colW, 56), "Nextbots roam", ref nextbots); y += 70;
            UI.Label(new Rect(body.x, y, 200, 52), "Password (optional)", 17, UI.Muted);
            host.Password = UI.TextField(new Rect(body.x + 210, y, colW - 210, 52), host.Password, "mp_pass", true, "none");
            UI.Toggle(new Rect(x2, y, colW, 56), "Allow modded weapons", ref host.AllowMods, "Players may use weapons from the Modding Hub. You (the host) check every mod with the anti-cheat limits and everyone uses your copy. Off = modded weapons are hidden."); y += 70;

            UI.Label(new Rect(body.x, y, body.width, 60),
                Settings.Data.restrictedNetwork
                    ? "School network mode: no port is opened and no admin password is needed. Players connect through free relay servers (secure web traffic). You get a server code to share (Esc > Players). Keep the game open while others play."
                    : "You are the server: keep the game open while others play. Friends on the same Wi-Fi can always join, and anyone can join with your server code (Esc > Players). If Windows asks for an admin password, cancel and turn on School / work network mode below instead.",
                15, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);

            if (UI.Button(new Rect(body.xMax - 340, body.yMax - 70, 340, 70), "HOST SERVER", ButtonKind.Primary, IconFactory.Get("play"), 26, mapNames.Length > 0 && !string.IsNullOrWhiteSpace(host.Name)))
            {
                Settings.Save();
                host.MaxPlayers = Mathf.RoundToInt(maxPlayers);
                host.Public = visibility == 0;
                host.Loadout = loadout;
                var map = maps[mapIndex];
                var options = new MapOptions { Time = (TimeOfDay)timeIndex, StartingNpcs = Mathf.RoundToInt(npcs), Loadout = 0, Nextbots = nextbots, AiIgnore = false };
                if (!NetManager.I.StartHost(host, map, options)) UIRoot.Push(new MessageDialog("COULDN'T HOST", NetManager.I.Status));
            }
        }

        // ------------------------------------------------------------------ direct

        void DrawDirect(Rect body)
        {
            float y = body.y;
            UI.Label(new Rect(body.x, y, body.width, 40), "Type the host's 8-letter server code (they see it in Esc > Players). Codes work on any network, including school Wi-Fi." + (Settings.Data.restrictedNetwork ? "" : " You can also type an IP:port."), 18, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true); y += 54;
            direct = UI.TextField(new Rect(body.x, y, 560, 56), direct, "mp_direct", false, Settings.Data.restrictedNetwork ? "Server code, e.g. K7P2QX9M" : "Server code (K7P2QX9M) or IP:port"); y += 70;
            directPassword = UI.TextField(new Rect(body.x, y, 560, 56), directPassword, "mp_dpass", true, "Password (if the server has one)"); y += 80;
            if (UI.Button(new Rect(body.x, y, 300, 64), "CONNECT", ButtonKind.Primary, IconFactory.Get("play"), 24, direct.Trim().Length > 0)) Connect(null, direct, directPassword);

            // Network options (right column)
            float x2 = body.x + 620, w2 = body.width - 620;
            float y2 = body.y + 54;
            UI.Label(new Rect(x2, y2, w2, 30), "<b>Network options</b> (only if connecting doesn't work)", 17, UI.Accent2); y2 += 36;
            proxyEdit ??= Settings.Data.netProxy ?? "";
            relayEdit ??= Settings.Data.customRelay ?? "";
            UI.Label(new Rect(x2, y2, w2, 26), "Web proxy (some schools need one): host:port", 15, UI.Muted); y2 += 28;
            proxyEdit = UI.TextField(new Rect(x2, y2, w2, 50), proxyEdit, "mp_proxy", false, "automatic"); y2 += 62;
            UI.Label(new Rect(x2, y2, w2, 26), "Own relay server (everyone must use the same one)", 15, UI.Muted); y2 += 28;
            relayEdit = UI.TextField(new Rect(x2, y2, w2, 50), relayEdit, "mp_relay", false, "built-in free relays (e.g. wss://my.broker/mqtt)"); y2 += 62;
            bool dirty = proxyEdit.Trim() != (Settings.Data.netProxy ?? "") || relayEdit.Trim() != (Settings.Data.customRelay ?? "");
            if (UI.Button(new Rect(x2, y2, 220, 52), "APPLY", ButtonKind.Normal, IconFactory.Get("check"), 18, dirty))
            {
                Settings.Data.netProxy = proxyEdit.Trim();
                Settings.Data.customRelay = relayEdit.Trim();
                Settings.Save();
                NetManager.I.NetworkSettingsChanged();
            }
            y2 += 64;
            var lb = NetManager.I.Lobby;
            if (lb != null)
            {
                var sb = new System.Text.StringBuilder("Relays: ");
                foreach (var b in lb.Brokers) sb.Append(b.Name).Append(b.Connected ? " <color=#5ad95f>online</color>   " : " <color=#999999>" + b.Status + "</color>   ");
                UI.Label(new Rect(x2, y2, w2, 90), sb.ToString(), 14, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            }

            y += 100;
            if (!Settings.Data.restrictedNetwork)
                UI.Label(new Rect(body.x, y, 580, 80), "Same network? Your LAN address: " + AddressUtil.BestLanAddress() + ":" + NetManager.DefaultPort, 17, UI.Accent2, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
        }

        static void Connect(ServerInfo sv, string address, string password)
        {
            Settings.Save();
            var screen = new ConnectingScreen(sv != null ? sv.Name : address);
            UIRoot.Push(screen);
            System.Action ok = () => { UIRoot.Remove(screen); };
            System.Action<string> fail = reason => { UIRoot.Remove(screen); NetManager.I.StartBrowsing(); UIRoot.Push(new MessageDialog("COULDN'T JOIN", reason)); };
            if (sv != null) NetManager.I.Join(sv, password, ok, fail);
            else NetManager.I.JoinDirect(address, password, ok, fail);
        }
    }

    public sealed class ConnectingScreen : UIScreen
    {
        readonly string target;
        public ConnectingScreen(string target) { this.target = target; }
        public override bool IsModal => true;

        public override bool OnBack()
        {
            NetManager.I.Shutdown("cancelled");
            NetManager.I.StartBrowsing();
            UIRoot.Pop();
            return true;
        }

        public override void Draw()
        {
            UI.Dim(0.65f);
            var r = UI.Centered(640, 300);
            UI.Panel(r, "Connecting");
            UI.Label(new Rect(r.x + 30, r.y + 80, r.width - 60, 40), target, 24, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UI.Label(new Rect(r.x + 30, r.y + 124, r.width - 60, 40), NetManager.I.Status, 18, UI.Muted);
            var spin = new Rect(r.xMax - 90, r.y + 90, 50, 50);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Time.unscaledTime * 360f, spin.center * UI.Scale);
            UI.Tex(spin, IconFactory.Get("refresh"), UI.Accent2);
            GUI.matrix = m;
            if (UI.Button(new Rect(r.x + 30, r.yMax - 86, 220, 60), "CANCEL", ButtonKind.Normal)) OnBack();
        }
    }

    public sealed class PasswordDialog : UIScreen
    {
        readonly System.Action<string> done;
        string pw = "";
        public PasswordDialog(System.Action<string> done) { this.done = done; }
        public override bool IsModal => true;

        public override void Draw()
        {
            UI.Dim(0.6f);
            var r = UI.Centered(600, 300);
            UI.Panel(r, "Password");
            pw = UI.TextField(new Rect(r.x + 30, r.y + 96, r.width - 60, 56), pw, "pw_dialog", true, "Server password");
            var e = Event.current;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            if (UI.Button(new Rect(r.x + 30, r.yMax - 86, 220, 60), "CANCEL", ButtonKind.Normal)) UIRoot.Pop();
            if (UI.Button(new Rect(r.xMax - 250, r.yMax - 86, 220, 60), "JOIN", ButtonKind.Primary) || enter) { UIRoot.Pop(); done(pw); }
        }
    }

    /// <summary>
    /// T: type a chat message (Enter sends, Esc cancels). Multiplayer: text chat with everyone. Singleplayer: a
    /// local chat for testing. In both, "/command" runs a dev-console command and shows its output in the chat.
    /// </summary>
    public sealed class ChatScreen : UIScreen
    {
        public static void Submit(string text)
        {
            var nm = NetManager.I;
            text = (text ?? "").Trim();
            if (nm == null || text.Length == 0) return;
            if (text.Length > 160) text = text.Substring(0, 160);
            if (text.StartsWith("/") && text.Length > 1)
            {
                nm.AddChat("> " + text, UI.Muted);
                int lines = 0;
                void Capture(string msg, string stack, LogType type)
                {
                    if (lines >= 6 || string.IsNullOrEmpty(msg) || msg.StartsWith("> ")) return;
                    foreach (var line in msg.Split('\n'))
                    {
                        if (lines++ >= 6) break;
                        nm.AddChat(line, type == LogType.Error || type == LogType.Exception ? UI.Bad : type == LogType.Warning ? UI.Warn : UI.Accent2);
                    }
                }
                Application.logMessageReceived += Capture;
                try { DevConsole.Execute(text.Substring(1)); }
                finally { Application.logMessageReceived -= Capture; }
                return;
            }
            if (NetState.Active) nm.SendChat(text);
            else nm.AddChat(nm.LocalName + ": " + text, Color.white);
        }

        string text = "";
        bool focused;
        public override bool IsModal => true;
        public override bool ShowCursor => false;

        public override void Draw()
        {
            var r = new Rect(30, UI.VH - 230, 760, 52);
            UI.Rounded(new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 12), new Color(0, 0, 0, 0.6f), true);
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                Submit(text);
                UIRoot.Remove(this);
                e.Use();
                return;
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { UIRoot.Remove(this); e.Use(); return; }
            text = UI.TextField(r, text, "chat_input", false, NetState.Active ? "Say something... (Enter to send, /command to run a command)" : "Type a message or a /command (e.g. /help, /god)");
            if (text.Length > 160) text = text.Substring(0, 160);
            if (!focused && e.type == EventType.Repaint) { GUI.FocusControl("chat_input"); focused = true; }
        }
    }
}
