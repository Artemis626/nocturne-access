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
using static NocturneAccess.Systems.Navigation.Angles;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Walks the player to the selected point of interest (docs/PATHFINDING.md sections 2.3 and 5). Home asks; the walk
    /// starts at the next field update. Every field update, before the game reads the pads (FieldState prefix), it holds
    /// the game's own pad buttons, as keys would be pressed:
    /// - Third person (fixed-camera rooms): fldPlayerCalc_Nml reads DDS3_ORGMAP_PRESS U, D, L, R relative to the camera
    ///   (U = camera yaw, R +90, D +180, L +270) and snaps the facing goal to world multiples of 45 degrees. The pads are
    ///   chosen again every tick because the "fixed" camera pans. The next run starts 20 units before the end of the
    ///   current one (the facing eases, so turns curve); drift over 40 units is corrected with the neighbouring 45.
    /// - First person: a 4-direction path; for each run the game's own turn (pad L +90, R -90 in PlayerKakudo, D turns
    ///   round, one tick each, waiting for the turn to finish as CentreWalk does), then pad U to the run's end.
    /// Re-plans when more than 90 units off the run or still for 8 ticks (at most 6 times). Arrival is the game's own:
    /// its hit index (fldEveHit.gFldEveRngIdx) is the target AND the player faces it (within 50 degrees): the 0.0.22
    /// test showed the "Open Door" prompt waits for the facing. If in reach but not facing, it turns towards the target.
    /// - World map (0.0.42, fieldID below 10): fldPlayerCalc_Wm2 reads the same pads fixed to the world (U north, R east,
    ///   16 units a tick, no facing): Press with yaw 0; in reach is arrival at once (the prompt needs no facing); a field
    ///   change (a random battle, 2 to 202) ends the walk; ticks where the game ignores the pads (NoInpPlCnt,
    ///   fldWmWarpMode) are not "still"; 90 s timeout.
    /// Ends after 30 s, or at once when the player presses a movement key. It never presses confirm.
    /// </summary>
    internal static class AutoWalk
    {
        private const float NextRunLead = 20f;
        private const float HalfTick = 14.5f;
        private const float OffPathLimit = 90f;
        private const float CorrectOn = 40f, CorrectOff = 10f;
        private const float FacingTolerance = 50f;
        private const int StillTicks = 8;
        private const int MaxReplans = 6;
        private const long TimeoutMs = 30000;
        private const long WorldTimeoutMs = 90000;   // Shinjuku is about 70 x 80 metres at 4.8 a second; Medical Center to Yoyogi Park took 26 s
        private const int ProgressTicks = 45;         // world map: no progress along the run for this long (about 1.5 s) re-plans
        private const float ProgressStep = 30f;
        private const long TurnWaitMs = 1500;
        private const int ApproachTicks = 30;
        private const int FaceTicks = 20;
        private const long RequestMs = 500;
        private const int IdleTicks = 3;        // the movement routine did not run: an event or dialogue took over
        private const float FacingMinDistance = 80f;
        // A door's prompt needs the facing even right at it: in the Healing Room (field 15 area 16) the player stood 39 units
        // from door 01d_01 facing away from it; "Arrived." without a turn gave no "Open Door" (20:37 session).
        private const float DoorFacingMinDistance = 15f;
        private const float FrontDistance = 30f;     // the point in front of a door the route aims at (the player stops about 25 out)
        private const float FrontLateral = 45f;      // a door's hit counts only this close to the line straight out of its centre
        private const float AxisReach = 150f;        // first person: a person is approached on a compass line, this far (talk ball radius 160 to 180)
        private const float AimTolerance = 45f;

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
        private static bool _active, _firstPerson, _world;
        private static Poi _target;
        private static List<PathRun> _runs;
        private static int _run, _replans, _still, _approach, _facing, _area, _field, _combo = -1, _turns, _idle, _moveCount;
        private static bool _correcting;
        private static float _startSlack;   // how far the player stood off the first run at planning (start cell away from a blocked spot)
        private static float _lastX, _lastZ;
        private static int _progressRun = -1, _progressTicks;
        private static float _progressAlong;
        private static long _startMs, _turnMs = long.MinValue / 2;

        // Warps (0.0.52, WarpMap): the walk's real target while it heads for a warp on the way, and the wait for the game to
        // move the player after a warp fired (or after it stopped taking movement for another reason: an event).
        private const float WarpJump = 300f;          // farther than this from where the movement stopped: the player was warped
        private const long EventWaitMs = 1200;        // no jump by then: an event or dialogue, the walk ends as before
        private const long WarpWaitMs = 4000;         // a warp aimed for that has not moved the player by then
        private const long ResumeWaitMs = 8000;       // after the jump, until the game gives movement back
        private const int MaxWarps = 12;
        private static Poi _final;
        private static bool _awaitWarp, _awaitPlanned, _jumped;
        private static long _awaitMs;
        private static float _awaitX, _awaitZ;
        private static int _resumeTicks, _warpsTaken;

        internal static bool Active => _active;

        /// <summary>Home (OnUpdate).</summary>
        internal static void Request() => _requestMs = Clock.ElapsedMilliseconds;

        /// <summary>OnUpdate: queued speech; a walk stops when the field update stops (battle, event, menu).</summary>
        internal static void Tick()
        {
            if (_active && !FieldState.IsExploring && !(_awaitWarp && Clock.ElapsedMilliseconds - _awaitMs < ResumeWaitMs))
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

            PadInput.ReleaseAll();
            // A random battle on the world map switches field 2 to 202 and back: the walk ends and does not resume by itself.
            if (gb.fieldID != _field || gb.areaID != _area)
            {
                Stop(gb.fieldID != _field ? "field change" : "area change", null);
                return;
            }
            if (_firstPerson != FirstPersonNow(gb))
            {
                Stop("view changed", "Auto-walk stopped.");
                return;
            }
            string key = RealMovementKey();
            if (key.Length > 0)
            {
                Stop($"pad {key} pressed", "Auto-walk stopped.");
                return;
            }
            if (Clock.ElapsedMilliseconds - _startMs > (_world ? WorldTimeoutMs : TimeoutMs))
            {
                Stop("timed out", "Auto-walk timed out.");
                return;
            }
            if (_awaitWarp)
            {
                AwaitWarp(gb);
                return;
            }
            // A story event or dialogue stops the game's movement routine while the field update goes on (0.0.23 test,
            // field 11: trigger 01eve_15 moved the player and opened a dialogue): end the walk instead of re-planning.
            int moves = FieldState.MoveCount;
            _idle = moves == _moveCount ? _idle + 1 : 0;
            _moveCount = moves;
            if (_idle >= IdleTicks)
            {
                // A door that moves the player itself (the Underground Mall stairs, 3 Oct 20:49:45: its hit was the target, then the
                // game walked the player through) takes movement as soon as it fires: that is arrival, not an interruption.
                float d = (float)Math.Sqrt((_target.X - gb.playerX) * (_target.X - gb.playerX) + (_target.Z - gb.playerZ) * (_target.Z - gb.playerZ));
                if (_target.IsDoorLike && InReach(_target, d) && !(_final != null && IsTouchWarp(gb, _target)))
                {
                    Stop("the game took movement with the target door's hit active", "Arrived.");
                    return;
                }
                // A warp on the way, or not: wait a moment to see whether the game moves the player (0.0.52).
                BeginAwait(gb, planned: _final != null && IsTouchWarp(gb, _target) && InReach(_target, d));
                return;
            }

            float x = gb.playerX, z = gb.playerZ;
            float moved = Math.Abs(x - _lastX) + Math.Abs(z - _lastZ);
            _lastX = x;
            _lastZ = z;
            _still = moved < 2f ? _still + 1 : 0;
            if (Turning(gb))
                _still = 0;   // a first-person turn stands still on purpose
            if (_world && (gb.NoInpPlCnt > 0 || fldPlayer.fldWmWarpMode != 0))
                _still = 0;   // fldPlayerCalc_Wm2 ignores the pads then (read offline): waiting, not blocked

            float toTarget = Bearing(x, z, _target.X, _target.Z);
            float flat = (float)Math.Sqrt((_target.X - x) * (_target.X - x) + (_target.Z - z) * (_target.Z - z));
            // The world map: the game's prompt ("Enter", "Talk") fires on entering range, the marker has no facing (logs 1 Oct
            // 19:57, 3 Oct 14:07): in reach is arrival, at once. Pressing on towards the target would walk past it.
            if (_world && _target.IsDoorLike && InReach(_target, flat))
            {
                Stop("the game's hit index is the target (world map)", "Arrived.");
                return;
            }

            // People and item boxes: the game's hit index fires at the edge of its range, the prompt ("Talk", "Examine") only
            // closer (Shibuya, 3 Oct 15:17: the demon's hit at 232 units, "Talk" at 172, after Francesco stepped on). Arrival is
            // the prompt on screen; with the hit but no prompt, carry on straight at the target (Approach ends with "Near the
            // ..." if it never comes). Before the prompt has ever been written this session, the hit counts, as up to 0.0.43.
            bool? prompt = _target.IsDoorLike ? null : Menus.AnnouncementReader.InteractionPromptVisible;
            if (prompt.HasValue && InReach(_target, flat))
            {
                if (prompt.Value)
                {
                    // A story volume needs the player turned to it, as a door does (Francesco, 5 Oct 2026: in Nyx's Lounge the
                    // "Talk" and "Open Door" of Loki and his room did nothing until he turned): face it first (0.0.54).
                    if (_target.Kind == PoiKind.Event && !_world)
                    {
                        float faceEvent = Bearing(x, z, _target.X, _target.Z);
                        if (!Facing(gb, faceEvent) && ++_facing <= FaceTicks)
                        {
                            FaceTowards(gb, faceEvent);
                            return;
                        }
                    }
                    Stop("the game's prompt is shown and its hit index is the target", "Arrived.");
                    return;
                }
                if (_approach == 0)
                    Log.Info($"[Nav:Walk] The game's hit index is the target at {FieldData.F(flat)} units, but no prompt yet: stepping closer.");
                Approach(gb, x, z, toTarget, flat);
                return;
            }
            if (_world && InReach(_target, flat))
            {
                Stop("the game's hit index is the target (world map)", "Arrived.");
                return;
            }

            float face = FaceBearing(_target, x, z);
            // A door's hit fires at the edge of its 300-wide volume: walking along the wall, that is beside the door. Only in front
            // of it (or at the end of the route) does it count.
            bool inFront = Lateral(_target, x, z) <= FrontLateral || _run >= _runs.Count;
            if (InReach(_target, flat) && _final != null && IsTouchWarp(gb, _target))
            {
                // A warp on the way: it fires as the player stands in it; keep walking in until the game takes over.
                if (_firstPerson)
                    PadInput.Hold(SDF_PADMAP.SDF_PADMAP_U, true);
                else
                    Press(toTarget);
                return;
            }
            if (InReach(_target, flat) && inFront)
            {
                bool noFacing = !_target.HasFront && flat < (_target.IsDoorLike ? DoorFacingMinDistance : FacingMinDistance);
                if (noFacing || Facing(gb, face) || ++_facing > FaceTicks)
                {
                    Stop($"the game's hit index is the target{(_facing > FaceTicks ? " (facing not reached)" : "")}", "Arrived.");
                    return;
                }
                FaceTowards(gb, face);
                return;
            }

            if (_run >= _runs.Count)
            {
                Approach(gb, x, z, toTarget, flat);
                return;
            }

            PathRun r = _runs[_run];
            Dir(r.Dir, out float ux, out float uz);
            float along = (x - r.Ax) * ux + (z - r.Az) * uz;
            float cross = (x - r.Ax) * uz - (z - r.Az) * ux;   // > 0: the player is to the right of the run (east of a run going north)
            // World map: sliding along a wall the marker keeps moving (the pads alternate between the run and its correction)
            // without getting on, so the still test never fires: 14:36 session, 10 s against wall #20 west of the Medical Center.
            if (_world)
            {
                if (_run != _progressRun || along > _progressAlong + ProgressStep)
                {
                    _progressRun = _run;
                    _progressAlong = along;
                    _progressTicks = 0;
                }
                else if (++_progressTicks > ProgressTicks)
                {
                    Replan(gb, $"no progress along run {_run + 1} for {ProgressTicks} ticks");
                    return;
                }
            }
            float lead = _firstPerson ? HalfTick : NextRunLead;
            if (along >= r.Length - lead)
            {
                _run++;
                _correcting = false;
                _turns = 0;
                if (_run >= _runs.Count)
                {
                    Log.Info($"[Nav:Walk] Goal cell reached at {FieldData.F(x)} {FieldData.F(z)}; approaching the {_target.InSentence}.");
                    Approach(gb, x, z, toTarget, flat);
                    return;
                }
                r = _runs[_run];
                LogRun(gb, "next run");
                Dir(r.Dir, out ux, out uz);
                cross = (x - r.Ax) * uz - (z - r.Az) * ux;
            }

            if (Math.Abs(cross) > OffPathLimit + (_run == 0 ? _startSlack : 0f) || (_still >= StillTicks && !Turning(gb)))
            {
                Replan(gb, Math.Abs(cross) > OffPathLimit ? $"{FieldData.F(cross)} off the run" : $"still for {_still} ticks");
                return;
            }

            if (_firstPerson)
            {
                // Face the run with the game's own turn, then walk.
                if (!FacingSquare(gb, r.Bearing))
                {
                    if (!TurnTo(gb, r.Bearing))
                        Replan(gb, "the turn did not happen");
                    return;
                }
                PadInput.Hold(SDF_PADMAP.SDF_PADMAP_U, true);
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

        /// <summary>
        /// The game's own "in reach": doors through fldEveHit.OldHitDoor (it resets on leaving the volume); others through
        /// gFldEveRngIdx or OldHitInf, which can keep an old value after the player walked away (0.0.23 test: 7 long after
        /// the elevator door), so the distance is checked too.
        /// </summary>
        internal static bool InReach(Poi target, float flat)
        {
            int i = target.Index;
            if (target.Kind == PoiKind.Door)
                return fldEveHit.OldHitDoor == i || (fldEveHit.gFldEveRngIdx == i && flat <= 150f);
            if (target.Kind == PoiKind.Place)
                return (fldEveHit.OldHitDoor == i || fldEveHit.gFldEveRngIdx == i) && flat <= 250f;
            return (fldEveHit.OldHitInf == i || fldEveHit.gFldEveRngIdx == i) && flat <= 350f;
        }

        private static void TryStart(fldGlobalWork_t gb)
        {
            if (_active)
                Stop("new request", null);
            if (!FieldState.IsExploring)
            {
                Say("Auto-walk is not available here.");
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
            CentreWalk.Cancel("auto-walk");
            _target = poi;
            _area = gb.areaID;
            _field = gb.fieldID;
            _world = FieldState.WorldMap(gb);
            _firstPerson = FirstPersonNow(gb);
            _replans = 0;
            _idle = 0;
            _moveCount = FieldState.MoveCount;
            _startMs = Clock.ElapsedMilliseconds;
            _final = null;
            _awaitWarp = false;
            _warpsTaken = 0;
            if (!Plan(gb, "start") && !StartWarpLeg(gb, poi, "start"))
            {
                Say("No path.");
                return;
            }
            _active = true;
            Say($"Walking to {poi.InSentence}, {PointsOfInterest.Describe(gb.playerX, gb.playerY, gb.playerZ, poi)}{poi.Via}.");
        }

        private static bool IsTouchWarp(fldGlobalWork_t gb, Poi p) => p != null && p.IsDoorLike && WarpMap.TouchWarp(gb, p.Index) != null;

        /// <summary>
        /// The target cannot be walked to, but warps reach it (WarpMap.RouteTo): head for the first warp, keeping the target in
        /// _final. False (target unchanged) when no warps lead there or the first warp cannot be walked to.
        /// </summary>
        private static bool StartWarpLeg(fldGlobalWork_t gb, Poi goal, string why)
        {
            List<Warp> legs = WarpMap.RouteTo(gb, goal);
            if (legs == null || legs.Count == 0)
                return false;
            Poi warp = PointsOfInterest.Build(gb).FirstOrDefault(q => q.Index == legs[0].Index);
            if (warp == null)
                return false;
            _target = warp;
            _final = goal;
            if (Plan(gb, $"{why}, warp {legs[0].Name} first, {legs.Count} warps to {goal.ResName}"))
                return true;
            _target = goal;
            _final = null;
            return false;
        }

        /// <summary>The game stopped taking movement: a warp aimed for (planned), or an event or an unplanned warp.</summary>
        private static void BeginAwait(fldGlobalWork_t gb, bool planned)
        {
            PadInput.ReleaseAll();
            _awaitWarp = true;
            _awaitPlanned = planned;
            _jumped = false;
            _resumeTicks = 0;
            _awaitMs = Clock.ElapsedMilliseconds;
            _awaitX = gb.playerX;
            _awaitZ = gb.playerZ;
            _moveCount = FieldState.MoveCount;
            Log.Info($"[Nav:Walk] The game took movement at {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)}{(planned ? $" in warp {_target.ResName}" : "")}: waiting for a warp.");
        }

        /// <summary>
        /// While waiting: a jump of more than WarpJump is a warp; once the game gives movement back the walk goes on from the
        /// arrival point (0.0.52; before, any loss of movement ended the walk: "Walk interrupted by an event", 19 times in the
        /// 15:36 session, most of them warps). No jump within EventWaitMs: an event, the walk ends as before.
        /// </summary>
        private static void AwaitWarp(fldGlobalWork_t gb)
        {
            long waited = Clock.ElapsedMilliseconds - _awaitMs;
            float moved = Dist(gb.playerX, gb.playerZ, _awaitX, _awaitZ);
            int moves = FieldState.MoveCount;
            bool moving = moves != _moveCount;
            _moveCount = moves;
            if (!_jumped && moved > WarpJump)
            {
                _jumped = true;
                Log.Info($"[Nav:Walk] Warped {FieldData.F(moved)} to {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)} after {waited} ms.");
            }
            if (_jumped)
            {
                _resumeTicks = moving ? _resumeTicks + 1 : 0;
                if (_resumeTicks >= 2)
                {
                    _awaitWarp = false;
                    ContinueAfterWarp(gb);
                }
                else if (waited > ResumeWaitMs)
                {
                    Stop("no movement after the warp", "Walk interrupted by an event.");
                }
                return;
            }
            if (!_awaitPlanned && waited > EventWaitMs)
                Stop("the game stopped taking movement (event or dialogue)", "Walk interrupted by an event.");
            else if (_awaitPlanned && waited > WarpWaitMs)
                Stop("the warp did not move the player", "Walk interrupted by an event.");
        }

        private static void ContinueAfterWarp(fldGlobalWork_t gb)
        {
            if (++_warpsTaken > MaxWarps)
            {
                Stop($"more than {MaxWarps} warps", "Blocked.");
                return;
            }
            Poi goal = _final ?? _target;
            _final = null;
            _target = goal;
            _replans = 0;
            _idle = 0;
            _startMs = Clock.ElapsedMilliseconds;
            WalkGrid.Ensure(gb.fieldID, gb.areaID, gb.playerX, gb.playerY, gb.playerZ);
            if (Plan(gb, $"after warp {_warpsTaken}") || StartWarpLeg(gb, goal, $"after warp {_warpsTaken}"))
                return;
            Stop("no path after the warp", "No path from here.");
        }

        /// <summary>
        /// The route to a point of interest, shared with RouteGuide (End). Where the walk should end:
        /// - a door with a facing (gEvnRngBox.boxnml): straight in front of it, FrontDistance out. Its volume is 300 wide, so
        ///   aiming at the centre from along the wall ended the walk at the volume's edge, beside the door (21:31 session);
        /// - a person or box in first person: on one of the four compass lines through it, AxisReach away, so the last turn
        ///   faces it squarely (the shortest of the four routes that ends within AimTolerance of its point);
        /// - otherwise the reachable cell nearest the target, at least 120 from a person or box (outside its 100 x 100 box).
        /// The goal must lie within the target's ReachRadius, or inside a door's volume (Poi.NearVolume, 0.0.49). Null if there is no route.
        /// </summary>
        internal static List<PathRun> Route(fldGlobalWork_t gb, Poi target, bool firstPerson, out float gx, out float gz, out string aim)
        {
            float px = gb.playerX, py = gb.playerY, pz = gb.playerZ;
            int allow = target.IsDoorLike ? WarpMap.TouchWarp(gb, target.Index)?.Zone.Id ?? -1 : -1;   // a warp aimed for may be entered (0.0.52)
            List<PathRun> runs;
            if (target.HasFront)
            {
                aim = "in front";
                runs = WalkGrid.Plan(px, py, pz, target.X + target.NX * FrontDistance, target.FloorY, target.Z + target.NZ * FrontDistance, 0f, firstPerson, out gx, out gz, allow);
                if (runs != null && target.NearVolume(gx, gz))
                    return runs;
            }
            else if (firstPerson && !target.IsDoorLike)
            {
                List<PathRun> best = null;
                float bestLength = float.MaxValue, bx = 0, bz = 0;
                string bestAim = "";
                for (int d = 0; d < 4; d++)
                {
                    float ax = target.X + (d == 1 ? AxisReach : d == 3 ? -AxisReach : 0f);
                    float az = target.Z + (d == 0 ? AxisReach : d == 2 ? -AxisReach : 0f);
                    List<PathRun> r = WalkGrid.Plan(px, py, pz, ax, target.FloorY, az, 0f, true, out float cx, out float cz);
                    if (r == null || Dist(cx, cz, ax, az) > AimTolerance)
                        continue;
                    float length = r.Sum(q => q.Length);
                    if (length < bestLength)
                    {
                        bestLength = length;
                        best = r;
                        bx = cx;
                        bz = cz;
                        bestAim = new[] { "north", "east", "south", "west" }[d] + " of it";
                    }
                }
                if (best != null)
                {
                    gx = bx;
                    gz = bz;
                    aim = bestAim;
                    return best;
                }
            }
            aim = "nearest";
            runs = WalkGrid.Plan(px, py, pz, target.X, target.FloorY, target.Z, target.IsDoorLike ? 0f : 120f, firstPerson, out gx, out gz, allow);
            if (runs != null && !target.NearVolume(gx, gz))
                return null;
            return runs;
        }

        /// <summary>The compass bearing the player should face at the end: into a door (against its facing), else towards the target.</summary>
        internal static float FaceBearing(Poi target, float x, float z)
            => target.HasFront ? Bearing(0f, 0f, -target.NX, -target.NZ) : Bearing(x, z, target.X, target.Z);

        /// <summary>Distance along a door's wall from the point straight in front of its centre (0 without a facing).</summary>
        internal static float Lateral(Poi target, float x, float z)
            => target.HasFront ? Math.Abs((x - target.X) * target.NZ - (z - target.Z) * target.NX) : 0f;

        private static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        private static bool Plan(fldGlobalWork_t gb, string why)
        {
            var clock = Stopwatch.StartNew();
            _runs = Route(gb, _target, _firstPerson, out float gx, out float gz, out string aim);
            if (_runs != null && !_target.NearVolume(gx, gz))
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
            _facing = 0;
            _turns = 0;
            _correcting = false;
            _combo = -1;
            _progressRun = -1;
            _lastX = gb.playerX;
            _startSlack = 0f;
            if (_runs.Count > 0)
            {
                Dir(_runs[0].Dir, out float sx, out float sz);
                _startSlack = Math.Abs((gb.playerX - _runs[0].Ax) * sz - (gb.playerZ - _runs[0].Az) * sx);
            }
            _lastZ = gb.playerZ;
            Log.Info($"[Nav:Walk] Plan ({why}, {(_firstPerson ? "first person" : _world ? "world map" : "third person")}) from {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)} " +
                     $"to {_target.Label} {_target.ResName} (gEvnRng {_target.Index}) at {FieldData.F(_target.X)} {FieldData.F(_target.Z)}: " +
                     $"goal cell {FieldData.F(gx)} {FieldData.F(gz)} ({aim}), {_runs.Count} runs [{string.Join(", ", _runs.Select(r => $"{r.Bearing}:{FieldData.F(r.Length)}"))}], " +
                     $"{clock.ElapsedMilliseconds} ms.");
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

        /// <summary>
        /// After the goal cell: straight at the target so the game's hit check can fire (doors: into the volume). A person or box
        /// closer than FacingMinDistance without the hit will not give one (the game's talk ball has a radius of 160 to 180):
        /// turning on there only spins, as the bearing flips round the target (0.0.24 test, hidden NPC npc_hoji_01).
        /// </summary>
        private static void Approach(fldGlobalWork_t gb, float x, float z, float toTarget, float flat)
        {
            if (++_approach > ApproachTicks || (_approach > 5 && _still >= 5 && !Turning(gb)) || (_firstPerson && !_target.IsDoorLike && flat < FacingMinDistance && !Turning(gb)))
            {
                Stop($"approach ended at {FieldData.F(x)} {FieldData.F(z)} without the game's hit (gFldEveRngIdx {fldEveHit.gFldEveRngIdx})",
                     $"Near the {_target.InSentence}.");
                return;
            }
            if (_target.HasFront && Lateral(_target, x, z) <= FrontLateral)
                toTarget = FaceBearing(_target, x, z);   // in front of a door: straight into it
            if (_firstPerson)
            {
                float square = Square(toTarget);
                if (!FacingSquare(gb, square))
                {
                    TurnTo(gb, square);
                    _approach--;   // turning does not use up the approach
                    return;
                }
                PadInput.Hold(SDF_PADMAP.SDF_PADMAP_U, true);
                return;
            }
            Press(toTarget);
        }

        /// <summary>In reach but not facing the target: turn towards it (first person: the game's 90-degree turn).</summary>
        private static void FaceTowards(fldGlobalWork_t gb, float toTarget)
        {
            if (_firstPerson)
            {
                float square = Square(toTarget);
                if (!FacingSquare(gb, square))
                    TurnTo(gb, square);
                return;
            }
            Press(toTarget);
        }

        /// <summary>
        /// One tick of the game's own first-person turn towards a compass bearing (a multiple of 90): pad L adds 90 to
        /// PlayerKakudo, R subtracts 90, D turns round (0.0.15 to 0.0.18 logs). Waits while a turn is running.
        /// Returns false when turns stop working (4 presses for one run).
        /// </summary>
        private static bool TurnTo(fldGlobalWork_t gb, float bearing)
        {
            if (Turning(gb) || Clock.ElapsedMilliseconds - _turnMs < 100)
                return true;
            if (++_turns > 4)
                return false;
            int want = Mod360((int)Math.Round(180 - bearing));                 // compass bearing = 180 - PlayerKakudo
            int now = GridSense.Normalize90(gb.PlayerKakudoGoal);
            int delta = Mod360(want - now);
            SDF_PADMAP pad = delta == 180 ? SDF_PADMAP.SDF_PADMAP_D : delta == 90 ? SDF_PADMAP.SDF_PADMAP_L : SDF_PADMAP.SDF_PADMAP_R;
            if (OffSquare(gb) > 2)
                pad = SDF_PADMAP.SDF_PADMAP_R;   // a diagonal facing kept from third person: square it first
            PadInput.Hold(pad, true);
            _turnMs = Clock.ElapsedMilliseconds;
            Log.Info($"[Nav:Walk] Turn: facing {FieldData.F(gb.PlayerKakudo)}, want bearing {FieldData.F(bearing)} (kakudo {want}): pad {pad.ToString().Replace("SDF_PADMAP_", "")}.");
            return true;
        }

        /// <summary>The mod's first-person walking: off on the world map, where the state kept from dungeons does not apply.</summary>
        private static bool FirstPersonNow(fldGlobalWork_t gb)
            => !FieldState.WorldMap(gb) && gb.cammode == 0 && FirstPersonView.On;

        private static bool Turning(fldGlobalWork_t gb)
            => _firstPerson && (fldPlayer.gFldPlayerSyukanDly != 0 || Math.Abs(Diff(gb.PlayerKakudo, gb.PlayerKakudoGoal)) > 2);

        private static bool FacingSquare(fldGlobalWork_t gb, float bearing)
            => !Turning(gb) && OffSquare(gb) <= 2 && Math.Abs(Diff(180 - gb.PlayerKakudoGoal, bearing)) < 1;

        private static bool Facing(fldGlobalWork_t gb, float bearing)
            => !Turning(gb) && Math.Abs(Diff(180 - gb.PlayerKakudo, bearing)) <= FacingTolerance;

        private static float OffSquare(fldGlobalWork_t gb)
            => Math.Abs(Diff(gb.PlayerKakudoGoal, GridSense.Normalize90(gb.PlayerKakudoGoal)));

        /// <summary>
        /// Holds the pad combination whose camera-relative bearing is nearest the wanted compass bearing (third person). On the
        /// world map the pads are fixed to the world (fldPlayerCalc_Wm2, read offline: U north, R east, two pads the diagonal):
        /// yaw 0, Combos[round(bearing / 45)], whatever the camera does.
        /// </summary>
        private static void Press(float bearing)
        {
            float camYaw = 0;
            GameObject cam = _world ? null : fldCamera.fldCameraObj;
            if (cam != null)
                camYaw = cam.transform.eulerAngles.y;
            int best = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < Combos.Length; i++)
            {
                float diff = Math.Abs(Diff(camYaw + i * 45f, bearing));
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = i;
                }
            }
            foreach (SDF_PADMAP p in Combos[best])
                PadInput.Hold(p, true);
            if (best != _combo)
            {
                _combo = best;
                Log.Info($"[Nav:Walk] Bearing {FieldData.F(Mod360(bearing))}, camera yaw {FieldData.F(camYaw)}: pads " +
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
            if (PadInput.Real(SDF_PADMAP.SDF_PADMAP_L1, 1)) return "L1";
            if (PadInput.Real(SDF_PADMAP.SDF_PADMAP_R1, 1)) return "R1";
            return "";
        }

        /// <summary>World unit vector of a path direction (0 north = +Z, clockwise).</summary>
        private static void Dir(int dir, out float ux, out float uz)
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
            _final = null;
            _awaitWarp = false;
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
