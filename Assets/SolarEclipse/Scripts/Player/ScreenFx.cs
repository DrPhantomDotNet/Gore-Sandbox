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
            float g = Rules.LowGravity ? -3.2f : -9.81f;
            if (Mathf.Abs(Physics.gravity.y - g) > 0.01f) Physics.gravity = new Vector3(0f, g, 0f);
            if (c == null) return;
            bool want = Rules.Noir || Rules.Retro || Rules.Mirror;
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
            if (!Rules.Noir && !Rules.Retro) { Graphics.Blit(src, dst); return; }
            if (mat == null)
            {
                var sh = Shader.Find("SolarEclipse/ScreenFx");
                if (sh == null) { Graphics.Blit(src, dst); return; }
                mat = new Material(sh) { hideFlags = HideFlags.DontSave };
            }
            mat.SetFloat("_Mode", Rules.Retro ? 2f : 1f);
            Graphics.Blit(src, dst, mat);
        }

        void OnDisable() { GL.invertCulling = false; if (cam != null) cam.ResetProjectionMatrix(); }
    }
}
