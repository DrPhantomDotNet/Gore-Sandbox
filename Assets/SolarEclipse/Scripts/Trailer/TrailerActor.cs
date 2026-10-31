using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A full-body pose: joint angles (degrees, local to the parent part) plus a pelvis offset.</summary>
    public sealed class TPose
    {
        public readonly Dictionary<PartType, Vector3> E = new Dictionary<PartType, Vector3>();
        public Vector3 Off;
        public TPose Set(PartType p, float x, float y = 0f, float z = 0f) { E[p] = new Vector3(x, y, z); return this; }
        public TPose O(float x, float y, float z) { Off = new Vector3(x, y, z); return this; }
        public TPose Copy() { var c = new TPose { Off = Off }; foreach (var kv in E) c.E[kv.Key] = kv.Value; return c; }
        /// <summary>This pose with another pose's joints laid over it.</summary>
        public TPose With(TPose over) { var c = Copy(); foreach (var kv in over.E) c.E[kv.Key] = kv.Value; if (over.Off != Vector3.zero) c.Off = over.Off; return c; }
        public Vector3 Get(PartType p) => E.TryGetValue(p, out var v) ? v : TrailerPoses.Stand.E.TryGetValue(p, out var s) ? s : Vector3.zero;
    }

    public struct TKey
    {
        public float T; public TPose P; public bool Snap;
        public TKey(float t, TPose p, bool snap = false) { T = t; P = p; Snap = snap; }
    }

    /// <summary>
    /// A Goredoll played like a puppet for the trailer: its body parts are kinematic and driven by hand-made keyframe
    /// clips (with smooth blending, a running cycle, breathing and head turns) until Release(), when the real ragdoll,
    /// physics and gore take over with the momentum of the last frame. Animation runs on scaled time, so slow motion
    /// slows the actors too.
    /// </summary>
    public sealed class TrailerActor : MonoBehaviour
    {
        public Goredoll Doll;
        public bool Puppet { get; private set; }
        public WeaponModel Weapon;
        public float Gait;               // 0 standing .. 1 running (legs + arm swing)
        public float GaitSpeed = 6f;     // m/s used for the stride
        public bool ArmsBusy = true;     // running: keep the clip's arms (holding a weapon)
        public Vector3? LookAt;
        public float Breathe = 1f;

        float time, clipStart, fadeStart = -1f, phase;
        TKey[] clip;
        TPose fadeFrom, last = TrailerPoses.Stand;
        Vector3 moveFrom, moveTo; float moveStart, moveDur = -1f, moveEase = 1f;
        float yawFrom, yawTo, turnStart, turnDur = -1f;
        readonly Dictionary<PartType, Vector3> prevWorld = new Dictionary<PartType, Vector3>();
        readonly Dictionary<PartType, Vector3> vel = new Dictionary<PartType, Vector3>();
        static readonly PartType[] Order =
        {
            PartType.Pelvis, PartType.Abdomen, PartType.Chest, PartType.Neck, PartType.Head,
            PartType.UpperArmL, PartType.ForearmL, PartType.HandL, PartType.UpperArmR, PartType.ForearmR, PartType.HandR,
            PartType.ThighL, PartType.ShinL, PartType.FootL, PartType.ThighR, PartType.ShinR, PartType.FootR,
        };

        // ------------------------------------------------------------------ creation

        public static TrailerActor Spawn(Vector3 feet, float yaw, CustomLook look = null, string preset = "Normal", int seed = 0)
        {
            string p = look != null ? preset + "~c" + look.Encode() : preset;
            var d = GoredollFactory.Spawn(feet, yaw, p, -1, null, seed == 0 ? Random.Range(1, 999999) : seed);
            if (d == null) return null;
            var a = d.gameObject.AddComponent<TrailerActor>();
            a.Doll = d;
            a.Hold();
            return a;
        }

        /// <summary>Take the body over (kinematic puppet).</summary>
        public void Hold()
        {
            Puppet = true;
            if (Doll.Brain != null) Doll.Brain.enabled = false;
            var ab = Doll.GetComponent<ArmedBrain>(); if (ab != null) ab.enabled = false;
            if (Doll.Ragdoll != null) Doll.Ragdoll.enabled = false;
            foreach (var part in Doll.Parts.Values)
            {
                if (part == null || part.Body == null) continue;
                part.Body.isKinematic = true;
                part.Body.interpolation = RigidbodyInterpolation.None;
            }
            if (Doll.Face != null) Doll.Face.Expression = Expression.Neutral;
        }

        public TrailerActor Look(Expression e) { if (Doll.Face != null) Doll.Face.Expression = e; return this; }

        /// <summary>Weapon in the right hand. Guns point along the arm; blades stick out of the fist.</summary>
        public WeaponModel Arm(string id, bool blade)
        {
            if (Weapon != null) Destroy(Weapon.gameObject);
            Weapon = null;
            if (!Doll.Parts.TryGetValue(PartType.HandR, out var hand) || hand == null) return null;
            if (!blade) { Weapon = ArmedDolls.MountGun(hand.transform, id, Doll.gameObject.layer); return Weapon; }
            WeaponModel m;
            try { m = WeaponModels.Build(id, Doll.gameObject.layer); } catch (System.Exception e) { Debug.LogWarning("[Trailer] weapon " + id + ": " + e.Message); return null; }
            if (m == null) return null;
            var rot = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            var grip = m.GripR != null ? new Vector3(0f, m.GripR.localPosition.y, m.GripR.localPosition.z) : Vector3.zero;
            m.transform.SetParent(hand.transform, false);
            m.transform.localRotation = rot;
            m.transform.localPosition = new Vector3(0f, -0.085f, 0.0f) - rot * grip;
            foreach (var t in m.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Doll.gameObject.layer;
            Weapon = m;
            return m;
        }

        public Vector3 Muzzle => Weapon != null && Weapon.Muzzle != null ? Weapon.Muzzle.position : Part(PartType.HandR).position + transform.forward * 0.4f;
        public Vector3 MuzzleDir => Weapon != null ? (Weapon.Muzzle != null ? Weapon.Muzzle.forward : Weapon.transform.forward) : transform.forward;

        public Transform Part(PartType t) => Doll.Parts.TryGetValue(t, out var p) && p != null ? p.transform : transform;
        public BodyPart BodyPart(PartType t) => Doll.Parts.TryGetValue(t, out var p) ? p : null;
        public Vector3 Chest => Part(PartType.Chest).position + Vector3.up * 0.15f;
        public Vector3 Head => Part(PartType.Head).position + Vector3.up * 0.1f;

        // ------------------------------------------------------------------ control

        /// <summary>Play keyframes (times in seconds from now, scaled time). Blends from the current pose.</summary>
        public TrailerActor Play(params TKey[] keys)
        {
            fadeFrom = last.Copy();
            fadeStart = time;
            clip = keys;
            clipStart = time;
            return this;
        }

        public TrailerActor Pose(TPose p) => Play(new TKey(0f, p));

        public TrailerActor MoveTo(Vector3 to, float duration, float ease = 1f)
        {
            moveFrom = transform.position; moveTo = to; moveStart = time; moveDur = Mathf.Max(0.01f, duration); moveEase = ease;
            return this;
        }

        public TrailerActor Turn(float yaw, float duration)
        {
            yawFrom = transform.eulerAngles.y; yawTo = yawFrom + Mathf.DeltaAngle(yawFrom, yaw); turnStart = time; turnDur = Mathf.Max(0.01f, duration);
            return this;
        }

        /// <summary>Spin a full turn (or more) over a duration.</summary>
        public TrailerActor Spin(float degrees, float duration)
        {
            yawFrom = transform.eulerAngles.y; yawTo = yawFrom + degrees; turnStart = time; turnDur = Mathf.Max(0.01f, duration);
            return this;
        }

        public void Face(Vector3 target)
        {
            var d = target - transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }

        /// <summary>Let go: the real ragdoll takes over with the motion it had, plus a push.</summary>
        public void Release(Vector3 push = default, bool dead = true)
        {
            if (!Puppet) return;
            Puppet = false;
            foreach (var part in Doll.Parts.Values)
            {
                if (part == null || part.Body == null) continue;
                part.Body.isKinematic = false;
                part.Body.interpolation = RigidbodyInterpolation.Interpolate;
                vel.TryGetValue(part.Type, out var v);
                part.Body.velocity = Vector3.ClampMagnitude(v, 25f) + push;
            }
            if (Doll.Ragdoll != null) { Doll.Ragdoll.enabled = true; Doll.Ragdoll.Fall(4f); }
            if (dead) Doll.Kill("Trailer");
            if (Doll.Face != null) Doll.Face.Expression = dead ? Expression.Dead : Expression.Pain;
        }

        // ------------------------------------------------------------------ gore helpers (release first)

        public void Slice(PartType t, Vector3 planeNormal, Vector3 push = default)
        {
            Release(push);
            var p = BodyPart(t);
            if (p != null && Doll.Dismember != null) Doll.Dismember.Slice(p, p.Col != null ? p.Col.bounds.center : p.transform.position, planeNormal);
        }

        public void Sever(PartType t, Vector3 dir, Vector3 push = default)
        {
            Release(push);
            var p = BodyPart(t);
            if (p != null && Doll.Dismember != null) Doll.Dismember.Sever(p, p.transform.position, -dir, false);
        }

        public void Gib(PartType t, Vector3 dir, Vector3 push = default)
        {
            Release(push);
            var p = BodyPart(t);
            if (p != null && Doll.Dismember != null) Doll.Dismember.Gib(p, dir);
        }

        public void Hit(PartType t, Vector3 dir, float amount, DamageType type, float force = 200f)
        {
            var p = BodyPart(t);
            if (p == null) return;
            var pt = p.Col != null ? p.Col.bounds.center : p.transform.position;
            p.TakeDamage(new DamageInfo { Amount = amount, Type = type, Point = pt - dir * 0.08f, Direction = dir, Normal = -dir, Force = dir * force, Weapon = "Trailer", Caliber = 9f, Penetration = 1f });
        }

        // ------------------------------------------------------------------ evaluation

        void LateUpdate()
        {
            if (Doll == null) { Destroy(this); return; }
            float dt = Time.deltaTime;
            time += dt;
            if (!Puppet) return;
            if (Doll.IsDead) { Release(); return; } // killed by something else (explosion, bullets): let it fall

            // Root motion
            if (moveDur > 0f)
            {
                float u = Mathf.Clamp01((time - moveStart) / moveDur);
                float e = moveEase >= 1f ? u * u * (3f - 2f * u) : u;
                transform.position = Vector3.Lerp(moveFrom, moveTo, e);
                if (u >= 1f) moveDur = -1f;
            }
            if (turnDur > 0f)
            {
                float u = Mathf.Clamp01((time - turnStart) / turnDur);
                float e = u * u * (3f - 2f * u);
                transform.rotation = Quaternion.Euler(0, Mathf.Lerp(yawFrom, yawTo, e), 0);
                if (u >= 1f) turnDur = -1f;
            }

            var pose = Evaluate();
            // Running: legs and (free) arms from the cycle.
            if (Gait > 0.01f)
            {
                phase += dt * GaitSpeed * 1.25f;
                var run = TrailerPoses.Run(phase);
                foreach (var t in new[] { PartType.ThighL, PartType.ShinL, PartType.FootL, PartType.ThighR, PartType.ShinR, PartType.FootR })
                    pose.E[t] = Vector3.Lerp(pose.Get(t), run.Get(t), Gait);
                if (!ArmsBusy)
                    foreach (var t in new[] { PartType.UpperArmL, PartType.ForearmL, PartType.UpperArmR, PartType.ForearmR })
                        pose.E[t] = Vector3.Lerp(pose.Get(t), run.Get(t), Gait);
                pose.E[PartType.Chest] = pose.Get(PartType.Chest) + new Vector3(8f * Gait, 0, 0);
                pose.Off += new Vector3(0, (Mathf.Abs(Mathf.Cos(phase)) * 0.05f - 0.03f) * Gait, 0);
            }
            // Breathing + head aim
            if (Breathe > 0f)
            {
                float b = Mathf.Sin(time * 2.2f) * Breathe;
                pose.E[PartType.Chest] = pose.Get(PartType.Chest) + new Vector3(b * 1.5f, 0, 0);
                pose.E[PartType.UpperArmL] = pose.Get(PartType.UpperArmL) + new Vector3(0, 0, -b * 1f);
                pose.E[PartType.UpperArmR] = pose.Get(PartType.UpperArmR) + new Vector3(0, 0, b * 1f);
            }
            last = pose;
            Apply(pose);
        }

        TPose Evaluate()
        {
            TPose target;
            if (clip == null || clip.Length == 0) target = last.Copy();
            else
            {
                float t = time - clipStart;
                int i = 0;
                while (i < clip.Length - 1 && t >= clip[i + 1].T) i++;
                if (i >= clip.Length - 1 || t <= clip[0].T) target = (t <= clip[0].T ? clip[0] : clip[clip.Length - 1]).P.Copy();
                else
                {
                    var a = clip[i]; var b = clip[i + 1];
                    float u = Mathf.Clamp01((t - a.T) / Mathf.Max(0.001f, b.T - a.T));
                    if (b.Snap) u = u < 1f ? 0f : 1f;
                    u = u * u * (3f - 2f * u);
                    target = Blend(a.P, b.P, u);
                }
            }
            // Cross-fade from where we were.
            if (fadeFrom != null)
            {
                float f = Mathf.Clamp01((time - fadeStart) / 0.12f);
                if (f < 1f) target = Blend(fadeFrom, target, f * f * (3f - 2f * f));
                else fadeFrom = null;
            }
            return target;
        }

        static readonly List<PartType> keysTmp = new List<PartType>();
        static TPose Blend(TPose a, TPose b, float u)
        {
            var r = new TPose { Off = Vector3.Lerp(a.Off, b.Off, u) };
            keysTmp.Clear();
            foreach (var k in a.E.Keys) keysTmp.Add(k);
            foreach (var k in b.E.Keys) if (!a.E.ContainsKey(k)) keysTmp.Add(k);
            foreach (var k in keysTmp)
            {
                var qa = Quaternion.Euler(a.Get(k)); var qb = Quaternion.Euler(b.Get(k));
                r.E[k] = Quaternion.Slerp(qa, qb, u).eulerAngles;
            }
            return r;
        }

        void Apply(TPose p)
        {
            var worldRot = new Dictionary<PartType, Quaternion>(17);
            var pos = new Dictionary<PartType, Vector3>(17);
            foreach (var t in Order)
            {
                if (!Doll.Parts.TryGetValue(t, out var part) || part == null || part.Severed || part.Gibbed) continue;
                var spec = Anatomy.Parts[t];
                var e = p.Get(t);
                // Head aim toward a point.
                if (t == PartType.Head && LookAt.HasValue)
                {
                    var local = transform.InverseTransformPoint(LookAt.Value) - (spec.Pivot + p.Off);
                    float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70f, 70f);
                    float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -40f, 40f);
                    e = new Vector3(pitch * 0.6f, yaw * 0.6f, 0);
                }
                var q = Quaternion.Euler(e);
                Quaternion wr; Vector3 wp;
                if (spec.Parent.HasValue && worldRot.TryGetValue(spec.Parent.Value, out var pr))
                {
                    var pspec = Anatomy.Parts[spec.Parent.Value];
                    wr = pr * q;
                    wp = pos[spec.Parent.Value] + pr * (spec.Pivot - pspec.Pivot);
                }
                else { wr = q; wp = spec.Pivot + p.Off; }
                worldRot[t] = wr; pos[t] = wp;
                var tr = part.transform;
                tr.localPosition = wp;
                tr.localRotation = wr;
                // Velocity for the hand-off to physics.
                var w = tr.position;
                if (prevWorld.TryGetValue(t, out var pw) && Time.deltaTime > 0.0001f) vel[t] = Vector3.Lerp(vel.TryGetValue(t, out var ov) ? ov : Vector3.zero, (w - pw) / Time.deltaTime, 0.5f);
                prevWorld[t] = w;
            }
        }
    }

    /// <summary>The trailer's pose library (degrees). X- swings a limb forward / up; chest and head X+ lean forward.</summary>
    public static class TrailerPoses
    {
        static TPose P() => new TPose();

        public static readonly TPose Stand = P()
            .Set(PartType.UpperArmL, 0, 0, -5).Set(PartType.UpperArmR, 0, 0, 5).Set(PartType.ForearmL, -8).Set(PartType.ForearmR, -8);

        /// <summary>Running cycle at a phase (radians).</summary>
        public static TPose Run(float ph)
        {
            float s = Mathf.Sin(ph);
            return P()
                .Set(PartType.ThighL, -38f * s - 8f).Set(PartType.ThighR, 38f * s - 8f)
                .Set(PartType.ShinL, Mathf.Max(0f, s) * 75f + 12f).Set(PartType.ShinR, Mathf.Max(0f, -s) * 75f + 12f)
                .Set(PartType.FootL, -10f).Set(PartType.FootR, -10f)
                .Set(PartType.UpperArmL, 35f * s - 5f, 0, -8).Set(PartType.UpperArmR, -35f * s - 5f, 0, 8)
                .Set(PartType.ForearmL, -75f).Set(PartType.ForearmR, -75f);
        }

        // ---------------- fighter stances
        static TPose Legs(float spread, float bend) => P()
            .Set(PartType.ThighL, -bend * 0.8f, 0, -spread).Set(PartType.ShinL, bend * 1.2f).Set(PartType.FootL, -bend * 0.4f)
            .Set(PartType.ThighR, bend * 0.3f, 0, spread).Set(PartType.ShinR, bend * 0.9f).Set(PartType.FootR, -bend * 0.9f)
            .O(0, -bend * 0.0035f, 0);

        public static TPose BladeGuard => Legs(9, 28)
            .Set(PartType.Abdomen, 4, 10).Set(PartType.Chest, 8, 18).Set(PartType.Neck, -4, -12).Set(PartType.Head, -4, -14)
            .Set(PartType.UpperArmR, -35, 0, -12).Set(PartType.ForearmR, -80).Set(PartType.HandR, -25)
            .Set(PartType.UpperArmL, -42, 0, 28).Set(PartType.ForearmL, -85).Set(PartType.HandL, -20);

        public static TPose DrawBlade => Legs(6, 10)
            .Set(PartType.Chest, 2, -25).Set(PartType.Head, 0, 20)
            .Set(PartType.UpperArmR, -20, 0, -35).Set(PartType.ForearmR, -110).Set(PartType.HandR, -10)
            .Set(PartType.UpperArmL, 10, 0, -15).Set(PartType.ForearmL, -60);

        public static TPose ShoulderRoll => Stand.With(P().Set(PartType.Chest, -4, 0, 6).Set(PartType.Neck, 0, 0, -18).Set(PartType.Head, -6, 10, -12)
            .Set(PartType.UpperArmL, -5, 0, -14).Set(PartType.UpperArmR, -5, 0, 14));

        public static TPose SlashWindup => Legs(10, 32)
            .Set(PartType.Abdomen, -4, 25).Set(PartType.Chest, -6, 35).Set(PartType.Neck, 0, -20).Set(PartType.Head, 0, -25)
            .Set(PartType.UpperArmR, -150, 0, 35).Set(PartType.ForearmR, -45).Set(PartType.HandR, -20)
            .Set(PartType.UpperArmL, -130, 0, 5).Set(PartType.ForearmL, -55);

        public static TPose SlashFollow => Legs(12, 36).O(0, -0.14f, 0.12f)
            .Set(PartType.Abdomen, 10, -25).Set(PartType.Chest, 18, -40).Set(PartType.Neck, -6, 25).Set(PartType.Head, -6, 25)
            .Set(PartType.UpperArmR, -45, 0, -55).Set(PartType.ForearmR, -15).Set(PartType.HandR, -30)
            .Set(PartType.UpperArmL, -20, 0, -10).Set(PartType.ForearmL, -40);

        public static TPose BladeOut => Legs(14, 30)   // blade held out to the side for the spin
            .Set(PartType.Chest, 10, 0).Set(PartType.UpperArmR, -80, 0, 70).Set(PartType.ForearmR, -5).Set(PartType.HandR, -60)
            .Set(PartType.UpperArmL, -40, 0, -60).Set(PartType.ForearmL, -20);

        public static TPose Thrust => Legs(4, 20).O(0, -0.12f, 0.35f)
            .Set(PartType.ThighR, -45, 0, 6).Set(PartType.ShinR, 55).Set(PartType.ThighL, 30, 0, -4).Set(PartType.ShinL, 15)
            .Set(PartType.Chest, 16, -8).Set(PartType.Head, -10, 6)
            .Set(PartType.UpperArmR, -88, 0, -4).Set(PartType.ForearmR, -4).Set(PartType.HandR, -88)
            .Set(PartType.UpperArmL, -60, 0, 20).Set(PartType.ForearmL, -60);

        public static TPose DodgeBack => Legs(10, 40).O(0, -0.12f, -0.2f)
            .Set(PartType.Abdomen, -12).Set(PartType.Chest, -22, 10).Set(PartType.Head, 10)
            .Set(PartType.UpperArmR, -60, 0, 40).Set(PartType.ForearmR, -50).Set(PartType.HandR, -30)
            .Set(PartType.UpperArmL, -10, 0, -50).Set(PartType.ForearmL, -40);

        // ---------------- guns
        public static TPose AimPistol => Legs(7, 14)
            .Set(PartType.Chest, 4, 8).Set(PartType.Head, 2, -6)
            .Set(PartType.UpperArmR, -86, 0, -8).Set(PartType.ForearmR, -4)
            .Set(PartType.UpperArmL, -78, 0, 20).Set(PartType.ForearmL, -28);

        public static TPose AimRifle => Legs(9, 18)
            .Set(PartType.Chest, 6, 14).Set(PartType.Head, 4, -12)
            .Set(PartType.UpperArmR, -70, 0, -14).Set(PartType.ForearmR, -24)
            .Set(PartType.UpperArmL, -68, 0, 34).Set(PartType.ForearmL, -38);

        public static TPose Recoil => AimRifle.With(P().Set(PartType.Chest, -6, 18).Set(PartType.UpperArmR, -96, 0, -14).Set(PartType.ForearmR, -30).Set(PartType.UpperArmL, -92, 0, 34).Set(PartType.Head, -6, -12)).O(0, -0.06f, -0.1f);

        public static TPose GunDown => Legs(5, 6).Set(PartType.UpperArmR, -30, 0, -6).Set(PartType.ForearmR, -20).Set(PartType.UpperArmL, -5, 0, -8);

        // ---------------- heavy weapon
        public static TPose HammerUp => Legs(14, 18)
            .Set(PartType.Abdomen, -10).Set(PartType.Chest, -18).Set(PartType.Neck, -10).Set(PartType.Head, -14)
            .Set(PartType.UpperArmR, -175, 0, 12).Set(PartType.ForearmR, -40).Set(PartType.HandR, -50)
            .Set(PartType.UpperArmL, -172, 0, -12).Set(PartType.ForearmL, -45).Set(PartType.HandL, -40);

        public static TPose HammerDown => Legs(16, 46).O(0, -0.22f, 0.15f)
            .Set(PartType.Abdomen, 22).Set(PartType.Chest, 32).Set(PartType.Head, -20)
            .Set(PartType.UpperArmR, -55, 0, -5).Set(PartType.ForearmR, -8).Set(PartType.HandR, -70)
            .Set(PartType.UpperArmL, -55, 0, 5).Set(PartType.ForearmL, -10).Set(PartType.HandL, -60);

        // ---------------- knife
        public static TPose KnifeGuard => Legs(10, 26)
            .Set(PartType.Chest, 12, 10).Set(PartType.Head, -10, -8)
            .Set(PartType.UpperArmR, -50, 0, -10).Set(PartType.ForearmR, -70).Set(PartType.HandR, -40)
            .Set(PartType.UpperArmL, -55, 0, 18).Set(PartType.ForearmL, -95);

        public static TPose Block => Legs(10, 26).O(0, -0.04f, -0.08f)
            .Set(PartType.Chest, -4, -15).Set(PartType.UpperArmL, -95, 0, 25).Set(PartType.ForearmL, -95)
            .Set(PartType.UpperArmR, -60, 0, -20).Set(PartType.ForearmR, -90).Set(PartType.HandR, -60);

        public static TPose Elbow => Legs(12, 28).O(0, -0.05f, 0.18f)
            .Set(PartType.Chest, 14, -45).Set(PartType.Head, -6, 30)
            .Set(PartType.UpperArmR, -95, 0, 55).Set(PartType.ForearmR, -140)
            .Set(PartType.UpperArmL, -40, 0, -10).Set(PartType.ForearmL, -60);

        public static TPose ThroatSlash => Legs(12, 30).O(0, -0.08f, 0.2f)
            .Set(PartType.Chest, 10, -50).Set(PartType.Head, -8, 35)
            .Set(PartType.UpperArmR, -88, 0, -60).Set(PartType.ForearmR, -12).Set(PartType.HandR, -80)
            .Set(PartType.UpperArmL, -60, 0, 30).Set(PartType.ForearmL, -70);

        // ---------------- enemies / reactions
        public static TPose Menace => Legs(12, 22)
            .Set(PartType.Chest, 14).Set(PartType.Head, -12)
            .Set(PartType.UpperArmL, -25, 0, -28).Set(PartType.ForearmL, -70).Set(PartType.UpperArmR, -25, 0, 28).Set(PartType.ForearmR, -70);

        public static TPose PipeUp => Legs(8, 18)
            .Set(PartType.Chest, -8, 20).Set(PartType.UpperArmR, -165, 0, 20).Set(PartType.ForearmR, -50).Set(PartType.HandR, -30)
            .Set(PartType.UpperArmL, -30, 0, -20).Set(PartType.ForearmL, -50);

        public static TPose PipeSwing => Legs(10, 30).O(0, -0.08f, 0.2f)
            .Set(PartType.Chest, 20, -30).Set(PartType.UpperArmR, -60, 0, -50).Set(PartType.ForearmR, -10).Set(PartType.HandR, -40)
            .Set(PartType.UpperArmL, -10, 0, -20).Set(PartType.ForearmL, -40);

        public static TPose Flinch => Legs(8, 18).O(0, -0.03f, -0.12f)
            .Set(PartType.Abdomen, -14).Set(PartType.Chest, -20, 12).Set(PartType.Neck, -10).Set(PartType.Head, -22, -15)
            .Set(PartType.UpperArmL, -60, 0, -40).Set(PartType.ForearmL, -60).Set(PartType.UpperArmR, -40, 0, 50).Set(PartType.ForearmR, -40);

        public static TPose Scream => Legs(10, 10)
            .Set(PartType.Chest, -10).Set(PartType.Head, -25)
            .Set(PartType.UpperArmL, -10, 0, -55).Set(PartType.ForearmL, -30).Set(PartType.UpperArmR, -10, 0, 55).Set(PartType.ForearmR, -30);

        // ---------------- sandbox idles
        public static TPose NeckStretch => Stand.With(P().Set(PartType.Neck, 0, 0, 22).Set(PartType.Head, 0, 0, 18).Set(PartType.UpperArmR, -10, 0, 30).Set(PartType.ForearmR, -120));
        public static TPose LookHands => Stand.With(P().Set(PartType.Chest, 12).Set(PartType.Neck, 18).Set(PartType.Head, 25)
            .Set(PartType.UpperArmL, -55, 0, 12).Set(PartType.ForearmL, -70).Set(PartType.HandL, 40)
            .Set(PartType.UpperArmR, -55, 0, -12).Set(PartType.ForearmR, -70).Set(PartType.HandR, 40));
        public static TPose Fall => P()
            .Set(PartType.Chest, -15).Set(PartType.Head, -20)
            .Set(PartType.UpperArmL, -150, 0, -40).Set(PartType.ForearmL, -30).Set(PartType.UpperArmR, -120, 0, 50).Set(PartType.ForearmR, -40)
            .Set(PartType.ThighL, -40).Set(PartType.ShinL, 60).Set(PartType.ThighR, 10).Set(PartType.ShinR, 20);
        public static TPose EdgeStand => Stand.With(P().Set(PartType.Chest, 18).Set(PartType.Head, 30).Set(PartType.UpperArmL, -20, 0, -40).Set(PartType.UpperArmR, -20, 0, 40));

        // ---------------- crew walk (gun up)
        public static TPose CrewWalk => P()
            .Set(PartType.Chest, 4, 6).Set(PartType.UpperArmR, -60, 0, -8).Set(PartType.ForearmR, -40)
            .Set(PartType.UpperArmL, -55, 0, 25).Set(PartType.ForearmL, -55);
        public static TPose Swagger => P()
            .Set(PartType.Chest, -4).Set(PartType.Head, -6)
            .Set(PartType.UpperArmR, -30, 0, 15).Set(PartType.ForearmR, -60).Set(PartType.UpperArmL, 5, 0, -10).Set(PartType.ForearmL, -15);
        public static TPose CoverFire => Legs(16, 60).O(0, -0.38f, 0)
            .Set(PartType.Chest, 12, 30).Set(PartType.Head, 0, -25)
            .Set(PartType.UpperArmR, -90, 0, -20).Set(PartType.ForearmR, -6).Set(PartType.UpperArmL, -80, 0, 10).Set(PartType.ForearmL, -30);
    }
}
