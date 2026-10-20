using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum AiAction { Idle, Wander, Investigate, Watch, Greet, Flee, Hide, Surrender, Fight, Panic, Crawl, PlayDead, Dance }

    /// <summary>
    /// The Goredoll's mind. Perceives the player (sight cone + line of sight, being aimed at), hears gunfire and
    /// noises, remembers threats and who hurt it, and has a personality (bravery, aggression, curiosity).
    /// Utility scoring with hysteresis picks an action: wander, investigate, watch, greet, flee, hide, surrender,
    /// fight back, panic, crawl away or play dead. Movement uses the nav grid with A* paths and obstacle avoidance.
    /// </summary>
    public sealed class GoredollBrain : MonoBehaviour
    {
        public float Bravery, Aggression, Curiosity, Sociability;
        public float Fear, Anger;
        public AiAction Action = AiAction.Idle;
        public string Thought = "";
        public bool ForceDance;
        /// <summary>"Hostile" preset (horde mode): always angry, never scared, hunts the nearest player.</summary>
        public bool Hostile;

        Goredoll doll;
        ActiveRagdoll body;
        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;
        Vector3 threatPos, noisePos, goal;
        float threatTime = -99f, noiseTime = -99f, noiseThreat, hurtTime = -99f, thinkTimer, actionTime, stuckTimer, punchTimer, greetCooldown, nextScream, playDeadUntil;
        bool seesPlayer, aimedAt, greeted;
        float playerDist = 999f;
        Transform fightTarget;
        IDamageable fightDamageable;
        readonly HashSet<Goredoll> seenBodies = new HashSet<Goredoll>();

        public bool IsCalm => Fear < 0.2f && Action != AiAction.Flee && Action != AiAction.Panic;
        public bool Running => Action == AiAction.Flee || Action == AiAction.Panic || Action == AiAction.Fight;
        public bool AwareOfPlayer => seesPlayer || Time.time - threatTime < 6f;

        public void Init(Goredoll d)
        {
            doll = d;
            body = d.Ragdoll;
            Bravery = Random.value; Aggression = Mathf.Pow(Random.value, 1.6f); Curiosity = Random.value; Sociability = Random.value;
            GameEvents.Noise += OnNoise;
        }

        void OnDestroy() { GameEvents.Noise -= OnNoise; }

        public void ApplyPreset(string preset)
        {
            switch (preset)
            {
                case "Coward": Bravery = 0.05f; Aggression = 0f; break;
                case "Aggressive": Bravery = 0.8f; Aggression = 0.95f; Anger = 0.5f; break;
                case "Brave": Bravery = 0.95f; Aggression = 0.4f; break;
                case "Passive": Bravery = 0.5f; Aggression = 0f; Curiosity = 0f; break;
                case "Hostile": Bravery = 1f; Aggression = 1f; Anger = 1f; Hostile = true; break;
            }
        }

        Vector3 Pelvis => doll.Parts[PartType.Pelvis].Body.position;
        Vector3 HeadPos => doll.Parts.TryGetValue(PartType.Head, out var h) && h != null && !h.Severed ? h.transform.position + Vector3.up * 0.1f : Pelvis + Vector3.up * 0.7f;

        /// <summary>The player this Goredoll is paying attention to (nearest living one; any player in multiplayer).</summary>
        IPlayerTarget focus;
        float focusPick;
        bool IgnorePlayer => Rules.AiIgnorePlayer || focus == null || focus.IsDead;

        void PickFocus()
        {
            if (Time.time < focusPick && focus != null && !focus.IsDead) return;
            focusPick = Time.time + 0.5f;
            focus = PlayerTargets.Nearest(Pelvis, 80f);
        }

        // ------------------------------------------------------------------ perception

        void OnNoise(NoiseEvent e)
        {
            if (doll == null || !doll.Vitals.Conscious || Rules.FreezeAi) return;
            if (e.Source is Goredoll g && g == doll) return;
            float d = Vector3.Distance(e.Position, Pelvis);
            if (d > e.Radius) return;
            float k = 1f - d / e.Radius;
            if (e.Threat > noiseThreat || Time.time - noiseTime > 3f) { noisePos = e.Position; noiseThreat = e.Threat; noiseTime = Time.time; }
            if (e.Threat >= 0.5f)
            {
                Fear = Mathf.Clamp01(Fear + k * 0.35f * (1.15f - Bravery));
                if (PlayerTargets.IsPlayerSource(e.Source)) { threatPos = e.Position; threatTime = Time.time; }
            }
        }

        public void OnDamaged(DamageInfo info, float damage)
        {
            hurtTime = Time.time;
            Fear = Mathf.Clamp01(Fear + damage / 55f * (1.25f - Bravery));
            Anger = Mathf.Clamp01(Anger + damage / 70f * (0.3f + Aggression));
            var p = info.Source as IPlayerTarget ?? (info.Source is Weapon ? PlayerController.Instance : null);
            if (p != null) { threatPos = p.Position; threatTime = Time.time; focus = p; focusPick = Time.time + 4f; }
            else if (info.Source is Component c) { threatPos = c.transform.position; threatTime = Time.time; }
            if (Action == AiAction.Surrender && Random.value < 0.6f) SetAction(AiAction.Panic);
            if (Action == AiAction.PlayDead) playDeadUntil = 0;
        }

        void Perceive()
        {
            seesPlayer = false; aimedAt = false; playerDist = 999f;
            PickFocus();
            var p = focus;
            if (!IgnorePlayer)
            {
                var eye = p.EyePosition;
                var head = HeadPos;
                playerDist = Vector3.Distance(head, eye);
                var fwd = doll.Parts[PartType.Chest].transform.forward;
                var to = eye - head;
                bool inCone = playerDist < 3f || (playerDist < 45f && Vector3.Angle(fwd, to) < 60f);
                if (inCone && !Physics.Linecast(head, eye, Layers.SightMask, QueryTriggerInteraction.Ignore))
                {
                    seesPlayer = true;
                    if (p.HoldingGun)
                    {
                        var aimDir = p.AimDirection;
                        var toMe = (head - eye).normalized;
                        aimedAt = playerDist < 30f && Vector3.Angle(aimDir, toMe) < 7f;
                        threatPos = p.Position; threatTime = Time.time;
                        if (aimedAt) Fear = Mathf.Clamp01(Fear + 0.04f * (1.2f - Bravery));
                    }
                    else if (Anger > 0.4f) { threatPos = p.Position; threatTime = Time.time; }
                }
            }
            // Seeing dead or mutilated Goredolls is frightening.
            if (Time.frameCount % 4 == 0)
                foreach (var other in Goredoll.All)
                {
                    if (other == doll || seenBodies.Contains(other)) continue;
                    if (other.Vitals.Alive && other.TotalBleed < 5f) continue;
                    var op = other.Parts[PartType.Pelvis].Body.position;
                    if (Vector3.Distance(op, Pelvis) > 18f) continue;
                    if (Physics.Linecast(HeadPos, op + Vector3.up * 0.3f, Layers.SightMask, QueryTriggerInteraction.Ignore)) continue;
                    seenBodies.Add(other);
                    Fear = Mathf.Clamp01(Fear + 0.22f * (1.1f - Bravery));
                    threatPos = op; threatTime = Time.time;
                    if (Random.value < 0.5f) doll.Voice.Say("v_scream", 0.9f);
                }
        }

        // ------------------------------------------------------------------ decisions

        void Update()
        {
            if (doll == null) return;
            var v = doll.Vitals;
            if (!v.Conscious || v.Paralysis == Paralysis.Total || Rules.FreezeAi)
            {
                body.DesiredVelocity = Vector3.zero;
                Thought = !v.Alive ? "dead" : v.Paralysis == Paralysis.Total ? "paralysed — can't move" : Rules.FreezeAi ? "frozen" : "unconscious";
                if (v.Conscious && Time.frameCount % 12 == 0) Perceive();
                UpdateFace();
                return;
            }
            Fear = Mathf.MoveTowards(Fear, Mathf.Clamp01(v.Pain / 200f), Time.deltaTime * (Time.time - threatTime < 5f ? 0.01f : 0.035f));
            Anger = Mathf.MoveTowards(Anger, 0f, Time.deltaTime * 0.015f);
            if (Rules.AiAggression > 0) Anger = Mathf.Max(Anger, Rules.AiAggression * 0.8f);
            if (Hostile) { Anger = 1f; Fear = Mathf.Min(Fear, 0.3f); }
            thinkTimer -= Time.deltaTime;
            if (thinkTimer <= 0f)
            {
                thinkTimer = Random.Range(0.18f, 0.28f);
                Perceive();
                Decide();
            }
            Act(Time.deltaTime);
            UpdateFace();
        }

        void Decide()
        {
            if (ForceDance) { SetAction(AiAction.Dance); return; }
            if (body.IsDown) { DecideWhileDown(); return; }
            if (Action == AiAction.PlayDead) return;
            bool threatRecent = Time.time - threatTime < 8f;
            bool hurtRecent = Time.time - hurtTime < 1.6f;
            float scoreCurrent = Score(Action, threatRecent, hurtRecent);
            AiAction best = Action;
            float bestScore = scoreCurrent + 0.12f;
            foreach (AiAction a in System.Enum.GetValues(typeof(AiAction)))
            {
                if (a == AiAction.Crawl || a == AiAction.PlayDead || a == AiAction.Dance) continue;
                float s = Score(a, threatRecent, hurtRecent);
                if (s > bestScore) { best = a; bestScore = s; }
            }
            if (best != Action) SetAction(best);
        }

        float Score(AiAction a, bool threatRecent, bool hurtRecent)
        {
            bool canFightPlayer = seesPlayer && !IgnorePlayer;
            switch (a)
            {
                case AiAction.Panic: return hurtRecent && Fear > 0.45f ? 0.95f : 0f;
                case AiAction.Fight:
                    if (Hostile && focus != null && !IgnorePlayer) return 2f;
                    if (Rules.NpcInfighting && NearestOther(12f) != null && Anger > 0.2f) return 0.7f + Anger * 0.3f;
                    return canFightPlayer && Anger > 0.45f && Aggression > 0.45f && playerDist < 10f && Fear < 0.75f ? Anger + Rules.AiAggression : 0f;
                case AiAction.Surrender: return aimedAt && playerDist < 14f && Fear > 0.25f && Bravery < 0.65f && !hurtRecent ? 0.75f + Fear * 0.2f : 0f;
                case AiAction.Flee: return threatRecent && Fear > 0.4f ? Fear : 0f;
                case AiAction.Hide: return threatRecent && Fear > 0.28f ? Fear * 0.82f + (Bravery < 0.35f ? 0.12f : 0f) : 0f;
                case AiAction.Investigate: return Time.time - noiseTime < 7f && Fear < 0.35f ? Curiosity * 0.7f * (1f - noiseThreat * 0.5f) : 0f;
                case AiAction.Greet: return seesPlayer && !greeted && Fear < 0.15f && Anger < 0.2f && playerDist < 9f && Time.time > greetCooldown ? 0.45f + Sociability * 0.25f : 0f;
                case AiAction.Watch: return (seesPlayer && Fear < 0.35f) ? 0.38f + (playerDist < 2.2f ? 0.1f : 0f) : 0f;
                case AiAction.Wander: return 0.3f + Curiosity * 0.05f;
                case AiAction.Idle: return 0.26f;
                default: return 0f;
            }
        }

        void DecideWhileDown()
        {
            var v = doll.Vitals;
            if (Action == AiAction.PlayDead)
            {
                body.WantsToGetUp = Time.time > playDeadUntil;
                if (Time.time > playDeadUntil) SetAction(AiAction.Flee);
                return;
            }
            bool threatRecent = Time.time - threatTime < 10f;
            if (Fear > 0.75f && Bravery < 0.3f && threatRecent && Random.value < 0.04f && Action != AiAction.Crawl)
            {
                SetAction(AiAction.PlayDead);
                playDeadUntil = Time.time + Random.Range(10f, 20f);
                body.WantsToGetUp = false;
                doll.Voice.Silence();
                return;
            }
            body.WantsToGetUp = true;
            body.WantsToCrawl = !body.LegsWork && threatRecent && Fear > 0.3f;
            if (body.WantsToCrawl && Action != AiAction.Crawl) SetAction(AiAction.Crawl);
            if (Random.value < 0.02f && v.Pain > 30f) doll.Voice.Say(Random.value < 0.5f ? "v_moan" : "v_whimper", 0.8f, false);
        }

        void SetAction(AiAction a)
        {
            var prev = Action;
            Action = a;
            actionTime = Time.time;
            path.Clear();
            pathIndex = 0;
            stuckTimer = 0;
            var p = focus;
            switch (a)
            {
                case AiAction.Wander:
                    if (NavGrid.Current != null && NavGrid.Current.RandomPoint(Pelvis, 14f, out var w)) PathTo(w);
                    break;
                case AiAction.Investigate:
                    PathTo(noisePos);
                    if (Random.value < 0.5f) doll.Voice.Say("v_huh", 0.8f, false);
                    break;
                case AiAction.Flee:
                    if (NavGrid.Current != null && NavGrid.Current.FleePoint(threatPos, Pelvis, Random.Range(15f, 28f), out var f)) PathTo(f);
                    if (prev != AiAction.Panic && Random.value < 0.6f) doll.Voice.Say(Random.value < 0.6f ? "v_scream" : "v_no");
                    break;
                case AiAction.Hide:
                    if (NavGrid.Current != null && p != null && NavGrid.Current.FindCover(p.EyePosition, Pelvis, 14f, out var c)) PathTo(c);
                    else if (NavGrid.Current != null && NavGrid.Current.FleePoint(threatPos, Pelvis, 12f, out var f2)) PathTo(f2);
                    break;
                case AiAction.Surrender:
                    doll.Voice.Say(Random.value < 0.6f ? "v_no" : "v_whimper");
                    break;
                case AiAction.Greet:
                    greeted = true;
                    greetCooldown = Time.time + 30f;
                    doll.Voice.Say("v_hey", 0.9f);
                    break;
                case AiAction.Panic:
                    goal = Pelvis + Random.insideUnitSphere.normalized * 6f;
                    doll.Voice.Say("v_scream");
                    break;
                case AiAction.Fight:
                    if (Random.value < 0.6f) doll.Voice.Say("v_argh");
                    break;
            }
        }

        void PathTo(Vector3 target)
        {
            goal = target;
            if (NavGrid.Current == null || !NavGrid.Current.FindPath(Pelvis, target, path)) { path.Clear(); path.Add(target); }
            pathIndex = path.Count > 1 ? 1 : 0;
        }

        Goredoll NearestOther(float radius)
        {
            Goredoll best = null;
            float bd = radius;
            foreach (var g in Goredoll.All)
            {
                if (g == doll || !g.Vitals.Alive) continue;
                float d = Vector3.Distance(g.Parts[PartType.Pelvis].Body.position, Pelvis);
                if (d < bd) { bd = d; best = g; }
            }
            return best;
        }

        // ------------------------------------------------------------------ actions

        float MaxSpeed(bool run)
        {
            float legs = Mathf.Min(Part(PartType.ThighL), Part(PartType.ShinL), Part(PartType.ThighR), Part(PartType.ShinR));
            float blood = Mathf.Clamp01(1f - doll.Vitals.BloodLossFraction * 1.8f);
            float baseSpeed = run ? 4.3f : 1.35f;
            return baseSpeed * Mathf.Lerp(0.35f, 1f, legs) * Mathf.Lerp(0.45f, 1f, blood);
        }

        float Part(PartType t) => doll.Parts.TryGetValue(t, out var p) ? p.Function : 0f;

        void Act(float dt)
        {
            var p = focus;
            Vector3? look = (seesPlayer || AwareOfPlayer) && p != null && !IgnorePlayer ? p.EyePosition : (Time.time - noiseTime < 4f ? noisePos : (Vector3?)null);
            body.LookTarget = look;
            Vector3 move = Vector3.zero;
            bool run = false;
            body.Pose = PoseMode.Idle;
            switch (Action)
            {
                case AiAction.Idle:
                    Thought = "chilling";
                    if (Time.time - actionTime > 4f) body.Pose = PoseMode.LookAround;
                    break;
                case AiAction.Wander:
                    Thought = "going for a walk";
                    move = FollowPath();
                    if (move == Vector3.zero && Time.time - actionTime > 2f) SetAction(Random.value < 0.5f ? AiAction.Idle : AiAction.Wander);
                    break;
                case AiAction.Investigate:
                    Thought = "what was that noise?";
                    move = FollowPath();
                    if (move == Vector3.zero) { body.Pose = PoseMode.LookAround; if (Time.time - actionTime > 8f) SetAction(AiAction.Idle); }
                    break;
                case AiAction.Watch:
                    Thought = "watching you";
                    if (p != null && playerDist < 2.2f) move = -MathX.Flat(p.Position - Pelvis).normalized * 0.8f;
                    if (p != null) FaceTowards(p.Position);
                    break;
                case AiAction.Greet:
                    Thought = "saying hi";
                    body.Pose = PoseMode.Wave;
                    if (p != null) FaceTowards(p.Position);
                    if (Time.time - actionTime > 2.5f) SetAction(AiAction.Watch);
                    break;
                case AiAction.Surrender:
                    Thought = "please don't shoot!";
                    body.Pose = PoseMode.HandsUp;
                    if (p != null) FaceTowards(p.Position);
                    if (!aimedAt && Time.time - actionTime > 4f) SetAction(Fear > 0.4f ? AiAction.Flee : AiAction.Watch);
                    if (Random.value < 0.006f) doll.Voice.Say(Random.value < 0.5f ? "v_no" : "v_whimper", 0.8f, false);
                    break;
                case AiAction.Flee:
                    Thought = "RUN!";
                    run = true;
                    move = FollowPath();
                    if (move == Vector3.zero) SetAction(Fear > 0.5f ? AiAction.Hide : AiAction.Idle);
                    if (Time.time > nextScream && Random.value < 0.01f) { nextScream = Time.time + 4f; doll.Voice.Say(Random.value < 0.5f ? "v_scream" : "v_no", 1f, false); }
                    break;
                case AiAction.Hide:
                    Thought = "hiding";
                    move = FollowPath();
                    if (move == Vector3.zero) body.Pose = PoseMode.Cower;
                    run = path.Count > 0 && pathIndex < path.Count;
                    if (Time.time - actionTime > 15f && Fear < 0.3f) SetAction(AiAction.Idle);
                    break;
                case AiAction.Panic:
                    Thought = "AAAAAH";
                    run = true;
                    move = MathX.Flat(goal - Pelvis);
                    if (move.magnitude < 1f || Time.time - actionTime > 1.6f) { goal = Pelvis + Quaternion.Euler(0, Random.Range(-120, 120), 0) * MathX.Flat(Pelvis - threatPos).normalized * 6f; actionTime = Time.time - 0.2f; if (Random.value < 0.3f) SetAction(AiAction.Flee); }
                    move = move.normalized;
                    break;
                case AiAction.Fight:
                    DoFight(p, ref move, ref run, dt);
                    break;
                case AiAction.Crawl:
                    Thought = "crawling away";
                    body.DesiredVelocity = MathX.Flat(Pelvis - threatPos).normalized * 0.8f;
                    return;
                case AiAction.PlayDead:
                    Thought = "(playing dead)";
                    break;
                case AiAction.Dance:
                    Thought = "~ dancing ~";
                    body.Pose = PoseMode.Dance;
                    break;
            }

            if (move.sqrMagnitude > 0.01f)
            {
                move = Avoid(move.normalized);
                float speed = MaxSpeed(run);
                body.DesiredVelocity = move * speed;
                if (Action != AiAction.Watch && Action != AiAction.Fight) FaceTowards(Pelvis + move);
                if (body.Pose == PoseMode.Idle) body.Pose = run ? PoseMode.Run : PoseMode.Walk;
                // Stuck? pick something else.
                if (MathX.Flat(RB.Vel(doll.Parts[PartType.Pelvis].Body)).magnitude < 0.2f) stuckTimer += dt; else stuckTimer = 0;
                if (stuckTimer > 2.5f) { stuckTimer = 0; SetAction(Action == AiAction.Flee ? AiAction.Panic : AiAction.Wander); }
            }
            else body.DesiredVelocity = Vector3.zero;
        }

        void DoFight(IPlayerTarget p, ref Vector3 move, ref bool run, float dt)
        {
            Thought = "fighting back!";
            fightTarget = null; fightDamageable = null;
            if (Rules.NpcInfighting)
            {
                var other = NearestOther(15f);
                if (other != null) { fightTarget = other.Parts[PartType.Chest].transform; fightDamageable = other.Parts[PartType.Chest]; }
            }
            if (fightTarget == null && p != null && !IgnorePlayer) { fightTarget = p.Root; fightDamageable = p.Damageable; }
            if (fightTarget == null) { SetAction(AiAction.Idle); return; }
            var to = MathX.Flat(fightTarget.position - Pelvis);
            FaceTowards(fightTarget.position);
            body.LookTarget = fightTarget.position + Vector3.up * 1.4f;
            if (to.magnitude > 1.15f) { move = to.normalized; run = to.magnitude > 3f; body.Pose = run ? PoseMode.Run : PoseMode.Guard; return; }
            body.Pose = PoseMode.Punch;
            punchTimer += dt;
            body.PunchProgress = punchTimer / 0.35f;
            if (punchTimer >= 0.35f && punchTimer - dt < 0.35f)
            {
                AudioManager.Play("whoosh_big", Pelvis + Vector3.up, 0.6f);
                if (to.magnitude < 1.4f && fightDamageable != null)
                {
                    var dir = to.normalized;
                    fightDamageable.TakeDamage(new DamageInfo { Amount = Random.Range(8f, 15f), Force = dir * 40f, Point = fightTarget.position + Vector3.up * 1.4f, Normal = -dir, Direction = dir, Type = DamageType.Blunt, Source = doll, Weapon = doll.DisplayName + "'s fist" });
                    AudioManager.Play("punch_hit", fightTarget.position + Vector3.up * 1.4f, 0.9f);
                }
            }
            if (punchTimer > 1.0f) { punchTimer = 0; if (Random.value < 0.2f) doll.Voice.Say("v_argh", 0.8f, false); }
            if (Fear > 0.8f) SetAction(AiAction.Flee);
        }

        Vector3 FollowPath()
        {
            if (path.Count == 0 || pathIndex >= path.Count) return Vector3.zero;
            var target = path[pathIndex];
            var to = MathX.Flat(target - Pelvis);
            if (to.magnitude < 0.7f)
            {
                pathIndex++;
                if (pathIndex >= path.Count) return Vector3.zero;
                to = MathX.Flat(path[pathIndex] - Pelvis);
            }
            return to.normalized;
        }

        Vector3 Avoid(Vector3 dir)
        {
            var origin = Pelvis + Vector3.up * -0.3f;
            if (!Physics.SphereCast(origin, 0.25f, dir, out _, 1.1f, Layers.Mask(Layers.Default, Layers.Props), QueryTriggerInteraction.Ignore)) return dir;
            foreach (float a in new[] { 40f, -40f, 75f, -75f, 110f, -110f })
            {
                var d = Quaternion.Euler(0, a, 0) * dir;
                if (!Physics.SphereCast(origin, 0.25f, d, out _, 1.1f, Layers.Mask(Layers.Default, Layers.Props), QueryTriggerInteraction.Ignore)) return d;
            }
            return -dir;
        }

        void FaceTowards(Vector3 point)
        {
            var to = MathX.Flat(point - Pelvis);
            if (to.sqrMagnitude > 0.01f) body.DesiredYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }

        void UpdateFace()
        {
            var face = doll.Face;
            if (face == null) return;
            var v = doll.Vitals;
            if (!v.Alive) { face.Expression = Expression.Dead; face.ClearLook(); return; }
            if (v.State == LifeState.Unconscious || Action == AiAction.PlayDead) { face.Expression = Expression.Unconscious; return; }
            if (v.AirwayBlood > 0.4f) face.Expression = Expression.Choking;
            else if (v.Pain > 45f) face.Expression = Expression.Pain;
            else if (Action == AiAction.Fight) face.Expression = Expression.Angry;
            else if (Fear > 0.4f || Action == AiAction.Surrender) face.Expression = Expression.Afraid;
            else if (Action == AiAction.Investigate) face.Expression = Expression.Curious;
            else face.Expression = Expression.Neutral;
            var p = focus;
            if (p != null && (seesPlayer || v.Paralysis == Paralysis.Total && playerDist < 20f)) face.LookAt(p.EyePosition);
            else face.ClearLook();
        }
    }
}
