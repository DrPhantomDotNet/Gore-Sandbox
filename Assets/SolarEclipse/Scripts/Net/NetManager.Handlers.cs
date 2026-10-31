using System.Collections.Generic;
using SolarEclipse.Net;
using UnityEngine;

namespace SolarEclipse
{
    public sealed partial class NetManager
    {
        // ================================================================== dispatch

        void Dispatch(NetConnection conn, NetMessage m)
        {
            var rd = r.Set(m.Data, 0, m.Data.Length);
            if (NetState.IsHost)
            {
                if (!(conn.Tag is NetPlayer p)) return;
                HostHandle(p, (Msg)m.Type, rd);
            }
            else
            {
                using (NetState.Remote()) ClientHandle((Msg)m.Type, rd);
            }
        }

        // ================================================================== client side

        /// <summary>True while a client draws an explosion the host announced.</summary>
        public static bool ApplyingExplosionFx;

        void ClientHandle(Msg type, NetReader rd)
        {
            switch (type)
            {
                case Msg.Welcome: OnWelcome(rd); break;
                case Msg.CustomMap: OnCustomMap(rd); break;
                case Msg.ModRules: case Msg.ModWeaponDef: case Msg.ModReject: ClientModMsg(type, rd); break;
                case Msg.ThrowableFx: case Msg.C4Detonated: ClientExplosivesMsg(type, rd); break;
                case Msg.ProfileData: case Msg.BadgeGrant: ClientProfileMsg(type, rd); break; // NetManager.Profile.cs
                case Msg.OWState: case Msg.OWEvent: ClientOWMsg(type, rd); break; // OpenWorld/OWNet.cs
                case Msg.PlayerList:
                {
                    int n = rd.Byte();
                    var seen = new HashSet<byte>();
                    for (int i = 0; i < n; i++)
                    {
                        byte id = rd.Byte(); string name = rd.String(); bool creative = rd.Bool(), isHost = rd.Bool(); int ping = rd.Short();
                        seen.Add(id);
                        if (!Players.TryGetValue(id, out var p)) { p = new NetPlayer { Id = id }; Players[id] = p; }
                        p.Name = name; p.Creative = creative; p.IsHost = isHost; p.PingMs = ping;
                        if (p.Avatar != null) p.Avatar.SetName(name);
                        EnsureAvatar(p);
                    }
                    foreach (var id in new List<byte>(Players.Keys))
                        if (!seen.Contains(id) && id != LocalId) { var p = Players[id]; if (p.Avatar != null) Destroy(p.Avatar.gameObject); Players.Remove(id); }
                    break;
                }
                case Msg.PlayerLeft:
                {
                    byte id = rd.Byte(); string why = rd.String();
                    if (Players.TryGetValue(id, out var p)) { AddChat(p.Name + " " + why + ".", UI.Warn); if (p.Avatar != null) Destroy(p.Avatar.gameObject); Players.Remove(id); }
                    break;
                }
                case Msg.Chat:
                {
                    byte id = rd.Byte(); string text = rd.String();
                    var p = Get(id);
                    AddChat((p != null ? p.Name : "?") + ": " + text, p != null && p.IsHost ? UI.Accent2 : Color.white);
                    break;
                }
                case Msg.Toast:
                {
                    if (rd.Remaining == 0) break;
                    rd.Byte(); var text = rd.String();
                    if (!string.IsNullOrEmpty(text)) AddChat(text, UI.Warn);
                    break;
                }
                case Msg.Permission:
                {
                    LocalCreative = rd.Bool();
                    GameEvents.ShowToast(LocalCreative ? "The host gave you Creative mode: noclip (V), Spawn Gun and spawn menu unlocked." : "The host removed your Creative mode.");
                    ApplyLoadout();
                    break;
                }
                case Msg.Kicked:
                {
                    var reason = rd.String();
                    Shutdown(reason);
                    GameManager.I.ReturnToMenu();
                    UIRoot.Push(new MessageDialog("REMOVED FROM SERVER", reason));
                    break;
                }
                case Msg.Rules:
                {
                    float ts = rd.Float(), grav = rd.Float();
                    Rules.NoFallDamage = rd.Bool(); Rules.Invincible = rd.Bool(); Rules.InfiniteAmmo = rd.Bool(); Rules.NoReload = rd.Bool();
                    Rules.BulletTracers = rd.Bool(); Rules.AiIgnorePlayer = rd.Bool(); Rules.PlayerSpeedMultiplier = rd.Float(); Rules.JumpMultiplier = rd.Float();
                    Server.PvP = rd.Bool(); Rules.Xray = rd.Bool();
                    if (!Mathf.Approximately(Rules.TimeScale, ts)) Rules.TimeScale = ts;
                    if (!Mathf.Approximately(Rules.GravityScale, grav)) { Rules.GravityScale = grav; Rules.ApplyGravity(); }
                    break;
                }
                case Msg.Ping: { var t = rd.Float(); w.Reset(); w.Float(t); ToHost(Msg.Pong, false); break; }
                case Msg.HitMarker: GameEvents.Hit(rd.Vec3(), rd.Float()); break;
                case Msg.Voice:
                {
                    byte id = rd.Byte(); rd.UShort(); int frames = rd.Byte();
                    var data = rd.Bytes(rd.Remaining);
                    if (id != LocalId) VoiceChat.I?.Receive(id, data, 0, frames);
                    break;
                }
                case Msg.Snapshot: ApplySnapshot(Copy(rd)); break;

                case Msg.SpawnDoll:
                {
                    ushort id = rd.UShort(); int seed = rd.Int(); string preset = rd.String(); int variant = rd.Int();
                    var feet = rd.Vec3(); float yaw = rd.Float(); byte owner = rd.Byte();
                    if (NetEntity.Get(id) != null || WorldRoot.Current == null) break;
                    var d = GoredollFactory.Spawn(feet, yaw, preset, variant, null, seed);
                    if (d == null) break;
                    var e = NetEntity.Register(d.gameObject, id, NetKind.Doll);
                    e.Owner = owner;
                    d.MakeProxy();
                    if (owner == LocalId) SpawnGun.UndoStack.Add(d.gameObject);
                    break;
                }
                case Msg.SpawnProp:
                {
                    ushort id = rd.UShort(); string def = rd.String(); var pos = rd.Vec3(); var rot = rd.Quat();
                    float scale = rd.Float(); bool frozen = rd.Bool(); byte owner = rd.Byte();
                    if (NetEntity.Get(id) != null || WorldRoot.Current == null) break;
                    var rb = PropFactory.Spawn(def, pos, rot);
                    if (rb == null) break;
                    var e = NetEntity.Register(rb.gameObject, id, NetKind.Prop);
                    e.DefId = def; e.Owner = owner; e.Frozen = frozen;
                    e.MakeProxy();
                    if (Mathf.Abs(scale - 1f) > 0.001f) rb.transform.localScale = Vector3.one * scale;
                    if (owner == LocalId) SpawnGun.UndoStack.Add(rb.gameObject);
                    break;
                }
                case Msg.SpawnNextbot:
                {
                    ushort id = rd.UShort(); string def = rd.String(); var pos = rd.Vec3();
                    if (NetEntity.Get(id) != null || WorldRoot.Current == null) break;
                    var nb = Nextbots.Spawn(def, pos);
                    nb.Proxy = true;
                    var e = NetEntity.Register(nb.gameObject, id, NetKind.Nextbot);
                    e.DefId = def;
                    e.MakeProxy();
                    break;
                }
                case Msg.Despawn:
                {
                    var e = NetEntity.Get(rd.UShort());
                    if (e == null) break;
                    var d = e.GetComponent<Goredoll>();
                    SpawnGun.UndoStack.Remove(e.gameObject);
                    if (d != null) d.Despawn(); else Destroy(e.gameObject);
                    break;
                }
                case Msg.MapRemoved:
                {
                    int n = rd.UShort();
                    for (int i = 0; i < n; i++) { var e = NetEntity.Get(rd.UShort()); if (e != null) Destroy(e.gameObject); }
                    break;
                }
                case Msg.EntityScale:
                {
                    var e = NetEntity.Get(rd.UShort()); float s = rd.Float();
                    if (e != null) e.transform.localScale = Vector3.one * s;
                    break;
                }
                case Msg.EntityFreeze:
                {
                    var e = NetEntity.Get(rd.UShort()); bool f = rd.Bool();
                    if (e != null) e.Frozen = f;
                    break;
                }
                case Msg.DollDamage:
                {
                    var d = DollOf(rd.UShort()); var pt = (PartType)rd.Byte(); var info = rd.Damage();
                    if (d == null || !d.Parts.TryGetValue(pt, out var part) || part == null) break;
                    part.TakeDamage(NetIO.ToWorld(info, part.transform));
                    break;
                }
                case Msg.DollDismember:
                {
                    var d = DollOf(rd.UShort()); byte kind = rd.Byte(); var pt = (PartType)rd.Byte();
                    var lp = rd.Vec3(); var ld = rd.Vec3(); bool sharp = rd.Bool();
                    if (d == null || !d.Parts.TryGetValue(pt, out var part) || part == null) break;
                    var wp = part.transform.TransformPoint(lp); var wd = part.transform.TransformDirection(ld);
                    if (kind == 0) d.Dismember.Sever(part, wp, wd, sharp);
                    else if (kind == 1) d.Dismember.Slice(part, wp, wd);
                    else d.Dismember.Gib(part, wd);
                    FreeSeveredParts(d);
                    break;
                }
                case Msg.DollState:
                {
                    var d = DollOf(rd.UShort()); int sev = rd.Int(), gib = rd.Int(); bool dead = rd.Bool(); string cause = rd.String();
                    if (d == null) break;
                    for (int i = 0; i < 17; i++)
                    {
                        if (!d.Parts.TryGetValue((PartType)i, out var part) || part == null) continue;
                        if ((gib & (1 << i)) != 0) d.Dismember.Gib(part, Vector3.up);
                        else if ((sev & (1 << i)) != 0 && !part.Severed) d.Dismember.Sever(part, part.transform.position, Vector3.up, false);
                    }
                    FreeSeveredParts(d);
                    if (dead) { d.Vitals.State = LifeState.Dead; d.Vitals.CauseOfDeath = cause; d.Voice.Silence(); }
                    break;
                }
                case Msg.DollSay:
                {
                    var d = DollOf(rd.UShort()); string clip = rd.String(); float vol = rd.Float(); bool interrupt = rd.Bool();
                    if (d != null) d.Voice.Say(clip, vol, interrupt);
                    break;
                }
                case Msg.DollDeath:
                {
                    var d = DollOf(rd.UShort()); string cause = rd.String(), killer = rd.String();
                    if (d == null) break;
                    d.Vitals.State = LifeState.Dead;
                    d.Vitals.CauseOfDeath = cause;
                    d.Voice.Silence();
                    GameEvents.Kill((string.IsNullOrEmpty(killer) ? "" : killer + "  >  ") + d.DisplayName + "  -  " + cause, new Color(1f, 0.45f, 0.4f));
                    break;
                }
                case Msg.DollRevive:
                {
                    var d = DollOf(rd.UShort());
                    if (d != null) d.Revive();
                    break;
                }
                case Msg.EntityDamage:
                {
                    var e = NetEntity.Get(rd.UShort()); var info = rd.Damage();
                    if (e == null) break;
                    foreach (var mb in e.GetComponents<MonoBehaviour>()) if (mb is IDamageable dmg) dmg.TakeDamage(info);
                    break;
                }
                case Msg.ExplosionFx:
                    ApplyingExplosionFx = true;
                    try { Explosion.Create(rd.Vec3(), rd.Float(), 0f, null, 0f); }
                    finally { ApplyingExplosionFx = false; }
                    break;
                case Msg.FireFx: PlayRemoteFire(rd); break;
                case Msg.HurtPlayer:
                {
                    var info = rd.Damage(); string from = rd.String();
                    var pc = PlayerController.Instance;
                    if (pc == null || pc.Health.Dead) break;
                    if (!string.IsNullOrEmpty(from) && string.IsNullOrEmpty(info.Weapon)) info.Weapon = from;
                    pc.Health.TakeDamage(info);
                    if (pc.Health.Dead) { w.Reset(); w.String(pc.Health.DeathCause + (string.IsNullOrEmpty(from) ? "" : " (" + from + ")")); ToHost(Msg.PlayerDied); }
                    break;
                }
            }
        }

