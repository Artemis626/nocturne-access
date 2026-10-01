using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2Cppfield_H;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NocturneAccess.Core;
using NocturneAccess.Systems.Navigation;
using UnityEngine;
// Inside NocturneAccess, a bare "Input" means the NocturneAccess.Input namespace.
using UnityInput = UnityEngine.Input;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Navigation diagnostic, read only (docs/NAVIGATION-DESIGN.md section 4 step 1 and section 5 step 1).
    /// Installed only when [NocturneAccess_Navigation] Diagnostics = true.
    ///
    /// - [Nav:Change]: fieldID, areaID, blockID, AmSBlock, AmFloor, ElvIdx, cammode, cammode2,
    ///   syukan_cam_reset_flg, fldEveHit.gFldEveRngIdx / OldHitDoor / OldHitInf, the collision rebuild and the
    ///   place ("basyo") of the floor under the player, each logged when it changes, with game names.
    /// - [Nav:Trace]: while exploring, at most every TraceIntervalMs and only when something changed: fldGb
    ///   position and facing, the player and camera objects, cammode, the floor polygon under the player, the
    ///   field-update dispatch (fldPlayerCalc_Nml / _syukan calls) and the logical actions that answered true
    ///   in SteamInputAssign.padcheck (D = DOWN, T = TRIG, R = REP).
    /// - [Nav:Gate]: pressing V (or padcheck answering true for FD_Subjectivity) starts a 1 s capture of every
    ///   EventBit.evtBitCheck and dds3ConfigMain.cfgGetBit call, compared with the calls seen outside captures.
    ///   The checks of the frame before the trigger are included, since the game may read V before OnUpdate.
    /// The F5 dump (NavDump) adds the baseline tables.
    /// </summary>
    internal static class NavProbe
    {
        private const long CaptureMs = 1000;
        private const int ActionCount = (int)SIActionName.MAX;
        private const int MaxFlagLines = 60;
        private static readonly string[] PressTypes = { "DOWN", "TRIG", "REP" };

        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        internal static bool Installed { get; private set; }

        // padcheck tallies, per action and press type
        private static readonly long[,] PadCalls = new long[ActionCount, 3];
        private static readonly long[,] PadTrues = new long[ActionCount, 3];
        private static readonly int[] TraceTrueTypes = new int[ActionCount];
        private static int _padIndexMask;

        // field update dispatch
        private static long _nmlCalls;
        private static long _syukanCalls;
        private static bool _syukanSeen;

        internal static readonly CheckRecorder EvtBits = new CheckRecorder();
        internal static readonly CheckRecorder CfgBits = new CheckRecorder();

        // V capture
        private static bool _capturing;
        private static long _captureStartMs;
        private static string _captureTrigger = "";
        private static string _captureBefore = "";
        private static long _capNml, _capSyukan;
        private static int _capCalc;
        private static readonly long[] CapSubjCalls = new long[3];
        private static readonly long[] CapSubjTrues = new long[3];

        // trace
        private static long _lastTraceMs;
        private static bool _haveTrace;
        private static PlayerSnapshot _lastTrace;
        private static long _traceNml, _traceSyukan;
        private static int _basyo = int.MinValue;

        // change watcher
        private static readonly Dictionary<string, int> Watched = new Dictionary<string, int>();

        private static int _errors;

        internal static long NmlCalls => _nmlCalls;
        internal static long SyukanCalls => _syukanCalls;
        internal static int PadIndexMask => _padIndexMask;

        internal static void Install()
        {
            if (!Config.NavDiagnostics.Value)
            {
                Log.Info("[Nav] Diagnostics off (Diagnostics = false in the config). Probe hooks not installed.");
                return;
            }

            Type i = typeof(int);
            Patcher.Postfix("Navigation probe", typeof(SteamInputAssign), nameof(SteamInputAssign.padcheck),
                new[] { i, typeof(SIActionName), typeof(SIPressType) }, AccessTools.Method(typeof(NavProbe), nameof(AfterPadcheck)));
            Patcher.Postfix("Navigation probe", typeof(EventBit), nameof(EventBit.evtBitCheck), new[] { i },
                AccessTools.Method(typeof(NavProbe), nameof(AfterEvtBitCheck)));
            Patcher.Postfix("Navigation probe", typeof(dds3ConfigMain), nameof(dds3ConfigMain.cfgGetBit), new[] { typeof(uint) },
                AccessTools.Method(typeof(NavProbe), nameof(AfterCfgGetBit)));
            Patcher.Postfix("Navigation probe", typeof(fldPlayer), nameof(fldPlayer.fldPlayerCalc_Nml), Type.EmptyTypes,
                AccessTools.Method(typeof(NavProbe), nameof(AfterCalcNml)));
            Patcher.Postfix("Navigation probe", typeof(fldPlayer), nameof(fldPlayer.fldPlayerCalc_Nml_syukan), Type.EmptyTypes,
                AccessTools.Method(typeof(NavProbe), nameof(AfterCalcSyukan)));
            Installed = true;
            Log.Info($"[Nav] Diagnostics on: trace every {Config.NavTraceIntervalMs.Value} ms while exploring; V starts a story-flag capture.");
        }

        internal static void Tick()
        {
            if (!Installed)
                return;

            try
            {
                long now = Clock.ElapsedMilliseconds;
                EvtBits.EndFrame();
                CfgBits.EndFrame();

                if (UnityInput.GetKeyDown(KeyCode.V))
                    StartCapture("key V");
                if (_capturing && now - _captureStartMs >= CaptureMs)
                    FinishCapture();

                WatchChanges();

                if (!FieldState.IsExploring)
                {
                    Array.Clear(TraceTrueTypes, 0, TraceTrueTypes.Length);
                    return;
                }

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

        // ---- hooks (every body guarded: an exception here would reach the game) ----

        private static void AfterPadcheck(int __0, SIActionName __1, SIPressType __2, bool __result)
        {
            try
            {
                int a = (int)__1, t = (int)__2;
                if ((uint)a >= ActionCount || (uint)t > 2)
                    return;
                if ((uint)__0 < 31)
                    _padIndexMask |= 1 << __0;
                PadCalls[a, t]++;
                if (!__result)
                    return;
                PadTrues[a, t]++;
                TraceTrueTypes[a] |= 1 << t;
                if (__1 == SIActionName.FD_Subjectivity)
                    StartCapture("padcheck FD_Subjectivity " + PressTypes[t]);
            }
            catch
            {
                // diagnostic only
            }
        }

        private static void AfterEvtBitCheck(int __0, bool __result)
        {
            try
            {
                EvtBits.Record(__0, __result ? 1 : 0, FieldState.InPlayerCalc);
            }
            catch
            {
                // diagnostic only
            }
        }

        private static void AfterCfgGetBit(uint __0, int __result)
        {
            try
            {
                CfgBits.Record((int)__0, __result, FieldState.InPlayerCalc);
            }
            catch
            {
                // diagnostic only
            }
        }

        private static void AfterCalcNml() => _nmlCalls++;

        private static void AfterCalcSyukan()
        {
            _syukanCalls++;
            if (_syukanSeen)
                return;
            _syukanSeen = true;
            try
            {
                Log.Info("[Nav:Gate] fldPlayerCalc_Nml_syukan ran for the first time (first-person movement routine).");
            }
            catch
            {
                // diagnostic only
            }
        }

        // ---- V capture ----

        private static void StartCapture(string trigger)
        {
            if (_capturing)
                return;

            _capturing = true;
            _captureStartMs = Clock.ElapsedMilliseconds;
            _captureTrigger = trigger;
            _captureBefore = CamState();
            _capNml = _nmlCalls;
            _capSyukan = _syukanCalls;
            _capCalc = FieldState.CalcCount;
            int s = (int)SIActionName.FD_Subjectivity;
            for (int t = 0; t < 3; t++)
            {
                CapSubjCalls[t] = PadCalls[s, t];
                CapSubjTrues[t] = PadTrues[s, t];
            }
            EvtBits.BeginCapture();
            CfgBits.BeginCapture();
            Log.Info($"[Nav:Gate] Capture started by {trigger}; exploring {FieldState.IsExploring}; {_captureBefore}.");
        }

        private static void FinishCapture()
        {
            _capturing = false;
            List<KeyValuePair<int, CheckStat>> evt = EvtBits.EndCapture();
            List<KeyValuePair<int, CheckStat>> cfg = CfgBits.EndCapture();

            long ms = Clock.ElapsedMilliseconds - _captureStartMs;
            Log.Info($"[Nav:Gate] Capture by {_captureTrigger} ended after {ms} ms: fldPlayerCalc {FieldState.CalcCount - _capCalc} " +
                     $"(Nml {_nmlCalls - _capNml}, syukan {_syukanCalls - _capSyukan}); before {_captureBefore}; after {CamState()}.");

            int s = (int)SIActionName.FD_Subjectivity;
            var sb = new StringBuilder("[Nav:Gate] padcheck FD_Subjectivity during capture:");
            for (int t = 0; t < 3; t++)
                sb.Append($" {PressTypes[t]} {PadCalls[s, t] - CapSubjCalls[t]} calls {PadTrues[s, t] - CapSubjTrues[t]} true;");
            Log.Info(sb.ToString());

            LogCapture("evtBitCheck", evt, EvtBits, annotate: true);
            LogCapture("cfgGetBit", cfg, CfgBits, annotate: false);
            Log.Info("[Nav:Gate] " + NewGame2Line());
        }

        private static void LogCapture(string name, List<KeyValuePair<int, CheckStat>> captured, CheckRecorder recorder, bool annotate)
        {
            Dictionary<int, CheckStat> baseline = recorder.BaselineCopy();
            Log.Info($"[Nav:Gate] {name}: {captured.Count} distinct values during the capture (with the frame before); " +
                     $"{baseline.Count} seen outside captures. Rarest outside captures first.");

            IEnumerable<KeyValuePair<int, CheckStat>> ordered = captured
                .OrderBy(kv => baseline.TryGetValue(kv.Key, out CheckStat b) ? b.Calls : 0)
                .ThenByDescending(kv => kv.Value.Calls);
            int lines = 0;
            foreach (KeyValuePair<int, CheckStat> kv in ordered)
            {
                if (++lines > MaxFlagLines)
                {
                    Log.Info($"[Nav:Gate]   ... {captured.Count - MaxFlagLines} more");
                    break;
                }
                baseline.TryGetValue(kv.Key, out CheckStat b);
                string note = annotate ? NewGame2Name(kv.Key) : "";
                Log.Info($"[Nav:Gate]   {kv.Key}{note}: capture {kv.Value} | outside {(b == null ? "never" : b.ToString())}");
            }
        }

        private static string CamState()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return "fldGb null";
            return $"cammode {gb.cammode}/{gb.cammode2}, syukan_cam_reset_flg {gb.syukan_cam_reset_flg}, " +
                   $"gFldPlayerSyukanDly {fldPlayer.gFldPlayerSyukanDly}, PlayerKakudo {FieldData.F(gb.PlayerKakudo)}";
        }

        internal static string NewGame2Line()
        {
            return "NewGame2: endcnt " + dds3GlobalWork.NewGame2_endcnt +
                   ", SYS_ENDING0..7 " + string.Join(" ", EndingFlags()) +
                   ", SYS_ZENSHO_ALL " + dds3GlobalWork.NewGame2_SYS_ZENSHO_ALL +
                   ", EV_ZENSYO_ALL " + dds3GlobalWork.NewGame2_EV_ZENSYO_ALL;
        }

        private static int[] EndingFlags() => new[]
        {
            dds3GlobalWork.NewGame2_SYS_ENDING0, dds3GlobalWork.NewGame2_SYS_ENDING1, dds3GlobalWork.NewGame2_SYS_ENDING2,
            dds3GlobalWork.NewGame2_SYS_ENDING3, dds3GlobalWork.NewGame2_SYS_ENDING4, dds3GlobalWork.NewGame2_SYS_ENDING5,
            dds3GlobalWork.NewGame2_SYS_ENDING6, dds3GlobalWork.NewGame2_SYS_ENDING7,
        };

        /// <summary>" (= NewGame2_SYS_ENDINGn)" when a flag number equals one of the NewGame2 statics.</summary>
        internal static string NewGame2Name(int flag)
        {
            try
            {
                int[] endings = EndingFlags();
                for (int n = 0; n < endings.Length; n++)
                    if (endings[n] == flag)
                        return $" (= NewGame2_SYS_ENDING{n})";
                if (dds3GlobalWork.NewGame2_SYS_ZENSHO_ALL == flag)
                    return " (= NewGame2_SYS_ZENSHO_ALL)";
                if (dds3GlobalWork.NewGame2_EV_ZENSYO_ALL == flag)
                    return " (= NewGame2_EV_ZENSYO_ALL)";
            }
            catch
            {
                // statics unavailable
            }
            return "";
        }

        // ---- change watcher and trace ----

        private static void WatchChanges()
        {
            // Field statics are only read once the game's own field update has run.
            if (FieldState.CalcCount == 0)
                return;

            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;

            int field = gb.fieldID;
            Watch("fieldID", field);
            int area = gb.areaID;
            Watch("areaID", area, v => FieldData.Q(FieldData.AreaName(field, v)));
            Watch("blockID", gb.blockID);
            Watch("AmSBlock", gb.AmSBlock);
            Watch("AmFloor", gb.AmFloor, v => FieldData.Q(FieldData.FloorName(field, v)));
            Watch("ElvIdx", gb.ElvIdx);
            Watch("cammode", gb.cammode);
            Watch("cammode2", gb.cammode2);
            Watch("syukan_cam_reset_flg", gb.syukan_cam_reset_flg);
            Watch("gFldEveRngIdx", fldEveHit.gFldEveRngIdx, EventRangeName);
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

            string keys = TakeTraceKeys();
            string analog = AnalogInput.TakeActivity();
            long nml = _nmlCalls - _traceNml, syukan = _syukanCalls - _traceSyukan;
            _traceNml = _nmlCalls;
            _traceSyukan = _syukanCalls;

            bool changed = !_haveTrace
                           || Math.Abs(s.X - _lastTrace.X) > 0.001f || Math.Abs(s.Y - _lastTrace.Y) > 0.001f || Math.Abs(s.Z - _lastTrace.Z) > 0.001f
                           || s.Kakudo != _lastTrace.Kakudo || s.KakudoGoal != _lastTrace.KakudoGoal
                           || s.CamMode != _lastTrace.CamMode || s.CamMode2 != _lastTrace.CamMode2
                           || Math.Abs(s.CamYaw - _lastTrace.CamYaw) > 1f || Math.Abs(s.ObjYaw - _lastTrace.ObjYaw) > 0.01f
                           || syukan > 0 || keys.Length > 0 || analog.Length > 0;
            if (!changed)
                return;

            _haveTrace = true;
            _lastTrace = s;
            string floorText = floor == null ? "none" : $"#{floor.Index} y {FieldData.F(planeY)} basyo {basyo}";
            Log.Info($"[Nav:Trace] pos {FieldData.F(s.X)} {FieldData.F(s.Y)} {FieldData.F(s.Z)} kakudo {FieldData.F(s.Kakudo)} goal {FieldData.F(s.KakudoGoal)}" +
                     $" | obj {(s.HasObject ? FieldData.V(s.ObjPos) + " yaw " + FieldData.F(s.ObjYaw) : "none")}" +
                     $" | cam {(s.HasCamera ? FieldData.V(s.CamPos) + " yaw " + FieldData.F(s.CamYaw) : "none")}" +
                     $" | cammode {s.CamMode}/{s.CamMode2} act {s.Act} run {(s.Run ? 1 : 0)} dir {s.KeyInputDir}" +
                     $" | floor {floorText} | calc nml {nml} syukan {syukan} | keys {(keys.Length == 0 ? "-" : keys)} | analog {(analog.Length == 0 ? "-" : analog)}");
        }

        private static string TakeTraceKeys()
        {
            var sb = new StringBuilder();
            for (int a = 0; a < ActionCount; a++)
            {
                int types = TraceTrueTypes[a];
                if (types == 0)
                    continue;
                TraceTrueTypes[a] = 0;
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append((SIActionName)a).Append(':');
                if ((types & 1) != 0) sb.Append('D');
                if ((types & 2) != 0) sb.Append('T');
                if ((types & 4) != 0) sb.Append('R');
            }
            return sb.ToString();
        }

        /// <summary>One line per action that padcheck was asked about: calls and true answers per press type.</summary>
        internal static IEnumerable<string> PadcheckLines(bool onlyInteresting)
        {
            for (int a = 0; a < ActionCount; a++)
            {
                long calls = PadCalls[a, 0] + PadCalls[a, 1] + PadCalls[a, 2];
                long trues = PadTrues[a, 0] + PadTrues[a, 1] + PadTrues[a, 2];
                if (calls == 0 && onlyInteresting)
                    continue;
                if (onlyInteresting && trues == 0 && a != (int)SIActionName.FD_Subjectivity)
                    continue;
                var sb = new StringBuilder($"{(SIActionName)a}:");
                for (int t = 0; t < 3; t++)
                    sb.Append($" {PressTypes[t]} {PadCalls[a, t]} calls {PadTrues[a, t]} true;");
                yield return sb.ToString();
            }
        }
    }

    /// <summary>Calls of one checked value (an EventBit flag, a config bit) with their answers.</summary>
    internal sealed class CheckStat
    {
        public long Calls, InCalc, NonZero, Zero;
        public int Last;

        public void Add(int result, bool inCalc)
        {
            Calls++;
            if (inCalc) InCalc++;
            if (result != 0) NonZero++; else Zero++;
            Last = result;
        }

        public void AddAll(CheckStat o)
        {
            Calls += o.Calls;
            InCalc += o.InCalc;
            NonZero += o.NonZero;
            Zero += o.Zero;
            Last = o.Last;
        }

        public void Subtract(CheckStat o)
        {
            Calls -= o.Calls;
            InCalc -= o.InCalc;
            NonZero -= o.NonZero;
            Zero -= o.Zero;
        }

        public CheckStat Clone() => new CheckStat { Calls = Calls, InCalc = InCalc, NonZero = NonZero, Zero = Zero, Last = Last };

        public override string ToString() => $"{Calls} calls ({InCalc} in fldPlayerCalc), nonzero {NonZero} zero {Zero}, last {Last}";
    }

    /// <summary>
    /// Counts calls per checked value outside captures (the baseline) and during a capture. The previous and
    /// current frame are kept apart so a capture can include the frame in which the game read the key.
    /// </summary>
    internal sealed class CheckRecorder
    {
        private readonly object _lock = new object();
        private readonly Dictionary<int, CheckStat> _baseline = new Dictionary<int, CheckStat>();
        private readonly Dictionary<int, CheckStat> _capture = new Dictionary<int, CheckStat>();
        private Dictionary<int, CheckStat> _frame = new Dictionary<int, CheckStat>();
        private Dictionary<int, CheckStat> _prevFrame = new Dictionary<int, CheckStat>();
        private bool _capturing;

        public void Record(int key, int result, bool inCalc)
        {
            lock (_lock)
            {
                if (_capturing)
                {
                    Get(_capture, key).Add(result, inCalc);
                    return;
                }
                Get(_baseline, key).Add(result, inCalc);
                Get(_frame, key).Add(result, inCalc);
            }
        }

        public void EndFrame()
        {
            lock (_lock)
            {
                (_prevFrame, _frame) = (_frame, _prevFrame);
                _frame.Clear();
            }
        }

        public void BeginCapture()
        {
            lock (_lock)
            {
                _capture.Clear();
                foreach (Dictionary<int, CheckStat> f in new[] { _prevFrame, _frame })
                {
                    foreach (KeyValuePair<int, CheckStat> kv in f)
                    {
                        Get(_capture, kv.Key).AddAll(kv.Value);
                        if (_baseline.TryGetValue(kv.Key, out CheckStat b))
                        {
                            b.Subtract(kv.Value);
                            if (b.Calls <= 0)
                                _baseline.Remove(kv.Key);
                        }
                    }
                    f.Clear();
                }
                _capturing = true;
            }
        }

        public List<KeyValuePair<int, CheckStat>> EndCapture()
        {
            lock (_lock)
            {
                _capturing = false;
                return _capture.Select(kv => new KeyValuePair<int, CheckStat>(kv.Key, kv.Value.Clone())).ToList();
            }
        }

        public Dictionary<int, CheckStat> BaselineCopy()
        {
            lock (_lock)
            {
                return _baseline.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
            }
        }

        private static CheckStat Get(Dictionary<int, CheckStat> d, int key)
        {
            if (!d.TryGetValue(key, out CheckStat s))
            {
                s = new CheckStat();
                d[key] = s;
            }
            return s;
        }
    }
}
