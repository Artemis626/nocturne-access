using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;
using static NocturneAccess.Systems.Navigation.Angles;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Spoken directions to the selected point of interest, on demand (End). The route is AutoWalk's (WalkGrid.Plan from
    /// the player's current position, 4 directions in first person, 8 in fixed-camera rooms), planned at each press; the
    /// player walks with the game's own keys and the mod never presses anything.
    /// - First person: relative to the facing: "Ahead, 12 metres, then right." / "Turn left, 5 metres." / "Turn round".
    /// - Third person (fixed camera): the key direction on screen, from the camera yaw as AutoWalk.Press chooses its pads:
    ///   "Up, 5 metres, then left."
    /// - In reach: "In reach." when facing the target, else the turn to face it.
    /// 0.0.25 spoke every change while walking; Francesco (20:37 session): too much audio clutter, the wall and corridor
    /// cues must come first, so directions are spoken only when End is pressed.
    /// </summary>
    internal static class RouteGuide
    {
        private const float MinRun = 60f;            // shorter first runs (a half-cell jog onto the grid) are not spoken
        private const float FacingTolerance = 50f;
        private static readonly string[] Keys = { "up", "up-right", "right", "down-right", "down", "down-left", "left", "up-left" };

        /// <summary>End (OnUpdate).</summary>
        internal static void Speak()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (FieldState.WorldMap(gb))
            {
                SpeechManager.Say("Directions are not available on the world map yet.", interrupt: true);   // docs/HANDOFF-OVERWORLD.md plan step 3
                return;
            }
            if (gb == null || !FieldState.IsExploring)
            {
                SpeechManager.Say("Directions are not available here.", interrupt: true);
                return;
            }
            Poi target = PointsOfInterest.Current(gb);
            if (target == null)
            {
                SpeechManager.Say("Select a point of interest first, with Page Up.", interrupt: true);
                return;
            }
            string text = Directions(gb, target);
            if (target.HasDetail)
                text = $"{TextUtil.Capitalize(target.InSentence)}. {text}";   // "Explored door. Ahead, 12 metres." / "Door, passage. Ahead, ..."
            Log.Info($"[Nav:Guide] To {target.Label} {target.ResName} (gEvnRng {target.Index}) from {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)}: {text}");
            SpeechManager.Say(text, interrupt: true);
        }

        private static string Directions(fldGlobalWork_t gb, Poi target)
        {
            bool firstPerson = gb.cammode == 0 && FirstPersonView.On;
            float x = gb.playerX, z = gb.playerZ, yaw = CameraYaw();
            float flat = (float)Math.Sqrt((target.X - x) * (target.X - x) + (target.Z - z) * (target.Z - z));
            float toTarget = Bearing(x, z, target.X, target.Z);

            if (AutoWalk.InReach(target, flat) && AutoWalk.Lateral(target, x, z) <= 45f)
            {
                float face = AutoWalk.FaceBearing(target, x, z);
                if (Math.Abs(Diff(180 - gb.PlayerKakudo, face)) <= FacingTolerance)
                    return "In reach.";
                string turn = firstPerson ? Relative(gb, Square(face)) : Key(face, yaw);
                return $"In reach. {TextUtil.Capitalize(turn)} to face it.";
            }

            if (!WalkGrid.Ensure(gb.fieldID, gb.areaID, gb.playerX, gb.playerY, gb.playerZ))
                return "No path.";
            List<PathRun> runs = AutoWalk.Route(gb, target, firstPerson, out float gx, out float gz, out string aim);
            if (runs == null)
                return "No path from here.";

            // Remaining length of each run from the player; drop tiny leading runs.
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
                legs.Add((firstPerson ? Square(toTarget) : Mod360((float)Math.Round(toTarget / 45.0) * 45f), flat));   // at the goal cell: straight at it

            (float bearing, float length) first = legs[0];
            string word = firstPerson ? Relative(gb, first.bearing) : Key(first.bearing, yaw);
            string then = "";
            if (legs.Count > 1)
                then = firstPerson ? TurnWord(Mod360(legs[1].bearing - first.bearing)) : Key(legs[1].bearing, yaw);
            int metres = Math.Max(1, (int)Math.Round(first.length / 100f));
            return $"{TextUtil.Capitalize(word)}, {metres} {(metres == 1 ? "metre" : "metres")}{(then.Length > 0 ? ", then " + then : "")}.";
        }

        /// <summary>First person: a compass bearing (multiple of 90) relative to the facing: "ahead", "turn right", "turn round", "turn left".</summary>
        private static string Relative(fldGlobalWork_t gb, float bearing)
        {
            float facing = 180f - gb.PlayerKakudoGoal;   // compass bearing = 180 - PlayerKakudo
            int rel = (int)Mod360((float)Math.Round(Mod360(bearing - facing) / 90.0) * 90f);
            return rel == 0 ? "ahead" : rel == 180 ? "turn round" : "turn " + TurnWord(rel);
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
    }
}
