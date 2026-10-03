using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Menus;
using static NocturneAccess.Systems.Navigation.Angles;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Where am I (Delete, 0.0.44; Francesco, 3 Oct 2026: the game has no way to check the current location, most needed on
    /// the world map). Spoken on demand, from the game's own data:
    /// - the location banner's last text (fieldUI/flocation): the area in dungeons ("Hachiko Intersection"), the district on
    ///   the world map ("Yoyogi Kamizonocho"), which the game rewrites as the marker crosses districts; on the world map
    ///   preceded by the mod's word "Overworld". The field name tables (fldGlobal.area_name, basyo_name) read empty in every
    ///   dump, so the banner is the one name the game shows;
    /// - in first person, the facing ("Facing north");
    /// - the nearest reachable point of interest, as Page Up says it ("Nearest: Yoyogi Park entrance, 6 metres, south-east").
    /// "Overworld, Yoyogi Kamizonocho. Nearest: Soul, 6 metres, north-west."
    /// </summary>
    internal static class WhereAmI
    {
        private static readonly string[] Compass = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        internal static void Speak()
        {
            var parts = new List<string>();
            string place = AnnouncementReader.LocationText;
            fldGlobalWork_t gb = fldGlobal.fldGb;
            bool exploring = gb != null && FieldState.IsExploring;
            bool world = exploring && FieldState.WorldMap(gb);
            if (world)
                parts.Add(place.Length > 0 ? $"Overworld, {place}" : "Overworld");
            else if (place.Length > 0)
                parts.Add(place);

            if (exploring)
            {
                if (!world && gb.cammode == 0 && FirstPersonView.On)
                    parts.Add("Facing " + Compass[(int)(Mod360((float)Math.Round(Mod360(180f - gb.PlayerKakudo) / 45.0) * 45f) / 45) % 8]);
                Poi nearest = PointsOfInterest.Build(gb).FirstOrDefault(q => q.Reachable);
                if (nearest != null)
                    parts.Add($"Nearest: {nearest.Spoken}, {PointsOfInterest.Describe(gb.playerX, gb.playerY, gb.playerZ, nearest)}");
            }

            string text = parts.Count == 0 ? "Location not known." : string.Join(". ", parts) + ".";
            Log.Info($"[Nav:Where] Field {(gb == null ? "-" : gb.fieldID.ToString())} area {(gb == null ? "-" : gb.areaID.ToString())}" +
                     $"{(gb == null ? "" : $" at {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)}")}: {text}");
            SpeechManager.Say(text, interrupt: true, force: true);
        }
    }
}
