using System.Collections.Generic;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Rockets, grenades and C4 over the network (Msg 140-143). See <see cref="Explosives"/> for the model:
    /// the host simulates the real projectiles, clients only show visual copies.
    /// </summary>
    public sealed partial class NetManager
    {
        readonly Dictionary<byte, float> lastThrowable = new Dictionary<byte, float>();

        // ---- client → host

        public void RequestThrowable(ThrowableKind kind, Vector3 pos, Vector3 vel, float fuse)
        {
            w.Reset(); w.Byte((byte)kind); w.Vec3(pos); w.Vec3(vel); w.Float(fuse); ToHost(Msg.ReqThrowable);
        }

        public void RequestDetonate()
        {
            w.Reset(); w.Byte(0); ToHost(Msg.ReqDetonate);
        }

        // ---- host → clients

        public void BroadcastThrowable(byte owner, ThrowableKind kind, Vector3 pos, Vector3 vel, float fuse, NetPlayer except)
        {
            if (!InGame || !HasClients) return;
            w.Reset(); w.Byte(owner); w.Byte((byte)kind); w.Vec3(pos); w.Vec3(vel); w.Float(fuse);
            ToClients(Msg.ThrowableFx, true, except);
        }

        public void BroadcastDetonate(byte owner)
        {
            if (!InGame || !HasClients) return;
            w.Reset(); w.Byte(owner); ToClients(Msg.C4Detonated);
        }

        // ---- handlers (hooked from the switches in NetManager.Handlers.cs)

        void HostExplosivesMsg(NetPlayer p, Msg type, NetReader rd)
        {
            if (type == Msg.ReqThrowable)
            {
                var kind = (ThrowableKind)rd.Byte(); var pos = rd.Vec3(); var vel = rd.Vec3(); float fuse = rd.Float();
                if ((byte)kind > (byte)ThrowableKind.C4) return;
                // Rate limit (single shot weapons; generous for grenade spam) and sanity checks.
                if (lastThrowable.TryGetValue(p.Id, out var last) && Time.time - last < 0.2f) return;
                lastThrowable[p.Id] = Time.time;
                if (float.IsNaN(pos.x + pos.y + pos.z + vel.x + vel.y + vel.z + fuse)) return;
                if (p.Avatar != null && Vector3.Distance(pos, p.Pos) > 5f) pos = p.Pos + Vector3.up * 1.6f;
                vel = Vector3.ClampMagnitude(vel, 70f);
                fuse = Mathf.Clamp(fuse, 0.02f, Explosives.FragFuse);
                Explosives.Spawn(kind, pos, vel, fuse, SourceFor(p), p.Id, false);
                if (kind == ThrowableKind.Rocket) AudioManager.Play("rocket_fire", pos, 1f, Random.Range(0.96f, 1.04f), 1f, 3f, 200f);
                BroadcastThrowable(p.Id, kind, pos, vel, fuse, p);
            }
            else if (type == Msg.ReqDetonate)
            {
                rd.Byte();
                Explosives.DetonateOwner(p.Id);
            }
        }

        void ClientExplosivesMsg(Msg type, NetReader rd)
        {
            if (type == Msg.ThrowableFx)
            {
                byte owner = rd.Byte(); var kind = (ThrowableKind)rd.Byte(); var pos = rd.Vec3(); var vel = rd.Vec3(); float fuse = rd.Float();
                if ((byte)kind > (byte)ThrowableKind.C4) return;
                Explosives.Spawn(kind, pos, vel, fuse, null, owner, true);
                if (kind == ThrowableKind.Rocket) AudioManager.Play("rocket_fire", pos, 1f, Random.Range(0.96f, 1.04f), 1f, 3f, 200f);
            }
            else if (type == Msg.C4Detonated)
            {
                C4Charge.TriggerAll(rd.Byte());
            }
        }
    }
}
