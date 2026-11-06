using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// The Red Temple's alarm told by sound (0.0.158; Francesco, 6 Nov 2026: "a simple audio cue with a protective delay should also
    /// announce the activation of the shadow zone, and vice versa"). In the Amala Temple's Red Temple (field 34, areas 11 to 17) a
    /// shadow sets story flag 1703 (013_01eve_02 and the rest: COMM 0x8), which stops the warp devices and puts the shadows to
    /// sleep; the entrance hall's tiles and area 16's tiles clear it (COMM 0x9). A change is played only once the flag has kept its
    /// new value for SettleMs with the player exploring (the catch runs a short scene first, and a load or area change is not a
    /// change: the first reading in the temple is only remembered), and never sooner than GapMs after the last cue.
    /// </summary>
    internal static class AlarmCue
    {
        private const int Field = 34, FirstArea = 11, LastArea = 17, Flag = 1703;
        private const long SettleMs = 1000, GapMs = 2000;

        private static bool _known, _state, _pending;
        private static bool _pendingState;
        private static long _changedAt, _lastCueAt = long.MinValue / 2;
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();

        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || gb.fieldID != Field || gb.areaID < FirstArea || gb.areaID > LastArea)
            {
                // Outside the temple (a battle field too): keep what was known, so a battle does not count as a change.
                if (gb == null || gb.fieldID < 100)
                    _known = false;
                _pending = false;
                return;
            }
            bool on = EventBit.evtBitCheck(Flag);
            long now = Clock.ElapsedMilliseconds;
            if (!_known)
            {
                _known = true;
                _state = on;
                _pending = false;
                return;
            }
            if (on != _state)
            {
                if (!_pending || _pendingState != on)
                {
                    _pending = true;
                    _pendingState = on;
                    _changedAt = now;
                }
                if (now - _changedAt < SettleMs || !FieldState.IsExploring || now - _lastCueAt < GapMs)
                    return;
                _state = on;
                _pending = false;
                _lastCueAt = now;
                Log.Info($"[Nav:Alarm] Field {gb.fieldID} area {gb.areaID}: flag {Flag} {(on ? "on (the shadows' alarm)" : "off (reset)")}: tone{((Config.AlarmSound?.Value ?? true) ? "" : " (off in the config)")}.");
                if (Config.AlarmSound?.Value ?? true)
                {
                    if (on)
                        Earcons.AlarmOn();
                    else
                        Earcons.AlarmOff();
                }
                return;
            }
            _pending = false;
        }
    }
}
