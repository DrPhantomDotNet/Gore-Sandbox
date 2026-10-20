using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Launches anything that touches it straight up (player, ragdolls, props).</summary>
    public sealed class JumpPad : MonoBehaviour
    {
        public float Launch = 16f;
        float lastSound;

        void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                var v = RB.Vel(rb);
                v.y = Launch;
                RB.SetVel(rb, v);
                // Lift the whole ragdoll, not just the limb that touched.
                var doll = other.GetComponentInParent<Goredoll>();
                if (doll != null) doll.Launch(Vector3.up * Launch);
                Boing();
                return;
            }
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null) { player.Launch(Vector3.up * Launch * 1.05f); Boing(); }
        }

        void Boing()
        {
            if (Time.time - lastSound < 0.15f) return;
            lastSound = Time.time;
            AudioManager.Play("boing", transform.position, 1f, Random.Range(0.95f, 1.1f));
        }

        public static JumpPad Create(Transform parent, Vector3 pos, float launch = 16f)
        {
            var go = new GameObject("Jump Pad");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var mb = new MeshBuilder { Color = new Color(0.15f, 0.15f, 0.17f), Gloss = 0.4f };
            mb.Cylinder(Vector3.zero, Vector3.up * 0.15f, 1.2f, 1.1f, 24);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("JumpPadBase");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 1.15f; col.height = 0.3f; col.center = Vector3.up * 0.075f; col.direction = 1;
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, false);
            var gmb = new MeshBuilder { Color = Color.white };
            gmb.Cylinder(Vector3.up * 0.15f, Vector3.up * 0.17f, 0.95f, 0.95f, 24);
            glowGo.AddComponent<MeshFilter>().sharedMesh = gmb.ToMesh("JumpPadGlow");
            glowGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.Glow(new Color(0.2f, 1f, 0.5f), 2f);
            var trig = new GameObject("Trigger");
            trig.layer = Layers.Trigger;
            trig.transform.SetParent(go.transform, false);
            var box = trig.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2f, 0.6f, 2f);
            box.center = Vector3.up * 0.4f;
            var pad = trig.AddComponent<JumpPad>();
            pad.Launch = launch;
            return pad;
        }
    }
}
