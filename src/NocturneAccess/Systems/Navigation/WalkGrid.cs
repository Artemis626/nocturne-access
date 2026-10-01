using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NocturneAccess.Core;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>One straight run of a planned path, in one world direction (a multiple of 45 degrees).</summary>
    internal struct PathRun
    {
        public float Ax, Az, Bx, Bz;
        public int Dir;   // 0 north (+Z), 1 north-east, 2 east (+X), ... 7 north-west

        public float Bearing => Dir * 45f;
        public float Length => (float)Math.Sqrt((Bx - Ax) * (Bx - Ax) + (Bz - Az) * (Bz - Az));
    }

    /// <summary>
    /// Walkability grid of the current area and an 8-direction search over it (docs/PATHFINDING.md section 2.2).
    /// - Cells of CellSize units over the floor polygons at the player's height (horizontal, |normal.y| 0.5 or more,
    ///   Attr 0, plane within FloorBand of the player's Y). Horizontal overlays (Attr 0x800, 0x1000, 0x8000) reach past the
    ///   walls and are not floors (checked offline on the Healing Room dump, docs/PATHFINDING.md section 2.2).|normal.y| 0.5 or more,
    ///   plane within FloorBand of the player's Y; NPC boxes, Attr 0x80, are not floors).
    /// - A cell is walkable when its centre lies on such a floor and is at least Clearance units from every vertical
    ///   polygon overlapping the body (5 to 180 units above the player's Y; game Y points down): walls, closed doors,
    ///   furniture and NPC boxes. The game stops the player 60 units from a wall.
    /// - Search: Dijkstra from the player's cell (8 directions, or 4 in first person, where the game turns in 90-degree
    ///   steps; diagonal cost 1.414; no cutting past a blocked corner; a penalty per direction change; steps closer than
    ///   NearRange to a wall cost up to 1 + NearCost times more, so routes keep to the middle of corridors), then the reachable cell nearest the target, at least a minimum distance away.
    /// Rebuilt when the collision copy changes (CollisionCache.Version), the area changes or the player's floor moves.
    /// </summary>
    internal static class WalkGrid
    {
        internal const float CellSize = 30f;
        private const float Clearance = 65f;
        private const float FloorBand = 60f;
        private const float BodyLow = 5f, BodyHigh = 180f;
        private const int MaxCells = 400;          // per side; larger areas get a window around the player
        private const float TurnPenalty = 2f;
        private const float NearRange = 300f;      // cells closer than this to a wall cost more: routes keep to the middle
        private const float NearCost = 2f;
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };   // index = Dir: 0 north (+Z) clockwise
        private static readonly int[] Dz = { 1, 1, 0, -1, -1, -1, 0, 1 };

        private static int _version = -1, _field = int.MinValue, _area = int.MinValue;
        private static float _floorY = float.NaN;
        private static float _x0, _z0;              // centre of cell (0, 0)
        private static int _w, _h;
        private static byte[] _cell = Array.Empty<byte>();   // 0 no floor, 1 walkable, 2 blocked
        private static float[] _near = Array.Empty<float>();  // distance to the nearest wall, capped at NearRange
        private static readonly List<int> LastPath = new List<int>();
        private static bool[] _reach = Array.Empty<bool>();
        private static int _reachStart = -1, _reachBuild = -1, _builds;

        internal static int Width => _w;
        internal static int Height => _h;

        /// <summary>Builds the grid if the area, the collision copy or the floor height changed. Returns false if there is no floor.</summary>
        internal static bool Ensure(int field, int area, float px, float py, float pz)
        {
            CollisionCache.Refresh(field, area);
            if (_version == CollisionCache.Version && _field == field && _area == area && Math.Abs(py - _floorY) < 30 && Contains(px, pz))
                return _w > 0;
            Build(field, area, px, py, pz);
            return _w > 0;
        }

        private static void Build(int field, int area, float px, float py, float pz)
        {
            var clock = Stopwatch.StartNew();
            _version = CollisionCache.Version;
            _builds++;
            _field = field;
            _area = area;
            _floorY = py;
            LastPath.Clear();

            var floors = new List<CollisionPoly>();
            var walls = new List<(float ax, float az, float bx, float bz)>();
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (CollisionPoly p in CollisionCache.Polys)
            {
                if (p.Points.Length < 2)
                    continue;
                if (Math.Abs(p.Normal.y) >= 0.5f)
                {
                    if (p.Points.Length < 3 || p.Attr != 0 || Math.Abs(PlaneY(p, p.Points[0].x, p.Points[0].z) - py) > FloorBand)
                        continue;
                    floors.Add(p);
                    foreach (Vector4 v in p.Points)
                    {
                        minX = Math.Min(minX, v.x); maxX = Math.Max(maxX, v.x);
                        minZ = Math.Min(minZ, v.z); maxZ = Math.Max(maxZ, v.z);
                    }
                }
                else if (Math.Abs(p.Normal.y) < 0.2f)
                {
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (Vector4 v in p.Points)
                    {
                        lo = Math.Min(lo, v.y);
                        hi = Math.Max(hi, v.y);
                    }
                    // Y points down: the body spans py - BodyHigh .. py - BodyLow.
                    if (hi < py - BodyHigh || lo > py - BodyLow)
                        continue;
                    Footprint(p, out float ax, out float az, out float bx, out float bz);
                    walls.Add((ax, az, bx, bz));
                }
            }

            if (floors.Count == 0)
            {
                _w = _h = 0;
                _cell = Array.Empty<byte>();
                Log.Info($"[Nav:Walk] Grid: no floor at y {FieldData.F(py)} in field {field} area {area}.");
                return;
            }

            // Window of at most MaxCells per side around the player.
            float span = MaxCells * CellSize;
            minX = Math.Max(minX, px - span / 2); maxX = Math.Min(maxX, px + span / 2);
            minZ = Math.Max(minZ, pz - span / 2); maxZ = Math.Min(maxZ, pz + span / 2);
            _x0 = (float)Math.Floor(minX / CellSize) * CellSize + CellSize / 2;
            _z0 = (float)Math.Floor(minZ / CellSize) * CellSize + CellSize / 2;
            _w = Math.Min(MaxCells, (int)Math.Ceiling((maxX - _x0) / CellSize) + 1);
            _h = Math.Min(MaxCells, (int)Math.Ceiling((maxZ - _z0) / CellSize) + 1);
            _cell = new byte[_w * _h];
            _near = new float[_w * _h];
            for (int k = 0; k < _near.Length; k++)
                _near[k] = NearRange;

            foreach (CollisionPoly f in floors)
            {
                float fx0 = float.MaxValue, fz0 = float.MaxValue, fx1 = float.MinValue, fz1 = float.MinValue;
                foreach (Vector4 v in f.Points)
                {
                    fx0 = Math.Min(fx0, v.x); fx1 = Math.Max(fx1, v.x);
                    fz0 = Math.Min(fz0, v.z); fz1 = Math.Max(fz1, v.z);
                }
                ForCells(fx0, fz0, fx1, fz1, (i, j, x, z) =>
                {
                    if (_cell[j * _w + i] == 0 && CollisionCache.ContainsXZ(f.Points, x, z))
                        _cell[j * _w + i] = 1;
                });
            }

            foreach ((float ax, float az, float bx, float bz) in walls)
            {
                ForCells(Math.Min(ax, bx) - Clearance, Math.Min(az, bz) - Clearance, Math.Max(ax, bx) + Clearance, Math.Max(az, bz) + Clearance,
                    (i, j, x, z) =>
                    {
                        int k = j * _w + i;
                        if (_cell[k] == 1 && SegmentDistance(x, z, ax, az, bx, bz) < Clearance)
                            _cell[k] = 2;
                    });
                ForCells(Math.Min(ax, bx) - NearRange, Math.Min(az, bz) - NearRange, Math.Max(ax, bx) + NearRange, Math.Max(az, bz) + NearRange,
                    (i, j, x, z) =>
                    {
                        int k = j * _w + i;
                        float d = SegmentDistance(x, z, ax, az, bx, bz);
                        if (d < _near[k])
                            _near[k] = d;
                    });
            }

            int walkable = 0;
            foreach (byte c in _cell)
                if (c == 1)
                    walkable++;
            Log.Info($"[Nav:Walk] Grid for field {field} area {area}: {_w} x {_h} cells of {CellSize} from {FieldData.F(_x0)} {FieldData.F(_z0)}, " +
                     $"{floors.Count} floors, {walls.Count} walls, {walkable} walkable cells, {clock.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// A path from (px, pz) to the reachable walkable cell nearest (tx, tz) that is at least minDist from it.
        /// Returns null if no cell is reachable. The runs start at the player's cell.
        /// </summary>
        internal static List<PathRun> Plan(float px, float pz, float tx, float tz, float minDist, bool fourWay, out float goalX, out float goalZ)
        {
            goalX = goalZ = 0;
            LastPath.Clear();
            int start = NearestWalkable(px, pz, 6);
            if (start < 0)
                return null;

            int n = _w * _h;
            var dist = new float[n];
            var from = new int[n];
            var dir = new sbyte[n];
            for (int k = 0; k < n; k++)
            {
                dist[k] = float.MaxValue;
                from[k] = -1;
                dir[k] = -1;
            }
            var open = new PriorityQueue<int, float>();
            dist[start] = 0;
            open.Enqueue(start, 0);
            while (open.TryDequeue(out int cur, out float d))
            {
                if (d > dist[cur])
                    continue;
                int ci = cur % _w, cj = cur / _w;
                for (int s = 0; s < 8; s += fourWay ? 2 : 1)
                {
                    int ni = ci + Dx[s], nj = cj + Dz[s];
                    if (!Walkable(ni, nj))
                        continue;
                    bool diagonal = Dx[s] != 0 && Dz[s] != 0;
                    if (diagonal && (!Walkable(ci + Dx[s], cj) || !Walkable(ci, cj + Dz[s])))
                        continue;
                    float near = 1f - _near[nj * _w + ni] / NearRange;
                    float nd = d + (diagonal ? 1.4142f : 1f) * (1f + NearCost * near * near) + (dir[cur] >= 0 && dir[cur] != s ? TurnPenalty : 0f);
                    int nk = nj * _w + ni;
                    if (nd < dist[nk])
                    {
                        dist[nk] = nd;
                        from[nk] = cur;
                        dir[nk] = (sbyte)s;
                        open.Enqueue(nk, nd);
                    }
                }
            }

            int goal = -1;
            float best = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                if (dist[k] == float.MaxValue)
                    continue;
                float x = _x0 + (k % _w) * CellSize, z = _z0 + (k / _w) * CellSize;
                float e = (float)Math.Sqrt((x - tx) * (x - tx) + (z - tz) * (z - tz));
                if (e < minDist)
                    continue;
                // Nearest to the target first, then the shorter walk.
                float score = e + dist[k] * 0.01f;
                if (score < best)
                {
                    best = score;
                    goal = k;
                }
            }
            if (goal < 0)
                return null;

            for (int k = goal; k >= 0; k = from[k])
                LastPath.Add(k);
            LastPath.Reverse();
            goalX = _x0 + (goal % _w) * CellSize;
            goalZ = _z0 + (goal / _w) * CellSize;
            return Runs(LastPath, dir);
        }

        private static List<PathRun> Runs(List<int> cells, sbyte[] dir)
        {
            var runs = new List<PathRun>();
            for (int k = 1; k < cells.Count; k++)
            {
                int d = dir[cells[k]];
                float ax = _x0 + (cells[k - 1] % _w) * CellSize, az = _z0 + (cells[k - 1] / _w) * CellSize;
                float bx = _x0 + (cells[k] % _w) * CellSize, bz = _z0 + (cells[k] / _w) * CellSize;
                if (runs.Count > 0 && runs[runs.Count - 1].Dir == d)
                {
                    PathRun last = runs[runs.Count - 1];
                    last.Bx = bx;
                    last.Bz = bz;
                    runs[runs.Count - 1] = last;
                }
                else
                    runs.Add(new PathRun { Ax = ax, Az = az, Bx = bx, Bz = bz, Dir = d });
            }
            return runs;
        }

        /// <summary>The walkable cell nearest (x, z) within maxRing cells, or -1.</summary>
        private static int NearestWalkable(float x, float z, int maxRing)
        {
            int ci = (int)Math.Round((x - _x0) / CellSize), cj = (int)Math.Round((z - _z0) / CellSize);
            int best = -1;
            float bestD = float.MaxValue;
            for (int r = 0; r <= maxRing && best < 0; r++)
                for (int j = cj - r; j <= cj + r; j++)
                    for (int i = ci - r; i <= ci + r; i++)
                    {
                        if (Math.Max(Math.Abs(i - ci), Math.Abs(j - cj)) != r || !Walkable(i, j))
                            continue;
                        float cx = _x0 + i * CellSize - x, cz = _z0 + j * CellSize - z;
                        float d = cx * cx + cz * cz;
                        if (d < bestD)
                        {
                            bestD = d;
                            best = j * _w + i;
                        }
                    }
            return best;
        }

        /// <summary>
        /// Whether a walkable cell within radius of (x, z) can be reached from (px, pz): a flood over the grid from the
        /// player's cell (same moves as Plan), cached until the grid or the player's cell changes. Points of interest in
        /// other rooms of the same area (0.0.21 test: the item box and door 01d_01 west of the Healing Room) fail this.
        /// </summary>
        internal static bool ReachableNear(float px, float pz, float x, float z, float radius)
        {
            int start = NearestWalkable(px, pz, 6);
            if (start < 0)
                return false;
            if (start != _reachStart || _reach.Length != _cell.Length || _reachBuild != _builds)
                Flood(start);

            int r = (int)Math.Ceiling(radius / CellSize);
            int ci = (int)Math.Round((x - _x0) / CellSize), cj = (int)Math.Round((z - _z0) / CellSize);
            for (int j = cj - r; j <= cj + r; j++)
                for (int i = ci - r; i <= ci + r; i++)
                {
                    if (i < 0 || j < 0 || i >= _w || j >= _h || !_reach[j * _w + i])
                        continue;
                    float dx = _x0 + i * CellSize - x, dz = _z0 + j * CellSize - z;
                    if (dx * dx + dz * dz <= radius * radius)
                        return true;
                }
            return false;
        }

        private static void Flood(int start)
        {
            _reach = new bool[_cell.Length];
            _reachStart = start;
            _reachBuild = _builds;
            var queue = new Queue<int>();
            _reach[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int ci = cur % _w, cj = cur / _w;
                for (int s = 0; s < 8; s++)
                {
                    int ni = ci + Dx[s], nj = cj + Dz[s];
                    if (!Walkable(ni, nj) || _reach[nj * _w + ni])
                        continue;
                    if (Dx[s] != 0 && Dz[s] != 0 && (!Walkable(ci + Dx[s], cj) || !Walkable(ci, cj + Dz[s])))
                        continue;
                    _reach[nj * _w + ni] = true;
                    queue.Enqueue(nj * _w + ni);
                }
            }
        }

        private static bool Walkable(int i, int j) => i >= 0 && j >= 0 && i < _w && j < _h && _cell[j * _w + i] == 1;

        private static bool Contains(float x, float z)
            => _w > 0 && x >= _x0 - CellSize && z >= _z0 - CellSize && x <= _x0 + _w * CellSize && z <= _z0 + _h * CellSize;

        /// <summary>
        /// The grid as text, north at the top: '.' walkable, '#' blocked, ' ' no floor, '*' last planned path,
        /// '@' the player, plus the given marks (points of interest).
        /// </summary>
        internal static string AsciiMap(float px, float pz, IEnumerable<(float x, float z, char mark)> marks)
        {
            if (_w == 0)
                return "(no grid)";
            var rows = new char[_h][];
            for (int j = 0; j < _h; j++)
            {
                rows[j] = new char[_w];
                for (int i = 0; i < _w; i++)
                {
                    byte c = _cell[j * _w + i];
                    rows[j][i] = c == 1 ? '.' : c == 2 ? '#' : ' ';
                }
            }
            foreach (int k in LastPath)
                rows[k / _w][k % _w] = '*';
            foreach ((float x, float z, char mark) in marks)
                Mark(rows, x, z, mark);
            Mark(rows, px, pz, '@');

            var sb = new StringBuilder();
            sb.AppendLine($"grid {_w} x {_h}, cell {CellSize}, west edge x {FieldData.F(_x0)}, south edge z {FieldData.F(_z0)}; north at the top, east to the right");
            for (int j = _h - 1; j >= 0; j--)
                sb.AppendLine(new string(rows[j]).TrimEnd());
            return sb.ToString();
        }

        private static void Mark(char[][] rows, float x, float z, char mark)
        {
            int i = (int)Math.Round((x - _x0) / CellSize), j = (int)Math.Round((z - _z0) / CellSize);
            if (i >= 0 && j >= 0 && i < _w && j < _h)
                rows[j][i] = mark;
        }

        private static void ForCells(float x0, float z0, float x1, float z1, Action<int, int, float, float> action)
        {
            int i0 = Math.Max(0, (int)Math.Floor((x0 - _x0) / CellSize)), i1 = Math.Min(_w - 1, (int)Math.Ceiling((x1 - _x0) / CellSize));
            int j0 = Math.Max(0, (int)Math.Floor((z0 - _z0) / CellSize)), j1 = Math.Min(_h - 1, (int)Math.Ceiling((z1 - _z0) / CellSize));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    action(i, j, _x0 + i * CellSize, _z0 + j * CellSize);
        }

        private static float PlaneY(CollisionPoly p, float x, float z)
        {
            Vector4 a = p.Points[0];
            return a.y - (p.Normal.x * (x - a.x) + p.Normal.z * (z - a.z)) / p.Normal.y;
        }

        /// <summary>The two points farthest apart in X and Z (a vertical polygon's footprint), as in GridSense.</summary>
        private static void Footprint(CollisionPoly p, out float ax, out float az, out float bx, out float bz)
        {
            Vector4 a = p.Points[0], b = p.Points[0];
            float best = -1;
            for (int i = 0; i < p.Points.Length; i++)
                for (int j = i + 1; j < p.Points.Length; j++)
                {
                    float dx = p.Points[i].x - p.Points[j].x, dz = p.Points[i].z - p.Points[j].z;
                    float d = dx * dx + dz * dz;
                    if (d > best)
                    {
                        best = d;
                        a = p.Points[i];
                        b = p.Points[j];
                    }
                }
            ax = a.x; az = a.z; bx = b.x; bz = b.z;
        }

        private static float SegmentDistance(float px, float pz, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            float len2 = dx * dx + dz * dz;
            float t = len2 <= 0 ? 0 : Math.Clamp(((px - ax) * dx + (pz - az) * dz) / len2, 0f, 1f);
            float cx = ax + t * dx - px, cz = az + t * dz - pz;
            return (float)Math.Sqrt(cx * cx + cz * cz);
        }
    }
}
