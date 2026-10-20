using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Shoot it and it vents, rockets around, then detonates.</summary>
    public sealed class PropaneTank : MonoBehaviour, IDamageable
    {
        bool venting, exploded;
        float timer;
        Vector3 ventLocal = Vector3.up;
        AudioSource hiss;

        public bool IsAlive => !exploded;

        public void TakeDamage(DamageInfo info)
        {
            if (NetGuard.Intercept(this, info)) return;
            if (exploded) return;
            if (!venting)
            {
                venting = true;
                timer = Random.Range(1.5f, 3f);
                ventLocal = transform.InverseTransformDirection(-info.Direction).normalized;
                hiss = AudioManager.CreateLoop("hiss", transform, 1f);
            }
            else if (info.Amount > 30f) timer = Mathf.Min(timer, 0.1f);
        }

        void FixedUpdate()
        {
            if (!venting || exploded) return;
            var rb = GetComponent<Rigidbody>();
            var ventWorld = transform.TransformDirection(ventLocal);
            if (rb != null) rb.AddForceAtPosition(-ventWorld * 260f, transform.position + ventWorld * 0.2f, ForceMode.Force);
            FX.Smoke(transform.position + ventWorld * 0.3f, ventWorld * 6f, 0.3f, new Color(0.9f, 0.9f, 0.9f, 0.5f), 0.8f);
            timer -= Time.fixedDeltaTime;
            if (timer <= 0f)
            {
                exploded = true;
                Explosion.Create(transform.position, 6f, 140f, this);
                Destroy(gameObject);
            }
        }
    }
}
