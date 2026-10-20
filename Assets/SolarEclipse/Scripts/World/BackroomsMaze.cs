using System;
using System.Collections.Generic;

namespace SolarEclipse
{
    /// <summary>
    /// Pure-C# layout generator for the Backrooms (no UnityEngine, so it can be tested offline).
    /// The level is an N x N grid of square cells. Every edge between two cells is a solid wall, an open gap
    /// or a doorway (wall with a walk-through gap). Generation only ever opens edges of a random spanning tree
    /// plus extra loops and rooms, so every cell is reachable from every other cell. Pillars stand only on grid
    /// vertices whose four touching edges are all open, so they never cut a path.
    /// Deterministic for a given (size, seed): multiplayer clients build the same layout as the host.
    /// </summary>
    public sealed class BackroomsMaze
    {
        public const byte Solid = 0, Open = 1, Door = 2;

        public readonly int N;
        /// <summary>Edge between cell (x,z) and (x+1,z). Size (N-1) x N.</summary>
        public readonly byte[,] EdgeE;
        /// <summary>Edge between cell (x,z) and (x,z+1). Size N x (N-1).</summary>
        public readonly byte[,] EdgeN;
        /// <summary>Cell belongs to a carved room / open hall.</summary>
        public readonly bool[,] Room;
        /// <summary>Interior grid vertices (1..N-1) holding a pillar.</summary>
        public readonly List<(int x, int z)> Pillars = new List<(int x, int z)>();
        public readonly int SpawnX, SpawnZ;

        BackroomsMaze(int n)
        {
            N = n;
            EdgeE = new byte[n - 1, n];
            EdgeN = new byte[n, n - 1];
            Room = new bool[n, n];
            SpawnX = n / 2;
            SpawnZ = n / 2;
        }

        public static BackroomsMaze Generate(int n, int seed)
        {
            if (n < 4) n = 4;
            var m = new BackroomsMaze(n);
            var rnd = new Random(seed);

            // 1) Random spanning tree (Kruskal): guarantees full connectivity.
            var edges = new List<(bool east, int x, int z)>();
            for (int z = 0; z < n; z++) for (int x = 0; x < n - 1; x++) edges.Add((true, x, z));
            for (int z = 0; z < n - 1; z++) for (int x = 0; x < n; x++) edges.Add((false, x, z));
            for (int i = edges.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = edges[i]; edges[i] = edges[j]; edges[j] = t; }
            var parent = new int[n * n];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            var leftover = new List<(bool east, int x, int z)>();
            foreach (var e in edges)
            {
                int a = e.z * n + e.x, b = e.east ? a + 1 : a + n;
                int ra = Find(a), rb = Find(b);
                if (ra != rb) { parent[ra] = rb; m.Set(e.east, e.x, e.z, rnd.NextDouble() < 0.4 ? Door : Open); }
                else leftover.Add(e);
            }

            // 2) Extra openings so it loops instead of dead-ending everywhere.
            foreach (var e in leftover)
                if (rnd.NextDouble() < 0.16) m.Set(e.east, e.x, e.z, rnd.NextDouble() < 0.5 ? Door : Open);

            // 3) Rooms and big open halls (all internal walls removed).
            int rooms = Math.Max(3, n * n / 55);
            for (int i = 0; i < rooms; i++)
            {
                bool hall = i < Math.Max(1, rooms / 6);
                int w = hall ? 5 + rnd.Next(4) : 2 + rnd.Next(3), h = hall ? 5 + rnd.Next(4) : 2 + rnd.Next(3);
                int x0 = rnd.Next(Math.Max(1, n - w)), z0 = rnd.Next(Math.Max(1, n - h));
                m.Carve(x0, z0, Math.Min(n, x0 + w), Math.Min(n, z0 + h));
            }
            // Spawn room: always a 3x3 open room in the middle.
            m.Carve(m.SpawnX - 1, m.SpawnZ - 1, m.SpawnX + 2, m.SpawnZ + 2);

            // 4) Pillars on fully-open interior vertices (never on the spawn room's centre ring).
            for (int vz = 1; vz < n; vz++)
                for (int vx = 1; vx < n; vx++)
                {
                    if (m.EdgeE[vx - 1, vz - 1] != Open || m.EdgeE[vx - 1, vz] != Open || m.EdgeN[vx - 1, vz - 1] != Open || m.EdgeN[vx, vz - 1] != Open) continue;
                    bool nearSpawn = Math.Abs(vx - (m.SpawnX + 0.5)) < 1.6 && Math.Abs(vz - (m.SpawnZ + 0.5)) < 1.6;
                    if (nearSpawn) continue;
                    bool inRoom = m.Room[vx - 1, vz - 1] && m.Room[vx, vz] && m.Room[vx - 1, vz] && m.Room[vx, vz - 1];
                    if (rnd.NextDouble() < (inRoom ? 0.55 : 0.3)) m.Pillars.Add((vx, vz));
                }
            return m;
        }

        void Set(bool east, int x, int z, byte v) { if (east) EdgeE[x, z] = v; else EdgeN[x, z] = v; }

        void Carve(int x0, int z0, int x1, int z1)
        {
            x0 = Math.Max(0, x0); z0 = Math.Max(0, z0); x1 = Math.Min(N, x1); z1 = Math.Min(N, z1);
            for (int z = z0; z < z1; z++)
                for (int x = x0; x < x1; x++)
                {
                    Room[x, z] = true;
                    if (x + 1 < x1) EdgeE[x, z] = Open;
                    if (z + 1 < z1) EdgeN[x, z] = Open;
                }
        }

        public bool Passable(int x, int z, int dx, int dz)
        {
            int nx = x + dx, nz = z + dz;
            if (nx < 0 || nz < 0 || nx >= N || nz >= N) return false;
            if (dx == 1) return EdgeE[x, z] != Solid;
            if (dx == -1) return EdgeE[nx, z] != Solid;
            if (dz == 1) return EdgeN[x, z] != Solid;
            return EdgeN[x, nz] != Solid;
        }

        /// <summary>Breadth-first distance (in cells) from (sx,sz) to every cell; -1 = unreachable.</summary>
        public int[,] Distances(int sx, int sz)
        {
            var d = new int[N, N];
            for (int z = 0; z < N; z++) for (int x = 0; x < N; x++) d[x, z] = -1;
            var q = new Queue<(int, int)>();
            d[sx, sz] = 0; q.Enqueue((sx, sz));
            int[] dxs = { 1, -1, 0, 0 }, dzs = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                var (x, z) = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    if (!Passable(x, z, dxs[k], dzs[k])) continue;
                    int nx = x + dxs[k], nz = z + dzs[k];
                    if (d[nx, nz] >= 0) continue;
                    d[nx, nz] = d[x, z] + 1;
                    q.Enqueue((nx, nz));
                }
            }
            return d;
        }
    }
}
