using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2Cppbasic_H;
using Il2Cppfield_H;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NocturneAccess.Core;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>One warp of the current area: a door volume that moves the player to a point of the same area.</summary>
    internal sealed class Warp
    {
        public int Index;                 // fldEveHit.gEvnRng index of its volume
        public string Name = "";          // the volume's name, "01d_03"
        public int MoveType;              // door table movetype: 2 fires when walked into
        public string DestName = "";      // the arrival point's name, "01pos_02"
        public WalkGrid.Zone Zone;
        public float FloorY;              // the volume's floor (box bottom; Y points down)
        public bool HasDest;
        public float DestX, DestY, DestZ;
        public string DestFrom = "";      // how the arrival point was found, for the log
        public bool Touch => MoveType == 2;
        public bool Ladder => MoveType == 5;
        /// <summary>The mod's word for it: "warp" (fires when walked into), "ladder" (movetype 5), else "door" (confirmed).</summary>
        public string Word => Touch ? "warp" : Ladder ? "ladder" : "door";
    }

    /// <summary>
    /// The current area's warps (0.0.52; Francesco, 4 Oct 2026: the Amala Network's hidden warps made auto-walk loop and left
    /// whole parts of an area out of the Page Up list). Read as data only, offline first:
    /// - which door volumes warp, and where to: the door table (DoorTable.SameAreaWarp: warptype 0 to this field and area,
    ///   arriving at warp_pos); movetype 2 fires as soon as the player walks in (no door model);
    /// - the arrival point: the resource of that name in the loaded area file. fldFileResolver.pF2Head (an
    ///   fldFileInfoHeaderFL2_t) holds pResourceTypeTable[], each fldResourceTypeTable_t a list of fldResHead_t
    ///   (resourceName, sisei.pos), the same records as fldEveHit.gEvnRng; pF1Head (fldFileInfoHeaderFL1_t) is searched too.
    ///   Checked against the 15:56 log (area 3): 01d_03 leads to 01pos_02 and the player landed at 2600 -1260, beside
    ///   01d_02, whose entry leads back to 01pos_03, where the player had stood (3960 -2610).
    /// - each volume's shape: gEvnRngBox's corners (vtx) and facing (boxnml), as PointsOfInterest reads them.
    /// Touch warps are kept out of routes (WalkGrid); RouteTo chains warps when a target is reachable only through them.
    /// </summary>
    internal static class WarpMap
    {
        private const string Tag = "[Nav:Warp]";
        private const float EntryMargin = 60f;    // the walkable ring around a warp's volume it is entered from
        private static readonly Regex DoorName = FieldData.DoorVolume;

        private static int _field = int.MinValue, _area = int.MinValue, _count = -1;
        private static IntPtr _ranges;
        private static List<Warp> _warps = new List<Warp>();
        private static readonly HashSet<string> Logged = new HashSet<string>();

        /// <summary>The current area's warps with a destination in this area, rebuilt when the area or its volumes change.</summary>
        internal static List<Warp> All(int field, int area)
        {
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            IntPtr ptr = ranges == null ? IntPtr.Zero : ranges.Pointer;
            int n = ranges == null ? 0 : Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            if (field == _field && area == _area && ptr == _ranges && n == _count)
                return _warps;
            _field = field;
            _area = area;
            _ranges = ptr;
            _count = n;
            _warps = new List<Warp>();
            if (FieldState.WorldMapField(field))
                return _warps;
            try
            {
                Il2CppReferenceArray<putevebox_s> boxes = fldEveHit.gEvnRngBox;
                for (int i = 0; i < n; i++)
                {
                    fldResHead_t h = ranges[i];
                    string name = h?.resourceName ?? "";
                    if (h == null || h.sisei == null || h.resourceType != 10 || !DoorName.IsMatch(name))
                        continue;
                    if (!DoorTable.SameAreaWarp(field, area, name, out int moveType, out string pos))
                        continue;
                    var w = new Warp { Index = i, Name = name, MoveType = moveType, DestName = pos };
                    putevebox_s box = boxes != null && i < boxes.Length ? boxes[i] : null;
                    w.Zone = ZoneOf(box, h.sisei.pos, i, out w.FloorY);
                    if (FindPoint(pos, out Vector4 p, out string from))
                    {
                        w.HasDest = true;
                        w.DestX = p.x;
                        w.DestY = p.y;
                        w.DestZ = p.z;
                        w.DestFrom = from;
                    }
                    _warps.Add(w);
                }
                foreach (Warp w in _warps.Where(w => w.Ladder && !w.HasDest))
                    LandBesidePartner(w);
                string key = $"{field}/{area}/{n}";
                if (_warps.Count > 0 && Logged.Add(key))
                    Log.Info($"{Tag} Field {field} area {area}: {_warps.Count} warps: " + string.Join("; ", _warps.Select(w =>
                        $"{w.Name} (movetype {w.MoveType}) at {FieldData.F(w.Zone.X)} {FieldData.F(w.Zone.Z)} " +
                        $"{FieldData.F(w.Zone.HalfWidth * 2)} x {FieldData.F(w.Zone.HalfDepth * 2)} -> {w.DestName} " +
                        (w.HasDest ? $"at {FieldData.F(w.DestX)} {FieldData.F(w.DestY)} {FieldData.F(w.DestZ)} ({w.DestFrom})" : "NOT FOUND"))));
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} Warps of {field}/{area} could not be read: {ex.Message}");
            }
            return _warps;
        }

        // A ladder's two ends are two movetype 5 volumes at one spot, one per floor (Ginza underpass area 8: 01d_12 at -9300 -200 on
        // the floor y 0, 01d_16 above it on y -800). Climbing one lands LadderLanding in front of the other, along its facing:
        // 01d_12 to -9300 -802 -50 (01d_16 faces +z), 01d_13 down to -3900 -2 2450 (01d_09 at z 2600 faces -z); 21:36 session,
        // 5 Oct 2026. The arrival points (01pos_24, 01pos_05) are not readable in game.
        private const float LadderLanding = 150f, LadderPartnerReach = 250f, LadderFloorGap = 300f;

        /// <summary>A ladder whose arrival point was not found lands in front of its other end (partner on another floor, same spot).</summary>
        private static void LandBesidePartner(Warp w)
        {
            Warp partner = _warps
                .Where(o => o != w && o.Ladder && Math.Abs(o.FloorY - w.FloorY) >= LadderFloorGap
                            && Math.Abs(o.Zone.X - w.Zone.X) <= LadderPartnerReach && Math.Abs(o.Zone.Z - w.Zone.Z) <= LadderPartnerReach)
                .OrderBy(o => Math.Abs(o.Zone.X - w.Zone.X) + Math.Abs(o.Zone.Z - w.Zone.Z))
                .FirstOrDefault();
            if (partner == null)
                return;
            w.HasDest = true;
            w.DestX = partner.Zone.X + partner.Zone.NX * LadderLanding;
            w.DestZ = partner.Zone.Z + partner.Zone.NZ * LadderLanding;
            w.DestY = partner.FloorY;
            w.DestFrom = $"in front of {partner.Name}";
        }

        /// <summary>"1 ladder", "2 ladders and 1 warp": the legs of a route by kind, in the order first met.</summary>
        internal static string Phrase(List<Warp> legs)
        {
            var parts = legs.GroupBy(w => w.Word).Select(g => $"{g.Count()} {g.Key}{(g.Count() == 1 ? "" : "s")}").ToList();
            return parts.Count <= 1 ? string.Concat(parts) : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }

        /// <summary>The warp whose volume is gEvnRng[index] in the current area (any kind), or null.</summary>
        internal static Warp At(fldGlobalWork_t gb, int index)
            => gb == null ? null : All(gb.fieldID, gb.areaID).FirstOrDefault(w => w.Index == index);

        /// <summary>Forgets the cached warps and points (a destination was learned, or a save file loaded).</summary>
        internal static void Invalidate()
        {
            _count = -1;
            _basicPoints = null;
        }

        /// <summary>The volumes routes keep out of: the warps that fire when walked into.</summary>
        internal static List<WalkGrid.Zone> Zones(int field, int area) => All(field, area).Where(w => w.Touch).Select(w => w.Zone).ToList();

        /// <summary>The touch warp whose volume is gEvnRng[index] in the current area, or null.</summary>
        internal static Warp TouchWarp(fldGlobalWork_t gb, int index)
            => gb == null ? null : All(gb.fieldID, gb.areaID).FirstOrDefault(w => w.Index == index && w.Touch);

        /// <summary>
        /// The warps to take, in order, to reach the target: an empty list when it can be walked to directly, null when not even
        /// through warps. Regions are the walk grid's connected parts with every touch warp kept out; a warp joins the regions
        /// around its volume to the region of its arrival point. Breadth first: the fewest warps.
        /// </summary>
        internal static List<Warp> RouteTo(fldGlobalWork_t gb, Poi target)
        {
            try
            {
                if (gb == null || target == null || !WalkGrid.Ensure(gb.fieldID, gb.areaID, gb.playerX, gb.playerY, gb.playerZ))
                    return null;
                int start = WalkGrid.RegionAt(gb.playerX, gb.playerY, gb.playerZ);
                if (start < 0)
                    return null;
                HashSet<int> goal = TargetRegions(gb, target);
                if (goal.Contains(start))
                    return new List<Warp>();
                Graph g = GraphOf(gb);
                List<Warp> warps = g.Warps;
                if (warps.Count == 0 || goal.Count == 0)
                    return null;
                Dictionary<Warp, HashSet<int>> entries = g.Entries;
                Dictionary<Warp, int> dests = g.Dests;
                var prev = new Dictionary<int, (int from, Warp via)>();
                var queue = new Queue<int>();
                prev[start] = (-1, null);
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int r = queue.Dequeue();
                    foreach (Warp w in warps)
                    {
                        int d = dests[w];
                        if (d < 0 || prev.ContainsKey(d) || !entries[w].Contains(r))
                            continue;
                        prev[d] = (r, w);
                        if (goal.Contains(d))
                        {
                            var legs = new List<Warp>();
                            for (int at = d; prev[at].via != null; at = prev[at].from)
                                legs.Add(prev[at].via);
                            legs.Reverse();
                            return legs;
                        }
                        queue.Enqueue(d);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} Route to {target?.ResName} could not be worked out: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// The walk grid's regions joined by this area's warps: every warp with a known arrival (touch warps, and from 0.0.55 ladders
        /// and doors the player confirms: the Ginza underpass joins its rooms by ladders inside one area, Francesco 5 Oct 2026),
        /// the regions each is entered from and the region it lands in. Built once per grid and warp list.
        /// </summary>
        private sealed class Graph
        {
            public List<Warp> Warps = new List<Warp>();
            public Dictionary<Warp, HashSet<int>> Entries = new Dictionary<Warp, HashSet<int>>();
            public Dictionary<Warp, int> Dests = new Dictionary<Warp, int>();
            public int Builds = -1;
            public List<Warp> Source;
        }

        private static Graph _graph = new Graph();

        private static Graph GraphOf(fldGlobalWork_t gb)
        {
            List<Warp> all = All(gb.fieldID, gb.areaID);
            if (_graph.Builds == WalkGrid.Builds && ReferenceEquals(_graph.Source, all))
                return _graph;
            var g = new Graph { Builds = WalkGrid.Builds, Source = all, Warps = all.Where(w => w.HasDest).ToList() };
            foreach (Warp w in g.Warps)
            {
                g.Entries[w] = WalkGrid.RegionsAround(w.Zone, w.FloorY, EntryMargin);
                g.Dests[w] = WalkGrid.RegionAt(w.DestX, w.DestY, w.DestZ);
            }
            _graph = g;
            return g;
        }

        /// <summary>The regions reachable from the given ones, walking and taking every warp, ladder or door of the area with a known arrival.</summary>
        internal static HashSet<int> Closure(fldGlobalWork_t gb, HashSet<int> from)
        {
            var seen = new HashSet<int>(from);
            Graph g = GraphOf(gb);
            var queue = new Queue<int>(from);
            while (queue.Count > 0)
            {
                int r = queue.Dequeue();
                foreach (Warp w in g.Warps)
                {
                    int d = g.Dests[w];
                    if (d < 0 || seen.Contains(d) || !g.Entries[w].Contains(r))
                        continue;
                    seen.Add(d);
                    queue.Enqueue(d);
                }
            }
            return seen;
        }

        /// <summary>The regions a walk to the target can end in: around a warp's volume, or near the target.</summary>
        internal static HashSet<int> TargetRegions(fldGlobalWork_t gb, Poi target)
        {
            // Any warp, ladder or same-area door: the ring around its volume (Ginza area 2, 6 Oct 2026: door 01d_08 into the room
            // with the overworld stairs sits in a thick wall, no walkable cell within 120 of its centre, so it and the exit beyond
            // were "not reachable", 0.0.58).
            Warp w = target.IsDoorLike ? At(gb, target.Index) : null;
            if (w != null)
                return WalkGrid.RegionsAround(w.Zone, w.FloorY, EntryMargin);
            float radius = target.IsDoorLike ? Math.Max(target.ReachRadius, 120f) : target.ReachRadius;
            return WalkGrid.RegionsNear(target.X, target.FloorY, target.Z, radius);
        }

        /// <summary>A volume's shape in X and Z from its box corners and facing; without a box, 100 around its centre.</summary>
        private static WalkGrid.Zone ZoneOf(putevebox_s box, Vector4 centre, int id, out float floorY)
        {
            var zone = new WalkGrid.Zone { Id = id, X = centre.x, Z = centre.z, NX = 0f, NZ = 1f, HalfDepth = 100f, HalfWidth = 100f };
            floorY = centre.y + 100f;
            Il2CppStructArray<Vector4> vtx = box?.vtx;
            if (vtx == null || vtx.Length == 0)
                return zone;
            float sx = 0f, sz = 0f, bottom = float.MinValue;
            for (int k = 0; k < vtx.Length; k++)
            {
                sx += vtx[k].x;
                sz += vtx[k].z;
                bottom = Math.Max(bottom, vtx[k].y);
            }
            zone.X = sx / vtx.Length;
            zone.Z = sz / vtx.Length;
            floorY = bottom;
            Vector4 n = box.boxnml;
            float len = (float)Math.Sqrt(n.x * n.x + n.z * n.z);
            if (len > 0.5f && Math.Abs(n.y) < 0.5f)
            {
                zone.NX = n.x / len;
                zone.NZ = n.z / len;
            }
            float hd = 0f, hw = 0f;
            for (int k = 0; k < vtx.Length; k++)
            {
                float dx = vtx[k].x - zone.X, dz = vtx[k].z - zone.Z;
                hd = Math.Max(hd, Math.Abs(dx * zone.NX + dz * zone.NZ));
                hw = Math.Max(hw, Math.Abs(dz * zone.NX - dx * zone.NZ));
            }
            if (hd + hw > 1f)
            {
                zone.HalfDepth = hd;
                zone.HalfWidth = hw;
            }
            return zone;
        }

        /// <summary>The position of the area file's resource named name (an arrival point, "01pos_02").</summary>
        private static bool FindPoint(string name, out Vector4 pos, out string from)
        {
            pos = default;
            from = "area file";
            var found = new List<(uint type, Vector4 p)>();
            foreach (var (_, tables) in TypeTables())
            {
                for (int t = 0; tables != null && t < tables.Length; t++)
                {
                    fldResourceTypeTable_t table = tables[t];
                    Il2CppReferenceArray<fldResHead_t> data = table?.pResourceDataTable;
                    int count = data == null ? 0 : Math.Min((int)table.nResourceDataTable, data.Length);
                    for (int k = 0; k < count; k++)
                    {
                        fldResHead_t h = data[k];
                        if (h?.sisei != null && h.resourceName == name)
                            found.Add((table.type, h.sisei.pos));
                    }
                }
            }
            from = "game objects";
            if (found.Count == 0 && FindBasic(name, out pos))
                return true;
            from = "learned";
            if (found.Count == 0)
                return ExploredDoors.LearnedArrival(_field, _area, name, out pos);
            from = "area file";
            if (found.Count > 1 && Logged.Add("dup:" + name))
                Log.Info($"{Tag} {found.Count} resources named {name}: " + string.Join("; ", found.Select(f => $"type {f.type} at {FieldData.F(f.p.x)} {FieldData.F(f.p.z)}")) + "; the first is used.");
            pos = found[0].p;
            return true;
        }

        /// <summary>
        /// The game registers each named point of a field as a "basic" (dds3PointBasic.dds3AddPointBasic(serialID, point, name)),
        /// linked to the others (dds3Basic_t next, prev, worknext, workprev, workpointer) under the world
        /// (dds3WorldBasic.dds3TheWorld, the active scene dds3GetActiveScene). 0.0.53: the area file's resource tables were not
        /// reachable in game (Ginza, 21:49 dump: no table at all), so the basics are searched, reading their links and names
        /// only, up to MaxBasics; the position is the basic's poseset.pos. Logged once per area for checking.
        /// </summary>
        private static Dictionary<string, Vector4> _basicPoints;
        private static string _basicKey = "";

        private static bool FindBasic(string name, out Vector4 pos)
        {
            pos = default;
            string areaKey = $"{_field}/{_area}";
            if (_basicPoints == null || _basicKey != areaKey)
            {
                _basicKey = areaKey;
                _basicPoints = ReadBasics(out int searched, out int named, out List<string> sample);
                Log.Info($"{Tag} Field {_field} area {_area}: {searched} of the game's objects searched, {named} named, {_basicPoints.Count} points; names with \"pos\": {string.Join(", ", sample)}.");
            }
            return _basicPoints.TryGetValue(name, out pos);
        }

        private static Dictionary<string, Vector4> ReadBasics(out int searched, out int named, out List<string> sample)
        {
            const int MaxBasics = 20000;
            var points = new Dictionary<string, Vector4>();
            var seen = new HashSet<IntPtr>();
            var queue = new Queue<dds3Basic_t>();
            named = 0;
            sample = new List<string>();
            try
            {
                Enqueue(queue, seen, dds3WorldBasic.dds3TheWorld);
                Enqueue(queue, seen, dds3WorldBasic.dds3GetActiveScene());
                Enqueue(queue, seen, dds3WorldBasic.dds3GetControlScene());
                while (queue.Count > 0 && seen.Count < MaxBasics)
                {
                    dds3Basic_t b = queue.Dequeue();
                    string n = b.name ?? "";
                    if (n.Length > 0)
                    {
                        named++;
                        if (n.Contains("pos") && b.poseset != null && !points.ContainsKey(n))
                        {
                            points[n] = b.poseset.pos;
                            if (sample.Count < 30)
                                sample.Add($"{n} {FieldData.F(b.poseset.pos.x)} {FieldData.F(b.poseset.pos.z)}");
                        }
                    }
                    Enqueue(queue, seen, b.next);
                    Enqueue(queue, seen, b.prev);
                    Enqueue(queue, seen, b.worknext);
                    Enqueue(queue, seen, b.workprev);
                    Enqueue(queue, seen, b.workpointer);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} The game's objects could not be searched: {ex.Message}");
            }
            searched = seen.Count;
            return points;
        }

        private static void Enqueue(Queue<dds3Basic_t> queue, HashSet<IntPtr> seen, dds3Basic_t b)
        {
            if (b != null && b.Pointer != IntPtr.Zero && seen.Add(b.Pointer))
                queue.Enqueue(b);
        }

        private static IEnumerable<(string label, Il2CppReferenceArray<fldResourceTypeTable_t> tables)> TypeTables()
        {
            Il2CppReferenceArray<fldResourceTypeTable_t> f2 = null, f1 = null;
            try
            {
                f2 = fldFileResolver.pF2Head?.TryCast<fldFileInfoHeaderFL2_t>()?.pResourceTypeTable;
            }
            catch
            {
            }
            try
            {
                f1 = fldFileResolver.pF1Head?.TryCast<fldFileInfoHeaderFL1_t>()?.pResourceTypeTable;
            }
            catch
            {
            }
            if (f2 != null)
                yield return ("pF2Head", f2);
            if (f1 != null)
                yield return ("pF1Head", f1);
        }

        /// <summary>For the navigation dump: the area file's resource tables (type, count, the first names).</summary>
        internal static IEnumerable<string> DescribeTables()
        {
            foreach ((string label, Il2CppReferenceArray<fldResourceTypeTable_t> tables) in TypeTables())
            {
                for (int t = 0; tables != null && t < tables.Length; t++)
                {
                    fldResourceTypeTable_t table = tables[t];
                    Il2CppReferenceArray<fldResHead_t> data = table?.pResourceDataTable;
                    int count = data == null ? 0 : Math.Min((int)table.nResourceDataTable, data.Length);
                    var names = new List<string>();
                    for (int k = 0; k < count && names.Count < 40; k++)
                        if (data[k] != null)
                            names.Add($"{data[k].resourceName} {FieldData.F(data[k].sisei?.pos.x ?? 0)} {FieldData.F(data[k].sisei?.pos.z ?? 0)}");
                    yield return $"  {label} table {t}: type {table?.type}, {count} resources: {string.Join("; ", names)}";
                }
            }
        }
    }
}
