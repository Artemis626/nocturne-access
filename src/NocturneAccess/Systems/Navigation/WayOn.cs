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
            if (!Known(gb.fieldID, gb.areaID, out _) || !Heard.Add((gb.fieldID, gb.areaID)))
                return;
            Log.Info($"{Tag} Field {gb.fieldID} area {gb.areaID} has a way on: tone{((Config.WayOnSound?.Value ?? true) ? "" : " (off in the config)")}.");
            if (Config.WayOnSound?.Value ?? true)
                Earcons.WayOn();
        }

        /// <summary>The story events crossed on every walk of a route through door pairs, warps or ladders; a walk with no route counts one.</summary>
        private static int LegCrossings(fldGlobalWork_t gb, Poi q)
        {
            float sx = gb.playerX, sy = gb.playerY, sz = gb.playerZ;
            int total = 0;
            foreach (Warp leg in q.Legs)
            {
                int c = WalkGrid.StoryCrossings(sx, sy, sz, leg.Zone.X, leg.FloorY, leg.Zone.Z);
                total += (c < 0 ? 1 : c) + WarpMap.LandingCost(leg, q);
                if (!leg.HasDest)
                    return total;
                sx = leg.DestX;
                sy = leg.DestY;
                sz = leg.DestZ;
            }
            int last = WalkGrid.StoryCrossings(sx, sy, sz, q.X, q.FloorY, q.Z);
            return total + (last < 0 ? 1 : last);
        }

        /// <summary>q is a warp of this area whose landing is known and walkable from the player crossing no story event (0.0.159: a landing behind a shadow line is not "walkable already").</summary>
        private static bool LandsWithinReach(fldGlobalWork_t gb, Poi q)
        {
            Warp w = WarpMap.At(gb, q.Index);
            if (w == null || !w.HasDest || w.Ladder)
                return false;
            // A landing inside a story volume is never "walkable already": the walk test does not count the volume it ends in, and area
            // 15's 01d_07 (its landing inside the shadow 01eve_04: the alarm on purpose) was left out (14:34:08).
            if (WalkGrid.SoftZoneAt(w.DestX, w.DestZ) >= 0)
                return false;
            return WalkGrid.StoryCrossings(gb.playerX, gb.playerY, gb.playerZ, w.DestX, w.DestY, w.DestZ) == 0;
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
            if (!Known(gb.fieldID, gb.areaID, out List<string> names))
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
            // Equal highest stats: the nearer stat route first (Francesco: the nearest, or any).
            if (Tied.Count > 1)
                candidates = candidates.Where(q => Tied.Overlaps(q.Names)).OrderBy(q => q.Distance)
                                       .Concat(candidates.Where(q => !Tied.Overlaps(q.Names))).ToList();
            // A point walked to crossing no story event first; then through doors here; then walked to through the fewest story
            // events (in the listed order among equals); then beyond doors (0.0.128: Yoyogi Park's pixies warp the player back, and
            // the flood counts a door behind one as walkable). The story event that is itself the target is not counted (the Amala
            // Network 2's soul room names a soul).
            // 0.0.132: a route through door pairs counts the story events on each of its walks too (Yoyogi Park's column maze, area 16,
            // 29 Oct 2026, 15:15: the north door "via 1 door" was chosen, and the walk to the pair ran into a pixie that sends the
            // player back to area 15, four times running).
            // A warp or same-area door whose landing the player can already walk to leads nowhere new: it is the one just taken, or
            // its far side is this room (0.0.153; Francesco, 5 Nov 2026, 13:47: the White Temple's beam 01d_22, area 22, put him
            // on 01pos_51, and the way on named the same beam three times, "No path from here", until he took a door by hand).
            var spent = candidates.Where(q => q.Reachable && LandsWithinReach(gb, q)).ToList();
            if (spent.Count > 0 && spent.Count < candidates.Count)
            {
                Log.Info($"{Tag} Field {gb.fieldID} area {gb.areaID}: {string.Join(", ", spent.Select(q => q.ResName))} land where the player can walk already: left out.");
                candidates = candidates.Except(spent).ToList();
            }
            var crossings = new Dictionary<Poi, int>();
            var viaCrossings = new Dictionary<Poi, int>();
            foreach (Poi q in candidates.Where(q => q.Reachable && q.ViaWarps == 0 && q.Route == null))
                crossings[q] = WalkGrid.StoryCrossings(gb.playerX, gb.playerY, gb.playerZ, q.X, q.FloorY, q.Z);
            foreach (Poi q in candidates.Where(q => q.Reachable && q.ViaWarps > 0 && q.Route == null && q.Legs != null))
                viaCrossings[q] = LegCrossings(gb, q);
            int fewest = crossings.Values.Concat(viaCrossings.Values).Where(c => c > 0).DefaultIfEmpty(-1).Min();
            // Walked to or through doors here, crossing no story event: the first in the listed order (0.0.153; a walk used to come
            // before any door route, and the Red Temple's west wing, field 34 area 13, chose 01d_05 back to the entrance over its north
            // door 01d_01 "via 1 door", the listed first: a loop through areas 11, 12 and 13, 5 Nov 2026 14:12).
            Poi pick = candidates.FirstOrDefault(q => (crossings.TryGetValue(q, out int c) && c == 0)    // walked to
                                                      || (q.Reachable && !crossings.ContainsKey(q)
                                                          && (!viaCrossings.TryGetValue(q, out int v) || v == 0))) // through doors here
                       ?? candidates.FirstOrDefault(q => (crossings.TryGetValue(q, out int c) || viaCrossings.TryGetValue(q, out c)) && c == fewest) // through story events
                       ?? candidates.FirstOrDefault(q => q.Reachable)                                      // no route planned
                       ?? candidates.FirstOrDefault(q => q.Listed);                                        // beyond doors
            Log.Info($"{Tag} Field {gb.fieldID} area {gb.areaID}: " + string.Join(", ", names.Select(n =>
            {
                Poi q = candidates.FirstOrDefault(c => c.Names.Contains(n));
                string state = q == null ? "not here"
                    : crossings.TryGetValue(q, out int c) ? (c == 0 ? "walked to" : c < 0 ? "walked to, no route" : $"walked to through {c} story event{(c == 1 ? "" : "s")}")
                    : viaCrossings.TryGetValue(q, out int v) ? $"via {q.ViaLegs}{(v > 0 ? $" through {v} story event{(v == 1 ? "" : "s")}" : v < 0 ? ", a walk not planned" : "")}"
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
            // A stat route says its stat (mod words Francesco kept, 2 Nov 2026): "Way on, Strength."
            string word = pick.Names.Select(n => RouteWord.TryGetValue(n, out string w) ? w : null).FirstOrDefault(w => w != null);
            string lead = word != null ? $"Way on, {word}. " : Words + " ";
            if (pick.Names.Any(Alarming.Contains))
                lead += AlarmWords + " ";
            RouteGuide.SpeakWith(lead);
        }

        private static readonly Dictionary<int, int> Until = new Dictionary<int, int>();

        /// <summary>The way-on candidates of (field, area), unless there are none or the field's "until" flag is set.</summary>
        private static bool Known(int field, int area, out List<string> names)
        {
            names = null;
            if (!_table.TryGetValue((field, area), out names))
                return false;
            if (Until.TryGetValue(field, out int flag) && EventBit.evtBitCheck(flag))
            {
                names = null;
                return false;
            }
            // 0.0.140: "01d_11+1700" holds only once story flag 1700 is set, "01d_04-1700" only while it is off (the Black Temple:
            // a Jump Off on 2F breaks the 1F floor, flag 1700, and opens the hole down to B1F).
            Alarming.Clear();
            names = names.Select(Gated).Where(n => n != null).ToList();
            names = ByStat(names);
            return names.Count > 0;
        }

        // ---- Stat routes (0.0.141; Francesco, 2 Nov 2026) ----
        // The Third Kalpa's three wings lead to the same ladder down, each behind doors that test one stat ("Voice of the Door":
        // prm_PLAYER_GET_PARAM, passed when the stat is at least the number). "01eve_01@S15" is a Strength route needing 15
        // (S Strength, M Magic, L Luck: the game's param ids 0, 2, 5). The route of the player's highest stat is kept; first among
        // the routes the player can pass, else among all. Equal highest stats keep both, the nearer chosen (Speak).
        private static readonly (char Tag, int Id, string Word)[] Stats = { ('S', 0, "Strength"), ('M', 2, "Magic"), ('L', 5, "Luck") };
        private static readonly HashSet<string> Tied = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> RouteWord = new Dictionary<string, string>(StringComparer.Ordinal);

        private static List<string> ByStat(List<string> names)
        {
            Tied.Clear();
            RouteWord.Clear();
            var routes = new List<(string Name, int Id, string Word, int Need, int Have)>();
            foreach (string n in names)
            {
                int at = n.IndexOf('@');
                if (at < 0 || at + 2 > n.Length || !int.TryParse(n.Substring(at + 2), out int need))
                    continue;
                var s = Stats.FirstOrDefault(x => x.Tag == n[at + 1]);
                if (s.Word == null)
                    continue;
                routes.Add((n, s.Id, s.Word, need, datCalc.datGetPlayerParam(s.Id)));
            }
            if (routes.Count == 0)
                return names;
            var pool = routes.Where(r => r.Have >= r.Need).ToList();
            if (pool.Count == 0)
                pool = routes;
            int best = pool.Max(r => r.Have);
            var kept = pool.Where(r => r.Have == best).ToList();
            Log.Info($"{Tag} Stats: " + string.Join(", ", Stats.Select(s => $"{s.Word} {datCalc.datGetPlayerParam(s.Id)}"))
                     + "; routes " + string.Join(", ", routes.Select(r => $"{r.Name} ({r.Word} {r.Need}{(r.Have >= r.Need ? "" : ", not passed")})"))
                     + $"; kept " + string.Join(" and ", kept.Select(r => r.Word).Distinct()) + ".");
            var keep = new List<string>();
            foreach (var r in kept)
            {
                string v = r.Name.Substring(0, r.Name.IndexOf('@'));
                keep.Add(v);
                RouteWord[v] = r.Word;
            }
            // Two routes of one stat keep the listed order (the West wing's Strength 25 door before the Strength 20 one).
            if (kept.Select(r => r.Word).Distinct().Count() > 1)
                Tied.UnionWith(keep);
            // Untagged names stay after the stat route, as fallbacks.
            return keep.Concat(names.Where(n => n.IndexOf('@') < 0)).Distinct().ToList();
        }

        /// <summary>The volume name of a candidate whose flag condition holds now, else null.</summary>
        // 0.0.158: "!01d_07-1703": taking this point sets off the Red Temple's alarm on purpose (its landing lies inside a shadow);
        // the way on says so first (Francesco, 6 Nov 2026, asked for the warning; the words are the mod's).
        private static readonly HashSet<string> Alarming = new HashSet<string>(StringComparer.Ordinal);
        private const string AlarmWords = "Sets off the alarm on purpose.";

        private static string Gated(string token)
        {
            bool alarming = token.StartsWith("!", StringComparison.Ordinal);
            if (alarming)
                token = token.Substring(1);
            string name = GatedPlain(token);
            if (alarming && name != null)
                Alarming.Add(name);
            return name;
        }

        private static string GatedPlain(string token)
        {
            int at = token.IndexOfAny(new[] { '+', '-' });
            if (at < 0 || !int.TryParse(token.Substring(at + 1), out int flag))
                return token;
            bool set = EventBit.evtBitCheck(flag);
            return (token[at] == '+') == set ? token.Substring(0, at) : null;
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
                    // "16 until 74": the field's way on holds only while story flag 74 is off (0.0.133: Yoyogi Park's pixies leave
                    // with Sakahagi's defeat, and Francesco wants no way on there afterwards).
                    if (parts.Length == 3 && parts[1] == "until" && int.TryParse(parts[0], out int uf) && int.TryParse(parts[2], out int flag))
                    {
                        Until[uf] = flag;
                        continue;
                    }
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
