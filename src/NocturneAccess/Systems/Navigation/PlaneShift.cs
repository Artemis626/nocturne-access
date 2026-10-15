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
    /// mirage."), and the player stays where they stood (-2086 2866 to -2100 2750). So: an area change of this field started
    /// from an event volume (not a door, not a "Jump Off", which drops to the floor below 275 away), landing within ShiftReach
    /// of where the player stood. Said once the game gives control back, after its own messages and the location banner, in
    /// Francesco's words.
    /// </summary>
    internal static class PlaneShift
    {
        private const int KabukichoPrison = 25;
        private const float ShiftReach = 200f;
        private const long FromEventMs = 3000, SettleMs = 800, PendingMs = 15000;
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

        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;
            long now = Clock.ElapsedMilliseconds;
            if (gb.fieldID != _field || gb.areaID != _area)
            {
                bool shift = gb.fieldID == KabukichoPrison && _field == KabukichoPrison && _event != null && now - _eventMs <= FromEventMs;
                if (shift)
                {
                    _pending = true;
                    _changeMs = now;
                    Log.Info($"[Nav:Plane] Area {_area} to {gb.areaID} from event {_event}: a shift if the player stays put.");
                }
                _field = gb.fieldID;
                _area = gb.areaID;
                _event = null;
                _checked = null;
                return;
            }

            if (_pending)
            {
                if (now - _changeMs > PendingMs)
                    _pending = false;
                else if (FieldState.IsExploring && now - _changeMs >= SettleMs)
                {
                    _pending = false;
                    float dx = gb.playerX - _eventX, dz = gb.playerZ - _eventZ;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d <= ShiftReach)
                    {
                        Log.Info($"[Nav:Plane] Walking plane shifted in field {gb.fieldID}: now area {gb.areaID}, {FieldData.F(d)} from where the player stood.");
                        SpeechManager.Say(Words, interrupt: false);
                    }
                    else
                        Log.Info($"[Nav:Plane] Not a shift: the player landed {FieldData.F(d)} away.");
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
