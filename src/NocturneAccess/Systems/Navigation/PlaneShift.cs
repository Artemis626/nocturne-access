using System;
using System.Diagnostics;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Kabukicho Prison's mirage (0.0.83; Francesco, 15 Oct 2026: the game is vague when the Umugi Stone shifts the walking
    /// plane). Using the stone at a "Mysterious Wall" swaps the building for its mirage and back: the game changes the area
    /// (15:20:25 area 19 to 3, "The mirage is disappearing."; 15:24:01 area 1 to 17, "The building has been shrouded in a
    /// mirage."), and the player stays about where they stood.
    ///
    /// 0.0.84: a shift is an area change of this field, started from an event volume (not a door, not a "Jump Off"), between an
    /// area of 0 to 3 and the same area plus 16 (MirageOffset). The field script agrees: the six Mysterious Wall procedures
    /// (001_01eve_01, 002_01eve_06, 004_01eve_02, 017_01eve_05, 018_01eve_05, 020_01eve_05, areas 0, 1, 3, 16, 17, 19) are the
    /// ones that call fldCommand_FLD_CALL_WAPEX (9 to 14) in those areas. 0.0.83 measured the distance from where the player
    /// stood instead (200 at most): the wall of area 1 puts the player 233 away, so entering the mirage there was never said
    /// (20:46, 20:55, 21:24, 21:45), while the Manikin's hole of area 12 drops 317 away and must not count.
    /// Said once the game gives control back, after its own messages, in Francesco's words, held against interrupts for HoldMs
    /// (21:29:44: a filter key 0.7 s later cut it).
    /// </summary>
    internal static class PlaneShift
    {
        private const int KabukichoPrison = 25;
        private const int MirageOffset = 16, MirageAreas = 4;   // areas 0..3 and their mirage 16..19
        private const long FromEventMs = 3000, PendingMs = 15000;
        private const int HoldMs = 4000;
        internal const string Words = "Walking plane shifted: floor and ceiling have swapped places.";

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly System.Text.RegularExpressions.Regex EventName = FieldData.EventVolume;

        private static int _field = int.MinValue, _area = int.MinValue;
        private static string _event;              // the last event volume stood in (not a jump)
        private static long _eventMs = long.MinValue / 2;
        private static float _eventX, _eventZ;
        private static string _checked;            // the volume last asked whether it is a jump
        private static bool _checkedJump;
        private static bool _pending;
        private static long _changeMs;

        /// <summary>Areas a and b are one floor of the prison and its mirage.</summary>
        private static bool MiragePair(int a, int b)
            => Math.Min(a, b) >= 0 && Math.Min(a, b) < MirageAreas && Math.Abs(a - b) == MirageOffset;

        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;
            long now = Clock.ElapsedMilliseconds;
            if (gb.fieldID != _field || gb.areaID != _area)
            {
                bool fromEvent = gb.fieldID == KabukichoPrison && _field == KabukichoPrison && _event != null && now - _eventMs <= FromEventMs;
                if (fromEvent && MiragePair(_area, gb.areaID))
                {
                    _pending = true;
                    _changeMs = now;
                    Log.Info($"[Nav:Plane] Area {_area} to {gb.areaID} from event {_event}: the mirage shift; said once the player has control.");
                }
                else if (fromEvent)
                    Log.Info($"[Nav:Plane] Area {_area} to {gb.areaID} from event {_event}: not a mirage pair, no shift.");
                _field = gb.fieldID;
                _area = gb.areaID;
                _event = null;
                _checked = null;
                return;
            }

            if (_pending)
            {
                if (now - _changeMs > PendingMs)
                {
                    _pending = false;
                    Log.Info("[Nav:Plane] The player did not take control in time: nothing said.");
                }
                else if (FieldState.IsExploring)
                {
                    _pending = false;
                    float dx = gb.playerX - _eventX, dz = gb.playerZ - _eventZ;
                    Log.Info($"[Nav:Plane] Walking plane shifted in field {gb.fieldID}: now area {gb.areaID}, {FieldData.F((float)Math.Sqrt(dx * dx + dz * dz))} from where the player stood, {now - _changeMs} ms after the change.");
                    SpeechManager.SayHeld(Words, HoldMs);
                }
            }

            if (gb.fieldID != KabukichoPrison)
                return;
            int i = fldEveHit.gFldEveRngIdx;
            if (i < 0)
                return;
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (ranges == null || i >= Math.Min(fldEveHit.gEvnRngNum, ranges.Length))
                return;
            fldResHead_t h = ranges[i];
            string name = h?.resourceName ?? "";
            if (h == null || h.resourceType != 10 || !EventName.IsMatch(name))
                return;
            if (name != _checked)
            {
                _checked = name;
                _checkedJump = ExploredDoors.IsJumpOff(gb, name);
            }
            if (_checkedJump)
            {
                _event = null;
                return;
            }
            _event = name;
            _eventMs = now;
            _eventX = gb.playerX;
            _eventZ = gb.playerZ;
        }
    }
}
