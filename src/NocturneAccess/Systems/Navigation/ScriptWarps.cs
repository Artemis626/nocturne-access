using System;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// "You've been warped." (Francesco's words, 22 Nov 2026, the Diet Building): when an event volume the player walked into moves
    /// the player by its own script, the line is said once the player has control again, held against key presses. The game plays
    /// its own sound, so no tone. The move is the script command CALL_FIELD2 (0x98, `evtCommand.evtCommand_CALL_FIELD2`, a
    /// postfix): the Diet Building's trick paintings (f033.bf: areas 7 to 18, to the same area or the next, "You entered the
    /// painting."), the Yurakucho Tunnel's slides, Kabukicho's cell that sends the player back. Only when the volume the player
    /// stands in (`fldEveHit.gFldEveRngIdx`) has a procedure that places the player (`FieldScript.Placements`): area start
    /// procedures and other scripted scenes say nothing. Door-table warps keep ExploredDoors' "Warped.".
    /// </summary>
    internal static class ScriptWarps
    {
        private const string Tag = "[Nav:ScriptWarp]";
        internal const string Words = "You've been warped.";
        private const long SettleMs = 400, GiveUpMs = 30000;
        private const int HoldMs = 2500;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _calledMs = -1, _exploringSinceMs = -1;
        private static string _volume = "";
        private static int _field = -1, _area = -1;

        internal static void Install()
        {
            Patcher.Postfix("Script warps", typeof(evtCommand), nameof(evtCommand.evtCommand_CALL_FIELD2), Type.EmptyTypes,
                AccessTools.Method(typeof(ScriptWarps), nameof(AfterCallField2)));
        }

        private static void AfterCallField2()
        {
            try
            {
                fldGlobalWork_t gb = fldGlobal.fldGb;
                if (gb == null || gb.fieldID <= 0 || gb.fieldID >= 100)
                    return;
                int idx = fldEveHit.gFldEveRngIdx;
                var ranges = fldEveHit.gEvnRng;
                string name = ranges != null && idx >= 0 && idx < ranges.Length && idx < fldEveHit.gEvnRngNum ? ranges[idx]?.resourceName ?? "" : "";
                if (name.Length == 0)
                {
                    Log.Info($"{Tag} CALL_FIELD2 in field {gb.fieldID} area {gb.areaID} with no event volume under the player: not said.");
                    return;
                }
                var paths = FieldScript.Placements(gb.areaID, name);
                if (paths == null || !paths.Any(p => p.Count > 0))
                {
                    Log.Info($"{Tag} CALL_FIELD2 in field {gb.fieldID} area {gb.areaID} while in {name}, whose procedure places no one: not said.");
                    return;
                }
                _calledMs = Clock.ElapsedMilliseconds;
                _exploringSinceMs = -1;
                _volume = name;
                _field = gb.fieldID;
                _area = gb.areaID;
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} {ex.Message}");
            }
        }

        internal static void Tick()
        {
            if (_calledMs < 0)
                return;
            long now = Clock.ElapsedMilliseconds;
            if (now - _calledMs > GiveUpMs)
            {
                Log.Info($"{Tag} {_volume}: the player had no control within {GiveUpMs / 1000} s; not said.");
                _calledMs = -1;
                return;
            }
            if (!FieldState.IsExploring)
            {
                _exploringSinceMs = -1;
                return;
            }
            if (_exploringSinceMs < 0)
                _exploringSinceMs = now;
            if (now - _exploringSinceMs < SettleMs)
                return;
            fldGlobalWork_t gb = fldGlobal.fldGb;
            Log.Info($"{Tag} {_field}/{_area}/{_volume} moved the player to area {gb?.areaID} at {FieldData.F(gb?.playerX ?? 0)} {FieldData.F(gb?.playerZ ?? 0)}: \"{Words}\"");
            SpeechManager.SayHeld(Words, HoldMs);
            _calledMs = -1;
        }
    }
}
