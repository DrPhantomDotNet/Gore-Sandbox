using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Use it (or shoot it) and a soda can drops out. Gore Sandbox 2: enough hits break it (screen dies, cans spill).</summary>
    public sealed class VendingMachine : MonoBehaviour, IInteractable, IDamageable
    {
        float last;
        int damageCount;
        bool broken;
        public string Prompt => broken ? "Broken" : "Buy a soda (free!)";
        public bool IsAlive => !broken;

        public static void Create(Transform parent, Vector3 pos, float yaw, Color color)
        {
            var go = new GameObject("Vending Machine");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var mb = new MeshBuilder { Color = color, Gloss = 0.5f };
            mb.BevelBox(new Vector3(0, 1f, 0), new Vector3(1f, 2f, 0.8f), 0.04f);
            mb.SetColor(new Color(0.08f, 0.08f, 0.1f), 0.2f);
            mb.Box(new Vector3(0, 0.25f, 0.41f), new Vector3(0.7f, 0.25f, 0.02f));
            mb.SetColor(new Color(0.7f, 0.7f, 0.72f), 0.8f);
            for (int i = 0; i < 6; i++) mb.Box(new Vector3(0.38f, 1.55f - i * 0.12f, 0.41f), new Vector3(0.08f, 0.06f, 0.02f));
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Vending");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var glass = new GameObject("Front");
            glass.transform.SetParent(go.transform, false);
            var gm = new MeshBuilder { Color = Color.white };
            gm.Box(new Vector3(-0.1f, 1.2f, 0.405f), new Vector3(0.65f, 1.3f, 0.01f));
            glass.AddComponent<MeshFilter>().sharedMesh = gm.ToMesh("VendFront");
            glass.AddComponent<MeshRenderer>().sharedMaterial = Mats.Glow(new Color(0.9f, 0.95f, 1f), 0.8f);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 1f, 0); bc.size = new Vector3(1f, 2f, 0.8f);
            go.AddComponent<Surface>().Type = SurfaceType.Metal;
            go.AddComponent<VendingMachine>();
        }

        public void Interact(GameObject user) { if (!broken) Dispense(); }

        public void TakeDamage(DamageInfo info)
        {
            if (NetGuard.Intercept(this, info)) return;
            if (broken) return;
            damageCount++;
            FX.Sparks(info.Point, info.Normal, 8);
            if (damageCount >= 15) { Break(); return; }
            if (damageCount % 3 == 0) Dispense();
        }

        void Break()
        {
            broken = true;
            var front = transform.Find("Front");
            if (front != null) { var r = front.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = Mats.Glass; }
            AudioManager.Play("glass_break", transform.position, 1f);
            FX.Sparks(transform.TransformPoint(new Vector3(0, 1.2f, 0.45f)), transform.forward, 30);
            for (int i = 0; i < 8; i++)
            {
                var can = PropFactory.Spawn("can", transform.TransformPoint(new Vector3(Random.Range(-0.35f, 0.25f), Random.Range(0.6f, 1.7f), 0.55f)), Random.rotation);
                if (can != null) RB.SetVel(can, transform.forward * Random.Range(1f, 3f) + Random.insideUnitSphere);
            }
        }

        void Dispense()
        {
            if (Time.time - last < 0.5f) return;
            last = Time.time;
            AudioManager.Play("metal_impact", transform.position, 0.6f);
            var pos = transform.TransformPoint(new Vector3(0, 0.3f, 0.6f));
            var can = PropFactory.Spawn("can", pos, Random.rotation);
            if (can != null) RB.SetVel(can, transform.forward * 2f);
        }
    }
}
