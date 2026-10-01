using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2Cpp;
using Il2Cppfield_H;
using Il2Cpplibsdf_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;
// Inside NocturneAccess, a bare "Input" means the NocturneAccess.Input namespace.
using UnityInput = UnityEngine.Input;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Blobber mode (docs/NAVIGATION-DESIGN.md sections 3.2, 9 and 10; Francesco 1 Oct 2026: toggle, on by default,
    /// speak only changes, S left native for now, fixed-camera rooms handled by the mod's own facing).
    ///
    /// Two layers, chosen every field update:
    /// - First person (cammode 0 and FirstPersonView on): the game already turns in exact 90-degree steps and walks
    ///   straight along the facing. The mod only makes W (pad U) and A / D (pad L / R, side steps) move exactly one
    ///   cell: the real press is hidden from the game and pad U / L / R is held until the next cell centre.
    /// - Mod facing (fixed-camera rooms, cammode 1 or 3, where the game forbids first person; or first person off):
    ///   the mod keeps a facing (north, east, south, west). Q / E turn it (instant, spoken). W / A / D press the pad
    ///   direction (or diagonal pair) whose camera-relative bearing rounds to the wanted world axis: third-person
    ///   walking is camera relative (U = camera yaw, R = +90, D = +180, L = +270) and the game snaps the result to
    ///   world multiples of 45 degrees (0.0.15, Healing Room).
    /// A step targets the next multiple of CellSize along its axis (at least 100 units ahead). It ends within half a
    /// tick of the target (29 units per tick), when blocked for 3 ticks (spoken "Wall" unless a door is there: the
    /// game's own "Open Door" prompt follows), after 3 s, or when exploring stops. Holding the key keeps stepping.
    /// Speech after each step: only what changed ahead, left and right (wall, opening, door). After a turn: the facing
    /// and what is ahead.
    /// </summary>
    internal static class BlobberMode
    {
        private const float HalfTick = 14.5f;
        private const float MinStep = 100f;
        private const int StallTicks = 3;
        private const long StepTimeoutMs = 3000;

        private enum Layer { None, FirstPerson, ModFacing }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Queue<string> Pending = new Queue<string>();
        private static float _cell = 600f;
        private static bool _enabled;
        private static Layer _layer = Layer.None;
        private static int _modFacing;
        private static int _turnRequest;   // -1 left, +1 right, from Q / E in OnUpdate

        // step
        private static bool _stepping;
        private static string _stepName = "";
        private static float _dx, _dz, _startX, _startZ, _target, _lastAlong;
        private static int _stall, _ticks;
        private static long _stepStartMs;
        private static SDF_PADMAP[] _pads = Array.Empty<SDF_PADMAP>();
        private static int _wantFacing;

        // speech baseline
        private static bool _haveView;
        private static CellView _view;
        private static int _viewFacing = -1;
        private static int _fpFacingSpoken = -1;
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
            StopStep("toggled");
            PadInput.SuppressNone();
            _layer = Layer.None;
            _haveView = false;
            Log.Info($"[Nav] Blobber mode {(_enabled ? "on" : "off")}.");
            SpeechManager.Say(_enabled ? "Blobber mode on" : "Blobber mode off", interrupt: true, force: true);
        }

        /// <summary>OnUpdate: Q / E turn requests in the mod-facing layer, and queued speech.</summary>
        internal static void Tick()
        {
            if (_enabled && _layer == Layer.ModFacing && FieldState.IsExploring)
            {
                if (UnityInput.GetKeyDown(KeyCode.Q))
                    _turnRequest = -1;
                else if (UnityInput.GetKeyDown(KeyCode.E))
                    _turnRequest = 1;
            }

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
                if (_layer != Layer.None)
                {
                    StopStep("world map");
                    PadInput.SuppressNone();
                    _layer = Layer.None;
                }
                return;
            }

            if (gb.fieldID != _field || gb.areaID != _area)
            {
                _field = gb.fieldID;
                _area = gb.areaID;
                StopStep("area change");
                _haveView = false;
                _fpFacingSpoken = -1;
            }
            CollisionCache.Refresh(gb.fieldID, gb.areaID);

            // First person is the default view in blobber mode wherever the game allows it.
            if (!FirstPersonView.On && FirstPersonView.Available && gb.cammode == 0 && !FirstPersonView.UserTurnedOff)
                FirstPersonView.SetOn(true, speak: true);

            Layer layer = gb.cammode == 0 && FirstPersonView.On ? Layer.FirstPerson : Layer.ModFacing;
            if (layer != _layer)
                EnterLayer(layer, gb);

            if (_stepping)
            {
                ContinueStep(gb);
                return;
            }

            if (_layer == Layer.FirstPerson)
                AnnounceFirstPersonTurn(gb);
            else if (_turnRequest != 0)
            {
                _modFacing = ((_modFacing + (_turnRequest > 0 ? -90 : 90)) % 360 + 360) % 360;
                _turnRequest = 0;
                AnnounceFacing(gb, _modFacing);
            }

            int facing = _layer == Layer.FirstPerson ? GridSense.Normalize90(gb.PlayerKakudoGoal) : _modFacing;
            if (PadInput.IsDown(SDF_PADMAP.SDF_PADMAP_U))
                StartStep(gb, facing, "forward", SDF_PADMAP.SDF_PADMAP_U, PadInput.WasPressed(SDF_PADMAP.SDF_PADMAP_U));
            else if (PadInput.IsDown(SDF_PADMAP.SDF_PADMAP_L))
                StartStep(gb, facing + 90, "left", SDF_PADMAP.SDF_PADMAP_L, PadInput.WasPressed(SDF_PADMAP.SDF_PADMAP_L));
            else if (PadInput.IsDown(SDF_PADMAP.SDF_PADMAP_R))
                StartStep(gb, facing - 90, "right", SDF_PADMAP.SDF_PADMAP_R, PadInput.WasPressed(SDF_PADMAP.SDF_PADMAP_R));
        }

        private static void EnterLayer(Layer layer, fldGlobalWork_t gb)
        {
            StopStep("view change");
            _layer = layer;
            PadInput.SuppressNone();
            PadInput.Suppress(SDF_PADMAP.SDF_PADMAP_U, true);
            PadInput.Suppress(SDF_PADMAP.SDF_PADMAP_L, true);
            PadInput.Suppress(SDF_PADMAP.SDF_PADMAP_R, true);
            _haveView = false;
            if (layer == Layer.ModFacing)
            {
                _modFacing = GridSense.Normalize90(gb.PlayerKakudo);
                Log.Info($"[Nav:Blob] Mod-facing layer (cammode {gb.cammode}, first person {(FirstPersonView.On ? "on" : "off")}), facing {_modFacing}.");
                AnnounceFacing(gb, _modFacing);
            }
            else
            {
                _fpFacingSpoken = GridSense.Normalize90(gb.PlayerKakudoGoal);
                Log.Info($"[Nav:Blob] First-person layer, facing {_fpFacingSpoken}.");
            }
        }

        private static void AnnounceFirstPersonTurn(fldGlobalWork_t gb)
        {
            int goal = GridSense.Normalize90(gb.PlayerKakudoGoal);
            if (goal == _fpFacingSpoken)
                return;
            // Speak once the game's own turn has finished easing.
            float diff = Math.Abs(((gb.PlayerKakudo - gb.PlayerKakudoGoal) % 360 + 540) % 360 - 180);
            if (diff > 2)
                return;
            _fpFacingSpoken = goal;
            AnnounceFacing(gb, goal);
        }

        private static void AnnounceFacing(fldGlobalWork_t gb, int facing)
        {
            CellView v = GridSense.Look(gb.playerX, gb.playerY, gb.playerZ, facing, _cell);
            _view = v;
            _viewFacing = facing;
            _haveView = true;
            string ahead = v.Ahead == SideKind.Wall ? "wall ahead" : v.Ahead == SideKind.Door ? "door ahead" : "open ahead";
            Say($"{Cap(GridSense.FacingName(facing))}, {ahead}.");
        }

        private static void StartStep(fldGlobalWork_t gb, int kakudo, string name, SDF_PADMAP firstPersonPad, bool newPress)
        {
            kakudo = ((kakudo % 360) + 360) % 360;
            float x = gb.playerX, z = gb.playerZ;
            SideKind side = GridSense.Side(x, gb.playerY, z, kakudo, _cell);
            if (side == SideKind.Wall)
            {
                // Bump only once per key press, not on every tick while held.
                if (newPress)
                {
                    Log.Info($"[Nav:Blob] Step {name} refused: wall (pos {FieldData.F(x)} {FieldData.F(z)}, facing {kakudo}).");
                    Say("Wall.");
                }
                return;
            }

            GridSense.Direction(kakudo, out _dx, out _dz);
            float along0 = x * _dx + z * _dz;
            float next = (float)Math.Ceiling((along0 + MinStep) / _cell) * _cell;
            _target = next - along0;
            _startX = x;
            _startZ = z;
            _lastAlong = 0;
            _stall = 0;
            _ticks = 0;
            _stepStartMs = Clock.ElapsedMilliseconds;
            _stepName = name;
            _wantFacing = kakudo;
            _pads = _layer == Layer.FirstPerson ? new[] { firstPersonPad } : PadsFor(kakudo);
            foreach (SDF_PADMAP p in _pads)
                PadInput.Hold(p, true);
            _stepping = true;
            Log.Info($"[Nav:Blob] Step {name} ({_layer}): from {FieldData.F(x)} {FieldData.F(z)}, facing {kakudo}, distance {FieldData.F(_target)}, " +
                     $"pads {string.Join("+", Array.ConvertAll(_pads, p => p.ToString().Replace("SDF_PADMAP_", "")))}.");
        }

        private static void ContinueStep(fldGlobalWork_t gb)
        {
            float along = (gb.playerX - _startX) * _dx + (gb.playerZ - _startZ) * _dz;
            _ticks++;
            if (_ticks == 3 && _layer == Layer.ModFacing)
            {
                int goal = GridSense.Normalize90(gb.PlayerKakudoGoal);
                if (goal != _wantFacing)
                    Log.Warn($"[Nav:Blob] Mod-facing step: the game's facing goal is {FieldData.F(gb.PlayerKakudoGoal)}, wanted {_wantFacing}.");
            }

            if (_target - along <= HalfTick)
            {
                FinishStep(gb, along, blocked: false);
                return;
            }
            if (along - _lastAlong < 1f)
            {
                if (++_stall >= StallTicks)
                {
                    FinishStep(gb, along, blocked: true);
                    return;
                }
            }
            else
                _stall = 0;
            _lastAlong = along;

            if (Clock.ElapsedMilliseconds - _stepStartMs > StepTimeoutMs)
                FinishStep(gb, along, blocked: true);
        }

        private static void FinishStep(fldGlobalWork_t gb, float along, bool blocked)
        {
            PadInput.ReleaseAll();
            _stepping = false;
            float cross = (gb.playerX - _startX) * _dz - (gb.playerZ - _startZ) * _dx;
            Log.Info($"[Nav:Blob] Step {_stepName} {(blocked ? "blocked" : "done")}: moved {FieldData.F(along)} of {FieldData.F(_target)}, " +
                     $"drift {FieldData.F(cross)}, now {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)}, {_ticks} ticks.");

            int facing = _layer == Layer.FirstPerson ? GridSense.Normalize90(gb.PlayerKakudoGoal) : _modFacing;
            CellView v = GridSense.Look(gb.playerX, gb.playerY, gb.playerZ, facing, _cell);
            if (blocked)
            {
                // A door stops the player at its wall; the game's own "Open Door" prompt says what it is.
                if (GridSense.Side(gb.playerX, gb.playerY, gb.playerZ, _wantFacing, _cell) != SideKind.Door)
                    Say("Wall.");
                _view = v;
                _viewFacing = facing;
                _haveView = true;
                return;
            }

            string change = Changes(v, facing);
            _view = v;
            _viewFacing = facing;
            _haveView = true;
            if (change.Length > 0)
                Say(change);
        }

        private static string Changes(CellView v, int facing)
        {
            if (!_haveView || _viewFacing != facing)
                return Describe(v.Ahead, "ahead");
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

        /// <summary>The pad button, or diagonal pair, whose camera-relative bearing is nearest the wanted world facing.</summary>
        private static SDF_PADMAP[] PadsFor(int kakudo)
        {
            float camYaw = 0;
            GameObject cam = fldCamera.fldCameraObj;
            if (cam != null)
                camYaw = cam.transform.eulerAngles.y;
            float want = ((180 - kakudo) % 360 + 360) % 360;   // compass bearing = Unity yaw = 180 - kakudo

            SDF_PADMAP[][] combos =
            {
                new[] { SDF_PADMAP.SDF_PADMAP_U },
                new[] { SDF_PADMAP.SDF_PADMAP_U, SDF_PADMAP.SDF_PADMAP_R },
                new[] { SDF_PADMAP.SDF_PADMAP_R },
                new[] { SDF_PADMAP.SDF_PADMAP_D, SDF_PADMAP.SDF_PADMAP_R },
                new[] { SDF_PADMAP.SDF_PADMAP_D },
                new[] { SDF_PADMAP.SDF_PADMAP_D, SDF_PADMAP.SDF_PADMAP_L },
                new[] { SDF_PADMAP.SDF_PADMAP_L },
                new[] { SDF_PADMAP.SDF_PADMAP_U, SDF_PADMAP.SDF_PADMAP_L },
            };
            int best = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < combos.Length; i++)
            {
                float bearing = camYaw + i * 45f;
                float diff = Math.Abs(((bearing - want) % 360 + 540) % 360 - 180);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = i;
                }
            }
            Log.Info($"[Nav:Blob] Camera yaw {FieldData.F(camYaw)}, wanted bearing {FieldData.F(want)}: combo {best} ({FieldData.F(bestDiff)} degrees off before the game's 45-degree snap).");
            return combos[best];
        }

        private static void StopStep(string why)
        {
            if (!_stepping)
                return;
            PadInput.ReleaseAll();
            _stepping = false;
            Log.Info($"[Nav:Blob] Step {_stepName} stopped: {why}.");
        }

        /// <summary>OnUpdate: a step stops when the field update stops (battle, event, menu).</summary>
        internal static void CheckExploring()
        {
            if (_stepping && !FieldState.IsExploring)
                StopStep("no longer exploring");
        }

        private static void Say(string text)
        {
            Log.Info("[Nav:Blob] Say: " + text);
            Pending.Enqueue(text);
        }

        private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
