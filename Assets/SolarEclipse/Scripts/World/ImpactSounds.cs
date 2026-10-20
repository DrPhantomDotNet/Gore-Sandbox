using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Plays a surface-appropriate impact sound and stirs up a little dust when a body hits something hard.</summary>
    public sealed class ImpactSounds : MonoBehaviour
    {
        float last;
        Surface surface;
        Rigidbody rb;

        void Awake() { surface = GetComponent<Surface>(); rb = GetComponent<Rigidbody>(); }

        void OnCollisionEnter(Collision c)
        {
            if (Time.time - last < 0.08f) return;
            float speed = c.relativeVelocity.magnitude;
            if (speed < 1.5f) return;
            last = Time.time;
            var type = surface != null ? surface.Type : SurfaceType.Concrete;
            string clip = type == SurfaceType.Metal ? (rb != null && rb.mass > 25f ? "metal_heavy" : "metal_impact") : type == SurfaceType.Wood ? "wood_impact" : type == SurfaceType.Glass ? "ricochet" : "body_thud_soft";
            float vol = Mathf.Clamp01(speed / 10f);
            AudioManager.Play(clip, c.GetContact(0).point, vol, Random.Range(0.9f, 1.1f) * (rb != null && rb.mass < 3f ? 1.3f : 1f), 1f, 1f, 40f);
            if (speed > 6f) GameEvents.EmitNoise(transform.position, speed * 2f, 0.1f, this);
        }
    }
}
