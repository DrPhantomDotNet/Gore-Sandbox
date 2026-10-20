using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Red barrel. Shoot it a few times, or hit it hard, and it goes up.</summary>
    public sealed class ExplosiveBarrel : MonoBehaviour, IDamageable
    {
        public float Health = 45f;
        bool exploded, burning;
        float fuse;

        public bool IsAlive => !exploded;

        public void TakeDamage(DamageInfo info)
        {
            if (NetGuard.Intercept(this, info)) return;
            if (exploded) return;
            Health -= info.Amount;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.AddForceAtPosition(info.Force * 0.5f, info.Point, ForceMode.Impulse);
            if (info.Type == DamageType.Explosion && Health <= 0) { fuse = Time.time + Random.Range(0.05f, 0.25f); burning = true; return; }
            if (Health <= 0f) Explode();
            else if (Health < 20f && !burning) { burning = true; fuse = Time.time + Random.Range(2f, 4f); AudioManager.Play("hiss", transform.position, 0.7f); }
        }

        void Update()
        {
            if (!burning || exploded) return;
            FX.Fire(transform.position + transform.up * 0.5f, Vector3.up * 2f + Random.insideUnitSphere, 0.35f);
            if (Time.time >= fuse) Explode();
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.impulse.magnitude > 260f) TakeDamage(DamageInfo.Simple(Health, Vector3.zero, transform.position, DamageType.Blunt));
        }

        public void Explode()
        {
            if (exploded) return;
            exploded = true;
            Explosion.Create(transform.position + Vector3.up * 0.3f, 7f, 160f, this);
            Destroy(gameObject);
        }
    }
}
