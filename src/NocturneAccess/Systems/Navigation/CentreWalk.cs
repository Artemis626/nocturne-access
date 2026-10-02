using System;
using System.Diagnostics;
using Il2Cpp;
using Il2Cppfield_H;
using Il2Cpplibsdf_H;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Walks the player to the centre of the current cell with the game's own first-person moves (Francesco, 1 Oct 2026:
    /// a centring key, after collision with objects such as the corridor gurneys slid him sideways). Pads are held for
    /// the game exactly as keys would be (docs/NAVIGATION-DESIGN.md section 12):
    /// 1. sideways: pad L1 (Q, side step left) or R1 (E, right) until within half a tick of the centre line;
    /// 2. along the facing: pad U (W) if the centre is ahead; if it is behind, the game has no backward walk, so pad D
    ///    for one tick (S, turn round), pad U back to the centre, pad D again (turn back to the original facing).
    /// Walking moves 29 units per tick, so "centred" means within 14.5 units on both axes; positions are never written.
    /// Ends as centred, as blocked (the game's collision stopped a move for 3 ticks), or after 10 s. BlobberMode cancels
    /// it when the player presses any movement key.
    /// </summary>
    internal static class CentreWalk
    {
        private const float Tolerance = 14.5f;
        private const int StallTicks = 3;
        private const long TimeoutMs = 10000;
        private const long TurnWaitMs = 1500;

        private enum Phase { None, Align, Side, Ahead, TurnOut, Back, TurnBack }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static Phase _phase = Phase.None;
        private static float _cx, _cz, _lastX, _lastZ;
        private static int _facing0, _turnTo, _stall;
        private static long _startMs, _phaseMs;
        private static bool _blocked;
        private static bool _alignPressed;

        internal static bool Active => _phase != Phase.None;

        internal static void Start(fldGlobalWork_t gb, float cell, float ox, float oz)
        {
            _cx = (float)Math.Round((gb.playerX - ox) / cell) * cell + ox;   // the area's square centres (WalkGrid.Lattice)
            _cz = (float)Math.Round((gb.playerZ - oz) / cell) * cell + oz;
            _facing0 = GridSense.Normalize90(gb.PlayerKakudoGoal);
            _lastX = gb.playerX;
            _lastZ = gb.playerZ;
            _stall = 0;
            _blocked = false;
            _startMs = _phaseMs = Clock.ElapsedMilliseconds;
            _alignPressed = false;
            _phase = OffSquare(gb) > 2 ? Phase.Align : Phase.Side;
            Offsets(gb, out float along, out float left);
            Log.Info($"[Nav:Blob] Centring from {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)} to {FieldData.F(_cx)} {FieldData.F(_cz)}, " +
                     $"facing {_facing0}: {FieldData.F(along)} ahead, {FieldData.F(left)} to the left{(_phase == Phase.Align ? $", facing {FieldData.F(gb.PlayerKakudo)} not square" : "")}.");
        }

        internal static void Cancel(string why)
        {
            if (!Active)
                return;
            PadInput.ReleaseAll();
            _phase = Phase.None;
            Log.Info($"[Nav:Blob] Centring stopped: {why}.");
        }

        /// <summary>
        /// One field update, before the game reads the pads. Returns null while running, else true when centred or
        /// false when it could not get there.
        /// </summary>
        internal static bool? Step(fldGlobalWork_t gb, bool gameMoved)
        {
            if (!Active)
                return null;
            PadInput.ReleaseAll();
            long now = Clock.ElapsedMilliseconds;
            float moved = Math.Abs(gb.playerX - _lastX) + Math.Abs(gb.playerZ - _lastZ);
            // A move can only be blocked when the game read the keys last tick (movement routine ran, no turn delay).
            bool counts = gameMoved && fldPlayer.gFldPlayerSyukanDly == 0;
            _lastX = gb.playerX;
            _lastZ = gb.playerZ;
            if (now - _startMs > TimeoutMs)
                return Finish(gb, "timed out");

            Offsets(gb, out float along, out float left);
            switch (_phase)
            {
                case Phase.Align:
                    // A diagonal facing (kept from third person): one tick of pad R, the game's own D turn, which snaps to 90.
                    if (!_alignPressed)
                    {
                        if (fldPlayer.gFldPlayerSyukanDly == 0)
                        {
                            PadInput.Hold(SDF_PADMAP.SDF_PADMAP_R, true);
                            _alignPressed = true;
                            _phaseMs = now;
                        }
                    }
                    else if (OffSquare(gb) <= 2 && Eased(gb) && fldPlayer.gFldPlayerSyukanDly == 0)
                    {
                        _facing0 = GridSense.Normalize90(gb.PlayerKakudoGoal);
                        Log.Info($"[Nav:Blob] Centring: facing squared to {_facing0}.");
                        Enter(Phase.Side);
                    }
                    else if (now - _phaseMs > TurnWaitMs)
                        return Finish(gb, "the facing could not be squared");
                    return null;

                case Phase.Side:
                    if (Math.Abs(left) <= Tolerance)
                        Enter(Phase.Ahead);
                    else if (Stalled(moved, counts))
                    {
                        _blocked = true;
                        Log.Info("[Nav:Blob] Centring: side step blocked.");
                        Enter(Phase.Ahead);
                    }
                    else
                        PadInput.Hold(left > 0 ? SDF_PADMAP.SDF_PADMAP_L1 : SDF_PADMAP.SDF_PADMAP_R1, true);
                    return null;

                case Phase.Ahead:
                    if (Math.Abs(along) <= Tolerance)
                        return Finish(gb, "done");
                    if (along > 0)
                    {
                        if (Stalled(moved, counts))
                        {
                            _blocked = true;
                            return Finish(gb, "blocked ahead");
                        }
                        PadInput.Hold(SDF_PADMAP.SDF_PADMAP_U, true);
                    }
                    else if (fldPlayer.gFldPlayerSyukanDly == 0)
                        TurnRound(Phase.TurnOut, (_facing0 + 180) % 360);
                    return null;

                case Phase.TurnOut:
                    if (TurnFinished(gb))
                        Enter(Phase.Back);
                    else if (now - _phaseMs > TurnWaitMs)
                        return Finish(gb, "the turn round did not happen");
                    return null;

                case Phase.Back:
                    // The facing is now reversed, so "along" counts towards the centre behind the original facing.
                    if (along <= Tolerance)
                        TurnRound(Phase.TurnBack, _facing0);
                    else if (Stalled(moved, counts))
                    {
                        _blocked = true;
                        Log.Info("[Nav:Blob] Centring: walk back blocked.");
                        TurnRound(Phase.TurnBack, _facing0);
                    }
                    else
                        PadInput.Hold(SDF_PADMAP.SDF_PADMAP_U, true);
                    return null;

                case Phase.TurnBack:
                    if (TurnFinished(gb))
                        return Finish(gb, "done");
                    if (now - _phaseMs > TurnWaitMs)
                        return Finish(gb, "the turn back did not happen");
                    return null;
            }
            return null;
        }

        private static void Offsets(fldGlobalWork_t gb, out float along, out float left)
        {
            int facing = GridSense.Normalize90(gb.PlayerKakudoGoal);
            GridSense.Direction(facing, out float dx, out float dz);
            GridSense.Direction(facing + 90, out float lx, out float lz);
            float ex = _cx - gb.playerX, ez = _cz - gb.playerZ;
            along = ex * dx + ez * dz;
            left = ex * lx + ez * lz;
        }

        private static bool Stalled(float moved, bool counts)
        {
            if (!counts)
                return false;
            if (moved >= 1f)
            {
                _stall = 0;
                return false;
            }
            return ++_stall > StallTicks;
        }

        private static void Enter(Phase phase)
        {
            _phase = phase;
            _phaseMs = Clock.ElapsedMilliseconds;
            _stall = 0;
        }

        /// <summary>Pad D for this one tick: the game's own S, a 180-degree turn (fldPlayerCalc_Nml_syukan).</summary>
        private static void TurnRound(Phase next, int facing)
        {
            _turnTo = facing;
            PadInput.Hold(SDF_PADMAP.SDF_PADMAP_D, true);
            Enter(next);
            Log.Info($"[Nav:Blob] Centring: turning round to {facing}.");
        }

        private static bool TurnFinished(fldGlobalWork_t gb)
        {
            if (GridSense.Normalize90(gb.PlayerKakudoGoal) != _turnTo || fldPlayer.gFldPlayerSyukanDly != 0)
                return false;
            return Eased(gb);
        }

        private static bool Eased(fldGlobalWork_t gb)
            => Math.Abs(Angles.Diff(gb.PlayerKakudo, gb.PlayerKakudoGoal)) <= 2;

        /// <summary>Degrees between the facing goal and the nearest multiple of 90.</summary>
        private static float OffSquare(fldGlobalWork_t gb)
            => Math.Abs(Angles.Diff(gb.PlayerKakudoGoal, GridSense.Normalize90(gb.PlayerKakudoGoal)));

        private static bool Finish(fldGlobalWork_t gb, string how)
        {
            PadInput.ReleaseAll();
            _phase = Phase.None;
            float ex = gb.playerX - _cx, ez = gb.playerZ - _cz;
            bool centred = Math.Abs(ex) <= Tolerance + 0.5f && Math.Abs(ez) <= Tolerance + 0.5f
                           && GridSense.Normalize90(gb.PlayerKakudoGoal) == _facing0;
            Log.Info($"[Nav:Blob] Centring {how}{(_blocked ? " (a move was blocked)" : "")}: now {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)}, " +
                     $"off by {FieldData.F(ex)} {FieldData.F(ez)}, facing {GridSense.Normalize90(gb.PlayerKakudoGoal)}, " +
                     $"{(Clock.ElapsedMilliseconds - _startMs)} ms: {(centred ? "centred" : "not centred")}.");
            return centred;
        }
    }
}
