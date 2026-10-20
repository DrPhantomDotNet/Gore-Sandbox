using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    public enum NetKind : byte { MapObject, Prop, Doll, Nextbot }

    /// <summary>
    /// Network identity. Map objects get ids 1..N in hierarchy order right after the (seeded, identical) map build
    /// on every machine; things spawned during play get ids from 10000 up, assigned by the host.
    /// On clients, physics bodies of networked objects are kinematic "proxies" that follow host snapshots.
    /// </summary>
    public sealed class NetEntity : MonoBehaviour
    {
        public static readonly Dictionary<ushort, NetEntity> All = new Dictionary<ushort, NetEntity>();
        public static ushort NextDynamicId = 10000;
        public static bool SuppressDespawn;

        public ushort Id;
        public NetKind Kind;
        public string DefId = "";
        public Rigidbody Body;
        public bool Frozen;            // frozen by the spawn gun (kinematic on purpose)
        public bool WasKinematic;      // kinematic before becoming a proxy (script driven)
        public bool Proxy;
        public byte Owner;             // player who spawned it (for undo)
        public int Seed;
        // Client-side smoothing targets
        public Vector3 TargetPos;
        public Quaternion TargetRot = Quaternion.identity;
        public bool HasTarget;
        public float LastSentTime = -99f;
        public float Scale = 1f;

        public static NetEntity Get(ushort id) => id != 0 && All.TryGetValue(id, out var e) && e != null ? e : null;

        public static NetEntity Of(Component c) => c == null ? null : c.GetComponentInParent<NetEntity>();

        public static NetEntity Register(GameObject go, ushort id, NetKind kind)
        {
            var e = go.GetComponent<NetEntity>() ?? go.AddComponent<NetEntity>();
            if (e.Id != 0 && All.TryGetValue(e.Id, out var old) && old == e) All.Remove(e.Id);
            e.Id = id;
            e.Kind = kind;
            e.Body = go.GetComponent<Rigidbody>();
            if (e.Body != null) e.WasKinematic = e.Body.isKinematic;
            All[id] = e;
            return e;
        }

        /// <summary>Client: the host simulates this object; we only display it.</summary>
        public void MakeProxy()
        {
            Proxy = true;
            if (Body != null && Kind != NetKind.Doll)
            {
                Body.isKinematic = true;
                Body.interpolation = RigidbodyInterpolation.None;
            }
            TargetPos = transform.position;
            TargetRot = transform.rotation;
        }

        void Update()
        {
            if (!Proxy || !HasTarget || Kind == NetKind.Doll) return;
            if (WasKinematic && !Frozen) return; // script-driven (fans, presses) run locally
            float k = 1f - Mathf.Exp(-18f * Time.deltaTime);
            var t = transform;
            if ((t.position - TargetPos).sqrMagnitude > 9f) { t.SetPositionAndRotation(TargetPos, TargetRot); return; }
            t.SetPositionAndRotation(Vector3.Lerp(t.position, TargetPos, k), Quaternion.Slerp(t.rotation, TargetRot, k));
        }

        void OnDestroy()
        {
            if (Id != 0 && All.TryGetValue(Id, out var e) && e == this) All.Remove(Id);
            if (!SuppressDespawn && NetState.IsHost && Id != 0 && NetManager.I != null) NetManager.I.OnEntityDestroyed(this);
        }

        /// <summary>Assigns deterministic ids to every networked object of a freshly built map.</summary>
        public static int RegisterMap(Transform worldRoot)
        {
            ushort id = 1;
            int count = 0;
            foreach (var t in worldRoot.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (go.layer == Layers.Boundaries || go.GetComponentInParent<Goredoll>() != null || go.GetComponentInParent<PlayerController>() != null) continue;
                if (go.GetComponent<RubberDuck>() != null) continue; // collectibles stay personal
                if (t.parent != null && t.parent.GetComponentInParent<NetEntity>() != null) continue;
                bool networked = go.GetComponent<Rigidbody>() != null;
                if (!networked)
                    foreach (var mb in go.GetComponents<MonoBehaviour>())
                        if (mb is IDamageable || mb is IInteractable) { networked = true; break; }
                if (!networked) continue;
                Register(go, id++, NetKind.MapObject);
                count++;
            }
            return count;
        }

        public static void ClearAll()
        {
            All.Clear();
            NextDynamicId = 10000;
        }
    }

    /// <summary>Anything the AI can treat as "a player": the local player and (on the host) remote players.</summary>
    public interface IPlayerTarget
    {
        Vector3 Position { get; }
        Vector3 EyePosition { get; }
        Vector3 AimDirection { get; }
        bool HoldingGun { get; }
        bool IsDead { get; }
        Transform Root { get; }
        IDamageable Damageable { get; }
        string PlayerName { get; }
    }

    public static class PlayerTargets
    {
        public static readonly List<IPlayerTarget> All = new List<IPlayerTarget>();

        public static IPlayerTarget Nearest(Vector3 from, float maxDistance = 1000f, bool aliveOnly = true)
        {
            IPlayerTarget best = null;
            float bd = maxDistance * maxDistance;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var p = All[i];
                if (p == null || (p is Object o && o == null)) { All.RemoveAt(i); continue; }
                if (aliveOnly && p.IsDead) continue;
                float d = (p.Position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        /// <summary>True if a damage/noise source is any player (local or remote).</summary>
        public static bool IsPlayerSource(object source) => source is PlayerController || source is Weapon || source is IPlayerTarget;
    }
}
