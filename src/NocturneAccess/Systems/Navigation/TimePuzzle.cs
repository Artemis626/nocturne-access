using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppfield_H;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// The Obelisk's time puzzles (field 31; 0.0.98, Francesco's choice, 20 Oct 2026): while a glowing sphere's effect is on
    /// (story flag 1614, set by the sphere's procedure and cleared by "Will you go back?" Yes or by an exit tile), the Pages
    /// list holds only the next tile of the floor's solution ("Next step: Time passed quickly"), so a stray press cannot pick
    /// a wrong tile while the floor's time runs.
    ///
    /// How the puzzle works, from the field script (f031.bf, 0.0.99): touching the sphere sets Kagutsuchi to New; the floor
    /// has blocks (01pos_NN), each advancing the moon by a fixed number of phases when stepped onto (the tile procedure's IX 249:
    /// 0 to 4; the guides' "symbols"); every "Time passed quickly" tile is a bridge to a block (from the start) or between two
    /// blocks (either way); "Will you go back?" tiles lie between the start and the first blocks (Yes: back to the start, the
    /// puzzle off); the exit tiles end the puzzle and the path or chest they open depends on the moon reached (4/8 or 8/8). The
    /// guides give the block values in order (Ludo: 32F "3 then 1", 52F 8/8 "1-3-4", 77F left 8/8 "3-1-3-1"); each is matched
    /// here to the script's bridges. Tables are keyed by script area (areaID + 1) and the sphere touched; a floor or sphere
    /// with no table keeps the normal list (each tile says its narration, 0.0.97). Progress: the tiles the game fires
    /// (fldEveHit.gFldEveRngIdx) in order; it starts again whenever 1614 goes off.
    /// </summary>
    internal static class TimePuzzle
    {
        private const int Obelisk = 31;
        private const int PuzzleFlag = 1614;

        /// <summary>One step: the event volume to walk onto ("a|b": whichever of them is nearer), and its question's answer.</summary>
        internal readonly struct Step
        {
            internal readonly string Volume, Answer;
            internal Step(string volume, string answer = "") { Volume = volume; Answer = answer; }
        }

        // Script area 3 (32F; the sphere 01eve_01): blocks 01pos_03 (1) and 01pos_04 (3); start bridges 01eve_02 (to 03) and
        // 01eve_03 (to 04); bridge 01eve_07 (03 and 04); exit 01eve_06. 4/8: 3 then 1. Francesco solved it so (20 Oct, 14:34;
        // his "No" to 01eve_05 was a detour: that tile lies on the way back to the start).
        // Script area 5 (52F; sphere 01eve_05): blocks 08 (3), 09 (1), 10 (4), 11 (2); start bridges 06 (08), 07 (09); bridges
        // 10 (09-08), 11 (10-08), 12 (11-09), 13 (11-10); exits 14, 15. The path onward is 8/8 (Samurai Gamers: 3 steps; Ludo:
        // 1-3-4): 07, 10, 11, exit. (The chest is 4/8, 3-4-2-1-2: 06, 11, 13, 12, 12.)
        // Script area 7, sphere 01eve_50 (77F, the left puzzle): blocks 52 (3), 53 (0), 54 (1), 55 (3); start bridges 51 (52),
        // 52 (53); bridges 57 (53-52), 58 (54-52), 59 (55-53), 60 (55-54); exits 55, 56. The path is 8/8 (Samurai Gamers: 4
        // steps; Ludo: 3-1-3-1): 51, 58, 60, 60, exit. The right puzzle (sphere 01eve_30) waits: the guides do not say which of
        // its two routes opens the path; script areas 11 and 14 (85F, 128F) also wait.
        private static readonly Dictionary<(int Area, string Sphere), Step[]> Solutions = new Dictionary<(int, string), Step[]>
        {
            [(3, "01eve_01")] = new[] { new Step("01eve_03"), new Step("01eve_07"), new Step("01eve_06") },
            [(5, "01eve_05")] = new[] { new Step("01eve_07"), new Step("01eve_10"), new Step("01eve_11"), new Step("01eve_14|01eve_15") },
            [(7, "01eve_50")] = new[] { new Step("01eve_51"), new Step("01eve_58"), new Step("01eve_60"), new Step("01eve_60"), new Step("01eve_55|01eve_56") },
        };

        private static readonly HashSet<(int, string)> Spheres = new HashSet<(int, string)>
        {
            (3, "01eve_01"), (5, "01eve_05"), (7, "01eve_30"), (7, "01eve_50"), (11, "01eve_30"), (14, "01eve_30"),
        };

        private static int _field = -1, _area = -1, _done;
        private static bool _on;
        private static string _lastHit = "", _sphere = "";

        /// <summary>The next step, or null when no time puzzle with a known solution is running here.</summary>
        internal static Step? Next(fldGlobalWork_t gb)
        {
            if (gb == null || gb.fieldID != Obelisk || !_on || gb.fieldID != _field || gb.areaID != _area)
                return null;
            if (!Solutions.TryGetValue((gb.areaID + 1, _sphere), out Step[] steps) || _done >= steps.Length)
                return null;
            return steps[_done];
        }

        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;
            if (gb.fieldID != _field || gb.areaID != _area)
            {
                _field = gb.fieldID;
                _area = gb.areaID;
                _done = 0;
                _on = false;
                _lastHit = "";
                _sphere = "";
            }
            if (gb.fieldID != Obelisk)
                return;
            int area = gb.areaID + 1;
            string name = "";
            int i = fldEveHit.gFldEveRngIdx;
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (i >= 0 && ranges != null && i < Math.Min(fldEveHit.gEvnRngNum, ranges.Length))
                name = ranges[i]?.resourceName ?? "";
            if (name.Length > 0 && Spheres.Contains((area, name)))
                _sphere = name;
            bool on;
            try { on = EventBit.evtBitCheck(PuzzleFlag); }
            catch { return; }
            Solutions.TryGetValue((area, _sphere), out Step[] steps);
            if (on != _on)
            {
                _on = on;
                _done = 0;
                _lastHit = "";
                Log.Info($"[Nav:Puzzle] Time puzzle {(on ? "on" : "off")} in field {gb.fieldID} area {gb.areaID}, sphere {(_sphere.Length > 0 ? _sphere : "unknown")}: " +
                         (steps != null ? $"{steps.Length} steps known." : "no solution table, the full list stays."));
            }
            // Out of every volume: the same tile can count again (77F's path crosses bridge 01eve_60 twice).
            if (name.Length == 0)
            {
                _lastHit = "";
                return;
            }
            if (!_on || steps == null || name == _lastHit)
                return;
            _lastHit = name;
            // The tile just fired: the step after it is next (a later step counts too, so a run that skips ahead follows on).
            for (int k = _done; k < steps.Length; k++)
            {
                if (Array.IndexOf(steps[k].Volume.Split('|'), name) < 0)
                    continue;
                _done = k + 1;
                Log.Info($"[Nav:Puzzle] Step {k + 1} of {steps.Length} done ({name}); {(_done < steps.Length ? $"next {steps[_done].Volume}" : "all done")}.");
                return;
            }
        }
    }
}
