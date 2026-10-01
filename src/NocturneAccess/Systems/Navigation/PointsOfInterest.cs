using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    internal enum PoiKind { Door, Person, ItemBox }

    internal sealed class Poi
    {
        public int Index;          // fldEveHit.gEvnRng index: the game's own hit index (gFldEveRngIdx) when in reach
        public PoiKind Kind;
        public string ResName = "";
        public float X, Z;
        public float Distance;
        public bool Reachable = true;   // false: no walkable cell within ReachRadius can be reached from the player (another room)

        public string Label => Kind == PoiKind.Door ? "Door" : Kind == PoiKind.Person ? "Person" : "Item box";

        /// <summary>How close a reachable cell must be: a door sits in its wall (goal cells about 75 away), a person or a box in
        /// furniture (the Soul's goal cell was 135 away, 0.0.21 test).</summary>
        public float ReachRadius => Kind == PoiKind.Door ? 150f : 250f;
    }

    /// <summary>
    /// Points of interest of the current area, from fldEveHit.gEvnRng and its gEvnRngBox (docs/PATHFINDING.md section 1):
    /// - doors: type 10 named "NNd_MM";
    /// - people: an NPC index (box npcIdx) whose fld_Npc.fldNpc entry is not Type 999;
    /// - item boxes: box TakaraIdx 0 or more, or an NPC of Type 999 ("itembox1").
    /// Story triggers ("NNeve_MM") are invisible and left out. The game has no names for these (empty name tables), so the
    /// mod says its own category words. Page Up / Page Down move the selection (nearest first) and speak kind, distance in
    /// metres (game units / 100) and compass direction (north = +Z, the game's compass).
    /// </summary>
    internal static class PointsOfInterest
    {
        private static readonly Regex DoorName = new Regex(@"^\d+d_\d+$");
        private static readonly string[] Compass = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        private static int _selected = -1;     // gEvnRng index
        private static int _field = int.MinValue, _area = int.MinValue;

        internal static Poi Selected { get; private set; }

        /// <summary>
        /// The current area's points of interest, nearest first, each marked Reachable from the player through the walk
        /// grid (all marked reachable when there is no grid here).
        /// </summary>
        internal static List<Poi> Build(fldGlobalWork_t gb)
        {
            float px = gb.playerX, pz = gb.playerZ;
            bool grid = WalkGrid.Ensure(gb.fieldID, gb.areaID, px, gb.playerY, pz);
            var list = new List<Poi>();
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            Il2CppReferenceArray<putevebox_s> boxes = fldEveHit.gEvnRngBox;
            Il2CppReferenceArray<fldNpc_tag> npcs = fld_Npc.fldNpc;
            int n = ranges == null ? 0 : Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            int npcCount = npcs == null ? 0 : Math.Min(fld_Npc.fldNpcNum, npcs.Length);
            for (int i = 0; i < n; i++)
            {
                fldResHead_t h = ranges[i];
                if (h == null || h.sisei == null)
                    continue;
                string name = h.resourceName ?? "";
                putevebox_s box = boxes != null && i < boxes.Length ? boxes[i] : null;
                PoiKind? kind = null;
                if (h.resourceType == 10 && DoorName.IsMatch(name))
                    kind = PoiKind.Door;
                else if (box != null && box.TakaraIdx >= 0)
                    kind = PoiKind.ItemBox;
                else if (box != null && box.npcIdx >= 0 && box.npcIdx < npcCount && npcs[box.npcIdx] != null)
                    kind = npcs[box.npcIdx].Type == 999 ? PoiKind.ItemBox : PoiKind.Person;
                if (kind == null)
                    continue;

                Vector4 p = h.sisei.pos;
                list.Add(new Poi
                {
                    Index = i,
                    Kind = kind.Value,
                    ResName = name,
                    X = p.x,
                    Z = p.z,
                    Distance = (float)Math.Sqrt((p.x - px) * (p.x - px) + (p.z - pz) * (p.z - pz)),
                });
                Poi added = list[list.Count - 1];
                if (grid)
                    added.Reachable = WalkGrid.ReachableNear(px, pz, added.X, added.Z, added.ReachRadius);
            }
            return list.OrderBy(q => q.Distance).ToList();
        }

        /// <summary>Page Up (+1) / Page Down (-1).</summary>
        internal static void Cycle(int step)
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || !FieldState.IsExploring || gb.fieldID < 10)
            {
                SpeechManager.Say("No points of interest here.", interrupt: true);
                return;
            }
            if (gb.fieldID != _field || gb.areaID != _area)
            {
                _field = gb.fieldID;
                _area = gb.areaID;
                _selected = -1;
            }

            List<Poi> all = Build(gb);
            Log.Info($"[Nav:Poi] {all.Count} in field {gb.fieldID} area {gb.areaID}: " +
                     string.Join("; ", all.Select(q => $"{q.Index} {q.Label} {q.ResName} at {FieldData.F(q.X)} {FieldData.F(q.Z)} d {FieldData.F(q.Distance)}{(q.Reachable ? "" : " (not reachable, left out)")}")));
            List<Poi> list = all.Where(q => q.Reachable).ToList();
            if (list.Count == 0)
            {
                Selected = null;
                SpeechManager.Say("No points of interest here.", interrupt: true);
                return;
            }

            int at = list.FindIndex(q => q.Index == _selected);
            at = at < 0 ? (step > 0 ? 0 : list.Count - 1) : ((at + step) % list.Count + list.Count) % list.Count;
            Poi poi = list[at];
            _selected = poi.Index;
            Selected = poi;
            SpeechManager.Say($"{poi.Label}, {Describe(gb.playerX, gb.playerZ, poi.X, poi.Z)}. {at + 1} of {list.Count}.", interrupt: true);
        }

        /// <summary>The selected point of interest, re-read from the game's list (null if none or the area changed).</summary>
        internal static Poi Current(fldGlobalWork_t gb)
        {
            if (gb == null || gb.fieldID != _field || gb.areaID != _area || _selected < 0)
                return null;
            return Build(gb).FirstOrDefault(q => q.Index == _selected && q.Reachable);
        }

        /// <summary>"5 metres, south-east".</summary>
        internal static string Describe(float px, float pz, float x, float z)
        {
            float dx = x - px, dz = z - pz;
            double metres = Math.Sqrt(dx * dx + dz * dz) / 100.0;
            string dist = metres < 1 ? "here" : $"{Math.Round(metres)} {(Math.Round(metres) == 1 ? "metre" : "metres")}";
            if (metres < 1)
                return dist;
            double bearing = (Math.Atan2(dx, dz) * 180.0 / Math.PI + 360) % 360;   // 0 = +Z = north, 90 = +X = east
            return $"{dist}, {Compass[(int)Math.Round(bearing / 45.0) % 8]}";
        }
    }
}
