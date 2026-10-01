using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2Cpp;
using Il2Cppfield_H;
using Il2Cpplibsdf_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Blobber mode, version 2 (docs/NAVIGATION-DESIGN.md sections 11 and 12; Francesco 1 Oct 2026: toggle, on by
    /// default; it never changes the game's input, only movement is snapped to the grid; cues never block; fixed-camera
    /// rooms keep the game's own controls; speak only changes).
    ///
    /// Only in first person (fldGb.cammode 0 and FirstPersonView on), where the game itself turns in exact 90-degree
    /// steps (A / D), turns round (S) and walks straight along the facing (W) or sideways (Q / E). Every key keeps the
    /// game's own meaning. The one change: when the player lets go of W after walking straight ahead, the mod keeps
    /// pad U pressed for the game until the next cell centre (a multiple of CellSize along the facing), so a walk
    /// always ends on a cell centre; a tap of W walks one cell. The snap ends within half a tick of the centre, when
    /// the game's collision stops the player (3 ticks without moving), after 3 s, on a view or area change, when
    /// exploring stops, or at once when the player presses W again or any other movement key (read before the game
    /// reads it, so A / D still turn: with pad U held the game would walk diagonally instead, section 12).
    ///
    /// Speech (only changes): when the player reaches a cell centre while moving, what changed ahead, left and right
    /// (wall, opening, door); after the game's own turn, the facing and what is ahead.
    /// </summary>
    internal static class BlobberMode
    {
        private const float HalfTick = 14.5f;     // walking moves 29 units per tick
        private const float CentreTolerance = 15f;
        private const int StallTicks = 3;
        private const long SnapTimeoutMs = 3000;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Queue<string> Pending = new Queue<string>();
        private static float _cell = 600f;
        private static bool _enabled;
        private static bool _firstPerson;

        // the player's keys, as the game will read them this tick
        private static bool _realU, _prevRealU;
        private static string _otherKey = "";

        // movement since the previous field update
        private static bool _havePrev;
        private static float _prevX, _prevZ;

        // snap
        private static bool _snapping;
        private static int _snapFacing;
        private static float _dx, _dz, _startX, _startZ, _target, _lastAlong;
        private static int _stall, _ticks;
        private static long _snapStartMs;

        // speech baseline
        private static bool _haveView;
        private static CellView _view;
        private static int _viewFacing = -1;
        private static int _facingSpoken = -1;
        private static long _cueCell = long.MinValue;
        private static int _area = int.MinValue, _field = int.MinValue;

        internal static bool Enabled => _enabled;

        internal static void Init()
        {
            _cell = Math.Max(100, Config.BlobberCellSize.Value);
            _enabled = Config.BlobberOnAtStart.Value && PadInput.Installed;
            Log.Info($"[Nav] Blobber mode {(_enabled ? "on" : "off")} at start (cell {_cell} units). F4 toggles it.");
        }

        internal static void Toggle()
        {
            if (!PadInput.Installed)
            {
                SpeechManager.Say("Blobber mode unavailable: pad injection is off.", interrupt: true, force: true);
                return;
            }
            _enabled = !_enabled;
            FirstPersonView.ClearUserChoice();
            StopSnap("toggled");
            _firstPerson = false;
            _haveView = false;
            Log.Info($"[Nav] Blobber mode {(_enabled ? "on" : "off")}.");
            SpeechManager.Say(_enabled ? "Blobber mode on" : "Blobber mode off", interrupt: true, force: true);
        }

        /// <summary>OnUpdate: speech queued during the field update.</summary>
        internal static void Tick()
        {
            while (Pending.Count > 0)
                SpeechManager.Say(Pending.Dequeue(), interrupt: true);
        }

        /// <summary>Called by FieldState at the start of every field update (inside fldPlayerCalc, before the game reads pads).</summary>
        internal static void BeforeCalc()
        {
            if (!_enabled)
                return;

            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;

            // The world map (fieldID below 10) has its own movement routine (fldPlayerCalc_Wm2): blobber mode stays out.
            if (gb.fieldID < 10)
            {
                Leave("world map");
                return;
            }

            if (gb.fieldID != _field || gb.areaID != _area)
            {
                _field = gb.fieldID;
                _area = gb.areaID;
                StopSnap("area change");
                _haveView = false;
                _havePrev = false;
                _facingSpoken = -1;
                _cueCell = long.MinValue;
            }
            CollisionCache.Refresh(gb.fieldID, gb.areaID);

            // First person is the default view in blobber mode wherever the game allows it.
            if (!FirstPersonView.On && FirstPersonView.Available && gb.cammode == 0 && !FirstPersonView.UserTurnedOff)
                FirstPersonView.SetOn(true, speak: true);

            // Fixed-camera rooms (cammode 1 or 3) and third person: the game's own controls, nothing from blobber mode.
            if (gb.cammode != 0 || !FirstPersonView.On)
            {
                Leave(gb.cammode != 0 ? $"cammode {gb.cammode}" : "third person");
                return;
            }
            if (!_firstPerson)
            {
                _firstPerson = true;
                _facingSpoken = GridSense.Normalize90(gb.PlayerKakudoGoal);
                _haveView = false;
                _havePrev = false;
                _prevRealU = false;
                Log.Info($"[Nav:Blob] First person: grid snap on, facing {_facingSpoken}.");
            }

            float x = gb.playerX, z = gb.playerZ;
            float moveX = _havePrev ? x - _prevX : 0, moveZ = _havePrev ? z - _prevZ : 0;
            ReadKeys();

            if (_snapping)
                ContinueSnap(gb);
            else if (_prevRealU && !_realU && _otherKey.Length == 0)
                TryStartSnap(gb, moveX, moveZ);

            AnnounceTurn(gb);
            CueAtCentre(gb, moveX, moveZ);

            _prevRealU = _realU;
            _prevX = x;
            _prevZ = z;
            _havePrev = true;
        }

        private static void Leave(string why)
        {
            if (!_firstPerson)
                return;
            StopSnap(why);
            _firstPerson = false;
            _haveView = false;
            Log.Info($"[Nav:Blob] Grid snap off: {why}.");
        }

        /// <summary>The keys fldPlayerCalc_Nml_syukan reads, asked the same way (section 12).</summary>
        private static void ReadKeys()
        {
            _realU = PadInput.Real(SDF_PADMAP.SDF_PADMAP_U, 0);
            _otherKey =
                PadInput.Real(SDF_PADMAP.SDF_PADMAP_L, 0) ? "L" :
                PadInput.Real(SDF_PADMAP.SDF_PADMAP_R, 0) ? "R" :
                PadInput.Real(SDF_PADMAP.SDF_PADMAP_D, 0) ? "D" :
                PadInput.Real(SDF_PADMAP.SDF_PADMAP_L1, 1) ? "L1" :
                PadInput.Real(SDF_PADMAP.SDF_PADMAP_R1, 1) ? "R1" :
                PadInput.Real(SDF_PADMAP.SDF_PADMAP_RD, 1) ? "RD" : "";
        }

        /// <summary>W was let go this tick. Snap only after a straight walk along the facing (not diagonal, not against a wall).</summary>
        private static void TryStartSnap(fldGlobalWork_t gb, float moveX, float moveZ)
        {
            int facing = GridSense.Normalize90(gb.PlayerKakudoGoal);
            GridSense.Direction(facing, out float dx, out float dz);
            float alongMove = moveX * dx + moveZ * dz;
            float crossMove = Math.Abs(moveX * dz - moveZ * dx);
            if (alongMove < 1f || crossMove > alongMove * 0.3f)
            {
                Log.Info($"[Nav:Blob] W released, no snap: last move {FieldData.F(alongMove)} ahead, {FieldData.F(crossMove)} across.");
                return;
            }

            float x = gb.playerX, z = gb.playerZ;
            float along = x * dx + z * dz;
            float nearest = (float)Math.Round(along / _cell) * _cell;
            if (Math.Abs(along - nearest) <= HalfTick)
            {
                Log.Info($"[Nav:Blob] W released at a cell centre ({FieldData.F(x)} {FieldData.F(z)}): no snap.");
                return;
            }

            _dx = dx;
            _dz = dz;
            _target = (float)Math.Ceiling(along / _cell) * _cell - along;
            _startX = x;
            _startZ = z;
            _snapFacing = facing;
            _lastAlong = 0;
            _stall = 0;
            _ticks = 0;
            _snapStartMs = Clock.ElapsedMilliseconds;
            _snapping = true;
            PadInput.Hold(SDF_PADMAP.SDF_PADMAP_U, true);
            Log.Info($"[Nav:Blob] Snap: W released at {FieldData.F(x)} {FieldData.F(z)}, facing {facing}, {FieldData.F(_target)} to the next centre.");
        }

        private static void ContinueSnap(fldGlobalWork_t gb)
        {
            if (_realU)
            {
                StopSnap("W pressed again");
                return;
            }
            if (_otherKey.Length > 0)
            {
                StopSnap($"pad {_otherKey} pressed");
                return;
            }
            if (GridSense.Normalize90(gb.PlayerKakudoGoal) != _snapFacing)
            {
                StopSnap("facing changed");
                return;
            }

            float along = (gb.playerX - _startX) * _dx + (gb.playerZ - _startZ) * _dz;
            _ticks++;
            if (_target - along <= HalfTick)
            {
                EndSnap(gb, along, "done");
                return;
            }
            if (along - _lastAlong < 1f)
            {
                if (++_stall >= StallTicks)
                {
                    EndSnap(gb, along, "stopped by the game");
                    return;
                }
            }
            else
                _stall = 0;
            _lastAlong = along;

            if (Clock.ElapsedMilliseconds - _snapStartMs > SnapTimeoutMs)
                EndSnap(gb, along, "timed out");
        }

        private static void EndSnap(fldGlobalWork_t gb, float along, string how)
        {
            PadInput.ReleaseAll();
            _snapping = false;
            float cross = (gb.playerX - _startX) * _dz - (gb.playerZ - _startZ) * _dx;
            Log.Info($"[Nav:Blob] Snap {how}: moved {FieldData.F(along)} of {FieldData.F(_target)}, drift {FieldData.F(cross)}, " +
                     $"now {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)}, {_ticks} ticks.");
        }

        private static void StopSnap(string why)
        {
            if (!_snapping)
                return;
            PadInput.ReleaseAll();
            _snapping = false;
            Log.Info($"[Nav:Blob] Snap stopped: {why}.");
        }

        /// <summary>OnUpdate: a snap stops when the field update stops (battle, event, menu).</summary>
        internal static void CheckExploring()
        {
            if (_snapping && !FieldState.IsExploring)
                StopSnap("no longer exploring");
        }

        /// <summary>After the game's own turn (A, D, S) has finished easing: the facing and what is ahead.</summary>
        private static void AnnounceTurn(fldGlobalWork_t gb)
        {
            int goal = GridSense.Normalize90(gb.PlayerKakudoGoal);
            if (goal == _facingSpoken)
                return;
            float diff = Math.Abs(((gb.PlayerKakudo - gb.PlayerKakudoGoal) % 360 + 540) % 360 - 180);
            if (diff > 2)
                return;
            _facingSpoken = goal;
            CellView v = GridSense.Look(gb.playerX, gb.playerY, gb.playerZ, goal, _cell);
            _view = v;
            _viewFacing = goal;
            _haveView = true;
            Say($"{Cap(GridSense.FacingName(goal))}, {Describe(v.Ahead, "ahead")}.");
        }

        /// <summary>
        /// While moving (walking, snapping or side-stepping), on reaching a cell centre along the direction of motion,
        /// speak what changed ahead, left and right. Each cell speaks once until the player reaches another.
        /// </summary>
        private static void CueAtCentre(fldGlobalWork_t gb, float moveX, float moveZ)
        {
            if (Math.Abs(moveX) < 1f && Math.Abs(moveZ) < 1f)
                return;
            float x = gb.playerX, z = gb.playerZ;
            bool alongX = Math.Abs(moveX) >= Math.Abs(moveZ);
            float c = alongX ? x : z;
            float c0 = c - (alongX ? moveX : moveZ);
            float centre = (float)Math.Round(c / _cell) * _cell;
            float a = c0 - centre, b = c - centre;
            bool atCentre = Math.Abs(b) <= CentreTolerance || (a < 0 && b > 0) || (a > 0 && b < 0);
            if (!atCentre)
                return;

            long cell = ((long)Math.Round(x / _cell) << 32) ^ (uint)(int)Math.Round(z / _cell);
            if (cell == _cueCell)
                return;
            _cueCell = cell;

            int facing = GridSense.Normalize90(gb.PlayerKakudoGoal);
            CellView v = GridSense.Look(x, gb.playerY, z, facing, _cell);
            string change = Changes(v, facing);
            _view = v;
            _viewFacing = facing;
            _haveView = true;
            Log.Info($"[Nav:Blob] Centre {FieldData.F(x)} {FieldData.F(z)}: ahead {v.Ahead}, left {v.Left}, right {v.Right}.");
            if (change.Length > 0)
                Say(change);
        }

        private static string Changes(CellView v, int facing)
        {
            if (!_haveView || _viewFacing != facing)
                return Cap(Describe(v.Ahead, "ahead")) + ".";
            var parts = new List<string>();
            if (v.Ahead != _view.Ahead) parts.Add(Describe(v.Ahead, "ahead"));
            if (v.Left != _view.Left) parts.Add(Describe(v.Left, "left"));
            if (v.Right != _view.Right) parts.Add(Describe(v.Right, "right"));
            return parts.Count == 0 ? "" : Cap(string.Join(", ", parts)) + ".";
        }

        private static string Describe(SideKind k, string where)
        {
            switch (k)
            {
                case SideKind.Wall: return "wall " + where;
                case SideKind.Door: return "door " + where;
                default: return where == "ahead" ? "open ahead" : "opening " + where;
            }
        }

        private static void Say(string text)
        {
            Log.Info("[Nav:Blob] Say: " + text);
            Pending.Enqueue(text);
        }

        private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
