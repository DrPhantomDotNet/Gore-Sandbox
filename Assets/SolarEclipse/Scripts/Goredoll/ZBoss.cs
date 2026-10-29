using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Z, the story chapter 1 boss. A normal Goredoll (so hits, wounds and blood all look real) with a big health
    /// pool on top: every hit (from anything but himself) drains the pool (headshots x2) through
    /// <see cref="StoryHooks.Process"/>, while the body itself is kept alive and in one piece (no organ deaths, no
    /// dismemberment, part health topped up) until the pool is empty.
    /// Stage 0: waits, untouchable. Stage 1 (100-50%): melee brawler (punches, kicks, charges).
    /// Stage 2 (below 50%): enraged, pulls a Glock and fights with the armed AI (<see cref="ArmedBrain"/>), faster.
    /// Pool at 0 in stage 2: he is killed for real (ragdoll, gore) and <see cref="OnDefeated"/> fires.
    /// Singleplayer only (story mode), so the brain runs locally.
    /// </summary>
    public sealed class ZBoss : MonoBehaviour
    {
        public const float MaxPool = 1500f;
        public const string Name = "Z";

        public float Pool = MaxPool;
        /// <summary>0 = waiting (untouchable), 1 = brawler, 2 = enraged gunman.</summary>
        public int Stage { get; private set; }
        public bool Defeated { get; private set; }
        public float LastHitTime { get; private set; } = -99f;
        public Goredoll Doll => doll;
        public Action OnEnraged, OnDefeated;

        Goredoll doll;
        ActiveRagdoll body;
        ArmedBrain gun;
        float downSince = -1f, attackTimer, nextAttack, chargeUntil, nextCharge, stuckT;
        int combo;
        bool charging, struck;

        public static ZBoss Spawn(Vector3 feet, float yaw)
        {
            var look = new CustomLook
            {
                Name = Name, Skin = new Color(0.86f, 0.68f, 0.55f), Shirt = new Color(0.1f, 0.1f, 0.11f), Pants = new Color(0.22f, 0.2f, 0.18f),
                Shoes = new Color(0.08f, 0.07f, 0.07f), Hair = new Color(0.07f, 0.05f, 0.04f), Eyes = new Color(0.25f, 0.15f, 0.08f),
                HairStyle = 1, LongSleeves = true, Beard = true, Height = 1.08f, Build = 1.25f,
            };
            var doll = GoredollFactory.Spawn(feet, yaw, "Passive~c" + look.Encode(), -1, null, 0);
            doll.DisplayName = Name;
            var sp = doll.GetComponent<Spawned>();
            if (sp != null) sp.DisplayName = Name;
            if (doll.Brain != null) doll.Brain.enabled = false;
            doll.Vitals.Immortal = true;
            var z = doll.gameObject.AddComponent<ZBoss>();
            z.doll = doll;
            z.body = doll.Ragdoll;
            return z;
        }

        Vector3 Pelvis => doll.Parts[PartType.Pelvis].Body.position;

        public void BeginFight()
        {
            if (Stage != 0 || Defeated) return;
            Stage = 1;
            nextCharge = Time.time + UnityEngine.Random.Range(3f, 5f);
            nextAttack = Time.time + 0.6f;
            doll.Voice.Say("v_argh", 1f, false);
        }

        // ------------------------------------------------------------------ damage (from StoryHooks / BodyPart.TakeDamage)

        public bool OnHit(BodyPart part, ref DamageInfo info)
        {
            if (Defeated) return true; // gore allowed once he's beaten
            if (Stage == 0) return false; // waiting for you: untouchable
            if (info.Source is Goredoll g && g == doll) return false;
            if (info.Source is BodyPart bp && bp.Doll == doll) return false;
            if (info.Type == DamageType.Fall) return false; // his own falls don't count (or hurt)
            if (info.Amount <= 0f) return true;

            float dmg = info.Amount * Mathf.Max(0.1f, Rules.DamageMultiplier) * (part.IsHead ? 2f : 1f);
            Pool -= dmg;
            LastHitTime = Time.time;
            if (Stage == 1 && Pool <= MaxPool * 0.5f) EnterStage2();
            if (Pool <= 0f)
            {
                if (Stage < 2) Pool = 1f; // the fight always goes through stage 2
                else
                {
                    Pool = 0f;
                    Defeat();
                    return true;
                }
            }
            // Keep the body fighting: cap what this single hit can do to the limb and keep it out of "destroyed".
            if (info.Type == DamageType.Crush || info.Type == DamageType.Admin) info.Type = DamageType.Blunt;
            float cap = part.MaxHealth * 0.3f / Mathf.Max(0.2f, part.Spec.DamageMultiplier);
            if (info.Amount > cap) info.Amount = cap;
            part.Health = Mathf.Max(part.Health, part.MaxHealth * 0.6f);
            return true;
        }

        void EnterStage2()
        {
            if (Stage != 1) return;
            Stage = 2;
            charging = false;
            body.Pose = PoseMode.Idle;
            try
            {
                gun = doll.gameObject.AddComponent<ArmedBrain>();
                gun.Init(doll, false);
                doll.DisplayName = Name; // ArmedBrain doesn't rename (only ArmedDolls.Attach does), but be safe
            }
            catch (Exception e) { Debug.LogWarning("Z gun setup failed: " + e.Message); gun = null; }
            if (doll.Brain != null) { doll.Brain.enabled = false; doll.Brain.Bravery = 1f; doll.Brain.Fear = 0f; }
            if (body.IsDown) body.ForceStandNow();
            AudioManager.Play("slide_back", Pelvis + Vector3.up * 0.5f, 1f, 1f, 1f, 2f, 30f);
            doll.Voice.Say("v_argh", 1f, false);
            OnEnraged?.Invoke();
        }

        void Defeat()
        {
            if (Defeated) return;
            Defeated = true;
            doll.Vitals.Immortal = false;
            doll.KilledByPlayer = true;
            doll.Kill("Beaten in a fight");
            if (body != null) { body.DesiredVelocity = Vector3.zero; body.AimWeight = 0f; }
            OnDefeated?.Invoke();
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (doll == null || body == null || Defeated) return;
            if (!doll.Parts.ContainsKey(PartType.Pelvis)) return;
            KeepAlive();
            var p = PlayerController.Instance;

            // Don't stay down for long: he's a boss.
            if (body.IsDown && body.State != RagdollState.Limp)
            {
                if (downSince < 0f) downSince = Time.time;
                if (Time.time - downSince > (Stage == 2 ? 1.2f : 1.7f)) { body.ForceStandNow(); downSince = -1f; }
            }
            else if (body.State == RagdollState.Limp && doll.Vitals.Alive) { body.ForceStandNow(); downSince = -1f; }
            else downSince = -1f;

            if (Stage == 2 && gun != null) { UpdateFace(p, true); return; } // the armed AI is driving
            if (body.IsDown) { body.DesiredVelocity = Vector3.zero; return; }

            if (Stage == 0 || p == null || p.IsDead) { Idle(p); UpdateFace(p, Stage > 0); return; }
            Brawl(p, Time.deltaTime);
            UpdateFace(p, true);
        }

        void LateUpdate()
        {
            // Enraged: everything he does is faster (ArmedBrain set the velocity this frame).
            if (Stage == 2 && !Defeated && body != null && gun != null && gun.enabled) body.DesiredVelocity *= 1.45f;
        }

        void KeepAlive()
        {
            var v = doll.Vitals;
            v.Immortal = true;
            v.BloodLost = Mathf.Min(v.BloodLost, Anatomy.TotalBlood * 0.12f);
            v.Pain = Mathf.Min(v.Pain, 30f);
            v.Shock = 0f; v.HeadTrauma = 0f; v.LungDamage = 0f; v.AirwayBlood = 0f;
            v.HeartDestroyed = v.BrainDestroyed = false;
            v.Paralysis = Paralysis.None;
            v.Consciousness = 1f;
            if (v.State == LifeState.Unconscious) v.State = LifeState.Alive;
            foreach (var part in doll.Parts.Values)
            {
                if (part == null || part.Severed || part.Gibbed) continue;
                part.Fractured = false;
                part.Destroyed = false;
                if (part.Health < part.MaxHealth * 0.6f) part.Health = part.MaxHealth * 0.6f;
            }
        }

        void Idle(PlayerController p)
        {
            body.DesiredVelocity = Vector3.zero;
            body.Pose = PoseMode.Idle;
            body.LookTarget = null;
            if (p == null) return;
            var to = MathX.Flat(p.Position - Pelvis);
            if (to.magnitude < 16f)
            {
                body.DesiredYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                body.LookTarget = p.EyePosition;
            }
        }

        void Brawl(PlayerController p, float dt)
        {
            var target = p.Position;
            var to = MathX.Flat(target - Pelvis);
            float dist = to.magnitude;
            var dir = dist > 0.01f ? to / dist : transform.forward;
            body.DesiredYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            body.LookTarget = p.EyePosition;

            // Charge: a straight sprint at you that hits hard and knocks you back.
            if (!charging && Time.time > nextCharge && dist > 3.5f && dist < 14f && Clear(dir, dist))
            {
                charging = true;
                struck = false;
                chargeUntil = Time.time + 1.4f;
                AudioManager.Play("whoosh_big", Pelvis + Vector3.up, 0.8f, 0.7f);
                doll.Voice.Say("v_argh", 1f, false);
            }
            if (charging)
            {
                body.Pose = PoseMode.Run;
                body.DesiredVelocity = Avoid(dir) * 7.2f;
                if (!struck && dist < 1.3f)
                {
                    struck = true;
                    Strike(p, dir, UnityEngine.Random.Range(20f, 26f), "Z's charge", 9f, 3.5f);
                    chargeUntil = Mathf.Min(chargeUntil, Time.time + 0.25f);
                }
                if (Time.time > chargeUntil)
                {
                    charging = false;
                    nextCharge = Time.time + UnityEngine.Random.Range(5f, 8f);
                }
                Stuck(dt);
                return;
            }

            if (dist > 1.25f)
            {
                bool run = dist > 2.6f;
                body.Pose = run ? PoseMode.Run : PoseMode.Guard;
                body.DesiredVelocity = Avoid(dir) * (run ? 4.6f : 2.2f);
                attackTimer = 0f;
                Stuck(dt);
                return;
            }

            // In range: punch, punch, kick.
            body.DesiredVelocity = Vector3.zero;
            if (Time.time < nextAttack) { body.Pose = PoseMode.Guard; return; }
            bool kick = combo % 3 == 2;
            float windup = kick ? 0.45f : 0.3f;
            body.Pose = PoseMode.Punch;
            attackTimer += dt;
            body.PunchProgress = attackTimer / windup;
            if (attackTimer >= windup)
            {
                AudioManager.Play("whoosh_big", Pelvis + Vector3.up, 0.6f, kick ? 0.8f : 1f);
                if (dist < 1.6f)
                {
                    if (kick) Strike(p, dir, UnityEngine.Random.Range(16f, 22f), "Z's kick", 6f, 2.5f);
                    else Strike(p, dir, UnityEngine.Random.Range(9f, 14f), "Z's fist", 0f, 0f);
                }
                combo++;
                attackTimer = 0f;
                nextAttack = Time.time + (kick ? 1.1f : UnityEngine.Random.Range(0.45f, 0.75f));
                if (UnityEngine.Random.value < 0.25f) doll.Voice.Say("v_argh", 0.8f, false);
            }
        }

        void Strike(PlayerController p, Vector3 dir, float dmg, string weapon, float push, float up)
        {
            var hitPoint = p.EyePosition + Vector3.down * 0.4f;
            p.Damageable.TakeDamage(new DamageInfo
            {
                Amount = dmg, Force = dir * 40f, Point = hitPoint, Normal = -dir, Direction = dir,
                Type = DamageType.Blunt, Source = doll, Weapon = weapon,
            });
            AudioManager.Play("punch_hit", hitPoint, 1f);
            CameraFX.Shake(push > 0f ? 0.45f : 0.2f);
            if (push > 0f && !p.IsDead) p.Launch(dir * push + Vector3.up * up);
        }

        bool Clear(Vector3 dir, float dist)
            => !Physics.SphereCast(Pelvis, 0.3f, dir, out _, Mathf.Max(0.1f, dist - 1f), Layers.Mask(Layers.Default, Layers.Props), QueryTriggerInteraction.Ignore);

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
            return dir;
        }

        void Stuck(float dt)
        {
            var pelvis = doll.Parts[PartType.Pelvis].Body;
            if (MathX.Flat(RB.Vel(pelvis)).magnitude < 0.25f) stuckT += dt; else stuckT = 0f;
            if (stuckT > 1.5f) { stuckT = 0f; charging = false; nextCharge = Time.time + 3f; }
        }

        void UpdateFace(PlayerController p, bool angry)
        {
            var face = doll.Face;
            if (face == null) return;
            face.Expression = angry ? Expression.Angry : Expression.Neutral;
            if (p != null && Vector3.Distance(p.Position, Pelvis) < 20f) face.LookAt(p.EyePosition); else face.ClearLook();
        }

        /// <summary>Spawns a pistol thug (an ordinary Gunman Goredoll) to help Z in stage 2.</summary>
        public static Goredoll SpawnThug(Vector3 feet, float yaw)
        {
            var d = GoredollFactory.Spawn(feet, yaw, ArmedDolls.Gunman + "~a0", -1, null, 0);
            return d;
        }
    }
}