        static byte[] Copy(NetReader rd) => rd.Bytes(rd.Remaining);

        static Goredoll DollOf(ushort id)
        {
            var e = NetEntity.Get(id);
            return e != null ? e.GetComponent<Goredoll>() : null;
        }

        /// <summary>Client: limbs cut off a proxy doll become local physics debris.</summary>
        static void FreeSeveredParts(Goredoll d)
        {
            foreach (var p in d.Parts.Values)
            {
                if (p == null || !p.Severed || p.Gibbed || p.Body == null || !p.Body.isKinematic) continue;
                p.Body.isKinematic = false;
                p.Body.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        void OnWelcome(NetReader rd)
        {
            LocalId = rd.Byte(); ServerName = rd.String(); string hostName = rd.String();
            string mapId = rd.String(); int seed = rd.Int();
            var options = new MapOptions { Time = (TimeOfDay)rd.Byte(), Loadout = rd.Byte(), Nextbots = rd.Bool(), AiIgnore = rd.Bool(), StartingNpcs = 0 };
            LocalCreative = rd.Bool(); Server.PvP = rd.Bool(); clientLoadout = rd.Byte();
            if (rd.Remaining > 0) options.Weather = (WeatherKind)rd.Byte(); // appended last in SendWelcome
            Players[LocalId] = new NetPlayer { Id = LocalId, Name = LocalName, Guid = LocalGuid, Creative = LocalCreative };
            Status = "Joined " + ServerName;
            Debug.Log($"[Net] Welcome to '{ServerName}' (host {hostName}) as player {LocalId}; map {mapId}");
            var cb = onJoined;
            onJoined = null; onJoinFailed = null;
            cb?.Invoke();
            var map = MapCatalog.All.Find(m => m.Id == mapId) ?? MapCatalog.All[0];
            GameManager.I.StartMap(map, options, seed, true);
        }

        void PlayRemoteFire(NetReader rd)
        {
            byte shooter = rd.Byte(); int widx = rd.Byte(); var origin = rd.Vec3(); int n = rd.Byte();
            var dirs = new List<Vector3>();
            for (int i = 0; i < n; i++) dirs.Add(rd.Vec3());
            var def = Mods.ModNet.FromNet(widx); // modded weapons use the host's session indices (Net/NetManager.Mods.cs)
            if (def == null) return;
            var p = Get(shooter);
            var muzzle = p != null && p.Avatar != null ? p.Avatar.MuzzlePosition : origin;
            if (p != null && p.Avatar != null) p.Avatar.OnFire(def);
            AudioManager.Play(def.FireSound ?? "gun_glock", muzzle, 1f, Random.Range(0.96f, 1.04f), 1f, 3f, 150f);
            if (Guns2.IsHarpoon(def)) { foreach (var d in dirs) Harpoon.Launch(origin + d * 0.35f, d, def, null, false); return; } // visual-only harpoon
            if (Mods.ModWeapons.IsMod(def)) { Mods.ModWeapons.FireRemote(def, origin, dirs, null, muzzle, false, 0); return; }
            for (int i = 0; i < dirs.Count; i++) Ballistics.Fire(origin, dirs[i], def, null, muzzle, def.Kind == WeaponKind.Admin ? DamageType.Admin : DamageType.Bullet, def.Pellets == 1 || i % 3 == 0, false);
        }

        // ================================================================== host side

        void HostHandle(NetPlayer p, Msg type, NetReader rd)
        {
            CurrentRequester = p;
            try { HostHandleInner(p, type, rd); }
            finally { CurrentRequester = null; }
        }

        bool CreativeOk(NetPlayer p) => p.IsHost || p.Creative;

        void HostHandleInner(NetPlayer p, Msg type, NetReader rd)
        {
            var src = SourceFor(p);
            switch (type)
            {
                case Msg.ClientReady:
                    if (p.Ready) break;
                    p.Ready = true;
                    SendWorldState(p);
                    SendProfilesTo(p); // NetManager.Profile.cs
                    EnsureAvatar(p);
                    SystemMessage(p.Name + " joined the game.");
                    BroadcastPlayerList();
                    BroadcastRules();
                    break;
                case Msg.PlayerState:
                    p.Pos = rd.Vec3(); p.Yaw = rd.Float(); p.Pitch = rd.Float(); p.Flags = rd.Byte(); p.Weapon = rd.Byte(); p.Health = rd.Byte();
                    if (p.Avatar == null) EnsureAvatar(p);
                    break;
                case Msg.Pong: p.PingMs = (int)((Now - rd.Float()) * 1000); break;
                case Msg.ReqOW: case Msg.CarInput: HostOWMsg(p, type, rd); break; // OpenWorld/OWNet.cs
                case Msg.Voice:
                {
                    var data = rd.Bytes(rd.Remaining);
                    if (data.Length < 3) break;
                    int frames = data[2];
                    if (frames > 4 || data.Length < 3 + frames * Net.Adpcm.FrameBytes) break;
                    VoiceChat.I?.Receive(p.Id, data, 3, frames);
                    w.Reset(); w.Byte(p.Id); w.Bytes(data, 0, data.Length);
                    ToClients(Msg.Voice, false, p);
                    break;
                }
                case Msg.Chat: { var t = rd.String().Trim(); if (t.Length > 0) BroadcastChat(p.Id, t.Length > 160 ? t.Substring(0, 160) : t); break; }
                case Msg.PlayerDied: SystemMessage(p.Name + " died — " + rd.String()); break;

                case Msg.ReqDamagePart:
                {
                    var d = DollOf(rd.UShort()); var pt = (PartType)rd.Byte(); var info = rd.Damage();
                    if (d == null || !d.Parts.TryGetValue(pt, out var part) || part == null) break;
                    info = NetIO.ToWorld(info, part.transform);
                    info.Source = src;
                    part.TakeDamage(info);
                    break;
                }
                case Msg.ReqDamageEntity:
                {
                    var e = NetEntity.Get(rd.UShort()); var info = rd.Damage();
                    if (e == null) break;
                    info.Source = src;
                    foreach (var mb in e.GetComponents<MonoBehaviour>()) if (mb is IDamageable dmg) dmg.TakeDamage(info);
                    break;
                }
                case Msg.ReqImpulse:
                {
                    var rb = BodyOf(rd.UShort(), rd.Byte()); var impulse = rd.Vec3(); var point = rd.Vec3();
                    if (rb != null && !rb.isKinematic) rb.AddForceAtPosition(Vector3.ClampMagnitude(impulse, 4000f), point, ForceMode.Impulse);
                    break;
                }
                case Msg.ReqLaunch:
                {
                    var d = DollOf(rd.UShort()); var v = rd.Vec3();
                    if (d != null) d.Launch(Vector3.ClampMagnitude(v, 60f));
                    break;
                }
                case Msg.ReqDismember:
                {
                    var d = DollOf(rd.UShort()); byte kind = rd.Byte(); var pt = (PartType)rd.Byte(); var point = rd.Vec3(); var dir = rd.Vec3(); bool sharp = rd.Bool();
                    if (d == null || !d.Parts.TryGetValue(pt, out var part) || part == null) break;
                    if (kind == 0) d.Dismember.Sever(part, point, dir, sharp);
                    else if (kind == 1) d.Dismember.Slice(part, point, dir);
                    else if (CreativeOk(p)) d.Dismember.Gib(part, dir);
                    break;
                }
                case Msg.ReqInteract:
                {
                    var e = NetEntity.Get(rd.UShort());
                    if (e == null || p.Avatar == null) break;
                    if (Vector3.Distance(e.transform.position, p.Pos) > 6f) break;
                    foreach (var mb in e.GetComponents<MonoBehaviour>()) if (mb is IInteractable it) { it.Interact(p.Avatar.gameObject); break; }
                    break;
                }
                case Msg.ReqFire:
                {
                    int widx = rd.Byte(); var origin = rd.Vec3(); int n = rd.Byte();
                    var def = Mods.ModNet.FromNet(widx); // the host's own validated copy for modded weapons
                    if (def == null || n > 16) break;
                    var dirs = new List<Vector3>();
                    for (int i = 0; i < n; i++) dirs.Add(rd.Vec3().normalized);
                    if (Vector3.Distance(origin, p.Pos) > 4f) origin = p.Pos + Vector3.up * 1.6f; // sanity
                    var muzzle = p.Avatar != null ? p.Avatar.MuzzlePosition : origin;
                    if (p.Avatar != null) p.Avatar.OnFire(def);
                    AudioManager.Play(def.FireSound ?? "gun_glock", muzzle, 1f, Random.Range(0.96f, 1.04f), 1f, 3f, 150f);
                    GameEvents.EmitNoise(origin, def.NoiseRadius, 1f, src);
                    Ballistics.BeginShot();
                    if (Guns2.IsHarpoon(def)) foreach (var d in dirs) Harpoon.Launch(origin + d * 0.35f, d, def, src, true); // host-side pinning
                    else if (Mods.ModWeapons.IsMod(def)) Mods.ModWeapons.FireRemote(def, origin, dirs, src, muzzle, true, Layers.RemoteShootMask);
                    else
                    for (int i = 0; i < dirs.Count; i++) Ballistics.Fire(origin, dirs[i], def, src, muzzle, DamageType.Bullet, def.Pellets == 1 || i % 3 == 0, true, Layers.RemoteShootMask);
                    Ballistics.EndShot(dirs.Count > 0 ? dirs[0] : Vector3.forward);
                    BroadcastFire(p.Id, widx, origin, dirs);
                    break;
                }
                case Msg.ReqHurtPlayer:
                {
                    byte target = rd.Byte(); var info = rd.Damage();
                    info.Source = src;
                    if (!Server.PvP) break;
                    if (target == LocalId) PlayerController.Instance?.Health.TakeDamage(info);
                    else HurtRemotePlayer(Get(target), info, src);
                    break;
                }
                case Msg.ReqThrowable: case Msg.ReqDetonate: HostExplosivesMsg(p, type, rd); break;
                case Msg.ReqModWeapon: HostModMsg(p, type, rd); break;
                case Msg.ProfileUpdate: case Msg.ReqBadgeGrant: HostProfileMsg(p, type, rd); break; // NetManager.Profile.cs
                case Msg.ReqExplosion:
                {
                    var pos = rd.Vec3(); float radius = Mathf.Min(rd.Float(), 20f), dmg = Mathf.Min(rd.Float(), 1000f), push = Mathf.Min(rd.Float(), 4f);
                    if (CreativeOk(p)) Explosion.Create(pos, radius, dmg, src, push);
                    break;
                }
                case Msg.ReqAdmin:
                {
                    var mode = (AdminGun.Mode)rd.Byte(); var origin = rd.Vec3(); var dir = rd.Vec3();
                    if (CreativeOk(p)) AdminGun.Perform(mode, origin, dir.normalized, p.Avatar != null ? p.Avatar.MuzzlePosition : origin, src);
                    break;
                }
                case Msg.ReqDollCommand:
                {
                    var d = DollOf(rd.UShort()); byte cmd = rd.Byte(); var pt = (PartType)rd.Byte();
                    if (d == null) break;
                    switch (cmd)
                    {
                        case 1: d.Kill("Euthanised"); break;
                        case 2: if (CreativeOk(p)) d.Revive(); break;
                        case 3: if (d.Parts.TryGetValue(pt, out var a)) foreach (var wnd in a.Wounds) wnd.BleedRate *= wnd.Arterial ? 0.5f : 0.15f; break;
                        case 4: foreach (var part in d.Parts.Values) if (part != null && (part.Type == pt || IsDistal(part.Type, pt))) foreach (var wnd in part.Wounds) { wnd.BleedRate = 0; wnd.Arterial = false; } break;
                        case 5: d.Vitals.Adrenaline = 1f; d.Vitals.Consciousness = Mathf.Max(d.Vitals.Consciousness, 0.7f); if (d.Vitals.State == LifeState.Unconscious) d.Vitals.State = LifeState.Alive; break;
                        case 6: if (CreativeOk(p)) d.Brain.ForceDance = true; break;
                    }
                    break;
                }

                // ---- Creative-only world editing
                case Msg.ReqSpawn:
                {
                    string def = rd.String(); var pos = rd.Vec3(); var rot = rd.Quat(); string preset = rd.String(); int variant = rd.Int(); bool frozen = rd.Bool();
                    if (!CreativeOk(p)) { Deny(p); break; }
                    var sd = SpawnCatalog.Find(def);
                    if (sd == null) break;
                    var (op, ov, of) = (SpawnCatalog.GoredollPreset, SpawnCatalog.GoredollVariant, SpawnCatalog.SpawnFrozen);
                    SpawnCatalog.GoredollPreset = preset; SpawnCatalog.GoredollVariant = variant; SpawnCatalog.SpawnFrozen = frozen;
                    try { SpawnCatalog.Spawn(sd, pos, rot); }
                    finally { SpawnCatalog.GoredollPreset = op; SpawnCatalog.GoredollVariant = ov; SpawnCatalog.SpawnFrozen = of; }
                    break;
                }
                case Msg.ReqDelete:
                {
                    var e = NetEntity.Get(rd.UShort());
                    if (e == null) break;
                    if (!CreativeOk(p)) { Deny(p); break; }
                    if (e.Kind == NetKind.MapObject && SpawnGun.DeletableRoot(e.GetComponentInChildren<Collider>()) == null) break;
                    SpawnGun.DeleteObject(e.gameObject);
                    break;
                }
                case Msg.ReqGrab:
                {
                    var rb = BodyOf(rd.UShort(), rd.Byte()); var local = rd.Vec3();
                    if (!CreativeOk(p) || rb == null) break;
                    ReleaseRemoteGrab(p, Vector3.zero);
                    p.GrabBody = rb; p.GrabLocal = local; p.GrabTarget = rb.transform.TransformPoint(local); p.GrabRot = rb.rotation;
                    var part = rb.GetComponent<BodyPart>();
                    p.GrabIsDoll = part != null && part.Doll != null;
                    if (p.GrabIsDoll && !part.Severed) part.Doll.Ragdoll.Fall(3f);
                    p.LastGrabMsg = Time.time;
                    break;
                }
                case Msg.ReqGrabMove:
                    p.GrabTarget = rd.Vec3(); p.GrabRot = rd.Quat(); p.GrabRotating = rd.Bool(); p.LastGrabMsg = Time.time;
                    break;
                case Msg.ReqGrabEnd: ReleaseRemoteGrab(p, Vector3.ClampMagnitude(rd.Vec3(), 40f)); break;
                case Msg.ReqFreeze:
                {
                    var rb = BodyOf(rd.UShort(), rd.Byte()); bool frozen = rd.Bool();
                    if (!CreativeOk(p) || rb == null) break;
                    if (p.GrabBody == rb) ReleaseRemoteGrab(p, Vector3.zero);
                    rb.isKinematic = frozen;
                    if (!frozen) rb.WakeUp();
                    var ne = rb.GetComponent<NetEntity>();
                    if (ne != null) SetFrozen(ne, frozen);
                    AudioManager.Play(frozen ? "sg_freeze" : "sg_unfreeze", rb.position, 0.7f);
                    break;
                }
                case Msg.ReqScale:
                {
                    var e = NetEntity.Get(rd.UShort()); float s = Mathf.Clamp(rd.Float(), 0.15f, 6f);
                    if (!CreativeOk(p) || e == null || e.Kind == NetKind.Doll || e.Kind == NetKind.Nextbot) break;
                    float old = e.transform.localScale.x;
                    e.transform.localScale = Vector3.one * s;
                    if (e.Body != null) e.Body.mass = Mathf.Max(0.05f, e.Body.mass * Mathf.Pow(s / Mathf.Max(0.01f, old), 3f));
                    OnScaled(e);
                    break;
                }
            }
        }

        void Deny(NetPlayer p)
        {
            w.Reset(); w.Byte(0); w.String("You need Creative mode for that. Ask the host (Esc > Players).");
            ToPlayer(p, Msg.Toast);
        }

        static bool IsDistal(PartType p, PartType of)
        {
            var spec = Anatomy.Parts[p];
            while (spec.Parent.HasValue) { if (spec.Parent.Value == of) return true; spec = Anatomy.Parts[spec.Parent.Value]; }
            return false;
        }

        /// <summary>Resolves (entity id, part) to a rigidbody: a Goredoll part when part != 255, else the entity's body.</summary>
        static Rigidbody BodyOf(ushort id, byte part)
        {
            var e = NetEntity.Get(id);
            if (e == null) return null;
            if (part != 255)
            {
                var d = e.GetComponent<Goredoll>();
                return d != null && d.Parts.TryGetValue((PartType)part, out var bp) && bp != null && !bp.Gibbed ? bp.Body : null;
            }
            return e.Body;
        }

        // ================================================================== remote spawn-gun grabs (host)

        void UpdateRemoteGrabs()
        {
            float dt = Time.deltaTime;
            foreach (var p in Players.Values)
            {
                var rb = p.GrabBody;
                if (rb == null) continue;
                if (Time.time - p.LastGrabMsg > 1.5f) { ReleaseRemoteGrab(p, Vector3.zero); continue; }
                var grabWorld = rb.transform.TransformPoint(p.GrabLocal);
                if (rb.isKinematic)
                {
                    var newRot = p.GrabIsDoll && !p.GrabRotating ? rb.rotation : p.GrabRot;
                    rb.MovePosition(p.GrabTarget - newRot * Vector3.Scale(p.GrabLocal, rb.transform.lossyScale));
                    rb.MoveRotation(newRot);
                    continue;
                }
                var desired = Vector3.ClampMagnitude((p.GrabTarget - grabWorld) * 14f, 40f);
                RB.SetVel(rb, Vector3.Lerp(RB.Vel(rb), desired, 1f - Mathf.Exp(-18f * dt)));
                rb.WakeUp();
                if (!p.GrabIsDoll || p.GrabRotating)
                {
                    var q = p.GrabRot * Quaternion.Inverse(rb.rotation);
                    q.ToAngleAxis(out float ang, out var axis);
                    if (ang > 180f) ang -= 360f;
                    if (!float.IsNaN(axis.x) && axis.sqrMagnitude > 0.001f) rb.angularVelocity = Vector3.ClampMagnitude(axis * ang * Mathf.Deg2Rad * 12f, 30f);
                }
            }
        }

        void ReleaseRemoteGrab(NetPlayer p, Vector3 throwVelocity)
        {
            if (p.GrabBody == null) return;
            if (!p.GrabBody.isKinematic)
                RB.SetVel(p.GrabBody, throwVelocity != Vector3.zero ? throwVelocity : Vector3.ClampMagnitude(RB.Vel(p.GrabBody), 20f));
            p.GrabBody = null;
        }

        // ================================================================== client requests (called by gameplay code)

        public bool ClientGrabActive;
        public Vector3 ClientGrabTarget;
        public Quaternion ClientGrabRot = Quaternion.identity;
        public bool ClientGrabRotating;
        double lastGrabSend;

        void UpdateClientGrab(double now)
        {
            if (!ClientGrabActive || now - lastGrabSend < 1.0 / 30.0) return;
            lastGrabSend = now;
            w.Reset(); w.Vec3(ClientGrabTarget); w.Quat(ClientGrabRot); w.Bool(ClientGrabRotating);
            ToHost(Msg.ReqGrabMove, false);
        }

        /// <summary>Finds the network handle of a rigidbody: (entity id, part type or 255). False if not networked.</summary>
        public static bool Handle(Rigidbody rb, out ushort id, out byte part)
        {
            id = 0; part = 255;
            if (rb == null) return false;
            var bp = rb.GetComponent<BodyPart>();
            if (bp != null && bp.Doll != null)
            {
                if (bp.Severed) return false; // severed limbs are local debris
                var de = bp.Doll.GetComponent<NetEntity>();
                if (de == null) return false;
                id = de.Id; part = (byte)bp.Type;
                return true;
            }
            var e = rb.GetComponentInParent<NetEntity>();
            if (e == null) return false;
            id = e.Id;
            return true;
        }

        public void RequestDamagePart(BodyPart part, DamageInfo info)
        {
            var e = part.Doll != null ? part.Doll.GetComponent<NetEntity>() : null;
            if (e == null) return;
            w.Reset(); w.UShort(e.Id); w.Byte((byte)part.Type); w.Damage(NetIO.ToLocal(info, part.transform));
            ToHost(Msg.ReqDamagePart);
        }

        public void RequestDamageEntity(NetEntity e, DamageInfo info) { w.Reset(); w.UShort(e.Id); w.Damage(info); ToHost(Msg.ReqDamageEntity); }

        public void RequestImpulse(Rigidbody rb, Vector3 impulse, Vector3 point)
        {
            if (!Handle(rb, out var id, out var part)) return;
            w.Reset(); w.UShort(id); w.Byte(part); w.Vec3(impulse); w.Vec3(point);
            ToHost(Msg.ReqImpulse);
        }

        public void RequestLaunch(Goredoll d, Vector3 v)
        {
            var e = d.GetComponent<NetEntity>();
            if (e == null) return;
            w.Reset(); w.UShort(e.Id); w.Vec3(v); ToHost(Msg.ReqLaunch);
        }

        public void RequestDismember(Goredoll d, byte kind, BodyPart part, Vector3 point, Vector3 dir, bool sharp)
        {
            var e = d.GetComponent<NetEntity>();
            if (e == null) return;
            w.Reset(); w.UShort(e.Id); w.Byte(kind); w.Byte((byte)part.Type); w.Vec3(point); w.Vec3(dir); w.Bool(sharp);
            ToHost(Msg.ReqDismember);
        }

        public void RequestDollCommand(Goredoll d, byte cmd, PartType part = PartType.Pelvis)
        {
            var e = d.GetComponent<NetEntity>();
            if (e == null) return;
            w.Reset(); w.UShort(e.Id); w.Byte(cmd); w.Byte((byte)part); ToHost(Msg.ReqDollCommand);
        }

        public void RequestSpawn(SpawnableDef def, Vector3 pos, Quaternion rot)
        {
            w.Reset(); w.String(def.Id); w.Vec3(pos); w.Quat(rot); w.String(Armor.WithSpawnArmor(SpawnCatalog.GoredollPreset)); w.Int(SpawnCatalog.GoredollVariant); w.Bool(SpawnCatalog.SpawnFrozen);
            ToHost(Msg.ReqSpawn);
        }

        public void RequestDelete(GameObject go)
        {
            var e = go != null ? go.GetComponentInParent<NetEntity>() : null;
            if (e == null) return;
            w.Reset(); w.UShort(e.Id); ToHost(Msg.ReqDelete);
        }

        public void RequestGrab(Rigidbody rb, Vector3 localPoint)
        {
            if (!Handle(rb, out var id, out var part)) return;
            w.Reset(); w.UShort(id); w.Byte(part); w.Vec3(localPoint);
            ToHost(Msg.ReqGrab);
            ClientGrabActive = true;
        }

        public void RequestGrabEnd(Vector3 throwVelocity)
        {
            ClientGrabActive = false;
            w.Reset(); w.Vec3(throwVelocity); ToHost(Msg.ReqGrabEnd);
        }

        public void RequestFreeze(Rigidbody rb, bool frozen)
        {
            if (!Handle(rb, out var id, out var part)) return;
            if (frozen) ClientGrabActive = false;
            w.Reset(); w.UShort(id); w.Byte(part); w.Bool(frozen); ToHost(Msg.ReqFreeze);
        }

        public void RequestScale(NetEntity e, float scale) { w.Reset(); w.UShort(e.Id); w.Float(scale); ToHost(Msg.ReqScale, false); }

        public void RequestInteract(NetEntity e) { w.Reset(); w.UShort(e.Id); ToHost(Msg.ReqInteract); }

        public void RequestExplosion(Vector3 pos, float radius, float damage, float push)
        {
            w.Reset(); w.Vec3(pos); w.Float(radius); w.Float(damage); w.Float(push); ToHost(Msg.ReqExplosion);
        }

        public void RequestFire(WeaponDef def, Vector3 origin, List<Vector3> dirs)
        {
            int netIndex = Mods.ModNet.ToNet(def);
            if (netIndex < 0) return; // modded weapon the host hasn't accepted (yet)
            w.Reset(); w.Byte((byte)netIndex); w.Vec3(origin); w.Byte((byte)dirs.Count);
            foreach (var d in dirs) w.Vec3(d);
            ToHost(Msg.ReqFire);
        }

        public void RequestAdmin(AdminGun.Mode mode, Vector3 origin, Vector3 dir)
        {
            w.Reset(); w.Byte((byte)mode); w.Vec3(origin); w.Vec3(dir); ToHost(Msg.ReqAdmin);
        }

        public void RequestHurtPlayer(RemotePlayer target, DamageInfo info)
        {
            if (target == null || target.Player == null) return;
            w.Reset(); w.Byte(target.Player.Id); w.Damage(info); ToHost(Msg.ReqHurtPlayer);
        }
    }
}
