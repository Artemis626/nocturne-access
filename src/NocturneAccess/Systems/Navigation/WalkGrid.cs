using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppfield_H;
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
    /// Walkability grid of the current area, on several levels, and a search over it (docs/PATHFINDING.md sections 2.2, 6).
    /// - Cells of CellSize units in X and Z. Each cell has up to Layers floor heights: the floor polygons (horizontal,
    ///   |normal.y| 0.5 or more, Attr 0; overlays with Attr 0x800 / 0x1000 / 0x8000 reach past walls and are not floors)
    ///   whose outline contains the cell centre, at their plane height there. One cell can hold a ground floor and a
    ///   landing above it (the stairwell of the Medical Center, field 14 area 14, has its 2F door right above the 1F door).
    /// - A node (cell, layer) is walkable unless a vertical polygon that overlaps the body above that floor (StepHeight to
    ///   BodyHigh units up; game Y points down) lies closer than Clearance: walls, closed doors, furniture, NPC boxes.
    ///   Stair steps lower than StepHeight do not block. The game stops the player 60 units from a wall.
    /// - Moves join neighbouring nodes whose heights differ by MaxStep or less (stairs, ramps), 8 directions or 4 in first
    ///   person (the game turns in 90-degree steps), no cutting past a blocked corner. Cost: 1 per cell (1.414 diagonal),
    ///   up to 1 + NearCost times more within NearRange of a wall (routes keep to the middle), plus TurnPenalty per turn.
    /// - The goal is the reachable node nearest the target in X and Z, at least minDist away and within TargetBand of
    ///   the target's height.
    /// Rebuilt when the collision copy changes (CollisionCache.Version), the area changes or the player leaves the window.
    /// </summary>
    internal static class WalkGrid
    {
        internal const float CellSize = 30f;
        private const int Layers = 3;
        private const float Clearance = 65f;
        private const float StepHeight = 30f, BodyHigh = 180f;
        private const float MaxStep = 45f;
        private const float MergeBand = 20f;
        private const float TargetBand = 200f;
        private const float StartBand = 80f;
        private const int MaxCells = 400;          // per side; larger areas get a window around the player
        private const float TurnPenalty = 2f;
        private const float NearRange = 300f;
        private const float NearCost = 2f;
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };   // index = Dir: 0 north (+Z) clockwise
        private static readonly int[] Dz = { 1, 1, 0, -1, -1, -1, 0, 1 };

        private static int _version = -1, _field = int.MinValue, _area = int.MinValue;
        private static float _x0, _z0;                         // centre of cell (0, 0)
        private static int _w, _h;
        private static byte[] _count = Array.Empty<byte>();     // floor layers per cell
        private static float[] _height = Array.Empty<float>();  // per node (cell * Layers + layer)
        private static byte[] _state = Array.Empty<byte>();     // per node: 1 walkable, 2 blocked
        private static float[] _near = Array.Empty<float>();    // per node: distance to the nearest wall, capped
        private static readonly List<int> LastPath = new List<int>();
        private static float[] _dist = Array.Empty<float>();      // Plan's working arrays, reused
        private static int[] _from = Array.Empty<int>();
        private static sbyte[] _dir = Array.Empty<sbyte>();
        private static readonly PriorityQueue<int, float> Open = new PriorityQueue<int, float>();
        private static bool[] _reach = Array.Empty<bool>();
        private static int _reachStart = -1, _reachBuild = -1, _builds;
        private static readonly List<(float x, float z)> HiddenNpcs = new List<(float x, float z)>();
        private static int _hiddenKey;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _hiddenReadMs = long.MinValue / 2;
        private const long HiddenNpcPollMs = 500;   // the per-frame path (Lattice) reads the NPC table at most this often
        private static int _latticeBuild = -1;
        private static float _latticeCell, _latticeX, _latticeZ;
        private const float NpcBoxReach = 80f;   // an NPC box is 100 x 100 around the NPC's position

        internal static int Width => _w;
        internal static int Height => _h;

        /// <summary>Builds the grid if the area or the collision copy changed, or the player left it. False if there is no floor.</summary>
        internal static bool Ensure(int field, int area, float px, float py, float pz) => Ensure(field, area, px, py, pz, everyFrame: false);

        /// <param name="everyFrame">
        /// Called on every field update (blobber mode, 30 a second): the hidden NPCs, read through the game's NPC table, are
        /// checked at most every HiddenNpcPollMs, so someone the story shows or hides reaches the grid up to that much later
        /// (Francesco, 2 Oct 2026). A change of area, collision or window rebuilds at once, with a fresh read.
        /// </param>
        private static bool Ensure(int field, int area, float px, float py, float pz, bool everyFrame)
        {
            CollisionCache.Refresh(field, area);
            bool same = _version == CollisionCache.Version && _field == field && _area == area && Contains(px, pz);
            long now = Clock.ElapsedMilliseconds;
            if (same && everyFrame && now - _hiddenReadMs < HiddenNpcPollMs)
                return _w > 0;
            _hiddenReadMs = now;
            int hidden = ReadHiddenNpcs();
            if (same && _hiddenKey == hidden)
                return _w > 0;
            _hiddenKey = hidden;
            Build(field, area, px, py, pz);
            return _w > 0;
        }

        /// <summary>
        /// Positions of the NPCs whose object is inactive (PointsOfInterest.Shown): their collision boxes (Attr 0x80) stay in
        /// the collision copy but do not stop the player (basement npc_hoji_01, 0.0.24 test). Returns a key that changes when
        /// the set changes, so the grid is rebuilt when the story shows or hides someone.
        /// </summary>
        private static int ReadHiddenNpcs()
        {
            HiddenNpcs.Clear();
            Il2CppReferenceArray<fldNpc_tag> npcs = fld_Npc.fldNpc;
            int n = npcs == null ? 0 : Math.Min(fld_Npc.fldNpcNum, npcs.Length);
            int key = 17;
            for (int i = 0; i < n; i++)
            {
                fldNpc_tag e = npcs[i];
                if (e == null || PointsOfInterest.Shown(e))
                    continue;
                HiddenNpcs.Add((e.x, e.z));
                key = key * 31 + i + 1;
            }
            return key;
        }

        private static bool HiddenNpcBox(CollisionPoly p)
        {
            if ((p.Attr & 0x80) == 0 || HiddenNpcs.Count == 0)
                return false;
            float cx = 0, cz = 0;
            foreach (Vector4 v in p.Points)
            {
                cx += v.x;
                cz += v.z;
            }
            cx /= p.Points.Length;
            cz /= p.Points.Length;
            foreach ((float x, float z) in HiddenNpcs)
                if (Math.Abs(cx - x) <= NpcBoxReach && Math.Abs(cz - z) <= NpcBoxReach)
                    return true;
            return false;
        }

        /// <summary>Narrows [lo, hi] to 'span' around p, kept inside [lo, hi], only when it is wider than that.</summary>
        private static void Window(ref float lo, ref float hi, float p, float span)
        {
            if (hi - lo <= span)
                return;
            float start = Math.Min(Math.Max(p - span / 2, lo), hi - span);
            lo = start;
            hi = start + span;
        }

        private static void Build(int field, int area, float px, float py, float pz)
        {
            var clock = Stopwatch.StartNew();
            _builds++;
            _version = CollisionCache.Version;
            _field = field;
            _area = area;
            LastPath.Clear();

            var floors = new List<CollisionPoly>();
            var walls = new List<(float ax, float az, float bx, float bz, float lo, float hi)>();
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (CollisionPoly p in CollisionCache.Polys)
            {
                if (p.Points.Length < 2)
                    continue;
                if (Math.Abs(p.Normal.y) >= 0.5f)
                {
                    if (p.Points.Length < 3 || p.Attr != 0)
                        continue;
                    floors.Add(p);
                    foreach (Vector4 v in p.Points)
                    {
                        minX = Math.Min(minX, v.x); maxX = Math.Max(maxX, v.x);
                        minZ = Math.Min(minZ, v.z); maxZ = Math.Max(maxZ, v.z);
                    }
                }
                else if (Math.Abs(p.Normal.y) < 0.2f && !HiddenNpcBox(p) && !CollisionCache.SwitchedOff(p))
                {
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (Vector4 v in p.Points)
                    {
                        lo = Math.Min(lo, v.y);
                        hi = Math.Max(hi, v.y);
                    }
                    Footprint(p, out float ax, out float az, out float bx, out float bz);
                    walls.Add((ax, az, bx, bz, lo, hi));
                }
            }

            if (floors.Count == 0)
            {
                _w = _h = 0;
                _count = Array.Empty<byte>();
                Log.Info($"[Nav:Walk] Grid: no floor in field {field} area {area}.");
                return;
            }

            // Only an area larger than the window is cut, and then the window slides to hold the player while staying inside
            // the area. Centring it on the player cut areas that fit: back in B1 from the Healing Room (z -5650), a window
            // ending at z 350 left out the elevator at z 850 (13:23 session, 2 Oct 2026: "not reachable, left out").
            float span = (MaxCells - 1) * CellSize;
            Window(ref minX, ref maxX, px, span);
            Window(ref minZ, ref maxZ, pz, span);
            _x0 = (float)Math.Floor(minX / CellSize) * CellSize + CellSize / 2;
            _z0 = (float)Math.Floor(minZ / CellSize) * CellSize + CellSize / 2;
            _w = Math.Min(MaxCells, (int)Math.Ceiling((maxX - _x0) / CellSize) + 1);
            _h = Math.Min(MaxCells, (int)Math.Ceiling((maxZ - _z0) / CellSize) + 1);
            int cells = _w * _h;
            _count = new byte[cells];
            _height = new float[cells * Layers];
            _state = new byte[cells * Layers];
            _near = new float[cells * Layers];

            foreach (CollisionPoly f in floors)
            {
                float fx0 = float.MaxValue, fz0 = float.MaxValue, fx1 = float.MinValue, fz1 = float.MinValue;
                foreach (Vector4 v in f.Points)
                {
                    fx0 = Math.Min(fx0, v.x); fx1 = Math.Max(fx1, v.x);
                    fz0 = Math.Min(fz0, v.z); fz1 = Math.Max(fz1, v.z);
                }
                ForCells(fx0, fz0, fx1, fz1, (c, x, z) =>
                {
                    if (!CollisionCache.ContainsXZ(f.Points, x, z))
                        return;
                    float y = PlaneY(f, x, z);
                    int n = _count[c];
                    for (int l = 0; l < n; l++)
                        if (Math.Abs(_height[c * Layers + l] - y) <= MergeBand)
                            return;
                    if (n >= Layers)
                        return;
                    _height[c * Layers + n] = y;
                    _state[c * Layers + n] = 1;
                    _near[c * Layers + n] = NearRange;
                    _count[c] = (byte)(n + 1);
                });
            }

            foreach ((float ax, float az, float bx, float bz, float lo, float hi) in walls)
            {
                ForCells(Math.Min(ax, bx) - NearRange, Math.Min(az, bz) - NearRange, Math.Max(ax, bx) + NearRange, Math.Max(az, bz) + NearRange,
                    (c, x, z) =>
                    {
                        int n = _count[c];
                        if (n == 0)
                            return;
                        float d = SegmentDistance(x, z, ax, az, bx, bz);
                        if (d >= NearRange)
                            return;
                        for (int l = 0; l < n; l++)
                        {
                            int k = c * Layers + l;
                            float y = _height[k];
                            // Y points down: the body above a floor at y spans y - BodyHigh .. y - StepHeight.
                            if (lo >= y - StepHeight || hi <= y - BodyHigh)
                                continue;
                            if (d < _near[k])
                                _near[k] = d;
                            if (d < Clearance)
                                _state[k] = 2;
                        }
                    });
            }

            int walkable = 0, multi = 0;
            for (int c = 0; c < cells; c++)
            {
                if (_count[c] > 1)
                    multi++;
                for (int l = 0; l < _count[c]; l++)
                    if (_state[c * Layers + l] == 1)
                        walkable++;
            }
            Log.Info($"[Nav:Walk] Grid for field {field} area {area}: {_w} x {_h} cells of {CellSize} from {FieldData.F(_x0)} {FieldData.F(_z0)}, " +
                     $"{floors.Count} floors, {walls.Count} walls, {walkable} walkable places, {multi} cells with more than one level, {clock.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Where blobber mode's square centres lie in this area: the offset (ox, oz), in steps of CellSize below the square
        /// size, whose centres (ox + k * cell, oz + m * cell) sit furthest from walls on walkable floor, summed over the area
        /// (WalkGrid near distances). The basement's corridors are centred on multiples of 600 (offset 0, verified); the
        /// Medical Center 2F corridor runs between walls at x 1800 and 2400, so its centre 2100 lies half a square off and
        /// every snap and F3 there ended against a wall (21:31 session). Offset 0 is kept unless another is 10 % better.
        /// </summary>
        internal static void Lattice(int field, int area, float px, float py, float pz, float cell, out float ox, out float oz)
        {
            ox = oz = 0f;
            if (!Ensure(field, area, px, py, pz, everyFrame: true))
                return;
            if (_latticeBuild == _builds && _latticeCell == cell)
            {
                ox = _latticeX;
                oz = _latticeZ;
                return;
            }
            var clock = Stopwatch.StartNew();
            int steps = Math.Max(1, (int)Math.Round(cell / CellSize));
            float zeroScore = LatticeScore(0f, 0f, cell), best = zeroScore;
            for (int a = 0; a < steps; a++)
                for (int b = 0; b < steps; b++)
                {
                    float s = LatticeScore(a * CellSize, b * CellSize, cell);
                    if (s > best)
                    {
                        best = s;
                        ox = a * CellSize;
                        oz = b * CellSize;
                    }
                }
            if (best < zeroScore * 1.1f)
                ox = oz = 0f;
            else
            {
                // Wall distances are capped (NearRange), so near the best offset the score is flat: take the middle of the
                // run of offsets within 3 % of the best, on each axis. The 2F (field 15 area 3) gave 300 on one build and
                // 240 on another (22:14 session); the corridor's middle is 300.
                float bx = ox, bz = oz;
                float mx = PlateauMiddle(bx, best, cell, steps, o => LatticeScore(o, bz, cell));
                oz = PlateauMiddle(bz, best, cell, steps, o => LatticeScore(mx, o, cell));
                ox = mx;
            }
            _latticeBuild = _builds;
            _latticeCell = cell;
            _latticeX = ox;
            _latticeZ = oz;
            Log.Info($"[Nav:Walk] Square centres for field {field} area {area}: offset {FieldData.F(ox)} {FieldData.F(oz)} " +
                     $"(score {FieldData.F(best)}, offset 0 scores {FieldData.F(zeroScore)}), {clock.ElapsedMilliseconds} ms.");
        }

        /// <summary>The middle of the run of offsets around 'at' (stepping CellSize, wrapping at cell) scoring within 3 % of best.</summary>
        private static float PlateauMiddle(float at, float best, float cell, int steps, Func<float, float> score)
        {
            int lo = 0, hi = 0;
            while (lo > -steps / 2 && score(Wrap(at + (lo - 1) * CellSize, cell)) >= best * 0.97f)
                lo--;
            while (hi < steps / 2 && score(Wrap(at + (hi + 1) * CellSize, cell)) >= best * 0.97f)
                hi++;
            return Wrap(at + (lo + hi) / 2 * CellSize, cell);
        }

        private static float Wrap(float o, float cell) => ((o % cell) + cell) % cell;

        private static float LatticeScore(float ox, float oz, float cell)
        {
            float score = 0f;
            float x0 = _x0 - CellSize / 2, z0 = _z0 - CellSize / 2;
            float xs = (float)Math.Ceiling((x0 - ox) / cell) * cell + ox, zs = (float)Math.Ceiling((z0 - oz) / cell) * cell + oz;
            for (float z = zs; z < z0 + _h * CellSize; z += cell)
                for (float x = xs; x < x0 + _w * CellSize; x += cell)
                {
                    int i = (int)Math.Round((x - _x0) / CellSize), j = (int)Math.Round((z - _z0) / CellSize);
                    if (i < 0 || j < 0 || i >= _w || j >= _h)
                        continue;
                    int c = j * _w + i;
                    float bestNear = 0f;
                    for (int l = 0; l < _count[c]; l++)
                        if (_state[c * Layers + l] == 1)
                            bestNear = Math.Max(bestNear, _near[c * Layers + l]);
                    score += bestNear;
                }
            return score;
        }

        /// <summary>
        /// A path from the player to the reachable node nearest (tx, tz) in X and Z, at least minDist from it and within
        /// TargetBand of the target's height ty. Null if none. The runs start at the player's cell.
        /// </summary>
        internal static List<PathRun> Plan(float px, float py, float pz, float tx, float ty, float tz, float minDist, bool fourWay,
            out float goalX, out float goalZ)
        {
            goalX = goalZ = 0;
            LastPath.Clear();
            int start = StartNode(px, py, pz);
            if (start < 0)
                return null;

            // The search arrays are kept between calls (about 1.4 MB in the basement, and a walk to a person plans up to
            // four times); they are reset here, so every search starts from the same state as with new arrays.
            int n = _state.Length;
            if (_dist.Length != n)
            {
                _dist = new float[n];
                _from = new int[n];
                _dir = new sbyte[n];
            }
            float[] dist = _dist;
            int[] from = _from;
            sbyte[] dir = _dir;
            Array.Fill(dist, float.MaxValue);
            Array.Fill(from, -1);
            Array.Fill(dir, (sbyte)-1);
            PriorityQueue<int, float> open = Open;
            open.Clear();
            dist[start] = 0;
            open.Enqueue(start, 0);
            while (open.TryDequeue(out int cur, out float d))
            {
                if (d > dist[cur])
                    continue;
                int cell = cur / Layers;
                int ci = cell % _w, cj = cell / _w;
                float y = _height[cur];
                for (int s = 0; s < 8; s += fourWay ? 2 : 1)
                {
                    int nk = Step(ci + Dx[s], cj + Dz[s], y);
                    if (nk < 0)
                        continue;
                    bool diagonal = Dx[s] != 0 && Dz[s] != 0;
                    if (diagonal && (Step(ci + Dx[s], cj, y) < 0 || Step(ci, cj + Dz[s], y) < 0))
                        continue;
                    float near = 1f - _near[nk] / NearRange;
                    float nd = d + (diagonal ? 1.4142f : 1f) * (1f + NearCost * near * near) + (dir[cur] >= 0 && dir[cur] != s ? TurnPenalty : 0f);
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
                if (dist[k] == float.MaxValue || Math.Abs(_height[k] - ty) > TargetBand)
                    continue;
                CellCentre(k / Layers, out float x, out float z);
                float e = (float)Math.Sqrt((x - tx) * (x - tx) + (z - tz) * (z - tz));
                if (e < minDist)
                    continue;
                float score = e + dist[k] * 0.01f;   // nearest to the target first, then the shorter walk
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
            CellCentre(goal / Layers, out goalX, out goalZ);
            return Runs(LastPath, dir);
        }

        /// <summary>The walkable node of cell (i, j) reachable from a floor at height y (within MaxStep), or -1.</summary>
        private static int Step(int i, int j, float y)
        {
            if (i < 0 || j < 0 || i >= _w || j >= _h)
                return -1;
            int c = j * _w + i, best = -1;
            float bestD = MaxStep;
            for (int l = 0; l < _count[c]; l++)
            {
                int k = c * Layers + l;
                float d = Math.Abs(_height[k] - y);
                if (_state[k] == 1 && d <= bestD)
                {
                    bestD = d;
                    best = k;
                }
            }
            return best;
        }

        private static List<PathRun> Runs(List<int> nodes, sbyte[] dir)
        {
            var runs = new List<PathRun>();
            for (int k = 1; k < nodes.Count; k++)
            {
                int d = dir[nodes[k]];
                CellCentre(nodes[k - 1] / Layers, out float ax, out float az);
                CellCentre(nodes[k] / Layers, out float bx, out float bz);
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

        /// <summary>The walkable node nearest the player within 6 cells, on a floor within StartBand of the player's height, or -1.</summary>
        private static int StartNode(float x, float y, float z)
        {
            int ci = (int)Math.Round((x - _x0) / CellSize), cj = (int)Math.Round((z - _z0) / CellSize);
            int best = -1;
            float bestD = float.MaxValue;
            for (int r = 0; r <= 6 && best < 0; r++)
                for (int j = cj - r; j <= cj + r; j++)
                    for (int i = ci - r; i <= ci + r; i++)
                    {
                        if (Math.Max(Math.Abs(i - ci), Math.Abs(j - cj)) != r || i < 0 || j < 0 || i >= _w || j >= _h)
                            continue;
                        int c = j * _w + i;
                        for (int l = 0; l < _count[c]; l++)
                        {
                            int k = c * Layers + l;
                            if (_state[k] != 1 || Math.Abs(_height[k] - y) > StartBand)
                                continue;
                            CellCentre(c, out float cx, out float cz);
                            float d = (cx - x) * (cx - x) + (cz - z) * (cz - z);
                            if (d < bestD)
                            {
                                bestD = d;
                                best = k;
                            }
                        }
                    }
            return best;
        }

        /// <summary>
        /// Whether a walkable node within radius of (x, z) and within TargetBand of height y can be reached from the player:
        /// a flood with the same moves as Plan (8 directions), cached until the grid or the player's node changes.
        /// </summary>
        internal static bool ReachableNear(float px, float py, float pz, float x, float y, float z, float radius)
        {
            int start = StartNode(px, py, pz);
            if (start < 0)
                return false;
            if (start != _reachStart || _reach.Length != _state.Length || _reachBuild != _builds)
                Flood(start);

            int r = (int)Math.Ceiling(radius / CellSize);
            int ci = (int)Math.Round((x - _x0) / CellSize), cj = (int)Math.Round((z - _z0) / CellSize);
            for (int j = cj - r; j <= cj + r; j++)
                for (int i = ci - r; i <= ci + r; i++)
                {
                    if (i < 0 || j < 0 || i >= _w || j >= _h)
                        continue;
                    int c = j * _w + i;
                    CellCentre(c, out float cx, out float cz);
                    if ((cx - x) * (cx - x) + (cz - z) * (cz - z) > radius * radius)
                        continue;
                    for (int l = 0; l < _count[c]; l++)
                    {
                        int k = c * Layers + l;
                        if (_reach[k] && Math.Abs(_height[k] - y) <= TargetBand)
                            return true;
                    }
                }
            return false;
        }

        private static void Flood(int start)
        {
            _reach = new bool[_state.Length];
            _reachStart = start;
            _reachBuild = _builds;
            var queue = new Queue<int>();
            _reach[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cell = cur / Layers;
                int ci = cell % _w, cj = cell / _w;
                float y = _height[cur];
                for (int s = 0; s < 8; s++)
                {
                    int nk = Step(ci + Dx[s], cj + Dz[s], y);
                    if (nk < 0 || _reach[nk])
                        continue;
                    if (Dx[s] != 0 && Dz[s] != 0 && (Step(ci + Dx[s], cj, y) < 0 || Step(ci, cj + Dz[s], y) < 0))
                        continue;
                    _reach[nk] = true;
                    queue.Enqueue(nk);
                }
            }
        }

        private static bool Contains(float x, float z)
            => _w > 0 && x >= _x0 - CellSize && z >= _z0 - CellSize && x <= _x0 + _w * CellSize && z <= _z0 + _h * CellSize;

        private static void CellCentre(int c, out float x, out float z)
        {
            x = _x0 + (c % _w) * CellSize;
            z = _z0 + (c / _w) * CellSize;
        }

        /// <summary>
        /// The grid at the player's level as text, north at the top: '.' walkable, '#' blocked, ':' a floor only at another
        /// level, ' ' no floor, '*' last planned path, '@' the player, plus the given marks (points of interest).
        /// </summary>
        internal static string AsciiMap(float px, float py, float pz, IEnumerable<(float x, float z, char mark)> marks)
        {
            if (_w == 0)
                return "(no grid)";
            var rows = new char[_h][];
            for (int j = 0; j < _h; j++)
            {
                rows[j] = new char[_w];
                for (int i = 0; i < _w; i++)
                {
                    int c = j * _w + i;
                    char ch = _count[c] == 0 ? ' ' : ':';
                    for (int l = 0; l < _count[c]; l++)
                    {
                        int k = c * Layers + l;
                        if (Math.Abs(_height[k] - py) <= StartBand)
                            ch = _state[k] == 1 ? '.' : '#';
                    }
                    rows[j][i] = ch;
                }
            }
            foreach (int k in LastPath)
            {
                int c = k / Layers;
                rows[c / _w][c % _w] = '*';
            }
            foreach ((float x, float z, char mark) in marks)
                Mark(rows, x, z, mark);
            Mark(rows, px, pz, '@');

            var sb = new StringBuilder();
            sb.AppendLine($"grid {_w} x {_h}, cell {CellSize}, west edge x {FieldData.F(_x0)}, south edge z {FieldData.F(_z0)}, level y {FieldData.F(py)}; north at the top, east to the right");
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

        private static void ForCells(float x0, float z0, float x1, float z1, Action<int, float, float> action)
        {
            int i0 = Math.Max(0, (int)Math.Floor((x0 - _x0) / CellSize)), i1 = Math.Min(_w - 1, (int)Math.Ceiling((x1 - _x0) / CellSize));
            int j0 = Math.Max(0, (int)Math.Floor((z0 - _z0) / CellSize)), j1 = Math.Min(_h - 1, (int)Math.Ceiling((z1 - _z0) / CellSize));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    action(j * _w + i, _x0 + i * CellSize, _z0 + j * CellSize);
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
