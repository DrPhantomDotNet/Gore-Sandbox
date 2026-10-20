using System;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Big red button. Press with E or shoot it.</summary>
    public sealed class PushButton : MonoBehaviour, IInteractable, IDamageable
    {
        public string Label = "Press";
        public Action OnPressed;
        public float Cooldown = 0.6f;
        float last = -10f;
        Transform cap;

        public string Prompt => Label;
        public bool IsAlive => true;

        public void Init(Transform capTransform) { cap = capTransform; }

        public void Interact(GameObject user) => Press();
        public void TakeDamage(DamageInfo info) { if (NetGuard.Intercept(this, info)) return; if (info.Amount > 1f) Press(); }

        public void Press()
        {
            if (Time.time - last < Cooldown) return;
            last = Time.time;
            AudioManager.Play("button", transform.position, 0.9f);
            OnPressed?.Invoke();
        }

        void Update()
        {
            if (cap == null) return;
            float pressed = Mathf.Clamp01(1f - (Time.time - last) / 0.25f);
            cap.localPosition = new Vector3(0, 0.12f - pressed * 0.07f, 0);
        }

        public static PushButton Create(Transform parent, Vector3 pos, Quaternion rot, string label, Action onPressed)
        {
            var root = new GameObject("Button: " + label);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);
            var mb = new MeshBuilder { Color = new Color(0.25f, 0.25f, 0.28f), Gloss = 0.5f };
            mb.BevelBox(new Vector3(0, 0.5f, 0), new Vector3(0.5f, 1f, 0.5f), 0.04f);
            root.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("ButtonBase");
            root.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            root.AddComponent<BoxCollider>().center = new Vector3(0, 0.55f, 0);
            root.GetComponent<BoxCollider>().size = new Vector3(0.5f, 1.1f, 0.5f);
            root.AddComponent<Surface>().Type = SurfaceType.Metal;
            var capGo = new GameObject("Cap");
            capGo.transform.SetParent(root.transform, false);
            capGo.transform.localPosition = new Vector3(0, 1.0f, 0);
            var top = new GameObject("CapMesh");
            top.transform.SetParent(capGo.transform, false);
            var cmb = new MeshBuilder { Color = new Color(0.85f, 0.08f, 0.08f), Gloss = 0.7f };
            cmb.Cylinder(Vector3.zero, Vector3.up * 0.1f, 0.16f, 0.14f, 16);
            top.AddComponent<MeshFilter>().sharedMesh = cmb.ToMesh("ButtonCap");
            top.AddComponent<MeshRenderer>().sharedMaterial = Mats.Glow(new Color(0.9f, 0.1f, 0.1f), 0.6f);
            var b = root.AddComponent<PushButton>();
            b.Label = label;
            b.OnPressed = onPressed;
            b.Init(capGo.transform);
            return b;
        }
    }
}
