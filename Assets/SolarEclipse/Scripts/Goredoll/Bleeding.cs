using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Visual bleeding for one Goredoll: drips and arterial spurts from wounds (pulsing with the heartbeat),
    /// blood trails while moving, and pools that grow under a body lying still.
    /// </summary>
    public sealed class Bleeding : MonoBehaviour
    {
        sealed class Emitter
        {
            public BodyPart Part;
            public Wound Wound;
            public Vector3 LocalPos, LocalNormal;
            public float Accum, PulsePhase;
        }

        const int MaxEmitters = 8;
        readonly List<Emitter> emitters = new List<Emitter>();
        Goredoll doll;
        float poolTimer, poolSize, lastBeat, trailTimer;
        Vector3 lastPoolPos;

        public void Init(Goredoll d) { doll = d; }

        public void AddEmitter(BodyPart part, Wound w)
        {
            if (emitters.Count >= MaxEmitters)
            {
                // Replace the weakest bleeder.
                int weakest = 0;
                for (int i = 1; i < emitters.Count; i++) if (emitters[i].Wound.BleedRate < emitters[weakest].Wound.BleedRate) weakest = i;
                if (emitters[weakest].Wound.BleedRate > w.BleedRate) return;
                emitters.RemoveAt(weakest);
            }
            emitters.Add(new Emitter { Part = part, Wound = w, LocalPos = w.LocalPoint, LocalNormal = w.LocalNormal.sqrMagnitude > 0.01f ? w.LocalNormal.normalized : Vector3.forward });
        }

        /// <summary>Adds a stump emitter at a severed joint (body side).</summary>
        public void AddStump(BodyPart part, Vector3 localPos, Vector3 localNormal, Wound w)
        {
            emitters.Add(new Emitter { Part = part, Wound = w, LocalPos = localPos, LocalNormal = localNormal.normalized });
        }

        void Update()
        {
            if (doll == null || !Settings.GoreEnabled) return;
            var v = doll.Vitals;
            float hr = Mathf.Max(v.HeartRate, 1f);
            bool beat = false;
            if (v.Alive && Time.time - lastBeat > 60f / hr) { lastBeat = Time.time; beat = true; }
            float pressure = v.Alive ? Mathf.Clamp01(v.Systolic / 120f) : 0.1f;
            float dt = Time.deltaTime;

            for (int i = emitters.Count - 1; i >= 0; i--)
            {
                var e = emitters[i];
                if (e.Part == null || e.Part.Gibbed || e.Wound.BleedRate < 0.05f) { emitters.RemoveAt(i); continue; }
                var t = e.Part.transform;
                var pos = t.TransformPoint(e.LocalPos);
                var normal = t.TransformDirection(e.LocalNormal);
                float rate = e.Wound.BleedRate * Rules.BleedMultiplier * Settings.BloodScale;
                if (e.Wound.Arterial && v.Alive)
                {
                    if (beat)
                    {
                        // Spurt with the heartbeat. Neck wounds jet out and arc down onto the floor.
                        var dir = e.Part.Type == PartType.Neck ? (normal * 0.6f + Vector3.down * 0.5f).normalized : normal;
                        BloodFX.Spurt(pos, dir, Mathf.Clamp(rate / 25f, 0.4f, 2.5f) * pressure);
                        if (rate > 15f && Random.value < 0.5f) AudioManager.Play("arterial", pos, 0.5f * pressure, Random.Range(0.9f, 1.1f), 1f, 1f, 18f);
                    }
                }
                e.Accum += rate * dt;
                // One drip per ~4 ml.
                while (e.Accum > 4f)
                {
                    e.Accum -= 4f;
                    BloodFX.Drip(pos + Random.insideUnitSphere * 0.01f, RB.Vel(e.Part.Body) * 0.5f);
                }
                // Wounds slowly clot unless arterial.
                if (!e.Wound.Arterial) e.Wound.BleedRate *= Mathf.Exp(-dt / 60f);
                else if (!v.Alive) e.Wound.BleedRate *= Mathf.Exp(-dt / 20f);
            }

            // Pools: body lying still with blood loss.
            var pelvis = doll.Parts[PartType.Pelvis].Body;
            float speed = RB.Vel(pelvis).magnitude;
            if (doll.TotalBleed > 1f && speed < 0.3f && doll.Ragdoll.IsDown)
            {
                poolTimer += dt;
                if (poolTimer > 2f && (Vector3.Distance(lastPoolPos, pelvis.position) > 0.6f || poolSize < 0.1f))
                {
                    if (Physics.Raycast(pelvis.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1.5f, Layers.DecalMask, QueryTriggerInteraction.Ignore))
                    {
                        poolSize = Mathf.Clamp(0.6f + v.BloodLossFraction * 4f, 0.6f, 2.4f);
                        Decals.Blood(hit.point, hit.normal, poolSize, 2, hit.collider, 18f + poolSize * 6f);
                        lastPoolPos = pelvis.position;
                    }
                }
            }
            else poolTimer = 0f;

            // Trails while being dragged or crawling.
            if (doll.TotalBleed > 2f && speed > 0.6f)
            {
                trailTimer += dt * speed;
                if (trailTimer > 0.35f && Physics.Raycast(pelvis.position + Vector3.up * 0.2f, Vector3.down, out var th, 1.3f, Layers.DecalMask, QueryTriggerInteraction.Ignore))
                {
                    trailTimer = 0;
                    Decals.Blood(th.point, th.normal, Random.Range(0.25f, 0.5f), 3, th.collider, 0f, 70f);
                }
            }
        }
    }
}
