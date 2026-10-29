using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// One limb or body segment: rigidbody + collider + joint, its own health, wounds, fractures and organs.
    /// Turns raw damage into anatomically-placed wounds and hands consequences to the physiology.
    /// </summary>
    public sealed class BodyPart : MonoBehaviour, IDamageable
    {
        public PartType Type;
        public PartSpec Spec;
        public Goredoll Doll;
        public Rigidbody Body;
        public Collider Col;
        public ConfigurableJoint Joint;
        public BodyPart Parent;
        public readonly List<BodyPart> Children = new List<BodyPart>();
        public MeshFilter Filter;
        public MeshRenderer Renderer;
        public Quaternion StartLocalRotation;
        public float Health, MaxHealth;
        public bool Fractured, Destroyed, Severed, Gibbed, Sliced;
        public readonly List<Wound> Wounds = new List<Wound>();
        public GameObject OrgansVisual;
        float lastImpact;

        public bool IsAlive => !Gibbed && Doll != null;
        public bool CanSever => Spec.CanSever && !Severed && !Gibbed && Joint != null;
        public bool IsHead => Type == PartType.Head;
        public bool IsLimb => Spec.IsArm || Spec.IsLeg;

        /// <summary>0..1 how well this part's muscles still work.</summary>
        public float Function
        {
            get
            {
                if (Destroyed || Severed || Gibbed) return 0f;
                float f = Mathf.Clamp(Health / MaxHealth * 1.3f, 0.25f, 1f);
                if (Fractured) f *= 0.15f;
                return f;
            }
        }

        public float BleedRate
        {
            get { float b = 0; foreach (var w in Wounds) b += w.BleedRate; return b; }
        }

        // ------------------------------------------------------------------ damage

        public void TakeDamage(DamageInfo info)
        {
            if (Doll == null || Gibbed) return;
            if (Doll.NetProxy && NetState.ClientIntercepts)
            {
                // The host simulates this Goredoll: send our hit, ignore everything else (the host already did it).
                if (NetState.LocalAction && !Severed) NetManager.I.RequestDamagePart(this, info);
                return;
            }
            if (NetState.IsHost && !Severed) NetManager.I.BroadcastDollDamage(this, info);
            if (!StoryHooks.Process(this, ref info)) return; // story NPCs: Z's boss health pool (Goredoll/ZBoss.cs)
            if (Body != null && info.Force.sqrMagnitude > 0)
                Body.AddForceAtPosition(info.Force * Rules.BulletForceMultiplier, info.Point, ForceMode.Impulse);
            if (info.Amount <= 0f) return;
            if (!Armor.Process(this, ref info)) return; // helmets/vests stop or weaken hits (Goredoll/Armor.cs)

            float dmg = info.Amount * Rules.DamageMultiplier * Spec.DamageMultiplier / Mathf.Max(0.05f, Rules.NpcHealthMultiplier);
            var wound = CreateWound(info, dmg);
            Health -= dmg;
            if (Rules.OneHitKill && !Severed) Doll.Vitals.Die("One-hit kill");

            ApplyOrganEffects(wound, info);
            PlayHitEffects(info, wound, dmg);
            if (info.Source is PlayerController || info.Source is Weapon) { Stats.Add(Stats.Hits); if (IsHead && info.IsBallistic) Stats.Add(Stats.Headshots); }

            if (Doll.Zombie != null) Doll.Zombie.OnPartHit(this, info, dmg);
            Doll.OnPartDamaged(this, info, wound, dmg);
            CheckDismemberment(info, dmg);

            if (!Destroyed && Health <= 0f)
            {
                Destroyed = true;
                if (IsHead && Doll.Vitals.Alive) Doll.Vitals.Die("Massive head trauma");
            }
        }

        Wound CreateWound(DamageInfo info, float dmg)
        {
            var local = transform.InverseTransformPoint(info.Point);
            var localDir = transform.InverseTransformDirection(info.Direction.sqrMagnitude > 0 ? info.Direction : -info.Normal).normalized;
            var w = new Wound
            {
                Part = Type, Time = Time.time, LocalPoint = local, LocalNormal = transform.InverseTransformDirection(info.Normal),
                Weapon = info.Weapon ?? info.Type.ToString(), Caliber = info.Caliber, Damage = dmg, Location = Anatomy.Describe(Type, local),
            };
            float depth = 0f, widen = 0f, bleed, pain;
            switch (info.Type)
            {
                case DamageType.Bullet:
                case DamageType.Buckshot:
                case DamageType.Admin:
                {
                    float grazeAngle = Vector3.Angle(info.Direction, -info.Normal);
                    if (info.IsExit) { w.Kind = WoundKind.GunshotExit; bleed = 3.5f + info.Caliber * 0.2f; pain = 10f; }
                    else if (grazeAngle > 78f && !IsHead) { w.Kind = WoundKind.Graze; bleed = 0.8f; pain = 12f; }
                    else { w.Kind = WoundKind.GunshotEntry; bleed = 1.2f + info.Caliber * 0.12f; pain = 22f; depth = 0.5f; w.Penetrating = true; }
                    break;
                }
                case DamageType.Slash: w.Kind = WoundKind.Laceration; bleed = 2.5f + dmg * 0.05f; pain = 25f; depth = 0.035f; widen = 0.01f; break;
                case DamageType.Stab: w.Kind = WoundKind.Stab; bleed = 3f + dmg * 0.04f; pain = 30f; depth = 0.13f; w.Penetrating = true; break;
                case DamageType.Explosion: w.Kind = WoundKind.Burn; bleed = 1f + dmg * 0.03f; pain = 45f; depth = 0.04f; widen = 0.02f; break;
                case DamageType.Crush: w.Kind = WoundKind.Crush; bleed = 4f; pain = 60f; depth = 0.2f; widen = 0.06f; break;
                default: w.Kind = WoundKind.Blunt; bleed = dmg > 30f ? 0.5f : 0f; pain = Mathf.Min(40f, dmg * 0.7f); depth = 0f; widen = 0.035f; break;
            }
            if (w.Kind != WoundKind.GunshotExit && (depth > 0f || widen > 0f))
            {
                foreach (var o in Anatomy.Track(Type, local, localDir, depth, widen))
                {
                    // Blunt force bruises organs but only breaks bones / vessels when it is heavy.
                    if (info.Type == DamageType.Blunt && o.Kind != OrganKind.Bone && dmg < 40f) continue;
                    w.Organs.Add(o);
                    bleed += o.BleedMl * (o.Arterial ? 1f : 0.6f);
                    pain += o.Pain;
                    if (o.Arterial) w.Arterial = true;
                    if (o.Kind == OrganKind.Bone) w.Fracture = true;
                }
            }
            if (!w.Fracture && (info.Type == DamageType.Blunt || info.Type == DamageType.Fall || info.Type == DamageType.Crush) && dmg > MaxHealth * 0.5f && IsLimb && Random.value < 0.6f)
                w.Fracture = true;
            if (w.Fracture) Fractured = true;
            w.BleedRate = Settings.GoreEnabled && !Rules.NoBleeding ? bleed : 0f;
            w.Pain = pain;
            Wounds.Add(w);
            Doll.Wounds.Add(w);
            return w;
        }

        void ApplyOrganEffects(Wound w, DamageInfo info)
        {
            var v = Doll.Vitals;
            v.AddPain(w.Pain);
            bool penetrating = info.IsBallistic || info.Type == DamageType.Stab || info.Type == DamageType.Crush;
            foreach (var o in w.Organs)
            {
                switch (o.Kind)
                {
                    case OrganKind.Brain:
                        if (penetrating && (Rules.InstantDeathOnHeadshot || info.Amount > 40f)) { v.BrainDestroyed = true; v.Die("Penetrating brain injury"); }
                        else v.HeadTrauma = Mathf.Clamp01(v.HeadTrauma + info.Amount / 70f);
                        break;
                    case OrganKind.Heart: if (penetrating) v.HeartDestroyed = true; break;
                    case OrganKind.Lung: v.LungDamage = Mathf.Min(2f, v.LungDamage + 0.8f); break;
                    case OrganKind.Trachea: case OrganKind.Carotid:
                        v.AirwayBlood = Mathf.Clamp01(v.AirwayBlood + 0.55f);
                        Doll.Voice.StartChoking();
                        break;
                    case OrganKind.ThoracicSpine: case OrganKind.LumbarSpine: case OrganKind.CervicalSpine:
                        if (penetrating && v.Paralysis != Paralysis.Total)
                        {
                            v.Paralysis = Paralysis.Total;
                            Stats.Add(Stats.Paralyzed);
                            GameEvents.ShowToast(Doll.DisplayName + "'s spinal cord was severed — paralysed.");
                        }
                        break;
                    case OrganKind.Eye: if (Doll.Face != null) Doll.Face.DestroyEye(o.Center.x < 0); break;
                }
            }
            if (Type == PartType.Neck && info.IsBallistic) Stats.Add(Stats.NeckShots);
            if (v.Paralysis == Paralysis.Total && info.Amount > 0) v.HitsWhileParalyzed++;
            if (IsHead && info.Type == DamageType.Blunt) v.HeadTrauma = Mathf.Clamp01(v.HeadTrauma + info.Amount / 90f);
        }

        void PlayHitEffects(DamageInfo info, Wound w, float dmg)
        {
            var pos = info.Point;
            if (Settings.GoreEnabled)
            {
                float amount = Mathf.Clamp(dmg / 35f, 0.3f, 2.5f) * Settings.BloodScale;
                switch (w.Kind)
                {
                    case WoundKind.GunshotEntry: BloodFX.Entry(pos, info.Normal, info.Direction, amount); break;
                    case WoundKind.GunshotExit: BloodFX.Exit(pos, info.Direction, amount * 1.6f); break;
                    case WoundKind.Laceration: case WoundKind.Stab: BloodFX.Entry(pos, info.Normal, info.Direction, amount * 0.8f); break;
                    case WoundKind.Blunt: if (dmg > 20f) BloodFX.Mist(pos, info.Normal, amount * 0.4f); break;
                    default: BloodFX.Burst(pos, info.Normal, amount * 0.6f, Settings.BloodColor); break;
                }
                int cell;
                float size;
                switch (w.Kind)
                {
                    case WoundKind.GunshotEntry: cell = info.Type == DamageType.Buckshot ? 15 : (info.Caliber > 8.5f ? 1 : 0); size = 0.045f + info.Caliber * 0.002f; break;
                    case WoundKind.GunshotExit: cell = info.Caliber > 8f ? 3 : 2; size = 0.07f + info.Caliber * 0.004f; break;
                    case WoundKind.Graze: cell = 12; size = 0.08f; break;
                    case WoundKind.Laceration: cell = Random.value < 0.5f ? 4 : 5; size = 0.12f; break;
                    case WoundKind.Stab: cell = 6; size = 0.07f; break;
                    case WoundKind.Burn: cell = 8; size = 0.14f; break;
                    case WoundKind.Crush: cell = 14; size = 0.14f; break;
                    default: cell = dmg > 25f ? 14 : 7; size = 0.1f; break;
                }
                if (Type == PartType.Neck && w.Arterial) cell = 13;
                float rot = info.Type == DamageType.Slash ? Vector3.SignedAngle(transform.right, info.Direction, info.Normal) : -1f;
                if (rot < 0 && rot != -1f) rot += 360f;
                if (w.Kind != WoundKind.Blunt || dmg > 12f) Decals.Wound(pos, info.Normal, cell, size, Col, rot);
                if (w.BleedRate > 0.5f) Doll.Bleeding.AddEmitter(this, w);
            }
            string clip = IsHead && info.IsBallistic ? "headshot" : info.Type == DamageType.Stab ? "stab" : info.Type == DamageType.Slash ? "slice" : "flesh_hit";
            if (info.Type == DamageType.Blunt || info.Type == DamageType.Fall) clip = dmg > 25f ? "punch_hit" : "body_thud_soft";
            AudioManager.Play(clip, pos, Mathf.Clamp(0.5f + dmg / 60f, 0.5f, 1f), Random.Range(0.92f, 1.08f), 1f, 1.5f, 50f);
            if (w.Fracture) AudioManager.Play("bone_crack", pos, 0.9f);
        }

        void CheckDismemberment(DamageInfo info, float dmg)
        {
            if (!Settings.Dismemberment || Doll.Dismember == null) return;
            bool extreme = Settings.Unreal;
            if (info.Type == DamageType.Crush && Gibbable) { Doll.Dismember.Gib(this, info.Direction); return; }
            if (info.Type == DamageType.Explosion)
            {
                if (dmg > MaxHealth * (extreme ? 0.9f : 1.3f) && Gibbable && Random.value < 0.7f) { Doll.Dismember.Gib(this, info.Direction); return; }
                if (dmg > MaxHealth * 0.6f && CanSever && Random.value < 0.7f) { Doll.Dismember.Sever(this, transform.position, -info.Direction, false); return; }
            }
            if (info.IsBallistic)
            {
                // Big rounds at close range pop heads.
                if (IsHead && (dmg > 140f || info.Type == DamageType.Admin) && Random.value < (extreme ? 0.9f : 0.6f)) { Doll.Dismember.Gib(this, info.Direction); return; }
                if (CanSever && (Health < -MaxHealth * (extreme ? 0.2f : 0.55f) || Rules.AlwaysDismember)) { Doll.Dismember.Sever(this, info.Point, info.Normal, false); return; }
            }
            if (Rules.AlwaysDismember && CanSever) Doll.Dismember.Sever(this, info.Point, info.Normal, info.IsSharp);
        }

        public bool Gibbable => !Gibbed && Type != PartType.Pelvis;

        // ------------------------------------------------------------------ physics impacts

        void OnCollisionEnter(Collision c)
        {
            if (Doll == null || Body == null || Time.time - lastImpact < 0.15f) return;
            if (c.collider.gameObject.layer == Layers.Ragdoll && c.collider.GetComponentInParent<Goredoll>() == Doll) return;
            if (c.contactCount == 0) return;
            // Impact speed along the contact normal (m/s): independent of how light this limb is.
            float dv = Mathf.Abs(Vector3.Dot(c.relativeVelocity, c.GetContact(0).normal));
            bool upright = Doll.Ragdoll != null && (Doll.Ragdoll.IsStanding || Doll.Ragdoll.State == RagdollState.GettingUp);
            // Feet/legs touching static ground while standing or walking are not impacts.
            if (upright && (Spec.IsLeg || Type == PartType.Pelvis) && c.rigidbody == null && dv < 7f) return;
            if (dv < 2.5f) return;
            lastImpact = Time.time;
            var contact = c.GetContact(0);
            AudioManager.Play(dv > 8f ? "body_thud" : "body_thud_soft", contact.point, Mathf.Clamp01(dv / 12f), Random.Range(0.9f, 1.1f), 1f, 1.5f, 40f);
            Doll.Ragdoll.OnImpact(dv);
            if (dv > 9f)
            {
                var src = c.rigidbody != null ? (object)c.rigidbody : this;
                TakeDamage(new DamageInfo
                {
                    Amount = (dv - 9f) * 6f, Point = contact.point, Normal = contact.normal, Direction = -contact.normal,
                    Type = c.rigidbody != null ? DamageType.Blunt : DamageType.Fall, Source = src, Weapon = c.rigidbody != null ? "Impact" : "Fall",
                });
                if (dv > 12f && Settings.GoreEnabled) Decals.Blood(contact.point, contact.normal, 0.5f, 0, c.collider);
            }
        }
    }
}
