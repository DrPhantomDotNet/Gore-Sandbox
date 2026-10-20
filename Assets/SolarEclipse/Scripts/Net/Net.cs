using System;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    public enum NetRole { None, Host, Client }

    /// <summary>
    /// Quick questions the rest of the game asks about multiplayer. Offline, everything is "authoritative" and
    /// "creative", so single player is unchanged.
    /// </summary>
    public static class NetState
    {
        public static NetRole Role = NetRole.None;
        public static bool Active => Role != NetRole.None;
        public static bool IsHost => Role == NetRole.Host;
        public static bool IsClient => Role == NetRole.Client;
        /// <summary>True when this machine owns the simulation (offline or host).</summary>
        public static bool Authority => Role != NetRole.Client;

        /// <summary>Set while a client is replaying something the host told it to do.</summary>
        public static bool ApplyingRemote;
        /// <summary>Set while the local player's own input is being processed (weapons, kicks, use, spawn menu, console).</summary>
        public static bool LocalAction;

        /// <summary>Creative mode (noclip, spawn gun, admin gun, spawn menu, cheats). Always on offline and for the host.</summary>
        public static bool LocalCreative => Role != NetRole.Client || (NetManager.I != null && NetManager.I.LocalCreative);

        /// <summary>
        /// A client should forward this action to the host instead of doing it locally. Returns true when the caller
        /// must stop (the request was sent, or the action belongs to the host's simulation and is ignored here).
        /// </summary>
        public static bool ClientIntercepts => Role == NetRole.Client && !ApplyingRemote;

        public static LocalScope Local() => new LocalScope(true);
        public static RemoteScope Remote() => new RemoteScope(true);

        public readonly struct LocalScope : IDisposable
        {
            readonly bool prev;
            public LocalScope(bool on) { prev = LocalAction; LocalAction = on; }
            public void Dispose() => LocalAction = prev;
        }

        public readonly struct RemoteScope : IDisposable
        {
            readonly bool prev;
            public RemoteScope(bool on) { prev = ApplyingRemote; ApplyingRemote = on; }
            public void Dispose() => ApplyingRemote = prev;
        }
    }

    /// <summary>Unity type helpers for the binary protocol.</summary>
    public static class NetIO
    {
        public static void Vec3(this NetWriter w, Vector3 v) { w.Float(v.x); w.Float(v.y); w.Float(v.z); }
        public static Vector3 Vec3(this NetReader r) => new Vector3(r.Float(), r.Float(), r.Float());

        public static void Quat(this NetWriter w, Quaternion q)
        {
            if (q.w < 0) { q.x = -q.x; q.y = -q.y; q.z = -q.z; q.w = -q.w; }
            w.Short((short)(Mathf.Clamp(q.x, -1f, 1f) * 32767f)); w.Short((short)(Mathf.Clamp(q.y, -1f, 1f) * 32767f));
            w.Short((short)(Mathf.Clamp(q.z, -1f, 1f) * 32767f)); w.Short((short)(Mathf.Clamp(q.w, -1f, 1f) * 32767f));
        }

        public static Quaternion Quat(this NetReader r)
        {
            var q = new Quaternion(r.Short() / 32767f, r.Short() / 32767f, r.Short() / 32767f, r.Short() / 32767f);
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (m < 0.0001f) return Quaternion.identity;
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

        /// <summary>Position relative to a base in millimetres (±32 m).</summary>
        public static void RelPos(this NetWriter w, Vector3 v)
        {
            w.Short((short)Mathf.Clamp(Mathf.RoundToInt(v.x * 1000f), -32767, 32767));
            w.Short((short)Mathf.Clamp(Mathf.RoundToInt(v.y * 1000f), -32767, 32767));
            w.Short((short)Mathf.Clamp(Mathf.RoundToInt(v.z * 1000f), -32767, 32767));
        }

        public static Vector3 RelPos(this NetReader r) => new Vector3(r.Short() / 1000f, r.Short() / 1000f, r.Short() / 1000f);

        public static void Damage(this NetWriter w, DamageInfo d)
        {
            w.Float(d.Amount); w.Vec3(d.Force); w.Vec3(d.Point); w.Vec3(d.Normal); w.Vec3(d.Direction);
            w.Byte((byte)d.Type); w.Float(d.Caliber); w.Float(d.Penetration); w.Bool(d.IsExit); w.String(d.Weapon ?? "");
        }

        public static DamageInfo Damage(this NetReader r) => new DamageInfo
        {
            Amount = r.Float(), Force = r.Vec3(), Point = r.Vec3(), Normal = r.Vec3(), Direction = r.Vec3(),
            Type = (DamageType)r.Byte(), Caliber = r.Float(), Penetration = r.Float(), IsExit = r.Bool(), Weapon = r.String(),
        };

        /// <summary>Damage with points/directions expressed in a transform's local space (for exact wound placement on proxies).</summary>
        public static DamageInfo ToLocal(DamageInfo d, Transform t)
        {
            d.Point = t.InverseTransformPoint(d.Point); d.Normal = t.InverseTransformDirection(d.Normal);
            d.Direction = t.InverseTransformDirection(d.Direction); d.Force = t.InverseTransformDirection(d.Force);
            return d;
        }

        public static DamageInfo ToWorld(DamageInfo d, Transform t)
        {
            d.Point = t.TransformPoint(d.Point); d.Normal = t.TransformDirection(d.Normal);
            d.Direction = t.TransformDirection(d.Direction); d.Force = t.TransformDirection(d.Force);
            return d;
        }
    }
}
