using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Systems.Navigation;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Navigation diagnostic, read only. Runs only when [NocturneAccess_Navigation] Diagnostics = true.
    /// - [Nav:Change]: fieldID, areaID, blockID, AmSBlock, AmFloor, ElvIdx, cammode, cammode2, syukan_cam_reset_flg,
    ///   fldEveHit.gFldEveRngIdx / OldHitDoor / OldHitInf, the collision rebuild and the place ("basyo") of the floor under
    ///   the player, each logged when it changes, with game names.
    /// - [Nav:Trace]: while exploring, at most every TraceIntervalMs and only when something changed: fldGb position and
    ///   facing, the player and camera objects, cammode, the floor polygon under the player, the movement routines that ran
    ///   (fldPlayerCalc_Nml / _syukan) and the pad buttons the player really pressed (PadInput).
    /// 0.0.67: the V story-flag capture (every evtBitCheck and cfgGetBit call, the padcheck tallies, the New Game+ statics) is
    /// gone; it served the first-person research of 0.0.12 to 0.0.18, whose results are in docs/NAVIGATION-DESIGN.md
    /// section 8. Its three hooks ran on some of the game's most frequent calls.
    /// </summary>
    internal static class NavProbe
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        internal static bool Installed { get; private set; }

        private static long _lastTraceMs;
        private static bool _haveTrace;
        private static PlayerSnapshot _lastTrace;
        private static int _traceNml, _traceSyukan;
        private static int _basyo = int.MinValue;
        private static bool _syukanSeen;
        private static int _watchField;
        private static readonly Func<int, string> AreaOf = v => FieldData.Q(FieldData.AreaName(_watchField, v));
        private static readonly Func<int, string> FloorOf = v => FieldData.Q(FieldData.FloorName(_watchField, v));
        private static readonly Func<int, string> RangeOf = EventRangeName;
        private static readonly Dictionary<string, int> Watched = new Dictionary<string, int>();
        private static int _errors;

        internal static void Install()
        {
            if (!Config.NavDiagnostics.Value)
            {
                Log.Info("[Nav] Diagnostics off (Diagnostics = false in the config).");
                return;
            }
            Installed = true;
            Log.Info($"[Nav] Diagnostics on: trace every {Config.NavTraceIntervalMs.Value} ms while exploring.");
        }

        internal static void Tick()
        {
            if (!Installed)
                return;
            try
            {
                WatchChanges();
                if (!FieldState.IsExploring)
                    return;
                long now = Clock.ElapsedMilliseconds;
                if (now - _lastTraceMs >= Math.Max(50, Config.NavTraceIntervalMs.Value))
                {
                    _lastTraceMs = now;
                    Trace();
                }
            }
            catch (Exception ex)
            {
                if (++_errors <= 3)
                    Log.Error($"[Nav] Probe tick failed: {ex}");
            }
        }

        private static void WatchChanges()
        {
            // Field statics are only read once the game's own field update has run.
            if (FieldState.CalcCount == 0)
                return;
            if (!_syukanSeen && FieldState.SyukanCount > 0)
            {
                _syukanSeen = true;
                Log.Info("[Nav:Gate] fldPlayerCalc_Nml_syukan ran for the first time (first-person movement routine).");
            }

            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;

            // The describers are static delegates reading _watchField: no closure is made on every frame (0.0.113).
            _watchField = gb.fieldID;
            Watch("fieldID", _watchField);
            Watch("areaID", gb.areaID, AreaOf);
            Watch("blockID", gb.blockID);
            Watch("AmSBlock", gb.AmSBlock);
            Watch("AmFloor", gb.AmFloor, FloorOf);
            Watch("ElvIdx", gb.ElvIdx);
            Watch("cammode", gb.cammode);
            Watch("cammode2", gb.cammode2);
            Watch("syukan_cam_reset_flg", gb.syukan_cam_reset_flg);
            Watch("gFldEveRngIdx", fldEveHit.gFldEveRngIdx, RangeOf);
            Watch("OldHitDoor", fldEveHit.OldHitDoor);
            Watch("OldHitInf", fldEveHit.OldHitInf);
        }

        private static void Watch(string name, int value, Func<int, string> describe = null)
        {
            bool known = Watched.TryGetValue(name, out int old);
            if (known && old == value)
                return;
            Watched[name] = value;
            string from = known ? old.ToString() : "start";
            string extra = describe == null ? "" : " " + describe(value);
            Log.Info($"[Nav:Change] {name} {from} -> {value}{extra}");
        }

        private static string EventRangeName(int idx)
        {
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (ranges == null || idx < 0 || idx >= ranges.Length || idx >= fldEveHit.gEvnRngNum)
                return "";
            fldResHead_t head = ranges[idx];
            return head == null ? "" : "gEvnRng " + FieldData.Q(head.resourceName);
        }

        private static void Trace()
        {
            PlayerSnapshot s = FieldData.ReadPlayer();
            if (!s.Valid)
                return;

            var watch = Stopwatch.StartNew();
            if (CollisionCache.Refresh(s.FieldId, s.AreaId))
                Log.Info($"[Nav:Change] collision read in {watch.ElapsedMilliseconds} ms: {CollisionCache.Polys.Count} polygons (gfldCollsionNum {CollisionCache.SourceCount}, " +
                         $"array {CollisionCache.ArrayLength}, gfldCollsionBlkNum {CollisionCache.SourceBlockCount}) for field {s.FieldId} area {s.AreaId}");

            CollisionPoly floor = CollisionCache.FloorUnder(s.X, s.Y, s.Z, out float planeY);
            int basyo = floor != null && floor.HasFace ? floor.Basyo : -1;
            if (basyo != _basyo)
            {
                string name = basyo < 0 ? "" : " " + FieldData.Q(FieldData.BasyoName(s.FieldId, basyo));
                Log.Info($"[Nav:Change] basyo (floor polygon under the player) {(_basyo == int.MinValue ? "start" : _basyo.ToString())} -> {basyo}{name}");
                _basyo = basyo;
            }

            string pads = PadInput.TakeTraceButtons();
            int nml = FieldState.NmlCount - _traceNml, syukan = FieldState.SyukanCount - _traceSyukan;
            _traceNml = FieldState.NmlCount;
            _traceSyukan = FieldState.SyukanCount;

            bool changed = !_haveTrace
                           || Math.Abs(s.X - _lastTrace.X) > 0.001f || Math.Abs(s.Y - _lastTrace.Y) > 0.001f || Math.Abs(s.Z - _lastTrace.Z) > 0.001f
                           || s.Kakudo != _lastTrace.Kakudo || s.KakudoGoal != _lastTrace.KakudoGoal
                           || s.CamMode != _lastTrace.CamMode || s.CamMode2 != _lastTrace.CamMode2
                           || Math.Abs(s.CamYaw - _lastTrace.CamYaw) > 1f || Math.Abs(s.ObjYaw - _lastTrace.ObjYaw) > 0.01f
                           || syukan > 0 || pads.Length > 0;
            if (!changed)
                return;

            _haveTrace = true;
            _lastTrace = s;
            string floorText = floor == null ? "none" : $"#{floor.Index} y {FieldData.F(planeY)} basyo {basyo}";
            Log.Info($"[Nav:Trace] pos {FieldData.F(s.X)} {FieldData.F(s.Y)} {FieldData.F(s.Z)} kakudo {FieldData.F(s.Kakudo)} goal {FieldData.F(s.KakudoGoal)}" +
                     $" | obj {(s.HasObject ? FieldData.V(s.ObjPos) + " yaw " + FieldData.F(s.ObjYaw) : "none")}" +
                     $" | cam {(s.HasCamera ? FieldData.V(s.CamPos) + " yaw " + FieldData.F(s.CamYaw) : "none")}" +
                     $" | cammode {s.CamMode}/{s.CamMode2} act {s.Act} run {(s.Run ? 1 : 0)} dir {s.KeyInputDir}" +
                     $" | floor {floorText} | calc nml {nml} syukan {syukan} | pads {(pads.Length == 0 ? "-" : pads)}");
        }
    }
}
