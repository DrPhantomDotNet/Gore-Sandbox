using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// A katana in each hand. LMB = fast slash that alternates right blade / left blade (click again during the
    /// follow-through to chain a combo, which speeds the swings up). RMB = both blades cross-slash at once (big damage,
    /// almost always cuts). Hit logic is the single <see cref="Katana"/>'s: rays swept through each blade's arc, passing
    /// through up to three bodies and slicing limbs where the blade crossed them (Dismemberment.Slice).
    /// The right blade is the weapon model itself (socket pose); the left blade is the model's "LeftBlade" child, posed
    /// in camera space every frame and converted into the socket's space. The left hand rides its SupportL anchor.
    /// </summary>
    public sealed class DualKatanas : Weapon
    {
        enum Action { None, Slash, Cross }

        sealed class Blade
        {
            public int Side;                 // +1 right, -1 left
            public Vector3 LastDir;
            public bool Blocked;
            public int HitCount;
            public readonly HashSet<Rigidbody> Bodies = new HashSet<Rigidbody>();
            public readonly HashSet<Collider> Cols = new HashSet<Collider>();
            public void Reset(Vector3 dir) { LastDir = dir; Blocked = false; HitCount = 0; Bodies.Clear(); Cols.Clear(); }
        }

        Action action;
        float t, dur;
        int combo, nextSide = 1, slashSide = 1;
        bool queued;
        readonly Blade right = new Blade { Side = 1 }, left = new Blade { Side = -1 };
        Transform leftBlade;
        const float Reach = 2.1f;

        static readonly Vector3 Idle = new Vector3(0.15f, -0.22f, 0.3f);
        static readonly Vector3 IdleEuler = new Vector3(-40f, -7f, -10f);

        public override bool Busy => action != Action.None;
        public override string ModeText => combo > 1 ? "COMBO x" + combo : "";

        protected override void OnAttach()
        {
            leftBlade = Model.transform.Find("LeftBlade");
        }

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.Knife);
            Vm.Left.SetPose(HandPose.Knife);
            action = Action.None;
            combo = 0;
            t = 0f;
            AudioManager.Play("katana_draw", Eye, 0.55f, Random.Range(0.97f, 1.03f), 0f);
            AudioManager.Play("katana_draw", Eye, 0.45f, Random.Range(1.05f, 1.12f), 0f);
        }

        protected override void OnHolster()
        {
            Vm.Right.SetPose(HandPose.Relaxed);
            Vm.Left.SetPose(HandPose.Relaxed);
        }

        // ------------------------------------------------------------------ poses (written for the right blade; the left is mirrored)

        static Vector3 Mirror(Vector3 p) => new Vector3(-p.x, p.y, p.z);
        static Quaternion Rot(Vector3 euler, int side) => side > 0 ? Quaternion.Euler(euler) : Quaternion.Euler(euler.x, -euler.y, -euler.z);
        static Vector3 Pos(Vector3 p, int side) => side > 0 ? p : Mirror(p);

        void IdlePose(int side, out Vector3 pos, out Quaternion rot)
        {
            float bob = Mathf.Sin(Time.time * 1.5f + (side > 0 ? 0f : 1.3f)) * 0.003f;
            pos = Pos(Idle + new Vector3(0, bob, 0), side);
            rot = Rot(IdleEuler, side);
        }

        /// <summary>Slash pose for one blade: wind up on its own side, cut across the body.</summary>
        void SlashPose(int side, float k, ref Vector3 pos, ref Quaternion rot)
        {
            var wind = Pos(new Vector3(0.22f, 0.03f, 0.2f), side);
            var through = Pos(new Vector3(-0.16f, -0.26f, 0.42f), side);
            var windRot = Rot(new Vector3(-55f, 40f, -60f), side);
            var throughRot = Rot(new Vector3(30f, -60f, -50f), side);
            Vector3 p0 = pos; Quaternion r0 = rot;
            if (k < 0.2f) { float e = Ease(k / 0.2f); pos = Vector3.Lerp(p0, wind, e); rot = Quaternion.Slerp(r0, windRot, e); }
            else if (k < 0.5f) { float e = Ease((k - 0.2f) / 0.3f); pos = Vector3.Lerp(wind, through, e); rot = Quaternion.Slerp(windRot, throughRot, e); }
            else { float e = Ease((k - 0.5f) / 0.5f); pos = Vector3.Lerp(through, p0, e); rot = Quaternion.Slerp(throughRot, r0, e); }
        }

        /// <summary>Cross-slash pose: both blades high and wide, then scissor down through the middle.</summary>
        void CrossPose(int side, float k, ref Vector3 pos, ref Quaternion rot)
        {
            var wind = Pos(new Vector3(0.24f, 0.1f, 0.14f), side);
            var through = Pos(new Vector3(-0.1f, -0.25f, 0.44f), side);
            var windRot = Rot(new Vector3(-80f, 35f, -55f), side);
            var throughRot = Rot(new Vector3(35f, -50f, -40f), side);
            Vector3 p0 = pos; Quaternion r0 = rot;
            if (k < 0.35f) { float e = Ease(k / 0.35f); pos = Vector3.Lerp(p0, wind, e); rot = Quaternion.Slerp(r0, windRot, e); }
            else if (k < 0.6f) { float e = Ease((k - 0.35f) / 0.25f); pos = Vector3.Lerp(wind, through, e); rot = Quaternion.Slerp(windRot, throughRot, e); }
            else { float e = Ease((k - 0.6f) / 0.4f); pos = Vector3.Lerp(through, p0, e); rot = Quaternion.Slerp(throughRot, r0, e); }
        }

        public override void Tick(InputFrame f, float dt)
        {
            Vm.ResetHands();
            if (action == Action.None)
            {
                if (f.Fire || queued) Begin(Action.Slash);
                else if (f.AimPressed) Begin(Action.Cross);
                else if (t > 0f) { t += dt; if (t > 0.9f) { combo = 0; t = 0f; } }
            }
            else if (f.Fire && action == Action.Slash && t > 0.4f) queued = true;

            IdlePose(1, out var rp, out var rr);
            IdlePose(-1, out var lp, out var lr);

            if (action == Action.Slash)
            {
                t += dt / dur;
                var b = slashSide > 0 ? right : left;
                if (slashSide > 0) SlashPose(1, t, ref rp, ref rr); else SlashPose(-1, t, ref lp, ref lr);
                // The resting blade pulls back into a guard.
                float guard = Bell(t, 0f, 1f);
                if (slashSide > 0) { lp += new Vector3(0.02f, 0.02f, -0.04f) * guard; lr *= Quaternion.Euler(-12f * guard, 0, 0); }
                else { rp += new Vector3(-0.02f, 0.02f, -0.04f) * guard; rr *= Quaternion.Euler(-12f * guard, 0, 0); }
                if (t >= 0.2f && t < 0.5f && !b.Blocked) Sweep(b, SlashDir(slashSide, (t - 0.2f) / 0.3f), false);
                if (t >= 1f || (queued && t >= 0.6f)) End();
            }
            else if (action == Action.Cross)
            {
                t += dt / dur;
                CrossPose(1, t, ref rp, ref rr);
                CrossPose(-1, t, ref lp, ref lr);
                if (t >= 0.35f && t < 0.6f)
                {
                    float k = (t - 0.35f) / 0.25f;
                    if (!right.Blocked) Sweep(right, CrossDir(1, k), true);
                    if (!left.Blocked) Sweep(left, CrossDir(-1, k), true);
                }
                if (t >= 1f) End();
            }
            else TickInspect(f, dt, ref rp, ref rr, 2f);

            Vm.TargetPos = rp;
            Vm.TargetRot = rr;
            if (leftBlade != null)
            {
                // Camera-space left pose -> socket space (the socket follows the right blade's target pose).
                var inv = Quaternion.Inverse(rr);
                leftBlade.localPosition = inv * (lp - rp);
                leftBlade.localRotation = inv * lr;
            }
        }

        void End()
        {
            action = Action.None;
            t = 0.001f; // combo window timer
        }

        void Begin(Action a)
        {
            bool chained = queued || (t > 0f && t < 0.9f);
            queued = false;
            action = a;
            t = 0f;
            inspectT = -1f;
            combo = chained ? combo + 1 : 1;
            if (a == Action.Slash)
            {
                slashSide = nextSide;
                nextSide = -nextSide;
                dur = Mathf.Lerp(0.3f, 0.21f, Mathf.Clamp01((combo - 1) / 4f));
                var b = slashSide > 0 ? right : left;
                b.Reset(SlashDir(slashSide, 0f));
                Swing("katana_swing", 0.7f);
            }
            else
            {
                dur = 0.62f;
                right.Reset(CrossDir(1, 0f));
                left.Reset(CrossDir(-1, 0f));
                Swing("katana_swing", 0.75f);
                AudioManager.Play("katana_swing", Eye + Aim * 0.5f, 0.6f, Random.Range(1.05f, 1.15f), 0f);
            }
        }

        Vector3 SlashDir(int side, float k)
        {
            float yaw = Mathf.Lerp(40f * side, -40f * side, k);
            float pitch = Mathf.Lerp(-22f, 25f, k);
            return CamT.rotation * Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
        }

        Vector3 CrossDir(int side, float k)
        {
            float yaw = Mathf.Lerp(45f * side, -35f * side, k);
            float pitch = Mathf.Lerp(-32f, 28f, k);
            return CamT.rotation * Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
        }

        void Sweep(Blade b, Vector3 now, bool cross)
        {
            for (int i = 1; i <= 5 && !b.Blocked && b.HitCount < 3; i++)
            {
                var dir = Vector3.Slerp(b.LastDir, now, i / 5f);
                var hits = Physics.RaycastAll(Eye, dir, Reach, Layers.ShootMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
                foreach (var hit in hits)
                {
                    if (b.Cols.Contains(hit.collider)) continue;
                    var part = hit.collider.GetComponent<BodyPart>();
                    var sweep = (now - b.LastDir).sqrMagnitude > 1e-6f ? (now - b.LastDir).normalized : CamT.right * -b.Side;
                    b.Cols.Add(hit.collider);
                    if (part != null)
                    {
                        if (part.Body != null && !b.Bodies.Add(part.Body)) continue;
                        FleshHit(part, hit, dir, sweep, cross);
                        if (++b.HitCount >= 3) break;
                        continue; // the blade passes through flesh
                    }
                    WorldHit(b, hit, sweep);
                    break;
                }
            }
            b.LastDir = now;
        }

        void FleshHit(BodyPart part, RaycastHit hit, Vector3 dir, Vector3 sweep, bool cross)
        {
            float dmg = Def.Damage * (cross ? 1.7f : 1f) * (1f + Mathf.Min(combo - 1, 4) * 0.07f);
            part.TakeDamage(new DamageInfo { Amount = dmg, Force = sweep * Def.Force, Point = hit.point, Normal = hit.normal, Direction = sweep, Type = DamageType.Slash, Source = Player, Weapon = Def.Name, Penetration = 0.5f });
            AudioManager.Play("slice", hit.point, 1f, Random.Range(0.9f, 1.08f));
            var planeNormal = Vector3.Cross(sweep, dir).normalized;
            bool canCut = part.CanSever && Settings.Dismemberment && part.Doll != null;
            float chance = cross ? 0.92f : (part.IsHead ? 0.3f : 0.55f);
            if (canCut && (Rules.AlwaysDismember || part.Health <= part.MaxHealth * 0.6f || Random.value < chance))
            {
                part.Doll.Dismember.Slice(part, hit.point, planeNormal);
                AudioManager.Play("dismember", hit.point, 0.9f);
                CameraFX.Shake(cross ? 0.22f : 0.15f);
            }
            CameraFX.Punch(cross ? 0.45f : 0.3f, -slashSide * 0.5f);
            GameEvents.EmitNoise(hit.point, 5f, 0.4f, Player);
        }

        void WorldHit(Blade b, RaycastHit hit, Vector3 sweep)
        {
            b.Blocked = true;
            hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Amount = Def.Damage * 0.6f, Force = sweep * 6f, Point = hit.point, Normal = hit.normal, Direction = sweep, Type = DamageType.Slash, Source = Player, Weapon = Def.Name });
            if (hit.rigidbody != null) NetPhysics.Impulse(hit.rigidbody, sweep * 8f, hit.point);
            var surf = Surface.Of(hit.collider);
            AudioManager.Play(surf == SurfaceType.Metal || surf == SurfaceType.Concrete ? "katana_clang" : "knife_hit_hard", hit.point, 0.8f, Random.Range(0.9f, 1.1f));
            if (surf == SurfaceType.Metal || surf == SurfaceType.Concrete) FX.Sparks(hit.point, hit.normal, 10, 5f);
            Decals.BulletHole(hit.point, hit.normal, surf, hit.collider, 0.08f);
            CameraFX.Shake(0.15f);
            // The blade bounces off: skip ahead to the recovery (a single slash; the cross waits for both blades).
            if (action == Action.Slash) t = Mathf.Max(t, 0.5f);
            else if (right.Blocked && left.Blocked) t = Mathf.Max(t, 0.6f);
            GameEvents.EmitNoise(hit.point, 8f, 0.5f, Player);
        }
    }
}
