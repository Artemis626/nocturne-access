using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using Il2Cppfield_H;
using Il2Cppnewdata_H;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// The next tile of a switching-wall puzzle, read from the game's own state (0.0.210; Francesco, 27 Nov 2026: three attempts
    /// at the Kagutsuchi Tower's magic blocks failed the same way, the way on naming a tile already walked through). Each tile's
    /// procedure turns held objects' walls on and off, so the hit flags of those walls (fldGlobal.fldGbChkHit) are the puzzle's
    /// progress, whatever the player did, after battles and reloads. Data/TilePuzzles.txt, written offline by
    /// tools/unity/hojisim.pl TABLE=1, gives for each state the tile to cross next and from which side; the way on's token
    /// "@tiles" (Data/WayOn.txt) stands for it. Several rows of one state differ by where the player stands: the rows whose
    /// side the player is on count, the nearest plane first.
    /// </summary>
    internal static class TilePuzzle
    {
        internal const string Token = "@tiles";
        private const string Tag = "[Nav:WayOn]";

        private sealed class Row
        {
            public string Off = "", Flags = "", Tile = "";
            public int Side;
            public float X, Z, NX, NZ, SX, SZ;   // SX, SZ: a point of the walkable part the row holds for (the player must be in it)
        }

        private static Dictionary<(int, int), List<int>> _hojis;
        private static Dictionary<(int, int), List<Row>> _rows;

        /// <summary>
        /// The tile to walk through next in the player's area, or null (no table, a state with no row: solved, or a dead end).
        /// 'state' describes what was read, for the log.
        /// </summary>
        internal static string Next(fldGlobalWork_t gb, out string state)
        {
            state = "";
            Load();
            if (gb == null || !_hojis.TryGetValue((gb.fieldID, gb.areaID), out List<int> hojis))
                return null;
            List<int> off = hojis.Where(h => fldGlobal.fldGbChkHit(gb.fieldID, gb.areaID + 1, h) != 0).ToList();
            string offKey = off.Count == 0 ? "-" : string.Join(",", off);
            List<Row> rows = _rows.TryGetValue((gb.fieldID, gb.areaID), out List<Row> all)
                ? all.Where(r => r.Off == offKey && FlagsHold(gb.fieldID, gb.areaID, r.Flags)).ToList() : new List<Row>();
            float px = gb.playerX, pz = gb.playerZ;
            // The walkable part the player is in, on the live map (its walls follow the same hit flags): a row holds only where
            // its standing point is in the same part (area 14's dead-end pocket by 01pos_06 shares wall states with the route).
            WalkGrid.Ensure(gb.fieldID, gb.areaID, px, gb.playerY, pz);
            int here = WalkGrid.RegionAt(px, gb.playerY, pz);
            List<Row> inPart = rows.Where(r => here < 0 || WalkGrid.RegionAt(r.SX, gb.playerY, r.SZ) is int there && (there < 0 || there == here)).ToList();
            // The side of the plane's line only breaks ties between rows of one part (area 12's {1,2}: tile 02 north of it, 03
            // between 02 and 03). It is no test on its own (0.0.211; 20:41 session: area 14's walk reaches 01eve_10 round its
            // end, so from 01pos_02 the player stands on the line's other side, and the only row was thrown away).
            Row pick = inPart.OrderBy(r => ((px - r.X) * r.NX + (pz - r.Z) * r.NZ > 0f ? 1 : 0) == r.Side ? 0 : 1)
                             .ThenBy(r => (px - r.X) * (px - r.X) + (pz - r.Z) * (pz - r.Z)).FirstOrDefault();
            state = $"walls off {offKey}, {rows.Count} row{(rows.Count == 1 ? "" : "s")} for it" +
                    (rows.Count > 0 && inPart.Count == 0 ? ", none in the player's walkable part" : "") +
                    (pick != null ? $": {pick.Tile} from its {(pick.Side == 1 ? "normal" : "back")} side" : rows.Count == 0 ? " (solved, or no way on from here)" : "");
            return pick?.Tile;
        }

        /// <summary>"-": every story flag the puzzle tests is off; "1650": that flag on (the table lists the flags on).</summary>
        private static bool FlagsHold(int field, int area, string flags)
        {
            HashSet<int> on = flags == "-" ? new HashSet<int>() : new HashSet<int>(flags.Split(',').Select(int.Parse));
            foreach (int f in AreaFlags.TryGetValue((field, area), out HashSet<int> tested) ? tested : new HashSet<int>())
                if (EventBit.evtBitCheck(f) != on.Contains(f))
                    return false;
            return true;
        }

        private static readonly Dictionary<(int, int), HashSet<int>> AreaFlags = new Dictionary<(int, int), HashSet<int>>();   // the story flags each area's rows test

        private static void Load()
        {
            if (_rows != null)
                return;
            _hojis = new Dictionary<(int, int), List<int>>();
            _rows = new Dictionary<(int, int), List<Row>>();
            try
            {
                using Stream s = typeof(TilePuzzle).Assembly.GetManifestResourceStream("TilePuzzles.txt");
                if (s == null)
                {
                    Log.Warn($"{Tag} The tile puzzle data (TilePuzzles.txt) is not in the mod.");
                    return;
                }
                using var reader = new StreamReader(s);
                string line;
                int count = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    int hash = line.IndexOf('#');
                    string[] p = (hash >= 0 ? line.Substring(0, hash) : line).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 3 || !int.TryParse(p[0], out int field) || !int.TryParse(p[1], out int area))
                        continue;
                    if (p[2] == "hojis")
                    {
                        _hojis[(field, area)] = p.Skip(3).Select(int.Parse).ToList();
                        continue;
                    }
                    if (p.Length < 12)
                        continue;
                    var row = new Row
                    {
                        Off = p[2], Flags = p[3], Tile = p[4], Side = int.Parse(p[5]),
                        X = float.Parse(p[6], System.Globalization.CultureInfo.InvariantCulture),
                        Z = float.Parse(p[7], System.Globalization.CultureInfo.InvariantCulture),
                        NX = float.Parse(p[8], System.Globalization.CultureInfo.InvariantCulture),
                        NZ = float.Parse(p[9], System.Globalization.CultureInfo.InvariantCulture),
                        SX = float.Parse(p[10], System.Globalization.CultureInfo.InvariantCulture),
                        SZ = float.Parse(p[11], System.Globalization.CultureInfo.InvariantCulture),
                    };
                    if (row.Flags != "-")
                        foreach (string f in row.Flags.Split(','))
                            (AreaFlags.TryGetValue((field, area), out HashSet<int> set) ? set : AreaFlags[(field, area)] = new HashSet<int>()).Add(int.Parse(f));
                    if (!_rows.TryGetValue((field, area), out List<Row> list))
                        _rows[(field, area)] = list = new List<Row>();
                    list.Add(row);
                    count++;
                }
                Log.Info($"{Tag} Tile puzzle data: {count} states in {_rows.Count} areas.");
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} The tile puzzle data could not be read: {ex.Message}");
            }
        }
    }
}
