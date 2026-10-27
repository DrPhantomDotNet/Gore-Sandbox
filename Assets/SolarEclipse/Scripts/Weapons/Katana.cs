using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Two-handed katana. LMB = fast diagonal slash that alternates left/right (click again during the follow-through
    /// to chain a combo, which speeds the swings up). The blade sweeps rays through its arc, passes through up to three
    /// bodies and slices limbs exactly where it crossed them (Dismemberment.Slice). RMB = overhead vertical cleave.
    /// </summary>
    public sealed class Katana : Weapon
    {
        enum Action { None, Slash, Cleave }
        Action action;
        float t, dur;
        int slashDir = 1, combo;
        bool queued, blocked;
        Vector3 lastSampleDir;
        readonly HashSet<Rigidbody> hitBodies = new HashSet<Rigidbody>();
        readonly HashSet<Collider> hitCols = new HashSet<Collider>();
        int hitCount;
        const float Reach = 2.15f;

        static readonly Vector3 Idle = new Vector3(0.12f, -0.22f, 0.3f);
        static readonly Quaternion IdleRot = Quaternion.Euler(-38f, -8f, -8f);

        public override bool Busy => action != Action.None;
        public override string ModeText => combo > 1 ? "COMBO x" + combo : "";

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.Knife);
            Vm.Left.SetPose(HandPose.Knife);
            action = Action.None;
            combo = 0;
            AudioManager.Play("katana_draw", Eye, 0.55f, Random.Range(0.97f, 1.03f), 0f);
        }

        protected override void OnHolster()
        {
            Vm.Right.SetPose(HandPose.Relaxed);
            Vm.Left.SetPose(HandPose.Relaxed);
        }

        public override void Tick(InputFrame f, float dt)
        {
            if (action == Action.None)
            {
                if (f.Fire || queued) Begin(Action.Slash);
                else if (f.AimPressed) Begin(Action.Cleave);
                else if (t > 0f) { t += dt; if (t > 0.9f) { combo = 0; t = 0f; } }
            }
            else if (f.Fire && action == Action.Slash && t > 0.45f) queued = true;

            var pos = Idle + new Vector3(0, Mathf.Sin(Time.time * 1.5f) * 0.003f, 0);
            var rot = IdleRot;

            if (action == Action.Slash)
            {
                t += dt / dur;
                float s = slashDir;
                var wind = new Vector3(0.22f * s, 0.02f, 0.2f);
                var through = new Vector3(-0.2f * s, -0.26f, 0.4f);
                var windRot = Quaternion.Euler(-55f, 40f * s, -60f * s);
                var throughRot = Quaternion.Euler(30f, -60f * s, -50f * s);
                if (t < 0.2f) { float k = Ease(t / 0.2f); pos = Vector3.Lerp(pos, wind, k); rot = Quaternion.Slerp(rot, windRot, k); }
                else if (t < 0.5f) { float k = Ease((t - 0.2f) / 0.3f); pos = Vector3.Lerp(wind, through, k); rot = Quaternion.Slerp(windRot, throughRot, k); if (!blocked) Sweep(SweepDir(k)); }
                else { float k = Ease((t - 0.5f) / 0.5f); pos = Vector3.Lerp(through, pos, k); rot = Quaternion.Slerp(throughRot, rot, k); }
                if (t >= 1f) End();
                else if (queued && t >= 0.62f) End();
            }
            else if (action == Action.Cleave)
            {
                t += dt / dur;
                var up = new Vector3(0.04f, 0.12f, 0.12f);
                var down = new Vector3(0.0f, -0.32f, 0.45f);
                var upRot = Quaternion.Euler(-95f, -4f, 0f);
                var downRot = Quaternion.Euler(45f, -4f, 0f);
                if (t < 0.35f) { float k = Ease(t / 0.35f); pos = Vector3.Lerp(pos, up, k); rot = Quaternion.Slerp(rot, upRot, k); }
                else if (t < 0.6f) { float k = Ease((t - 0.35f) / 0.25f); pos = Vector3.Lerp(up, down, k); rot = Quaternion.Slerp(upRot, downRot, k); if (!blocked) Sweep(SweepDir(k)); }
                else { float k = Ease((t - 0.6f) / 0.4f); pos = Vector3.Lerp(down, pos, k); rot = Quaternion.Slerp(downRot, rot, k); }
                if (t >= 1f) End();
            }
            else TickInspect(f, dt, ref pos, ref rot, 2f);

            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
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
            blocked = false;
            hitBodies.Clear();
            hitCols.Clear();
            hitCount = 0;
            inspectT = -1f;
            combo = chained ? combo + 1 : 1;
            if (a == Action.Slash)
            {
                slashDir = -slashDir;
                dur = Mathf.Lerp(0.36f, 0.27f, Mathf.Clamp01((combo - 1) / 3f));
            }
            else dur = 0.6f;
            lastSampleDir = SweepDir(0f);
            Swing("katana_swing", 0.7f);
        }

        Vector3 SweepDir(float k)
        {
            if (action == Action.Cleave)
            {
                float pitch = Mathf.Lerp(-45f, 35f, k);
                return CamT.rotation * Quaternion.Euler(pitch, 0f, 0) * Vector3.forward;
            }
            float s = slashDir;
            float yaw = Mathf.Lerp(40f * s, -40f * s, k);
            float pitch2 = Mathf.Lerp(-22f, 25f, k);
            return CamT.rotation * Quaternion.Euler(pitch2, yaw, 0) * Vector3.forward;
        }

        void Sweep(Vector3 now)
        {
            for (int i = 1; i <= 5 && !blocked && hitCount < 3; i++)
            {
                var dir = Vector3.Slerp(lastSampleDir, now, i / 5f);
                var hits = Physics.RaycastAll(Eye, dir, Reach, Layers.ShootMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (hitCols.Contains(hit.collider)) continue;
                    var part = hit.collider.GetComponent<BodyPart>();
                    var sweep = (now - lastSampleDir).sqrMagnitude > 1e-6f ? (now - lastSampleDir).normalized : CamT.right * -slashDir;
                    if (part != null)
                    {
                        hitCols.Add(hit.collider);
                        if (part.Body != null && !hitBodies.Add(part.Body)) continue;
                        FleshHit(part, hit, dir, sweep);
                        if (++hitCount >= 3) break;
                        continue; // the blade passes through flesh
                    }
                    hitCols.Add(hit.collider);
                    WorldHit(hit, sweep);
                    break;
                }
            }
            lastSampleDir = now;
        }

        void FleshHit(BodyPart part, RaycastHit hit, Vector3 dir, Vector3 sweep)
        {
            bool cleave = action == Action.Cleave;
            float dmg = Def.Damage * (cleave ? 1.5f : 1f) * (1f + Mathf.Min(combo - 1, 3) * 0.08f);
            part.TakeDamage(new DamageInfo { Amount = dmg, Force = sweep * Def.Force, Point = hit.point, Normal = hit.normal, Direction = sweep, Type = DamageType.Slash, Source = Player, Weapon = Def.Name, Penetration = 0.5f });
            AudioManager.Play("slice", hit.point, 1f, Random.Range(0.9f, 1.05f));
            var planeNormal = Vector3.Cross(sweep, dir).normalized;
            bool canCut = part.CanSever && Settings.Dismemberment && part.Doll != null;
            float chance = cleave ? 0.85f : (part.IsHead ? 0.35f : 0.6f);
            if (canCut && (Rules.AlwaysDismember || part.Health <= part.MaxHealth * 0.6f || Random.value < chance))
            {
                part.Doll.Dismember.Slice(part, hit.point, planeNormal);
                AudioManager.Play("dismember", hit.point, 0.9f);
                CameraFX.Shake(0.15f);
            }
            CameraFX.Punch(0.3f, -slashDir * 0.5f);
            GameEvents.EmitNoise(hit.point, 5f, 0.4f, Player);
        }

        void WorldHit(RaycastHit hit, Vector3 sweep)
        {
            blocked = true;
            hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Amount = Def.Damage * 0.6f, Force = sweep * 6f, Point = hit.point, Normal = hit.normal, Direction = sweep, Type = DamageType.Slash, Source = Player, Weapon = Def.Name });
            if (hit.rigidbody != null) NetPhysics.Impulse(hit.rigidbody, sweep * 8f, hit.point);
            var surf = Surface.Of(hit.collider);
            AudioManager.Play(surf == SurfaceType.Metal || surf == SurfaceType.Concrete ? "katana_clang" : "knife_hit_hard", hit.point, 0.8f, Random.Range(0.9f, 1.1f));
            if (surf == SurfaceType.Metal || surf == SurfaceType.Concrete) FX.Sparks(hit.point, hit.normal, 10, 5f);
            Decals.BulletHole(hit.point, hit.normal, surf, hit.collider, 0.08f);
            CameraFX.Shake(0.15f);
            if (action == Action.Slash) t = Mathf.Max(t, 0.5f); else t = Mathf.Max(t, 0.6f); // blade bounces off
            GameEvents.EmitNoise(hit.point, 8f, 0.5f, Player);
        }
    }
}
