using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// A posable human with no physics: the same procedural body as a Goredoll (HumanModel + FaceRig), for scenes
    /// that need exact, stable poses (the Gore Sandbox 2 intro's body on the gurney, the operating-room menu patient).
    /// Pose it with <see cref="Set"/> (local Euler angles, same conventions as Weapons/FightingStyles.cs FightAnim).
    /// </summary>
    public sealed class StaticHuman : MonoBehaviour
    {
        public readonly Dictionary<PartType, Transform> Parts = new Dictionary<PartType, Transform>();
        public Transform Model { get; private set; }
        public FaceRig Face { get; private set; }
        public float Height { get; private set; } = 1f;

        public static StaticHuman Create(Transform parent, Appearance look, int layer = Layers.Effects, string name = "StaticHuman")
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<StaticHuman>();
            h.Build(look ?? Appearance.Random(-1), layer);
            return h;
        }

        void Build(Appearance look, int layer)
        {
            Height = look.Height;
            Model = new GameObject("Model").transform;
            Model.SetParent(transform, false);
            Model.localScale = Vector3.one * look.Height;
            foreach (PartType t in System.Enum.GetValues(typeof(PartType)))
            {
                var spec = Anatomy.Parts[t];
                var go = new GameObject(spec.Name);
                go.transform.SetParent(spec.Parent.HasValue ? Parts[spec.Parent.Value] : Model, false);
                go.transform.localPosition = spec.Parent.HasValue ? spec.Pivot - Anatomy.Parts[spec.Parent.Value].Pivot : spec.Pivot;
                go.AddComponent<MeshFilter>().sharedMesh = HumanModel.Build(t, look);
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                Parts[t] = go.transform;
            }
            Face = FaceRig.Build(Parts[PartType.Head], look);
            IconFactory.SetLayer(Model.gameObject, layer);
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        public void Set(PartType p, float x, float y = 0f, float z = 0f)
        {
            if (Parts.TryGetValue(p, out var t) && t != null) t.localRotation = Quaternion.Euler(x, y, z);
        }

        public Vector3 PartPosition(PartType p, Vector3 local = default) => Parts.TryGetValue(p, out var t) && t != null ? t.TransformPoint(local) : transform.position;
    }
}
