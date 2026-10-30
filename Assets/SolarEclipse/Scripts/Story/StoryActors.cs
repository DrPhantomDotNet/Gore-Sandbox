using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// A story character that can't be hurt (like Big Marcus): a static Goredoll model that breathes, turns to look at
    /// you and can be talked to. Bullets pass through; the player bumps into it.
    /// </summary>
    public sealed class StoryStatic : MonoBehaviour
    {
        Transform model, head, chest;
        Quaternion headRest;
        FaceRig face;
        float yaw, baseYaw, t0;
        public StoryTalk Talk { get; private set; }
        public Vector3 HeadPosition => head != null ? head.position + Vector3.up * 0.15f : transform.position + Vector3.up * 2f;

        public static StoryStatic Spawn(Transform parent, string name, CustomLook look, Vector3 feet, float yawDeg)
        {
            var app = look.ToAppearance();
            app.Name = name;
            var root = new GameObject("Story " + name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(feet, Quaternion.Euler(0, yawDeg, 0));
            var s = root.AddComponent<StoryStatic>();
            s.yaw = s.baseYaw = yawDeg;
            s.t0 = UnityEngine.Random.value * 10f;
            GameObject m;
            try { m = CustomDolls.BuildMannequin(app, look.Armor, look.Glasses, null, 0); }
            catch (Exception e) { Debug.LogWarning("Story character model failed: " + e.Message); m = new GameObject("Mannequin"); }
            m.transform.SetParent(root.transform, false);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            m.transform.localScale = Vector3.one * app.Height;
            s.model = m.transform;
            s.head = m.transform.Find("Head");
            s.chest = m.transform.Find("Chest");
            if (s.head != null) { s.headRest = s.head.localRotation; s.face = s.head.GetComponentInChildren<FaceRig>(); }

            var col = new GameObject("Story Blocker");
            col.layer = Layers.Trigger;
            col.transform.SetParent(root.transform, false);
            var cap = col.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 1.0f * app.Height, 0);
            cap.height = 2f * app.Height;
            cap.radius = 0.38f;
            s.Talk = root.AddComponent<StoryTalk>();
            s.Talk.Label = "Talk to " + name;
            return s;
        }

        void Update()
        {
            float t = Time.time + t0;
            var p = PlayerController.Instance;
            bool near = false;
            if (p != null)
            {
                var to = MathX.Flat(p.Position - transform.position);
                float d = to.magnitude;
                near = d < 10f;
                float targetYaw = near && d > 0.2f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : baseYaw;
                yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, 70f * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
                if (face != null) { if (near) face.LookAt(p.EyePosition); else face.ClearLook(); }
            }
            if (chest != null) chest.localScale = new Vector3(1f + Mathf.Sin(t * 1.6f) * 0.012f, 1f + Mathf.Sin(t * 1.6f) * 0.01f, 1f + Mathf.Sin(t * 1.6f) * 0.02f);
            if (model != null) model.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.35f) * 1.4f);
            if (head != null)
            {
                float pitch = 0f;
                if (near && p != null)
                {
                    var d = p.EyePosition - head.position;
                    pitch = Mathf.Clamp(-Mathf.Atan2(d.y, MathX.Flat(d).magnitude) * Mathf.Rad2Deg, -20f, 20f);
                }
                head.localRotation = headRest * Quaternion.Euler(pitch + Mathf.Sin(t * 0.8f) * 1.5f, Mathf.Sin(t * 0.27f) * (near ? 3f : 12f), 0);
            }
        }
    }

    /// <summary>
    /// A story boss: a Goredoll with a big health pool (headshots count double). It waits (untouchable) until the story
    /// starts the fight, then fights with a rifle; below half health it gets enraged (faster). Limbs stay on until the
    /// pool is empty; then it dies for real.
    /// </summary>
    public sealed class StoryBoss : MonoBehaviour
    {
        public float MaxPool = 1200f, Pool = 1200f;
        public bool IsAwake { get; private set; }
        public bool Enraged { get; private set; }
        public bool Defeated { get; private set; }
        public float LastHitTime = -99f;
        public Action OnDefeated, OnEnraged;
        Goredoll doll;
        ActiveRagdoll body;
        ArmedBrain gun;
        float downSince = -1f;

        public Goredoll Doll => doll;

        public static StoryBoss Spawn(string name, CustomLook look, Vector3 feet, float yaw, float health)
        {
            look.Name = name;
            if (look.Armor < 2) look.Armor = 2;
            var doll = GoredollFactory.Spawn(feet, yaw, "Passive~c" + look.Encode(), -1, null, 0);
            doll.DisplayName = name;
            var sp = doll.GetComponent<Spawned>();
            if (sp != null) sp.DisplayName = name;
            if (doll.Brain != null) doll.Brain.enabled = false;
            doll.Vitals.Immortal = true;
            var b = doll.gameObject.AddComponent<StoryBoss>();
            b.doll = doll;
            b.body = doll.Ragdoll;
            b.MaxPool = b.Pool = health;
            return b;
        }

        /// <summary>Starts the fight (the story's Kill step on this character).</summary>
        public void Wake()
        {
            if (IsAwake || Defeated || doll == null) return;
            IsAwake = true;
            try
            {
                gun = doll.gameObject.AddComponent<ArmedBrain>();
                gun.Init(doll, true);
                doll.DisplayName = doll.Look != null ? doll.Look.Name : doll.DisplayName;
            }
            catch (Exception e) { Debug.LogWarning("Boss gun setup failed: " + e.Message); gun = null; }
            if (doll.Brain != null) { doll.Brain.enabled = false; doll.Brain.Bravery = 1f; doll.Brain.Fear = 0f; }
            doll.Voice.Say("v_argh", 1f, false);
        }

        Vector3 Pelvis => doll.Parts[PartType.Pelvis].Body.position;

        public bool OnHit(BodyPart part, ref DamageInfo info)
        {
            if (Defeated) return true;
            if (!IsAwake) return false;
            if (info.Source is Goredoll g && g == doll) return false;
            if (info.Source is BodyPart bp && bp.Doll == doll) return false;
            if (info.Type == DamageType.Fall) return false;
            if (info.Amount <= 0f) return true;
            float dmg = info.Amount * Mathf.Max(0.1f, Rules.DamageMultiplier) * (part.IsHead ? 2f : 1f);
            if (info.Type == DamageType.Explosion) dmg = Mathf.Min(dmg, 30f);
            Pool -= dmg;
            LastHitTime = Time.time;
            if (!Enraged && Pool <= MaxPool * 0.5f)
            {
                Enraged = true;
                doll.Voice.Say("v_argh", 1f, false);
                OnEnraged?.Invoke();
            }
            if (Pool <= 0f)
            {
                Pool = 0f;
                Defeated = true;
                doll.Vitals.Immortal = false;
                doll.KilledByPlayer = true;
                doll.Kill("Beaten");
                if (body != null) { body.DesiredVelocity = Vector3.zero; body.AimWeight = 0f; }
                OnDefeated?.Invoke();
                return true;
            }
            if (info.Type == DamageType.Crush || info.Type == DamageType.Admin) info.Type = DamageType.Blunt;
            float cap = part.MaxHealth * 0.3f / Mathf.Max(0.2f, part.Spec.DamageMultiplier);
            if (info.Amount > cap) info.Amount = cap;
            part.Health = Mathf.Max(part.Health, part.MaxHealth * 0.6f);
            return true;
        }

        void Update()
        {
            if (doll == null || body == null || Defeated) return;
            if (!doll.Parts.ContainsKey(PartType.Pelvis)) return;
            KeepAlive();
            if (body.IsDown && body.State != RagdollState.Limp)
            {
                if (downSince < 0f) downSince = Time.time;
                if (Time.time - downSince > 1.3f) { body.ForceStandNow(); downSince = -1f; }
            }
            else if (body.State == RagdollState.Limp && doll.Vitals.Alive) { body.ForceStandNow(); downSince = -1f; }
            else downSince = -1f;
            if (!IsAwake)
            {
                // Waiting: stand, face the player.
                body.DesiredVelocity = Vector3.zero;
                body.Pose = PoseMode.Idle;
                var p = PlayerController.Instance;
                body.LookTarget = null;
                if (p != null)
                {
                    var to = MathX.Flat(p.Position - Pelvis);
                    if (to.magnitude < 18f) { body.DesiredYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg; body.LookTarget = p.EyePosition; }
                }
            }
        }

        void LateUpdate()
        {
            if (Enraged && !Defeated && body != null && gun != null && gun.enabled) body.DesiredVelocity *= 1.4f;
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
    }
}
