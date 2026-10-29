using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum LifeState { Alive, Unconscious, Dead }
    public enum Paralysis { None, LowerBody, Total }

    /// <summary>
    /// Blood volume, haemodynamics, breathing, oxygenation, pain, shock and consciousness.
    /// Bleeding drains blood; the body compensates (faster heart and breathing) until it can't.
    /// </summary>
    public sealed class Physiology
    {
        public float BloodLost;        // ml
        public float HeartRate = 72f;  // bpm
        public float Systolic = 120f, Diastolic = 80f;
        public float RespRate = 14f;   // breaths/min
        public float SpO2 = 98f;
        public float Pain;             // 0..100
        public float Adrenaline;       // 0..1 (fear)
        public float Consciousness = 1f; // 0..1
        public float Shock;            // 0..1
        public float LungDamage;       // 0..2
        public float AirwayBlood;      // 0..1 (neck/trachea injury → choking)
        public float HeadTrauma;       // 0..1
        public bool HeartDestroyed, BrainDestroyed, Decapitated;
        public Paralysis Paralysis;
        public int HitsWhileParalyzed;
        public LifeState State = LifeState.Alive;
        /// <summary>Zombie: no blood loss, pain, shock or organ deaths. Only dies when UndeadMayDie is set (3 headshots, head destroyed, Kill) or when decapitated.</summary>
        public bool Undead, UndeadMayDie;
        /// <summary>Story bosses (Goredoll/ZBoss.cs): nothing can kill them while set.</summary>
        public bool Immortal;
        public string CauseOfDeath = "";
        public float TimeOfDeath = -1f;
        float heartStopTimer = -1f, hypoxiaTimer, faintTimer;

        public float BloodLossFraction => BloodLost / Anatomy.TotalBlood;
        public float BloodVolume => Mathf.Max(0, Anatomy.TotalBlood - BloodLost);
        public bool Alive => State != LifeState.Dead;
        public bool Conscious => State == LifeState.Alive;

        /// <summary>Stage I-IV haemorrhagic shock classification.</summary>
        public string HemorrhageClass
        {
            get
            {
                float f = BloodLossFraction;
                if (f < 0.15f) return "Class I (<15%)";
                if (f < 0.30f) return "Class II (15-30%)";
                if (f < 0.40f) return "Class III (30-40%)";
                return "Class IV (>40%)";
            }
        }

        /// <summary>Glasgow Coma Scale estimate 3..15.</summary>
        public int GCS => State == LifeState.Dead ? 3 : Mathf.Clamp(Mathf.RoundToInt(3 + 12 * Consciousness), 3, 15);

        public string AVPU => State == LifeState.Dead ? "Unresponsive (deceased)" : Consciousness > 0.85f ? "Alert" : Consciousness > 0.55f ? "Responds to voice" : Consciousness > 0.3f ? "Responds to pain" : "Unresponsive";

        public void Tick(float dt, float totalBleed)
        {
            if (State == LifeState.Dead)
            {
                // Passive oozing after death.
                BloodLost = Mathf.Min(Anatomy.TotalBlood, BloodLost + totalBleed * 0.1f * dt);
                HeartRate = MathX.Damp(HeartRate, 0, 3f, dt);
                RespRate = 0; Systolic = MathX.Damp(Systolic, 0, 2f, dt); Diastolic = MathX.Damp(Diastolic, 0, 2f, dt);
                SpO2 = MathX.Damp(SpO2, 0, 0.5f, dt);
                return;
            }
            if (Undead)
            {
                if (Decapitated) { UndeadMayDie = true; Die("Decapitation"); return; }
                BloodLost = Mathf.Min(BloodLost, Anatomy.TotalBlood * 0.1f); Pain = 0; Adrenaline = 0; Shock = 0; Consciousness = 1f;
                HeartRate = 0; RespRate = 0; SpO2 = 98f; AirwayBlood = 0; HeadTrauma = 0; LungDamage = 0;
                HeartDestroyed = BrainDestroyed = false; Paralysis = Paralysis.None; faintTimer = 0; hypoxiaTimer = 0; heartStopTimer = -1f;
                State = LifeState.Alive;
                return;
            }
            float pumping = Mathf.Clamp(HeartRate / 80f, 0.2f, 2f) * Mathf.Clamp01(Systolic / 100f + 0.2f);
            BloodLost = Mathf.Min(Anatomy.TotalBlood, BloodLost + totalBleed * pumping * dt);
            float f = BloodLossFraction;

            Adrenaline = Mathf.MoveTowards(Adrenaline, 0f, dt * 0.03f);
            Pain = Mathf.MoveTowards(Pain, 0f, dt * (Rules.NoPain ? 50f : 0.8f));
            Shock = Mathf.Clamp01((f - 0.15f) / 0.25f * 0.7f + Pain / 100f * 0.3f);

            // Heart: compensatory tachycardia, then decompensation and arrest.
            float hrTarget = 72f + Adrenaline * 45f + Pain * 0.35f + Mathf.Clamp01(f / 0.35f) * 75f;
            if (f > 0.35f) hrTarget = Mathf.Lerp(hrTarget, 35f, (f - 0.35f) / 0.05f);
            if (HeartDestroyed) hrTarget = 0f;
            HeartRate = MathX.Damp(HeartRate, Mathf.Clamp(hrTarget, 0, 190), 0.8f, dt);
            float sysTarget = 122f - Mathf.Max(0, f - 0.15f) * 280f + Adrenaline * 12f;
            Systolic = MathX.Damp(Systolic, Mathf.Max(0, sysTarget), 0.6f, dt);
            Diastolic = Systolic * 0.66f;

            // Breathing gets faster with blood loss, pain, fear and lung damage.
            float rrTarget = 14f + Adrenaline * 9f + Mathf.Clamp01(f / 0.4f) * 22f + LungDamage * 8f + Pain * 0.06f + AirwayBlood * 10f;
            if (Paralysis == Paralysis.Total) rrTarget += 4f + HitsWhileParalyzed * 3f;
            RespRate = MathX.Damp(RespRate, Mathf.Clamp(rrTarget, 6f, 48f), 0.7f, dt);

            float spoTarget = 98f - LungDamage * 9f - AirwayBlood * 30f - Mathf.Max(0, f - 0.3f) * 80f;
            SpO2 = MathX.Damp(SpO2, Mathf.Clamp(spoTarget, 30f, 99f), 0.25f, dt);
            AirwayBlood = Mathf.MoveTowards(AirwayBlood, 0f, dt * 0.004f);

            // Consciousness
            float cTarget = 1f - Mathf.Clamp01((f - 0.27f) / 0.1f) - HeadTrauma - Mathf.Clamp01((80f - SpO2) / 25f);
            if (faintTimer > 0) { faintTimer -= dt; cTarget = Mathf.Min(cTarget, 0.1f); }
            Consciousness = MathX.Damp(Consciousness, Mathf.Clamp01(cTarget), 0.9f, dt);
            HeadTrauma = Mathf.MoveTowards(HeadTrauma, 0f, dt * 0.01f);
            State = Consciousness < 0.3f ? LifeState.Unconscious : LifeState.Alive;

            // Death checks
            if (BrainDestroyed) Die("Catastrophic brain injury");
            else if (Decapitated) Die("Decapitation");
            else if (f >= 0.42f) Die("Exsanguination (haemorrhagic shock)");
            else if (HeartDestroyed)
            {
                if (heartStopTimer < 0) heartStopTimer = Random.Range(6f, 14f);
                heartStopTimer -= dt;
                if (heartStopTimer <= 0) Die("Cardiac arrest (penetrating cardiac injury)");
            }
            if (SpO2 < 55f) { hypoxiaTimer += dt; if (hypoxiaTimer > 20f) Die(AirwayBlood > 0.4f ? "Asphyxia (aspiration of blood)" : "Hypoxia"); }
            else hypoxiaTimer = 0f;
        }

        public void AddPain(float amount)
        {
            if (Rules.NoPain) return;
            Pain = Mathf.Clamp(Pain + amount, 0, 100);
            Adrenaline = Mathf.Clamp01(Adrenaline + amount / 80f);
            // Very sudden extreme pain can make someone pass out briefly.
            if (amount > 55f && Random.value < 0.25f) faintTimer = Random.Range(3f, 8f);
        }

        public void Die(string cause)
        {
            if (State == LifeState.Dead || Immortal) return;
            if (Undead && !UndeadMayDie && !Decapitated) return;
            State = LifeState.Dead;
            CauseOfDeath = cause;
            TimeOfDeath = Time.time;
            Consciousness = 0;
        }

        public void ReviveFully()
        {
            BloodLost = 0; HeartRate = 80; Systolic = 120; RespRate = 16; SpO2 = 98; Pain = 0; Adrenaline = 0.3f; Consciousness = 1;
            Shock = 0; LungDamage = 0; AirwayBlood = 0; HeadTrauma = 0; HeartDestroyed = BrainDestroyed = false; Paralysis = Paralysis.None;
            HitsWhileParalyzed = 0; UndeadMayDie = false; State = LifeState.Alive; CauseOfDeath = ""; heartStopTimer = -1; hypoxiaTimer = 0; faintTimer = 0;
        }
    }
}
