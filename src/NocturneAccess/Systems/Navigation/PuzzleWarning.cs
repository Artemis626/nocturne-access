using System.Collections.Generic;
using System.Diagnostics;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// A warning where a puzzle cannot be solved blind without the way on (0.0.207; Francesco, 27 Nov 2026: the Kagutsuchi Tower's
    /// magic blocks, unlike earlier way-on places, "look straight up undoable" without it, and since 0.0.198 a player may have
    /// switched the way on off for the whole game). Said once per load (ExploredDoors.ClearAll calls ForgetHeard), on first
    /// reaching such an area while the way on is off, once the player has been exploring for SettleMs with no dialogue due, and
    /// held against interrupts, as the chase line is.
    /// </summary>
    internal static class PuzzleWarning
    {
        // Field 36 areas 12 to 14: the magic blocks south of the second Terminal (switching walls, docs/class-index.md)
        private static readonly HashSet<(int field, int area)> Areas = new HashSet<(int, int)> { (36, 12), (36, 13), (36, 14) };

        internal const string Words = "This floor contains a puzzle challenge considered unsolvable without the way on indications, " +
                                      "which can be turned on or off in the mod's settings window, Shift+F, Navigation. " +
                                      "If you've set it to off so far and are having trouble, consider setting it to on.";   // Francesco's words, 27 Nov 2026

        private const long SettleMs = 2000;
        private const int HoldMs = 9000;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static bool _told;
        private static long _settledSince = -1;

        internal static void ForgetHeard()
        {
            _told = false;
            _settledSince = -1;
        }

        internal static void Tick()
        {
            if (_told || WayOn.Enabled)
                return;
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || !Areas.Contains((gb.fieldID, gb.areaID)) || !FieldState.IsExploring
                || Dialogue.DialogueReader.PageDue || Dialogue.DialogueReader.SpokeWithin(SettleMs))
            {
                _settledSince = -1;
                return;
            }
            long now = Clock.ElapsedMilliseconds;
            if (_settledSince < 0)
                _settledSince = now;
            if (now - _settledSince < SettleMs)
                return;
            _told = true;
            Log.Info($"[WayOn] Said in field {gb.fieldID} area {gb.areaID}: the puzzle warning (the way on is off).");
            SpeechManager.SayHeld(Words, HoldMs);
        }
    }
}
