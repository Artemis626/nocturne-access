using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Shift+End, the way on (0.0.118; Francesco, 26 Oct 2026: the Labyrinth of Amala is near unsolvable without a sighted
    /// player's map, so its route comes from walkthroughs, as the Obelisk's puzzles did). Says "Way on." and selects the
    /// area's onward point, then speaks End's line for it (name and directions); Home then walks there. The Pages list stays
    /// as it was: only the point chosen is kept listed (across rooms, like a landmark) until the player leaves the area.
    ///
    /// The data is Data/WayOn.txt (embedded): per field and area, the onward volumes in order of preference, derived from
    /// walkthroughs and matched to the game's own names and door table.
    /// </summary>
    internal static class WayOn
    {
        private const string Tag = "[Nav:WayOn]";
        private const string Words = "Way on.";

        private static Dictionary<(int Field, int Area), List<string>> _table;
        private static int _markField = int.MinValue, _markArea = int.MinValue;
        private static readonly HashSet<string> Marked = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Whether this volume is the way on just chosen here (PointsOfInterest: listed across rooms, like a landmark).</summary>
        internal static bool IsMarked(int field, int area, string name)
            => field == _markField && area == _markArea && Marked.Contains(name);

        // ---- The tone (0.0.122; Francesco, 27 Oct 2026) ----
        // Once per area with a way on: when the player first has control there after a save is loaded (the title screen and a
        // load forget what was heard: ExploredDoors.ClearAll), and on first entering each such area.
        private static readonly HashSet<(int, int)> Heard = new HashSet<(int, int)>();
        private static int _toneField = int.MinValue, _toneArea = int.MinValue;
        private static bool _tonePending;

        /// <summary>A save loaded or the title screen: every area's tone is played again on reaching it.</summary>
        internal static void ForgetHeard()
        {
            Heard.Clear();
            _toneField = _toneArea = int.MinValue;
        }

        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;
            if (gb.fieldID != _toneField || gb.areaID != _toneArea)
            {
                _toneField = gb.fieldID;
                _toneArea = gb.areaID;
                _tonePending = true;
            }
            if (!_tonePending || !FieldState.IsExploring)
                return;
            _tonePending = false;
            Load();
            if (!_table.ContainsKey((gb.fieldID, gb.areaID)) || !Heard.Add((gb.fieldID, gb.areaID)))
                return;
            Log.Info($"{Tag} Field {gb.fieldID} area {gb.areaID} has a way on: tone{((Config.WayOnSound?.Value ?? true) ? "" : " (off in the config)")}.");
            if (Config.WayOnSound?.Value ?? true)
                Earcons.WayOn();
        }

        /// <summary>Shift+End.</summary>
        internal static void Speak()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || !FieldState.IsExploring)
            {
                SpeechManager.Say($"{Words} Not available here.", interrupt: true);
                return;
            }
            Load();
            if (!_table.TryGetValue((gb.fieldID, gb.areaID), out List<string> names))
            {
                Log.Info($"{Tag} No way on known for field {gb.fieldID} area {gb.areaID}.");
                SpeechManager.Say($"{Words} None known for this area.", interrupt: true);
                return;
            }

            // Every candidate counts as a landmark while choosing, so one behind doors of this area is found too.
            _markField = gb.fieldID;
            _markArea = gb.areaID;
            Marked.Clear();
            Marked.UnionWith(names);
            List<Poi> pois = PointsOfInterest.Build(gb);
            List<Poi> candidates = names.Select(n => pois.FirstOrDefault(q => q.Names.Contains(n))).Where(q => q != null).ToList();
            // A point walked to crossing no story event first; then through doors here; then walked to through the fewest story
            // events (in the listed order among equals); then beyond doors (0.0.128: Yoyogi Park's pixies warp the player back, and
            // the flood counts a door behind one as walkable). The story event that is itself the target is not counted (the Amala
            // Network 2's soul room names a soul).
            var crossings = new Dictionary<Poi, int>();
            foreach (Poi q in candidates.Where(q => q.Reachable && q.ViaWarps == 0 && q.Route == null))
                crossings[q] = WalkGrid.StoryCrossings(gb.playerX, gb.playerY, gb.playerZ, q.X, q.FloorY, q.Z);
            int fewest = crossings.Values.Where(c => c > 0).DefaultIfEmpty(-1).Min();
            Poi pick = candidates.FirstOrDefault(q => crossings.TryGetValue(q, out int c) && c == 0)        // walked to
                       ?? candidates.FirstOrDefault(q => q.Reachable && !crossings.ContainsKey(q))         // through doors here
                       ?? candidates.FirstOrDefault(q => crossings.TryGetValue(q, out int c) && c == fewest) // walked to through story events
                       ?? candidates.FirstOrDefault(q => crossings.ContainsKey(q))                         // walked to, no route planned
                       ?? candidates.FirstOrDefault(q => q.Listed);                                        // beyond doors
            Log.Info($"{Tag} Field {gb.fieldID} area {gb.areaID}: " + string.Join(", ", names.Select(n =>
            {
                Poi q = candidates.FirstOrDefault(c => c.Names.Contains(n));
                string state = q == null ? "not here"
                    : crossings.TryGetValue(q, out int c) ? (c == 0 ? "walked to" : c < 0 ? "walked to, no route" : $"walked to through {c} story event{(c == 1 ? "" : "s")}")
                    : q.Reachable ? "via " + (q.Route != null ? "other areas" : q.ViaLegs)
                    : q.Listed ? "beyond doors" : "not reachable";
                return $"{n} {state}";
            })) + $"; chosen {pick?.ResName ?? "none"}.");
            Marked.Clear();
            if (pick == null)
            {
                SpeechManager.Say($"{Words} Not found from here.", interrupt: true);
                return;
            }
            Marked.UnionWith(pick.Names);
            PointsOfInterest.Select(gb, pick);
            RouteGuide.SpeakWith(Words + " ");
        }

        private static void Load()
        {
            if (_table != null)
                return;
            _table = new Dictionary<(int, int), List<string>>();
            try
            {
                using Stream s = typeof(WayOn).Assembly.GetManifestResourceStream("WayOn.txt");
                if (s == null)
                {
                    Log.Warn($"{Tag} The way-on data (WayOn.txt) is not in the mod.");
                    return;
                }
                using var reader = new StreamReader(s);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int hash = line.IndexOf('#');
                    string[] parts = (hash >= 0 ? line.Substring(0, hash) : line).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3 || !int.TryParse(parts[0], out int field) || !int.TryParse(parts[1], out int area))
                        continue;
                    _table[(field, area)] = parts.Skip(2).ToList();
                }
                Log.Info($"{Tag} Way-on data: {_table.Count} areas in {_table.Keys.Select(k => k.Field).Distinct().Count()} fields.");
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} The way-on data could not be read: {ex.Message}");
            }
        }
    }
}
