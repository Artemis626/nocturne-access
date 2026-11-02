using System;
using System.Diagnostics;
using System.Text;
using Il2Cpp;
using NocturneAccess.Core;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Log only (0.0.142; Francesco, 2 Nov 2026: step 1 of making the Third Kalpa's chase playable). Raidou (Dante in the
    /// other edition) is not an NPC but the game's own chase AI, fldTest.fldRun_Dante (FLDRUN_DANTE_t), run by fldTest.fldRunView;
    /// read offline: while story flag 2803 is set, fldTest.fldRunEncountChk catches the player when the x-z distance to him is
    /// under 250. Every SampleMs while that flag is on (and at once when his AI state changes): his state, position, facing,
    /// speed, route, the game's own distance and direction to the player, and the player's position. Nothing is said or changed;
    /// the log tells his patrol, what each state number means and how near he comes before a catch.
    /// </summary>
    internal static class ChaseProbe
    {
        private const int ChaseFlag = 2803;   // fldRunEncountChk tests it first (0xAF3)
        private const long SampleMs = 250;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _lastMs = long.MinValue / 2;
        private static bool _in;
        private static int _samples, _lastStatus = int.MinValue, _errors;

        internal static void Tick()
        {
            Il2Cppfield_H.fldGlobalWork_t gb = fldGlobal.fldGb;
            bool on;
            try
            {
                on = gb != null && gb.fieldID < 100 && EventBit.evtBitCheck(ChaseFlag);
            }
            catch
            {
                on = false;
            }
            if (on != _in)
            {
                _in = on;
                if (on)
                    Begin(gb);
                else
                    Log.Info($"[Chase] Flag {ChaseFlag} off (field {gb?.fieldID} area {gb?.areaID}) after {_samples} samples.");
                _samples = 0;
                _lastStatus = int.MinValue;
            }
            if (!on)
                return;
            long now = Clock.ElapsedMilliseconds;
            try
            {
                fldTest.FLDRUN_DANTE_t d = fldTest.fldRun_Dante;
                if (d == null)
                    return;
                bool changed = d.status != _lastStatus;
                if (!changed && now - _lastMs < SampleMs)
                    return;
                _lastMs = now;
                float dx = gb.playerX - d.x, dz = gb.playerZ - d.z;
                var sb = new StringBuilder();
                sb.Append($"[Chase] {(changed ? "state " : "")}t {now} area {gb.areaID} st {d.status} (was {d.old_status}) mode {d.wkmode} stance {d.stance} motion {d.motion} ");
                sb.Append($"at {d.x:0} {d.y:0} {d.z:0} dir {d.dir:0.###} speed {d.speed:0.##} cell {d.ix},{d.iz} goal {d.goal_ix},{d.goal_iz} ");
                sb.Append($"route {d.RouteIdx}/{d.RouteDataTop} now {d.now} goal {d.goal} tikamiti {d.tikamiti} cnt {d.cnt_ex} gun {d.gunlight_anime} upd {d.update_flag} ");
                sb.Append($"| d_p_len {d.d_p_len:0} d_p_dir {d.d_p_dir:0.###} d_c_dir {d.d_c_dir:0.###} | player {gb.playerX:0} {gb.playerZ:0} xz {Math.Sqrt(dx * dx + dz * dz):0}");
                fldTest.FLDRUN_DOOR_t door = d.pDoor;
                if (door != null)
                    sb.Append($" | door {door.name} {door.ix},{door.iz} dir {door.dir:0.###}");
                fldTest.FLDRUN_PLAYER_t p = fldTest.fldRun_Player;
                if (p != null)
                    sb.Append($" | player cell {p.ix},{p.iz}");
                Log.Info(sb.ToString());
                _lastStatus = d.status;
                _samples++;
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Warn($"[Chase] Sample failed: {ex.Message}");
                _lastMs = now + 5000;   // not every frame
            }
        }

        /// <summary>Once per chase: the map grid and the doors the AI knows.</summary>
        private static void Begin(Il2Cppfield_H.fldGlobalWork_t gb)
        {
            try
            {
                var sb = new StringBuilder($"[Chase] Flag {ChaseFlag} on in field {gb.fieldID} area {gb.areaID}: sampling every {SampleMs} ms. ");
                sb.Append($"Map O {fldTest.fldRun_MAP_OX},{fldTest.fldRun_MAP_OZ} B {fldTest.fldRun_MAP_BX},{fldTest.fldRun_MAP_BZ}; doors:");
                var doors = fldTest.fldRunDoorTbl;
                for (int i = 0; doors != null && i < doors.Length; i++)
                    if (doors[i] != null)
                        sb.Append($" {doors[i].name} {doors[i].ix},{doors[i].iz} dir {doors[i].dir:0.###};");
                Log.Info(sb.ToString());
            }
            catch (Exception ex)
            {
                Log.Warn($"[Chase] Start line failed: {ex.Message}");
            }
        }
    }
}
