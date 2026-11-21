using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Time Stop power (pause menu > fun rules): every Goredoll and prop freezes in place, mid-air included,
    /// while you keep moving. Hit and shoot them while time is stopped; when time starts again they move off with the
    /// speed they had plus every hit you landed meanwhile. Host / singleplayer only.
    /// </summary>
    public sealed class TimeStop : MonoBehaviour
    {
        public static bool Active;
        static TimeStop inst;
        sealed class Saved { public Vector3 V, W; public Vector3 Pending; public bool WasKinematic; }
        readonly Dictionary<Rigidbody, Saved> frozen = new Dictionary<Rigidbody, Saved>();
        bool on; float nextScan;
        static readonly Collider[] Hits = new Collider[512];
        static readonly int Mask = Layers.Mask(Layers.Ragdoll, Layers.Props, Layers.Debris);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (inst != null) return; var go = new GameObject("Time Stop"); DontDestroyOnLoad(go); inst = go.AddComponent<TimeStop>(); }

        /// <summary>A hit while time is stopped: remembered and released when time starts again.</summary>
        public static bool Absorb(Rigidbody rb, Vector3 velocityChange)
        {
            if (inst == null || !inst.on || rb == null || !inst.frozen.TryGetValue(rb, out var s)) return false;
            s.Pending += velocityChange;
            return true;
        }

        void Update()
        {
            bool want = Active && !NetState.IsClient && PlayerController.Instance != null;
            if (want && !on) { on = true; Stats.Add("timestops"); AudioManager.Play2D("tinnitus", 0.25f, 0.6f, AudioBus.Sfx); CameraFX.Flash(new Color(0.6f, 0.7f, 1f), 0.25f); }
            if (on && want && Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.25f; Scan(); }
            if (on && !want) Release();
        }

        void Scan()
        {
            var p = PlayerController.Instance;
            int n = Physics.OverlapSphereNonAlloc(p.transform.position, 120f, Hits, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var rb = Hits[i].attachedRigidbody;
                if (rb == null || frozen.ContainsKey(rb) || rb.isKinematic) continue;
                frozen[rb] = new Saved { V = RB.Vel(rb), W = rb.angularVelocity, WasKinematic = false };
                rb.isKinematic = true;
            }
        }

        void Release()
        {
            on = false;
            foreach (var kv in frozen)
            {
                var rb = kv.Key;
                if (rb == null) continue;
                rb.isKinematic = kv.Value.WasKinematic;
                if (!rb.isKinematic) { RB.SetVel(rb, kv.Value.V + kv.Value.Pending); rb.angularVelocity = kv.Value.W; }
            }
            frozen.Clear();
            AudioManager.Play2D("thump_heavy", 0.5f, 0.7f, AudioBus.Sfx);
            CameraFX.Shake(0.15f);
        }

        void OnDisable() { if (on) Release(); }
    }
}
