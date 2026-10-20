using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarEclipse
{
    /// <summary>
    /// Builds procedural meshes from primitives. Vertex colour = albedo, vertex alpha = smoothness (see SolarEclipse/Lit).
    /// Winding is fixed automatically from an "outward" hint, so primitives never render inside-out.
    /// </summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>(1024);
        public readonly List<Color> C = new List<Color>(1024);
        public readonly List<Vector2> UV = new List<Vector2>(1024);
        public readonly List<int> T = new List<int>(2048);

        public Color Color = Color.white;
        public float Gloss = 0.2f;

        Matrix4x4 matrix = Matrix4x4.identity;
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();

        public int VertexCount => V.Count;

        public MeshBuilder SetColor(Color c, float gloss = -1f)
        {
            Color = c;
            if (gloss >= 0f) Gloss = gloss;
            return this;
        }

        public void Push(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            stack.Push(matrix);
            matrix = matrix * Matrix4x4.TRS(position, rotation, scale);
        }

        public void Push(Vector3 position, Quaternion rotation) => Push(position, rotation, Vector3.one);
        public void Pop() => matrix = stack.Count > 0 ? stack.Pop() : Matrix4x4.identity;

        public int Vert(Vector3 p, Vector2 uv)
        {
            V.Add(matrix.MultiplyPoint3x4(p));
            var c = Color;
            c.a = Gloss;
            C.Add(ToMeshColor(c));
            UV.Add(uv);
            return V.Count - 1;
        }

        public int Vert(Vector3 p) => Vert(p, Vector2.zero);

        static Color ToMeshColor(Color c)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                var l = c.linear;
                l.a = c.a;
                return l;
            }
            return c;
        }

        /// <summary>Adds a triangle facing <paramref name="outward"/> (local space).</summary>
        public void Tri(int a, int b, int c, Vector3 outwardWorld)
        {
            var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (Vector3.Dot(n, outwardWorld) >= 0f) { T.Add(a); T.Add(b); T.Add(c); }
            else { T.Add(a); T.Add(c); T.Add(b); }
        }

        public void Quad(int a, int b, int c, int d, Vector3 outwardWorld)
        {
            Tri(a, b, c, outwardWorld);
            Tri(a, c, d, outwardWorld);
        }

        Vector3 Dir(Vector3 local) => matrix.MultiplyVector(local);

        // ---------------------------------------------------------------- primitives

        /// <summary>Flat-shaded box. UVs are scaled by size (world tiling) times <paramref name="uvScale"/>.</summary>
        public void Box(Vector3 center, Vector3 size, float uvScale = 1f)
        {
            var h = size * 0.5f;
            Face(center + new Vector3(0, 0, h.z), Vector3.forward, Vector3.up, h.x, h.y, size.x, size.y, uvScale);
            Face(center - new Vector3(0, 0, h.z), Vector3.back, Vector3.up, h.x, h.y, size.x, size.y, uvScale);
            Face(center + new Vector3(h.x, 0, 0), Vector3.right, Vector3.up, h.z, h.y, size.z, size.y, uvScale);
            Face(center - new Vector3(h.x, 0, 0), Vector3.left, Vector3.up, h.z, h.y, size.z, size.y, uvScale);
            Face(center + new Vector3(0, h.y, 0), Vector3.up, Vector3.forward, h.x, h.z, size.x, size.z, uvScale);
            Face(center - new Vector3(0, h.y, 0), Vector3.down, Vector3.forward, h.x, h.z, size.x, size.z, uvScale);
        }

        /// <summary>Box with chamfered edges and flat-shaded facets (reads far less "Minecraft").</summary>
        public void BevelBox(Vector3 center, Vector3 size, float bevel, float uvScale = 1f)
        {
            bevel = Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f);
            if (bevel <= 0.0005f) { Box(center, size, uvScale); return; }
            var inner = size * 0.5f - Vector3.one * bevel;
            float[] steps = { -1f, -0.995f, 0.995f, 1f };
            var pts = new Vector3[4, 4];
            for (int face = 0; face < 6; face++)
            {
                FaceAxes(face, out var n, out var u, out var v);
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                        pts[i, j] = RoundedBoxPoint(n + u * steps[i] + v * steps[j], inner, bevel);
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        var p0 = pts[i, j]; var p1 = pts[i + 1, j]; var p2 = pts[i + 1, j + 1]; var p3 = pts[i, j + 1];
                        var faceN = Vector3.Cross(p1 - p0, p2 - p0);
                        if (faceN.sqrMagnitude < 1e-12f) faceN = Vector3.Cross(p2 - p0, p3 - p0);
                        if (faceN.sqrMagnitude < 1e-12f) continue;
                        var outward = ((p0 + p2) * 0.5f);
                        int a = Vert(center + p0, new Vector2(Vector3.Dot(p0, u), Vector3.Dot(p0, v)) * uvScale);
                        int b = Vert(center + p1, new Vector2(Vector3.Dot(p1, u), Vector3.Dot(p1, v)) * uvScale);
                        int c = Vert(center + p2, new Vector2(Vector3.Dot(p2, u), Vector3.Dot(p2, v)) * uvScale);
                        int d = Vert(center + p3, new Vector2(Vector3.Dot(p3, u), Vector3.Dot(p3, v)) * uvScale);
                        Quad(a, b, c, d, Dir(outward));
                    }
            }
        }

        static Vector3 RoundedBoxPoint(Vector3 dir, Vector3 inner, float r)
        {
            // Project a cube direction onto a rounded box: clamp to inner box, then push out by r along the residual.
            var p = new Vector3(Mathf.Clamp(dir.x, -1, 1), Mathf.Clamp(dir.y, -1, 1), Mathf.Clamp(dir.z, -1, 1));
            var onInner = Vector3.Scale(p, inner);
            var residual = new Vector3(
                Mathf.Abs(p.x) >= 0.999f ? Mathf.Sign(p.x) : 0f,
                Mathf.Abs(p.y) >= 0.999f ? Mathf.Sign(p.y) : 0f,
                Mathf.Abs(p.z) >= 0.999f ? Mathf.Sign(p.z) : 0f);
            if (residual == Vector3.zero) residual = p;
            return onInner + residual.normalized * r;
        }

        static void FaceAxes(int face, out Vector3 n, out Vector3 u, out Vector3 v)
        {
            switch (face)
            {
                case 0: n = Vector3.right; u = Vector3.forward; v = Vector3.up; break;
                case 1: n = Vector3.left; u = Vector3.forward; v = Vector3.up; break;
                case 2: n = Vector3.up; u = Vector3.right; v = Vector3.forward; break;
                case 3: n = Vector3.down; u = Vector3.right; v = Vector3.forward; break;
                case 4: n = Vector3.forward; u = Vector3.right; v = Vector3.up; break;
                default: n = Vector3.back; u = Vector3.right; v = Vector3.up; break;
            }
        }

        /// <summary>A single quad facing <paramref name="normal"/>.</summary>
        public void Face(Vector3 center, Vector3 normal, Vector3 up, float halfW, float halfH, float uvW = 1f, float uvH = 1f, float uvScale = 1f)
        {
            var right = Vector3.Cross(up, -normal).normalized;
            int a = Vert(center - right * halfW + up * halfH, new Vector2(0, uvH) * uvScale);
            int b = Vert(center + right * halfW + up * halfH, new Vector2(uvW, uvH) * uvScale);
            int c = Vert(center + right * halfW - up * halfH, new Vector2(uvW, 0) * uvScale);
            int d = Vert(center - right * halfW - up * halfH, Vector2.zero);
            Quad(a, b, c, d, Dir(normal));
        }

        /// <summary>
        /// Generic loft: rings of points around an axis. ring(i) returns (center, radiusX, radiusZ) in local space,
        /// rings are perpendicular to <paramref name="axis"/>. Smooth shaded, optional caps.
        /// </summary>
        public void Loft(int rings, int segments, Func<int, (Vector3 center, float rx, float rz)> ring, Vector3 axis, bool capStart = true, bool capEnd = true, Func<int, int, float> radiusScale = null, Func<int, Color> ringColor = null, Func<int, int, Vector3> ringOffset = null)
        {
            axis = axis.normalized;
            var side = Mathf.Abs(Vector3.Dot(axis, Vector3.forward)) > 0.9f ? Vector3.right : Vector3.forward;
            var X = Vector3.Cross(axis, side).normalized;
            var Z = Vector3.Cross(X, axis).normalized;
            if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f) { X = Vector3.right; Z = Vector3.forward; }

            int baseIndex = V.Count;
            var centers = new Vector3[rings];
            for (int i = 0; i < rings; i++)
            {
                var (c, rx, rz) = ring(i);
                centers[i] = c;
                if (ringColor != null) Color = ringColor(i);
                for (int j = 0; j < segments; j++)
                {
                    float t = j / (float)segments * Mathf.PI * 2f;
                    float s = radiusScale != null ? radiusScale(i, j) : 1f;
                    var p = c + (X * Mathf.Cos(t) * rx + Z * Mathf.Sin(t) * rz) * s;
                    if (ringOffset != null) p += ringOffset(i, j);
                    Vert(p, new Vector2(j / (float)segments, i / (float)(rings - 1)));
                }
            }
            for (int i = 0; i < rings - 1; i++)
                for (int j = 0; j < segments; j++)
                {
                    int j2 = (j + 1) % segments;
                    int a = baseIndex + i * segments + j, b = baseIndex + i * segments + j2;
                    int c = baseIndex + (i + 1) * segments + j2, d = baseIndex + (i + 1) * segments + j;
                    var mid = (V[a] + V[c]) * 0.5f;
                    var centerW = matrix.MultiplyPoint3x4((centers[i] + centers[i + 1]) * 0.5f);
                    Quad(a, b, c, d, mid - centerW);
                }
            if (capStart) Cap(baseIndex, segments, centers[0], centers.Length > 1 ? centers[0] - centers[1] : -axis);
            if (capEnd) Cap(baseIndex + (rings - 1) * segments, segments, centers[rings - 1], centers.Length > 1 ? centers[rings - 1] - centers[rings - 2] : axis);
        }

        void Cap(int ringStart, int segments, Vector3 centerLocal, Vector3 outwardLocal)
        {
            int center = Vert(centerLocal, new Vector2(0.5f, 0.5f));
            var outward = Dir(outwardLocal);
            for (int j = 0; j < segments; j++)
                Tri(center, ringStart + j, ringStart + (j + 1) % segments, outward);
        }

        /// <summary>Tapered cylinder from a to b.</summary>
        public void Cylinder(Vector3 a, Vector3 b, float ra, float rb, int segments = 12, bool caps = true)
        {
            var axis = b - a;
            Loft(2, segments, i => (i == 0 ? a : b, i == 0 ? ra : rb, i == 0 ? ra : rb), axis, caps, caps);
        }

        /// <summary>Capsule-like limb: hemispherical-ish ends, radius tapering from ra to rb.</summary>
        public void Capsule(Vector3 a, Vector3 b, float ra, float rb, int segments = 14, int capRings = 4)
        {
            var axis = (b - a);
            float len = axis.magnitude;
            var dir = axis / Mathf.Max(len, 1e-5f);
            int rings = capRings * 2 + 2;
            Loft(rings, segments, i =>
            {
                if (i < capRings + 1)
                {
                    float t = i / (float)capRings; // 0..1
                    float ang = (1f - t) * Mathf.PI * 0.5f;
                    float r = ra * Mathf.Cos(ang);
                    return (a - dir * ra * Mathf.Sin(ang), Mathf.Max(r, 0.0005f), Mathf.Max(r, 0.0005f));
                }
                else
                {
                    float t = (i - capRings - 1) / (float)capRings;
                    float ang = t * Mathf.PI * 0.5f;
                    float r = rb * Mathf.Cos(ang);
                    return (b + dir * rb * Mathf.Sin(ang), Mathf.Max(r, 0.0005f), Mathf.Max(r, 0.0005f));
                }
            }, dir, true, true);
        }

        /// <summary>UV sphere / ellipsoid. <paramref name="deform"/> may move each unit-sphere point.</summary>
        public void Sphere(Vector3 center, Vector3 radii, int lat = 12, int lon = 16, Func<Vector3, Vector3> deform = null)
        {
            int baseIndex = V.Count;
            for (int i = 0; i <= lat; i++)
            {
                float phi = Mathf.PI * i / lat; // 0 top .. PI bottom
                for (int j = 0; j < lon; j++)
                {
                    float th = Mathf.PI * 2f * j / lon;
                    var unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    var p = Vector3.Scale(unit, radii);
                    if (deform != null) p = deform(p);
                    Vert(center + p, new Vector2(j / (float)lon, 1f - i / (float)lat));
                }
            }
            var c = matrix.MultiplyPoint3x4(center);
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int j2 = (j + 1) % lon;
                    int a = baseIndex + i * lon + j, b = baseIndex + i * lon + j2;
                    int d = baseIndex + (i + 1) * lon + j, e = baseIndex + (i + 1) * lon + j2;
                    var mid = (V[a] + V[e]) * 0.5f;
                    if (i != 0) Tri(a, b, e, mid - c);
                    if (i != lat - 1) Tri(a, e, d, mid - c);
                }
        }

        public void Torus(Vector3 center, float major, float minor, int segMajor = 16, int segMinor = 8, Vector3? axisOpt = null)
        {
            var axis = axisOpt ?? Vector3.up;
            var rot = Quaternion.FromToRotation(Vector3.up, axis);
            int baseIndex = V.Count;
            for (int i = 0; i < segMajor; i++)
            {
                float a = Mathf.PI * 2f * i / segMajor;
                var ringCenter = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * major;
                for (int j = 0; j < segMinor; j++)
                {
                    float b = Mathf.PI * 2f * j / segMinor;
                    var n = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                    Vert(center + rot * (ringCenter + n * minor));
                }
            }
            for (int i = 0; i < segMajor; i++)
                for (int j = 0; j < segMinor; j++)
                {
                    int i2 = (i + 1) % segMajor, j2 = (j + 1) % segMinor;
                    int a = baseIndex + i * segMinor + j, b = baseIndex + i2 * segMinor + j;
                    int c = baseIndex + i2 * segMinor + j2, d = baseIndex + i * segMinor + j2;
                    float ang = Mathf.PI * 2f * (i + 0.5f) / segMajor;
                    var rc = matrix.MultiplyPoint3x4(center + rot * (new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * major));
                    Quad(a, b, c, d, (V[a] + V[c]) * 0.5f - rc);
                }
        }

        /// <summary>Flat polygon prism (extruded outline in XY, depth along Z). Outline must be convex.</summary>
        public void Prism(Vector2[] outline, float depth, Vector3 offset)
        {
            float hz = depth * 0.5f;
            var front = new int[outline.Length];
            var back = new int[outline.Length];
            Vector2 centroid = Vector2.zero;
            foreach (var p in outline) centroid += p;
            centroid /= outline.Length;
            // Caps (own vertices for crisp edges)
            int cf = Vert(offset + new Vector3(centroid.x, centroid.y, hz));
            int cb = Vert(offset + new Vector3(centroid.x, centroid.y, -hz));
            for (int i = 0; i < outline.Length; i++)
            {
                front[i] = Vert(offset + new Vector3(outline[i].x, outline[i].y, hz));
                back[i] = Vert(offset + new Vector3(outline[i].x, outline[i].y, -hz));
            }
            for (int i = 0; i < outline.Length; i++)
            {
                int n = (i + 1) % outline.Length;
                Tri(cf, front[i], front[n], Dir(Vector3.forward));
                Tri(cb, back[i], back[n], Dir(Vector3.back));
            }
            for (int i = 0; i < outline.Length; i++)
            {
                int n = (i + 1) % outline.Length;
                var e = outline[n] - outline[i];
                var normal2 = new Vector2(e.y, -e.x);
                if (Vector2.Dot(normal2, (outline[i] + outline[n]) * 0.5f - centroid) < 0) normal2 = -normal2;
                int a = Vert(offset + new Vector3(outline[i].x, outline[i].y, hz));
                int b = Vert(offset + new Vector3(outline[n].x, outline[n].y, hz));
                int c = Vert(offset + new Vector3(outline[n].x, outline[n].y, -hz));
                int d = Vert(offset + new Vector3(outline[i].x, outline[i].y, -hz));
                Quad(a, b, c, d, Dir(new Vector3(normal2.x, normal2.y, 0)));
            }
        }

        public void Append(MeshBuilder other)
        {
            int offset = V.Count;
            foreach (var v in other.V) V.Add(matrix.MultiplyPoint3x4(v));
            C.AddRange(other.C);
            UV.AddRange(other.UV);
            foreach (var t in other.T) T.Add(t + offset);
        }

        public Mesh ToMesh(string name = "Procedural", bool recalcNormals = true)
        {
            var mesh = new Mesh { name = name };
            if (V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(V);
            mesh.SetColors(C);
            mesh.SetUVs(0, UV);
            mesh.SetTriangles(T, 0);
            if (recalcNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public Bounds LocalBounds()
        {
            if (V.Count == 0) return new Bounds();
            var b = new Bounds(V[0], Vector3.zero);
            foreach (var v in V) b.Encapsulate(v);
            return b;
        }
    }
}
