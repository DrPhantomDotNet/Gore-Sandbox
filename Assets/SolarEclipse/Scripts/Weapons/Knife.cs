using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Combat knife. LMB = diagonal slash that sweeps rays through the arc and procedurally slices limbs exactly
    /// where the blade crossed them. RMB = forward stab (deep penetrating wound). Q = flip inspect.
    /// </summary>
    public sealed class Knife : Weapon
    {
        enum Action { None, Slash, Stab }
        Action action;
        float t;
        int slashDir = 1;
        bool hitSomething, impactDone;
        Vector3 lastSampleDir;
        static readonly Vector3 Idle = new Vector3(0.15f, -0.17f, 0.32f);

        public override bool Busy => action != Action.None;

        protected override void OnEquip()
        {
            Vm.LeftOnAnchor = false;
            Vm.Left.SetPose(HandPose.Fist);
            Vm.Right.SetPose(HandPose.Knife);
            action = Action.None;
        }

        protected override void OnHolster() => Vm.Right.SetPose(HandPose.Relaxed);

        public override void Tick(InputFrame f, float dt)
        {
            if (action == Action.None)
            {
                if (f.Fire) Begin(Action.Slash);
                else if (f.AimPressed) Begin(Action.Stab);
            }

            var pos = Idle + new Vector3(0, Mathf.Sin(Time.time * 1.7f) * 0.003f, 0);
            var rot = Quaternion.Euler(-8f, -12f, -12f);

            if (action == Action.Slash)
            {
                t += dt / 0.42f;
                // Wind-up (0–0.22) → sweep across (0.22–0.55) → recover.
                float s = slashDir;
                var wind = new Vector3(0.24f * s, -0.04f, 0.28f);
                var through = new Vector3(-0.16f * s, -0.24f, 0.46f);
                var windRot = Quaternion.Euler(-25f, 35f * s, -70f * s);
                var throughRot = Quaternion.Euler(35f, -55f * s, -40f * s);
                if (t < 0.22f) { float k = Ease(t / 0.22f); pos = Vector3.Lerp(pos, wind, k); rot = Quaternion.Slerp(rot, windRot, k); }
                else if (t < 0.55f) { float k = Ease((t - 0.22f) / 0.33f); pos = Vector3.Lerp(wind, through, k); rot = Quaternion.Slerp(windRot, throughRot, k); SweepRays(k); }
                else { float k = Ease((t - 0.55f) / 0.45f); pos = Vector3.Lerp(through, pos, k); rot = Quaternion.Slerp(throughRot, rot, k); }
                if (t >= 1f) action = Action.None;
            }
            else if (action == Action.Stab)
            {
                t += dt / 0.5f;
                var back = Idle + new Vector3(0.02f, 0.02f, -0.08f);
                var thrust = new Vector3(0.05f, -0.1f, 0.62f);
                var thrustRot = Quaternion.Euler(-4f, -6f, -85f);
                if (t < 0.25f) { float k = Ease(t / 0.25f); pos = Vector3.Lerp(pos, back, k); }
                else if (t < 0.42f) { float k = Ease((t - 0.25f) / 0.17f); pos = Vector3.Lerp(back, thrust, k); rot = Quaternion.Slerp(rot, thrustRot, k); }
                else { float k = Ease((t - 0.42f) / 0.58f); pos = Vector3.Lerp(thrust, pos, k); rot = Quaternion.Slerp(thrustRot, rot, k); }
                if (!impactDone && t >= 0.4f) { impactDone = true; Stab(); }
                if (t >= 1f) action = Action.None;
            }
            else TickInspect(f, dt, ref pos, ref rot, 1.8f);

            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
            // Free hand stays in a loose guard on the left.
            Vm.LeftPos = new Vector3(-0.17f, -0.2f, 0.33f) + new Vector3(0, Mathf.Sin(Time.time * 1.7f + 1f) * 0.003f, 0);
            Vm.LeftRot = Quaternion.Euler(-12f, 10f, 72f);
        }

        void Begin(Action a)
        {
            action = a;
            t = 0f;
            hitSomething = impactDone = false;
            inspectT = -1f;
            if (a == Action.Slash) { slashDir = -slashDir; lastSampleDir = SweepDir(0f); }
            Swing(a == Action.Slash ? "whoosh_small" : "whoosh_small", 0.6f);
        }

        Vector3 SweepDir(float k)
        {
            float s = slashDir;
            // Screen-space arc from upper-right to lower-left (or mirrored).
            float yaw = Mathf.Lerp(32f * s, -32f * s, k);
            float pitch = Mathf.Lerp(-18f, 22f, k);
            return CamT.rotation * Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
        }

        void SweepRays(float k)
        {
            if (hitSomething) return;
            var now = SweepDir(k);
            // Fill the gap since last frame with several rays so fast swings don't skip limbs.
            for (int i = 1; i <= 4 && !hitSomething; i++)
            {
                var dir = Vector3.Slerp(lastSampleDir, now, i / 4f);
                if (Physics.Raycast(Eye, dir, out var hit, 1.55f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                {
                    hitSomething = true;
                    var sweep = (now - lastSampleDir).sqrMagnitude > 1e-6f ? (now - lastSampleDir).normalized : CamT.right * -slashDir;
                    SlashHit(hit, dir, sweep);
                }
            }
            lastSampleDir = now;
        }

        void SlashHit(RaycastHit hit, Vector3 dir, Vector3 sweep)
        {
            var part = hit.collider.GetComponent<BodyPart>();
            float dmg = Def.Damage;
            if (part != null)
            {
                part.TakeDamage(new DamageInfo { Amount = dmg, Force = sweep * Def.Force, Point = hit.point, Normal = hit.normal, Direction = sweep, Type = DamageType.Slash, Source = Player, Weapon = Def.Name, Penetration = 0.3f });
                AudioManager.Play("slice", hit.point, 0.9f, Random.Range(0.92f, 1.08f));
                // The cutting plane contains the view ray and the blade's sweep direction.
                var planeNormal = Vector3.Cross(sweep, dir).normalized;
                bool canCut = part.CanSever && Settings.Dismemberment && part.Doll != null;
                bool cut = canCut && (Rules.AlwaysDismember || part.Health <= part.MaxHealth * 0.4f || Random.value < (part.IsHead ? 0.06f : 0.22f));
                if (cut)
                {
                    part.Doll.Dismember.Slice(part, hit.point, planeNormal);
                    AudioManager.Play("dismember", hit.point, 0.9f);
                    CameraFX.Shake(0.15f);
                }
                CameraFX.Punch(0.3f, -slashDir * 0.6f);
            }
            else
            {
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Amount = dmg * 0.6f, Force = sweep * 5f, Point = hit.point, Normal = hit.normal, Direction = sweep, Type = DamageType.Slash, Source = Player, Weapon = Def.Name });
                AudioManager.Play("knife_hit_hard", hit.point, 0.7f, Random.Range(0.9f, 1.1f));
                if (Surface.Of(hit.collider) == SurfaceType.Metal || Surface.Of(hit.collider) == SurfaceType.Concrete) FX.Sparks(hit.point, hit.normal, 6, 4f);
                Decals.BulletHole(hit.point, hit.normal, Surface.Of(hit.collider), hit.collider, 0.06f);
                CameraFX.Shake(0.12f);
                t = Mathf.Max(t, 0.55f); // blade bounces off
            }
            GameEvents.EmitNoise(hit.point, 5f, 0.4f, Player);
        }

        void Stab()
        {
            if (!MeleeTrace(Aim, 1.6f, 0.06f, out var hit)) return;
            var part = hit.collider.GetComponent<BodyPart>();
            float dmg = Def.Damage * 1.6f;
            if (part != null)
            {
                // Deep: the wound track runs into the body along the view direction (organs get hit).
                part.TakeDamage(new DamageInfo { Amount = dmg, Force = Aim * Def.Force, Point = hit.point, Normal = hit.normal, Direction = Aim, Type = DamageType.Stab, Source = Player, Weapon = Def.Name, Penetration = 0.9f });
                AudioManager.Play("stab", hit.point, 0.95f, Random.Range(0.92f, 1.08f));
                CameraFX.Punch(0.4f, 0f);
            }
            else
            {
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo { Amount = dmg * 0.5f, Point = hit.point, Normal = hit.normal, Direction = Aim, Type = DamageType.Stab, Source = Player, Weapon = Def.Name });
                AudioManager.Play("knife_hit_hard", hit.point, 0.8f);
                FX.Sparks(hit.point, hit.normal, 5, 3f);
                Decals.BulletHole(hit.point, hit.normal, Surface.Of(hit.collider), hit.collider, 0.05f);
            }
            CameraFX.Shake(0.12f);
        }
    }
}
