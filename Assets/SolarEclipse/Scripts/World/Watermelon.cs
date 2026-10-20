using UnityEngine;

namespace SolarEclipse
{
    /// <summary>The classic gore test subject. Bursts into red chunks.</summary>
    public sealed class Watermelon : MonoBehaviour, IDamageable
    {
        float health = 25f;
        bool burst;
        public bool IsAlive => !burst;

        public void TakeDamage(DamageInfo info)
        {
            if (NetGuard.Intercept(this, info)) return;
            if (burst) return;
            health -= info.Amount;
            GetComponent<Rigidbody>()?.AddForceAtPosition(info.Force * 0.3f, info.Point, ForceMode.Impulse);
            if (health <= 0) Burst(info.Direction);
        }

        void OnCollisionEnter(Collision c) { if (c.relativeVelocity.magnitude > 9f) Burst(Vector3.up); }

        void Burst(Vector3 dir)
        {
            if (burst) return;
            burst = true;
            AudioManager.Play("gib", transform.position, 0.8f, 1.3f);
            BloodFX.Burst(transform.position, dir, 0.6f, new Color(0.9f, 0.15f, 0.2f));
            for (int i = 0; i < 7; i++)
            {
                var chunk = PropFactory.SimpleBody("Melon Chunk", transform.position + Random.insideUnitSphere * 0.1f, Random.rotation,
                    new Vector3(Random.Range(0.06f, 0.12f), Random.Range(0.04f, 0.08f), Random.Range(0.06f, 0.12f)), i % 2 == 0 ? new Color(0.9f, 0.2f, 0.25f) : new Color(0.2f, 0.5f, 0.15f), 0.3f, SurfaceType.Flesh, Layers.Debris);
                RB.SetVel(chunk, (Random.onUnitSphere + dir) * Random.Range(2f, 6f));
                Debris.Register(chunk.gameObject, 25f, Debris.GroupGib);
            }
            Destroy(gameObject);
        }
    }
}
