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
    /// (story flag 1614, set by the sphere's procedure and cleared by "Will you go back?" Yes or by the last tile), the Pages
    /// list holds only the next tile of the floor's solution ("Next step: Time passed quickly"), so a wrong press cannot pick
    /// a wrong tile while the floor's time runs. The solutions are read from the game's script and Francesco's own run, one
    /// table per floor (script area = areaID + 1); a floor with no table keeps the normal list (each tile says its narration,
    /// 0.0.97). Progress: the tiles the game fires (fldEveHit.gFldEveRngIdx) in order; it starts again whenever 1614 goes off.
    /// </summary>
    internal static class TimePuzzle
    {
        private const int Obelisk = 31;
        private const int PuzzleFlag = 1614;

        /// <summary>One step: the event volume to walk onto, and the answer to give its question ("" when it asks none).</summary>
        internal readonly struct Step
        {
            internal readonly string Volume, Answer;
            internal Step(string volume, string answer = "") { Volume = volume; Answer = answer; }
        }

        // Script area 3 (30F, area 2): the sphere 01eve_01 at -890 -500; the north "Time passed quickly" tile 01eve_03, its
        // "Will you go back?" tile 01eve_05 answered No, then the silent tiles 01eve_07 (moves an object, gimmick flag 4) and
        // 01eve_06 (ends the puzzle). Francesco's run, 20 Oct 2026, 14:34:12 to 14:34:34.
        private static readonly Dictionary<int, Step[]> Solutions = new Dictionary<int, Step[]>
        {
            [3] = new[] { new Step("01eve_03"), new Step("01eve_05", "No"), new Step("01eve_07"), new Step("01eve_06") },
        };

        private static int _field = -1, _area = -1, _done;
        private static bool _on;
        private static string _lastHit = "";

        /// <summary>The volume of the next step and its answer, or null when no time puzzle is running here.</summary>
        internal static Step? Next(fldGlobalWork_t gb)
        {
            if (gb == null || gb.fieldID != Obelisk || !_on || gb.fieldID != _field || gb.areaID != _area)
                return null;
            if (!Solutions.TryGetValue(gb.areaID + 1, out Step[] steps) || _done >= steps.Length)
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
            }
            if (gb.fieldID != Obelisk || !Solutions.TryGetValue(gb.areaID + 1, out Step[] steps))
                return;
            bool on;
            try { on = EventBit.evtBitCheck(PuzzleFlag); }
            catch { return; }
            if (on != _on)
            {
                _on = on;
                _done = 0;
                _lastHit = "";
                Log.Info($"[Nav:Puzzle] Time puzzle {(on ? "on" : "off")} in field {gb.fieldID} area {gb.areaID}: step 1 of {steps.Length} next.");
            }
            if (!_on)
                return;
            int i = fldEveHit.gFldEveRngIdx;
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (i < 0 || ranges == null || i >= Math.Min(fldEveHit.gEvnRngNum, ranges.Length))
                return;
            string name = ranges[i]?.resourceName ?? "";
            if (name == _lastHit)
                return;
            _lastHit = name;
            // The tile just fired: the step after it is next (a later step counts too, so a run that skips ahead follows on).
            for (int k = _done; k < steps.Length; k++)
            {
                if (steps[k].Volume != name)
                    continue;
                _done = k + 1;
                Log.Info($"[Nav:Puzzle] Step {k + 1} of {steps.Length} done ({name}); {(_done < steps.Length ? $"next {steps[_done].Volume}" : "all done")}.");
                return;
            }
        }
    }
}
