using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A lamp that breaks when shot: light dies, glass shatters, sparks fly.</summary>
    public sealed class ShootableLight : MonoBehaviour, IDamageable
    {
        Light lightSource;
        MeshRenderer bulb;
        bool broken;
        float flicker;

        public bool IsAlive => !broken;

        public void Init(Light l, MeshRenderer r) { lightSource = l; bulb = r; flicker = Random.value * 100f; }

        void Update()
        {
            if (broken || lightSource == null) return;
            // Occasional flicker for atmosphere.
            if (Random.value < 0.0015f) flicker = Time.time + Random.Range(0.05f, 0.4f);
            lightSource.enabled = Time.time > flicker || Random.value < 0.4f;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (NetGuard.Intercept(this, info)) return;
            if (broken || info.Amount < 5f) return;
            broken = true;
            if (lightSource != null) lightSource.enabled = false;
            if (bulb != null) bulb.sharedMaterial = Mats.Lit;
            FX.Glass(transform.position, info.Direction, 18);
            FX.Sparks(transform.position, Vector3.down, 30, 5f);
            FX.Flash(transform.position, new Color(0.8f, 0.9f, 1f), 8f, 3f, 0.15f);
            AudioManager.Play("glass_break", transform.position, 0.8f);
            GameEvents.EmitNoise(transform.position, 15f, 0.3f);
        }
    }
}
