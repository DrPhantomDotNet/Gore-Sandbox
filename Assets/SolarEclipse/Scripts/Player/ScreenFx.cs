using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Gore Sandbox 2 fun modes on the player camera (Pause > World): noir (black and white with red blood), retro
    /// (low resolution, few colours, scanlines), mirror world (the picture flipped left-right) and low gravity.
    /// The component is only added while a filter is on, so normal play pays nothing for it.
    /// </summary>
    public sealed class ScreenFx : MonoBehaviour
    {
        static Material mat;
        Camera cam;

        public static void Sync(Camera c)
        {
            float g = -9.81f * Rules.GravityScale * (Rules.LowGravity ? 0.33f : 1f); // respects the gravity slider and Moon Base
            if (Mathf.Abs(Physics.gravity.y - g) > 0.01f) Physics.gravity = new Vector3(0f, g, 0f);
            if (c == null) return;
            bool want = Rules.Noir || Rules.Retro || Rules.Mirror || Settings.Data.colorBlind > 0;
            var fx = c.GetComponent<ScreenFx>();
            if (want && fx == null) { fx = c.gameObject.AddComponent<ScreenFx>(); fx.cam = c; }
            else if (!want && fx != null) { c.ResetProjectionMatrix(); Destroy(fx); }
        }

        void OnPreCull()
        {
            if (cam == null) return;
            cam.ResetProjectionMatrix();
            if (Rules.Mirror) cam.projectionMatrix = cam.projectionMatrix * Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
        }
        void OnPreRender() { if (Rules.Mirror) GL.invertCulling = true; }
        void OnPostRender() { GL.invertCulling = false; }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!Rules.Noir && !Rules.Retro && Settings.Data.colorBlind <= 0) { Graphics.Blit(src, dst); return; }
            if (mat == null)
            {
                var sh = Shader.Find("SolarEclipse/ScreenFx");
                if (sh == null) { Graphics.Blit(src, dst); return; }
                mat = new Material(sh) { hideFlags = HideFlags.DontSave };
            }
            mat.SetFloat("_Mode", Rules.Retro ? 2f : Rules.Noir ? 1f : 3f);
            mat.SetFloat("_CB", Settings.Data.colorBlind); // GS2 colour-blind correction (1 protan, 2 deutan, 3 tritan)
            Graphics.Blit(src, dst, mat);
        }

        void OnDisable() { GL.invertCulling = false; if (cam != null) cam.ResetProjectionMatrix(); }
    }
}
