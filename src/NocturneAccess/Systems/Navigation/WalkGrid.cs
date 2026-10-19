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
    ///   |normal.y| 0.5 or more, Attr 0 or 0x80 (CollisionPoly.IsGroundAttr, 0.0.63); overlays with Attr 0x800 / 0x1000 / 0x8000 reach past walls and are not floors)
    ///   whose outline contains the cell centre, at their plane height there. One cell can hold a ground floor and a
    ///   landing above it (the stairwell of the Medical Center, field 14 area 14, has its 2F door right above the 1F door).
    /// - A node (cell, layer) is walkable unless a vertical polygon that overlaps the body above that floor (StepHeight, 55 since 0.0.64, to
    ///   BodyHigh units up; game Y points down) lies closer than Clearance: walls, closed doors, furniture, NPC boxes.
    ///   Stair steps lower than StepHeight do not block; a lower wall with no floor at its top still does when it reaches 35 above
    ///   the floor, where the game tests walls (a railing, 0.0.81, FloorAtTop). The game keeps the player 45 units from a wall (fldHit.fldHitChkUnit).
    /// - Moves join neighbouring nodes whose heights differ by MaxStep or less (stairs, ramps), to every such layer of the
    ///   neighbour (0.0.60), 8 directions or 4 in first person (the game turns in 90-degree steps), no cutting past a
    ///   blocked corner. Cost: 1 per cell (1.414 diagonal),
    ///   up to 1 + NearCost times more within NearRange of a wall (routes keep to the middle), plus TurnPenalty per turn.
    /// - The goal is the reachable node nearest the target in X and Z, at least minDist away and within TargetBand of
    ///   the target's height.
    /// - The world map (fieldID below 10, 0.0.41) has its own walls: see WorldClearance and BlockWorldWalls; from 0.0.43 no move may cross one (BlockWorldMoves).
    /// Rebuilt when the collision copy changes (CollisionCache.Version), the area changes or the player leaves the window
    /// (only areas larger than MaxCells squared in all, the world map, are cut to a window; 0.0.60).
    /// </summary>
    internal static class WalkGrid
    {
        internal const float CellSize = 30f;
        private const int Layers = 6;   // 3 until 0.0.60, 4 until 0.0.62 (the Mantra pyramid and field 24 area 9 stack more); heights past the limit are counted in the grid's log line
        // 0.0.78: 46 (was 65), just over the game's own radius. fldHit.fldHitChkUnit (read offline, 14 Oct 2026) pushes the player
        // 45 from a wall in dungeons (30 on the world map, 25 in one field); the 60 measured on 1 Oct is where the player ends,
        // the test being made one 29-unit step ahead. With 65 a passage under 130 wide had no cell: Ikebukuro East Hall's
        // escalators (108 wide) and the walkway beside the Mystical Chest (130 free) were cut. Checked on all 92 dumps
        // (tools/navregions.pl NAVCLEAR=46): no point becomes unreachable, several become reachable. Routes still keep to the
        // middle (NearCost).
        private const float Clearance = 46f;
        // 0.0.63: 55 and 55 (were 30 and 45). The Assembly of Nihilo's long corridor (field 20 area 6, dump 9 Oct 20:33:11) goes
        // down in 50-unit steps the player walks (stood at y 48 one step below the door at 0): each riser counted as a wall and
        // the corridor fell into 9 pieces, nothing reachable. Field 24 area 5's stairs have 40-unit risers, walls too (352
        // places reachable instead of 2 211). Checked offline on every saved dump (tools/navregions.pl NAVSTEP="55 55").
        private const float StepHeight = 55f, BodyHigh = 180f;
        private const float MaxStep = 55f;
        private const float MergeBand = 20f;
        private const float TargetBand = 200f;
        private const float StartBand = 80f;
        private const int MaxCells = 400;          // per side of the window, used only when the whole area holds more than MaxCells squared
        private const int MaxSide = 2000;          // per side of a whole area (a long, narrow tunnel), 60 000 units
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
        private static byte[] _blockedMoves = Array.Empty<byte>();   // world map: per node, bit s set when the move in direction s crosses a wall
        private static Dictionary<int, List<CollisionPoly>> _worldNear = new Dictionary<int, List<CollisionPoly>>();   // world map: walls near each cell
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
        /// <summary>Counts grid builds: anything derived from the grid (regions, warp graph, door links) is stale when it changes.</summary>
        internal static int Builds => _builds;

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
            // An area with no floor has no window to leave: it was rebuilt and logged on every frame in blobber mode
            // (field 15 area 19, 5 426 of the 7 616 lines of the 20:10 session, 2 Oct 2026).
            bool same = _version == CollisionCache.Version && _field == field && _area == area && (_w == 0 || Contains(px, pz));
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

            bool world = FieldState.WorldMapField(field);
            var floors = new List<CollisionPoly>();
            var walls = new List<(float ax, float az, float bx, float bz, float lo, float hi)>();
            var worldWalls = new List<CollisionPoly>();
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (CollisionPoly p in CollisionCache.Polys)
            {
                if (p.Points.Length < 2)
                    continue;
                if (Math.Abs(p.Normal.y) >= 0.5f)
                {
                    // Faces looking down (Y points down: Normal.y > 0) are ceilings and the undersides of blocks, never walked on
                    // (0.0.65): the cube hall's 450-unit blocks have their undersides 50 under the floor, which steps of 55 would
                    // join. The floor under the player faces up in all 60 saved dumps.
                    if (p.Points.Length < 3 || !p.IsGroundAttr || p.Normal.y > 0f)
                        continue;
                    floors.Add(p);
                    foreach (Vector4 v in p.Points)
                    {
                        minX = Math.Min(minX, v.x); maxX = Math.Max(maxX, v.x);
                        minZ = Math.Min(minZ, v.z); maxZ = Math.Max(maxZ, v.z);
                    }
                }
                else if (world)
                {
                    // The world map: every non-floor polygon of Attr 0 (and NPC boxes, 0x80) is a wall, tilted ones included;
                    // the tall vertical Attr 0x800 planes are region boundaries the marker walks through (3 units from
                    // them in the 20:26 trace) and cut Shinjuku into pieces when they counted as walls.
                    if (p.Points.Length >= 3 && (p.Attr == 0 || p.Attr == 0x80) && !HiddenNpcBox(p) && !CollisionCache.SwitchedOff(p))
                        worldWalls.Add(p);
                }
                else if (Math.Abs(p.Normal.y) < 0.2f && !p.IsTriggerAttr && !HiddenNpcBox(p) && !CollisionCache.SwitchedOff(p))
                {
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (Vector4 v in p.Points)
                    {
                        lo = Math.Min(lo, v.y);
                        hi = Math.Max(hi, v.y);
                    }
                    CollisionCache.Footprint(p, out float ax, out float az, out float bx, out float bz);
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
            // 0.0.60: the window is used only for areas larger than MaxCells squared in all (the world map). The Ginza
            // underpass tunnels are 12 630 units long and 3 000 wide: a 400-cell side (11 970) cut one end off, and its
            // ladders and doors were "not reachable" until the player walked there and the window slid (6 Oct 21:58, area 14).
            int side = MaxCells;
            double wholeX = (maxX - minX) / CellSize + 2, wholeZ = (maxZ - minZ) / CellSize + 2;
            if (wholeX * wholeZ <= (double)MaxCells * MaxCells && wholeX <= MaxSide && wholeZ <= MaxSide)
                side = MaxSide;
            else
            {
                float span = (MaxCells - 1) * CellSize;
                Window(ref minX, ref maxX, px, span);
                Window(ref minZ, ref maxZ, pz, span);
            }
            _x0 = (float)Math.Floor(minX / CellSize) * CellSize + CellSize / 2;
            _z0 = (float)Math.Floor(minZ / CellSize) * CellSize + CellSize / 2;
            _w = Math.Min(side, (int)Math.Ceiling((maxX - _x0) / CellSize) + 1);
            _h = Math.Min(side, (int)Math.Ceiling((maxZ - _z0) / CellSize) + 1);
            int cells = _w * _h;
            _count = new byte[cells];
            _blockedMoves = Array.Empty<byte>();
            _worldNear = new Dictionary<int, List<CollisionPoly>>();
            _height = new float[cells * Layers];
            _state = new byte[cells * Layers];
            _near = new float[cells * Layers];

            int dropped = 0;
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
                        {
                            // Merged floors keep the higher one (Y points down: the smaller y), where the player stands (0.0.74).
                            // The Nihilo core (field 20 area 7): its spiral ramp passes 10 under the floor; merged at the ramp's
                            // height, that strip joined the floor to the ramp, and a walk to the door at the spiral's foot circled
                            // the room on the floor above the ramp (12 Oct 2026, 21:06).
                            if (y < _height[c * Layers + l])
                                _height[c * Layers + l] = y;
                            return;
                        }
                    if (n >= Layers)
                    {
                        dropped++;
                        return;
                    }
                    _height[c * Layers + n] = y;
                    _state[c * Layers + n] = 1;
                    _near[c * Layers + n] = NearRange;
                    _count[c] = (byte)(n + 1);
                });
            }
            int seams = BridgeSeams(floors);

            int railed = 0;
            foreach ((float ax, float az, float bx, float bz, float lo, float hi) in walls)
            {
                int topFloor = -1;   // -1 not looked at yet, 0 none (a railing), 1 a floor at the top (a stair riser)
                ForCells(Math.Min(ax, bx) - NearRange, Math.Min(az, bz) - NearRange, Math.Max(ax, bx) + NearRange, Math.Max(az, bz) + NearRange,
                    (c, x, z) =>
                    {
                        int n = _count[c];
                        if (n == 0)
                            return;
                        float d = CollisionCache.SegmentDistance(x, z, ax, az, bx, bz);
                        if (d >= NearRange)
                            return;
                        for (int l = 0; l < n; l++)
                        {
                            int k = c * Layers + l;
                            float y = _height[k];
                            // Y points down: the body above a floor at y spans y - BodyHigh .. y - StepHeight.
                            if (hi <= y - BodyHigh)
                                continue;
                            if (lo >= y - StepHeight)
                            {
                                // Lower than a step (0.0.81): still a wall when it reaches the height the game tests walls at (35
                                // above the player, fldHitChkUnit: pos.y - 35) and no floor lies at its top: a railing, not a stair
                                // riser. Kabukicho Prison (field 25, 15 Oct 2026): 50-unit railings by the "Jump Off" volumes, which
                                // the map stepped over, so walks ran into them ("Blocked." at x 1840, five times).
                                if (!(lo < y - RailTestLift && hi > y - RailTestLift))
                                    continue;
                                if (topFloor < 0)
                                    topFloor = FloorAtTop(ax, az, bx, bz, lo) ? 1 : 0;
                                if (topFloor == 1)
                                    continue;
                                if (d < Clearance && _state[k] == 1)
                                    railed++;
                            }
                            if (d < _near[k])
                                _near[k] = d;
                            if (d < Clearance)
                                _state[k] = 2;
                        }
                    });
            }

            int crossings = 0;
            if (world)
            {
                BlockWorldWalls(worldWalls);
                crossings = BlockWorldMoves();
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
                     $"{floors.Count} floors, {(world ? worldWalls.Count + " world-map" : walls.Count.ToString())} walls, {(world ? crossings + " moves through walls blocked, " : "")}{walkable} walkable places, {multi} cells with more than one level" +
                     $"{(dropped > 0 ? $", {dropped} floor heights over the limit of {Layers} left out" : "")}" +
                     $"{(seams > 0 ? $", {seams} seam cells bridged" : "")}{(railed > 0 ? $", {railed} places blocked by railings" : "")}, {clock.ElapsedMilliseconds} ms.");
            ApplyAvoid(world ? new List<Zone>() : WarpMap.Zones(field, area));
        }

        /// <summary>
        /// A cell with no floor at its centre whose two sides, half a cell from the centre north and south (or east and west), lie on
        /// floors within MergeBand of each other takes the higher of the two (0.0.77). Ginza 1F (field 19 area 2): the hall floor
        /// ends at z -1090 and the pocket in front of same-area door 01d_08 starts at -1100; the row of cell centres at -1095 fell
        /// in that seam, so the pocket, the door and the exit beyond it were "not reachable" from the hall, which Francesco walked
        /// by hand (14 Oct 2026, 13:40; 4 and 6 Oct too). Checked on all 84 dumps (tools/navregions.pl, NAVSEAM): only Ginza 1F
        /// changes reachability; elsewhere 1 to 4 cells are bridged, joining no region. Returns the cells bridged.
        /// </summary>
        private static int BridgeSeams(List<CollisionPoly> floors)
        {
            const float half = CellSize / 2;
            var sides = new Dictionary<int, List<float>[]>();
            foreach (CollisionPoly f in floors)
            {
                float fx0 = float.MaxValue, fz0 = float.MaxValue, fx1 = float.MinValue, fz1 = float.MinValue;
                foreach (Vector4 v in f.Points)
                {
                    fx0 = Math.Min(fx0, v.x); fx1 = Math.Max(fx1, v.x);
                    fz0 = Math.Min(fz0, v.z); fz1 = Math.Max(fz1, v.z);
                }
                ForCells(fx0 - half, fz0 - half, fx1 + half, fz1 + half, (c, x, z) =>
                {
                    if (_count[c] != 0)
                        return;
                    for (int s = 0; s < 4; s++)
                    {
                        float sx = s == 2 ? x - half : s == 3 ? x + half : x;
                        float sz = s == 0 ? z - half : s == 1 ? z + half : z;
                        if (!CollisionCache.ContainsXZ(f.Points, sx, sz))
                            continue;
                        if (!sides.TryGetValue(c, out List<float>[] lists))
                            sides[c] = lists = new List<float>[4];
                        (lists[s] ??= new List<float>()).Add(PlaneY(f, sx, sz));
                    }
                });
            }
            int bridged = 0;
            foreach (KeyValuePair<int, List<float>[]> kv in sides)
            {
                float best = float.MaxValue;
                for (int axis = 0; axis < 2; axis++)
                {
                    List<float> a = kv.Value[2 * axis], b = kv.Value[2 * axis + 1];
                    if (a == null || b == null)
                        continue;
                    foreach (float ya in a)
                        foreach (float yb in b)
                            if (Math.Abs(ya - yb) <= MergeBand)
                                best = Math.Min(best, Math.Min(ya, yb));   // Y points down: the higher floor
                }
                if (best == float.MaxValue)
                    continue;
                int c = kv.Key;
                _height[c * Layers] = best;
                _state[c * Layers] = 1;
                _near[c * Layers] = NearRange;
                _count[c] = 1;
                bridged++;
            }
            return bridged;
        }

        // ---- Warps (0.0.52) ----
        // The Amala Network's hidden warps (movetype 2 door volumes, WarpMap) move the player as soon as they are walked into
        // (4 Oct 2026, 15:56 to 15:58: a route's first run crossed area 3's 01d_03 four times and the game sent the player back
        // each time). Their nodes are kept out of every search and flood, except the warp a route is aiming for (Plan's allow).

        // Kept clear of a warp: the player drifts off a run in turns (15:56:59: the hit of 01d_03 fired 13 inside its box while the
        // route ran 10 outside it) and its collision radius is 30.
        private const float AvoidMargin = 45f;
        private static short[] _avoid = Array.Empty<short>();   // per node: 0, or the warp zone's id + 1
        private static short _allow;                            // during Plan: the zone id + 1 that may be entered
        private static int[] _component = Array.Empty<int>();   // per node: connected region (avoid applied), -1 not walkable
        private static int _componentBuild = -1;

        /// <summary>A warp's volume in X and Z: centre, depth axis (its facing) and half extents.</summary>
        internal struct Zone
        {
            public int Id;
            public float X, Z, NX, NZ, HalfDepth, HalfWidth;

            public bool Contains(float x, float z, float margin)
            {
                float dx = x - X, dz = z - Z;
                return Math.Abs(dx * NX + dz * NZ) <= HalfDepth + margin && Math.Abs(dz * NX - dx * NZ) <= HalfWidth + margin;
            }
        }

        private static void ApplyAvoid(List<Zone> zones)
        {
            _avoid = new short[_state.Length];
            int marked = 0;
            foreach (Zone zone in zones)
            {
                float reach = Math.Max(zone.HalfDepth, zone.HalfWidth) + AvoidMargin + CellSize;
                ForCells(zone.X - reach, zone.Z - reach, zone.X + reach, zone.Z + reach, (c, x, z) =>
                {
                    if (!zone.Contains(x, z, AvoidMargin))
                        return;
                    for (int l = 0; l < _count[c]; l++)
                    {
                        _avoid[c * Layers + l] = (short)(zone.Id + 1);
                        marked++;
                    }
                });
            }
            if (zones.Count > 0)
                Log.Info($"[Nav:Walk] {zones.Count} warp volumes kept out of routes ({marked} places).");
        }

        private static bool Avoided(int k) => _avoid.Length == _state.Length && _avoid[k] != 0 && _avoid[k] != _allow;

        /// <summary>The connected region of the node nearest (x, y, z) (as a route would start there), or -1.</summary>
        internal static int RegionAt(float x, float y, float z)
        {
            int k = StartNode(x, y, z);
            if (k < 0)
                return -1;
            Regions();
            return _component[k];
        }

        /// <summary>The regions of the walkable nodes within margin of a warp's volume but outside it: the sides it is entered from.</summary>
        internal static HashSet<int> RegionsAround(Zone zone, float y, float margin)
        {
            Regions();
            var set = new HashSet<int>();
            float reach = Math.Max(zone.HalfDepth, zone.HalfWidth) + AvoidMargin + margin + CellSize;
            ForCells(zone.X - reach, zone.Z - reach, zone.X + reach, zone.Z + reach, (c, x, z) =>
            {
                if (zone.Contains(x, z, AvoidMargin) || !zone.Contains(x, z, AvoidMargin + margin))
                    return;
                for (int l = 0; l < _count[c]; l++)
                {
                    int k = c * Layers + l;
                    if (_component[k] >= 0 && AtHeight(k, y))
                        set.Add(_component[k]);
                }
            });
            return set;
        }

        /// <summary>The regions of the walkable nodes within radius of (x, z) at height y (a target's surroundings).</summary>
        internal static HashSet<int> RegionsNear(float x, float y, float z, float radius)
        {
            Regions();
            var set = new HashSet<int>();
            ForCells(x - radius, z - radius, x + radius, z + radius, (c, cx, cz) =>
            {
                if ((cx - x) * (cx - x) + (cz - z) * (cz - z) > radius * radius)
                    return;
                for (int l = 0; l < _count[c]; l++)
                {
                    int k = c * Layers + l;
                    if (_component[k] >= 0 && AtHeight(k, y))
                        set.Add(_component[k]);
                }
            });
            return set;
        }

        /// <summary>Labels the connected regions of the walkable nodes (8-way moves as Plan, warps kept out), once per grid.</summary>
        private static void Regions()
        {
            if (_componentBuild == _builds && _component.Length == _state.Length)
                return;
            _componentBuild = _builds;
            _component = new int[_state.Length];
            Array.Fill(_component, -1);
            short saved = _allow;
            _allow = 0;
            int label = 0;
            var queue = new Queue<int>();
            for (int k0 = 0; k0 < _state.Length; k0++)
            {
                if (_state[k0] != 1 || _component[k0] >= 0 || Avoided(k0))
                    continue;
                _component[k0] = label;
                queue.Enqueue(k0);
                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    int cell = cur / Layers;
                    int ci = cell % _w, cj = cell / _w;
                    float y = _height[cur];
                    for (int s = 0; s < 8; s++)
                    {
                        if (MoveBlocked(cur, s))
                            continue;
                        if (Dx[s] != 0 && Dz[s] != 0 && (Step(ci + Dx[s], cj, y) < 0 || Step(ci, cj + Dz[s], y) < 0))
                            continue;
                        int m = Steps(ci + Dx[s], cj + Dz[s], y);
                        for (int t = 0; t < m; t++)
                        {
                            int nk = StepBuf[t];
                            if (_component[nk] >= 0)
                                continue;
                            _component[nk] = label;
                            queue.Enqueue(nk);
                        }
                    }
                }
                label++;
            }
            _allow = saved;
        }

        // The world map's walls (docs/HANDOFF-OVERWORLD.md step 3). The game's collision (fldHit.fldHitChkUnitVWM, radius 30
        // there) pushes the marker out of a wall when it is closer than the radius to the wall's plane and lies inside its
        // outline; 13 of the 18 traced positions near walls sat at 29 to 30 (20:26 and 21:11 walks, 2 Oct 2026). The lanes
        // are narrow: the one west of the Medical Center leaves a free band a few units wide, which no cell centre hits, and
        // the marker squeezed through at 26 to 27 (once 17.7, at a block corner). So a cell is walkable when some point in it
        // (centre, then offsets of 6 and 12) clears every wall by WorldClearance, measured WorldLift above the floor:
        // checked offline against the Shinjuku dump, 22 puts all 92 traced positions on reachable cells and makes every
        // point of interest reachable; 24 already breaks the lane the player walked.
        // WorldLift 35 (0.0.79): fldHitChkUnitVWM tests the walls 35 above the marker (pos.y minus the constant 35, 0x1828C792C,
        // read offline 14 Oct 2026). At 15 the map tested under the highway's wall #1584, whose bottom is 20 above the road, so
        // the corner at -1506 -2690 looked open while the game held the marker there, 29.8 from that wall (8 Oct, 9 Oct, 14 Oct
        // 20:22). Over the 299 positions traced on field 3: none closer than 26 to a wall at 35; at 15 one sat 20 from wall #788.
        private const float WorldClearance = 22f, WorldLift = 35f, WorldBucketReach = 45f;   // 45: a diagonal move (42.4) crosses only walls this close
        private static readonly float[] WorldOffsets = { 0f, -6f, 6f, -12f, 12f };

        /// <summary>World map: blocks the nodes with no point clear of the walls (see WorldClearance) and sets wall distances.</summary>
        private static void BlockWorldWalls(List<CollisionPoly> walls)
        {
            var near = new Dictionary<int, List<CollisionPoly>>();
            foreach (CollisionPoly w in walls)
            {
                float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
                foreach (Vector4 v in w.Points)
                {
                    x0 = Math.Min(x0, v.x); x1 = Math.Max(x1, v.x);
                    z0 = Math.Min(z0, v.z); z1 = Math.Max(z1, v.z);
                }
                ForCells(x0 - NearRange, z0 - NearRange, x1 + NearRange, z1 + NearRange, (c, x, z) =>
                {
                    int n = _count[c];
                    if (n == 0)
                        return;
                    for (int l = 0; l < n; l++)
                    {
                        int k = c * Layers + l;
                        float d = ProjectedDistance(w, x, _height[k] - WorldLift, z);
                        if (d < _near[k])
                            _near[k] = d;
                    }
                    if (x >= x0 - WorldBucketReach && x <= x1 + WorldBucketReach && z >= z0 - WorldBucketReach && z <= z1 + WorldBucketReach)
                    {
                        if (!near.TryGetValue(c, out List<CollisionPoly> list))
                            near[c] = list = new List<CollisionPoly>();
                        list.Add(w);
                    }
                });
            }

            _worldNear = near;
            foreach (KeyValuePair<int, List<CollisionPoly>> e in near)
            {
                int c = e.Key;
                CellCentre(c, out float x, out float z);
                for (int l = 0; l < _count[c]; l++)
                {
                    int k = c * Layers + l;
                    if (_near[k] >= WorldClearance || Clear(e.Value, x, _height[k] - WorldLift, z))
                        continue;
                    _state[k] = 2;
                }
            }
        }

        /// <summary>
        /// World map (0.0.43): blocks every move between two walkable nodes whose segment (centre to centre, WorldLift above
        /// each floor) crosses a wall polygon. The cell test alone made walls thin: a cell 15 from a wall finds a point 12
        /// further off that clears it by 22, so the cells on both sides of a wall were walkable and routes went straight
        /// through buildings and cliffs (3 Oct 14:31 session: stuck against wall #20 west of the Medical Center, against
        /// #155 and #156 in Yoyogi). Checked offline (tools/worldgrid.pl EDGE=1): all 190 traced positions stay reachable.
        /// Returns the number of moves blocked.
        /// </summary>
        private static int BlockWorldMoves()
        {
            _blockedMoves = new byte[_state.Length];
            int blocked = 0;
            foreach (KeyValuePair<int, List<CollisionPoly>> e in _worldNear)
            {
                int c = e.Key;
                int ci = c % _w, cj = c / _w;
                CellCentre(c, out float x, out float z);
                for (int l = 0; l < _count[c]; l++)
                {
                    int k = c * Layers + l;
                    if (_state[k] != 1)
                        continue;
                    float y = _height[k];
                    for (int s = 0; s < 8; s++)
                    {
                        int nk = Step(ci + Dx[s], cj + Dz[s], y);
                        if (nk < 0)
                            continue;
                        CellCentre(nk / Layers, out float nx, out float nz);
                        if (Crosses(e.Value, x, y - WorldLift, z, nx, _height[nk] - WorldLift, nz))
                        {
                            _blockedMoves[k] |= (byte)(1 << s);
                            blocked++;
                        }
                    }
                }
            }
            return blocked;
        }

        /// <summary>Whether the segment a-b passes through one of the wall polygons (its triangles 0-1-2 and 0-2-3).</summary>
        private static bool Crosses(List<CollisionPoly> walls, float ax, float ay, float az, float bx, float by, float bz)
        {
            foreach (CollisionPoly w in walls)
            {
                Vector4 n = w.Normal;
                float da = n.x * ax + n.y * ay + n.z * az - w.Num;
                float db = n.x * bx + n.y * by + n.z * bz - w.Num;
                if (da * db > 0f || da == db)
                    continue;
                float t = da / (da - db);
                float px = ax + t * (bx - ax), py = ay + t * (by - ay), pz = az + t * (bz - az);
                Vector4[] p = w.Points;
                if (InTriangle(px, py, pz, p[0], p[1], p[2]) || (p.Length > 3 && InTriangle(px, py, pz, p[0], p[2], p[3])))
                    return true;
            }
            return false;
        }

        /// <summary>The move from node k in direction s goes through a wall (world map), or the game refused it on a walk (learned).</summary>
        private static bool MoveBlocked(int k, int s)
        {
            if (_blockedMoves.Length == _state.Length && (_blockedMoves[k] & (1 << s)) != 0)
                return true;
            if (Learned.Count == 0 || _learnedField != _field || _learnedArea != _area)
                return false;
            CellCentre(k / Layers, out float x, out float z);
            return Learned.Contains((AbsCell(x), AbsCell(z), s));
        }

        // ---- Moves the game refused (0.0.62) ----
        // The world map's highway (field 3, 8 Oct 2026, 21:27): at -1506 -2690 the route's first move east was walkable on the map,
        // but the game pushed the marker back each time; six identical re-plans, then "Blocked." (a nudge by hand got past). When a
        // walk stalls, AutoWalk blocks the move from the player's cell along the run, and the next one, both ways, so the re-plan
        // goes round. Kept until the area changes; cells by absolute position, so a moved world-map window keeps them.
        private static readonly HashSet<(int, int, int)> Learned = new HashSet<(int, int, int)>();
        private static int _learnedField = int.MinValue, _learnedArea = int.MinValue;

        private static int AbsCell(float v) => (int)Math.Floor(v / CellSize);

        /// <summary>Blocks the moves from (x, z) along direction dir (0 north, clockwise in 45s) for 'steps' cells, both ways.</summary>
        internal static void LearnBlocked(int field, int area, float x, float z, int dir, int steps, bool fourWays = false)
        {
            if (field != _learnedField || area != _learnedArea)
            {
                Learned.Clear();
                _learnedField = field;
                _learnedArea = area;
            }
            int ai = AbsCell(x), aj = AbsCell(z), back = (dir + 4) % 8;
            for (int n = 0; n < steps; n++)
            {
                Learned.Add((ai, aj, dir));
                ai += Dx[dir];
                aj += Dz[dir];
                Learned.Add((ai, aj, back));
            }
            _reachBuild = -1;        // floods and regions are stale
            _componentBuild = -1;
            Log.Info($"[Nav:Walk] Learned: the game refused the move {dir * 45} degrees from {FieldData.F(x)} {FieldData.F(z)}; {steps} cells blocked both ways ({Learned.Count} learned in this area).");
            // The player came into this square, so some way out exists: refusals in all eight directions mean the marker is held
            // (wedged in a corner), not that every move is a wall. Kept, they cut the player off from everything: "No path." and
            // the Page Up list "All, 0" (14 Oct 2026, 20:22:32, the highway corner). Forget this square's refusals (0.0.79).
            // A first-person walk moves in four directions only (0.0.82): all four refused is the same sign.
            int pi = AbsCell(x), pj = AbsCell(z), refused = 0;
            for (int s = 0; s < 8; s += fourWays ? 2 : 1)
                if (Learned.Contains((pi, pj, s)))
                    refused++;
            if (refused == (fourWays ? 4 : 8))
            {
                Learned.RemoveWhere(e => e.Item1 == pi && e.Item2 == pj);
                Log.Info($"[Nav:Walk] Learned: every move from {FieldData.F(x)} {FieldData.F(z)} refused; the marker is held there, its square's refusals forgotten ({Learned.Count} learned in this area).");
            }
        }

        /// <summary>
        /// World map: the straight line from the player to a candidate start cell goes through a wall, checked against the walls
        /// near both cells (a start up to 6 cells away must not lie behind a building).
        /// </summary>
        private static bool StartBehindWall(float x, float y, float z, int c, float cy)
        {
            if (_blockedMoves.Length != _state.Length)
                return false;
            CellCentre(c, out float cx, out float cz);
            int pi = (int)Math.Round((x - _x0) / CellSize), pj = (int)Math.Round((z - _z0) / CellSize);
            if (pi >= 0 && pj >= 0 && pi < _w && pj < _h && _worldNear.TryGetValue(pj * _w + pi, out List<CollisionPoly> a) &&
                Crosses(a, x, y - WorldLift, z, cx, cy - WorldLift, cz))
                return true;
            return _worldNear.TryGetValue(c, out List<CollisionPoly> b) && Crosses(b, x, y - WorldLift, z, cx, cy - WorldLift, cz);
        }

        /// <summary>Some point of the cell around (x, z) lies at least WorldClearance from every wall in the list.</summary>
        private static bool Clear(List<CollisionPoly> walls, float x, float y, float z)
        {
            foreach (float ox in WorldOffsets)
                foreach (float oz in WorldOffsets)
                {
                    bool free = true;
                    foreach (CollisionPoly w in walls)
                        if (ProjectedDistance(w, x + ox, y, z + oz) < WorldClearance)
                        {
                            free = false;
                            break;
                        }
                    if (free)
                        return true;
                }
            return false;
        }

        /// <summary>
        /// Distance from a point to a polygon's plane (normal . p = Num) when the point projects inside the polygon (its
        /// triangles 0-1-2 and 0-2-3), else MaxValue: the game's kind of wall test.
        /// </summary>
        private static float ProjectedDistance(CollisionPoly w, float x, float y, float z)
        {
            Vector4 n = w.Normal;
            float d = n.x * x + n.y * y + n.z * z - w.Num;
            float qx = x - d * n.x, qy = y - d * n.y, qz = z - d * n.z;
            Vector4[] p = w.Points;
            if (InTriangle(qx, qy, qz, p[0], p[1], p[2]) || (p.Length > 3 && InTriangle(qx, qy, qz, p[0], p[2], p[3])))
                return Math.Abs(d);
            return float.MaxValue;
        }

        private static bool InTriangle(float px, float py, float pz, Vector4 a, Vector4 b, Vector4 c)
        {
            double v0x = c.x - a.x, v0y = c.y - a.y, v0z = c.z - a.z;
            double v1x = b.x - a.x, v1y = b.y - a.y, v1z = b.z - a.z;
            double v2x = px - a.x, v2y = py - a.y, v2z = pz - a.z;
            double d00 = v0x * v0x + v0y * v0y + v0z * v0z, d01 = v0x * v1x + v0y * v1y + v0z * v1z, d02 = v0x * v2x + v0y * v2y + v0z * v2z;
            double d11 = v1x * v1x + v1y * v1y + v1z * v1z, d12 = v1x * v2x + v1y * v2y + v1z * v2z;
            double den = d00 * d11 - d01 * d01;
            if (Math.Abs(den) < 1e-6)
                return false;
            double u = (d11 * d02 - d01 * d12) / den, v = (d00 * d12 - d01 * d02) / den;
            return u >= -1e-4 && v >= -1e-4 && u + v <= 1 + 1e-4;
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
        /// <param name="allowZone">The id of the warp zone the route aims for (WarpMap), which may be entered; -1 for none.</param>
        internal static List<PathRun> Plan(float px, float py, float pz, float tx, float ty, float tz, float minDist, bool fourWay,
            out float goalX, out float goalZ, int allowZone = -1)
        {
            _allow = (short)(allowZone + 1);
            try
            {
                return PlanAllowing(px, py, pz, tx, ty, tz, minDist, fourWay, out goalX, out goalZ);
            }
            finally
            {
                _allow = 0;
            }
        }

        private static List<PathRun> PlanAllowing(float px, float py, float pz, float tx, float ty, float tz, float minDist, bool fourWay,
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
                    if (MoveBlocked(cur, s))
                        continue;
                    bool diagonal = Dx[s] != 0 && Dz[s] != 0;
                    if (diagonal && (Step(ci + Dx[s], cj, y) < 0 || Step(ci, cj + Dz[s], y) < 0))
                        continue;
                    int m = Steps(ci + Dx[s], cj + Dz[s], y);
                    for (int t = 0; t < m; t++)
                    {
                        int nk = StepBuf[t];
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
            }

            int goal = -1;
            float best = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                if (dist[k] == float.MaxValue || !AtHeight(k, ty))
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

        // Every walkable node of a neighbouring cell within MaxStep, not only the nearest in height (0.0.60). Under each stair of
        // the Ginza underpass (field 22) the track bed (y 200) runs on as a floor beneath the steps (y 175 to 25), so a cell at the
        // stair's foot holds both; Step took the track bed coming along the tracks and the stair coming down it, and the
        // platforms and the tracks were never joined (dump 6 Oct 14:01, area 8: three B2 regions, one for the tracks, while
        // Francesco walked between them; tools/navregions.pl). The moves are now the same in both directions.
        private static readonly int[] StepBuf = new int[Layers];

        /// <summary>
        /// Node k stands for a target at height y: within TargetBand, and no other floor of its cell is nearer y (0.0.60). With
        /// the moves of Steps the track bed under a platform (200 below it) can join the tracks; it must not stand in for the
        /// platform itself, which may be reached only by a ladder.
        /// </summary>
        private static bool AtHeight(int k, float y)
        {
            float d = Math.Abs(_height[k] - y);
            if (d > TargetBand)
                return false;
            int c = k / Layers;
            for (int l = 0; l < _count[c]; l++)
                if (Math.Abs(_height[c * Layers + l] - y) < d)
                    return false;
            return true;
        }

        /// <summary>
        /// Every walkable node of cell (i, j) within MaxStep of height y, into StepBuf; returns how many. Where the floor walked
        /// on (height y) goes on into the cell (a floor within ContinueBand of y), none under it: the player stays on that floor
        /// and cannot drop through it (0.0.74, the Nihilo core's spiral ramp under its floor). Floors above it stay open, so a
        /// stair whose foot lies on the Ginza track bed is still climbed from the tracks (0.0.60).
        /// </summary>
        private static int Steps(int i, int j, float y)
        {
            if (i < 0 || j < 0 || i >= _w || j >= _h)
                return 0;
            int c = j * _w + i, n = 0;
            float lowest = float.MaxValue;   // Y points down: no node below the floor that continues here
            for (int l = 0; l < _count[c]; l++)
            {
                float h = _height[c * Layers + l];
                if (Math.Abs(h - y) <= ContinueBand)
                    lowest = lowest == float.MaxValue ? h : Math.Max(lowest, h);
            }
            for (int l = 0; l < _count[c]; l++)
            {
                int k = c * Layers + l;
                if (_state[k] == 1 && Math.Abs(_height[k] - y) <= MaxStep && _height[k] <= lowest && !Avoided(k))
                    StepBuf[n++] = k;
            }
            return n;
        }

        private const float ContinueBand = 5f;

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
                if (_state[k] == 1 && d <= bestD && !Avoided(k))
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
                            if (_state[k] != 1 || Avoided(k) || Math.Abs(_height[k] - y) > StartBand || StartBehindWall(x, y, z, c, _height[k]))
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
            if (!FloodFrom(px, py, pz))
                return false;

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
                        if (_reach[k] && AtHeight(k, y))
                            return true;
                    }
                }
            return false;
        }

        /// <summary>
        /// Whether a walkable node whose cell centre lies in the rectangle x0..x1, z0..z1 (a door volume's footprint) and within
        /// TargetBand of height y can be reached from the player (0.0.49). Same flood as ReachableNear.
        /// </summary>
        internal static bool ReachableInRect(float px, float py, float pz, float x0, float z0, float x1, float z1, float y)
        {
            if (!FloodFrom(px, py, pz))
                return false;
            int i0 = Math.Max(0, (int)Math.Ceiling((x0 - _x0) / CellSize)), i1 = Math.Min(_w - 1, (int)Math.Floor((x1 - _x0) / CellSize));
            int j0 = Math.Max(0, (int)Math.Ceiling((z0 - _z0) / CellSize)), j1 = Math.Min(_h - 1, (int)Math.Floor((z1 - _z0) / CellSize));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    int c = j * _w + i;
                    for (int l = 0; l < _count[c]; l++)
                    {
                        int k = c * Layers + l;
                        if (_reach[k] && AtHeight(k, y))
                            return true;
                    }
                }
            return false;
        }

        /// <summary>The player's flood (ReachableNear, ReachableInRect), redone when the grid or the player's node changed; false without a start.</summary>
        private static bool FloodFrom(float px, float py, float pz)
        {
            int start = StartNode(px, py, pz);
            if (start < 0)
                return false;
            if (start != _reachStart || _reach.Length != _state.Length || _reachBuild != _builds)
                Flood(start);
            return true;
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
                    if (MoveBlocked(cur, s))
                        continue;
                    if (Dx[s] != 0 && Dz[s] != 0 && (Step(ci + Dx[s], cj, y) < 0 || Step(ci, cj + Dz[s], y) < 0))
                        continue;
                    int m = Steps(ci + Dx[s], cj + Dz[s], y);
                    for (int t = 0; t < m; t++)
                    {
                        int nk = StepBuf[t];
                        if (_reach[nk])
                            continue;
                        _reach[nk] = true;
                        queue.Enqueue(nk);
                    }
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

        // Railings (0.0.81): the game tests walls 35 above the player (fldHitChkUnit, like the world map's fldHitChkUnitVWM); a wall
        // lower than a step is a stair riser when a floor lies at its top within RailReach (the Nihilo corridor's 50-unit risers,
        // field 24 area 5's 40-unit ones), else a railing. Checked offline on every dump (tools/navregions.pl NAVRAIL): no point of
        // interest lost outside Kabukicho Prison.
        private const float RailTestLift = 35f, RailReach = 60f, RailTop = 10f;

        /// <summary>Some floor node within RailReach of the wall's footprint lies at the wall's top height (within RailTop).</summary>
        private static bool FloorAtTop(float ax, float az, float bx, float bz, float top)
        {
            bool found = false;
            ForCells(Math.Min(ax, bx) - RailReach, Math.Min(az, bz) - RailReach, Math.Max(ax, bx) + RailReach, Math.Max(az, bz) + RailReach,
                (c, x, z) =>
                {
                    if (found || _count[c] == 0 || CollisionCache.SegmentDistance(x, z, ax, az, bx, bz) >= RailReach)
                        return;
                    for (int l = 0; l < _count[c]; l++)
                        if (Math.Abs(_height[c * Layers + l] - top) <= RailTop)
                        {
                            found = true;
                            return;
                        }
                });
            return found;
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

    }
}
