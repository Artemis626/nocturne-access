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
    internal enum PoiKind { Door, Person, ItemBox, Place, Event }

    /// <summary>The Page Up list's filters (Shift+Page Up / Page Down, Francesco 4 Oct 2026), in their order; All first.</summary>
    internal enum PoiCategory { All, Doors, Rooms, Exits, Warps, Souls, Demons, ItemBoxes, Events }

    internal sealed class Poi
    {
        public int Index;          // fldEveHit.gEvnRng index: the game's own hit index (gFldEveRngIdx) when in reach
        public PoiKind Kind;
        public string ResName = "";
        public float X, Y, Z;     // Y points down; a door's volume centre is about 100 above its floor
        public float Distance;
        public bool Reachable = true;   // false: no walkable cell within ReachRadius can be reached from the player (another room)
        public string Name = "";        // the game's place name for a door (FldNameData.GetBasyoName), or ""
        public bool Soul;               // a person whose model is "shinentai" (the game's speaker name for them is "Soul")
        public bool Demon;              // a person with no field object: a demon, whose model is not a field object (the game's speaker name is "Demon")
        public float FloorY;            // the floor height to walk to: the player's height inside the volume's height span (gEvnRngBox.vtx), else its bottom; without a box Y + 100 for doors, Y for others; world-map entrances: the ground under the volume (0.0.42)
        public float NX, NZ;            // a door's facing (gEvnRngBox.boxnml, pointing out to the side the player stands on), or 0 0
        public bool HasFront => NX * NX + NZ * NZ > 0.25f;

        public bool Explored;           // a generic door (no place name) the player has gone through (ExploredDoors)
        public bool Empty;              // an item box the game has opened (fld_Npc.gfldTakaraWork MotPhase, PointsOfInterest.Opened)
        public bool World;              // on the world map (FieldState.WorldMap): doors are entrances to places, heights are terrain
        public bool ToWorld;            // a dungeon door that leads to the world map (DoorTable.ToField below 10): "Exit to overworld" (0.0.44), never "explored" (0.0.45)

        public string Label => Kind == PoiKind.Door ? (Warp ? "Warp" : ToWorld ? "Exit to overworld" : World ? (Name.Length > 0 ? Name + " entrance" : "Entrance") : Name.Length > 0 ? Name + " door" : "Door")
            : Kind == PoiKind.Place ? Name
            : Kind == PoiKind.Event ? (Name.Length > 0 ? "Event, " + Name : "Event")
            : Kind == PoiKind.Person ? (Soul ? "Soul" : Demon ? "Demon" : "Person") : "Item box";

        public string DoorKind = "";    // a generic door: "room", "passage" or "exit", from the game's door table (DoorTable), or ""

        /// <summary>
        /// The filter it belongs to: Exits lead off the field (door table "exit", "Exit to overworld"); Rooms are doors to a room
        /// (door table "room") and doors with the game's place name whose kind is not passage or exit (Terminal, Healing Room),
        /// and named event volumes; Doors are the other doors (passages, Amala's doors, world-map entrances); Souls include
        /// other people.
        /// </summary>
        public PoiCategory Category;

        /// <summary>A door volume that moves the player to another spot of this area as soon as it is walked into (WarpMap, 0.0.52): "Warp".</summary>
        public bool Warp;

        /// <summary>Reached only through this many warps (WarpMap.RouteTo), 0 when it can be walked to.</summary>
        public int ViaWarps;

        /// <summary>", through 2 warps", or "".</summary>
        public string Via => ViaWarps == 0 ? "" : ViaWarps == 1 ? ", through 1 warp" : $", through {ViaWarps} warps";

        /// <summary>"explored" or "empty", or "".</summary>
        public string State => Explored ? "explored" : Empty ? "empty" : "";

        /// <summary>Page Up / Page Down: "Door, explored, room", "Door, passage", "Item box, empty".</summary>
        public string Spoken => Label + (State.Length > 0 ? ", " + State : "") + (DoorKind.Length > 0 ? ", " + DoorKind : "");

        /// <summary>The label inside a sentence: game names and "Soul" keep their capitals, the mod's own words do not;
        /// "explored door", "empty item box"; a door with a kind as on Page Up: "door, explored, room".</summary>
        public string InSentence => Kind == PoiKind.Event ? (Name.Length > 0 ? "event, " + Name : "event")
            : DoorKind.Length > 0 || ToWorld ? Spoken.ToLowerInvariant()
            : (State.Length > 0 ? State + " " : "") + (Name.Length > 0 || Soul || Demon ? Label : Label.ToLowerInvariant());

        /// <summary>Something beyond the plain label is known (End says it first).</summary>
        public bool HasDetail => State.Length > 0 || DoorKind.Length > 0;

        /// <summary>Doors only: the in-reach check uses fldEveHit.OldHitDoor, which resets on leaving the volume (gFldEveRngIdx does not).</summary>
        public bool IsDoorLike => Kind == PoiKind.Door || Kind == PoiKind.Place;

        /// <summary>How close a reachable cell must be. A door sits in its wall: usable doors had walkable cells within about
        /// 75 units; Medical Center door 01d_06, whose volume lies behind the wall, had none nearer than 115 and never fired
        /// (0.0.23 test). A person or a box stands in furniture (the Soul's goal cell was 135 away, 0.0.21 test).</summary>
        public float ReachRadius => IsDoorLike ? 95f : 250f;

        /// <summary>A door's volume footprint in X and Z (gEvnRngBox.vtx), when it has a box: the game fires the door when the player
        /// enters it, so a walkable cell inside it (VolumeMargin around) is in reach whatever the distance to the centre.</summary>
        public bool HasBox;
        public float BoxX0, BoxX1, BoxZ0, BoxZ1;
        internal const float VolumeMargin = 30f;

        /// <summary>
        /// Whether a walk ending at (x, z) reaches the target: within ReachRadius of its centre, or for a door inside its volume
        /// footprint. The Underground Mall stairs (field 17 area 1, 01d_01, 3 Oct 2026) have a 600-wide volume centred on the
        /// stairs' central divider: the nearest walkable cells (65 clear of the divider) lay 105 from the centre and the way
        /// out was "not reachable" (0.0.49).
        /// </summary>
        internal bool NearVolume(float x, float z)
        {
            if (IsDoorLike && HasBox && x >= BoxX0 - VolumeMargin && x <= BoxX1 + VolumeMargin && z >= BoxZ0 - VolumeMargin && z <= BoxZ1 + VolumeMargin)
                return true;
            return (x - X) * (x - X) + (z - Z) * (z - Z) <= ReachRadius * ReachRadius;
        }
    }

    /// <summary>
    /// Points of interest of the current area, from fldEveHit.gEvnRng and its gEvnRngBox (docs/PATHFINDING.md section 1):
    /// - doors: type 10 named "NNd_MM", and type-10 event volumes ("NNeve_MM", the elevator) that have a place pad;
    ///   a door is named with the game's own text, FldNameData.GetBasyoName(fieldID, basyo) of the place pad in front of it:
    ///   a small horizontal polygon with Attr 0x1000 and a Basyo number (area 5: Terminal, Healing Room, Elevator), the
    ///   polygon that makes the location banner name the room when the player steps into the door volume;
    /// - people: an NPC index (box npcIdx) whose fld_Npc.fldNpc entry is not Type 999; "Soul" when its object is
    ///   "shinentai(Clone)" (the speaker name the game shows for them), "Demon" when it has no object;
    /// - item boxes: box TakaraIdx 0 or more, or an NPC of Type 999 ("itembox1").
    /// Other story triggers ("NNeve_MM") are invisible and left out. Unnamed things get the mod's own category words. Page Up / Page Down move the selection (nearest first) and speak kind, distance in
    /// metres (game units / 100) and compass direction (north = +Z, the game's compass).
    /// On the world map (0.0.39) the same list is read: doors are entrances named by their place pads ("Yoyogi Park
    /// entrance"), heights are not spoken, and from 0.0.41 only reachable points are listed, as in dungeons.
    /// </summary>
    internal static class PointsOfInterest
    {
        private static readonly Regex DoorName = new Regex(@"^\d+d_\d+$");
        private static readonly Regex EventName = new Regex(@"^\d+eve_\d+$");
        private const float PadReach = 300f;      // a place pad this close to a door names it
        // Larger place polygons are whole rooms, not door pads. Ginza's door pads are 800 x 400 (field 19, 4 Oct 2026: the Terminal,
        // Rag's Jewelry and the Cathedral lost their names and were taken for plain doors, "explored"); Shibuya's were under 700.
        private const float PadMaxSize = 1000f;
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
            float px = gb.playerX, py = gb.playerY, pz = gb.playerZ;
            // The world map has its own walking map from 0.0.41 (WalkGrid, world-map walls); 0.0.39 listed everything there.
            bool world = FieldState.WorldMap(gb);
            bool grid = WalkGrid.Ensure(gb.fieldID, gb.areaID, px, gb.playerY, pz);
            var list = new List<Poi>();
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            Il2CppReferenceArray<putevebox_s> boxes = fldEveHit.gEvnRngBox;
            Il2CppReferenceArray<fldNpc_tag> npcs = fld_Npc.fldNpc;
            int n = ranges == null ? 0 : Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            int npcCount = npcs == null ? 0 : Math.Min(fld_Npc.fldNpcNum, npcs.Length);
            List<CollisionPoly> pads = PlacePads();
            for (int i = 0; i < n; i++)
            {
                fldResHead_t h = ranges[i];
                if (h == null || h.sisei == null)
                    continue;
                string name = h.resourceName ?? "";
                putevebox_s box = boxes != null && i < boxes.Length ? boxes[i] : null;
                PoiKind? kind = null;
                Vector4 p = h.sisei.pos;
                string placeName = h.resourceType == 10 ? PlaceName(gb.fieldID, pads, p.x, p.y, p.z) : "";
                bool soul = false;
                bool demon = false;
                int takara = box != null && box.TakaraIdx >= 0 ? box.TakaraIdx : -1;
                if (h.resourceType == 10 && DoorName.IsMatch(name))
                    kind = PoiKind.Door;
                else if (h.resourceType == 10 && EventName.IsMatch(name) && placeName.Length > 0)
                    kind = PoiKind.Place;   // the basement elevator; dropped below when a real door already has the name
                else if (h.resourceType == 10 && EventName.IsMatch(name) && !RoomSized(box))
                    kind = PoiKind.Event;   // a story volume: Loki and his room's door in Nyx's Lounge (01eve_01 "Talk", 01eve_03 "Open Door"), 0.0.53
                else if (box != null && box.TakaraIdx >= 0)
                    kind = PoiKind.ItemBox;
                else if (box != null && box.npcIdx >= 0 && box.npcIdx < npcCount && npcs[box.npcIdx] != null)
                {
                    fldNpc_tag npc = npcs[box.npcIdx];
                    GameObject obj = npc.fldObj;
                    if (!Shown(npc))
                        continue;   // a hidden NPC the story has not placed yet (basement npc_hoji_01, 0.0.24 test)
                    kind = npc.Type == 999 ? PoiKind.ItemBox : PoiKind.Person;
                    soul = obj != null && (obj.name ?? "").StartsWith("shinentai", StringComparison.Ordinal);
                    demon = obj == null;
                    if (npc.Type == 999)
                        takara = npc.ItemBoxIdx;
                }
                if (kind == null)
                    continue;
                bool generic = kind == PoiKind.Door && placeName.Length == 0 && !world;   // world map entrances lead to places, not rooms
                int toField = kind == PoiKind.Door && !world ? DoorTable.ToField(gb.fieldID, gb.areaID, name) : -1;
                bool toWorld = toField > 0 && FieldState.WorldMapField(toField);

                VolumeSpan(box, gb.playerY, out float floorY, out float nx, out float nz);
                bool doorLike = kind == PoiKind.Door || kind == PoiKind.Place;
                bool hasBox = Footprint(box, out float bx0, out float bx1, out float bz0, out float bz1);
                // The world map: the ground under the volume, never the player's height. Shibuya's volume 01d_03 is 400 tall, so
                // from the low south (Soul 01npc_02, y about -1420) the player's height lay in its span and became the floor
                // to reach, 400 below the entrance: "not reachable" (3 Oct 14:10). Offline (tools/worldgrid.pl FLOOR=under,
                // Shinjuku dumps): the ground under each entrance's centre is within 8 of the walking map's nearest cell.
                if (world && doorLike && CollisionCache.FloorUnder(p.x, p.y, p.z, out float ground, groundOnly: true) != null)
                    floorY = ground;
                else if (world && doorLike && !float.IsNaN(floorY))
                    floorY = BoxBottom(box);
                list.Add(new Poi
                {
                    Index = i,
                    Kind = kind.Value,
                    ResName = name,
                    Name = kind == PoiKind.Door || kind == PoiKind.Place ? placeName : kind == PoiKind.Event ? ExploredDoors.PromptOf(gb.fieldID, gb.areaID, name) : "",
                    Soul = soul,
                    Demon = demon,
                    X = p.x,
                    Y = p.y,
                    Z = p.z,
                    FloorY = float.IsNaN(floorY) ? (doorLike ? p.y + 100f : p.y) : floorY,
                    NX = doorLike ? nx : 0f,
                    NZ = doorLike ? nz : 0f,
                    Distance = (float)Math.Sqrt((p.x - px) * (p.x - px) + (p.z - pz) * (p.z - pz)),
                    Explored = generic && !toWorld && ExploredDoors.IsExplored(gb.fieldID, gb.areaID, name),   // an exit to the overworld is used again and again: never "explored" (Francesco, 0.0.45)
                    Warp = kind == PoiKind.Door && !world && WarpMap.TouchWarp(gb, i) != null,
                    Empty = kind == PoiKind.ItemBox && Opened(takara),
                    World = world,
                    ToWorld = toWorld,
                    HasBox = hasBox,
                    BoxX0 = bx0,
                    BoxX1 = bx1,
                    BoxZ0 = bz0,
                    BoxZ1 = bz1,
                    DoorKind = generic && !toWorld ? DoorTable.KindOf(gb.fieldID, gb.areaID, name) : "",
                });
            }

            // Event volumes next to doors carry the door's place name ("Room A203"): keep one only when no real door has
            // that name nearby. Doors listed twice at one spot (two volumes) are merged.
            var kept = new List<Poi>();
            foreach (Poi q in list)
            {
                if (q.Kind == PoiKind.Place && list.Any(d => d.Kind == PoiKind.Door && d.Name == q.Name && Near(d, q, 800f)))
                    continue;
                if (q.Kind == PoiKind.Event && list.Any(d => d.Kind == PoiKind.Door && Near(d, q, 100f)))
                    continue;   // a story volume on a door: the door is listed
                Poi same = kept.FirstOrDefault(d => d.Kind == q.Kind && Near(d, q, 20f) && Math.Abs(d.Y - q.Y) < 50f);
                if (same != null)
                {
                    same.Explored |= q.Explored;   // either volume of a door listed twice may be the one gone through
                    same.Empty |= q.Empty;
                    same.ToWorld |= q.ToWorld;
                    if (same.DoorKind.Length == 0)
                        same.DoorKind = q.DoorKind;
                    if (same.ToWorld)
                    {
                        same.DoorKind = "";   // "Exit to overworld" already says it
                        same.Explored = false;
                    }
                    continue;
                }
                // A warp's own volume and margin are kept out of the grid: WarpMap.RouteTo below decides for it.
                if (grid)
                    q.Reachable = !q.Warp && WalkGrid.ReachableNear(px, py, pz, q.X, q.FloorY, q.Z, q.ReachRadius)
                                  || (q.IsDoorLike && q.HasBox && WalkGrid.ReachableInRect(px, py, pz, q.BoxX0 - Poi.VolumeMargin, q.BoxZ0 - Poi.VolumeMargin,
                                      q.BoxX1 + Poi.VolumeMargin, q.BoxZ1 + Poi.VolumeMargin, q.FloorY));
                kept.Add(q);
            }
            // Beyond warps (0.0.52): a point no walk reaches may be reached by taking warps (Francesco, 4 Oct 2026: parts of the Amala
            // Network were missing from the list until he stood in them).
            if (grid && !world && WarpMap.Zones(gb.fieldID, gb.areaID).Count > 0)
                foreach (Poi q in kept.Where(q => !q.Reachable))
                {
                    List<Warp> legs = WarpMap.RouteTo(gb, q);
                    if (legs != null)
                    {
                        q.Reachable = true;
                        q.ViaWarps = legs.Count;   // 0: walked to (a warp next to the player's part of the area)
                    }
                }
            foreach (Poi q in kept)
                q.Category = CategoryOf(q, gb);
            return kept.OrderBy(q => q.Distance).ToList();
        }

        private static PoiCategory CategoryOf(Poi q, fldGlobalWork_t gb)
        {
            switch (q.Kind)
            {
                case PoiKind.ItemBox:
                    return PoiCategory.ItemBoxes;
                case PoiKind.Person:
                    return q.Demon ? PoiCategory.Demons : PoiCategory.Souls;
                case PoiKind.Place:
                    return PoiCategory.Rooms;
                case PoiKind.Event:
                    return PoiCategory.Events;
            }
            if (q.Warp)
                return PoiCategory.Warps;
            if (q.ToWorld)
                return PoiCategory.Exits;
            if (q.World)
                return PoiCategory.Doors;
            string kind = q.Name.Length == 0 ? q.DoorKind : DoorTable.KindOf(gb.fieldID, gb.areaID, q.ResName);
            if (kind == "exit")
                return PoiCategory.Exits;
            if (kind == "room" || (q.Name.Length > 0 && kind != "passage"))
                return PoiCategory.Rooms;
            return PoiCategory.Doors;
        }

        private static readonly string[] CategoryNames = { "All", "Doors", "Rooms", "Exits", "Warps", "Souls", "Demons", "Item boxes", "Events" };

        /// <summary>
        /// Whether an NPC is in the world: its object (fldNpc_tag.fldObj) is active. The basement keeps npc_hoji_01 (hojiID
        /// 1, a Soul model) with its object inactive: the game never fires its hit, its collision box (Attr 0x80) does not stop
        /// the player, and walking to it ended in a spin (0.0.24 test).
        /// </summary>
        /// An NPC with no object at all is shown: the talking demons of the annex (14:07 and 14:30 sessions, 2 Oct 2026:
        /// "01npc_01" in area 12, "01npc_02" in area 13, "02npc_01"; tType 1, tCamera 2, fldObj null) were left out as hidden,
        /// while the game spoke them as "Demon".
        internal static bool Shown(fldNpc_tag npc)
        {
            if (npc == null)
                return false;
            GameObject obj = npc.fldObj;
            return obj == null || obj.activeInHierarchy;
        }

        /// <summary>
        /// From a volume's box corners (gEvnRngBox.vtx; all zero for people): the player's height when it lies in the box's height span, else the box bottom (NaN without a box), and its horizontal facing (boxnml; basement doors point out to the corridor).
        /// The annex gate's Pixie volume 01eve_01 has its centre at y 200, below the floor (doors: -100), and was left out as
        /// unreachable while the game fired its "Talk" from the floor at y 0 (21:31 session).
        /// </summary>
        private static void VolumeSpan(putevebox_s box, float playerY, out float floorY, out float nx, out float nz)
        {
            floorY = float.NaN;
            nx = nz = 0f;
            if (box == null)
                return;
            Vector4 n = box.boxnml;
            float len = (float)Math.Sqrt(n.x * n.x + n.z * n.z);
            if (len > 0.5f && Math.Abs(n.y) < 0.5f)
            {
                nx = n.x / len;
                nz = n.z / len;
            }
            Il2CppStructArray<Vector4> vtx = box.vtx;
            if (vtx == null || vtx.Length == 0)
                return;
            float lo = float.MaxValue, hi = float.MinValue, spread = 0f;
            for (int k = 0; k < vtx.Length; k++)
            {
                Vector4 v = vtx[k];
                lo = Math.Min(lo, v.y);
                hi = Math.Max(hi, v.y);
                spread += Math.Abs(v.x) + Math.Abs(v.z);
            }
            if (spread < 1f)
                return;   // no box (size 0 0 0)
            // Y points down: hi is the bottom of the box. Inside the span the game's hit can fire where the player stands; outside
            // it the floor is under the box (the stairwell's 1F door seen from 2F must stay downstairs).
            floorY = playerY >= lo - 50f && playerY <= hi + 50f ? playerY : hi;
        }

        /// <summary>A story volume as big as a room (more than 1500 across): a trigger for walking into an area, not a thing to go to.</summary>
        private static bool RoomSized(putevebox_s box)
            => Footprint(box, out float x0, out float x1, out float z0, out float z1) && (x1 - x0 > 1500f || z1 - z0 > 1500f);

        /// <summary>A volume's box footprint in X and Z (gEvnRngBox.vtx); false without a box (all zero for people).</summary>
        private static bool Footprint(putevebox_s box, out float x0, out float x1, out float z0, out float z1)
        {
            x0 = z0 = float.MaxValue;
            x1 = z1 = float.MinValue;
            Il2CppStructArray<Vector4> vtx = box?.vtx;
            if (vtx == null || vtx.Length == 0)
                return false;
            for (int k = 0; k < vtx.Length; k++)
            {
                x0 = Math.Min(x0, vtx[k].x); x1 = Math.Max(x1, vtx[k].x);
                z0 = Math.Min(z0, vtx[k].z); z1 = Math.Max(z1, vtx[k].z);
            }
            return x1 - x0 + z1 - z0 > 1f;
        }

        /// <summary>The bottom of a volume's box (the largest vtx y; Y points down). VolumeSpan has checked the box is real.</summary>
        private static float BoxBottom(putevebox_s box)
        {
            float hi = float.MinValue;
            Il2CppStructArray<Vector4> vtx = box.vtx;
            for (int k = 0; k < vtx.Length; k++)
                hi = Math.Max(hi, vtx[k].y);
            return hi;
        }

        /// <summary>
        /// Whether the game has opened item box takaraIdx (gEvnRngBox.TakaraIdx, fldNpc_tag.ItemBoxIdx: the same number in every
        /// dump): its fld_Npc.gfldTakaraWork entry has left phase 0. Read offline (2 Oct 2026): fld_Npc.fldItemBoxAdd sets
        /// MotPhase 3 when the game's fldGbChkTakaraOpen says the box was opened, else 0; fldItemBoxCalcTask moves an opening box
        /// through phases 1, 2 and 3. Seen in game: Annex 1F boxes 8 and 9 went from MotPhase 0 (15:05 dump) to 3 (15:06 dump)
        /// after being opened. fldGbChkTakaraOpen itself runs in protected code and is not called.
        /// </summary>
        private static bool Opened(int takaraIdx)
        {
            if (takaraIdx < 0)
                return false;
            Il2CppReferenceArray<fldTakaraWork_tag> work = fld_Npc.gfldTakaraWork;
            int n = work == null ? 0 : Math.Min(fld_Npc.gfldTakaraNum, work.Length);
            for (int i = 0; i < n; i++)
            {
                fldTakaraWork_tag w = work[i];
                if (w != null && w.TakaraIdx == takaraIdx)
                    return w.MotPhase != 0 || w.MotPhaseNext != 0;
            }
            return false;
        }

        private static bool Near(Poi a, Poi b, float d) => (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z) <= d * d;

        /// <summary>Place pads of the loaded area: small horizontal polygons with Attr 0x1000 and a Basyo number.</summary>
        private static List<CollisionPoly> PlacePads()
        {
            var pads = new List<CollisionPoly>();
            foreach (CollisionPoly c in CollisionCache.Polys)
            {
                if ((c.Attr & 0x1000) == 0 || c.Basyo == 0 || c.Points.Length < 3 || Math.Abs(c.Normal.y) < 0.5f)
                    continue;
                Bounds(c, out float x0, out float z0, out float x1, out float z1);
                if (x1 - x0 <= PadMaxSize && z1 - z0 <= PadMaxSize)
                    pads.Add(c);
            }
            return pads;
        }

        /// <summary>The game's name of the nearest place pad within PadReach of (x, z) on the door's floor (a door volume
        /// centre is about 100 above its floor; the stairwell has doors above each other), or "".</summary>
        private static string PlaceName(int field, List<CollisionPoly> pads, float x, float y, float z)
        {
            CollisionPoly best = null;
            float bestD = PadReach;
            foreach (CollisionPoly c in pads)
            {
                if (Math.Abs(c.Points[0].y - (y + 100f)) > 150f)
                    continue;
                Bounds(c, out float x0, out float z0, out float x1, out float z1);
                float dx = Math.Max(0, Math.Max(x0 - x, x - x1)), dz = Math.Max(0, Math.Max(z0 - z, z - z1));
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d <= bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            if (best == null)
                return "";
            try
            {
                FldNameData names = fldGlobal.fldNameData;
                string s = names == null ? null : names.GetBasyoName(field, best.Basyo);
                return TextUtil.Clean(s ?? "").Trim();
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav:Poi] Place name {best.Basyo} could not be read: {ex.Message}");
                return "";
            }
        }

        private static void Bounds(CollisionPoly c, out float x0, out float z0, out float x1, out float z1)
        {
            x0 = z0 = float.MaxValue;
            x1 = z1 = float.MinValue;
            foreach (Vector4 v in c.Points)
            {
                x0 = Math.Min(x0, v.x); x1 = Math.Max(x1, v.x);
                z0 = Math.Min(z0, v.z); z1 = Math.Max(z1, v.z);
            }
        }

        /// <summary>
        /// The list Page Up / Page Down go through: the reachable points of the current filter. In map order (0.0.51, config
        /// PoiMapOrder) it is fixed for the area: north to south in bands of MapBand units, west to east in a band, so a point
        /// keeps its number while the player walks (Francesco, 4 Oct 2026: nearest first renumbered everything after each
        /// corridor, "2 of 7" became "5 of 7", which prevents a mental map). Points reachable from one place and not from
        /// another still come and go (another room, another block).
        /// </summary>
        private static List<Poi> Listed(fldGlobalWork_t gb, bool log)
        {
            List<Poi> all = Build(gb);
            if (log)
                Log.Info($"[Nav:Poi] {all.Count} in field {gb.fieldID} area {gb.areaID}: " +
                         string.Join("; ", all.Select(q => $"{q.Index} {q.Spoken} [{q.Category}] {q.ResName} at {FieldData.F(q.X)} {FieldData.F(q.Z)} d {FieldData.F(q.Distance)}{(q.Reachable ? q.Via : " (not reachable, left out)")}")));
            IEnumerable<Poi> list = all.Where(q => q.Reachable && (_filter == PoiCategory.All || q.Category == _filter));
            if (Config.PoiMapOrder?.Value ?? true)
                list = list.OrderByDescending(q => (int)Math.Floor(q.Z / MapBand)).ThenBy(q => q.X).ThenBy(q => q.Index);
            return list.ToList();
        }

        private const float MapBand = 300f;
        private static PoiCategory _filter = PoiCategory.All;

        private static void NewArea(fldGlobalWork_t gb)
        {
            if (gb.fieldID == _field && gb.areaID == _area)
                return;
            _field = gb.fieldID;
            _area = gb.areaID;
            _selected = -1;
            Selected = null;
        }

        /// <summary>Page Up (+1) / Page Down (-1).</summary>
        internal static void Cycle(int step)
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || !FieldState.IsExploring)
            {
                SpeechManager.Say("No points of interest here.", interrupt: true);
                return;
            }
            NewArea(gb);

            List<Poi> list = Listed(gb, log: true);
            if (list.Count == 0)
            {
                Selected = null;
                _selected = -1;
                SpeechManager.Say(_filter == PoiCategory.All ? "No points of interest here." : $"No {CategoryNames[(int)_filter].ToLowerInvariant()} here.", interrupt: true);
                return;
            }

            int at = list.FindIndex(q => q.Index == _selected);
            at = at < 0 ? (step > 0 ? 0 : list.Count - 1) : ((at + step) % list.Count + list.Count) % list.Count;
            Poi poi = list[at];
            _selected = poi.Index;
            Selected = poi;
            SpeechManager.Say($"{poi.Spoken}, {Describe(gb.playerX, gb.playerY, gb.playerZ, poi)}{poi.Via}. {at + 1} of {list.Count}.", interrupt: true);
        }

        /// <summary>
        /// Shift+Page Up (+1) / Shift+Page Down (-1): the next filter that has points here ("Rooms, 3."); All is always offered.
        /// The selection stays when it belongs to the new filter, else Page Up starts from the first.
        /// </summary>
        internal static void CycleFilter(int step)
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || !FieldState.IsExploring)
            {
                SpeechManager.Say("No points of interest here.", interrupt: true);
                return;
            }
            NewArea(gb);
            List<Poi> reachable = Build(gb).Where(q => q.Reachable).ToList();
            int count = CategoryNames.Length;
            PoiCategory next = _filter;
            for (int k = 1; k <= count; k++)
            {
                var c = (PoiCategory)((((int)_filter + step * k) % count + count) % count);
                if (c == PoiCategory.All || reachable.Any(q => q.Category == c))
                {
                    next = c;
                    break;
                }
            }
            _filter = next;
            int n = next == PoiCategory.All ? reachable.Count : reachable.Count(q => q.Category == next);
            if (Selected != null && next != PoiCategory.All && Selected.Category != next)
            {
                Selected = null;
                _selected = -1;
            }
            Log.Info($"[Nav:Poi] Filter {next}: {n} of {reachable.Count}.");
            SpeechManager.Say($"{CategoryNames[(int)next]}, {n}.", interrupt: true);
        }

        /// <summary>Insert: the selected point again, from where the player stands now, with its number in the list.</summary>
        internal static void SpeakCurrent()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || !FieldState.IsExploring)
            {
                SpeechManager.Say("No points of interest here.", interrupt: true);
                return;
            }
            NewArea(gb);
            List<Poi> list = Listed(gb, log: false);
            int at = list.FindIndex(q => q.Index == _selected);
            string filter = _filter == PoiCategory.All ? "" : $" {CategoryNames[(int)_filter]}.";
            if (at < 0)
            {
                SpeechManager.Say($"Nothing selected.{filter}", interrupt: true);
                return;
            }
            Poi poi = list[at];
            Selected = poi;
            SpeechManager.Say($"{poi.Spoken}, {Describe(gb.playerX, gb.playerY, gb.playerZ, poi)}{poi.Via}. {at + 1} of {list.Count}.{filter}", interrupt: true);
        }

        /// <summary>The selected point of interest, re-read from the game's list (null if none or the area changed).</summary>
        internal static Poi Current(fldGlobalWork_t gb)
        {
            if (gb == null || gb.fieldID != _field || gb.areaID != _area || _selected < 0)
                return null;
            return Build(gb).FirstOrDefault(q => q.Index == _selected && q.Reachable);
        }

        /// <summary>"5 metres, south-east", plus ", upstairs" or ", downstairs" when the point is on another floor (not on
        /// the world map, where heights are the terrain's slopes).</summary>
        internal static string Describe(float px, float py, float pz, Poi p)
        {
            float dx = p.X - px, dz = p.Z - pz;
            double metres = Math.Sqrt(dx * dx + dz * dz) / 100.0;
            string text = metres < 1 ? "here" : $"{Math.Round(metres)} {(Math.Round(metres) == 1 ? "metre" : "metres")}";
            if (metres >= 1)
            {
                double bearing = (Math.Atan2(dx, dz) * 180.0 / Math.PI + 360) % 360;   // 0 = +Z = north, 90 = +X = east
                text += $", {Compass[(int)Math.Round(bearing / 45.0) % 8]}";
            }
            if (p.World)
                return text;
            // Y points down; a door's volume centre is about 100 above its floor, a person's or box's position on it.
            float above = py - p.FloorY;
            if (above > 150f)
                text += ", upstairs";
            else if (above < -150f)
                text += ", downstairs";
            return text;
        }
    }
}
