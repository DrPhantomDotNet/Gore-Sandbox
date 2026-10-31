using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Client -> host Open World requests (ReqOW). Every request carries the same fields.</summary>
    public enum OWReq : byte
    {
        HeistJoin = 1, HeistLeave = 2, HeistReady = 3, HeistUse = 4, CallCar = 5, Drive = 6, ExitCar = 7, Emote = 8,
        Text = 9, Money = 10, Call = 11, Bounty = 12, Crime = 13, Delivery = 14,
    }

    /// <summary>Host -> client Open World events (OWEvent).</summary>
    public enum OWEv : byte
    {
        Reward = 1, Text = 2, Money = 3, Call = 4, Emote = 5, CarDriver = 6, Toast = 7, CarWreck = 8, Heist = 9,
    }

    /// <summary>
    /// Open World over the network (Msg 230-241). Requests and events all share one simple layout:
    /// kind byte, player byte, string, long, Vector3, ushort (entity id).
    ///   host -> clients OWState  (230): heist lobby/session + wanted levels + bounty (1 per second and on change).
    ///   host -> client  OWEvent  (231): rewards, texts, money, calls, emotes, car drivers, toasts.
    ///   client -> host  ReqOW    (240): heist join/ready/use, call/drive/exit a car, emote, text, money, call, crimes.
    ///   client -> host  CarInput (241): the car you drive (unreliable, ~20 per second).
    /// Singleplayer runs the same code with no network: requests are handled immediately.
    /// </summary>
    public sealed partial class NetManager
    {
        // ------------------------------------------------------------------ client -> host

        public void OWRequest(OWReq kind, string s = "", long n = 0, Vector3 v = default, ushort e = 0, byte target = 255)
        {
            if (!NetState.IsClient) { OWDirector.HostRequest(OWDirector.MyId, kind, target, s ?? "", n, v, e); return; }
            w.Reset();
            w.Byte((byte)kind); w.Byte(target); w.String(s ?? ""); w.Long(n); w.Vec3(v); w.UShort(e);
            ToHost(Msg.ReqOW);
        }

        public void OWCarInput(ushort entity, float throttle, float steer, bool brake, bool boost)
        {
            if (!NetState.IsClient) return;
            w.Reset();
            w.UShort(entity);
            w.SByte((sbyte)Mathf.RoundToInt(Mathf.Clamp(throttle, -1f, 1f) * 100f));
            w.SByte((sbyte)Mathf.RoundToInt(Mathf.Clamp(steer, -1f, 1f) * 100f));
            w.Byte((byte)((brake ? 1 : 0) | (boost ? 2 : 0)));
            ToHost(Msg.CarInput, false);
        }

        // ------------------------------------------------------------------ host -> clients

        /// <summary>Host: send an event to one player (255 = everyone). Events for the host itself run directly.</summary>
        public void OWEvent(byte to, OWEv kind, byte from, string s = "", long n = 0, Vector3 v = default, ushort e = 0)
        {
            bool mine = to == 255 || to == OWDirector.MyId;
            if (mine) OWDirector.OnEvent(kind, from, s ?? "", n, v, e);
            if (!NetState.IsHost) return;
            if (to != 255 && to == OWDirector.MyId) return;
            w.Reset();
            w.Byte((byte)kind); w.Byte(from); w.String(s ?? ""); w.Long(n); w.Vec3(v); w.UShort(e);
            if (to == 255) ToClients(Msg.OWEvent);
            else if (Players.TryGetValue(to, out var p) && p.Conn != null) ToPlayer(p, Msg.OWEvent);
        }

        public void OWBroadcastState()
        {
            if (!NetState.IsHost || !HasClients) return;
            w.Reset();
            OWDirector.WriteState(w);
            ToClients(Msg.OWState);
        }

        /// <summary>Host: does this player exist (or is it us / singleplayer)?</summary>
        public bool OWHasPlayer(byte id) => id == OWDirector.MyId || (NetState.Active && Players.ContainsKey(id));

        public string OWName(byte id)
        {
            if (id == OWDirector.MyId) return LocalName;
            return Players.TryGetValue(id, out var p) ? p.Name : "Player " + id;
        }

        /// <summary>Position of a player (you or someone else).</summary>
        public bool OWPos(byte id, out Vector3 pos)
        {
            if (id == OWDirector.MyId && PlayerController.Instance != null) { pos = PlayerController.Instance.transform.position; return true; }
            if (Players.TryGetValue(id, out var p)) { pos = p.Avatar != null ? p.Avatar.transform.position : p.Pos; return true; }
            pos = Vector3.zero;
            return false;
        }

        // ------------------------------------------------------------------ dispatch

        void ClientOWMsg(Msg type, NetReader rd)
        {
            if (type == Msg.OWState) { OWDirector.ReadState(rd); return; }
            var kind = (OWEv)rd.Byte(); byte from = rd.Byte(); string s = rd.String(); long n = rd.Long(); var v = rd.Vec3(); ushort e = rd.UShort();
            OWDirector.OnEvent(kind, from, s, n, v, e);
        }

        void HostOWMsg(NetPlayer p, Msg type, NetReader rd)
        {
            if (type == Msg.CarInput)
            {
                ushort id = rd.UShort(); float th = rd.SByte() / 100f, st = rd.SByte() / 100f; byte fl = rd.Byte();
                var ne = NetEntity.Get(id);
                var car = ne != null ? ne.GetComponent<OWCar>() : null;
                if (car != null && car.DriverId == p.Id) car.SetInput(th, st, (fl & 1) != 0, (fl & 2) != 0);
                return;
            }
            var kind = (OWReq)rd.Byte(); byte target = rd.Byte(); string s = rd.String(); long n = rd.Long(); var v = rd.Vec3(); ushort e = rd.UShort();
            if (s.Length > 200) s = s.Substring(0, 200);
            OWDirector.HostRequest(p.Id, kind, target, s, n, v, e);
        }
    }
}
