using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Spoken directions to the selected point of interest (End; Francesco, 0.0.24 test: "directions that update on every
    /// step", as Pokemon Access and his Suikoden mod give them). The route is AutoWalk's (WalkGrid.Plan from the player,
    /// 4 directions in first person, 8 in fixed-camera rooms), planned again as the player moves; the player walks with
    /// the game's own keys and the mod never presses anything.
    /// - First person: relative to the facing: "Ahead, 12 metres, then right." / "Turn left, 5 metres." / "Turn round".
    /// - Third person (fixed camera): the key direction on screen, from the camera yaw as AutoWalk.Press chooses its pads:
    ///   "Up, 5 metres, then left."
    /// Speaks when the direction changes (a turn is due, at once, also while walking) and when the player stops with a
    /// new distance. In reach of the target: "Arrived." when facing it, else the turn to face it. End again, an area
    /// change or Home ends it; Page Up / Page Down move it to the newly selected point.
    /// </summary>
    internal static class RouteGuide
    {
        private const long PlanEveryMs = 250;
        private const long StillMs = 400;
        private const float MinRun = 60f;            // shorter first runs (a half-cell jog onto the grid) are not spoken
        private const float FacingTolerance = 50f;
        private const float FacingMinDistance = 80f;
        private static readonly string[] Keys = { "up", "up-right", "right", "down-right", "down", "down-left", "left", "up-left" };

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static bool _active, _firstPerson, _spokeStill;
        private static Poi _target;
        private static int _field, _area;
        private static string _lastKey = "", _lastFull = "";
        private static float _lastX, _lastZ, _planX = float.NaN, _planZ, _planFacing, _planYaw;
        private static long _movedMs, _planMs = long.MinValue / 2;

        internal static bool Active => _active;

        /// <summary>End (OnUpdate): directions on, or off when already on.</summary>
        internal static void Toggle()
        {
            if (_active)
            {
                Stop("End pressed", "Directions off.");
                return;
            }
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || !FieldState.IsExploring || gb.fieldID < 10)
            {
                SpeechManager.Say("Directions are not available here.", interrupt: true);
                return;
            }
            Poi poi = PointsOfInterest.Current(gb);
            if (poi == null)
            {
                SpeechManager.Say("Select a point of interest first, with Page Up.", interrupt: true);
                return;
            }
            if (!WalkGrid.Ensure(gb.fieldID, gb.areaID, gb.playerX, gb.playerY, gb.playerZ))
            {
                SpeechManager.Say("No path.", interrupt: true);
                return;
            }
            AutoWalk.Stop("directions requested", null);
            _target = poi;
            _field = gb.fieldID;
            _area = gb.areaID;
            _active = true;
            Reset(gb);
            Log.Info($"[Nav:Guide] Start to {poi.Label} {poi.ResName} (gEvnRng {poi.Index}) at {FieldData.F(poi.X)} {FieldData.F(poi.Z)}.");
            SpeechManager.Say($"Directions to {poi.InSentence}, {PointsOfInterest.Describe(gb.playerX, gb.playerY, gb.playerZ, poi)}.", interrupt: true);
        }

        private static void Reset(fldGlobalWork_t gb)
        {
            _lastKey = _lastFull = "";
            _planX = float.NaN;
            _planMs = long.MinValue / 2;
            _lastX = gb.playerX;
            _lastZ = gb.playerZ;
            _movedMs = Clock.ElapsedMilliseconds;
            _spokeStill = true;
        }

        internal static void Stop(string why, string speech)
        {
            if (!_active)
                return;
            _active = false;
            Log.Info($"[Nav:Guide] Ended: {why}.");
            if (speech != null)
                SpeechManager.Say(speech, interrupt: false);
        }

        /// <summary>OnUpdate.</summary>
        internal static void Tick()
        {
            if (!_active)
                return;
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;
            if (gb.fieldID != _field || gb.areaID != _area)
            {
                Stop("area change", null);
                return;
            }
            if (!FieldState.IsExploring || AutoWalk.Active)
                return;   // dialogue, menu, event: wait

            Poi selected = PointsOfInterest.Selected;
            if (selected != null && selected.Index != _target.Index)
            {
                Poi now = PointsOfInterest.Current(gb);
                if (now != null)
                {
                    _target = now;
                    Reset(gb);
                    Log.Info($"[Nav:Guide] Now to {now.Label} {now.ResName} (gEvnRng {now.Index}).");
                }
            }

            long ms = Clock.ElapsedMilliseconds;
            float x = gb.playerX, z = gb.playerZ;
            if (Math.Abs(x - _lastX) + Math.Abs(z - _lastZ) > 2f)
            {
                _movedMs = ms;
                _spokeStill = false;
            }
            _lastX = x;
            _lastZ = z;
            if (ms - _planMs < PlanEveryMs)
                return;

            _firstPerson = gb.cammode == 0 && FirstPersonView.On;
            float facing = gb.PlayerKakudoGoal, yaw = CameraYaw();
            if (!float.IsNaN(_planX) && Math.Abs(x - _planX) + Math.Abs(z - _planZ) <= 2f
                && Math.Abs(Diff(facing, _planFacing)) < 1f && Math.Abs(Diff(yaw, _planYaw)) < 1f
                && (_spokeStill || ms - _movedMs < StillMs))
                return;   // nothing changed since the last plan
            _planMs = ms;
            _planX = x;
            _planZ = z;
            _planFacing = facing;
            _planYaw = yaw;

            float flat = (float)Math.Sqrt((_target.X - x) * (_target.X - x) + (_target.Z - z) * (_target.Z - z));
            float toTarget = Mod360((float)(Math.Atan2(_target.X - x, _target.Z - z) * 180.0 / Math.PI));
            if (AutoWalk.InReach(_target, flat))
            {
                if (flat < FacingMinDistance || Math.Abs(Diff(180 - gb.PlayerKakudo, toTarget)) <= FacingTolerance)
                {
                    Stop("in reach and facing", null);
                    SpeechManager.Say("Arrived.", interrupt: false);
                    return;
                }
                string turn = _firstPerson ? Relative(gb, Square(toTarget), true) : Key(toTarget, yaw);
                Speak("face:" + turn, $"{Capital(turn)} to face it.");
                return;
            }

            if (!Instruction(gb, x, z, toTarget, flat, yaw, out string key, out string full))
            {
                Speak("nopath", "No path from here.");
                return;
            }
            bool still = ms - _movedMs >= StillMs;
            if (key != _lastKey)
                Speak(key, full);
            else if (still && !_spokeStill)
            {
                if (full != _lastFull)
                    Speak(key, full);
                _spokeStill = true;
            }
        }

        private static void Speak(string key, string full)
        {
            if (key == _lastKey && full == _lastFull)
                return;
            _lastKey = key;
            _lastFull = full;
            Log.Info($"[Nav:Guide] Say: {full}");
            SpeechManager.Say(full, interrupt: false);
        }

        /// <summary>The next leg of the route: key = the direction words (a change is spoken at once), full = the sentence.</summary>
        private static bool Instruction(fldGlobalWork_t gb, float x, float z, float toTarget, float flat, float yaw, out string key, out string full)
        {
            key = full = "";
            float minDist = _target.IsDoorLike ? 0f : 120f;   // as AutoWalk.Plan
            float floorY = _target.IsDoorLike ? _target.Y + 100f : _target.Y;
            if (!WalkGrid.Ensure(gb.fieldID, gb.areaID, gb.playerX, gb.playerY, gb.playerZ))
                return false;
            List<PathRun> runs = WalkGrid.Plan(gb.playerX, gb.playerY, gb.playerZ, _target.X, floorY, _target.Z, minDist, _firstPerson,
                out float gx, out float gz);
            if (runs == null || Math.Sqrt((gx - _target.X) * (gx - _target.X) + (gz - _target.Z) * (gz - _target.Z)) > _target.ReachRadius)
                return false;

            // Remaining length of each run from the player; drop finished and tiny leading runs.
            var legs = new List<(float bearing, float length)>();
            for (int i = 0; i < runs.Count; i++)
            {
                PathRun r = runs[i];
                float length = r.Length;
                if (i == 0)
                {
                    double a = r.Dir * Math.PI / 4;
                    float along = (x - r.Ax) * (float)Math.Sin(a) + (z - r.Az) * (float)Math.Cos(a);
                    length = Math.Max(0f, length - along);
                }
                if (legs.Count == 0 && length < MinRun && i + 1 < runs.Count)
                    continue;
                legs.Add((r.Bearing, length));
            }
            if (legs.Count == 0)
                legs.Add((_firstPerson ? Square(toTarget) : Mod360((float)Math.Round(toTarget / 45.0) * 45f), flat));   // at the goal cell: straight at it

            (float bearing, float length) first = legs[0];
            string word = _firstPerson ? Relative(gb, first.bearing, true) : Key(first.bearing, yaw);
            string then = "";
            if (legs.Count > 1)
                then = _firstPerson ? TurnWord(Mod360(legs[1].bearing - first.bearing)) : Key(legs[1].bearing, yaw);
            int metres = Math.Max(1, (int)Math.Round(first.length / 100f));
            key = word + "|" + then;
            full = $"{Capital(word)}, {metres} {(metres == 1 ? "metre" : "metres")}{(then.Length > 0 ? ", then " + then : "")}.";
            return true;
        }

        /// <summary>First person: a compass bearing (multiple of 90) relative to the facing: "ahead", "turn right", "turn round", "turn left".</summary>
        private static string Relative(fldGlobalWork_t gb, float bearing, bool turnVerb)
        {
            float facing = 180f - gb.PlayerKakudoGoal;   // compass bearing = 180 - PlayerKakudo
            int rel = (int)Mod360((float)Math.Round(Mod360(bearing - facing) / 90.0) * 90f);
            if (rel == 0)
                return "ahead";
            string w = TurnWord(rel);
            return turnVerb ? (rel == 180 ? "turn round" : "turn " + w) : w;
        }

        private static string TurnWord(float rel)
        {
            int r = (int)Mod360((float)Math.Round(rel / 90.0) * 90f);
            return r == 0 ? "ahead" : r == 90 ? "right" : r == 180 ? "back" : "left";
        }

        /// <summary>Third person: the key direction whose camera-relative bearing is nearest (U = camera yaw, clockwise in 45s).</summary>
        private static string Key(float bearing, float yaw)
            => Keys[(int)Mod360((float)Math.Round(Mod360(bearing - yaw) / 45.0) * 45f) / 45 % 8];

        private static float CameraYaw()
        {
            GameObject cam = fldCamera.fldCameraObj;
            return cam != null ? cam.transform.eulerAngles.y : 0f;
        }

        private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static float Square(float bearing) => Mod360((float)Math.Round(bearing / 90.0) * 90f);

        private static float Diff(float a, float b) => ((a - b) % 360 + 540) % 360 - 180;

        private static float Mod360(float a) => (a % 360 + 360) % 360;
    }
}
