using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Multiplayer player list. Host: grant/remove Creative mode, kick, ban, teleport, server options and the
    /// ban list. Everyone: ping, who's talking, who has Creative, and local voice mute.
    /// </summary>
    public static class PlayersPanel
    {
        static bool showBans;

        public static void Draw(Rect view)
        {
            var nm = NetManager.I;
            if (nm == null || !NetState.Active) { UI.Label(view, "Not in a multiplayer game.", 20, UI.Muted, TextAnchor.UpperLeft); return; }
            bool host = NetState.IsHost;
            float y = view.y;
            if (host)
            {
                var s = nm.Server;
                UI.Toggle(new Rect(view.x, y, view.width * 0.5f - 8, 50), "PvP damage", ref s.PvP);
                UI.Toggle(new Rect(view.x + view.width * 0.5f + 8, y, view.width * 0.5f - 8, 50), "Creative for new players", ref s.CreativeForAll);
                y += 58;
                UI.Label(new Rect(view.x, y, view.width, 30), "<b>SERVER CODE:  <color=#ffb040>" + nm.ServerCode + "</color></b>   <size=14>(friends: Multiplayer > Join by code. Works on any network)</size>", 20, Color.white);
                y += 34;
                string addr = nm.HasUdp
                    ? (nm.PublicEndpoint != null ? "Online: " + nm.PublicEndpoint + "   ·   " : "") + "LAN: " + Net.AddressUtil.BestLanAddress() + ":" + nm.Port
                    : "School network mode: no ports opened, everyone connects through the relay";
                UI.Label(new Rect(view.x, y, view.width, 26), addr + "   ·   " + nm.Players.Count + "/" + s.MaxPlayers + " players", 15, UI.Muted);
                y += 34;
                if (UI.Button(new Rect(view.x, y, 260, 44), showBans ? "SHOW PLAYERS" : "BANNED (" + Bans.List.Count + ")", ButtonKind.Ghost, IconFactory.Get(showBans ? "users" : "lock"), 16)) showBans = !showBans;
                y += 54;
            }
            var list = new Rect(view.x, y, view.width, view.yMax - y);
            if (host && showBans) { DrawBans(list); return; }

            const float rowH = 62f;
            var players = new System.Collections.Generic.List<NetPlayer>(nm.Players.Values);
            players.Sort((a, b) => a.Id.CompareTo(b.Id));
            float content = players.Count * (rowH + 8);
            float sc = UI.BeginScroll("mp_players", list, content);
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                var r = new Rect(0, i * (rowH + 8) - sc, list.width - 16, rowH);
                if (r.yMax < 0 || r.y > list.height) continue;
                bool me = p.Id == nm.LocalId;
                UI.Rounded(r, me ? new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.25f) : UI.PanelLight, true);
                bool talking = VoiceChat.I != null && (me ? VoiceChat.I.Transmitting : VoiceChat.I.IsTalking(p.Id));
                UI.Tex(new Rect(r.x + 12, r.y + 15, 32, 32), IconFactory.Get(talking ? "speaker" : p.IsHost ? "crown" : "person"), talking ? UI.Good : p.IsHost ? UI.Warn : UI.Muted);
                UI.Label(new Rect(r.x + 56, r.y, 260, rowH), p.Name + (me ? " (you)" : ""), 19, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                string tags = (p.IsHost ? "HOST  " : "") + (p.Creative || p.IsHost ? "CREATIVE  " : "") + (p.IsHost ? "" : p.PingMs + " ms");
                UI.Label(new Rect(r.x + 320, r.y, 220, rowH), tags, 14, p.Creative || p.IsHost ? UI.Accent2 : UI.Muted);
                float bx = r.xMax - 8;
                if (!me && VoiceChat.I != null)
                {
                    bool muted = VoiceChat.I.Muted.Contains(p.Name);
                    bx -= 96; if (UI.Button(new Rect(bx, r.y + 9, 90, 44), muted ? "UNMUTE" : "MUTE", ButtonKind.Ghost, null, 14, true, "Mute this player's voice (only for you).")) VoiceChat.I.ToggleMute(p);
                }
                if (host && !me && !p.IsHost)
                {
                    bx -= 76; if (UI.Button(new Rect(bx, r.y + 9, 70, 44), "BAN", ButtonKind.Danger, null, 14)) { var pp = p; UIRoot.Push(new ConfirmDialog("Ban " + p.Name + "?", "They're kicked and can't rejoin your servers (by player id and IP). Unban from the BANNED list.", () => NetManager.I.Ban(pp), "BAN", ButtonKind.Danger)); }
                    bx -= 82; if (UI.Button(new Rect(bx, r.y + 9, 76, 44), "KICK", ButtonKind.Danger, null, 14)) NetManager.I.Kick(p);
                    bx -= 70; if (UI.Button(new Rect(bx, r.y + 9, 64, 44), "TP", ButtonKind.Ghost, null, 14, true, "Teleport to this player.") && PlayerController.Instance != null) PlayerController.Instance.Teleport(p.Pos + Vector3.up * 0.2f + Vector3.back);
                    bool c = p.Creative;
                    bx -= 186;
                    if (UI.Button(new Rect(bx, r.y + 9, 180, 44), c ? "CREATIVE: ON" : "GIVE CREATIVE", c ? ButtonKind.Primary : ButtonKind.Normal, null, 14, true, "Creative mode: noclip, Spawn Gun, spawn menu, Admin Gun.")) NetManager.I.SetCreative(p, !c);
                }
            }
            UI.EndScroll("mp_players", list, content);
        }

        static void DrawBans(Rect list)
        {
            if (Bans.List.Count == 0) { UI.Label(list, "Nobody is banned.", 20, UI.Muted, TextAnchor.UpperLeft); return; }
            float content = Bans.List.Count * 62f;
            float sc = UI.BeginScroll("mp_bans", list, content);
            for (int i = 0; i < Bans.List.Count; i++)
            {
                var b = Bans.List[i];
                var r = new Rect(0, i * 62f - sc, list.width - 16, 54);
                UI.Rounded(r, UI.PanelLight, true);
                UI.Label(new Rect(r.x + 16, r.y, r.width - 200, r.height), b.Name + "   ·   " + b.Ip + "   ·   banned " + b.Date, 17, Color.white);
                if (UI.Button(new Rect(r.xMax - 150, r.y + 6, 140, 42), "UNBAN", ButtonKind.Normal, null, 15)) { Bans.Remove(b); break; }
            }
            UI.EndScroll("mp_bans", list, content);
        }
    }
}
