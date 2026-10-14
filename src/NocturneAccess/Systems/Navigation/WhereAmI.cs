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
    /// - the party's macca (0.0.80; first when not walking: in a shop, the Cathedral or the Fountain);
    /// - last, the moon phase (0.0.46), as the HUD shows it in the Vortex World, dungeons included ("Moon 3 eighths, waxing").
    /// "Overworld, Yoyogi Kamizonocho. Nearest: Soul, 6 metres, north-west. Moon 3 eighths, waxing."
    /// </summary>
    internal static class WhereAmI
    {
        /// <summary>
        /// The moon (Kagutsuchi) as the HUD shows it, from dds3GlobalWork.DDS3_GBWK.Moon.Age (0 to 15; read offline,
        /// docs/class-index.md "Moon phase"): evtMoonProcess2 passes min(Age, 16 - Age) to the HUD, which shows it as "n/8",
        /// 4 "HALF", 8 "FULL"; Age 1 to 7 waxing, 9 to 15 waning. Only where the game draws it (fldMain.fldChkVortex, plain
        /// code): not before the Conception. "" otherwise.
        /// </summary>
        internal static string MoonPhase()
        {
            try
            {
                if (fldGlobal.fldGb == null || fldMain.fldChkVortex() == 0)   // it reads fldGb.fieldID
                    return "";
                Il2Cppevent_H.evtMoon_t moon = dds3GlobalWork.DDS3_GBWK?.Moon;
                if (moon == null)
                    return "";
                int age = ((moon.Age % 16) + 16) % 16;
                int type = Math.Min(age, 16 - age);
                string label = type == 0 ? "new" : type == 4 ? "half" : type == 8 ? "full" : type == 1 ? "1 eighth" : $"{type} eighths";   // Francesco: "3 eighths", not "3 slash 8"
                string trend = age == 0 || age == 8 ? "" : age < 8 ? ", waxing" : ", waning";
                Log.Info($"[Nav:Where] Moon age {moon.Age}, type {type}.");
                return $"Moon {label}{trend}";
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav:Where] Moon phase could not be read: {ex.Message}");
                return "";
            }
        }


        /// <summary>
        /// The place's name from the game's name table (fldGlobal.fldNameData, FldNameData): GetFieldName(field) (the field, as the
        /// title shown on entering an area or loading a save; that title is an image, no text is written), else
        /// GetSuspendPosName(field, area) (the save list's "Ginza-B1", "Amala Network 1-B3"). "" when the table has none, or when
        /// the banner already says it. Every name is logged, to check which one matches the title card (0.0.54).
        /// </summary>
        private static string PlaceTitle(fldGlobalWork_t gb, string banner)
        {
            string field = "", area = "", suspend = "";
            try
            {
                FldNameData names = fldGlobal.fldNameData;
                if (names == null)
                    return "";
                field = Clean(() => names.GetFieldName(gb.fieldID));
                area = Clean(() => names.GetAreaName(gb.fieldID, gb.areaID));
                suspend = Clean(() => names.GetSuspendPosName(gb.fieldID, gb.areaID));
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav:Where] Place names could not be read: {ex.Message}");
            }
            Log.Info($"[Nav:Where] Names for field {gb.fieldID} area {gb.areaID}: field \"{field}\", area \"{area}\", suspend \"{suspend}\", banner \"{banner}\".");
            foreach (string candidate in new[] { field, suspend })
            {
                if (candidate.Length > 0 && !banner.Contains(candidate) && !candidate.Equals(banner, StringComparison.OrdinalIgnoreCase))
                    return candidate;
                if (candidate.Length > 0)
                    return "";   // the banner already says it
            }
            return "";
        }

        /// <summary>The game's name of a field ("Mantra's Headquarters", FldNameData.GetFieldName), or "" (0.0.78: doors into
        /// another field say where they lead).</summary>
        internal static string FieldName(int field)
        {
            FldNameData names = fldGlobal.fldNameData;
            return names == null ? "" : Clean(() => names.GetFieldName(field));
        }

        // A name the game has no text for comes back as its id ("SUSPEND013_L0001", "AREA22_L0000": 15:21 session, 5 Oct 2026).
        private static readonly System.Text.RegularExpressions.Regex TextId = new System.Text.RegularExpressions.Regex(@"^[A-Z]+\d*_L\d+$");

        private static string Clean(Func<string> read)
        {
            try
            {
                string text = TextUtil.Clean(read() ?? "");
                return TextId.IsMatch(text) ? "" : text;
            }
            catch
            {
                return "";
            }
        }

        internal static void Speak()
        {
            var parts = new List<string>();
            string place = AnnouncementReader.LocationText;
            fldGlobalWork_t gb = fldGlobal.fldGb;
            bool exploring = gb != null && FieldState.IsExploring;
            bool world = exploring && FieldState.WorldMap(gb);
            if (world)
                parts.Add(place.Length > 0 ? $"Overworld, {place}" : "Overworld");
            else
            {
                // The place's own name before the banner, which is often only the floor ("B1"): Francesco, 5 Oct 2026.
                string title = exploring ? PlaceTitle(gb, place) : "";
                if (title.Length > 0)
                    parts.Add(place.Length > 0 ? $"{title}, {place}" : title);
                else if (place.Length > 0)
                    parts.Add(place);
            }

            if (exploring)
            {
                if (!world && gb.cammode == 0 && FirstPersonView.On)
                    parts.Add("Facing " + CompassWord(180f - gb.PlayerKakudo));
                Poi nearest = PointsOfInterest.Build(gb).FirstOrDefault(q => q.Reachable && q.Route == null);   // not one through other areas
                if (nearest != null)
                    parts.Add($"Nearest: {nearest.Spoken}, {PointsOfInterest.Describe(gb.playerX, gb.playerY, gb.playerZ, nearest)}");
            }

            // The party's macca (0.0.80, Francesco, 14 Oct 2026): first in menus, shops and institutions, where it is what Delete is
            // pressed for; while walking after the place and the nearest point, before the moon, which stays last.
            string macca = Macca.Spoken();
            if (macca.Length > 0)
            {
                if (exploring)
                    parts.Add(macca);
                else
                    parts.Insert(0, macca);
            }

            string moon = MoonPhase();   // last, as Francesco asked (0.0.46)
            if (moon.Length > 0)
                parts.Add(moon);

            string text = parts.Count == 0 ? "Location not known." : string.Join(". ", parts) + ".";
            Log.Info($"[Nav:Where] Field {(gb == null ? "-" : gb.fieldID.ToString())} area {(gb == null ? "-" : gb.areaID.ToString())}" +
                     $"{(gb == null ? "" : $" at {FieldData.F(gb.playerX)} {FieldData.F(gb.playerZ)}")}: {text}");
            SpeechManager.Say(text, interrupt: true, force: true);
        }
    }
}
