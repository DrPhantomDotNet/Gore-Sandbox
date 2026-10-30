using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Dev panel BADGES tab (and the BADGES dialog from the players list): grant or remove Developer / Admin /
    /// Tester / Early Access for yourself or any player in the session. Grants are signed certificates
    /// (Core/Badges.cs); the signing key is unlocked in memory by the dev login password.
    /// </summary>
    public static class DevBadges
    {
        static string unlockPass = "";

        static bool Touch => Settings.TouchControlsActive || Application.isMobilePlatform;

        /// <summary>Players to show: you first, then everyone else in the session.</summary>
        static List<NetPlayer> Others()
        {
            var list = new List<NetPlayer>();
            var nm = NetManager.I;
            if (nm == null || !NetState.Active) return list;
            foreach (var p in nm.Players.Values) if (p.Id != nm.LocalId) list.Add(p);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        /// <summary>Signs and delivers a grant (on = true) or removal. target null = yourself.</summary>
        public static void Set(NetPlayer target, BadgeId badge, bool on)
        {
            if (!Badges.CanSign) { GameEvents.ShowToast("Badge key is locked"); return; }
            var nm = NetManager.I;
            bool self = target == null || (nm != null && target.Id == nm.LocalId && NetState.Active);
            var prof = self ? PlayerProfile.Local : nm != null ? nm.ProfileOf(target) : null;
            if (prof == null) { GameEvents.ShowToast("That player's profile hasn't arrived yet."); AudioManager.Ui("ui_error"); return; }
            var raw = Badges.Issue(prof.Subject, badge, on);
            if (raw == null) { GameEvents.ShowToast("Could not sign the badge."); return; }
            string who = self ? "you" : target.Name;
            if (self)
            {
                if (nm != null && NetState.Active) nm.SendBadgeGrant(nm.Local, raw);
                else if (PlayerProfile.AddCert(raw, out _)) GameEvents.ShowToast((on ? "Gave you " : "Removed your ") + Badges.Names[(int)badge] + " badge");
            }
            else
            {
                nm.SendBadgeGrant(target, raw);
                GameEvents.ShowToast((on ? "Sent " : "Removed ") + Badges.Names[(int)badge] + (on ? " to " : " from ") + who);
            }
            Debug.Log("[DevBadges] " + (on ? "Granted " : "Revoked ") + Badges.Names[(int)badge] + " " + (on ? "to " : "from ") + who + ".");
            AudioManager.Ui(on ? "ui_success" : "ui_click");
        }

        /// <summary>Key status line; returns false (and draws an unlock field) when signing isn't possible.</summary>
        static bool KeyStatus(Rect r)
        {
            if (Badges.CanSign) return true;
            if (Badges.Unlocking)
            {
                UI.Label(r, "Unlocking the badge signing key" + new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3), 18, UI.Warn);
                return false;
            }
            UI.Label(new Rect(r.x, r.y, r.width, 30), Badges.UnlockError ?? "The badge signing key is locked. Enter the dev password to unlock it (kept in memory only).", 16, Badges.UnlockError != null ? UI.Bad : UI.Muted);
            var f = new Rect(r.x, r.y + 36, Mathf.Min(420, r.width - 200), Touch ? 56 : 46);
            unlockPass = TouchKeys.Field(f, unlockPass, "dev_badge_pass", true, "password", out bool done);
            if ((MobileDev.Tap(new Rect(f.xMax + 10, f.y, 180, f.height), "UNLOCK", ButtonKind.Primary, 18) || done) && unlockPass.Length > 0)
            {
                Badges.Unlock(unlockPass);
                unlockPass = "";
            }
            return false;
        }

        public static void Draw(Rect body)
        {
            float y = body.y;
            float rowH = Touch ? 78f : 64f;
            UI.Label(new Rect(body.x, y, body.width, 30), "Badges are signed certificates: only this dev login can create them, and every game checks them. 'Player' is everyone's default.", 15, UI.Muted);
            y += 36;
            if (!KeyStatus(new Rect(body.x, y, body.width, 100))) return;

            // Quick: give yourself badges.
            var mine = PlayerProfile.Local.BadgeMask;
            float qx = body.x;
            UI.Label(new Rect(qx, y, 150, rowH - 14), "GIVE TO ME", 16, UI.Accent2, TextAnchor.MiddleLeft, FontStyle.Bold);
            qx += 150;
            float qw = Mathf.Min(230, (body.xMax - qx - 30) / 4f);
            foreach (var b in Badges.Grantable)
            {
                bool has = Badges.Has(mine, b);
                if (MobileDev.Tap(new Rect(qx, y, qw, rowH - 14), (has ? "REMOVE " : "") + Badges.Names[(int)b].ToUpperInvariant(), has ? ButtonKind.Danger : ButtonKind.Primary, Touch ? 16 : 14))
                    Set(null, b, !has);
                qx += qw + 10;
            }
            y += rowH + 4;

            var others = Others();
            var list = new Rect(body.x, y, body.width, body.yMax - y);
            int n = 1 + others.Count;
            float content = n * (rowH + 8) + (others.Count == 0 ? 60 : 0);
            float s = UI.BeginScroll("dev_badges", list, content);
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(0, i * (rowH + 8) - s, list.width - 16, rowH);
                if (r.yMax < 0 || r.y > list.height) continue;
                var p = i == 0 ? null : others[i - 1];
                Row(r, p);
            }
            if (others.Count == 0)
                UI.Label(new Rect(0, n * (rowH + 8) - s, list.width - 16, 50), NetState.Active ? "Nobody else is here yet." : "Singleplayer: only you. Host or join a multiplayer game to give badges to other players.", 16, UI.Muted, TextAnchor.MiddleLeft);
            UI.EndScroll("dev_badges", list, content);
        }

        /// <summary>One player: icon, name, current badges and a toggle per badge.</summary>
        static void Row(Rect r, NetPlayer p)
        {
            var nm = NetManager.I;
            bool self = p == null;
            var prof = self ? PlayerProfile.Local : nm != null ? nm.ProfileOf(p) : null;
            string name = self ? PlayerProfile.LocalName + " (you)" : p.Name;
            UI.Rounded(r, self ? UI.Accent.WithAlpha(0.22f) : UI.PanelLight, true);
            float toggles = Mathf.Min(r.width * 0.58f, 4 * 190f);
            BadgeChips.PlayerLine(new Rect(r.x + 10, r.y + 8, r.width - toggles - 30, r.height - 16), prof, name, Color.white, Touch ? 20 : 17);
            int mask = prof != null ? prof.BadgeMask : 0;
            float tw = (toggles - 30) / 4f, tx = r.xMax - toggles;
            foreach (var b in Badges.Grantable)
            {
                bool has = Badges.Has(mask, b);
                if (MobileDev.Tap(new Rect(tx, r.y + 8, tw, r.height - 16), Badges.Names[(int)b].ToUpperInvariant(), has ? ButtonKind.Primary : ButtonKind.Ghost, Touch ? 15 : 13, has, prof != null))
                    Set(p, b, !has);
                tx += tw + 10;
            }
        }
    }

    /// <summary>Badge toggles for one player, opened from the players list by a logged-in developer.</summary>
    public sealed class BadgeEditDialog : UIScreen
    {
        readonly NetPlayer target;
        public BadgeEditDialog(NetPlayer target) { this.target = target; }
        public override bool IsModal => true;
        public override bool BlocksGameplay => false;

        public override void Draw()
        {
            float a = Intro(0, 0.15f);
            UI.Dim(0.6f * a);
            var r = UI.Centered(Mathf.Min(760, UI.VW - 40), 560);
            UI.Panel(r, "Badges", a);
            var nm = NetManager.I;
            bool self = target == null || (nm != null && target.Id == nm.LocalId);
            var prof = self ? PlayerProfile.Local : nm != null ? nm.ProfileOf(target) : null;
            string name = self ? PlayerProfile.LocalName : target.Name;
            BadgeChips.PlayerLine(new Rect(r.x + 30, r.y + 80, r.width - 60, 56), prof, name, Color.white, 24);
            if (!DevPanel.LoggedIn || !Badges.CanSign)
            {
                UI.Label(new Rect(r.x + 30, r.y + 160, r.width - 60, 120), Badges.Unlocking ? "Unlocking the badge key..." : "Log in to the dev panel first (badges can only be given from there).", 20, UI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, true, true);
            }
            else if (prof == null)
            {
                UI.Label(new Rect(r.x + 30, r.y + 160, r.width - 60, 120), "Waiting for this player's profile...", 20, UI.Muted, TextAnchor.UpperLeft);
            }
            else
            {
                int mask = prof.BadgeMask;
                float bw = (r.width - 60 - 12) / 2f, y = r.y + 160;
                for (int i = 0; i < Badges.Grantable.Length; i++)
                {
                    var b = Badges.Grantable[i];
                    bool has = Badges.Has(mask, b);
                    var br = new Rect(r.x + 30 + (i % 2) * (bw + 12), y + (i / 2) * 84, bw, 72);
                    if (UI.Button(br, (has ? "ON:  " : "OFF:  ") + Badges.Names[(int)b].ToUpperInvariant(), has ? ButtonKind.Primary : ButtonKind.Normal, IconFactory.Get(Badges.Icons[(int)b]), 20, true, null, has))
                        DevBadges.Set(self ? null : target, b, !has);
                }
            }
            if (UI.Button(new Rect(r.xMax - 250, r.yMax - 86, 220, 62), "DONE", ButtonKind.Normal, null, 22)) UIRoot.Pop();
        }
    }
}
