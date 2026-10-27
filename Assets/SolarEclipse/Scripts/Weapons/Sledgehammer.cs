using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Two-handed sledgehammer. Hold LMB to wind up (up to ~1 s), release to swing: damage, knockback and screen shake
    /// scale with the wind-up. Bodies get launched, bones break, a full-power hit to the head crushes the skull; props
    /// take damage and get knocked flying.
    /// </summary>
    public sealed class Sledgehammer : Weapon
    {
        float charge, swingT = -1f, power, idle;
        bool hitDone, winding;

        static readonly Vector3 Idle = new Vector3(0.16f, -0.26f, 0.22f);
        static readonly Quaternion IdleRot = Quaternion.Euler(-55f, -10f, -15f);
        static readonly Vector3 WindPos = new Vector3(0.2f, 0.02f, 0.02f);
        static readonly Quaternion WindRot = Quaternion.Euler(-150f, -15f, -20f);
        static readonly Vector3 StrikePos = new Vector3(0.02f, -0.3f, 0.42f);
        static readonly Quaternion StrikeRot = Quaternion.Euler(35f, -2f, -5f);

        public override bool Busy => swingT >= 0f || winding;
        public override string ModeText => winding ? "POWER " + Mathf.RoundToInt(Power01 * 100f) + "%" : "";
        float Power01 => Mathf.Clamp01(charge / 1f);

        protected override void OnEquip()
        {
            Vm.Right.SetPose(HandPose.Fist);
            Vm.Left.SetPose(HandPose.Fist);
            charge = 0f; swingT = -1f; winding = false;
            AudioManager.Play("cloth", Eye, 0.6f, 0.8f, 0f);
        }

        protected override void OnHolster()
        {
            winding = false;
            swingT = -1f;
            Vm.Right.SetPose(HandPose.Relaxed);
            Vm.Left.SetPose(HandPose.Relaxed);
        }

        public override void Tick(InputFrame f, float dt)
        {
            idle += dt;
            var pos = Idle + new Vector3(0, Mathf.Sin(idle * 1.4f) * 0.004f, 0);
            var rot = IdleRot;

            if (swingT < 0f)
            {
                if (!winding && f.Fire) { winding = true; charge = 0f; inspectT = -1f; }
                if (winding)
                {
                    charge += dt;
                    float k = Ease(Mathf.Clamp01(charge / 0.6f));
                    pos = Vector3.Lerp(pos, WindPos, k) + Random.insideUnitSphere * 0.002f * Power01;
                    rot = Quaternion.Slerp(rot, WindRot, k);
                    if (!f.FireHeld || charge > 2.5f)
                    {
                        winding = false;
                        power = 0.3f + 0.7f * Power01;
                        swingT = 0f;
                        hitDone = false;
                        AudioManager.Play("sledge_swing", Eye + Aim * 0.5f, 0.6f + 0.4f * power, Random.Range(0.9f, 1.05f), 0f);
                        CameraFX.Punch(-1.2f * power, 0.4f);
                    }
                }
                else TickInspect(f, dt, ref pos, ref rot, 2.2f);
            }

            if (swingT >= 0f)
            {
                swingT += dt / Mathf.Lerp(0.55f, 0.42f, power);
                // Wind position → strike (fast, gravity-like ease-in) → heavy recovery.
                float startK = Ease(Mathf.Clamp01(charge / 0.6f));
                var from = Vector3.Lerp(Idle, WindPos, startK);
                var fromRot = Quaternion.Slerp(IdleRot, WindRot, startK);
                if (swingT < 0.4f)
                {
                    float k = swingT / 0.4f; k *= k;
                    pos = Vector3.Lerp(from, StrikePos, k);
                    rot = Quaternion.Slerp(fromRot, StrikeRot, k);
                }
                else
                {
                    float k = Ease(Ramp(swingT, 0.55f, 1f));
                    pos = Vector3.Lerp(StrikePos, pos, k);
                    rot = Quaternion.Slerp(StrikeRot, rot, k);
                }
                if (!hitDone && swingT >= 0.36f) { hitDone = true; DoHit(); }
                if (swingT >= 1f) swingT = -1f;
            }

            Vm.TargetPos = pos;
            Vm.TargetRot = rot;
        }

        void DoHit()
        {
            if (!MeleeTrace(Aim, 2.2f, 0.22f, out var hit)) return;
            var dir = Aim;
            float dmg = Def.Damage * (0.6f + 1.9f * power);   // ~70 .. ~225
            var part = hit.collider.GetComponent<BodyPart>();
            if (part != null)
            {
                part.TakeDamage(new DamageInfo
                {
                    Amount = dmg, Force = dir * Def.Force * power, Point = hit.point, Normal = hit.normal, Direction = dir,
                    Type = power > 0.75f ? DamageType.Crush : DamageType.Blunt, Source = Player, Weapon = Def.Name,
                });
                AudioManager.Play("sledge_flesh", hit.point, 1f, Random.Range(0.9f, 1.05f));
                AudioManager.Play("bone_crack", hit.point, 0.6f + 0.4f * power, Random.Range(0.85f, 1.05f));
                var doll = part.Doll;
                if (doll != null)
                {
                    // Crushed skull on a full-power head hit.
                    if (part.IsHead && power > 0.7f && Settings.Dismemberment && part.Gibbable)
                    {
                        doll.Dismember.Gib(part, dir);
                        AudioManager.Play("headshot", hit.point, 0.9f);
                    }
                    doll.Launch(dir * (5f + 13f * power) + Vector3.up * (2f + 4f * power));
                }
                CameraFX.Punch(1.2f * power, 0f);
            }
            else
            {
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = dmg * 1.5f, Force = dir * Def.Force * power, Point = hit.point, Normal = hit.normal, Direction = dir,
                    Type = DamageType.Blunt, Source = Player, Weapon = Def.Name,
                });
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    NetPhysics.Impulse(hit.rigidbody, (dir + Vector3.up * 0.25f).normalized * Mathf.Min(hit.rigidbody.mass * (4f + 10f * power), 600f), hit.point);
                var surf = Surface.Of(hit.collider);
                AudioManager.Play("sledge_world", hit.point, 0.7f + 0.3f * power, Random.Range(0.9f, 1.05f));
                if (surf == SurfaceType.Metal || surf == SurfaceType.Concrete) FX.Sparks(hit.point, hit.normal, 8 + (int)(12 * power), 5f);
                FX.Dust(hit.point, 0.15f, new Color(0.55f, 0.53f, 0.5f, 0.6f), 6 + (int)(10 * power));
                Decals.BulletHole(hit.point, hit.normal, surf, hit.collider, 0.12f + 0.1f * power);
                swingT = Mathf.Max(swingT, 0.55f); // the head rebounds
            }
            CameraFX.Shake(0.2f + 0.35f * power);
            Vm.AddRecoil(new Vector3(0f, 0.2f, -0.6f) * power, new Vector3(-12f * power, 0f, Random.Range(-4f, 4f)));
            GameEvents.EmitNoise(hit.point, 12f + 10f * power, 0.7f, Player);
        }
    }
}
