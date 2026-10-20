using UnityEngine;

namespace SolarEclipse
{
    public sealed class Radio : MonoBehaviour, IInteractable, IDamageable
    {
        AudioSource music;
        bool broken;
        public string Prompt => music != null && music.isPlaying ? "Turn off the radio" : "Turn on the radio";
        public bool IsAlive => !broken;

        public static void Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Radio");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var mb = new MeshBuilder { Color = new Color(0.55f, 0.18f, 0.12f), Gloss = 0.5f };
            mb.BevelBox(new Vector3(0, 0.15f, 0), new Vector3(0.5f, 0.3f, 0.18f), 0.03f);
            mb.SetColor(new Color(0.15f, 0.15f, 0.15f));
            mb.Cylinder(new Vector3(-0.12f, 0.15f, 0.09f), new Vector3(-0.12f, 0.15f, 0.1f), 0.09f, 0.09f, 16);
            mb.SetColor(new Color(0.8f, 0.8f, 0.8f), 0.8f);
            mb.Cylinder(new Vector3(0.18f, 0.3f, 0), new Vector3(0.28f, 0.6f, 0), 0.01f, 0.006f, 6);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Radio");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.15f, 0); bc.size = new Vector3(0.5f, 0.3f, 0.18f);
            var r = go.AddComponent<Radio>();
            r.music = AudioManager.CreateLoop("radio", go.transform, 0.55f, AudioBus.Music, 20f, false);
        }

        public void Interact(GameObject user)
        {
            if (broken || music == null) return;
            AudioManager.Play("button", transform.position, 0.6f);
            if (music.isPlaying) music.Stop(); else music.Play();
        }

        public void TakeDamage(DamageInfo info)
        {
            if (NetGuard.Intercept(this, info)) return;
            if (broken) return;
            broken = true;
            if (music != null) music.Stop();
            FX.Sparks(transform.position, Vector3.up, 20);
            AudioManager.Play("metal_impact", transform.position, 0.8f);
        }
    }
}
