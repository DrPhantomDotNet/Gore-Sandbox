using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SolarEclipse
{
    /// <summary>
    /// Walkability grid sampled from the level colliders, with A* pathfinding, random wander points,
    /// cover search and flee-point search for the Goredoll AI.
    /// </summary>
    public sealed class NavGrid
    {
        public static NavGrid Current;
        public readonly float Cell;
        public readonly Vector3 Origin;
        public readonly int W, H;
        readonly float[] heights;
        public int WalkableCount { get; private set; }
        public bool Ready { get; private set; }

        public NavGrid(Bounds area, float cell)
        {
            Cell = cell;
            Origin = new Vector3(area.min.x, 0, area.min.z);
            W = Mathf.Max(1, Mathf.CeilToInt(area.size.x / cell));
            H = Mathf.Max(1, Mathf.CeilToInt(area.size.z / cell));
            heights = new float[W * H];
            for (int i = 0; i < heights.Length; i++) heights[i] = float.NaN;
        }

        public IEnumerator Build(float probeTop, float probeBottom, Action<float> progress)
        {
            int obstacleMask = Layers.Mask(Layers.Default, Layers.Boundaries);
            int count = 0;
            for (int z = 0; z < H; z++)
            {
                for (int x = 0; x < W; x++)
                {
                    var top = new Vector3(Origin.x + (x + 0.5f) * Cell, probeTop, Origin.z + (z + 0.5f) * Cell);
                    if (Physics.Raycast(top, Vector3.down, out var hit, probeTop - probeBottom, Layers.Mask(Layers.Default), QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f)
                    {
                        var p = hit.point;
                        if (!Physics.CheckCapsule(p + Vector3.up * 0.55f, p + Vector3.up * 1.6f, 0.28f, obstacleMask, QueryTriggerInteraction.Ignore))
                        {
                            heights[z * W + x] = p.y;
                            WalkableCount++;
                        }
                    }
                    if (++count % 3000 == 0) { progress?.Invoke(count / (float)(W * H)); yield return null; }
                }
            }
            Ready = true;
            progress?.Invoke(1f);
        }

        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < W && z < H;
        public bool Walkable(int x, int z) => InBounds(x, z) && !float.IsNaN(heights[z * W + x]);
        public Vector3 CellCenter(int x, int z) => new Vector3(Origin.x + (x + 0.5f) * Cell, heights[z * W + x], Origin.z + (z + 0.5f) * Cell);

        public void ToCell(Vector3 p, out int x, out int z)
        {
            x = Mathf.FloorToInt((p.x - Origin.x) / Cell);
            z = Mathf.FloorToInt((p.z - Origin.z) / Cell);
        }

        public bool Nearest(Vector3 p, out int cx, out int cz, int maxRadius = 6)
        {
            ToCell(p, out cx, out cz);
            if (Walkable(cx, cz)) return true;
            for (int r = 1; r <= maxRadius; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        if (Walkable(cx + dx, cz + dz)) { cx += dx; cz += dz; return true; }
                    }
            return false;
        }

        public bool IsWalkable(Vector3 p) { ToCell(p, out var x, out var z); return Walkable(x, z); }

        // ------------------------------------------------------------------ A*

        readonly Dictionary<int, float> gScore = new Dictionary<int, float>();
        readonly Dictionary<int, int> cameFrom = new Dictionary<int, int>();
        readonly List<(float f, int id)> open = new List<(float, int)>();
        readonly HashSet<int> closed = new HashSet<int>();

        public bool FindPath(Vector3 from, Vector3 to, List<Vector3> path, int maxIterations = 6000)
        {
            path.Clear();
            if (!Ready || !Nearest(from, out var sx, out var sz) || !Nearest(to, out var tx, out var tz)) return false;
            int start = sz * W + sx, goal = tz * W + tx;
            gScore.Clear(); cameFrom.Clear(); open.Clear(); closed.Clear();
            gScore[start] = 0;
            Push(Heuristic(sx, sz, tx, tz), start);
            int iterations = 0, best = start;
            float bestH = float.MaxValue;
            while (open.Count > 0 && iterations++ < maxIterations)
            {
                int current = Pop();
                if (current == goal) { best = goal; break; }
                if (!closed.Add(current)) continue;
                int cx = current % W, cz = current / W;
                float h = Heuristic(cx, cz, tx, tz);
                if (h < bestH) { bestH = h; best = current; }
                float ch = heights[current];
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = cx + dx, nz = cz + dz;
                        if (!Walkable(nx, nz)) continue;
                        if (dx != 0 && dz != 0 && (!Walkable(cx + dx, cz) || !Walkable(cx, cz + dz))) continue;
                        int nid = nz * W + nx;
                        if (Mathf.Abs(heights[nid] - ch) > 0.5f) continue;
                        float step = (dx != 0 && dz != 0 ? 1.4142f : 1f);
                        float g = gScore[current] + step;
                        if (gScore.TryGetValue(nid, out var old) && old <= g) continue;
                        gScore[nid] = g;
                        cameFrom[nid] = current;
                        Push(g + Heuristic(nx, nz, tx, tz), nid);
                    }
            }
            // Reconstruct to the goal, or to the closest point reached.
            var cells = new List<int>();
            int c = best;
            cells.Add(c);
            while (cameFrom.TryGetValue(c, out var prev)) { c = prev; cells.Add(c); }
            cells.Reverse();
            foreach (var id in cells) path.Add(CellCenter(id % W, id / W));
            Smooth(path);
            return path.Count > 0;
        }

        static float Heuristic(int ax, int az, int bx, int bz)
        {
            int dx = Mathf.Abs(ax - bx), dz = Mathf.Abs(az - bz);
            return (dx + dz) + (1.4142f - 2f) * Mathf.Min(dx, dz);
        }

        void Push(float f, int id)
        {
            open.Add((f, id));
            int i = open.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (open[parent].f <= open[i].f) break;
                (open[parent], open[i]) = (open[i], open[parent]);
                i = parent;
            }
        }

        int Pop()
        {
            var top = open[0].id;
            open[0] = open[open.Count - 1];
            open.RemoveAt(open.Count - 1);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, s = i;
                if (l < open.Count && open[l].f < open[s].f) s = l;
                if (r < open.Count && open[r].f < open[s].f) s = r;
                if (s == i) break;
                (open[s], open[i]) = (open[i], open[s]);
                i = s;
            }
            return top;
        }

        /// <summary>Removes waypoints that have direct line of walk.</summary>
        void Smooth(List<Vector3> path)
        {
            if (path.Count < 3) return;
            var result = new List<Vector3> { path[0] };
            int anchor = 0;
            for (int i = 2; i < path.Count; i++)
            {
                if (!ClearWalk(path[anchor], path[i]))
                {
                    result.Add(path[i - 1]);
                    anchor = i - 1;
                }
            }
            result.Add(path[path.Count - 1]);
            path.Clear();
            path.AddRange(result);
        }

        bool ClearWalk(Vector3 a, Vector3 b)
        {
            float d = Vector3.Distance(a, b);
            int steps = Mathf.CeilToInt(d / (Cell * 0.5f));
            float lastH = a.y;
            for (int i = 1; i <= steps; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)steps);
                ToCell(p, out var x, out var z);
                if (!Walkable(x, z)) return false;
                float h = heights[z * W + x];
                if (Mathf.Abs(h - lastH) > 0.5f) return false;
                lastH = h;
            }
            return true;
        }

        // ------------------------------------------------------------------ queries for AI

        public bool RandomPoint(Vector3 near, float radius, out Vector3 point)
        {
            for (int i = 0; i < 30; i++)
            {
                var p = near + new Vector3(UnityEngine.Random.Range(-radius, radius), 0, UnityEngine.Random.Range(-radius, radius));
                ToCell(p, out var x, out var z);
                if (Walkable(x, z)) { point = CellCenter(x, z); return true; }
            }
            point = near;
            return false;
        }

        /// <summary>A reachable cell near <paramref name="near"/> that the threat cannot see.</summary>
        public bool FindCover(Vector3 threatEye, Vector3 near, float radius, out Vector3 cover)
        {
            cover = near;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < 40; i++)
            {
                var p = near + new Vector3(UnityEngine.Random.Range(-radius, radius), 0, UnityEngine.Random.Range(-radius, radius));
                ToCell(p, out var x, out var z);
                if (!Walkable(x, z)) continue;
                var c = CellCenter(x, z);
                if (!Physics.Linecast(threatEye, c + Vector3.up * 1.1f, Layers.SightMask, QueryTriggerInteraction.Ignore)) continue;
                float score = Vector3.Distance(near, c) - Vector3.Distance(threatEye, c) * 0.3f;
                if (score < best) { best = score; cover = c; found = true; }
            }
            return found;
        }

        /// <summary>A reachable point that increases distance from the threat.</summary>
        public bool FleePoint(Vector3 threat, Vector3 from, float distance, out Vector3 point)
        {
            point = from;
            float best = -1;
            var away = MathX.Flat(from - threat).normalized;
            if (away.sqrMagnitude < 0.01f) away = UnityEngine.Random.onUnitSphere;
            for (int i = 0; i < 24; i++)
            {
                var dir = Quaternion.Euler(0, UnityEngine.Random.Range(-75f, 75f), 0) * away;
                var p = from + dir * UnityEngine.Random.Range(distance * 0.5f, distance);
                if (!Nearest(p, out var x, out var z, 3)) continue;
                var c = CellCenter(x, z);
                float score = Vector3.Distance(c, threat);
                if (score > best) { best = score; point = c; }
            }
            return best > 0;
        }
    }
}
