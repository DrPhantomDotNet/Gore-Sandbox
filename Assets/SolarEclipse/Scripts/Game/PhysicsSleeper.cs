using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// GS2 performance: enforces the Max ragdolls limit (oldest corpses sink away; not in story mode), and every
    /// couple of seconds, dead Goredolls far (50 m+) from the player that have nearly
    /// stopped moving are put to sleep, so the physics engine skips them until something touches them.
    /// Host / singleplayer only (clients don't simulate).
    /// </summary>
    public sealed class PhysicsSleeper : MonoBehaviour
    {
        static PhysicsSleeper inst;
        float next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (inst != null) return; var go = new GameObject("Physics Sleeper"); DontDestroyOnLoad(go); inst = go.AddComponent<PhysicsSleeper>(); }

        void Update()
        {
            if (Time.unscaledTime < next || NetState.IsClient) return;
            next = Time.unscaledTime + 2f;
            var p = PlayerController.Instance;
            if (p == null) return;
            var at = p.transform.position;
            // GS2 corpse limit (Settings > Gameplay > Max ragdolls): past it, the oldest corpses sink away and are removed.
            int total = 0; Goredoll oldest = null;
            foreach (var d in Goredoll.All)
            {
                if (d == null) continue;
                total++;
                if (d.IsDead && d.GetComponent<CorpseFade>() == null && d.Zombie == null && (oldest == null || d.Vitals.TimeOfDeath < oldest.Vitals.TimeOfDeath)) oldest = d;
            }
            if (total > Mathf.Max(5, Settings.Data.maxRagdolls) && oldest != null && !StoryRunner.Active && !StoryMode.Active) oldest.gameObject.AddComponent<CorpseFade>();
            foreach (var d in Goredoll.All)
            {
                if (d == null || !d.IsDead || (d.Position - at).sqrMagnitude < 50f * 50f) continue;
                foreach (var bp in d.Parts.Values)
                    if (bp != null && bp.Body != null && !bp.Body.isKinematic && !bp.Body.IsSleeping() && RB.Vel(bp.Body).sqrMagnitude < 0.04f) bp.Body.Sleep();
            }
        }
    }

    /// <summary>Sinks a corpse into the ground over three seconds, then removes it.</summary>
    public sealed class CorpseFade : MonoBehaviour
    {
        Goredoll d; float start;
        void Start()
        {
            d = GetComponent<Goredoll>(); start = Time.time;
            if (d != null) foreach (var bp in d.Parts.Values) if (bp != null && bp.Body != null) { bp.Body.isKinematic = true; foreach (var c in bp.GetComponents<Collider>()) c.enabled = false; }
        }
        void Update()
        {
            if (d == null) { Destroy(this); return; }
            foreach (var bp in d.Parts.Values) if (bp != null && bp.Body != null) bp.Body.transform.position += Vector3.down * Time.deltaTime * 0.45f;
            if (Time.time - start > 3f) d.Despawn();
        }
    }
}
