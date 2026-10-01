using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Il2Cpp;
using Il2Cppfield_H;
using Il2Cpplibsdf_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Walks the player to the selected point of interest in third person (fixed-camera rooms first; docs/PATHFINDING.md
    /// section 2.3). Home asks; the walk starts at the next field update.
    ///
    /// Every field update, before the game reads the pads (FieldState prefix), it holds the pad combination whose
    /// camera-relative bearing is nearest the wanted world direction: fldPlayerCalc_Nml reads DDS3_ORGMAP_PRESS U, D, L,
    /// R relative to the camera (U = camera yaw, R +90, D +180, L +270) and snaps the facing goal to world multiples of
    /// 45 degrees. The pads are chosen again every tick because the "fixed" camera pans while the player walks.
    /// The path (WalkGrid) is a list of straight runs; the next run starts 20 units before the end of the current one
    /// (the facing eases, so turns curve). Re-plans when more than 90 units off the run or still for 8 ticks (at most
    /// 6 times). Ends when the game's own hit index (fldEveHit.gFldEveRngIdx) is the target ("Arrived."; the game speaks
    /// its prompt), after a final straight approach of at most 1 s towards the target, after 30 s, or at once when the
    /// player presses a movement key. It never presses confirm.
    /// </summary>
    internal static class AutoWalk
    {
        private const float NextRunLead = 20f;
        private const float OffPathLimit = 90f;
        private const float CorrectOn = 40f, CorrectOff = 10f;
        private const int StillTicks = 8;
        private const int MaxReplans = 6;
        private const long TimeoutMs = 30000;
        private const int ApproachTicks = 30;
        private const long RequestMs = 500;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Queue<string> Pending = new Queue<string>();
        private static readonly SDF_PADMAP[][] Combos =
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

        private static long _requestMs = long.MinValue / 2;
        private static bool _active;
        private static Poi _target;
        private static List<PathRun> _runs;
        private static int _run, _replans, _still, _approach, _area, _combo = -1;
        private static bool _correcting;
        private static float _lastX, _lastZ;
        private static long _startMs;

        internal static bool Active => _active;

        /// <summary>Home (OnUpdate).</summary>
        internal static void Request() => _requestMs = Clock.ElapsedMilliseconds;

        /// <summary>OnUpdate: queued speech; a walk stops when the field update stops (battle, event, menu).</summary>
        internal static void Tick()
        {
            if (_active && !FieldState.IsExploring)
                Stop("no longer exploring", null);
            while (Pending.Count > 0)
                SpeechManager.Say(Pending.Dequeue(), interrupt: true);
        }

        /// <summary>Called by FieldState at the start of every field update, before the game reads the pads.</summary>
        internal static void BeforeCalc()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;

            if (Clock.ElapsedMilliseconds - _requestMs <= RequestMs)
            {
                _requestMs = long.MinValue / 2;
                TryStart(gb);
            }
            if (!_active)
                return;

            if (gb.areaID != _area || gb.fieldID < 10)
            {
                Stop("area change", null);
                return;
            }
            string key = RealMovementKey();
            if (key.Length > 0)
            {
                Stop($"pad {key} pressed", "Auto-walk stopped.");
                return;
            }
            if (fldEveHit.gFldEveRngIdx == _target.Index)
            {
                Stop("the game's hit index is the target", "Arrived.");
                return;
            }
            if (Clock.ElapsedMilliseconds - _startMs > TimeoutMs)
            {
                Stop("timed out", "Auto-walk timed out.");
                return;
            }

            float x = gb.playerX, z = gb.playerZ;
            float moved = Math.Abs(x - _lastX) + Math.Abs(z - _lastZ);
            _lastX = x;
            _lastZ = z;
            _still = moved < 2f ? _still + 1 : 0;

            if (_run >= _runs.Count)
            {
                Approach(gb, x, z);
                return;
            }

            PathRun r = _runs[_run];
            GridSenseDir(r.Dir, out float ux, out float uz);
            float along = (x - r.Ax) * ux + (z - r.Az) * uz;
            float cross = (x - r.Ax) * uz - (z - r.Az) * ux;   // > 0: the player is to the right of the run (east of a run going north)
            if (along >= r.Length - NextRunLead)
            {
                _run++;
                _correcting = false;
                if (_run >= _runs.Count)
                {
                    Log.Info($"[Nav:Walk] Goal cell reached at {FieldData.F(x)} {FieldData.F(z)}; approaching the {_target.Label.ToLowerInvariant()}.");
                    Approach(gb, x, z);
                    return;
                }
                r = _runs[_run];
                LogRun(gb, "next run");
                GridSenseDir(r.Dir, out ux, out uz);
                cross = (x - r.Ax) * uz - (z - r.Az) * ux;
            }

            if (Math.Abs(cross) > OffPathLimit || _still >= StillTicks)
            {
                Replan(gb, Math.Abs(cross) > OffPathLimit ? $"{FieldData.F(cross)} off the run" : $"still for {_still} ticks");
                return;
            }

            // Back towards the run's line with the neighbouring 45-degree direction when drifting off it.
            if (Math.Abs(cross) > CorrectOn)
                _correcting = true;
            else if (Math.Abs(cross) < CorrectOff)
                _correcting = false;
            float bearing = r.Bearing;
            if (_correcting)
                bearing += cross > 0 ? -45f : 45f;   // right of the run: turn left (anticlockwise bearing)
            Press(bearing);
        }

        private static void TryStart(fldGlobalWork_t gb)
        {
            if (_active)
                Stop("new request", null);
            if (!FieldState.IsExploring || gb.fieldID < 10)
            {
                Say("Auto-walk is not available here.");
                return;
            }
            if (gb.cammode == 0 && FirstPersonView.On)
            {
                Say("Auto-walk works in fixed-camera rooms and third person for now.");
                return;
            }
            Poi poi = PointsOfInterest.Current(gb);
            if (poi == null)
            {
                Say("Select a point of interest first, with Page Up.");
                return;
            }
            if (!WalkGrid.Ensure(gb.fieldID, gb.areaID, gb.playerX, gb.playerY, gb.playerZ))
            {
                Say("No path.");
                return;
            }
            _target = poi;
            _area = gb.areaID;
            _replans = 0;
            _startMs = Clock.ElapsedMilliseconds;
            if (!Plan(gb, "start"))
            {
                Say("No path.");
                return;
            }
            _active = true;
            Say($"Walking to {poi.Label.ToLowerInvariant()}, {PointsOfInterest.Describe(gb.playerX, gb.playerZ, poi.X, poi.Z)}.");
        }

        private static bool Plan(fldGlobalWork_t gb, string why)
        {
            var clock = Stopwatch.StartNew();
            float minDist = _target.Kind == PoiKind.Door ? 0f : 120f;   // outside a person's or box's 100 x 100 box
            _runs = WalkGrid.Plan(gb.playerX, gb.playerZ, _target.X, _target.Z, minDist, out float gx, out float gz);
            if (_runs != null && Math.Sqrt((gx - _target.X) * (gx - _target.X) + (gz - _target.Z) * (gz - _target.Z)) > _target.ReachRadius)
            {
                Log.Info($"[Nav:Walk] Plan ({why}): the nearest reachable cell {FieldData.F(gx)} {FieldData.F(gz)} is too far from {_target.Label} {_target.ResName}.");
                _runs = null;
            }
            if (_runs == null)
            {
                Log.Info($"[Nav:Walk] Plan ({why}): no reachable cell towards {_target.Label} {_target.ResName} at {FieldData.F(_target.X)} {FieldData.F(_target.Z)}.");
                return false;
            }
            _run = 0;
            _still = 0;
            _approach = 0;
            _correcting = false;
            _combo = -1;
            _lastX = gb.playerX;
            _lastZ = gb.playerZ;
            Log.Info($"[Nav:Walk] Plan ({why}) from {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)} to {_target.Label} {_target.ResName} " +
                     $"(gEvnRng {_target.Index}) at {FieldData.F(_target.X)} {FieldData.F(_target.Z)}: goal cell {FieldData.F(gx)} {FieldData.F(gz)}, " +
                     $"{_runs.Count} runs [{string.Join(", ", _runs.Select(r => $"{r.Bearing}:{FieldData.F(r.Length)}"))}], {clock.ElapsedMilliseconds} ms.");
            return true;
        }

        private static void Replan(fldGlobalWork_t gb, string why)
        {
            PadInput.ReleaseAll();
            if (++_replans > MaxReplans)
            {
                Stop($"blocked after {MaxReplans} re-plans ({why})", "Blocked.");
                return;
            }
            WalkGrid.Ensure(gb.fieldID, gb.areaID, gb.playerX, gb.playerY, gb.playerZ);
            if (!Plan(gb, $"re-plan {_replans}: {why}"))
                Stop("no path on re-plan", "No path.");
        }

        /// <summary>After the goal cell: straight at the target so the game's hit check can fire (doors: into the volume).</summary>
        private static void Approach(fldGlobalWork_t gb, float x, float z)
        {
            if (++_approach > ApproachTicks || (_approach > 5 && _still >= 5))
            {
                Stop($"approach ended at {FieldData.F(x)} {FieldData.F(z)} without the game's hit (gFldEveRngIdx {fldEveHit.gFldEveRngIdx})",
                     $"Near the {_target.Label.ToLowerInvariant()}.");
                return;
            }
            float bearing = (float)(Math.Atan2(_target.X - x, _target.Z - z) * 180.0 / Math.PI);
            Press(bearing);
        }

        /// <summary>Holds the pad combination whose camera-relative bearing is nearest the wanted compass bearing.</summary>
        private static void Press(float bearing)
        {
            float camYaw = 0;
            GameObject cam = fldCamera.fldCameraObj;
            if (cam != null)
                camYaw = cam.transform.eulerAngles.y;
            int best = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < Combos.Length; i++)
            {
                float diff = Math.Abs(((camYaw + i * 45f - bearing) % 360 + 540) % 360 - 180);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = i;
                }
            }
            PadInput.ReleaseAll();
            foreach (SDF_PADMAP p in Combos[best])
                PadInput.Hold(p, true);
            if (best != _combo)
            {
                _combo = best;
                Log.Info($"[Nav:Walk] Bearing {FieldData.F(((bearing % 360) + 360) % 360)}, camera yaw {FieldData.F(camYaw)}: pads " +
                         $"{string.Join("+", Combos[best].Select(p => p.ToString().Replace("SDF_PADMAP_", "")))} ({FieldData.F(bestDiff)} off).");
            }
        }

        private static void LogRun(fldGlobalWork_t gb, string what)
        {
            PathRun r = _runs[_run];
            Log.Info($"[Nav:Walk] {what} {_run + 1} of {_runs.Count}: bearing {r.Bearing}, {FieldData.F(r.Length)} units, " +
                     $"at {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)}, facing {FieldData.F(gb.PlayerKakudo)} goal {FieldData.F(gb.PlayerKakudoGoal)}.");
        }

        private static string RealMovementKey()
        {
            if (PadInput.Real(SDF_PADMAP.SDF_PADMAP_U, 0)) return "U";
            if (PadInput.Real(SDF_PADMAP.SDF_PADMAP_D, 0)) return "D";
            if (PadInput.Real(SDF_PADMAP.SDF_PADMAP_L, 0)) return "L";
            if (PadInput.Real(SDF_PADMAP.SDF_PADMAP_R, 0)) return "R";
            return "";
        }

        /// <summary>World unit vector of a path direction (0 north = +Z, clockwise).</summary>
        private static void GridSenseDir(int dir, out float ux, out float uz)
        {
            double a = dir * Math.PI / 4;
            ux = (float)Math.Round(Math.Sin(a), 6);
            uz = (float)Math.Round(Math.Cos(a), 6);
        }

        internal static void Stop(string why, string speech)
        {
            if (!_active)
                return;
            _active = false;
            PadInput.ReleaseAll();
            fldGlobalWork_t gb = fldGlobal.fldGb;
            string at = gb == null ? "" : $" at {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)}";
            Log.Info($"[Nav:Walk] Ended{at} after {Clock.ElapsedMilliseconds - _startMs} ms, {_replans} re-plans: {why}.");
            if (speech != null)
                Say(speech);
        }

        private static void Say(string text)
        {
            Log.Info("[Nav:Walk] Say: " + text);
            Pending.Enqueue(text);
        }
    }
}
