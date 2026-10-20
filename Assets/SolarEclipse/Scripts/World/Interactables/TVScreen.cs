using UnityEngine;

namespace SolarEclipse
{
    /// <summary>A TV showing static. Shoot the screen to smash it.</summary>
    public sealed class TVScreen : MonoBehaviour, IDamageable
    {
        MeshRenderer screen;
        AudioSource loop;
        bool broken;
        Texture2D staticTex;
        Color[] pixels;
        public bool IsAlive => !broken;

        public static void Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("TV");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var mb = new MeshBuilder { Color = new Color(0.1f, 0.1f, 0.11f), Gloss = 0.4f };
            mb.BevelBox(new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.75f, 0.12f), 0.03f);
            mb.Box(new Vector3(0, 0.03f, 0), new Vector3(0.5f, 0.06f, 0.3f));
            mb.Box(new Vector3(0, 0.07f, 0), new Vector3(0.08f, 0.1f, 0.04f));
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("TV");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.45f, 0); bc.size = new Vector3(1.2f, 0.75f, 0.12f);
            go.AddComponent<Surface>().Type = SurfaceType.Glass;
            var scr = new GameObject("Screen");
            scr.transform.SetParent(go.transform, false);
            var sm = new MeshBuilder { Color = Color.white };
            sm.Face(new Vector3(0, 0.45f, 0.062f), Vector3.forward, Vector3.up, 0.53f, 0.31f);
            scr.AddComponent<MeshFilter>().sharedMesh = sm.ToMesh("TVScreen");
            var tv = go.AddComponent<TVScreen>();
            tv.screen = scr.AddComponent<MeshRenderer>();
            tv.staticTex = new Texture2D(64, 40, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tv.pixels = new Color[64 * 40];
            var mat = new Material(Mats.Glow(Color.white, 1.2f)) { mainTexture = tv.staticTex };
            mat.SetTexture("_EmissionMap", tv.staticTex);
            tv.screen.sharedMaterial = mat;
            tv.loop = AudioManager.CreateLoop("tv_static", go.transform, 0.35f, AudioBus.Sfx, 12f);
        }

        void Update()
        {
            if (broken || staticTex == null || Time.frameCount % 2 != 0) return;
            for (int i = 0; i < pixels.Length; i++) { float v = Random.value; pixels[i] = new Color(v, v, v, 1); }
            staticTex.SetPixels(pixels);
            staticTex.Apply(false);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (NetGuard.Intercept(this, info)) return;
            if (broken) return;
            broken = true;
            if (loop != null) loop.Stop();
            screen.sharedMaterial = Mats.Lit;
            FX.Glass(info.Point, info.Direction, 25);
            FX.Sparks(info.Point, -info.Direction, 25);
            AudioManager.Play("glass_break", info.Point, 1f);
        }
    }
}
