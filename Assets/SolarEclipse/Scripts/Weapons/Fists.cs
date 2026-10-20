using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Boxing-guard fists: both hands visible with curled fingers, knuckles forward, palms turned in.
    /// LMB alternates jabs/crosses, holding LMB charges a haymaker, RMB raises a guard.
    /// </summary>
    public sealed class Fists : Weapon
    {
        // Guard pose (camera space). Right fist low-right, left fist a bit further forward (orthodox stance).
        static readonly Vector3 GuardR = new Vector3(0.155f, -0.175f, 0.33f), GuardL = new Vector3(-0.15f, -0.15f, 0.39f);
        static readonly Vector3 GuardRotR = new Vector3(-12f, -10f, -72f);
        static readonly Vector3 BlockR = new Vector3(0.075f, -0.07f, 0.24f), BlockL = new Vector3(-0.07f, -0.06f, 0.26f);
        static readonly Vector3 BlockRotR = new Vector3(-55f, -25f, -80f);

        float punchT = 1f, block, charge, idle;
        bool punchRight = true, hitDone, heavy;

        public override string AmmoText => "";
        public override string ModeText => heavy ? "HAYMAKER" : "";
        public override bool Busy => punchT < 1f;

        protected override void OnEquip()
        {
            Vm.RightOnAnchor = Vm.LeftOnAnchor = false;
            Vm.TargetPos = new Vector3(0, -0.05f, 0.3f);
            Vm.Right.SetPose(HandPose.Fist);
            Vm.Left.SetPose(HandPose.Fist);
            punchT = 1f;
        }

        protected override void OnHolster()
        {
            Vm.Right.SetPose(HandPose.Relaxed);
            Vm.Left.SetPose(HandPose.Relaxed);
        }

        static Quaternion Mirror(Vector3 e) => Quaternion.Euler(e.x, -e.y, -e.z);

        public override void Tick(InputFrame f, float dt)
        {
            idle += dt;
            block = MathX.Damp(block, f.Aim && punchT >= 1f ? 1f : 0f, 14f, dt);

            if (f.FireHeld && punchT >= 1f && block < 0.3f) charge += dt;
            if (punchT >= 1f && block < 0.3f && (f.FireUp || (f.Fire && charge < 0.01f) || charge > 1.2f))
            {
                heavy = charge > 0.4f;
                StartPunch();
                charge = 0f;
            }
            if (!f.FireHeld && punchT < 1f) charge = 0f;

            if (punchT < 1f)
            {
                float dur = heavy ? 0.5f : 0.3f;
                punchT = Mathf.Min(1f, punchT + dt / dur);
                if (!hitDone && punchT > 0.38f) { hitDone = true; DoHit(); }
            }

            // Guard sway: gentle boxer bounce.
            float bounce = Mathf.Sin(idle * 3.2f) * 0.006f * (1f - block);
            var r = Vector3.Lerp(GuardR, BlockR, block) + new Vector3(0, bounce, 0);
            var l = Vector3.Lerp(GuardL, BlockL, block) + new Vector3(0, -bounce, 0);
            var rotR = Quaternion.Euler(Vector3.Lerp(GuardRotR, BlockRotR, block));
            var rotL = Mirror(Vector3.Lerp(GuardRotR, BlockRotR, block));

            // Charge wind-up pulls the punching hand back.
            float c = Mathf.Clamp01(charge / 0.6f);
            if (c > 0f)
            {
                if (punchRight) { r += new Vector3(0.04f, -0.01f, -0.1f) * c; rotR *= Quaternion.Euler(0, 0, -15f * c); }
                else { l += new Vector3(-0.04f, -0.01f, -0.1f) * c; rotL *= Quaternion.Euler(0, 0, 15f * c); }
            }

            if (punchT < 1f)
            {
                // Out fast, back slower; fist rotates palm-down (corkscrew) at full extension.
                float k = punchT < 0.38f ? Ease(punchT / 0.38f) : 1f - Ease((punchT - 0.38f) / 0.62f);
                if (punchRight)
                {
                    var target = heavy ? new Vector3(0.0f, -0.05f, 0.62f) : new Vector3(0.03f, -0.07f, 0.6f);
                    r = Vector3.Lerp(r, target, k);
                    rotR = Quaternion.Slerp(rotR, Quaternion.Euler(heavy ? -5f : -2f, heavy ? -25f : -8f, -10f), k);
                    l += new Vector3(0.01f, 0.02f, -0.05f) * k;
                }
                else
                {
                    var target = new Vector3(-0.02f, -0.065f, 0.62f);
                    l = Vector3.Lerp(l, target, k);
                    rotL = Quaternion.Slerp(rotL, Quaternion.Euler(-2f, 8f, 10f), k);
                    r += new Vector3(-0.01f, 0.02f, -0.05f) * k;
                }
            }

            Vm.RightPos = r; Vm.RightRot = rotR;
            Vm.LeftPos = l; Vm.LeftRot = rotL;
            Vm.AimWeight = 0f;
        }

        void StartPunch()
        {
            punchT = 0f;
            hitDone = false;
            punchRight = heavy || !punchRight;
            Swing(heavy ? "whoosh_big" : "whoosh_small", heavy ? 0.8f : 0.55f);
            CameraFX.Punch(-0.6f, punchRight ? 0.8f : -0.8f);
        }

        void DoHit()
        {
            float reach = heavy ? 1.6f : 1.45f;
            if (!MeleeTrace(Aim, reach, 0.18f, out var hit)) return;
            float dmg = Def.Damage * (heavy ? 2.6f : 1f);
            float force = Def.Force * (heavy ? 3.2f : 1f) * (Rules.SuperKick ? 6f : 1f);
            var info = new DamageInfo
            {
                Amount = dmg, Force = Aim * force, Point = hit.point, Normal = hit.normal, Direction = Aim,
                Type = DamageType.Blunt, Source = Player, Weapon = heavy ? "Haymaker" : "Fists",
            };
            var part = hit.collider.GetComponent<BodyPart>();
            if (part != null)
            {
                part.TakeDamage(info);
                AudioManager.Play("punch_hit", hit.point, 0.9f, Random.Range(0.9f, 1.1f));
                if (heavy && part.IsHead && part.Doll != null) part.Doll.Ragdoll.Fall();
                Stats.Add("punches");
            }
            else
            {
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(info);
                if (hit.rigidbody != null) NetPhysics.Impulse(hit.rigidbody, Aim * force, hit.point);
                AudioManager.Play(Surface.Of(hit.collider) == SurfaceType.Metal ? "metal_impact" : "body_thud_soft", hit.point, 0.6f, Random.Range(0.9f, 1.1f));
                FX.Dust(hit.point, 0.05f, new Color(0.6f, 0.6f, 0.6f, 0.6f), 4);
            }
            CameraFX.Shake(heavy ? 0.25f : 0.1f);
            GameEvents.EmitNoise(hit.point, 6f, 0.4f, Player);
        }
    }
}
