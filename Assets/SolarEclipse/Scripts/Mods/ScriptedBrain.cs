using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse.Mods
{
    /// <summary>
    /// The mind of a modded NPC: runs its SandScript (host / singleplayer only) and turns the script's commands into
    /// ActiveRagdoll movement, punches, kicks and gunfire. Replaces the normal GoredollBrain (which is switched off).
    /// Every number coming from the script is clamped here (anti-cheat), and every script error is caught.
    /// </summary>
    public sealed class ScriptedBrain : MonoBehaviour, ISandHost
    {
        enum Mode { Idle, Wander, Chase, Flee, MoveTo, Stop }

        public string ModId = "", ModName = "";
        public SandRuntime Runtime;
        public string Thought = "";

        Goredoll doll;
        ActiveRagdoll body;
        NpcModData data;
        Mode mode = Mode.Idle;
        float hp, maxHp, speed, damage;
        int team;
        object target, attacker, fleeFrom;
        Vector3 moveGoal;
        Vector3? lookPoint;
        float modeTime, nextPerceive, lastSeenTarget, crouchUntil, ragdollUntil, punchUntil, nextMelee, nextShot, wanderUntil, nextPath, stuck;
        bool kickPunch, deathFired, wasDead;
        float lastPartHealth;
        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;
        readonly HashSet<object> seen = new HashSet<object>();
        readonly Dictionary<SandHandler, bool> nearState = new Dictionary<SandHandler, bool>();
        float deathTime;

        // Weapon
        WeaponDef gun;
        WeaponDef shotDef;
        int gunIndex = -1, ammo;
        float reloadUntil;

        // Rate limits (anti-spam)
        float nextSay, nextSound, nextExplode, nextEffect, nextJump;

        // Entity handles for the script
        readonly List<object> ents = new List<object>();

        public static readonly List<ScriptedBrain> All = new List<ScriptedBrain>();

        public void Init(Goredoll d, NpcModData def, SandProgram program, string modId, string modName)
        {
            doll = d; body = d.Ragdoll; data = def; ModId = modId; ModName = modName;
            maxHp = Mathf.Clamp(def.health, NpcModData.MinHealth, NpcModData.MaxHealth);
            hp = maxHp;
            speed = Mathf.Clamp(def.speed, 0.5f, (float)SandApi.CapSpeed);
            damage = Mathf.Clamp(10f * def.strength, 1f, (float)SandApi.CapDamage);
            team = Mathf.Clamp(def.team, 0, 2);
            if (d.Brain != null) d.Brain.enabled = false;
            var armed = GetComponent<ArmedBrain>();
            if (armed != null) armed.enabled = false;
            // Tougher (or frailer) bodies for high (or low) health.
            float k = Mathf.Clamp(maxHp / 100f, 0.5f, 5f);
            foreach (var p in d.Parts.Values) if (p != null) { p.MaxHealth *= k; p.Health *= k; }
            lastPartHealth = PartHealth();
            SetWeapon(def.weapon);
            d.Damaged += OnDamaged;
            if (program != null) Runtime = new SandRuntime(program, this, d.Seed);
            All.Add(this);
            Fire("spawn", null);
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (doll != null) doll.Damaged -= OnDamaged;
        }

        bool Authority => doll != null && !doll.NetProxy && NetState.Authority;
        Vector3 Pelvis => doll.Parts[PartType.Pelvis].Body.position;
        Vector3 HeadPos => doll.Parts.TryGetValue(PartType.Head, out var h) && h != null && !h.Severed ? h.transform.position : Pelvis + Vector3.up * 0.6f;
        public float Health => hp;
        public bool Hostile => team == 1;

        void Fire(string evt, Dictionary<string, SVal> ctx)
        {
            if (Runtime == null || !Authority) return;
            try { Runtime.Fire(evt, ctx); }
            catch (Exception e) { Debug.LogWarning("[SandScript " + ModName + "] " + e.Message); }
        }

        // ------------------------------------------------------------------ weapon

        void SetWeapon(string id)
        {
            gun = null; shotDef = null; gunIndex = -1;
            if (string.IsNullOrEmpty(id)) return;
            var def = WeaponCatalog.Get(id);
            if (def == null || !NpcModData.WeaponAllowed(def)) return;
            gun = def;
            gunIndex = WeaponCatalog.All.IndexOf(def);
            if (def.Kind == WeaponKind.Firearm)
            {
                shotDef = new WeaponDef
                {
                    Id = def.Id, Name = def.Name, Kind = def.Kind, Damage = Mathf.Min(def.Damage, (float)SandApi.CapDamage), Penetration = def.Penetration,
                    Force = def.Force, Caliber = def.Caliber, Range = Mathf.Min(def.Range, 120f), Pellets = 1, FireSound = def.FireSound,
                };
                ammo = Mathf.Max(1, def.Mag);
            }
        }

        bool HasGun => gun != null && gun.Kind == WeaponKind.Firearm;

        // ------------------------------------------------------------------ damage / death

        float PartHealth()
        {
            float s = 0;
            foreach (var p in doll.Parts.Values) if (p != null && !p.Severed) s += Mathf.Max(0, p.Health);
            return s;
        }

        void OnDamaged(DamageInfo info, float dmg)
        {
            if (!Authority || doll.IsDead) return;
            var src = info.Source;
            if (src is Weapon) src = PlayerController.Instance;
            if (src is BodyPart bp) src = bp.Doll;
            if (src is IPlayerTarget || (src is Goredoll g && g != doll)) attacker = src;
            hp -= Mathf.Max(0, dmg);
            if (hp <= 0f) { hp = 0f; doll.Kill(ModName + " ran out of health"); return; }
            Fire("hurt", new Dictionary<string, SVal> { { "damage_taken", SVal.Num(Math.Round(dmg, 1)) } });
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (doll == null || !Authority) return;
            if (doll.IsDead)
            {
                body.DesiredVelocity = Vector3.zero;
                if (!deathFired) { deathFired = true; deathTime = Time.time; Fire("death", null); }
                // Let the death event finish (e.g. "wait 1" then "explode"), then stop.
                if (Time.time - deathTime < 6f) TickScript();
                Thought = "dead";
                return;
            }
            body.WantsToGetUp = Time.time > ragdollUntil;
            body.WantsToCrawl = true;
            if (Rules.FreezeAi) { body.DesiredVelocity = Vector3.zero; Thought = "frozen"; return; }

            if (Time.time >= nextPerceive) { nextPerceive = Time.time + 0.25f; Perceive(); }
            TickScript();
            Act(Time.deltaTime);
        }

        void TickScript()
        {
            if (Runtime == null) return;
            try
            {
                // "near player N" edges
                foreach (var h in Runtime.Program.Handlers)
                {
                    if (h.Event != "near player") continue;
                    var p = PlayerTargets.Nearest(Pelvis, (float)h.Number);
                    bool near = p != null && !p.IsDead && !Rules.AiIgnorePlayer;
                    nearState.TryGetValue(h, out var was);
                    nearState[h] = near;
                    if (near && !was) Runtime.Start(h, null);
                }
                Runtime.Tick();
            }
            catch (Exception e) { Debug.LogWarning("[SandScript " + ModName + "] " + e.Message); }
        }

        void Perceive()
        {
            if (!TargetValid(target))
            {
                if (target != null) { target = null; Fire("lose target", null); }
            }
            else if (CanSee(target)) lastSeenTarget = Time.time;
            else if (Time.time - lastSeenTarget > 6f) { target = null; Fire("lose target", null); }

            if (!Rules.AiIgnorePlayer)
                foreach (var p in PlayerTargets.All)
                {
                    if (p == null || p.IsDead || seen.Contains(p)) continue;
                    if (Vector3.Distance(HeadPos, p.EyePosition) > 40f || !CanSee(p)) continue;
                    seen.Add(p);
                    if (target == null) { target = p; lastSeenTarget = Time.time; }
                    Fire("see player", null);
                    return;
                }
            foreach (var g in Goredoll.All)
            {
                if (g == null || g == doll || g.IsDead || seen.Contains(g)) continue;
                if (Vector3.Distance(Pelvis, g.Position) > 30f || !CanSee(g)) continue;
                seen.Add(g);
                if (target == null) { target = g; lastSeenTarget = Time.time; }
                Fire("see npc", null);
                return;
            }
        }

        static bool TargetValid(object t)
        {
            if (t is IPlayerTarget p) return p != null && !p.IsDead && !(p is UnityEngine.Object o && o == null) && !Rules.AiIgnorePlayer;
            if (t is Goredoll g) return g != null && !g.IsDead;
            return false;
        }

        Vector3 PosOf(object t)
        {
            if (t is IPlayerTarget p) return p.Position;
            if (t is Goredoll g && g != null) return g.Position;
            return Pelvis;
        }

        Vector3 AimPosOf(object t)
        {
            if (t is IPlayerTarget p) return p.EyePosition - Vector3.up * 0.25f;
            if (t is Goredoll g && g != null && g.Parts.TryGetValue(PartType.Chest, out var c) && c != null) return c.transform.position;
            return PosOf(t);
        }

        bool CanSee(object t)
        {
            if (t == null) return false;
            var to = AimPosOf(t);
            var head = HeadPos;
            if (Vector3.Distance(head, to) < 2.5f) return true;
            var fwd = doll.Parts[PartType.Chest].transform.forward;
            if (Vector3.Angle(fwd, to - head) > 80f && Vector3.Distance(head, to) > 8f) return false;
            return !Physics.Linecast(head, to, Layers.SightMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ movement

        void SetMode(Mode m) { mode = m; modeTime = Time.time; stuck = 0; path.Clear(); }

        void PathTo(Vector3 t)
        {
            path.Clear();
            if (NavGrid.Current == null || !NavGrid.Current.FindPath(Pelvis, t, path)) { path.Clear(); path.Add(t); }
            pathIndex = path.Count > 1 ? 1 : 0;
        }

        Vector3 FollowPath()
        {
            if (path.Count == 0 || pathIndex >= path.Count) return Vector3.zero;
            var to = MathX.Flat(path[pathIndex] - Pelvis);
            if (to.magnitude < 0.7f)
            {
                pathIndex++;
                if (pathIndex >= path.Count) return Vector3.zero;
                to = MathX.Flat(path[pathIndex] - Pelvis);
            }
            return to.normalized;
        }

        Vector3 Toward(Vector3 goal, float stopAt)
        {
            var to = MathX.Flat(goal - Pelvis);
            if (to.magnitude <= stopAt) return Vector3.zero;
            bool direct = to.magnitude < 7f || !Physics.Linecast(HeadPos, goal + Vector3.up * 0.5f, Layers.SightMask, QueryTriggerInteraction.Ignore);
            if (direct) { path.Clear(); return to.normalized; }
            if (Time.time > nextPath || path.Count == 0) { nextPath = Time.time + 1.2f; PathTo(goal); }
            var m = FollowPath();
            return m == Vector3.zero ? to.normalized : m;
        }

        void Act(float dt)
        {
            Vector3 move = Vector3.zero;
            float s = speed;
            body.Pose = PoseMode.Idle;
            body.LookTarget = lookPoint;
            switch (mode)
            {
                case Mode.Idle: Thought = "..."; break;
                case Mode.Stop: Thought = "standing still"; break;
                case Mode.Wander:
                    Thought = "wandering";
                    s = Mathf.Min(speed, 1.4f);
                    move = FollowPath();
                    if (move == Vector3.zero || Time.time > wanderUntil)
                    {
                        if (NavGrid.Current != null && NavGrid.Current.RandomPoint(Pelvis, 12f, out var w)) PathTo(w);
                        else { path.Clear(); path.Add(Pelvis + new Vector3(UnityEngine.Random.Range(-8f, 8f), 0, UnityEngine.Random.Range(-8f, 8f))); pathIndex = 0; }
                        wanderUntil = Time.time + UnityEngine.Random.Range(4f, 9f);
                    }
                    break;
                case Mode.Chase:
                    if (!TargetValid(target)) { SetMode(Mode.Idle); break; }
                    Thought = "chasing " + NameOf(target);
                    FaceTowards(PosOf(target));
                    body.LookTarget = AimPosOf(target);
                    move = Toward(PosOf(target), HasGun ? 6f : 1.1f);
                    break;
                case Mode.Flee:
                    {
                        var from = TargetValid(fleeFrom) ? fleeFrom : target;
                        if (!TargetValid(from)) { SetMode(Mode.Wander); break; }
                        Thought = "fleeing";
                        var away = MathX.Flat(Pelvis - PosOf(from));
                        if (away.magnitude > 35f) { SetMode(Mode.Idle); break; }
                        move = Avoid(away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward);
                        break;
                    }
                case Mode.MoveTo:
                    Thought = "walking";
                    move = Toward(moveGoal, 0.8f);
                    if (move == Vector3.zero) SetMode(Mode.Idle);
                    break;
            }

            if (Time.time < crouchUntil) { body.Pose = PoseMode.Cower; move *= 0.3f; }
            if (Time.time < punchUntil)
            {
                body.Pose = PoseMode.Punch;
                body.PunchProgress = Mathf.Clamp01(1f - (punchUntil - Time.time) / (kickPunch ? 0.5f : 0.35f));
            }

            bool aiming = HasGun && TargetValid(target) && Time.time < nextShot + 1.5f;
            body.AimWeight = Mathf.MoveTowards(body.AimWeight, aiming ? 1f : 0f, dt * 4f);
            if (aiming)
            {
                var d = AimPosOf(target) - HeadPos;
                body.AimPitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            }

            if (move.sqrMagnitude > 0.01f && !body.IsDown)
            {
                float v = s * Rules.NpcSpeedMultiplier;
                body.DesiredVelocity = move.normalized * v;
                if (mode != Mode.Chase) FaceTowards(Pelvis + move);
                if (body.Pose == PoseMode.Idle) body.Pose = v > 2.6f ? PoseMode.Run : PoseMode.Walk;
                if (MathX.Flat(RB.Vel(doll.Parts[PartType.Pelvis].Body)).magnitude < 0.15f && body.IsStanding) stuck += dt; else stuck = 0f;
                if (stuck > 2.5f) { stuck = 0f; nextPath = 0f; path.Clear(); }
            }
            else
            {
                body.DesiredVelocity = Vector3.zero;
                if (lookPoint.HasValue) FaceTowards(lookPoint.Value);
            }
        }

        Vector3 Avoid(Vector3 dir)
        {
            var origin = Pelvis + Vector3.up * -0.3f;
            int mask = Layers.Mask(Layers.Default, Layers.Props);
            if (!Physics.SphereCast(origin, 0.25f, dir, out _, 1.1f, mask, QueryTriggerInteraction.Ignore)) return dir;
            foreach (float a in new[] { 40f, -40f, 75f, -75f, 110f, -110f })
            {
                var d = Quaternion.Euler(0, a, 0) * dir;
                if (!Physics.SphereCast(origin, 0.25f, d, out _, 1.1f, mask, QueryTriggerInteraction.Ignore)) return d;
            }
            return dir;
        }

        void FaceTowards(Vector3 point)
        {
            var to = MathX.Flat(point - Pelvis);
            if (to.sqrMagnitude > 0.01f) body.DesiredYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }

        static string NameOf(object t) => t is IPlayerTarget p ? p.PlayerName : t is Goredoll g && g != null ? g.DisplayName : "someone";

        // ------------------------------------------------------------------ combat

        bool CanHurt(object t)
        {
            if (t is IPlayerTarget) return team != 2 && !Rules.AiIgnorePlayer; // friendly mods never hurt players
            return t is Goredoll g && g != null && g != doll;
        }

        void Melee(object t, bool kick)
        {
            if (Time.time < nextMelee || body.IsDown || !doll.Vitals.Conscious) return;
            t = t ?? target;
            nextMelee = Time.time + (kick ? 0.9f : 0.55f);
            kickPunch = kick;
            punchUntil = Time.time + (kick ? 0.5f : 0.35f);
            if (t == null) { AudioManager.Play("whoosh_small", HeadPos, 0.6f, 1f, 1f, 1f, 15f); return; }
            FaceTowards(PosOf(t));
            float reach = gun != null && !HasGun ? 1.9f : kick ? 1.7f : 1.5f;
            if (Vector3.Distance(MathX.Flat(PosOf(t)), MathX.Flat(Pelvis)) > reach || !CanHurt(t))
            { AudioManager.Play("whoosh_small", HeadPos, 0.6f, 1f, 1f, 1f, 15f); return; }
            bool blade = gun != null && (gun.Kind == WeaponKind.Knife || gun.Kind == WeaponKind.Melee);
            float amount = Mathf.Min((float)SandApi.CapDamage, (blade ? Mathf.Max(damage, gun.Damage * 0.7f) : damage) * (kick ? 1.4f : 1f));
            var dir = MathX.Flat(PosOf(t) - Pelvis).normalized;
            var info = new DamageInfo
            {
                Amount = amount, Force = dir * (kick ? 90f : 40f), Normal = -dir, Direction = dir,
                Type = blade ? DamageType.Slash : DamageType.Blunt, Source = doll, Weapon = ModName + (blade ? "'s " + gun.Name : kick ? "'s kick" : "'s fist"),
            };
            if (t is IPlayerTarget p)
            {
                info.Point = p.EyePosition - Vector3.up * 0.4f;
                info.Amount *= 0.6f;
                p.Damageable?.TakeDamage(info);
            }
            else if (t is Goredoll g)
            {
                var part = g.Parts.TryGetValue(kick ? PartType.Abdomen : PartType.Chest, out var bp) && bp != null && !bp.Severed ? bp : g.Parts[PartType.Pelvis];
                info.Point = part.transform.position;
                part.TakeDamage(info);
            }
            AudioManager.Play(blade ? "knife_hit_hard" : "punch_hit", info.Point, 1f, UnityEngine.Random.Range(0.9f, 1.1f), 1f, 1.5f, 30f);
        }

        void Shoot(object t)
        {
            t = t ?? target;
            if (!HasGun || t == null || !CanHurt(t) || body.IsDown || !doll.Vitals.Conscious) return;
            if (Time.time < nextShot || Time.time < reloadUntil) return;
            if (ammo <= 0) { ammo = Mathf.Max(1, gun.Mag); reloadUntil = Time.time + Mathf.Max(1f, gun.ReloadTime); return; }
            nextShot = Time.time + Mathf.Max(0.15f, 60f / Mathf.Max(60f, gun.Rpm));
            ammo--;
            FaceTowards(PosOf(t));
            var hand = doll.Parts.TryGetValue(PartType.HandR, out var hr) && hr != null && !hr.Severed ? hr.transform.position : HeadPos;
            var origin = hand + Vector3.up * 0.05f;
            var aimAt = AimPosOf(t);
            float spread = 1.5f + Vector3.Distance(origin, aimAt) * 0.12f;
            var dir = Quaternion.Euler(UnityEngine.Random.Range(-spread, spread), UnityEngine.Random.Range(-spread, spread), 0) * (aimAt - origin).normalized;
            AudioManager.Play(gun.FireSound ?? "gun_glock", origin, 1f, UnityEngine.Random.Range(0.95f, 1.05f), 1f, 3f, 150f);
            GameEvents.EmitNoise(origin, gun.NoiseRadius, 0.9f, doll);
            if (t is IPlayerTarget p)
            {
                float d = Vector3.Distance(origin, aimAt);
                bool hit = Vector3.Angle(dir, aimAt - origin) < 2.2f && !Physics.Linecast(origin, aimAt, Layers.ShootMask, QueryTriggerInteraction.Ignore);
                Tracers.Spawn(origin, origin + dir * Mathf.Min(d, 60f), new Color(1f, 0.85f, 0.5f));
                if (hit)
                    p.Damageable?.TakeDamage(new DamageInfo
                    {
                        Amount = shotDef.Damage * 0.6f, Force = dir * shotDef.Force * 0.3f, Point = aimAt, Normal = -dir, Direction = dir,
                        Type = DamageType.Bullet, Source = doll, Weapon = ModName + "'s " + gun.Name, Caliber = gun.Caliber,
                    });
            }
            else Ballistics.Fire(origin, dir, shotDef, doll, origin);
            if (NetState.IsHost && gunIndex >= 0) NetManager.I.BroadcastFire(255, gunIndex, origin, new List<Vector3> { dir });
        }

        // ------------------------------------------------------------------ ISandHost

        double ISandHost.Now => Time.time;

        int ISandHost.Entity(string word)
        {
            object o = null;
            switch (word)
            {
                case "self": o = doll; break;
                case "target": o = TargetValid(target) ? target : null; break;
                case "attacker": o = TargetValid(attacker) ? attacker : null; break;
                case "player": o = Rules.AiIgnorePlayer ? null : PlayerTargets.Nearest(Pelvis, 80f); break;
                case "npc":
                    {
                        float bd = 40f;
                        foreach (var g in Goredoll.All)
                        {
                            if (g == null || g == doll || g.IsDead) continue;
                            float d = Vector3.Distance(g.Position, Pelvis);
                            if (d < bd) { bd = d; o = g; }
                        }
                        break;
                    }
            }
            return Handle(o);
        }

        int Handle(object o)
        {
            if (o == null) return 0;
            int i = ents.IndexOf(o);
            if (i >= 0) return i + 1;
            if (ents.Count >= 64) ents.Clear();
            ents.Add(o);
            return ents.Count;
        }

        object Ent(SVal v)
        {
            int id = v.EntId;
            if (id <= 0 || id > ents.Count) return null;
            var o = ents[id - 1];
            if (o is UnityEngine.Object uo && uo == null) return null;
            return o;
        }

        bool ISandHost.GetProp(string name, out SVal v)
        {
            switch (name)
            {
                case "health": v = SVal.Num(Mathf.Round(hp)); return true;
                case "max_health": v = SVal.Num(maxHp); return true;
                case "speed": v = SVal.Num(speed); return true;
                case "damage": v = SVal.Num(damage); return true;
                case "team": v = SVal.Str(SandApi.Teams[team]); return true;
                case "color": v = SVal.Str(""); return true;
                case "time": v = SVal.Num(Time.time); return true;
                case "has_target": v = SVal.Bool(TargetValid(target)); return true;
                case "ammo": v = SVal.Num(HasGun ? ammo : 0); return true;
                case "damage_taken": v = SVal.Zero; return true;
            }
            v = SVal.Zero;
            return false;
        }

        void ISandHost.SetProp(string name, SVal v)
        {
            var spec = SandApi.FindProp(name);
            float n = spec != null ? (float)SandApi.Clamp(v.AsNum, spec.Min, spec.Max) : 0f;
            switch (name)
            {
                case "speed": speed = n; break;
                case "max_health": maxHp = n; hp = Mathf.Min(hp, maxHp); break;
                case "health": hp = Mathf.Clamp(n, 0, maxHp); if (hp <= 0) doll.Kill(ModName + "'s script"); break;
                case "damage": damage = n; break;
                case "team":
                    {
                        int i = Array.IndexOf(SandApi.Teams, v.ToString().ToLowerInvariant());
                        if (i >= 0) team = i;
                        break;
                    }
                case "color":
                    {
                        var c = NpcModData.NamedColor(v.ToString());
                        if (c.HasValue) Tint(c.Value);
                        break;
                    }
            }
        }

        void Tint(Color c)
        {
            foreach (var t in new[] { PartType.Chest, PartType.Abdomen, PartType.UpperArmL, PartType.UpperArmR })
                if (doll.Parts.TryGetValue(t, out var p) && p != null && p.Renderer != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    p.Renderer.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", c);
                    mpb.SetColor("_Color", c);
                    p.Renderer.SetPropertyBlock(mpb);
                }
        }

        void ISandHost.Log(string text) => ModScriptLog.Add(ModName, text);

        SVal ISandHost.Call(string name, SVal[] a)
        {
            object E(int i) => i < a.Length ? Ent(a[i]) : null;
            double N(int i, double min, double max, double def = 0) => i < a.Length ? SandApi.Clamp(a[i].AsNum, min, max) : def;
            switch (name)
            {
                // ---- queries
                case "distance": { var o = E(0); return SVal.Num(o == null ? 999 : Math.Round(Vector3.Distance(PosOf(o), Pelvis), 2)); }
                case "is_dead": { var o = E(0); return SVal.Bool(o == null || !TargetValid(o)); }
                case "health_of":
                    {
                        var o = E(0);
                        if (o is Goredoll g) { var sb = g.GetComponent<ScriptedBrain>(); return SVal.Num(g.IsDead ? 0 : sb != null ? Mathf.Round(sb.hp) : Mathf.Round(100f * (1f - g.Vitals.BloodLossFraction))); }
                        if (o is PlayerController pc) return SVal.Num(pc.GetComponent<PlayerHealth>() is PlayerHealth ph && ph != null ? Mathf.Round(ph.Current) : 100);
                        return SVal.Num(o == null ? 0 : 100);
                    }
                case "can_see": return SVal.Bool(CanSee(E(0)));
                // ---- movement
                case "chase": { var o = E(0); if (o != null && o != (object)doll) { target = o; lastSeenTarget = Time.time; if (mode != Mode.Chase) SetMode(Mode.Chase); } break; }
                case "target": { var o = E(0); if (o != null && o != (object)doll) { target = o; lastSeenTarget = Time.time; } break; }
                case "flee": { fleeFrom = E(0) ?? target; if (mode != Mode.Flee) SetMode(Mode.Flee); break; }
                case "move_to": { var o = E(0); if (o != null) { moveGoal = PosOf(o); SetMode(Mode.MoveTo); } break; }
                case "wander": if (mode != Mode.Wander) { SetMode(Mode.Wander); wanderUntil = 0; } break;
                case "stop": SetMode(Mode.Stop); body.DesiredVelocity = Vector3.zero; break;
                case "look_at": { var o = E(0); lookPoint = o != null ? AimPosOf(o) : (Vector3?)null; if (o != null) FaceTowards(PosOf(o)); break; }
                case "jump":
                    if (Time.time >= nextJump && body.IsStanding)
                    {
                        nextJump = Time.time + 1.2f;
                        float j = (float)N(0, 1, SandApi.CapJump, 4);
                        doll.Launch(Vector3.up * j + RB.Vel(doll.Parts[PartType.Pelvis].Body) * 0.3f);
                    }
                    break;
                case "crouch": crouchUntil = Time.time + (float)N(0, 0.2, 10, 1.5); break;
                // ---- combat
                case "attack":
                    {
                        var o = E(0) ?? target;
                        if (o == null) break;
                        if (HasGun && Vector3.Distance(PosOf(o), Pelvis) > 2f) Shoot(o); else Melee(o, false);
                        break;
                    }
                case "punch": Melee(E(0), false); break;
                case "kick": Melee(E(0), true); break;
                case "shoot": Shoot(E(0)); break;
                case "give_weapon": SetWeapon(a.Length > 0 ? a[0].ToString().ToLowerInvariant() : ""); NpcModKind.MountWeapon(doll, gun != null ? gun.Id : null); break;
                case "explode":
                    if (Time.time >= nextExplode)
                    {
                        nextExplode = Time.time + 3f;
                        float r = (float)N(0, 0.5, SandApi.CapExplode, 2);
                        Explosion.Create(Pelvis, r, 30f + r * 15f, doll, 0.8f);
                    }
                    break;
                case "ragdoll": { float t = (float)N(0, 0.2, SandApi.CapRagdoll, 2); ragdollUntil = Time.time + t; body.Fall(t); break; }
                case "heal":
                    {
                        float n = (float)N(0, 0, SandApi.CapHeal);
                        hp = Mathf.Min(maxHp, hp + n);
                        foreach (var p in doll.Parts.Values) if (p != null && !p.Severed) p.Health = Mathf.Min(p.MaxHealth, p.Health + n * 0.2f);
                        break;
                    }
                // ---- talk / fx
                case "say":
                    if (Time.time >= nextSay && a.Length > 0) { nextSay = Time.time + 0.5f; SpeechBubbles.Say(doll.transform, HeadPos, ModChat.Clean(a[0].ToString())); }
                    break;
                case "play_sound":
                    if (Time.time >= nextSound && a.Length > 0) { nextSound = Time.time + 0.3f; ModSounds.Play(a[0].ToString(), HeadPos, doll); }
                    break;
                case "spawn_effect":
                    if (Time.time >= nextEffect && a.Length > 0) { nextEffect = Time.time + 0.1f; ModSounds.Effect(a[0].ToString(), HeadPos - Vector3.up * 0.3f, Vector3.up); }
                    break;
            }
            return SVal.Zero;
        }
    }
}
