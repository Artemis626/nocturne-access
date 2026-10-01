using System;
using System.Diagnostics;
using Il2Cpp;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Navigation;
using static NocturneAccess.Systems.Navigation.FieldData;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Input proof of concept (docs/NAVIGATION-DESIGN.md section 4 step 2), debug keys:
    /// - Step test (F3): holds CMN_UpFront through InputInjector for 500 ms.
    /// - Turn test (F4): holds FD_Turn_Right until PlayerKakudo or the camera yaw has changed by 90 degrees,
    ///   at most 1500 ms.
    /// Both run only while exploring and stop at once if exploring stops (battle, event, menu). Each result is
    /// measured at release and again 500 ms later (the facing eases), logged as [Nav:Test] and spoken briefly.
    /// </summary>
    internal static class MoveTest
    {
        private const long StepMs = 500;
        private const long TurnMaxMs = 1500;
        private const long SettleMs = 500;

        private enum Phase { Idle, Holding, Settling }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static Phase _phase = Phase.Idle;
        private static string _name = "";
        private static SIActionName _action;
        private static long _startMs, _releaseMs, _injectedAtStart;
        private static PlayerSnapshot _start;
        private static string _atRelease = "";

        internal static void StepTest() => Start("Step test", SIActionName.CMN_UpFront);

        internal static void TurnTest() => Start("Turn test", SIActionName.FD_Turn_Right);

        private static void Start(string name, SIActionName action)
        {
            if (_phase != Phase.Idle)
                return;
            if (!FieldState.IsExploring)
            {
                SpeechManager.Say("Not exploring.", interrupt: true, force: true);
                return;
            }

            _start = ReadPlayer();
            if (!_start.Valid)
                return;

            _name = name;
            _action = action;
            _startMs = Clock.ElapsedMilliseconds;
            _injectedAtStart = InputInjector.Injected;
            InputInjector.Hold(action, true);
            _phase = Phase.Holding;
            Log.Info($"[Nav:Test] {name}: holding {action}. Start {Describe(_start)}");
        }

        internal static void Tick()
        {
            if (_phase == Phase.Idle)
                return;

            long now = Clock.ElapsedMilliseconds;
            if (!FieldState.IsExploring)
            {
                InputInjector.ReleaseAll();
                _phase = Phase.Idle;
                Log.Info($"[Nav:Test] {_name}: stopped, no longer exploring.");
                return;
            }

            PlayerSnapshot s = ReadPlayer();
            if (_phase == Phase.Holding)
            {
                bool done = _action == SIActionName.CMN_UpFront
                    ? now - _startMs >= StepMs
                    : now - _startMs >= TurnMaxMs
                      || Math.Abs(AngleDelta(_start.Kakudo, s.Kakudo)) >= 90
                      || Math.Abs(AngleDelta(_start.CamYaw, s.CamYaw)) >= 90;
                if (!done)
                    return;

                InputInjector.Hold(_action, false);
                _releaseMs = now;
                _atRelease = $"after {now - _startMs} ms, {InputInjector.Injected - _injectedAtStart} injected answers: {Describe(s)}";
                _phase = Phase.Settling;
                return;
            }

            if (now - _releaseMs < SettleMs)
                return;

            _phase = Phase.Idle;
            float dx = s.X - _start.X, dz = s.Z - _start.Z;
            float moved = (float)Math.Sqrt(dx * dx + dz * dz);
            float turned = AngleDelta(_start.Kakudo, s.Kakudo);
            float camTurned = AngleDelta(_start.CamYaw, s.CamYaw);
            Log.Info($"[Nav:Test] {_name}: released {_atRelease}");
            Log.Info($"[Nav:Test] {_name}: settled {Describe(s)}; moved {F(moved)} units (dx {F(dx)} dz {F(dz)}), " +
                     $"facing {F(turned)} degrees, camera {F(camTurned)} degrees.");

            string spoken = _action == SIActionName.CMN_UpFront
                ? (moved > 1 ? $"Step test: moved {Math.Round(moved)}." : "Step test: did not move.")
                : $"Turn test: facing {Math.Round(turned)} degrees, camera {Math.Round(camTurned)} degrees.";
            SpeechManager.Say(spoken, interrupt: true, force: true);
        }

        /// <summary>Signed difference b - a in degrees, wrapped to -180..180.</summary>
        private static float AngleDelta(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180) d -= 360;
            if (d < -180) d += 360;
            return d;
        }

        private static string Describe(PlayerSnapshot s) =>
            $"pos {F(s.X)} {F(s.Z)} kakudo {F(s.Kakudo)} goal {F(s.KakudoGoal)} cam yaw {F(s.CamYaw)} cammode {s.CamMode} dir {s.KeyInputDir} act {s.Act}";
    }
}
