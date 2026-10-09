using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppfield_H;
using NocturneAccess.Core;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>One collision polygon, copied from fldHit.gfldCollision (collision_s and its pFace).</summary>
    internal sealed class CollisionPoly
    {
        public int Index;
        public short Mode2D, NumEdges, BlockId, HitFlag;
        public int ResId, HojiId, NpcId;
        public Vector4 Normal, Elv;
        public float Num;
        public float[] Min = Array.Empty<float>(), Max = Array.Empty<float>(), EdgeNums = Array.Empty<float>();
        public Vector4[] Points = Array.Empty<Vector4>(), EdgeNormals = Array.Empty<Vector4>();
        public int PointArrayLength;

        public bool HasFace;
        public uint Attr;
        public byte Myuka, Se, Amap0, Amap1;
        public ushort Basyo, TomeIdx;
        public short EncType, EncIdx, Spc1, Spc2;

        /// <summary>Floor-like by its normal (to verify which axis is up and which polygons are walls).</summary>
        public bool IsFloorLike => Math.Abs(Normal.y) >= 0.5f;

        /// <summary>
        /// A floor the player stands on: Attr 0, or 0x80 (0.0.63). The Mantra pyramid (field 24 area 17, dump 9 Oct 2026 14:07:59)
        /// has its floors and walls in 0x80 with no NPC (hoji 0, npc -1): 241 of its 272 floors, the one under the player
        /// among them, so the walking map had no floor there and nothing was reachable. In every other dump 0x80 is only on
        /// vertical faces (NPC boxes). Overlays (0x800, 0x1000, 0x8000) still reach past walls and are not floors.
        /// </summary>
        public bool IsGroundAttr => Attr == 0 || Attr == 0x80;
    }

    /// <summary>
    /// A managed copy of the loaded area's collision polygons, rebuilt when the area, the array or the count
    /// changes. Reading thousands of IL2CPP objects is slow; queries (floor under the player, later wall
    /// distances and the grid) run on this copy.
    /// </summary>
    internal static class CollisionCache
    {
        private static readonly List<CollisionPoly> Items = new List<CollisionPoly>();
        private static IntPtr _arrayPtr;
        private static int _count = -1;
        private static int _field = int.MinValue;
        private static int _area = int.MinValue;

        internal static IReadOnlyList<CollisionPoly> Polys => Items;

        /// <summary>fldHit.gfldCollsionNum and gfldCollsionBlkNum, and the array length, at the last rebuild.</summary>
        internal static int SourceCount { get; private set; }
        internal static int SourceBlockCount { get; private set; }
        internal static int ArrayLength { get; private set; }

        /// <summary>Increases on every rebuild, so derived data (GridSense walls) knows when to refresh.</summary>
        internal static int Version { get; private set; }

        /// <summary>Rebuilds when needed. Returns true if it rebuilt.</summary>
        internal static bool Refresh(int field, int area, bool force = false)
        {
            Il2CppReferenceArray<collision_s> array = fldHit.gfldCollision;
            int count = fldHit.gfldCollsionNum;
            IntPtr ptr = array == null ? IntPtr.Zero : array.Pointer;
            if (!force && ptr == _arrayPtr && count == _count && field == _field && area == _area && !Moved(array))
            {
                RefreshSwitches(field, area, rebuilt: false);
                return false;
            }

            _arrayPtr = ptr;
            _count = count;
            _field = field;
            _area = area;
            Items.Clear();
            Version++;
            SourceCount = count;
            SourceBlockCount = fldHit.gfldCollsionBlkNum;
            ArrayLength = array == null ? 0 : array.Length;
            if (array == null)
                return true;

            int n = Math.Min(count, array.Length);
            for (int i = 0; i < n; i++)
            {
                try
                {
                    collision_s c = array[i];
                    if (c != null)
                        Items.Add(Copy(i, c));
                }
                catch (Exception ex)
                {
                    Log.Warn($"[Nav] Collision polygon {i} could not be read: {ex.Message}");
                    break;
                }
            }
            RefreshSwitches(field, area, rebuilt: true);
            return true;
        }

        // ---- Polygons that move (0.0.65) ----
        // The copy was rebuilt only when the array or the count changed. The cube hall of the Assembly of Nihilo (field 20 area 17,
        // 9 Oct 2026) has 450-unit cubes and a switch event beside one: if the game moves polygons in place, the walking map would
        // keep the old shape. Every MoveCheckMs the first point of each polygon is compared with the copy; any change rebuilds,
        // logged. Not yet seen moving: the log line will tell.
        private const long MoveCheckMs = 1000;
        private static long _moveCheckMs = long.MinValue / 2;

        private static bool Moved(Il2CppReferenceArray<collision_s> array)
        {
            long now = Clock.ElapsedMilliseconds;
            if (array == null || now - _moveCheckMs < MoveCheckMs)
                return false;
            _moveCheckMs = now;
            try
            {
                foreach (CollisionPoly p in Items)
                {
                    if (p.Points.Length == 0 || p.Index >= array.Length)
                        continue;
                    Il2CppStructArray<Vector4> pts = array[p.Index]?.point;
                    if (pts == null || pts.Length == 0)
                        continue;
                    Vector4 a = pts[0], b = p.Points[0];
                    if (Math.Abs(a.x - b.x) > 1f || Math.Abs(a.y - b.y) > 1f || Math.Abs(a.z - b.z) > 1f)
                    {
                        Log.Info($"[Nav] Collision polygon {p.Index} moved from {FieldData.F(b.x)} {FieldData.F(b.y)} {FieldData.F(b.z)} to {FieldData.F(a.x)} {FieldData.F(a.y)} {FieldData.F(a.z)}: the walking map is rebuilt.");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav] Collision move check: {ex.Message}");
            }
            return false;
        }

        // ---- The game's own switches ----

        // fldHit.fldHitChkUnit (read offline, 2 Oct 2026) skips a polygon when fldGlobal.fldGbChkHit(fieldID, areaID + 1,
        // blockID) is not 0, or, for hojiID above 0, when fldGbChkNpc(fieldID, areaID + 1, hojiID) is not 0. The annex gate
        // (field 15 area 2, block 1, x 250 to 350 across the walkway) stays in the collision copy after the Annex Gate Pass
        // opens it; the walking map took it for a wall, so the Soul and the door past it were "not reachable" (13:58 session).
        private static readonly HashSet<int> OffBlocks = new HashSet<int>(), OffHojis = new HashSet<int>();
        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        private static long _switchReadMs = long.MinValue / 2;
        private static string _switchKey = "";
        private const long SwitchPollMs = 500;

        /// <summary>The game ignores this polygon now (its block's hit is off, or its held NPC is not placed).</summary>
        internal static bool SwitchedOff(CollisionPoly p)
            => OffBlocks.Contains(p.BlockId) || (p.HojiId > 0 && OffHojis.Contains(p.HojiId));

        /// <summary>
        /// Asks the game's two checks for every block and hoji id in the copy: after a rebuild at once, otherwise at most every
        /// SwitchPollMs. A change counts as a collision change (Version), so the walking map and the wall cues refresh.
        /// </summary>
        private static void RefreshSwitches(int field, int area, bool rebuilt)
        {
            long now = Clock.ElapsedMilliseconds;
            if (!rebuilt && now - _switchReadMs < SwitchPollMs)
                return;
            _switchReadMs = now;
            OffBlocks.Clear();
            OffHojis.Clear();
            try
            {
                Seen.Clear();
                foreach (CollisionPoly p in Items)
                    if (Seen.Add(p.BlockId) && fldGlobal.fldGbChkHit(field, area + 1, p.BlockId) != 0)
                        OffBlocks.Add(p.BlockId);
                Seen.Clear();
                foreach (CollisionPoly p in Items)
                    if (p.HojiId > 0 && Seen.Add(p.HojiId) && fldGlobal.fldGbChkNpc(field, area + 1, p.HojiId) != 0)
                        OffHojis.Add(p.HojiId);
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav] Collision switches could not be read: {ex.Message}");
            }
            string key = $"blocks off [{string.Join(" ", OffBlocks)}], held NPCs off [{string.Join(" ", OffHojis)}]";
            if (key == _switchKey)
                return;
            _switchKey = key;
            if (!rebuilt)
                Version++;
            Log.Info($"[Nav] Collision switches in field {field} area {area}: {key}.");
        }

        private static CollisionPoly Copy(int index, collision_s c)
        {
            var p = new CollisionPoly
            {
                Index = index,
                Mode2D = c.mode2D,
                NumEdges = c.numedges,
                BlockId = c.blockID,
                HitFlag = c.hitflag,
                ResId = c.resID,
                HojiId = c.hojiID,
                NpcId = c.npcID,
                Normal = c.normal,
                Elv = c.elv,
                Num = c.num,
                Min = Floats(c.min),
                Max = Floats(c.max),
                EdgeNums = Floats(c.e_num),
            };

            Il2CppStructArray<Vector4> points = c.point;
            if (points != null)
            {
                p.PointArrayLength = points.Length;
                int k = p.NumEdges > 0 ? Math.Min(p.NumEdges, points.Length) : points.Length;
                p.Points = new Vector4[k];
                for (int j = 0; j < k; j++)
                    p.Points[j] = points[j];
            }

            Il2CppStructArray<Vector4> edges = c.e_normal;
            if (edges != null)
            {
                int k = p.NumEdges > 0 ? Math.Min(p.NumEdges, edges.Length) : edges.Length;
                p.EdgeNormals = new Vector4[k];
                for (int j = 0; j < k; j++)
                    p.EdgeNormals[j] = edges[j];
            }

            fldResCollisionPolyFace_t face = c.pFace;
            if (face != null)
            {
                p.HasFace = true;
                p.Attr = face.Attr;
                p.Myuka = face.Myuka;
                p.Se = face.Se;
                p.Basyo = face.Basyo;
                p.Amap0 = face.Amap0;
                p.Amap1 = face.Amap1;
                p.TomeIdx = face.TomeIdx;
                p.EncType = face.EncType;
                p.EncIdx = face.EncIdx;
                p.Spc1 = face.Spc1;
                p.Spc2 = face.Spc2;
            }
            return p;
        }

        private static float[] Floats(Il2CppStructArray<float> a)
        {
            if (a == null)
                return Array.Empty<float>();
            var r = new float[a.Length];
            for (int i = 0; i < r.Length; i++)
                r[i] = a[i];
            return r;
        }

        /// <summary>
        /// The floor-like polygon whose outline contains (x, z) and whose plane is nearest to y, or null.
        /// Assumes the polygon points are in the same space as fldGb.playerX/Y/Z: the diagnostic run checks that.
        /// </summary>
        internal static CollisionPoly FloorUnder(float x, float y, float z, out float planeY, bool groundOnly = false, bool upOnly = false)
        {
            CollisionPoly best = null;
            float bestDist = float.MaxValue;
            planeY = 0;
            foreach (CollisionPoly p in Items)
            {
                if (!p.IsFloorLike || p.Points.Length < 3 || !ContainsXZ(p.Points, x, z) || (groundOnly && !p.IsGroundAttr) || (upOnly && p.Normal.y >= 0f))
                    continue;   // groundOnly: the walking map's floors, not the 0x800 / 0x1000 / 0x8000 overlays; upOnly: faces looking up
                                // (Y points down), not the undersides of blocks (0.0.65)

                Vector4 a = p.Points[0];
                float py = a.y - (p.Normal.x * (x - a.x) + p.Normal.z * (z - a.z)) / p.Normal.y;
                float d = Math.Abs(py - y);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                    planeY = py;
                }
            }
            return best;
        }

        /// <summary>Even-odd point-in-polygon test on the X and Z coordinates.</summary>
        internal static bool ContainsXZ(Vector4[] pts, float x, float z)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                float zi = pts[i].z, zj = pts[j].z;
                if ((zi > z) != (zj > z))
                {
                    float xCross = (pts[j].x - pts[i].x) * (z - zi) / (zj - zi) + pts[i].x;
                    if (x < xCross)
                        inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>Horizontal distance from (x, z) to the polygon's outline (0 when inside).</summary>
        internal static float DistanceXZ(CollisionPoly p, float x, float z)
        {
            if (p.Points.Length == 0)
                return float.MaxValue;
            if (p.Points.Length >= 3 && ContainsXZ(p.Points, x, z))
                return 0;

            float best = float.MaxValue;
            for (int i = 0, j = p.Points.Length - 1; i < p.Points.Length; j = i++)
            {
                float d = SegmentDistance(x, z, p.Points[j].x, p.Points[j].z, p.Points[i].x, p.Points[i].z);
                if (d < best)
                    best = d;
            }
            return best;
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
