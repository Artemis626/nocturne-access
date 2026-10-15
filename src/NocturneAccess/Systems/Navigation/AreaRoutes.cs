using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>A way to a point of the current area through other areas: the doors to go through, in order (AreaRoutes).</summary>
    internal sealed class AreaRoute
    {
        public List<string> Exits = new List<string>();                   // door keys "field/area/name"; the first is in the current area
        public int FirstIndex = -1;                                       // gEvnRng index of the first door, in the current area
        public HashSet<string> GoalDoors = new HashSet<string>();         // doors of the target's area from which it is reached on foot
        public int Areas => Exits.Count - 1;                              // other areas on the way
        public string Phrase => Areas <= 0 ? "1 door" : Areas == 1 ? "1 other area" : $"{Areas} other areas";
    }

    /// <summary>
    /// Routes through other areas (0.0.56, F7; Francesco, 6 Oct 2026: "routes via previously walked doors", toggleable). Some areas
    /// are split into parts joined only through other areas: in the Great Underpass of Ginza (field 22) area 4 has 01d_02 and
    /// 01d_03 on one part and 01d_01 on another, and the Page Up list left 01d_01 out ("not reachable") while the player stood on
    /// the first part (5 Oct 2026, 20:54). Only what the player has seen is used, so nothing is offered that the game may refuse:
    /// - the ways through doors: the door left and the door come in through after an area change (ExploredDoors), and back the
    ///   same way when the game's door table says the door come in through leads back to the area left (a one-way drop does not);
    /// - each visited area's door links: which of its doors can be walked to from which, from that area's walk grid (with its
    ///   warps, ladders and same-area doors, WarpMap.Closure), learned on entering it.
    /// A point the player cannot walk to is listed when a chain of those leads back into its part of the area: "Door, passage,
    /// 12 metres, east, via 1 other area". Home walks door by door (the player goes through each; the walk goes on in the next
    /// area), End gives the way to the first door. Kept per save file with the explored doors ("pass" and "reach" lines).
    /// Within one field only. F7 turns the routes off and on (config AreaRoutes); learning goes on either way.
    /// </summary>
    internal static class AreaRoutes
    {
        private const string Tag = "[Nav:Routes]";
        private const int MaxExits = 12;
        private const long LearnAfterMs = 1500;   // in a new area, once the player has had control this long

        private static readonly Dictionary<string, HashSet<string>> Passes = new Dictionary<string, HashSet<string>>();
        private static readonly Dictionary<string, HashSet<string>> Reach = new Dictionary<string, HashSet<string>>();
        // Landings of jumps (0.0.82): "field/area/J3-01eve_03" -> where the player landed after jumping off 25/3/01eve_03; a jump's
        // "pass" leads to its landing, and the landing's "reach" lists the doors walked to from it. One way: nothing leads back.
        private static readonly Dictionary<string, UnityEngine.Vector3> Landings = new Dictionary<string, UnityEngine.Vector3>();
        private static readonly Dictionary<string, HashSet<int>> LandingClosures = new Dictionary<string, HashSet<int>>();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        // The current area's door regions and closures, once per walk grid.
        private static readonly Dictionary<int, HashSet<int>> DoorRegions = new Dictionary<int, HashSet<int>>();
        private static readonly Dictionary<int, HashSet<int>> Closures = new Dictionary<int, HashSet<int>>();
        private static string _learnedKey = "";

        internal static bool Enabled => Config.AreaRoutes?.Value ?? false;

        internal static string Summary => $"{Passes.Sum(p => p.Value.Count)} ways through doors, door links of {Reach.Keys.Select(AreaOf).Distinct().Count()} areas";

        // ---- Keeping ----

        internal static void Clear()
        {
            Passes.Clear();
            Reach.Clear();
            Landings.Clear();
            LandingClosures.Clear();
            DoorRegions.Clear();
            Closures.Clear();
            _learnedKey = "";
            _tickKey = "";
        }

        /// <summary>A line of the explored file: "pass f/a/door f/a/door" or "reach f/a/door name name...". Others are ignored.</summary>
        internal static void ReadLine(string line)
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 && parts[0] == "pass")
                Add(Passes, parts[1], parts[2]);
            else if (parts.Length >= 2 && parts[0] == "reach")
                Reach[parts[1]] = new HashSet<string>(parts.Skip(2));   // the latest line wins
            else if (parts.Length == 5 && parts[0] == "land" && Parse(parts[1], out _, out _, out _)
                     && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                     && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                     && float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                Landings[parts[1]] = new UnityEngine.Vector3(x, y, z);
        }

        internal static IEnumerable<string> Lines()
            => Passes.OrderBy(p => p.Key, StringComparer.Ordinal).SelectMany(p => p.Value.OrderBy(t => t, StringComparer.Ordinal).Select(t => $"pass {p.Key} {t}"))
                .Concat(Reach.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => ReachLine(p.Key, p.Value)))
                .Concat(Landings.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => LandLine(p.Key, p.Value)));

        private static string LandLine(string key, UnityEngine.Vector3 p)
            => string.Format(CultureInfo.InvariantCulture, "land {0} {1} {2} {3}", key, p.x, p.y, p.z);

        private static string ReachLine(string key, HashSet<string> names) => $"reach {key} {string.Join(" ", names.OrderBy(n => n, StringComparer.Ordinal))}".TrimEnd();

        private static bool Add(Dictionary<string, HashSet<string>> map, string key, string value)
        {
            if (!map.TryGetValue(key, out HashSet<string> set))
                map[key] = set = new HashSet<string>();
            return set.Add(value);
        }

        private static bool Parse(string key, out int field, out int area, out string name)
        {
            field = area = -1;
            name = "";
            string[] p = key.Split('/');
            return p.Length == 3 && int.TryParse(p[0], out field) && int.TryParse(p[1], out area) && (name = p[2]).Length > 0;
        }

        private static string AreaOf(string key) => key.Substring(0, Math.Max(0, key.LastIndexOf('/')));

        /// <summary>
        /// ExploredDoors: the player left through door "from" and came in through door "to". Also the way back, when the game's door
        /// table says "to" leads to the area of "from".
        /// </summary>
        internal static void LearnPass(string from, string to)
        {
            if (from == null || to == null || from == to || !Parse(from, out int ff, out int fa, out _) || !Parse(to, out int tf, out int ta, out string tn))
                return;
            if (Add(Passes, from, to))
            {
                Log.Info($"{Tag} Through {from} to {to}: learned.");
                ExploredDoors.Append($"pass {from} {to}", $"The way through {from}");
            }
            if (DoorTable.Leads(tf, ta, tn, out int bf, out int ba) && bf == ff && ba == fa && Add(Passes, to, from))
            {
                Log.Info($"{Tag} Back through {to} to {from}: learned (the door table leads back).");
                ExploredDoors.Append($"pass {to} {from}", $"The way through {to}");
            }
        }

        /// <summary>
        /// ExploredDoors (0.0.82): the player jumped off "jump" and landed at (x, y, z) in another area, named landing there. One way:
        /// the jump leads to the landing; the landing's door links are learned when its area is walked (Learn).
        /// </summary>
        internal static void LearnLanding(string jump, string landing, float x, float y, float z)
        {
            if (jump == null || landing == null || !Parse(jump, out _, out _, out _) || !Parse(landing, out _, out _, out _))
                return;
            bool known = Landings.ContainsKey(landing);
            Landings[landing] = new UnityEngine.Vector3(x, y, z);
            if (!known)
                ExploredDoors.Append(LandLine(landing, Landings[landing]), $"The landing {landing}");
            if (Add(Passes, jump, landing))
            {
                Log.Info($"{Tag} Jumping off {jump} lands at {FieldData.F(x)} {FieldData.F(y)} {FieldData.F(z)} ({landing}): learned.");
                ExploredDoors.Append($"pass {jump} {landing}", $"The way through {jump}");
            }
            _learnedKey = "";   // the landing's links are learned on the next build here
        }

        // ---- The current area ----

        private static string _tickKey = "";
        private static long _exploringSince = -1;

        /// <summary>Every frame: in a new area (or a rebuilt grid) the door links are learned once the player has control.</summary>
        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || !FieldState.IsExploring)
            {
                _exploringSince = -1;
                return;
            }
            string key = $"{gb.fieldID}/{gb.areaID}/{WalkGrid.Builds}";
            if (key == _tickKey)
                return;
            long now = Clock.ElapsedMilliseconds;
            if (_exploringSince < 0)
                _exploringSince = now;
            if (now - _exploringSince < LearnAfterMs)
                return;
            _tickKey = key;   // set first: a failing build is not retried every frame
            if (!FieldState.WorldMap(gb))
                PointsOfInterest.Build(gb);   // its last step calls Mark, which learns
            _tickKey = $"{gb.fieldID}/{gb.areaID}/{WalkGrid.Builds}";   // the build may have rebuilt the grid
        }

        /// <summary>
        /// Called by PointsOfInterest.Build with the area's points (reachability from the player and through warps already set):
        /// learns the area's door links, then, when routes are on, marks the points reached only through other areas.
        /// </summary>
        internal static void Mark(fldGlobalWork_t gb, List<Poi> pois)
        {
            try
            {
                List<Poi> doors = pois.Where(q => (q.Kind == PoiKind.Door && !q.Warp) || q.JumpOff).ToList();
                Learn(gb, doors);
                if (!Enabled || Passes.Count == 0)
                    return;
                List<Poi> starts = doors.Where(q => q.Reachable).ToList();
                if (starts.Count == 0)
                    return;
                foreach (Poi q in pois.Where(q => !q.Reachable))
                {
                    HashSet<int> regions = WarpMap.TargetRegions(gb, q);
                    if (regions.Count == 0)
                        continue;
                    var goal = new HashSet<string>(doors.Where(d => Closures.TryGetValue(d.Index, out HashSet<int> c) && c.Overlaps(regions)).SelectMany(d => d.Names));
                    // Or a landing of this area it is walked to from (0.0.82): jumped into from the floor above.
                    foreach (KeyValuePair<string, HashSet<int>> l in LandingClosures)
                        if (l.Value.Overlaps(regions) && Parse(l.Key, out _, out _, out string landName))
                            goal.Add(landName);
                    if (goal.Count == 0)
                        continue;
                    AreaRoute route = Search(gb.fieldID, gb.areaID, starts, gb.areaID, goal);
                    if (route == null)
                        continue;
                    q.Reachable = true;
                    q.Route = route;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} Routes in {gb.fieldID}/{gb.areaID} could not be worked out: {ex.Message}");
            }
        }

        /// <summary>
        /// The way on from an area on the route (AutoWalk, after going through a door): from the doors the player can reach here, to
        /// a door of the target's area in goalDoors. Null when none is known.
        /// </summary>
        internal static AreaRoute Onward(fldGlobalWork_t gb, List<Poi> pois, int goalArea, HashSet<string> goalDoors)
        {
            List<Poi> starts = pois.Where(q => ((q.Kind == PoiKind.Door && !q.Warp) || q.JumpOff) && q.Reachable && q.Route == null).ToList();
            return starts.Count == 0 ? null : Search(gb.fieldID, gb.areaID, starts, goalArea, goalDoors);
        }

        /// <summary>
        /// This area's door links from its walk grid: for each door, the doors reachable from it on foot and through its warps. A
        /// door outside the grid's window (areas wider than 400 cells) keeps what was learned before. Written when it changes.
        /// </summary>
        private static void Learn(fldGlobalWork_t gb, List<Poi> doors)
        {
            string key = $"{gb.fieldID}/{gb.areaID}/{WalkGrid.Builds}/{doors.Count}";
            if (key == _learnedKey)
                return;
            _learnedKey = key;
            DoorRegions.Clear();
            Closures.Clear();
            foreach (Poi d in doors)
                DoorRegions[d.Index] = WarpMap.TargetRegions(gb, d);
            foreach (Poi d in doors.Where(d => DoorRegions[d.Index].Count > 0))
                Closures[d.Index] = WarpMap.Closure(gb, DoorRegions[d.Index]);

            int changed = 0;
            var groups = new List<string>();
            foreach (Poi d in doors)
            {
                if (!Closures.TryGetValue(d.Index, out HashSet<int> closure))
                    continue;
                var names = new HashSet<string>(doors.Where(e => e == d || DoorRegions[e.Index].Overlaps(closure)).SelectMany(e => e.Names));
                foreach (Poi e in doors.Where(e => DoorRegions[e.Index].Count == 0))
                    foreach (string n in e.Names)
                        if (Reach.TryGetValue(ExploredDoors.Key(gb.fieldID, gb.areaID, d.ResName), out HashSet<string> old) && old.Contains(n))
                            names.Add(n);
                string group = string.Join(" ", names.OrderBy(n => n, StringComparer.Ordinal));
                if (!groups.Contains(group))
                    groups.Add(group);
                foreach (string n in d.Names)
                {
                    string k = ExploredDoors.Key(gb.fieldID, gb.areaID, n);
                    if (Reach.TryGetValue(k, out HashSet<string> old) && old.SetEquals(names))
                        continue;
                    Reach[k] = new HashSet<string>(names);
                    ExploredDoors.Append(ReachLine(k, names), $"The door links of {k}");
                    changed++;
                }
            }
            // The landings of jumps into this area (0.0.82): the doors and jumps walked to from each.
            LandingClosures.Clear();
            string prefix = $"{gb.fieldID}/{gb.areaID}/";
            foreach (KeyValuePair<string, UnityEngine.Vector3> l in Landings.Where(l => l.Key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                HashSet<int> regions = WalkGrid.RegionsNear(l.Value.x, l.Value.y, l.Value.z, LandingReach);
                if (regions.Count == 0)
                    continue;
                HashSet<int> closure = WarpMap.Closure(gb, regions);
                LandingClosures[l.Key] = closure;
                var names = new HashSet<string>(doors.Where(e => DoorRegions[e.Index].Overlaps(closure)).SelectMany(e => e.Names));
                if (Reach.TryGetValue(l.Key, out HashSet<string> old) && old.SetEquals(names))
                    continue;
                Reach[l.Key] = names;
                ExploredDoors.Append(ReachLine(l.Key, names), $"The links of the landing {l.Key}");
                changed++;
            }
            if (changed > 0)
                Log.Info($"{Tag} Field {gb.fieldID} area {gb.areaID}: {doors.Count} doors, {changed} links learned; parts: {string.Join(" | ", groups)}.");
        }

        private const float LandingReach = 90f;   // the walk grid's regions this close to where the player landed

        /// <summary>
        /// Breadth first over "standing at a door": from the start doors (current area, reachable now) through the ways learned, then
        /// on foot to the doors linked in that area, until a door of goalArea in goalDoors is come in through. The fewest doors.
        /// </summary>
        private static AreaRoute Search(int field, int startArea, List<Poi> starts, int goalArea, HashSet<string> goalDoors)
        {
            var prev = new Dictionary<string, (string from, string exit, int depth)>();
            var queue = new Queue<string>();
            string found = null;
            bool Goal(string t) => Parse(t, out int f, out int a, out string n) && f == field && a == goalArea && goalDoors.Contains(n);

            void Through(string exitKey, string from, int depth)
            {
                if (found != null || !Passes.TryGetValue(exitKey, out HashSet<string> arrivals))
                    return;
                foreach (string t in arrivals)
                {
                    if (prev.ContainsKey(t) || !Parse(t, out int f, out _, out _) || f != field)
                        continue;
                    prev[t] = (from, exitKey, depth);
                    if (Goal(t))
                    {
                        found = t;
                        return;
                    }
                    queue.Enqueue(t);
                }
            }

            foreach (Poi s in starts)
                foreach (string n in s.Names)
                    Through(ExploredDoors.Key(field, startArea, n), null, 1);
            while (found == null && queue.Count > 0)
            {
                string at = queue.Dequeue();
                int depth = prev[at].depth;
                if (depth >= MaxExits || !Parse(at, out int f, out int a, out string name))
                    continue;
                var exits = new HashSet<string> { name };
                if (Reach.TryGetValue(at, out HashSet<string> linked))
                    exits.UnionWith(linked);
                foreach (string e in exits)
                    Through(ExploredDoors.Key(f, a, e), at, depth + 1);
            }
            if (found == null)
                return null;

            var route = new AreaRoute { GoalDoors = goalDoors };
            for (string at = found; at != null; at = prev[at].from)
                route.Exits.Add(prev[at].exit);
            route.Exits.Reverse();
            Parse(route.Exits[0], out _, out _, out string first);
            route.FirstIndex = starts.FirstOrDefault(s => s.Names.Contains(first))?.Index ?? -1;
            return route.FirstIndex < 0 ? null : route;
        }

        // ---- The key ----

        /// <summary>F7: routes through other areas on or off (kept in the config).</summary>
        internal static void Toggle()
        {
            bool on = !Enabled;
            Config.AreaRoutes.Value = on;
            Config.Save(Config.AreaRoutes);
            if (!on)
                AutoWalk.CancelJourney("routes through other areas turned off");
            Log.Info($"{Tag} Routes through other areas {(on ? "on" : "off")}; {Summary}.");
            SpeechManager.Say($"Routes through other areas {(on ? "on" : "off")}.", interrupt: true, force: true);
        }
    }
}
