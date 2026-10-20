using UnityEngine;

namespace SolarEclipse
{
    /// <summary>Wireframe debug mode and collider gizmos for the dev panel (drawn with GL after the scene).</summary>
    public sealed class CameraOverlays : MonoBehaviour
    {
        public static bool Wireframe;

        void OnPreRender() { if (Wireframe) GL.wireframe = true; }
        void OnPostRender()
        {
            GL.wireframe = false;
            if (!Rules.ShowColliders && !Rules.ShowAiThoughts) return;
            Mats.Lines.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            if (Rules.ShowColliders)
            {
                foreach (var doll in Goredoll.All)
                    foreach (var p in doll.Parts.Values)
                    {
                        if (p == null || p.Col == null || !p.Col.enabled) continue;
                        DrawBounds(p.Col.bounds, p.Severed ? new Color(1, 0.5f, 0, 0.8f) : new Color(0, 1, 0.4f, 0.8f));
                    }
            }
            if (Rules.ShowAiThoughts && NavGrid.Current != null)
            {
                foreach (var doll in Goredoll.All)
                {
                    var p = doll.Position;
                    GL.Color(new Color(0.3f, 0.7f, 1f, 0.9f));
                    var v = doll.Ragdoll.DesiredVelocity;
                    GL.Vertex(p); GL.Vertex(p + v);
                }
            }
            GL.End();
            GL.PopMatrix();
        }

        static void DrawBounds(Bounds b, Color c)
        {
            GL.Color(c);
            var mn = b.min; var mx = b.max;
            Vector3[] p =
            {
                new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mx.z),
                new Vector3(mn.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mx.z), new Vector3(mn.x, mx.y, mx.z),
            };
            int[] e = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
            for (int i = 0; i < e.Length; i++) GL.Vertex(p[e[i]]);
        }
    }
}
