using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum RagdollState { Standing, Fallen, GettingUp, Crawling, Limp }
    public enum PoseMode { Idle, Walk, Run, HandsUp, Cower, ClutchTorso, ClutchNeck, Guard, Punch, Wave, Dance, Writhe, Crawl, GetUp, Brace, LookAround }

    /// <summary>
    /// Physics-driven body: joint motors follow procedural poses, a balance controller keeps the pelvis upright and
    /// at standing height, and a locomotion force moves the body. Hits, impacts and injuries knock it down; it tries
    /// to get back up if its legs and brain still work, crawls if they don't, and goes fully limp when unconscious or dead.
    /// </summary>
    public sealed class ActiveRagdoll : MonoBehaviour
    {
        public RagdollState State = RagdollState.Standing;
        public PoseMode Pose = PoseMode.Idle;
        public Vector3 DesiredVelocity;
        public float DesiredYaw;
        public Vector3? LookTarget;
        public float PunchProgress;
        public bool WantsToGetUp = true;
        public bool WantsToCrawl;
        /// <summary>Zombie posture: arms reaching forward, head lolling, hunched torso.</summary>
        public bool ZombieArms;
        /// <summary>Armed Goredolls (ArmedBrain): 0..1 blend into a gun-aiming arm pose, pitch in degrees (+ = up).</summary>
        public float AimWeight, AimPitch;
        public bool AimRifle;

        Goredoll doll;
        float stagger, balance = 1f, downUntil, getUp, phase, airborne, deathTime = -1f, spasmSeed;
        readonly Vector3[] targets = new Vector3[17];
        float lastJointUpdate;

        public float Balance => balance;
        public bool IsDown => State == RagdollState.Fallen || State == RagdollState.Crawling || State == RagdollState.Limp;
        public bool IsStanding => State == RagdollState.Standing;

        public void Init(Goredoll d)
        {
            doll = d;
            DesiredYaw = currentYaw = d.transform.eulerAngles.y;
            rootTarget = Quaternion.Euler(0, currentYaw, 0);
            spasmSeed = Random.value * 100f;
        }

        Rigidbody Rb(PartType t) => doll.Parts.TryGetValue(t, out var p) && p != null && !p.Severed && !p.Gibbed ? p.Body : null;

        /// <summary>Overall muscle tone from the body's state.</summary>
        float GlobalStrength()
        {
            var v = doll.Vitals;
            if (Rules.FreezeAi && v.Alive) return 0.9f;
            if (v.State == LifeState.Dead)
            {
                if (deathTime < 0) deathTime = Time.time;
                float t = Time.time - deathTime;
                return t < 2.2f ? 0.18f * (1f - t / 2.2f) : 0f; // brief death spasm
            }
            if (v.State == LifeState.Unconscious) return 0.02f;
            if (v.Paralysis == Paralysis.Total) return 0f;
            return Mathf.Clamp01(0.35f + 0.65f * v.Consciousness) * (1f - v.Shock * 0.35f);
        }

        public bool LegsWork
        {
            get
            {
                foreach (var t in new[] { PartType.ThighL, PartType.ShinL, PartType.ThighR, PartType.ShinR })
                    if (!doll.Parts.TryGetValue(t, out var p) || p.Function < 0.3f) return false;
                return doll.Vitals.Paralysis == Paralysis.None;
            }
        }

        public bool ArmsWork
        {
            get
            {
                int ok = 0;
                foreach (var t in new[] { PartType.UpperArmL, PartType.UpperArmR })
                    if (doll.Parts.TryGetValue(t, out var p) && p.Function > 0.3f) ok++;
                return ok > 0 && doll.Vitals.Paralysis != Paralysis.Total;
            }
        }

        public bool CanStand => doll.Vitals.State == LifeState.Alive && LegsWork && doll.Vitals.Consciousness > 0.5f;

        // ------------------------------------------------------------------ events

        public void OnHit(float damage, PartType part)
        {
            float k = Anatomy.Parts[part].IsLeg ? 1.5f : part == PartType.Head ? 1.3f : 1f;
            stagger += damage / 60f * k;
            if (State == RagdollState.Standing && (stagger > 1.3f || (Anatomy.Parts[part].IsLeg && !LegsWork))) Fall(1.5f + damage / 40f);
        }

        public void OnImpact(float deltaV)
        {
            stagger += deltaV / 16f;
            if (State == RagdollState.Standing && deltaV > 8.5f) Fall(2f);
        }

        public void Fall(float minDown = 2.5f)
        {
            if (State == RagdollState.Limp) return;
            State = RagdollState.Fallen;
            downUntil = Time.time + minDown + Random.Range(0.3f, 1.2f);
            tiltTime = 0f;
            balance = 0f;
            getUp = 0f;
            if (doll.Vitals.Conscious && Random.value < 0.6f) doll.Voice.Say(Random.value < 0.5f ? "v_whoa" : "v_pain", 0.8f);
        }

        public void Launch(Vector3 velocity)
        {
            Fall(3f);
            foreach (var p in doll.Parts.Values)
                if (p.Body != null && !p.Severed) RB.SetVel(p.Body, RB.Vel(p.Body) + velocity);
        }

        public void ForceStandNow()
        {
            var pelvis = Rb(PartType.Pelvis);
            if (pelvis == null) return;
            BeginGetUp(pelvis);
            stagger = 0;
        }

        // ------------------------------------------------------------------ simulation
        //
        // How standing works:
        //  1. ComputePose() fills local joint targets (the "animation").
        //  2. Those are chained from a root rotation (upright, facing currentYaw) into a WORLD target rotation per part.
        //  3. Torso, head and legs get a critically damped PD torque toward their world target (inertia-aware, so a
        //     light foot and a heavy chest react the same). The joint motors also drive the same pose locally.
        //  4. A height controller supports the whole body's weight through pelvis + chest, so legs never buckle.
        //  5. Walking accelerates every part equally, so the body moves as one piece instead of being dragged.
        // Getting up is a timed blend from the lying orientation/height to standing.

        static readonly PartType[] Order =
        {
            PartType.Pelvis, PartType.Abdomen, PartType.Chest, PartType.Neck, PartType.Head,
            PartType.UpperArmL, PartType.ForearmL, PartType.HandL, PartType.UpperArmR, PartType.ForearmR, PartType.HandR,
            PartType.ThighL, PartType.ShinL, PartType.FootL, PartType.ThighR, PartType.ShinR, PartType.FootR,
        };

        // PD gains (angular acceleration per radian, damping per rad/s). Damping ≈ 2·√kp = critically damped.
        static float Kp(PartType t)
        {
            switch (t)
            {
                case PartType.Pelvis: return 650f;
                case PartType.Abdomen: case PartType.Chest: return 480f;
                case PartType.Neck: case PartType.Head: return 300f;
                case PartType.ThighL: case PartType.ThighR: return 420f;
                case PartType.ShinL: case PartType.ShinR: return 360f;
                case PartType.FootL: case PartType.FootR: return 260f;
                default: return 0f; // arms are driven by their joint motors only
            }
        }

        readonly Quaternion[] worldTargets = new Quaternion[17];
        Quaternion rootTarget = Quaternion.identity, getUpStartRot;
        float currentYaw, getUpStartHeight, tiltTime, groundY, getUpTime;
        int getUpFails;

        void FixedUpdate()
        {
            if (doll == null) return;
            float dt = Time.fixedDeltaTime;
            float strength = GlobalStrength();
            var pelvis = Rb(PartType.Pelvis);
            if (pelvis == null) return;

            UpdateState(pelvis, strength, dt);
            ComputePose(dt, pelvis);
            ApplyMuscles(strength);
            if (State == RagdollState.Standing || State == RagdollState.GettingUp) ApplyBalance(pelvis, dt, strength);
            else if (State == RagdollState.Crawling) Crawl(dt);
        }

        bool FindGround(Vector3 from, out float y)
        {
            if (Physics.Raycast(from + Vector3.up * 0.3f, Vector3.down, out var hit, 3f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) { y = hit.point.y; return true; }
            y = from.y - 1f;
            return false;
        }

        float YawOf(Vector3 dir)
        {
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir).eulerAngles.y : currentYaw;
        }

        void UpdateState(Rigidbody pelvis, float strength, float dt)
        {
            stagger = Mathf.MoveTowards(stagger, 0f, dt * 0.9f);
            bool alive = doll.Vitals.State == LifeState.Alive && doll.Vitals.Paralysis != Paralysis.Total;
            if (!alive) { State = RagdollState.Limp; balance = 0; return; }
            if (State == RagdollState.Limp) { State = RagdollState.Fallen; downUntil = Time.time + 2f; }

            switch (State)
            {
                case RagdollState.Standing:
                {
                    balance = Mathf.MoveTowards(balance, Mathf.Clamp01(1f - stagger * 0.35f), dt * 3f);
                    // Fall only if we stay badly tilted for a moment (a single shove is recovered from).
                    float tilt = Vector3.Angle(pelvis.transform.up, rootTarget * Vector3.up);
                    tiltTime = tilt > 50f ? tiltTime + dt : 0f;
                    bool grounded = FindGround(pelvis.position, out var gy) && pelvis.position.y - gy < 1.9f * doll.Look.Height;
                    airborne = grounded ? 0f : airborne + dt;
                    if (tiltTime > 0.25f || airborne > 0.6f || !CanStand) Fall(airborne > 0.6f ? 1.2f : 1.5f);
                    break;
                }
                case RagdollState.Fallen:
                    balance = 0;
                    if (Time.time > downUntil && WantsToGetUp && CanStand && !Rules.FreezeAi && RB.Vel(pelvis).magnitude < 2.5f)
                        BeginGetUp(pelvis);
                    else if (WantsToCrawl && !LegsWork && ArmsWork && doll.Vitals.Conscious) State = RagdollState.Crawling;
                    break;
                case RagdollState.Crawling:
                    if (!WantsToCrawl || !ArmsWork || !doll.Vitals.Conscious) State = RagdollState.Fallen;
                    else if (CanStand && WantsToGetUp) BeginGetUp(pelvis);
                    break;
                case RagdollState.GettingUp:
                {
                    getUp = Mathf.Min(1f, getUp + dt / getUpTime);
                    balance = 1f;
                    if (!CanStand) { Fall(3f); break; }
                    if (getUp >= 1f)
                    {
                        FindGround(pelvis.position, out var gy);
                        float h = pelvis.position.y - gy;
                        float tilt = Vector3.Angle(pelvis.transform.up, Vector3.up);
                        if (h > StandHeight(1f) * 0.7f && tilt < 35f) { State = RagdollState.Standing; stagger = 0; tiltTime = 0; getUpFails = 0; }
                        else if (getUp >= 1f && getUpFails++ < 4) BeginGetUp(pelvis);   // try again from wherever we are
                        else { Fall(4f); getUpFails = 0; }
                    }
                    break;
                }
            }
        }

        void BeginGetUp(Rigidbody pelvis)
        {
            State = RagdollState.GettingUp;
            getUp = 0f;
            getUpTime = Random.Range(1.4f, 1.9f);
            getUpStartRot = pelvis.rotation;
            FindGround(pelvis.position, out groundY);
            getUpStartHeight = Mathf.Max(0.1f, pelvis.position.y - groundY);
            // Stand up facing the way the head was pointing (or where the brain wants to face).
            currentYaw = YawOf(pelvis.transform.up.y < 0.7f ? pelvis.transform.up : pelvis.transform.forward);
            if (doll.Vitals.Conscious && Random.value < 0.5f) doll.Voice.Say("breath_in_2", 0.5f, false);
        }

        void ApplyMuscles(float strength)
        {
            float poseStrength = State == RagdollState.Fallen ? 0.35f : State == RagdollState.Limp ? 1f : State == RagdollState.Crawling ? 0.7f : 1f;
            bool spasm = doll.Vitals.State == LifeState.Dead && strength > 0.001f;
            foreach (var p in doll.Parts.Values)
            {
                var j = p.Joint;
                if (j == null || p.Severed || p.Gibbed) continue;
                float s = strength * poseStrength * p.Function * (p.Parent != null ? Mathf.Max(0.2f, p.Parent.Function) : 1f);
                var e = targets[(int)p.Type];
                if (spasm)
                    e += new Vector3(Mathf.PerlinNoise(Time.time * 6f, spasmSeed + (int)p.Type) - 0.5f, 0, Mathf.PerlinNoise(spasmSeed, Time.time * 6f + (int)p.Type) - 0.5f) * 60f;
                j.targetRotation = Quaternion.Inverse(Quaternion.Euler(e));
                float spring = p.Spec.Muscle * s;
                // Damping near critical for the limb's own inertia keeps limbs from oscillating.
                j.slerpDrive = new JointDrive { positionSpring = spring, positionDamper = spring * 0.08f + 3f, maximumForce = float.MaxValue };
            }
        }

        /// <summary>Pelvis height above the ground for the current leg pose (uses the straighter, weight-bearing leg).</summary>
        float StandHeight(float scale)
        {
            float LegHeight(PartType thigh, PartType shin, PartType foot)
            {
                float a = targets[(int)thigh].x, b = targets[(int)shin].x;
                float h = 0.05f + 0.42f * Mathf.Cos(a * Mathf.Deg2Rad) + 0.40f * Mathf.Cos((a + b) * Mathf.Deg2Rad) + 0.075f;
                return h;
            }
            float l = LegHeight(PartType.ThighL, PartType.ShinL, PartType.FootL);
            float r = LegHeight(PartType.ThighR, PartType.ShinR, PartType.FootR);
            return Mathf.Max(0.25f, Mathf.Max(l, r)) * doll.Look.Height * scale;
        }

        void ApplyBalance(Rigidbody pelvis, float dt, float strength)
        {
            float support = balance * Mathf.Clamp01(strength * 1.25f);
            if (support <= 0.01f) return;
            bool gettingUp = State == RagdollState.GettingUp;
            float k = gettingUp ? Mathf.SmoothStep(0f, 1f, getUp) : 1f;

            // ---- root target: upright + facing (lean forward when moving or crouching)
            if (!gettingUp) currentYaw = Mathf.MoveTowardsAngle(currentYaw, DesiredYaw, 260f * dt);
            float speed = MathX.Flat(RB.Vel(pelvis)).magnitude;
            float lean = Pose == PoseMode.Cower ? 22f : Mathf.Clamp(speed * 2.2f, 0f, 12f);
            var upright = Quaternion.Euler(lean, currentYaw, 0f);
            rootTarget = gettingUp ? Quaternion.Slerp(getUpStartRot, upright, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(getUp * 1.35f))) : upright;
            if (gettingUp) support = Mathf.Max(support, 0.6f);

            // ---- chain local pose targets into world targets
            worldTargets[(int)PartType.Pelvis] = rootTarget * Quaternion.Euler(targets[(int)PartType.Pelvis]);
            for (int i = 1; i < Order.Length; i++)
            {
                var t = Order[i];
                var parent = Anatomy.Parts[t].Parent.Value;
                worldTargets[(int)t] = worldTargets[(int)parent] * Quaternion.Euler(targets[(int)t]);
            }

            // ---- orientation PD on torso, head and (working) legs
            bool legs = LegsWork;
            foreach (var t in Order)
            {
                float kp = Kp(t);
                if (kp <= 0f) continue;
                if (!legs && Anatomy.Parts[t].IsLeg) continue;
                if (!doll.Parts.TryGetValue(t, out var part) || part == null || part.Severed || part.Gibbed) continue;
                var rb = part.Body;
                if (rb == null || rb.isKinematic) continue;
                float g = support * Mathf.Max(0.35f, part.Function);
                var err = MathX.AngularError(rb.rotation, worldTargets[(int)t]);
                var acc = err * kp - rb.angularVelocity * (2f * Mathf.Sqrt(kp));
                rb.AddTorque(Vector3.ClampMagnitude(acc * g, 900f), ForceMode.Acceleration);
            }

            // ---- height: carry the body's weight through pelvis (60%) and chest (40%)
            float mass = 0f;
            foreach (var p in doll.Parts.Values) if (p != null && !p.Severed && !p.Gibbed && p.Body != null) mass += p.Body.mass;
            bool grounded = FindGround(pelvis.position, out var gy);
            if (grounded)
            {
                float standH = StandHeight(1f) - 0.015f;          // feet press lightly into the floor
                float targetH = gettingUp ? Mathf.Lerp(getUpStartHeight, standH, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((getUp - 0.1f) / 0.8f))) : standH;
                float err = gy + targetH - pelvis.position.y;
                float vy = RB.Vel(pelvis).y;
                float a = Mathf.Clamp(err * 70f - vy * 14f, -12f, 22f);
                float lift = Mathf.Clamp(mass * (-Physics.gravity.y + a) * support, 0f, mass * 35f);
                var chest = Rb(PartType.Chest);
                if (chest != null && !chest.isKinematic)
                {
                    pelvis.AddForce(Vector3.up * lift * 0.6f, ForceMode.Force);
                    chest.AddForce(Vector3.up * lift * 0.4f, ForceMode.Force);
                }
                else pelvis.AddForce(Vector3.up * lift, ForceMode.Force);
            }

            // ---- locomotion: accelerate every part equally toward the requested velocity
            var v = MathX.Flat(RB.Vel(pelvis));
            var desired = gettingUp ? Vector3.zero : DesiredVelocity * Rules.NpcSpeedMultiplier;
            var accel = Vector3.ClampMagnitude((desired - v) * 6f, 14f) * support * (gettingUp ? 0.5f : 1f);
            foreach (var p in doll.Parts.Values)
                if (p != null && !p.Severed && !p.Gibbed && p.Body != null && !p.Body.isKinematic)
                    p.Body.AddForce(accel, ForceMode.Acceleration);
            phase += speed * dt * (Pose == PoseMode.Run ? 4.2f : 5.2f);
        }

        void Crawl(float dt)
        {
            var chest = Rb(PartType.Chest);
            if (chest == null) return;
            var dir = DesiredVelocity.sqrMagnitude > 0.01f ? DesiredVelocity.normalized : Vector3.zero;
            chest.AddForce(dir * 260f + Vector3.up * 120f, ForceMode.Force);
            var head = Rb(PartType.Head);
            if (head != null) head.AddForce(Vector3.up * 25f, ForceMode.Force);
            phase += dt * 3f;
        }

        // ------------------------------------------------------------------ poses

        void Set(PartType t, float x, float y = 0, float z = 0) => targets[(int)t] = new Vector3(x, y, z);

        void Side(bool right, PartType l, PartType r, float x, float y, float zOut)
        {
            float s = right ? 1f : -1f;
            targets[(int)(right ? r : l)] = new Vector3(x, y * s, zOut * s);
        }

        void Arms(float uaX, float uaZOut, float faX, float handX, bool rightOnly = false, bool leftOnly = false)
        {
            if (!leftOnly) { Side(true, PartType.UpperArmL, PartType.UpperArmR, uaX, 0, uaZOut); Side(true, PartType.ForearmL, PartType.ForearmR, faX, 0, 0); Side(true, PartType.HandL, PartType.HandR, handX, 0, 0); }
            if (!rightOnly) { Side(false, PartType.UpperArmL, PartType.UpperArmR, uaX, 0, uaZOut); Side(false, PartType.ForearmL, PartType.ForearmR, faX, 0, 0); Side(false, PartType.HandL, PartType.HandR, handX, 0, 0); }
        }

        void Legs(float thighX, float shinX, float footX, float zOut = 2f)
        {
            Side(true, PartType.ThighL, PartType.ThighR, thighX, 0, zOut); Side(false, PartType.ThighL, PartType.ThighR, thighX, 0, zOut);
            Set(PartType.ShinL, shinX); Set(PartType.ShinR, shinX);
            Set(PartType.FootL, footX); Set(PartType.FootR, footX);
        }

        void ComputePose(float dt, Rigidbody pelvis)
        {
            float t = Time.time;
            float breath = Mathf.Sin(t * Mathf.PI * 2f * doll.Vitals.RespRate / 60f);
            for (int i = 0; i < targets.Length; i++) targets[i] = Vector3.zero;
            var mode = Pose;
            if (State == RagdollState.Fallen) mode = doll.Vitals.Conscious ? PoseMode.Writhe : PoseMode.Idle;
            else if (State == RagdollState.Crawling) mode = PoseMode.Crawl;
            else if (State == RagdollState.GettingUp) mode = PoseMode.GetUp;
            else if (State == RagdollState.Limp) mode = PoseMode.Idle;

            // Base: relaxed idle
            Set(PartType.Abdomen, 2f + breath * 1.5f);
            Set(PartType.Chest, breath * 2.5f);
            Arms(-4f, 7f, -14f, 6f);
            Legs(-2f, 4f, -2f);

            switch (mode)
            {
                case PoseMode.Walk:
                case PoseMode.Run:
                {
                    float a = mode == PoseMode.Run ? 1.6f : 1f;
                    float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
                    float limpL = Limp(PartType.ThighL, PartType.ShinL), limpR = Limp(PartType.ThighR, PartType.ShinR);
                    Set(PartType.ThighL, -24f * a * s * limpL - 4f, 0, -2f);
                    Set(PartType.ThighR, 24f * a * s * limpR - 4f, 0, 2f);
                    Set(PartType.ShinL, 8f + 42f * a * Mathf.Clamp01(c) * limpL);
                    Set(PartType.ShinR, 8f + 42f * a * Mathf.Clamp01(-c) * limpR);
                    Set(PartType.FootL, -8f * a * s);
                    Set(PartType.FootR, 8f * a * s);
                    Set(PartType.UpperArmL, 20f * a * s, 0, -8f);
                    Set(PartType.UpperArmR, -20f * a * s, 0, 8f);
                    Set(PartType.ForearmL, -18f - 22f * (a - 1f) - 8f * Mathf.Clamp01(-s));
                    Set(PartType.ForearmR, -18f - 22f * (a - 1f) - 8f * Mathf.Clamp01(s));
                    Set(PartType.Abdomen, 4f * a + breath, 0, (limpL - limpR) * 6f);
                    Set(PartType.Chest, 2f * a, 6f * a * s, 0);
                    if (doll.TorsoBleeding) Clutch(false, 0.6f);
                    break;
                }
                case PoseMode.HandsUp:
                    Arms(-165f, 25f, -55f, 0f);
                    Set(PartType.Chest, -4f + breath * 3f);
                    break;
                case PoseMode.Cower:
                    Legs(-85f, 118f, -20f, 10f);
                    Set(PartType.Abdomen, 28f); Set(PartType.Chest, 18f);
                    Arms(-120f, -22f, -132f, 35f);
                    Set(PartType.Neck, 20f); Set(PartType.Head, 18f);
                    break;
                case PoseMode.ClutchTorso: Clutch(false, 1f); break;
                case PoseMode.ClutchNeck: Clutch(true, 1f); break;
                case PoseMode.Guard:
                    Arms(-55f, 10f, -125f, 20f);
                    Legs(-14f, 24f, -6f, 5f);
                    break;
                case PoseMode.Punch:
                {
                    Arms(-55f, 10f, -125f, 20f);
                    Legs(-14f, 24f, -6f, 5f);
                    float p = Mathf.Sin(Mathf.Clamp01(PunchProgress) * Mathf.PI);
                    Set(PartType.UpperArmR, Mathf.Lerp(-55f, -95f, p), 0, Mathf.Lerp(10f, -5f, p));
                    Set(PartType.ForearmR, Mathf.Lerp(-125f, -5f, p));
                    Set(PartType.Chest, 4f, -22f * p, 0);
                    break;
                }
                case PoseMode.Wave:
                    Set(PartType.UpperArmR, -150f, 0, 25f + 20f * Mathf.Sin(t * 8f));
                    Set(PartType.ForearmR, -45f);
                    break;
                case PoseMode.Dance:
                {
                    float d = Mathf.Sin(t * 7f);
                    Set(PartType.Abdomen, 4f, 0, d * 12f); Set(PartType.Chest, 0, d * 15f, -d * 8f);
                    Set(PartType.UpperArmL, -140f + d * 30f, 0, -40f); Set(PartType.UpperArmR, -140f - d * 30f, 0, 40f);
                    Set(PartType.ForearmL, -60f); Set(PartType.ForearmR, -60f);
                    Legs(-10f - Mathf.Abs(d) * 20f, 25f + Mathf.Abs(d) * 30f, 0f, 6f);
                    break;
                }
                case PoseMode.LookAround:
                    Set(PartType.Neck, 0, Mathf.Sin(t * 0.8f) * 25f, 0); Set(PartType.Head, 0, Mathf.Sin(t * 0.8f) * 35f, 0);
                    break;
                case PoseMode.Writhe:
                {
                    float n1 = Mathf.PerlinNoise(t * 0.7f, spasmSeed) - 0.5f, n2 = Mathf.PerlinNoise(spasmSeed, t * 0.6f) - 0.5f;
                    float pain = Mathf.Clamp01(doll.Vitals.Pain / 60f);
                    Legs(-55f + n1 * 60f * pain, 85f + n2 * 50f * pain, -15f, 8f);
                    Set(PartType.Abdomen, 25f * pain + n1 * 15f); Set(PartType.Chest, 15f * pain);
                    Clutch(doll.Vitals.AirwayBlood > 0.3f, 1f);
                    Set(PartType.Neck, -10f + n2 * 30f, n1 * 40f, 0);
                    break;
                }
                case PoseMode.Crawl:
                {
                    float s = Mathf.Sin(phase);
                    Set(PartType.UpperArmL, -130f + 45f * s, 0, -18f); Set(PartType.UpperArmR, -130f - 45f * s, 0, 18f);
                    Set(PartType.ForearmL, -40f - 40f * Mathf.Clamp01(-s)); Set(PartType.ForearmR, -40f - 40f * Mathf.Clamp01(s));
                    Set(PartType.Neck, -25f); Set(PartType.Head, -25f);
                    Legs(0f, 5f, 20f, 2f);
                    break;
                }
                case PoseMode.GetUp:
                {
                    // Tuck the legs, rise with the torso leaning forward, then straighten up.
                    float extend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((getUp - 0.35f) / 0.6f));
                    float straighten = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((getUp - 0.45f) / 0.5f));
                    Legs(Mathf.Lerp(-85f, -3f, extend), Mathf.Lerp(115f, 5f, extend), Mathf.Lerp(-25f, -2f, extend), 6f);
                    Set(PartType.Abdomen, Mathf.Lerp(32f, 2f, straighten));
                    Set(PartType.Chest, Mathf.Lerp(15f, 0f, straighten));
                    Arms(Mathf.Lerp(-55f, -4f, straighten), 14f, Mathf.Lerp(-35f, -14f, straighten), 5f);
                    Set(PartType.Neck, Mathf.Lerp(-15f, 0f, straighten));
                    break;
                }
                case PoseMode.Brace:
                    Arms(-80f, 15f, -20f, -20f);
                    break;
            }

            if (ZombieArms && (mode == PoseMode.Walk || mode == PoseMode.Run || mode == PoseMode.Idle || mode == PoseMode.LookAround || mode == PoseMode.Guard || mode == PoseMode.Punch))
            {
                float sw = Mathf.Sin(t * 1.7f + spasmSeed) * 9f;
                float lunge = mode == PoseMode.Punch ? Mathf.Sin(Mathf.Clamp01(PunchProgress) * Mathf.PI) : 0f;
                Set(PartType.UpperArmL, -80f + sw - lunge * 18f, 0, -10f);
                Set(PartType.UpperArmR, -76f - sw - lunge * 18f, 0, 10f);
                Set(PartType.ForearmL, -14f - lunge * 20f); Set(PartType.ForearmR, -18f - lunge * 20f);
                targets[(int)PartType.Neck] += new Vector3(12f + lunge * 22f, 0, Mathf.Sin(t * 0.9f + spasmSeed) * 10f);
                targets[(int)PartType.Abdomen] += new Vector3(7f + lunge * 10f, 0, 0);
            }

            if (AimWeight > 0.01f && (mode == PoseMode.Idle || mode == PoseMode.Walk || mode == PoseMode.Run || mode == PoseMode.LookAround || mode == PoseMode.Guard))
                AimPose(Mathf.Clamp01(AimWeight), Mathf.Clamp(AimPitch, -50f, 50f));

            // Head and neck look at the target while upright and conscious.
            if (LookTarget.HasValue && doll.Vitals.Conscious && (State == RagdollState.Standing || mode == PoseMode.Writhe))
            {
                var chest = Rb(PartType.Chest) ?? pelvis;
                var local = chest.transform.InverseTransformPoint(LookTarget.Value);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -75f, 75f);
                float pitch = Mathf.Clamp(-Mathf.Atan2(local.y - 0.4f, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 35f);
                targets[(int)PartType.Neck] += new Vector3(pitch * 0.4f, yaw * 0.4f, 0);
                targets[(int)PartType.Head] += new Vector3(pitch * 0.6f, yaw * 0.6f, 0);
            }
        }

        /// <summary>Gun held by the right hand along the hand's -Y axis: pistol = arms out straight, rifle = hip/chest hold.</summary>
        void AimPose(float w, float pitch)
        {
            void Blend(PartType t, float x, float y, float z) => targets[(int)t] = Vector3.Lerp(targets[(int)t], new Vector3(x, y, z), w);
            if (AimRifle)
            {
                Blend(PartType.UpperArmR, -18f - pitch * 0.3f, 0, -6f);
                Blend(PartType.ForearmR, -72f - pitch * 0.7f, 0, 0);
                Blend(PartType.HandR, 0f, 0, 0);
                Blend(PartType.UpperArmL, -55f - pitch * 0.5f, 0, 22f);
                Blend(PartType.ForearmL, -55f, 0, 0);
            }
            else
            {
                Blend(PartType.UpperArmR, -88f - pitch, 0, -8f);
                Blend(PartType.ForearmR, -4f, 0, 0);
                Blend(PartType.HandR, 0f, 0, 0);
                Blend(PartType.UpperArmL, -78f - pitch, 0, 20f);
                Blend(PartType.ForearmL, -14f, 0, 0);
            }
        }

        float Limp(PartType thigh, PartType shin)
        {
            float f = Mathf.Min(doll.Parts.TryGetValue(thigh, out var a) ? a.Function : 0f, doll.Parts.TryGetValue(shin, out var b) ? b.Function : 0f);
            return Mathf.Lerp(0.3f, 1f, f);
        }

        void Clutch(bool neck, float weight)
        {
            // Hand(s) go to the wound: neck or torso.
            float ua = neck ? -100f : -32f, z = neck ? -35f : -28f, fa = neck ? -142f : -100f;
            bool both = weight > 0.8f;
            var src = doll.Vitals;
            bool useRight = !doll.Parts.TryGetValue(PartType.UpperArmR, out var r) || r.Function > 0.3f;
            for (int i = 0; i < 2; i++)
            {
                bool right = i == 0;
                if (!both && right != useRight) continue;
                var uaT = right ? PartType.UpperArmR : PartType.UpperArmL;
                var faT = right ? PartType.ForearmR : PartType.ForearmL;
                var hT = right ? PartType.HandR : PartType.HandL;
                float s = right ? 1f : -1f;
                targets[(int)uaT] = Vector3.Lerp(targets[(int)uaT], new Vector3(ua, 0, z * s), weight);
                targets[(int)faT] = Vector3.Lerp(targets[(int)faT], new Vector3(fa, 0, 0), weight);
                targets[(int)hT] = Vector3.Lerp(targets[(int)hT], new Vector3(25f, 0, 0), weight);
            }
        }
    }
}
