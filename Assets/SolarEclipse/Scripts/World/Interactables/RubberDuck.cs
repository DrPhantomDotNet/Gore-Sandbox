using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Hidden collectible. Squeak it (E or shoot) to find it.</summary>
    public sealed class RubberDuck : MonoBehaviour, IInteractable, IDamageable
    {
        public string Id;
        bool found;
        public string Prompt => "Squeeze the duck";
        public bool IsAlive => true;

        public static void Create(Transform parent, Vector3 pos, string id)
        {
            var go = new GameObject("Rubber Duck");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            var mb = new MeshBuilder { Color = new Color(1f, 0.85f, 0.1f), Gloss = 0.75f };
            mb.Sphere(new Vector3(0, 0.07f, 0), new Vector3(0.1f, 0.07f, 0.13f), 8, 12);
            mb.Sphere(new Vector3(0, 0.17f, 0.06f), new Vector3(0.06f, 0.06f, 0.06f), 8, 12);
            mb.SetColor(new Color(1f, 0.45f, 0.05f), 0.6f);
            mb.Sphere(new Vector3(0, 0.16f, 0.12f), new Vector3(0.03f, 0.012f, 0.03f), 4, 8);
            mb.SetColor(Color.black, 0.9f);
            mb.Sphere(new Vector3(0.03f, 0.19f, 0.105f), Vector3.one * 0.008f, 4, 6);
            mb.Sphere(new Vector3(-0.03f, 0.19f, 0.105f), Vector3.one * 0.008f, 4, 6);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Duck");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var sc = go.AddComponent<SphereCollider>();
            sc.center = new Vector3(0, 0.1f, 0); sc.radius = 0.13f;
            go.AddComponent<RubberDuck>().Id = id;
        }

        public void Interact(GameObject user) => Squeak();
        public void TakeDamage(DamageInfo info) => Squeak();

        void Squeak()
        {
            AudioManager.Play("duck", transform.position, 1f, Random.Range(0.95f, 1.15f));
            transform.localScale = new Vector3(1.2f, 0.8f, 1.2f);
            if (found) return;
            found = true;
            if (PlayerPrefs.GetInt("duck_" + Id, 0) == 0)
            {
                PlayerPrefs.SetInt("duck_" + Id, 1);
                Stats.Add(Stats.Ducks);
                if (Stats.Get(Stats.Ducks) >= 5) EasterEggs.Found("Duck Collector", "Five rubber ducks. They're watching you now.");
                Toasts.Show("RUBBER DUCK FOUND", "Quack! (" + Stats.Get(Stats.Ducks).ToString("0") + " found)", UI.Warn);
            }
            else GameEvents.ShowToast("Quack. You already found this one.");
        }

        void Update() => transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.deltaTime * 10f);
    }
}
