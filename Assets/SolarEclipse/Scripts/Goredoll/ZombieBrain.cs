using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// The zombie mind. No fear, no emotions, no pain reactions: it perceives the nearest living prey (players, unless
    /// "AI ignores you" is on, and non-zombie Goredolls), shambles or sprints after it, bites in melee (players take
    /// damage through their normal IDamageable; bitten Goredolls are infected) and kneels over downed or dead
    /// Goredolls to eat them. Gunfire and shrieks pull it toward the sound. Runs only on the host / singleplayer.
    /// </summary>
    public sealed class ZombieBrain : MonoBehaviour
    {
        enum Mode { Idle, Wander, Investigate, Chase, Eat, Shriek }

        public ZombieKind Kind;
        public string Thought = "";
        public bool Eating => mode == Mode.Eat;

        Goredoll doll;
        ActiveRagdoll body;
        Mode mode = Mode.Idle;
        float think, modeTime, nextGroan, nextBite, nextPath, stuck, nextShriek, eatUntil, nextScare, wanderUntil;
        float speedWalk, speedChase, biteDamage, biteInterval, reach;
        Vector3 goal, investigatePos;
        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;

        // Current prey: either a player or a Goredoll.
        IPlayerTarget preyPlayer;
        Goredoll preyDoll;
        readonly HashSet<Goredoll> eaten = new HashSet<Goredoll>();

        public void Init(Goredoll d, ZombieKind kind)
        {
            doll = d;
            body = d.Ragdoll;
            Kind = kind;
            switch (kind)
            {
                case ZombieKind.Runner: speedWalk = 1.2f; speedChase = Random.Range(4.4f, 5.0f); biteDamage = 9f; biteInterval = 0.75f; reach = 1.25f; break;
                case ZombieKind.Crawler: speedWalk = 0.6f; speedChase = 1.0f; biteDamage = 12f; biteInterval = 1.0f; reach = 1.5f; break;
                case ZombieKind.Brute: speedWalk = 1.0f; speedChase = 1.9f; biteDamage = 26f; biteInterval = 1.5f; reach = 1.5f; break;
                case ZombieKind.Screamer: speedWalk = 1.1f; speedChase = 2.9f; biteDamage = 10f; biteInterval = 1.0f; reach = 1.25f; break;
                default: speedWalk = 0.8f; speedChase = Random.Range(1.2f, 1.6f); biteDamage = 13f; biteInterval = 1.1f; reach = 1.3f; break;
            }
            nextGroan = Time.time + Random.Range(1f, 6f);
            nextShriek = Time.time + 2f;
            GameEvents.Noise += OnNoise;
        }

        void OnDestroy() { GameEvents.Noise -= OnNoise; }

        Vector3 Pelvis => doll.Parts[PartType.Pelvis].Body.position;
        Vector3 HeadPos => doll.Parts.TryGetValue(PartType.Head, out var h) && h != null && !h.Severed ? h.transform.position : Pelvis + Vector3.up * 0.6f;
        bool Active => doll != null && !doll.NetProxy && NetState.Authority && !doll.IsDead;
        bool HasPrey => preyPlayer != null || preyDoll != null;

        Vector3 PreyPos
        {
            get
            {
                if (preyPlayer != null) return preyPlayer.Position;
                if (preyDoll != null) return preyDoll.Parts[PartType.Pelvis].Body.position;
                return Pelvis;
            }
        }

        // ------------------------------------------------------------------ events

        void OnNoise(NoiseEvent e)
        {
            if (!Active || Rules.FreezeAi) return;
            if (e.Source is Goredoll g && (g == doll || g.Zombie != null)) return;
            if (e.Threat < 0.45f) return;
            if (Vector3.Distance(e.Position, Pelvis) > e.Radius * 1.3f) return;
            if (mode == Mode.Chase || mode == Mode.Eat) return;
            investigatePos = e.Position;
            SetMode(Mode.Investigate);
        }

        /// <summary>A Screamer's shriek: go after its prey.</summary>
        public void Alert(IPlayerTarget player, Goredoll victim, Vector3 pos)
        {
            if (!Active || mode == Mode.Chase && HasPrey) return;
            if (player != null && !player.IsDead && !Rules.AiIgnorePlayer) { preyPlayer = player; preyDoll = null; SetMode(Mode.Chase); }
            else if (victim != null && !victim.IsDead) { preyDoll = victim; preyPlayer = null; SetMode(Mode.Chase); }
            else { investigatePos = pos; SetMode(Mode.Investigate); }
        }

        /// <summary>Hit by something: no pain or fear, but it turns on whoever did it.</summary>
        public void OnHurt(DamageInfo info)
        {
            if (!Active) return;
            var p = info.Source as IPlayerTarget ?? (info.Source is Weapon ? PlayerController.Instance : null);
            if (p != null && !p.IsDead && !Rules.AiIgnorePlayer && (preyPlayer == null || mode != Mode.Chase)) { preyPlayer = p; preyDoll = null; SetMode(Mode.Chase); }
        }

        // ------------------------------------------------------------------ perception

        static bool IsPrey(Goredoll g) => g != null && g.Zombie == null && !g.IsDead && g.Vitals != null;

        void Perceive()
        {
            var head = HeadPos;
            var fwd = doll.Parts[PartType.Chest].transform.forward;
            IPlayerTarget bestP = null; Goredoll bestD = null;
            float bd = float.MaxValue;
            if (!Rules.AiIgnorePlayer)
            {
                foreach (var p in PlayerTargets.All)
                {
                    if (p == null || p.IsDead) continue;
                    float d = Vector3.Distance(head, p.EyePosition);
                    if (d > 45f || d >= bd) continue;
                    if (!CanSee(head, fwd, p.EyePosition, d)) continue;
                    bd = d; bestP = p;
                }
            }
            foreach (var g in Goredoll.All)
            {
                if (!IsPrey(g) || g == doll) continue;
                var gp = g.Parts[PartType.Pelvis].Body.position;
                float d = Vector3.Distance(head, gp) + 1.5f; // players are slightly preferred
                if (d > 40f || d >= bd) continue;
                if (!CanSee(head, fwd, gp + Vector3.up * 0.4f, d)) continue;
                bd = d; bestD = g; bestP = null;
            }
            if (bestP != null || bestD != null)
            {
                bool fresh = !(mode == Mode.Chase && (bestP != null ? preyPlayer == bestP : preyDoll == bestD));
                // Keep the current prey unless the new one is clearly closer.
                if (mode == Mode.Chase && HasPrey && fresh && Vector3.Distance(Pelvis, PreyPos) < bd + 2f && PreyValid()) return;
                preyPlayer = bestP; preyDoll = bestD;
                if (mode != Mode.Chase) SetMode(Mode.Chase);
                if (fresh && Kind == ZombieKind.Screamer && Time.time > nextShriek) SetMode(Mode.Shriek);
                return;
            }
            // Nothing alive in sight: find something dead to eat.
            if (mode != Mode.Chase && mode != Mode.Eat)
            {
                Goredoll food = null; float fd = 25f;
                foreach (var g in Goredoll.All)
                {
                    if (g == null || g == doll || g.Zombie != null || eaten.Contains(g)) continue;
                    if (!g.IsDead && !(g.Ragdoll != null && g.Ragdoll.IsDown)) continue;
                    float d = Vector3.Distance(Pelvis, g.Parts[PartType.Pelvis].Body.position);
                    if (d < fd) { fd = d; food = g; }
                }
                if (food != null) { preyDoll = food; preyPlayer = null; SetMode(Mode.Chase); }
            }
        }

        bool CanSee(Vector3 head, Vector3 fwd, Vector3 target, float d)
        {
            if (d > 3f && d > 14f && Vector3.Angle(fwd, target - head) > 75f) return false;
            return !Physics.Linecast(head, target, Layers.SightMask, QueryTriggerInteraction.Ignore);
        }

        bool PreyValid()
        {
            if (preyPlayer != null) return !preyPlayer.IsDead && !Rules.AiIgnorePlayer;
            if (preyDoll != null) return preyDoll.Zombie == null && !eaten.Contains(preyDoll);
            return false;
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (doll == null || doll.NetProxy || !NetState.Authority) return;
            if (doll.IsDead) { body.DesiredVelocity = Vector3.zero; Thought = "dead (for good)"; return; }
            body.WantsToGetUp = true;
            body.WantsToCrawl = true;
            if (Rules.FreezeAi) { body.DesiredVelocity = Vector3.zero; Thought = "frozen"; return; }

            think -= Time.deltaTime;
            if (think <= 0f)
            {
                think = Random.Range(0.2f, 0.32f);
                if (!PreyValid()) { preyPlayer = null; preyDoll = null; if (mode == Mode.Chase || mode == Mode.Eat) SetMode(Mode.Wander); }
                Perceive();
            }
            Sounds();
            Act(Time.deltaTime);
        }

        void SetMode(Mode m)
        {
            if (m == Mode.Wander && NavGrid.Current != null && NavGrid.Current.RandomPoint(Pelvis, 12f, out var w)) PathTo(w);
            else if (m == Mode.Investigate) PathTo(investigatePos);
            else path.Clear();
            if (m == Mode.Chase && mode != Mode.Chase && Random.value < 0.6f) doll.Voice.Say("z_growl", 1f, false);
            if (m == Mode.Shriek)
            {
                nextShriek = Time.time + Random.Range(12f, 18f);
                doll.Voice.Say("z_shriek", 1f);
                GameEvents.EmitNoise(HeadPos, 40f, 0.2f, doll);
                foreach (var g in Goredoll.All)
                {
                    if (g == null || g == doll || g.Zombie == null || g.Zombie.Brain == null || g.IsDead) continue;
                    if (Vector3.Distance(g.Position, Pelvis) < 40f) g.Zombie.Brain.Alert(preyPlayer, preyDoll, PreyPos);
                }
            }
            if (m == Mode.Wander) wanderUntil = Time.time + Random.Range(4f, 9f);
            mode = m;
            modeTime = Time.time;
            stuck = 0f;
        }

        void PathTo(Vector3 target)
        {
            goal = target;
            path.Clear();
            if (NavGrid.Current == null || !NavGrid.Current.FindPath(Pelvis, target, path)) { path.Clear(); path.Add(target); }
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

        void Sounds()
        {
            if (Time.time < nextGroan) return;
            if (mode == Mode.Chase) { nextGroan = Time.time + Random.Range(2.2f, 4.5f); doll.Voice.Say(Random.value < 0.7f ? "z_growl" : "z_groan", 1f, false); }
            else if (mode != Mode.Eat) { nextGroan = Time.time + Random.Range(4f, 9f); doll.Voice.Say("z_groan", 0.85f, false); }
            else nextGroan = Time.time + 1f;
        }

        void Act(float dt)
        {
            Vector3 move = Vector3.zero;
            float speed = speedWalk;
            body.Pose = PoseMode.Idle;
            body.LookTarget = null;
            switch (mode)
            {
                case Mode.Idle:
                    Thought = "...";
                    if (Time.time - modeTime > Random.Range(2f, 5f)) SetMode(Mode.Wander);
                    break;
                case Mode.Wander:
                    Thought = "shambling";
                    move = FollowPath();
                    if (move == Vector3.zero || Time.time > wanderUntil) SetMode(Mode.Idle);
                    break;
                case Mode.Investigate:
                    Thought = "heard something";
                    move = FollowPath();
                    speed = Mathf.Lerp(speedWalk, speedChase, 0.6f);
                    if (move == Vector3.zero || Time.time - modeTime > 15f) SetMode(Mode.Wander);
                    break;
                case Mode.Shriek:
                    Thought = "SHRIEKING";
                    body.Pose = PoseMode.Brace;
                    if (HasPrey) { FaceTowards(PreyPos); body.LookTarget = PreyPos + Vector3.up * 1.5f; }
                    if (Time.time - modeTime > 1.6f) SetMode(HasPrey ? Mode.Chase : Mode.Wander);
                    break;
                case Mode.Chase:
                    if (!HasPrey) { SetMode(Mode.Wander); break; }
                    Chase(dt, ref move, ref speed);
                    break;
                case Mode.Eat:
                    Eat(dt);
                    break;
            }

            if (move.sqrMagnitude > 0.01f)
            {
                move = Avoid(move.normalized);
                // Lurch: speed pulses with the stride, plus a slight sideways sway.
                float lurch = Kind == ZombieKind.Runner ? 1f : 0.75f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 2.6f + Kind.GetHashCode()));
                var side = Vector3.Cross(Vector3.up, move) * Mathf.Sin(Time.time * 1.3f) * 0.15f;
                float s = speed * lurch * Rules.NpcSpeedMultiplier * LegFactor();
                body.DesiredVelocity = (move + side).normalized * s;
                if (mode != Mode.Chase) FaceTowards(Pelvis + move);
                if (body.Pose == PoseMode.Idle) body.Pose = s > 2.6f ? PoseMode.Run : PoseMode.Walk;
                if (MathX.Flat(RB.Vel(doll.Parts[PartType.Pelvis].Body)).magnitude < 0.15f && body.IsStanding) stuck += dt; else stuck = 0f;
                if (stuck > 2.5f) { stuck = 0f; nextPath = 0f; if (mode != Mode.Chase) SetMode(Mode.Wander); else goal = Pelvis + Random.insideUnitSphere * 3f; }
            }
            else body.DesiredVelocity = Vector3.zero;
        }

        float LegFactor()
        {
            if (Kind == ZombieKind.Crawler) return 1f;
            float f = 1f;
            foreach (var t in new[] { PartType.ThighL, PartType.ShinL, PartType.ThighR, PartType.ShinR })
                if (doll.Parts.TryGetValue(t, out var p) && p != null) f = Mathf.Min(f, Mathf.Max(0.4f, p.Function));
            return f;
        }

        void Chase(float dt, ref Vector3 move, ref float speed)
        {
            var target = PreyPos;
            var to = MathX.Flat(target - Pelvis);
            float dist = to.magnitude;
            speed = speedChase;
            FaceTowards(target);
            body.LookTarget = preyPlayer != null ? preyPlayer.EyePosition : target + Vector3.up * 0.3f;

            // Downed or dead Goredoll in reach: eat it.
            if (preyDoll != null && (preyDoll.IsDead || preyDoll.Ragdoll.IsDown) && dist < 1.4f)
            {
                eatUntil = Time.time + Random.Range(8f, 14f);
                nextBite = Time.time + 0.4f;
                SetMode(Mode.Eat);
                return;
            }
            // Panic the prey a little when we close in (Goredolls are scared of zombies).
            if (preyDoll != null && !preyDoll.IsDead && dist < 10f && Time.time > nextScare && preyDoll.Brain != null && preyDoll.Brain.enabled)
            {
                nextScare = Time.time + 2f;
                preyDoll.Brain.OnDamaged(new DamageInfo { Source = doll, Point = Pelvis, Amount = 0f }, 6f);
            }

            float attackRange = reach * (Kind == ZombieKind.Brute ? 1.1f : 1f) + (body.State == RagdollState.Crawling ? 0.2f : 0f);
            if (dist <= attackRange)
            {
                body.Pose = PoseMode.Punch;
                body.PunchProgress = Mathf.Clamp01(1f - (nextBite - Time.time) / biteInterval);
                Thought = "BITING";
                if (Time.time >= nextBite) { nextBite = Time.time + biteInterval * Random.Range(0.85f, 1.15f); Bite(); }
                if (dist > 0.7f) move = to.normalized * 0.4f;
                return;
            }
            Thought = preyPlayer != null ? "hungry for " + preyPlayer.PlayerName : "hunting " + preyDoll.DisplayName;
            // Straight at it when visible and close; otherwise follow the nav grid.
            bool direct = dist < 7f || !Physics.Linecast(HeadPos, target + Vector3.up * 0.5f, Layers.SightMask, QueryTriggerInteraction.Ignore);
            if (direct) { move = to.normalized; path.Clear(); }
            else
            {
                if (Time.time > nextPath || path.Count == 0) { nextPath = Time.time + 1.2f; PathTo(target); }
                move = FollowPath();
                if (move == Vector3.zero) move = to.normalized;
            }
        }

        void Bite()
        {
            doll.Voice.Say(Random.value < 0.5f ? "z_snarl" : "z_growl", 1f);
            var mouth = HeadPos + doll.Parts[PartType.Chest].transform.forward * 0.25f;
            if (preyPlayer != null)
            {
                var dmgTarget = preyPlayer.Damageable;
                if (dmgTarget == null || !dmgTarget.IsAlive) return;
                var dir = MathX.Flat(preyPlayer.Position - Pelvis).normalized;
                dmgTarget.TakeDamage(new DamageInfo
                {
                    Amount = biteDamage * Random.Range(0.85f, 1.15f), Force = dir * (Kind == ZombieKind.Brute ? 120f : 30f), Point = preyPlayer.EyePosition - Vector3.up * 0.3f,
                    Normal = -dir, Direction = dir, Type = DamageType.Blunt, Source = doll, Weapon = Zombies.Title(Kind) + " Zombie bite",
                });
                AudioManager.Play("z_bite", preyPlayer.EyePosition - Vector3.up * 0.3f, 0.9f, Random.Range(0.9f, 1.1f), 1f, 1.5f, 30f);
                if (Settings.GoreEnabled) BloodFX.Burst(preyPlayer.EyePosition - Vector3.up * 0.4f, -dir, 0.25f, Settings.BloodColor);
                return;
            }
            if (preyDoll == null) return;
            BiteDoll(preyDoll, Random.Range(0.8f, 1.2f) * biteDamage, true);
        }

        static readonly PartType[] BiteParts = { PartType.Neck, PartType.Chest, PartType.UpperArmL, PartType.UpperArmR, PartType.ForearmL, PartType.ForearmR, PartType.Abdomen, PartType.ThighL, PartType.ThighR };

        void BiteDoll(Goredoll victim, float amount, bool knockDown)
        {
            BodyPart part = null;
            for (int i = 0; i < 6 && part == null; i++)
            {
                var t = BiteParts[Random.Range(0, BiteParts.Length)];
                if (victim.Parts.TryGetValue(t, out var p) && p != null && !p.Severed && !p.Gibbed) part = p;
            }
            if (part == null && victim.Parts.TryGetValue(PartType.Chest, out var chest)) part = chest;
            if (part == null) return;
            victim.Bitten = true;
            var c = part.Col != null && part.Col.enabled ? part.Col.bounds.center : part.transform.position;
            var from = HeadPos;
            var pt = part.Col != null && part.Col.enabled ? part.Col.ClosestPoint(from) : c;
            var dir = (c - from).sqrMagnitude > 1e-4f ? (c - from).normalized : Vector3.down;
            part.TakeDamage(new DamageInfo
            {
                Amount = amount, Force = dir * (knockDown ? 25f : 4f), Point = pt, Normal = -dir, Direction = dir,
                Type = DamageType.Slash, Source = doll, Weapon = "Zombie bite",
            });
            AudioManager.Play("z_bite", pt, 1f, Random.Range(0.9f, 1.1f), 1f, 1.5f, 35f);
            if (Settings.GoreEnabled) BloodFX.Burst(pt, -dir, 0.5f, Settings.BloodColor);
            if (knockDown && !victim.IsDead && victim.Ragdoll != null && victim.Ragdoll.IsStanding && Random.value < (Kind == ZombieKind.Brute ? 0.8f : 0.3f)) victim.Ragdoll.Fall(2.5f);
        }

        void Eat(float dt)
        {
            Thought = "eating";
            if (preyDoll == null || preyDoll.Zombie != null) { SetMode(Mode.Wander); return; }
            var target = preyDoll.Parts[PartType.Chest].Body.position;
            var to = MathX.Flat(target - Pelvis);
            FaceTowards(target);
            body.LookTarget = target;
            body.Pose = PoseMode.Cower; // crouched over the body
            if (to.magnitude > 1.1f) body.DesiredVelocity = to.normalized * 0.6f;
            else body.DesiredVelocity = Vector3.zero;
            if (to.magnitude > 2.6f || (!preyDoll.IsDead && !preyDoll.Ragdoll.IsDown)) { SetMode(Mode.Chase); return; }
            if (Time.time >= nextBite)
            {
                nextBite = Time.time + Random.Range(0.8f, 1.3f);
                BiteDoll(preyDoll, preyDoll.IsDead ? 6f : 10f, false);
                AudioManager.Play("z_chew", HeadPos, 0.9f, Random.Range(0.9f, 1.1f), 1f, 1.5f, 30f);
                if (Settings.GoreEnabled) BloodFX.Mouth(doll);
            }
            if (Time.time > eatUntil)
            {
                eaten.Add(preyDoll);
                preyDoll = null;
                SetMode(Mode.Wander);
                return;
            }
            // Something alive close by interrupts the meal.
            if (!Rules.AiIgnorePlayer)
            {
                var p = PlayerTargets.Nearest(Pelvis, 6f);
                if (p != null && !p.IsDead) { preyPlayer = p; preyDoll = null; SetMode(Mode.Chase); }
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
            return -dir;
        }

        void FaceTowards(Vector3 point)
        {
            var to = MathX.Flat(point - Pelvis);
            if (to.sqrMagnitude > 0.01f) body.DesiredYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }
    }
}
