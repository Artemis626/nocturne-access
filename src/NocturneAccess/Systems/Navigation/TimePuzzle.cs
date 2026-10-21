using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

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
    /// puzzle off); the exit tiles end the puzzle, and where the route ends decides what lies beyond (77F right: its two exits do
    /// the same, the path or the chest is past them; the route's moon total is 8, 8/8, or 12, 4/8). The
    /// guides give the block values in order (Ludo: 32F "3 then 1", 52F 8/8 "1-3-4", 77F left 8/8 "3-1-3-1"); each is matched
    /// here to the script's bridges. Tables are keyed by script area (areaID + 1) and the sphere touched; a floor or sphere
    /// with no table keeps the normal list (each tile says its narration, 0.0.97). Progress: the tiles the game fires
    /// (fldEveHit.gFldEveRngIdx at the moment the game takes movement over, 0.0.103) in order; it starts again whenever 1614
    /// goes off.
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
        // steps; Ludo: 3-1-3-1): 51, 58, 60, 60, exit.
        // Script area 7, sphere 01eve_30 (77F, the right puzzle, west of the arrival; 0.0.103): blocks 33 (0), 34 (2), 35 (1),
        // 36 (3), 37 (4); start bridges 31 (33), 32 (34), 33 (35); bridges 39 (33-34), 40 (34-35), 41 (33-36), 42 (35-37); exits
        // 37 (south of 36) and 38 (south of 37), which do the same (flag 1614 off, atari_hoji_03 off). What lies beyond them
        // differs (dump 20 Oct 21:42:11, tools/navregions.pl): past exit 37 the rail of Ride 01eve_14, whose far end joins the
        // part of the floor with the left puzzle's sphere 01eve_50 (the path); past exit 38 Rides 01eve_16 (to an item box) and
        // 01eve_18. So the path is Ludo's 8/8 "1-2-2-3" (zeros not counted): 33, 40, 39, 39, 39, 41, exit 37.
        // Script area 14, sphere 01eve_30 (85F, area 13; 0.0.104): blocks 33 (1), 34 (2), 35 (3) in a row west to east at z 1750;
        // start bridges 31 (33), 32 (34), 33 (35) at z 1400; "Will you go back?" 34 to 36 at z 1500; bridges 39 (33-34), 40
        // (34-35); exits 37 (west of block 33) and 38 (north of block 35), the same script. Samurai Gamers: "Getting the Mystical
        // Chest" 4 steps, "Unlocking the Path to the Next Area" 7 steps; Ludo: 8/8 "2-3-2-1" (4: ends on 33, exit 37, west, the
        // item box side), 4/8 "1-2-1-2-1-2-3" (7: ends on 35, exit 38, north, toward the lift 01eve_08 at 0 4750). So the path
        // here is 4/8: 31, 39 five times, 40, exit 38.
        private static readonly Dictionary<(int Area, string Sphere), Step[]> Solutions = new Dictionary<(int, string), Step[]>
        {
            [(3, "01eve_01")] = new[] { new Step("01eve_03"), new Step("01eve_07"), new Step("01eve_06") },
            [(5, "01eve_05")] = new[] { new Step("01eve_07"), new Step("01eve_10"), new Step("01eve_11"), new Step("01eve_14|01eve_15") },
            [(7, "01eve_50")] = new[] { new Step("01eve_51"), new Step("01eve_58"), new Step("01eve_60"), new Step("01eve_60"), new Step("01eve_55|01eve_56") },
            [(7, "01eve_30")] = new[] { new Step("01eve_33"), new Step("01eve_40"), new Step("01eve_39"), new Step("01eve_39"), new Step("01eve_39"),
                                        new Step("01eve_41"), new Step("01eve_37") },
            [(11, "01eve_30")] = new[] { new Step("01eve_32"), new Step("01eve_38"), new Step("01eve_41"),
                                         new Step("01eve_41"), new Step("01eve_38"), new Step("01eve_38"), new Step("01eve_38"), new Step("01eve_40"),
                                         new Step("01eve_40"), new Step("01eve_40"), new Step("01eve_42") },
            [(14, "01eve_30")] = new[] { new Step("01eve_31"), new Step("01eve_39"), new Step("01eve_39"), new Step("01eve_39"), new Step("01eve_39"),
                                         new Step("01eve_39"), new Step("01eve_40"), new Step("01eve_38") },
        };

        // Script area 11, sphere 01eve_30 (area 10, above 103F; 0.0.106): the Moirai. Blocks in two rows (01pos_NN, index IX 1,
        // value): north row east to west 33 (0, 3), 34 (1, 1), 35 (2, 0); south row 36 (3, 1), 37 (4, 2), 38 (5, 3); start
        // bridges 31 (to 33), 32 (34), 33 (35); bridges 37 (33-34), 38 (34-35), 39 (33-36), 40 (34-37), 41 (35-38), 42 (36-37),
        // 43 (37-38); "Will you go back?" 34 to 36; no exit tile. After every step the shared procedure check_moon_state5 moves
        // the moon one phase at a time (evtCommand_GET_MOON16, 0 to 15) and, at the last phase, fights a sister
        // (evtCommand_CALL_BATTLE): on block index 3 (36) at 4 or 12, battle 270, flag 1611, beside 01pos_45 (east); on index 4
        // (37) at 8, battle 269, flag 1610, 01pos_47 (south); on index 5 (38) at 4 or 12, battle 268, flag 1609, 01pos_46
        // (west); flag 1605 once all three are set; passing new moon (0) drops the player through the floor (CALL_FIELD3 to
        // 01pos_39 to 44). 0.0.106 gave each sister a route of her own from the start: Atropos (east) fled twice and the
        // puzzle went on (21 Oct 2026, 16:20 and 16:25). Walkthroughs (Screen Rant, GameFAQs, Neoseeker): each sister flees,
        // all three must be fought in one run without leaving the board, youngest to oldest, right to left facing south:
        // Clotho (west), Lachesis (south), Atropos (east). After a battle the player stands on the sister's block, the puzzle
        // still on. One run (a search over the bridges, the moon only rising, below 16, stopping on no sister's spot out of
        // turn): 32, 38, 41 (4: Clotho); 41, 38, 38, 38, 40 (8: Lachesis); 40, 40, 42 (12: Atropos). Ends in battles: no solved
        // line.
        private const int MoiraiArea = 11;

        private static Step[] StepsFor(int area, string sphere)
        {
            return Solutions.TryGetValue((area, sphere), out Step[] steps) ? steps : null;
        }

        private static bool EndsInBattle(int area, string sphere) => area == MoiraiArea && sphere == "01eve_30";

        private static readonly HashSet<(int, string)> Spheres = new HashSet<(int, string)>
        {
            (3, "01eve_01"), (5, "01eve_05"), (7, "01eve_30"), (7, "01eve_50"), (11, "01eve_30"), (14, "01eve_30"),
        };

        // Francesco's words (20 Oct 2026, after 52F), said once the last step has ended the puzzle and the player has control.
        internal const string DoneWords = "Puzzle successfully completed, reached other side.";
        private const long DonePendingMs = 15000;
        private const int DoneHoldMs = 3000;

        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        private static int _field = -1, _area = -1, _done;
        private static bool _on, _donePending;
        private static long _doneMs;
        private static string _sphere = "", _lastStep = "";
        private static Step[] _steps;              // the route of the puzzle running now, fixed when it starts (0.0.106)
        private static int _moves;
        private static long _movedMs;
        private static bool _armed;
        // The movement routine still this long: an event took over (0.0.104). This tick runs several times per game frame, so 3
        // still ticks (0.0.103, AutoWalk's count, which runs once per field update) passed within one frame while walking: every
        // tick near a tile counted, and one crossing of 01eve_39 counted twice (21:55:30.123 and .157).
        private const long IdleMs = 150;
        private const float TileReach = 150f;     // a tile box is 200 wide: the player crosses it within 100 of its centre

        /// <summary>
        /// Whether volume name is a step the game has just fired (0.0.101; Francesco, 20 Oct 2026: "Walk interrupted by an event"
        /// after every tile distracted him): auto-walk then ends without its stop line, the tile's own scene follows.
        /// </summary>
        internal static bool StepTaken(string name) => name.Length > 0 && name == _lastStep;

        /// <summary>The next step, or null when no time puzzle with a known solution is running here.</summary>
        internal static Step? Next(fldGlobalWork_t gb)
        {
            if (gb == null || gb.fieldID != Obelisk || !_on || gb.fieldID != _field || gb.areaID != _area)
                return null;
            if (_steps == null || _done >= _steps.Length)
                return null;
            return _steps[_done];
        }

        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;
            // A battle (fields 200 to 299: 31 to 231) is not leaving the floor: the Moirai fights return the player to the
            // sister's block with the puzzle still on (21 Oct 2026, 16:20:22), and the run goes on (0.0.107).
            if (gb.fieldID >= 200 && gb.fieldID < 300)
                return;
            if (gb.fieldID != _field || gb.areaID != _area)
            {
                // An exit tile that leads into another area ends the puzzle there too (none seen yet: 52F's exit stays in area 4).
                if (_on && _field == Obelisk && _steps != null && _done >= _steps.Length && !EndsInBattle(_area + 1, _sphere))
                {
                    _donePending = true;
                    _doneMs = Clock.ElapsedMilliseconds;
                    Log.Info("[Nav:Puzzle] Solved: every step taken, then a new area; said once the player has control.");
                }
                _field = gb.fieldID;
                _area = gb.areaID;
                _done = 0;
                _on = false;
                _steps = null;
                _lastStep = "";
                _sphere = "";
            }
            if (_donePending)
            {
                if (Clock.ElapsedMilliseconds - _doneMs > DonePendingMs)
                {
                    _donePending = false;
                    Log.Info("[Nav:Puzzle] The player did not take control in time: the solved line not said.");
                }
                else if (FieldState.IsExploring)
                {
                    _donePending = false;
                    SpeechManager.SayHeld(DoneWords, DoneHoldMs);
                }
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
            // The route is fixed when the puzzle starts (0.0.106): the Moirai floor's flags change at its last step.
            Step[] steps = _on ? _steps : StepsFor(area, _sphere);
            if (on != _on)
            {
                // Off right after the last step: the exit tile ended it (a "Will you go back?" Yes ends it with steps left).
                if (!on && steps != null && _done >= steps.Length && !EndsInBattle(area, _sphere))
                {
                    _donePending = true;
                    _doneMs = Clock.ElapsedMilliseconds;
                    Log.Info("[Nav:Puzzle] Solved: every step taken and the puzzle ended; said once the player has control.");
                }
                _on = on;
                _steps = on ? steps : null;
                if (on)
                    _lastStep = "";   // kept when it goes off: the walk to the exit tile ends in the same moment
                _done = 0;
                Log.Info($"[Nav:Puzzle] Time puzzle {(on ? "on" : "off")} in field {gb.fieldID} area {gb.areaID}, sphere {(_sphere.Length > 0 ? _sphere : "unknown")}: " +
                         (steps != null ? $"{steps.Length} steps known." : "no solution table, the full list stays."));
            }
            // A tile fires when the game takes movement over (0.0.103): its scene stops the movement routine (FieldState.MoveCount
            // still for IdleMs) with the player on it. gFldEveRngIdx names the tile but keeps the last one
            // after the game moves the player off (52F, 16:09: 5 -> 8 -> 11 -> 9, never -1), so a bridge crossed twice in a row
            // (77F right: 01eve_39 three times, 77F left: 01eve_60 twice) showed no change; 0.0.99 to 0.0.102 counted it once.
            // The player within TileReach of the tile tells a firing from a menu opened later on a block (blocks lie 240 or more
            // from their bridges).
            int moves = FieldState.MoveCount;
            if (moves != _moves)
            {
                _moves = moves;
                _movedMs = Clock.ElapsedMilliseconds;
                _armed = true;
                return;
            }
            if (!_armed || Clock.ElapsedMilliseconds - _movedMs < IdleMs)
                return;
            _armed = false;
            if (!_on || steps == null || name.Length == 0)
                return;
            Vector4 at = ranges[i].sisei?.pos ?? default;
            float dx = gb.playerX - at.x, dz = gb.playerZ - at.z;
            if (dx * dx + dz * dz > TileReach * TileReach)
            {
                Log.Info($"[Nav:Puzzle] The game took movement {FieldData.F((float)Math.Sqrt(dx * dx + dz * dz))} from {name}: not a tile firing.");
                return;
            }
            // The tile just fired: the step after it is next (a later step counts too, so a run that skips ahead follows on).
            for (int k = _done; k < steps.Length; k++)
            {
                if (Array.IndexOf(steps[k].Volume.Split('|'), name) < 0)
                    continue;
                _done = k + 1;
                _lastStep = name;
                Log.Info($"[Nav:Puzzle] Step {k + 1} of {steps.Length} done ({name}); {(_done < steps.Length ? $"next {steps[_done].Volume}" : "all done")}.");
                return;
            }
        }
    }
}
