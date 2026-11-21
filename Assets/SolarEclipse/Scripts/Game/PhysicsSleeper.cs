using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// GS2 performance: every couple of seconds, dead Goredolls and props far from the player that have nearly
    /// stopped moving are put to sleep, so the physics engine skips them until something touches them.
    /// Host / singleplayer only (clients don't simulate).
    /// </summary>
    public sealed class PhysicsSleeper : MonoBehaviour
    {
        static PhysicsSleeper inst;
        float next;
        static readonly Collider[] Hits = new Collider[256];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (inst != null) return; var go = new GameObject("Physics Sleeper"); DontDestroyOnLoad(go); inst = go.AddComponent<PhysicsSleeper>(); }

        void Update()
        {
            if (Time.unscaledTime < next || NetState.IsClient) return;
            next = Time.unscaledTime + 2f;
            var p = PlayerController.Instance;
            if (p == null) return;
            var at = p.transform.position;
            foreach (var d in Goredoll.All)
            {
                if (d == null || !d.IsDead || (d.Position - at).sqrMagnitude < 50f * 50f) continue;
                foreach (var bp in d.Parts.Values)
                    if (bp != null && bp.Body != null && !bp.Body.isKinematic && !bp.Body.IsSleeping() && RB.Vel(bp.Body).sqrMagnitude < 0.04f) bp.Body.Sleep();
            }
        }
    }
}
