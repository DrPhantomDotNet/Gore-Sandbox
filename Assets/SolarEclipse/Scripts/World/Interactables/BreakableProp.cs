using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Crates, glass panes, bottles: take damage, then shatter into physics fragments.</summary>
    public sealed class BreakableProp : MonoBehaviour, IDamageable
    {
        public float Health = 40f;
        public SurfaceType Material = SurfaceType.Wood;
        public int Fragments = 6;
        public Color FragmentColor = new Color(0.6f, 0.42f, 0.25f);
        bool broken;

        public bool IsAlive => !broken;

        public void TakeDamage(DamageInfo info)
        {
            if (NetGuard.Intercept(this, info)) return;
            if (broken) return;
            Health -= info.Amount;
            var rb = GetComponent<Rigidbody>();
            if (rb != null && info.Force.sqrMagnitude > 0) rb.AddForceAtPosition(info.Force, info.Point, ForceMode.Impulse);
            if (Health <= 0f) Break(info.Direction);
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.impulse.magnitude > 60f * (GetComponent<Rigidbody>()?.mass ?? 10f) * 0.05f + 25f)
                TakeDamage(DamageInfo.Simple(c.impulse.magnitude * 0.3f, Vector3.zero, c.GetContact(0).point, DamageType.Blunt));
        }

        public void Break(Vector3 dir)
        {
            if (broken) return;
            broken = true;
            var bounds = GetComponent<Renderer>() != null ? GetComponent<Renderer>().bounds : new Bounds(transform.position, Vector3.one * 0.5f);
            var parentVel = GetComponent<Rigidbody>() != null ? RB.Vel(GetComponent<Rigidbody>()) : Vector3.zero;
            if (Material == SurfaceType.Glass)
            {
                FX.Glass(bounds.center, dir, 50);
                AudioManager.Play("glass_break_big", bounds.center, 1f);
            }
            else
            {
                AudioManager.Play("crate_break", bounds.center, 1f);
                FX.Dust(bounds.center, bounds.extents.magnitude * 0.6f, new Color(0.5f, 0.42f, 0.32f, 0.5f), 10);
                for (int i = 0; i < Fragments; i++)
                {
                    var size = new Vector3(Random.Range(0.06f, 0.14f), Random.Range(0.03f, 0.06f), Random.Range(0.3f, 0.7f)) * Mathf.Max(0.6f, bounds.size.x);
                    var pos = bounds.center + Vector3.Scale(Random.insideUnitSphere, bounds.extents * 0.7f);
                    var frag = PropFactory.SimpleBody("Fragment", pos, Random.rotation, size, FragmentColor * Random.Range(0.8f, 1.1f), 0.8f, SurfaceType.Wood, Layers.Debris);
                    RB.SetVel(frag, parentVel + (Random.onUnitSphere + dir * 0.5f) * Random.Range(1f, 4f));
                    Debris.Register(frag.gameObject, 20f);
                }
            }
            GameEvents.EmitNoise(bounds.center, 20f, 0.4f);
            Destroy(gameObject);
        }
    }
}
