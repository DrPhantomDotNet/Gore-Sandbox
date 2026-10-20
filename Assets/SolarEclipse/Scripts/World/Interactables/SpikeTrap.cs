using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A bed of spikes. Falling bodies get impaled (stab damage scaled by impact speed).</summary>
    public sealed class SpikeTrap : MonoBehaviour
    {
        public static void Create(Transform parent, Vector3 center, Vector2 size)
        {
            var go = new GameObject("Spike Pit");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var mb = new MeshBuilder { Color = new Color(0.35f, 0.33f, 0.32f), Gloss = 0.6f };
            mb.Box(new Vector3(0, 0.05f, 0), new Vector3(size.x, 0.1f, size.y));
            mb.SetColor(new Color(0.55f, 0.55f, 0.58f), 0.8f);
            for (float x = -size.x / 2 + 0.25f; x < size.x / 2; x += 0.5f)
                for (float z = -size.y / 2 + 0.25f; z < size.y / 2; z += 0.5f)
                    mb.Cylinder(new Vector3(x, 0.1f, z), new Vector3(x, 0.75f, z), 0.06f, 0.003f, 6, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Spikes");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var floor = go.AddComponent<BoxCollider>();
            floor.center = new Vector3(0, 0.05f, 0); floor.size = new Vector3(size.x, 0.1f, size.y);
            var trig = new GameObject("SpikeZone");
            trig.layer = Layers.Trigger;
            trig.transform.SetParent(go.transform, false);
            var box = trig.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, 0.45f, 0); box.size = new Vector3(size.x, 0.7f, size.y);
            trig.AddComponent<SpikeTrap>();
        }

        void OnTriggerEnter(Collider other)
        {
            var part = other.GetComponent<BodyPart>();
            var rb = other.attachedRigidbody;
            if (part != null && rb != null)
            {
                float speed = RB.Vel(rb).magnitude;
                if (speed < 2f) return;
                part.TakeDamage(new DamageInfo { Amount = speed * 9f, Force = Vector3.zero, Point = other.transform.position, Normal = Vector3.up, Direction = Vector3.down, Type = DamageType.Stab, Source = this, Weapon = "Spikes" });
                RB.SetVel(rb, RB.Vel(rb) * 0.2f);
                AudioManager.Play("stab", other.transform.position, 1f);
                return;
            }
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null) player.Health.TakeDamage(DamageInfo.Simple(35f, Vector3.zero, other.transform.position, DamageType.Stab, this));
        }
    }
}
