using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Root of the currently loaded map. Everything spawned during play lives under DynamicRoot.</summary>
    public sealed class WorldRoot : MonoBehaviour
    {
        public static WorldRoot Current { get; private set; }
        public MapDef Def;
        public MapOptions Options;
        public Transform DynamicRoot;
        public Vector3 PlayerSpawn;
        public float PlayerYaw;
        public float KillY = -40f;
        public Bounds Playable;
        public MapBuilder Builder;
        public float StartedAt;

        public static WorldRoot Create(MapDef def, MapOptions options)
        {
            var go = new GameObject("World · " + def.Name);
            var w = go.AddComponent<WorldRoot>();
            w.Def = def;
            w.Options = options;
            w.DynamicRoot = new GameObject("Dynamic").transform;
            w.DynamicRoot.SetParent(go.transform, false);
            w.StartedAt = Time.time;
            Current = w;
            return w;
        }

        /// <summary>Removes everything that was spawned (props, Goredolls, gibs) but keeps the map.</summary>
        public void ClearDynamic()
        {
            foreach (var d in Goredoll.All.ToArray()) if (d != null) d.Despawn();
            for (int i = DynamicRoot.childCount - 1; i >= 0; i--) Destroy(DynamicRoot.GetChild(i).gameObject);
            SpawnGun.UndoStack.Clear();
            Decals.ClearAll();
            Debris.ClearAll();
            FX.Clear();
            BloodFX.Clear();
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
