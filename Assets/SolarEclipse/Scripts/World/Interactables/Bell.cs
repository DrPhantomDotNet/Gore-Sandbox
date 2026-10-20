using UnityEngine;

namespace SolarEclipse
{
    public sealed class Bell : MonoBehaviour, IDamageable, IInteractable
    {
        float last;
        Transform body;
        float swing;
        public bool IsAlive => true;
        public string Prompt => "Ring the bell";

        public static void Create(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Bell");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var frame = new MeshBuilder { Color = new Color(0.35f, 0.25f, 0.15f), Gloss = 0.2f };
            frame.Box(new Vector3(-0.7f, 1.3f, 0), new Vector3(0.15f, 2.6f, 0.15f));
            frame.Box(new Vector3(0.7f, 1.3f, 0), new Vector3(0.15f, 2.6f, 0.15f));
            frame.Box(new Vector3(0, 2.6f, 0), new Vector3(1.6f, 0.15f, 0.2f));
            go.AddComponent<MeshFilter>().sharedMesh = frame.ToMesh("BellFrame");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var bellGo = new GameObject("BellBody");
            bellGo.transform.SetParent(go.transform, false);
            bellGo.transform.localPosition = new Vector3(0, 2.5f, 0);
            var bm = new MeshBuilder { Color = new Color(0.85f, 0.65f, 0.25f), Gloss = 0.85f };
            bm.Loft(8, 20, i => (new Vector3(0, -i * 0.08f, 0), 0.12f + Mathf.Pow(i / 7f, 2f) * 0.3f, 0.12f + Mathf.Pow(i / 7f, 2f) * 0.3f), Vector3.down, true, false);
            bellGo.AddComponent<MeshFilter>().sharedMesh = bm.ToMesh("Bell");
            bellGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.LitMetal;
            var sc = bellGo.AddComponent<SphereCollider>();
            sc.center = new Vector3(0, -0.35f, 0); sc.radius = 0.35f;
            bellGo.AddComponent<Surface>().Type = SurfaceType.Metal;
            bellGo.AddComponent<Bell>().body = bellGo.transform;
        }

        public void Interact(GameObject user) => Ring(1f);
        public void TakeDamage(DamageInfo info) { if (NetGuard.Intercept(this, info)) return; Ring(Mathf.Clamp(info.Amount / 30f, 0.4f, 1.2f)); }

        void Ring(float strength)
        {
            if (Time.time - last < 0.15f) return;
            last = Time.time;
            swing = Mathf.Clamp(swing + 25f * strength, 0, 40f);
            AudioManager.Play("bell", transform.position, strength, 1f, 1f, 4f, 150f);
            GameEvents.EmitNoise(transform.position, 50f, 0.2f);
        }

        void Update()
        {
            if (body == null) return;
            swing = Mathf.MoveTowards(swing, 0, Time.deltaTime * 6f);
            body.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 7f) * swing, 0, 0);
        }
    }
}
