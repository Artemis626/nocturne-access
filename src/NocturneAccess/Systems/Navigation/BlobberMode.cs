using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2Cpp;
using Il2Cppfield_H;
using Il2Cpplibsdf_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Menus;

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
    ///
    /// F3 (RequestCentre, Francesco 1 Oct 2026): CentreWalk walks to the centre of the current cell with the game's own
    /// moves (after collision slid the player off the grid), then speaks "Centred." with the facing and all three sides.
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
        private static float _ox, _oz;   // this area's square centres: ox + k * cell, oz + m * cell (WalkGrid.Lattice)
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
        private static int _snapDoor = -1, _snapVolume = -1;   // the game's hit indexes when the snap started

        // speech baseline
        private static bool _haveView;
        private static CellView _view;
        private static int _viewFacing = -1;
        private static int _facingSpoken = -1;
        private static long _cueCell = long.MinValue;
        private static int _area = int.MinValue, _field = int.MinValue;

        // F3 (centring), requested in OnUpdate and started at the next field update
        private const long CentreRequestMs = 500;
        private static long _centreRequestMs = long.MinValue / 2;
        private static int _lastSyukan;

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
            CentreWalk.Cancel("blobber mode toggled");
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

            // The world map has its own movement routine (fldPlayerCalc_Wm2): blobber mode stays out.
            if (FieldState.WorldMap(gb))
            {
                Leave("world map");
                RefuseCentre();
                return;
            }

            if (gb.fieldID != _field || gb.areaID != _area)
            {
                _field = gb.fieldID;
                _area = gb.areaID;
                StopSnap("area change");
                CentreWalk.Cancel("area change");
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
                RefuseCentre();
                return;
            }
            WalkGrid.Lattice(gb.fieldID, gb.areaID, gb.playerX, gb.playerY, gb.playerZ, _cell, out _ox, out _oz);
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
            // Did the game's first-person movement routine run last tick? Not during dialogue and events.
            bool gameMoved = FieldState.SyukanCount != _lastSyukan;
            _lastSyukan = FieldState.SyukanCount;
            ReadKeys();

            if (TakeCentreRequest())
            {
                if (!gameMoved)
                    Log.Info("[Nav:Blob] Centring ignored: the game is not taking movement (dialogue or event).");
                else
                {
                    StopSnap("centring");
                    CentreWalk.Start(gb, _cell, _ox, _oz);
                }
            }
            if (CentreWalk.Active && (_realU || _otherKey.Length > 0))
                CentreWalk.Cancel(_realU ? "W pressed" : $"pad {_otherKey} pressed");

            if (CentreWalk.Active)
            {
                bool? centred = CentreWalk.Step(gb, gameMoved);
                if (centred.HasValue)
                    SpeakCentred(gb, centred.Value);
                else
                    _facingSpoken = GridSense.Normalize90(gb.PlayerKakudoGoal);   // its own turns are not announced
            }
            else if (AutoWalk.Active)
            {
                // The walk makes its own turns and speaks its own result: no snap, no cues, a fresh baseline afterwards.
                StopSnap("auto-walk");
                _facingSpoken = GridSense.Normalize90(gb.PlayerKakudoGoal);
                _cueCell = CellKey(x, z);
                _haveView = false;
            }
            else
            {
                if (_snapping)
                    ContinueSnap(gb);
                else if (_prevRealU && !_realU && _otherKey.Length == 0)
                    TryStartSnap(gb, moveX, moveZ);

                if (gameMoved)
                {
                    AnnounceTurn(gb);
                    CueAtCentre(gb, moveX, moveZ);
                }
                else
                {
                    // The game moves or turns the player itself (a cutscene, a speaker turning to face): no cues, a fresh
                    // baseline. Before Forneus (16:11 session, 2 Oct 2026) the scripted walk across the Annex Lobby (syukan 0
                    // a second, no keys, no pads) spoke "East, open ahead", "South, open ahead", "Opening left" and sounded
                    // like the mod walking. The facing spoken is kept: once the player has control again, AnnounceTurn
                    // speaks the facing only if the game turned them (and on a new area, where it is -1).
                    _cueCell = CellKey(x, z);
                    _haveView = false;
                }
            }

            _prevRealU = _realU;
            _prevX = x;
            _prevZ = z;
            _havePrev = true;
        }

        private static void Leave(string why)
        {
            if (!_firstPerson)
                return;
            CentreWalk.Cancel(why);
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
            float oa = _ox * dx + _oz * dz;   // the lattice offset along the facing (dx, dz are 0 or 1 in size)
            float nearest = (float)Math.Round((along - oa) / _cell) * _cell + oa;
            if (Math.Abs(along - nearest) <= HalfTick)
            {
                Log.Info($"[Nav:Blob] W released at a cell centre ({FieldData.F(x)} {FieldData.F(z)}): no snap.");
                return;
            }

            _dx = dx;
            _dz = dz;
            _target = (float)Math.Ceiling((along - oa) / _cell) * _cell + oa - along;
            _startX = x;
            _startZ = z;
            _snapFacing = facing;
            _lastAlong = 0;
            _stall = 0;
            _ticks = 0;
            _snapStartMs = Clock.ElapsedMilliseconds;
            _snapDoor = fldEveHit.OldHitDoor;
            _snapVolume = fldEveHit.gFldEveRngIdx;
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
            // The game took a door or a story volume into account: no further push into it (0.0.59). On 6 Oct 2026, 14:01:45, a
            // snap carried the player 214 units on, 40 past the underpass Terminal door's centre, and the game then ignored every
            // key until Alt+F4; whether the push caused it is not known, but the door's own prompt needs no more walking.
            int door = fldEveHit.OldHitDoor, volume = fldEveHit.gFldEveRngIdx;
            if ((door >= 0 && door != _snapDoor) || (volume >= 0 && volume != _snapVolume && IsStoryVolume(volume)))
            {
                StopSnap($"the game's hit is volume {(door >= 0 ? door : volume)}");
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

        /// <summary>A story volume ("01eve_01"), not a person's or a box's talk range (those fire well before reaching them).</summary>
        private static bool IsStoryVolume(int index)
        {
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2Cppfield_H.fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (ranges == null || index >= Math.Min(fldEveHit.gEvnRngNum, ranges.Length))
                return false;
            string name = ranges[index]?.resourceName ?? "";
            return FieldData.EventVolume.IsMatch(name);
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
            if (CentreWalk.Active && !FieldState.IsExploring)
                CentreWalk.Cancel("no longer exploring");
        }

        /// <summary>After the game's own turn (A, D, S) has finished easing: the facing and what is ahead.</summary>
        private static void AnnounceTurn(fldGlobalWork_t gb)
        {
            int goal = GridSense.Normalize90(gb.PlayerKakudoGoal);
            if (goal == _facingSpoken)
                return;
            float diff = Math.Abs(Angles.Diff(gb.PlayerKakudo, gb.PlayerKakudoGoal));
            if (diff > 2)
                return;
            _facingSpoken = goal;
            CellView v = WithGameDoor(GridSense.Look(gb.playerX, gb.playerY, gb.playerZ, goal, _cell), gb.playerX, gb.playerZ, goal);
            _view = v;
            _viewFacing = goal;
            _haveView = true;
            Say($"{TextUtil.Capitalize(GridSense.FacingName(goal))}, {Describe(v.Ahead, "ahead")}.");
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
            float o = alongX ? _ox : _oz;
            float centre = (float)Math.Round((c - o) / _cell) * _cell + o;
            float a = c0 - centre, b = c - centre;
            bool atCentre = Math.Abs(b) <= CentreTolerance || (a < 0 && b > 0) || (a > 0 && b < 0);
            if (!atCentre)
                return;

            long cell = CellKey(x, z);
            if (cell == _cueCell)
                return;
            _cueCell = cell;

            int facing = GridSense.Normalize90(gb.PlayerKakudoGoal);
            CellView v = WithGameDoor(GridSense.Look(x, gb.playerY, z, facing, _cell), x, z, facing);
            string change = Changes(v, facing);
            _view = v;
            _viewFacing = facing;
            _haveView = true;
            Log.Info($"[Nav:Blob] Centre {FieldData.F(x)} {FieldData.F(z)}: ahead {v.Ahead}, left {v.Left}, right {v.Right}.");
            if (change.Length > 0)
                Say(change);
        }

        /// <summary>F3 (OnUpdate): walk to the centre of the current cell at the next field update (CentreWalk).</summary>
        internal static void RequestCentre()
        {
            if (!_enabled)
            {
                SpeechManager.Say("Blobber mode is off.", interrupt: true);
                return;
            }
            _centreRequestMs = Clock.ElapsedMilliseconds;
        }

        private static bool TakeCentreRequest()
        {
            bool fresh = Clock.ElapsedMilliseconds - _centreRequestMs <= CentreRequestMs;
            _centreRequestMs = long.MinValue / 2;
            return fresh;
        }

        private static void RefuseCentre()
        {
            if (TakeCentreRequest())
                Say("Centring works in first person only.");
        }

        /// <summary>End of a centring walk: the result and the full view, which becomes the baseline for later changes.</summary>
        private static void SpeakCentred(fldGlobalWork_t gb, bool centred)
        {
            int facing = GridSense.Normalize90(gb.PlayerKakudoGoal);
            CellView v = WithGameDoor(GridSense.Look(gb.playerX, gb.playerY, gb.playerZ, facing, _cell), gb.playerX, gb.playerZ, facing);
            _view = v;
            _viewFacing = facing;
            _haveView = true;
            _facingSpoken = facing;
            _cueCell = CellKey(gb.playerX, gb.playerZ);
            string view = $"{TextUtil.Capitalize(GridSense.FacingName(facing))}: {Describe(v.Ahead, "ahead")}, {Describe(v.Left, "left")}, {Describe(v.Right, "right")}.";
            Say((centred ? "Centred. " : "Blocked, not centred. ") + view);
        }

        private static long CellKey(float x, float z)
            => ((long)Math.Round((x - _ox) / _cell) << 32) ^ (uint)(int)Math.Round((z - _oz) / _cell);

        private static string Changes(CellView v, int facing)
        {
            if (!_haveView || _viewFacing != facing)
                return TextUtil.Capitalize(Describe(v.Ahead, "ahead")) + ".";
            var parts = new List<string>();
            if (v.Ahead != _view.Ahead) parts.Add(Describe(v.Ahead, "ahead"));
            if (v.Left != _view.Left) parts.Add(Describe(v.Left, "left"));
            if (v.Right != _view.Right) parts.Add(Describe(v.Right, "right"));
            return parts.Count == 0 ? "" : TextUtil.Capitalize(string.Join(", ", parts)) + ".";
        }

        /// <summary>
        /// The game's door prompt wins over the walking map (0.0.167): while "Open Door" (or "Enter", "Exit") is on screen and the
        /// door volume the game is touching (fldEveHit.OldHitDoor) lies in front of the facing, a wall ahead is a door ahead. On
        /// 13 Nov 2026 21:41:37 (Fourth Kalpa 1F, door 01d_01 in a north wall) the turn cue said "North, wall ahead." with the
        /// prompt up, and that was the door.
        /// </summary>
        private static CellView WithGameDoor(CellView v, float px, float pz, int facing)
        {
            try
            {
                if (v.Ahead == SideKind.Door || !AnnouncementReader.DoorPromptShown)
                    return v;
                int door = fldEveHit.OldHitDoor;
                if (door < 0 || !ExploredDoors.DoorCentre(door, out float cx, out float cz, out float nx, out float nz))
                    return v;
                GridSense.Direction(facing, out float dx, out float dz);
                bool inFront;
                if (nx != 0f || nz != 0f)
                    inFront = -(dx * nx + dz * nz) > DoorFacingCos;   // the volume faces out to the player's side
                else
                {
                    float ox = cx - px, oz = cz - pz;
                    float len = (float)Math.Sqrt(ox * ox + oz * oz);
                    inFront = len < 1f || (dx * ox + dz * oz) / len > DoorFacingCos;
                }
                if (inFront)
                {
                    Log.Info($"[Nav:Blob] The game's door prompt is up for volume {door} ahead: door, not {v.Ahead}.");
                    v.Ahead = SideKind.Door;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav:Blob] Door prompt check: {ex.Message}");
            }
            return v;
        }

        private const float DoorFacingCos = 0.7f;   // within about 45 degrees

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

    }
}
