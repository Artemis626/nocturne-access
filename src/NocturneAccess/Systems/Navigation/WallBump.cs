using System;
using System.Diagnostics;
using Il2Cpp;
using Il2Cppfield_H;
using Il2Cpplibsdf_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// A thud when the player walks into a wall (0.0.83; Francesco, 15 Oct 2026: unlike Strange Journey, this game has no such
    /// sound). While a movement key is held (in first person W only: A, D and S turn) and the player has not moved for
    /// StillMs while not turning, the game's collision has stopped them: one thud (Earcons.Thud). It sounds again only after
    /// the player has moved or let go. Only the player's own keys count (PadInput.Real), never the mod's walks.
    /// </summary>
    internal static class WallBump
    {
        private const long StillMs = 150;
        private const float MoveEpsilon = 0.5f;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static bool _armed = true, _hasLast;
        private static float _lastX, _lastZ;
        private static long _stillSince = -1;

        internal static void Tick()
        {
            if (!(Config.WallBumpSound?.Value ?? true))
                return;
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || !FieldState.IsExploring)
            {
                Reset();
                return;
            }
            bool firstPerson = gb.cammode == 0 && FirstPersonView.On;
            bool pressing = PadInput.Real(SDF_PADMAP.SDF_PADMAP_U, 0)
                            || (!firstPerson && (PadInput.Real(SDF_PADMAP.SDF_PADMAP_D, 0) || PadInput.Real(SDF_PADMAP.SDF_PADMAP_L, 0)
                                                 || PadInput.Real(SDF_PADMAP.SDF_PADMAP_R, 0)));
            float x = gb.playerX, z = gb.playerZ;
            bool moved = _hasLast && ((x - _lastX) * (x - _lastX) + (z - _lastZ) * (z - _lastZ) > MoveEpsilon * MoveEpsilon);
            _lastX = x;
            _lastZ = z;
            _hasLast = true;
            if (!pressing || moved)
            {
                _armed = true;
                _stillSince = -1;
                return;
            }
            if (firstPerson && Math.Abs(Angles.Diff(gb.PlayerKakudo, gb.PlayerKakudoGoal)) > 2f)
                return;   // turning: standing still is the turn, not a wall
            long now = Clock.ElapsedMilliseconds;
            if (_stillSince < 0)
            {
                _stillSince = now;
                return;
            }
            if (!_armed || now - _stillSince < StillMs)
                return;
            _armed = false;
            Earcons.Thud();
            Log.Debug($"[Nav:Bump] Wall at {FieldData.F(x)} {FieldData.F(z)}.");
        }

        private static void Reset()
        {
            _armed = true;
            _hasLast = false;
            _stillSince = -1;
        }
    }
}
