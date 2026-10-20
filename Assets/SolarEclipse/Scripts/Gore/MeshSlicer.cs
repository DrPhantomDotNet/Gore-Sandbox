using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarEclipse
{
    /// <summary>
    /// Cuts a mesh with a plane into two closed meshes. The cut faces are capped (submesh 1) with radial UVs so
    /// the flesh/fat/bone cross-section texture lines up. Shared vertices are kept so shading stays smooth.
    /// </summary>
    public static class MeshSlicer
    {
        sealed class Side
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();
            public readonly List<int> Cap = new List<int>();
            public readonly Dictionary<int, int> Map = new Dictionary<int, int>();

            public int Add(int src, Vector3[] v, Color[] c, Vector2[] uv)
            {
                if (Map.TryGetValue(src, out var i)) return i;
                V.Add(v[src]); C.Add(c.Length > 0 ? c[src] : Color.white); UV.Add(uv.Length > 0 ? uv[src] : Vector2.zero);
                i = V.Count - 1;
                Map[src] = i;
                return i;
            }

            public int AddNew(Vector3 p, Color c, Vector2 uv)
            {
                V.Add(p); C.Add(c); UV.Add(uv);
                return V.Count - 1;
            }
        }

        /// <summary>Returns false if the plane does not cross the mesh.</summary>
        public static bool Slice(Mesh mesh, Plane plane, out Mesh positive, out Mesh negative)
        {
            positive = negative = null;
            var v = mesh.vertices;
            var c = mesh.colors;
            var uv = mesh.uv;
            var tris = mesh.triangles;
            var dist = new float[v.Length];
            bool anyPos = false, anyNeg = false;
            for (int i = 0; i < v.Length; i++)
            {
                dist[i] = plane.GetDistanceToPoint(v[i]);
                if (dist[i] >= 0) anyPos = true; else anyNeg = true;
            }
            if (!anyPos || !anyNeg) return false;

            var pos = new Side();
            var neg = new Side();
            var cutPoints = new List<(Vector3 a, Vector3 b)>();
            var edgeCache = new Dictionary<long, (int pos, int neg, Vector3 p)>();

            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], d = tris[t + 2];
                bool pa = dist[a] >= 0, pb = dist[b] >= 0, pd = dist[d] >= 0;
                if (pa && pb && pd) { pos.T.Add(pos.Add(a, v, c, uv)); pos.T.Add(pos.Add(b, v, c, uv)); pos.T.Add(pos.Add(d, v, c, uv)); continue; }
                if (!pa && !pb && !pd) { neg.T.Add(neg.Add(a, v, c, uv)); neg.T.Add(neg.Add(b, v, c, uv)); neg.T.Add(neg.Add(d, v, c, uv)); continue; }

                // Rotate so 'lone' is the vertex alone on its side, keeping winding order.
                int lone, n1, n2;
                if (pa != pb && pa != pd) { lone = a; n1 = b; n2 = d; }
                else if (pb != pa && pb != pd) { lone = b; n1 = d; n2 = a; }
                else { lone = d; n1 = a; n2 = b; }
                bool lonePos = dist[lone] >= 0;
                var i1 = Intersect(lone, n1, v, c, uv, dist, pos, neg, edgeCache);
                var i2 = Intersect(lone, n2, v, c, uv, dist, pos, neg, edgeCache);
                var loneSide = lonePos ? pos : neg;
                var otherSide = lonePos ? neg : pos;
                int l1 = lonePos ? i1.pos : i1.neg, l2 = lonePos ? i2.pos : i2.neg;
                int o1 = lonePos ? i1.neg : i1.pos, o2 = lonePos ? i2.neg : i2.pos;
                // Lone side: one triangle (lone, i1, i2)
                loneSide.T.Add(loneSide.Add(lone, v, c, uv)); loneSide.T.Add(l1); loneSide.T.Add(l2);
                // Other side: quad (i1, n1, n2, i2) as two triangles
                int on1 = otherSide.Add(n1, v, c, uv), on2 = otherSide.Add(n2, v, c, uv);
                otherSide.T.Add(o1); otherSide.T.Add(on1); otherSide.T.Add(on2);
                otherSide.T.Add(o1); otherSide.T.Add(on2); otherSide.T.Add(o2);
                cutPoints.Add((i1.p, i2.p));
            }
            if (cutPoints.Count == 0) return false;

            // Caps: fan from the centroid of the cut outline.
            var centroid = Vector3.zero;
            foreach (var s in cutPoints) centroid += s.a + s.b;
            centroid /= cutPoints.Count * 2;
            float radius = 0.001f;
            foreach (var s in cutPoints) radius = Mathf.Max(radius, Vector3.Distance(s.a, centroid), Vector3.Distance(s.b, centroid));
            var n = plane.normal;
            var u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var w = Vector3.Cross(n, u);
            var flesh = Color.white;
            AddCap(pos, cutPoints, centroid, -n, u, w, radius, flesh);
            AddCap(neg, cutPoints, centroid, n, u, w, radius, flesh);

            positive = Build(pos, mesh.name + "_A");
            negative = Build(neg, mesh.name + "_B");
            return true;
        }

        static (int pos, int neg, Vector3 p) Intersect(int i, int j, Vector3[] v, Color[] c, Vector2[] uv, float[] dist, Side pos, Side neg, Dictionary<long, (int, int, Vector3)> cache)
        {
            long key = i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;
            if (cache.TryGetValue(key, out var hit)) return hit;
            float t = dist[i] / (dist[i] - dist[j]);
            var p = Vector3.Lerp(v[i], v[j], t);
            var col = c.Length > 0 ? Color.Lerp(c[i], c[j], t) : Color.white;
            var tex = uv.Length > 0 ? Vector2.Lerp(uv[i], uv[j], t) : Vector2.zero;
            var r = (pos.AddNew(p, col, tex), neg.AddNew(p, col, tex), p);
            cache[key] = r;
            return r;
        }

        static void AddCap(Side s, List<(Vector3 a, Vector3 b)> segs, Vector3 centroid, Vector3 outward, Vector3 u, Vector3 w, float radius, Color col)
        {
            Vector2 UV(Vector3 p) { var d = p - centroid; return new Vector2(0.5f + Vector3.Dot(d, u) / radius * 0.5f, 0.5f + Vector3.Dot(d, w) / radius * 0.5f); }
            int center = s.AddNew(centroid, col, new Vector2(0.5f, 0.5f));
            foreach (var seg in segs)
            {
                int a = s.AddNew(seg.a, col, UV(seg.a));
                int b = s.AddNew(seg.b, col, UV(seg.b));
                var nrm = Vector3.Cross(seg.a - centroid, seg.b - centroid);
                if (Vector3.Dot(nrm, outward) >= 0) { s.Cap.Add(center); s.Cap.Add(a); s.Cap.Add(b); }
                else { s.Cap.Add(center); s.Cap.Add(b); s.Cap.Add(a); }
            }
        }

        static Mesh Build(Side s, string name)
        {
            var m = new Mesh { name = name };
            if (s.V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(s.V);
            m.SetColors(s.C);
            m.SetUVs(0, s.UV);
            m.subMeshCount = 2;
            m.SetTriangles(s.T, 0);
            m.SetTriangles(s.Cap, 1);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
