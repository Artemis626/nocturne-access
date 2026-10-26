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
            Poi pick = candidates.FirstOrDefault(q => q.Reachable && q.ViaWarps == 0 && q.Route == null)   // walked to
                       ?? candidates.FirstOrDefault(q => q.Reachable)                                    // through doors here
                       ?? candidates.FirstOrDefault(q => q.Listed);                                      // beyond doors
            Log.Info($"{Tag} Field {gb.fieldID} area {gb.areaID}: " + string.Join(", ", names.Select(n =>
            {
                Poi q = candidates.FirstOrDefault(c => c.Names.Contains(n));
                return q == null ? $"{n} not here" : $"{n} {(q.Reachable ? (q.ViaWarps == 0 && q.Route == null ? "walked to" : "via " + (q.Route != null ? "other areas" : q.ViaLegs)) : q.Listed ? "beyond doors" : "not reachable")}";
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
