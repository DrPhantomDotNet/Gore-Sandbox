using System.Collections.Generic;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Player profiles (icon, hat, look, badge certificates) over the network.
    ///   client -> host  ProfileUpdate (220): own profile, after ClientReady and whenever it changes.
    ///   host -> clients ProfileData   (221): player id + profile (to everyone on change, to newcomers on ClientReady).
    ///   host -> client  BadgeGrant    (222): a signed badge certificate for that player to store.
    ///   client -> host  ReqBadgeGrant (223): target id + certificate (a dev on a client granting someone; the host
    ///                                        only forwards certificates that verify and match the target).
    /// The host replaces each client's certificate subject with the hash of the id it joined with, so a client
    /// can't present someone else's badges. Receivers verify every certificate again before showing it.
    /// </summary>
    public sealed partial class NetManager
    {
        sealed class ProfileEntry { public NetPlayer Owner; public ProfileData Data; }
        readonly Dictionary<byte, ProfileEntry> profiles = new Dictionary<byte, ProfileEntry>();

        /// <summary>The profile of a player (yours is the local one). Null until it arrives.</summary>
        public ProfileData ProfileOf(NetPlayer p)
        {
            if (p == null) return null;
            if (p.Id == LocalId || (!NetState.Active && p.Id == 0)) return PlayerProfile.Local;
            if (!profiles.TryGetValue(p.Id, out var e)) return null;
            if (e.Owner == null) e.Owner = p;
            return e.Owner == p ? e.Data : null;
        }

        void StoreProfile(byte id, ProfileData data)
        {
            Players.TryGetValue(id, out var owner);
            profiles[id] = new ProfileEntry { Owner = owner, Data = data };
            if (profiles.Count > 64)
                foreach (var k in new List<byte>(profiles.Keys)) if (!Players.ContainsKey(k)) profiles.Remove(k);
        }

        /// <summary>Called by PlayerProfile.Save: tell the server (or, as host, everyone).</summary>
        public void OnLocalProfileChanged()
        {
            if (NetState.IsClient) SendMyProfile();
            else if (NetState.IsHost) BroadcastProfile(LocalId, PlayerProfile.Local);
        }

        void SendMyProfile()
        {
            if (!NetState.IsClient) return;
            w.Reset();
            PlayerProfile.Local.Write(w);
            ToHost(Msg.ProfileUpdate);
        }

        void BroadcastProfile(byte id, ProfileData data)
        {
            if (!NetState.IsHost || data == null) return;
            w.Reset(); w.Byte(id); data.Write(w);
            ToClients(Msg.ProfileData);
        }

        void SendProfilesTo(NetPlayer target)
        {
            if (!NetState.IsHost || target == null) return;
            foreach (var p in Players.Values)
            {
                if (p == target) continue;
                var data = ProfileOf(p);
                if (data == null) continue;
                w.Reset(); w.Byte(p.Id); data.Write(w);
                ToPlayer(target, Msg.ProfileData);
            }
        }

        void HostProfileMsg(NetPlayer p, Msg type, NetReader rd)
        {
            if (type == Msg.ProfileUpdate)
            {
                var data = ProfileData.Read(rd);
                if (data == null) return;
                data.Name = p.Name;
                data.Subject = Badges.SubjectFor(p.Guid); // bind badges to the id this client joined with
                data.Certs.RemoveAll(c => { var cert = Badges.Parse(c); return cert == null || !BadgeCrypto.Same(cert.Subject, data.Subject) || !Badges.Verify(c); });
                data.Changed();
                StoreProfile(p.Id, data);
                BroadcastProfile(p.Id, data);
                return;
            }
            if (type == Msg.ReqBadgeGrant)
            {
                byte target = rd.Byte();
                var raw = rd.BlockWithLength();
                if (rd.Overflow) return;
                ForwardGrant(target, raw, p.Name);
            }
        }

        /// <summary>Host: deliver a verified certificate to its player (or keep it, when it's for the host).</summary>
        void ForwardGrant(byte target, byte[] raw, string from)
        {
            var cert = Badges.Parse(raw);
            if (cert == null || !Badges.Verify(raw)) { Log("Ignored an invalid badge certificate from " + from); return; }
            if (target == LocalId) { ReceiveGrant(raw); return; }
            var tp = Get(target);
            if (tp == null || !BadgeCrypto.Same(cert.Subject, Badges.SubjectFor(tp.Guid))) return;
            w.Reset(); w.BlockWithLength(raw, 0, raw.Length);
            ToPlayer(tp, Msg.BadgeGrant);
        }

        void ClientProfileMsg(Msg type, NetReader rd)
        {
            if (type == Msg.ProfileData)
            {
                byte id = rd.Byte();
                var data = ProfileData.Read(rd);
                if (data == null || id == LocalId) return;
                StoreProfile(id, data);
                return;
            }
            if (type == Msg.BadgeGrant)
            {
                var raw = rd.BlockWithLength();
                if (!rd.Overflow) ReceiveGrant(raw);
            }
        }

        void ReceiveGrant(byte[] raw)
        {
            if (PlayerProfile.AddCert(raw, out var cert) && cert != null)
            {
                string msg = cert.Grant ? "You received the " + Badges.Names[(int)cert.Badge] + " badge!" : "Your " + Badges.Names[(int)cert.Badge] + " badge was removed.";
                GameEvents.ShowToast(msg);
                AddChat(msg, Badges.Colors[(int)cert.Badge]);
            }
        }

        /// <summary>Dev panel: sends a signed certificate to a player (any role; the host checks and forwards it).</summary>
        public bool SendBadgeGrant(NetPlayer target, byte[] raw)
        {
            if (target == null || raw == null) return false;
            if (target.Id == LocalId) { ReceiveGrant(raw); return true; }
            if (NetState.IsHost) { ForwardGrant(target.Id, raw, LocalName); return true; }
            if (NetState.IsClient) { w.Reset(); w.Byte(target.Id); w.BlockWithLength(raw, 0, raw.Length); ToHost(Msg.ReqBadgeGrant); return true; }
            return false;
        }
    }
}
