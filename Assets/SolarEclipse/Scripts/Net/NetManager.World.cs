using System.Collections.Generic;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Client copy of a Goredoll: kinematic body parts that follow the host's snapshots smoothly.</summary>
    public sealed class DollProxy : MonoBehaviour
    {
        public Goredoll Doll;
        readonly Vector3[] pos = new Vector3[17];
        readonly Quaternion[] rot = new Quaternion[17];
        readonly bool[] has = new bool[17];

        public void SetTarget(PartType t, Vector3 p, Quaternion r)
        {
            int i = (int)t;
            pos[i] = p; rot[i] = r;
            if (!has[i] && Doll != null && Doll.Parts.TryGetValue(t, out var part) && part != null) part.transform.SetPositionAndRotation(p, r);
            has[i] = true;
        }

        void Update()
        {
            if (Doll == null) return;
            float k = 1f - Mathf.Exp(-20f * Time.deltaTime);
            foreach (var kv in Doll.Parts)
            {
                var part = kv.Value;
                int i = (int)kv.Key;
                if (part == null || !has[i] || part.Severed || part.Gibbed || part.Body == null || !part.Body.isKinematic) continue;
                var t = part.transform;
                if ((t.position - pos[i]).sqrMagnitude > 4f) t.SetPositionAndRotation(pos[i], rot[i]);
                else t.SetPositionAndRotation(Vector3.Lerp(t.position, pos[i], k), Quaternion.Slerp(t.rotation, rot[i], k));
            }
        }
    }

    public sealed partial class NetManager
    {
        /// <summary>Player whose request is being executed on the host (for spawn ownership and damage credit).</summary>
        public NetPlayer CurrentRequester;
        int mapEntityCount;
        readonly NetWriter sw = new NetWriter(1400);
        readonly List<byte[]> snapPackets = new List<byte[]>();

        public object SourceFor(NetPlayer p) => p != null && p.Avatar != null ? (object)p.Avatar : null;

        // ================================================================== map

        public void OnMapBuilt(Transform world)
        {
            NetEntity.ClearAll();
            mapEntityCount = NetEntity.RegisterMap(world);
            if (NetState.IsClient)
                foreach (var e in NetEntity.All.Values) e.MakeProxy();
            Debug.Log("[Net] " + mapEntityCount + " networked map objects");
        }

        // ================================================================== spawn hooks (host)

        bool HostWorld => NetState.IsHost && WorldRoot.Current != null;

        public void OnDollSpawned(Goredoll d, int seed, string preset, int variant)
        {
            if (!HostWorld || d.transform.root != WorldRoot.Current.transform.root) return;
            var e = NetEntity.Register(d.gameObject, NetEntity.NextDynamicId++, NetKind.Doll);
            e.Seed = seed; e.DefId = preset + "|" + variant;
            e.Owner = CurrentRequester != null ? CurrentRequester.Id : LocalId;
            if (HasClients) { WriteSpawnDoll(w.Reset(), e, d); ToClients(Msg.SpawnDoll); }
        }

        void WriteSpawnDoll(NetWriter wr, NetEntity e, Goredoll d)
        {
            var parts = e.DefId.Split('|');
            var feet = d.Parts.TryGetValue(PartType.Pelvis, out var pel) ? pel.transform.position - Vector3.up * Anatomy.Parts[PartType.Pelvis].Pivot.y * d.Look.Height : d.transform.position;
            wr.UShort(e.Id); wr.Int(e.Seed); wr.String(parts[0]); wr.Int(parts.Length > 1 && int.TryParse(parts[1], out var v) ? v : -1);
            wr.Vec3(feet); wr.Float(d.transform.eulerAngles.y); wr.Byte(e.Owner);
        }

        public void OnPropSpawned(GameObject go, string defId)
        {
            if (!HostWorld) return;
            var e = NetEntity.Register(go, NetEntity.NextDynamicId++, NetKind.Prop);
            e.DefId = defId;
            e.Owner = CurrentRequester != null ? CurrentRequester.Id : LocalId;
            if (HasClients) { WriteSpawnProp(w.Reset(), e); ToClients(Msg.SpawnProp); }
        }

        void WriteSpawnProp(NetWriter wr, NetEntity e)
        {
            wr.UShort(e.Id); wr.String(e.DefId); wr.Vec3(e.transform.position); wr.Quat(e.transform.rotation);
            wr.Float(e.transform.localScale.x); wr.Bool(e.Frozen); wr.Byte(e.Owner);
        }

        public void OnNextbotSpawned(Nextbot nb, string defId)
        {
            if (!HostWorld) return;
            var e = NetEntity.Register(nb.gameObject, NetEntity.NextDynamicId++, NetKind.Nextbot);
            e.DefId = defId;
            if (HasClients) { w.Reset(); w.UShort(e.Id); w.String(defId); w.Vec3(nb.transform.position); ToClients(Msg.SpawnNextbot); }
        }

        public void OnEntityDestroyed(NetEntity e)
        {
            if (!InGame || !HasClients) return;
            w.Reset(); w.UShort(e.Id);
            ToClients(Msg.Despawn);
        }

        public void SetFrozen(NetEntity e, bool frozen)
        {
            if (e == null) return;
            e.Frozen = frozen;
            if (NetState.IsHost && HasClients) { w.Reset(); w.UShort(e.Id); w.Bool(frozen); ToClients(Msg.EntityFreeze); }
        }

        public void OnScaled(NetEntity e)
        {
            if (NetState.IsHost && e != null && HasClients) { w.Reset(); w.UShort(e.Id); w.Float(e.transform.localScale.x); ToClients(Msg.EntityScale, false); }
        }

        // ================================================================== world state for a joining client

        void SendWorldState(NetPlayer p)
        {
            // Map objects that no longer exist (broken crates, exploded barrels...)
            var removed = new List<ushort>();
            for (ushort id = 1; id <= mapEntityCount; id++) if (NetEntity.Get(id) == null) removed.Add(id);
            if (removed.Count > 0) { w.Reset(); w.UShort((ushort)removed.Count); foreach (var id in removed) w.UShort(id); ToPlayer(p, Msg.MapRemoved); }

            foreach (var e in new List<NetEntity>(NetEntity.All.Values))
            {
                if (e == null) continue;
                switch (e.Kind)
                {
                    case NetKind.Prop: WriteSpawnProp(w.Reset(), e); ToPlayer(p, Msg.SpawnProp); break;
                    case NetKind.Nextbot: w.Reset(); w.UShort(e.Id); w.String(e.DefId); w.Vec3(e.transform.position); ToPlayer(p, Msg.SpawnNextbot); break;
                    case NetKind.Doll:
                    {
                        var d = e.GetComponent<Goredoll>();
                        if (d == null) break;
                        WriteSpawnDoll(w.Reset(), e, d);
                        ToPlayer(p, Msg.SpawnDoll);
                        int sev = 0, gib = 0;
                        foreach (var kv in d.Parts)
                        {
                            if (kv.Value == null) continue;
                            if (kv.Value.Gibbed) gib |= 1 << (int)kv.Key;
                            else if (kv.Value.Severed && kv.Value.Joint == null) sev |= 1 << (int)kv.Key;
                        }
                        w.Reset(); w.UShort(e.Id); w.Int(sev); w.Int(gib); w.Bool(d.IsDead); w.String(d.Vitals.CauseOfDeath ?? "");
                        ToPlayer(p, Msg.DollState);
                        break;
                    }
                    case NetKind.MapObject:
                        if (e.Frozen) { w.Reset(); w.UShort(e.Id); w.Bool(true); ToPlayer(p, Msg.EntityFreeze); }
                        if (Mathf.Abs(e.transform.localScale.x - 1f) > 0.01f) { w.Reset(); w.UShort(e.Id); w.Float(e.transform.localScale.x); ToPlayer(p, Msg.EntityScale); }
                        break;
                }
            }
            p.Conn.SendReliable((byte)Msg.Toast, new byte[0], 0, Now); // marks the end of the world state (harmless)
        }

        // ================================================================== snapshots

        void SendSnapshots()
        {
            if (!HasClients) return;
            snapPackets.Clear();
            sw.Reset();
            sw.Float(Time.time);
            float now = Time.time;

            // Players (including the host's own)
            var lp = PlayerController.Instance;
            if (lp != null && Players.TryGetValue(LocalId, out var host)) FillLocalState(host, lp);
            foreach (var p in Players.Values)
            {
                BeginRecord(1);
                sw.Byte(p.Id); sw.Vec3(p.Pos); sw.Short((short)(Mathf.Repeat(p.Yaw, 360f) * 90f)); sw.Short((short)(p.Pitch * 90f));
                sw.Byte(p.Flags); sw.Byte(p.Weapon); sw.Byte(p.Health);
                EndRecord();
            }

            // Goredolls
            foreach (var d in Goredoll.All)
            {
                if (d == null || d.NetProxy) continue;
                var e = d.GetComponent<NetEntity>();
                if (e == null || !d.Parts.TryGetValue(PartType.Pelvis, out var pelvis) || pelvis == null) continue;
                BeginRecord(2);
                var v = d.Vitals;
                sw.UShort(e.Id);
                byte bits = (byte)((int)v.State | ((int)v.Paralysis << 2) | ((int)d.Ragdoll.State << 4));
                sw.Byte(bits);
                sw.Byte((byte)(d.Face != null ? (int)d.Face.Expression : 0));
                sw.Byte((byte)Mathf.Clamp(v.HeartRate, 0, 255)); sw.Byte((byte)Mathf.Clamp(v.RespRate, 0, 255)); sw.Byte((byte)Mathf.Clamp(v.SpO2, 0, 100));
                sw.UShort((ushort)Mathf.Clamp(v.BloodLost, 0, 65000)); sw.Byte((byte)(Mathf.Clamp01(v.Consciousness) * 255));
                sw.Byte((byte)Mathf.Clamp(v.Pain, 0, 255)); sw.Byte((byte)(Mathf.Clamp01(v.AirwayBlood) * 255)); sw.Byte((byte)(Mathf.Clamp01(v.Shock) * 255));
                sw.Byte((byte)Mathf.Clamp(v.Systolic, 0, 255)); sw.Byte((byte)Mathf.Clamp(v.Diastolic, 0, 255));
                var basePos = pelvis.transform.position;
                sw.Vec3(basePos); sw.Quat(pelvis.transform.rotation);
                int mask = 0;
                foreach (var kv in d.Parts)
                    if (kv.Key != PartType.Pelvis && kv.Value != null && !kv.Value.Severed && !kv.Value.Gibbed) mask |= 1 << (int)kv.Key;
                sw.Int(mask);
                for (int i = 1; i < 17; i++)
                {
                    if ((mask & (1 << i)) == 0) continue;
                    var t = d.Parts[(PartType)i].transform;
                    sw.RelPos(t.position - basePos); sw.Quat(t.rotation);
                }
                EndRecord();
            }

            // Physics objects: moving ones every tick, resting ones every couple of seconds.
            int budget = 220;
            foreach (var e in NetEntity.All.Values)
            {
                if (e == null || e.Kind == NetKind.Doll || budget <= 0) continue;
                bool include;
                if (e.Kind == NetKind.Nextbot) include = true;
                else if (e.Body == null) continue;
                else if (e.Body.isKinematic && !e.Frozen) continue; // script driven, runs locally everywhere
                else include = !e.Body.IsSleeping() || now - e.LastSentTime > 2f + (e.Id % 7) * 0.1f;
                if (!include) continue;
                e.LastSentTime = now;
                budget--;
                BeginRecord(3);
                sw.UShort(e.Id); sw.Byte((byte)(e.Frozen ? 1 : 0)); sw.Vec3(e.transform.position); sw.Quat(e.transform.rotation);
                EndRecord();
            }
            if (sw.Length > 4) snapPackets.Add(sw.ToArray());
            foreach (var pkt in snapPackets)
                foreach (var p in Players.Values)
                    if (p.Conn != null && p.Ready) p.Conn.SendUnreliable((byte)Msg.Snapshot, pkt, pkt.Length);
        }

        int recordStart;

        void BeginRecord(byte type)
        {
            if (sw.Length > 960) { snapPackets.Add(sw.ToArray()); sw.Reset(); sw.Float(Time.time); }
            sw.Byte(type);
            recordStart = sw.Length;
            sw.UShort(0); // length placeholder
        }

        void EndRecord()
        {
            int len = sw.Length - recordStart - 2;
            sw.Buffer[recordStart] = (byte)len; sw.Buffer[recordStart + 1] = (byte)(len >> 8);
        }

        void FillLocalState(NetPlayer p, PlayerController pc)
        {
            p.Pos = pc.transform.position;
            p.Yaw = pc.Cam != null ? pc.Cam.transform.eulerAngles.y : pc.transform.eulerAngles.y;
            float pitch = pc.Cam != null ? pc.Cam.transform.eulerAngles.x : 0f;
            p.Pitch = pitch > 180f ? pitch - 360f : pitch;
            var cur = pc.Inventory != null ? pc.Inventory.Current : null;
            p.Weapon = (byte)(cur != null ? WeaponCatalog.All.IndexOf(cur.Def) : 0);
            byte f = 0;
            if (pc.Crouching) f |= NetPlayer.FCrouch;
            if (cur != null && cur.IsAiming) f |= NetPlayer.FAim;
            if (pc.Noclip) f |= NetPlayer.FNoclip;
            if (pc.Health.Dead) f |= NetPlayer.FDead;
            if (pc.Sprinting) f |= NetPlayer.FSprint;
            if (pc.Grounded) f |= NetPlayer.FGrounded;
            p.Flags = f;
            p.Health = (byte)Mathf.Clamp(pc.Health.Current, 0, 255);
        }

        void SendLocalPlayerState()
        {
            var pc = PlayerController.Instance;
            var me = Local;
            if (pc == null || me == null) return;
            FillLocalState(me, pc);
            w.Reset();
            w.Vec3(me.Pos); w.Float(me.Yaw); w.Float(me.Pitch); w.Byte(me.Flags); w.Byte(me.Weapon); w.Byte(me.Health);
            ToHost(Msg.PlayerState, false);
        }

        void ApplySnapshot(byte[] data)
        {
            var rd = new NetReader(data, 0, data.Length);
            rd.Float(); // host time
            while (rd.Remaining > 3 && !rd.Overflow)
            {
                byte type = rd.Byte();
                int len = rd.UShort();
                if (len > rd.Remaining) break;
                var rec = rd.Bytes(len);
                var rr = new NetReader(rec, 0, rec.Length);
                switch (type)
                {
                    case 1: ApplyPlayerRecord(rr); break;
                    case 2: ApplyDollRecord(rr); break;
                    case 3: ApplyEntityRecord(rr); break;
                }
            }
        }

        void ApplyPlayerRecord(NetReader rr)
        {
            byte id = rr.Byte();
            var pos = rr.Vec3(); float yaw = rr.Short() / 90f, pitch = rr.Short() / 90f;
            byte flags = rr.Byte(), weapon = rr.Byte(), health = rr.Byte();
            if (id == LocalId) return;
            // Unknown players wait for the player list (their name seeds their look, so it must be right).
            if (!Players.TryGetValue(id, out var p)) return;
            p.Pos = pos; p.Yaw = yaw; p.Pitch = pitch; p.Flags = flags; p.Weapon = weapon; p.Health = health;
            EnsureAvatar(p);
        }

        void ApplyDollRecord(NetReader rr)
        {
            ushort id = rr.UShort();
            byte bits = rr.Byte(), expr = rr.Byte(), hr = rr.Byte(), rrate = rr.Byte(), spo2 = rr.Byte();
            ushort lost = rr.UShort();
            byte cons = rr.Byte(), pain = rr.Byte(), airway = rr.Byte(), shock = rr.Byte(), sys = rr.Byte(), dia = rr.Byte();
            var basePos = rr.Vec3();
            var baseRot = rr.Quat();
            int mask = rr.Int();
            var e = NetEntity.Get(id);
            var d = e != null ? e.GetComponent<Goredoll>() : null;
            var proxy = d != null ? d.GetComponent<DollProxy>() : null;
            if (proxy != null) proxy.SetTarget(PartType.Pelvis, basePos, baseRot);
            for (int i = 1; i < 17; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var rel = rr.RelPos(); var rot = rr.Quat();
                if (proxy != null) proxy.SetTarget((PartType)i, basePos + rel, rot);
            }
            if (d == null) return;
            var v = d.Vitals;
            var newState = (LifeState)(bits & 3);
            if (v.State != LifeState.Dead || newState == LifeState.Dead) v.State = newState;
            v.Paralysis = (Paralysis)((bits >> 2) & 3);
            d.Ragdoll.State = (RagdollState)((bits >> 4) & 7);
            v.HeartRate = hr; v.RespRate = rrate; v.SpO2 = spo2; v.BloodLost = lost; v.Consciousness = cons / 255f;
            v.Pain = pain; v.AirwayBlood = airway / 255f; v.Shock = shock / 255f; v.Systolic = sys; v.Diastolic = dia;
            if (d.Face != null) d.Face.Expression = (Expression)expr;
        }

        void ApplyEntityRecord(NetReader rr)
        {
            ushort id = rr.UShort();
            byte flags = rr.Byte();
            var pos = rr.Vec3();
            var rot = rr.Quat();
            var e = NetEntity.Get(id);
            if (e == null || !e.Proxy) return;
            e.Frozen = (flags & 1) != 0;
            e.TargetPos = pos; e.TargetRot = rot; e.HasTarget = true;
        }

        // ================================================================== host → client events (broadcast helpers)

        public void BroadcastDollDamage(BodyPart part, DamageInfo info)
        {
            if (!InGame || !HasClients || part.Doll == null) return;
            var e = part.Doll.GetComponent<NetEntity>();
            if (e == null) return;
            w.Reset(); w.UShort(e.Id); w.Byte((byte)part.Type); w.Damage(NetIO.ToLocal(info, part.transform));
            ToClients(Msg.DollDamage);
        }

        /// <summary>kind: 0 sever, 1 slice, 2 gib.</summary>
        public void BroadcastDismember(Goredoll doll, byte kind, BodyPart part, Vector3 point, Vector3 dir, bool sharp)
        {
            if (!InGame || !HasClients) return;
            var e = doll.GetComponent<NetEntity>();
            if (e == null) return;
            w.Reset(); w.UShort(e.Id); w.Byte(kind); w.Byte((byte)part.Type);
            w.Vec3(part.transform.InverseTransformPoint(point)); w.Vec3(part.transform.InverseTransformDirection(dir)); w.Bool(sharp);
            ToClients(Msg.DollDismember);
        }

        public void BroadcastSay(Goredoll doll, string clip, float volume, bool interrupt)
        {
            if (!InGame || !HasClients) return;
            var e = doll.GetComponent<NetEntity>();
            if (e == null) return;
            w.Reset(); w.UShort(e.Id); w.String(clip); w.Float(volume); w.Bool(interrupt);
            ToClients(Msg.DollSay, false);
        }

        public void BroadcastDeath(Goredoll doll, string cause, string killer)
        {
            if (!InGame || !HasClients) return;
            var e = doll.GetComponent<NetEntity>();
            if (e == null) return;
            w.Reset(); w.UShort(e.Id); w.String(cause ?? ""); w.String(killer ?? "");
            ToClients(Msg.DollDeath);
        }

        public void BroadcastRevive(Goredoll doll)
        {
            if (!InGame || !HasClients) return;
            var e = doll.GetComponent<NetEntity>();
            if (e == null) return;
            w.Reset(); w.UShort(e.Id);
            ToClients(Msg.DollRevive);
        }

        public void BroadcastEntityDamage(NetEntity e, DamageInfo info)
        {
            if (!InGame || !HasClients || e == null) return;
            w.Reset(); w.UShort(e.Id); w.Damage(info);
            ToClients(Msg.EntityDamage);
        }

        public void BroadcastExplosion(Vector3 pos, float radius)
        {
            if (!InGame || !HasClients) return;
            w.Reset(); w.Vec3(pos); w.Float(radius);
            ToClients(Msg.ExplosionFx);
        }

        /// <summary>Gunfire visuals/sound for everyone except the shooter.</summary>
        public void BroadcastFire(byte shooter, int weaponIndex, Vector3 origin, List<Vector3> dirs)
        {
            if (!InGame || !HasClients) return;
            w.Reset(); w.Byte(shooter); w.Byte((byte)weaponIndex); w.Vec3(origin); w.Byte((byte)dirs.Count);
            foreach (var d in dirs) w.Vec3(d);
            ToClients(Msg.FireFx, false, Get(shooter));
        }

        public void SendHitMarker(RemotePlayer target, Vector3 point, float damage)
        {
            if (target == null || target.Player == null) return;
            w.Reset(); w.Vec3(point); w.Float(damage);
            ToPlayer(target.Player, Msg.HitMarker, false);
        }

        /// <summary>Host: a remote player's avatar was hit; their own game applies the damage.</summary>
        public void HurtRemotePlayer(NetPlayer p, DamageInfo info, object source)
        {
            if (p == null || p.Conn == null) return;
            if (!Server.PvP && PlayerTargets.IsPlayerSource(source)) return;
            w.Reset(); w.Damage(info); w.String(source is IPlayerTarget t ? t.PlayerName : info.Weapon ?? "");
            ToPlayer(p, Msg.HurtPlayer);
        }
    }
}
