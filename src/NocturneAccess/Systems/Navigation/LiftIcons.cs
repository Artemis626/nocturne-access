using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppfield_H;
using NocturneAccess.Core;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Which way a lift goes (0.0.206; Francesco, 27 Nov 2026: the map shows each lift's direction, as it does a ladder's, but the
    /// game's prompt only says "Ride"). The auto-map draws an up or a down arrow on every lift: its icon records
    /// (fldAutoMap.pfldAmArea[area].pSB[].pIcon[]) carry type 2 on lifts that go up and type 3 on lifts that go down, checked
    /// against the scripts' destinations on every saved dump with Ride volumes: the Obelisk (field 31), the Kagutsuchi Tower's
    /// lower floors (field 32: area 16's Ride to 196F type 2, its Rides down to 179F type 3; the shaft's blocks) and upper floors
    /// (field 36: area 12's lift to area 16 type 2, area 11's lift down to area 4 type 3, area 0's Ride down to field 32 type 3).
    /// Icons stand at the area's map position plus the volume's x z. Where two lifts share a spot (a shaft's top and bottom), the
    /// icons and the volumes are paired by height, highest first; a spot whose icons disagree and do not pair up says nothing.
    /// </summary>
    internal static class LiftIcons
    {
        internal const int RidePrompt = 5;   // fldPanel.inputpanel_mes0[5], "Ride" (the script prompt table in every dump)
        private const uint UpIcon = 2, DownIcon = 3;
        private const float SpotReach = 150f;
        internal const string UpWords = "lift goes up", DownWords = "lift goes down";   // Francesco's words, 27 Nov 2026

        private struct Icon { public uint Type; public float X, Y, Z; }

        private static int _field = int.MinValue, _area = int.MinValue, _count = -1;
        private static readonly List<Icon> Icons = new List<Icon>();
        private static readonly HashSet<string> Logged = new HashSet<string>();

        /// <summary>
        /// ", lift goes up" or ", lift goes down" for a Ride volume at p, from the auto-map's arrow there; "" when there is none.
        /// others: the y of every event volume of the area within reach of p, p's own included (the spot's lifts, top and bottom).
        /// </summary>
        internal static string Mark(fldGlobalWork_t gb, string name, Vector4 p, IList<float> others)
        {
            try
            {
                Load(gb);
                var here = Icons.Where(c => Math.Abs(c.X - p.x) <= SpotReach && Math.Abs(c.Z - p.z) <= SpotReach).OrderBy(c => c.Y).ToList();
                if (here.Count == 0)
                    return "";
                uint type;
                if (here.All(c => c.Type == here[0].Type))
                    type = here[0].Type;
                else
                {
                    // stacked lifts: pair by height (y grows downwards: the smallest is the highest)
                    var ys = others.OrderBy(y => y).ToList();
                    int rank = ys.FindIndex(y => Math.Abs(y - p.y) < 0.5f);
                    if (ys.Count != here.Count || rank < 0)
                    {
                        LogOnce(gb, name, $"{here.Count} arrows of both kinds and {ys.Count} volumes at the spot: no direction said");
                        return "";
                    }
                    type = here[rank].Type;
                }
                string words = type == UpIcon ? UpWords : type == DownIcon ? DownWords : "";
                LogOnce(gb, name, $"auto-map icon type {type} at the spot: \"{(words.Length > 0 ? words : "no direction")}\"");
                return words.Length > 0 ? ", " + words : "";
            }
            catch (Exception ex)
            {
                LogOnce(gb, name, "auto-map read failed: " + ex.Message);
                return "";
            }
        }

        private static void Load(fldGlobalWork_t gb)
        {
            Il2CppReferenceArray<fldAmAreaData_t> areas = fldAutoMap.pfldAmArea;
            int count = areas == null ? 0 : Math.Min(fldAutoMap.nfldAmArea, areas.Length);
            if (gb.fieldID == _field && gb.areaID == _area && count == _count)
                return;
            _field = gb.fieldID;
            _area = gb.areaID;
            _count = count;
            Icons.Clear();
            if (fldAutoMap.gAmap_NowField != gb.fieldID || gb.areaID < 0 || gb.areaID >= count)
                return;
            fldAmAreaData_t area = areas[gb.areaID];
            if (area?.pPos == null || area.pSB == null)
                return;
            float ax = area.pPos.x, ay = area.pPos.y, az = area.pPos.z;
            int nb = (int)Math.Min(area.nSB, (uint)area.pSB.Length);
            for (int j = 0; j < nb; j++)
            {
                fldAmSBlock_t b = area.pSB[j];
                if (b?.pIcon == null)
                    continue;
                int ni = (int)Math.Min(b.nIcon, (uint)b.pIcon.Length);
                for (int k = 0; k < ni; k++)
                {
                    fldAmIcon_t icon = b.pIcon[k];
                    if (icon?.pPos == null || (icon.type != UpIcon && icon.type != DownIcon))
                        continue;
                    Icons.Add(new Icon { Type = icon.type, X = icon.pPos.x - ax, Y = icon.pPos.y - ay, Z = icon.pPos.z - az });
                }
            }
        }

        private static void LogOnce(fldGlobalWork_t gb, string name, string what)
        {
            if (Logged.Add($"{gb.fieldID}/{gb.areaID}/{name}"))
                Log.Info($"[Nav:Lift] {gb.fieldID}/{gb.areaID}/{name}: {what}.");
        }
    }
}
