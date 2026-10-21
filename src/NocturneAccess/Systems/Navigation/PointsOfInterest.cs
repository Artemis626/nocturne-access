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
    internal enum PoiCategory { All, Doors, Rooms, Exits, Warps, Ladders, Souls, Demons, ItemBoxes, Actions, Events }

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
        public float[] OtherFloors = Array.Empty<float>();   // other floors under the volume within its height span, tried when FloorY is not reachable (0.0.94)
        public float NX, NZ;            // a door's facing (gEvnRngBox.boxnml, pointing out to the side the player stands on), or 0 0
        public bool HasFront => NX * NX + NZ * NZ > 0.25f;

        public bool Explored;           // a generic door (no place name) the player has gone through (ExploredDoors)
        public bool Empty;              // an item box the game has opened (fld_Npc.gfldTakaraWork MotPhase, PointsOfInterest.Opened)
        public bool AwayWarp;           // a walk-on warp into another area (door table movetype 3, the Ikebukuro Tunnel, 0.0.88): "Warp", kept out of routes
        public bool World;              // on the world map (FieldState.WorldMap): doors are entrances to places, heights are terrain
        public bool ToWorld;            // a dungeon door that leads to the world map (DoorTable.ToField below 10): "Exit to overworld" (0.0.44), never "explored" (0.0.45)

        public string Mark = "";         // world map: where an entrance lies when several share its name ("10 metres south of Mifunashiro", 0.0.95)
        public string Label => Kind == PoiKind.Door ? (Jump && Prompt.Length > 0 ? Prompt : Warp || AwayWarp ? "Warp" : Ladder ? LadderLabel : ToWorld ? "Exit to overworld" : World ? (Name.Length > 0 ? Name + " entrance" : "Entrance") + (Mark.Length > 0 ? ", " + Mark : "") : Name.Length > 0 ? Name + " door" : DoorWord)
            : Kind == PoiKind.Place ? Name
            : Kind == PoiKind.Event ? (Action ? ActionLabel : Story ? (Detail.Length > 0 ? "Story event: " + Detail : "Story event") : Name.Length > 0 ? "Event, " + Name : "Event")
            : Kind == PoiKind.Person ? (Soul ? "Soul" : Demon ? "Demon" : "Person") : "Item box";

        /// <summary>
        /// From the volume's own script (FieldScript, 0.0.71): an action waits on the game's prompt ("Examine", "Talk", "Ride";
        /// a door the game jumps down through, "Jump Off"); Detail is the game's first narration line after confirming ("There's
        /// a switch"). A story event runs as soon as it is walked into. Neither: the old "Event" (the script could not tell).
        /// </summary>
        public bool Action, Story, Jump;
        public string Prompt = "", Detail = "";
        public string ScriptNote = "";   // the dry run's verdict, for the log

        /// <summary>"Examine: There's a switch", or the prompt alone.</summary>
        private string ActionLabel => Detail.Length > 0 ? $"{Prompt}: {Detail}" : Prompt;

        public string DoorKind = "";    // a generic door: "room", "passage", "exit" or "to exit", from the game's door table (DoorTable), or ""

        /// <summary>
        /// The filter it belongs to: Exits lead off the field (door table "exit", "Exit to overworld"); Rooms are doors to a room
        /// (door table "room") and doors with the game's place name whose kind is not passage or exit (Terminal, Healing Room),
        /// and named event volumes; Doors are the other doors (passages, Amala's doors, world-map entrances); Souls include
        /// other people.
        /// </summary>
        public PoiCategory Category;

        /// <summary>A door volume that moves the player to another spot of this area as soon as it is walked into (WarpMap, 0.0.52): "Warp".</summary>
        public bool Warp;

        /// <summary>Reached only through this many warps, ladders or doors of this area (WarpMap.RouteTo), 0 when it can be walked to.</summary>
        public int ViaWarps;

        /// <summary>Those legs by kind, "1 ladder", "2 warps and 1 ladder" (WarpMap.Phrase, 0.0.55).</summary>
        public string ViaLegs = "";

        /// <summary>Reached only by going out of this area and back in through other areas (AreaRoutes, F7, 0.0.56), or null.</summary>
        public AreaRoute Route;

        /// <summary>", via 1 ladder", ", via 2 other areas", ", beyond doors", or "".</summary>
        public string Via => Beyond ? ", beyond doors" : Route != null ? $", via {Route.Phrase}" : ViaWarps == 0 ? "" : $", via {ViaLegs}";

        /// <summary>A landmark (Terminal, exit, stairs, named door, ladder) of this map with no known way there yet: listed anyway (0.0.59).</summary>
        public bool Beyond;

        /// <summary>The far end of a ladder whose near end the player can walk to: left out of the list (0.0.59).</summary>
        public bool Redundant;

        /// <summary>A ladder end (door table movetype 5): "Ladder up" / "Ladder down" (0.0.59).</summary>
        public bool Ladder;
        public int LadderDir;           // +1 up, -1 down, 0 not known

        /// <summary>"Ladder up": the game's place name for it ("Ladder"), or the mod's word, and where it takes the player.</summary>
        private string LadderLabel
        {
            get
            {
                // The game's own prompt for it, "Climb Ladder" or "Descend Ladder" (PANELMES 6 / 7, 0.0.75; Francesco, 13 Oct
                // 2026: ladders into other areas had lost their way since 0.0.74); else the place name and the mod's up / down.
                string prompt = LadderDir > 0 ? FieldScript.PanelText(ClimbPrompt) : LadderDir < 0 ? FieldScript.PanelText(DescendPrompt) : "";
                if (prompt.Length > 0)
                    return prompt;
                return (Name.Length > 0 ? Name : "Ladder") + (LadderDir > 0 ? " up" : LadderDir < 0 ? " down" : "");
            }
        }

        private const int ClimbPrompt = 6, DescendPrompt = 7;

        /// <summary>
        /// A landmark, listed from anywhere in a dungeon area (Francesco, 17 Oct 2026, 0.0.90: fourteen unidentifiable doors reachable
        /// from any point made the list confusing): a door or room the game names (Terminal, S-Terminal, Stone Cave, Mysterious
        /// Wall; Name from its place pad) or an exit to the overworld. Its way is said ("via 2 doors"), or "beyond doors" when none
        /// is known. Everything else (plain doors, ladders, item boxes, people, events) is listed only where the player can walk to
        /// it. Replaces 0.0.59's set (named doors, exits, ladders, "exit" doors) and 0.0.55's listing of every point through warps.
        /// </summary>
        public bool Landmark => !Warp && !AwayWarp && !Ladder && !Jump && (Kind == PoiKind.Door && (Name.Length > 0 || ToWorld || DoorKind == "exit" || DoorKind == "to exit") || Kind == PoiKind.Place && Name.Length > 0);

        /// <summary>The way through door pairs or warps to a point not yet known as a landmark (decided after "to exit", 0.0.91).</summary>
        internal List<Warp> HeldLegs;

        /// <summary>In the list: reachable, or a landmark listed beyond doors.</summary>
        public bool Listed => Reachable || Beyond;

        /// <summary>The names of other volumes merged into this one (a door listed twice at one spot), for AreaRoutes.</summary>
        public List<string> Aliases = new List<string>();

        /// <summary>Its volume name and those of the volumes merged into it.</summary>
        public IEnumerable<string> Names => new[] { ResName }.Concat(Aliases);

        /// <summary>"explored" or "empty", or "".</summary>
        public string State => Explored ? "explored" : Empty ? "empty" : "";

        /// <summary>
        /// Where a plain door leads (0.0.78; Francesco, 14 Oct 2026, options 1 and 2): the game's name of the field it leads into
        /// (FldNameData.GetFieldName, known before any visit; DestinationIsField), else the location banner the game showed on
        /// arriving through it (ExploredDoors.DestinationOf), unless that is the banner shown here. "" when not known.
        /// </summary>
        public string Destination = "";
        public bool DestinationIsField;

        /// <summary>The game's narration of the event that greets the player behind the door, with the flags of now (0.0.84;
        /// ExploredDoors.BehindLine): Kabukicho Prison's cells, "No one is here", then "This is the hole that the Manikin jumped
        /// into" once the Digging Manikin has dug it. "" when none is known.</summary>
        public string Behind = "";

        /// <summary>A door walked into (movetype 2) at the end of a run of steep, shallow steps into another area of this field:
        /// an escalator (Ikebukuro's East Hall, 14 Oct 2026; Francesco chose the word). EscalatorDir +1 up, -1 down, 0 not known.</summary>
        public bool Escalator;
        public int EscalatorDir;

        private string DoorWord => Escalator ? (EscalatorDir > 0 ? "Escalator up" : EscalatorDir < 0 ? "Escalator down" : "Escalator") : "Door";

        /// <summary>A door with no place name of its own: "Door", "Escalator up" (not a warp, ladder, jump, exit or world entrance).</summary>
        private bool PlainDoor => Kind == PoiKind.Door && !Jump && !Warp && !AwayWarp && !Ladder && !ToWorld && !World && Name.Length == 0;

        /// <summary>The door table's kind as said: "exit" is left out when the field it leads into is named.</summary>
        private string ShownKind => DestinationIsField && DoorKind == "exit" ? "" : DoorKind;

        /// <summary>"Explored door to East Hall 1F, passage", "Door to Mantra's Headquarters", "Escalator up, passage" (Francesco,
        /// 14 Oct 2026: "explored" before the door); inside a sentence the mod's words in lower case, the game's names as they are.</summary>
        private string DoorPhrase(bool inSentence)
        {
            string word = DoorWord;
            string lower = char.ToLowerInvariant(word[0]) + word.Substring(1);
            string head = Explored ? (inSentence ? "explored " : "Explored ") + lower : inSentence ? lower : word;
            string kind = ShownKind;
            return head + (Destination.Length > 0 ? " to " + Destination : "") + (kind.Length > 0 ? ", " + kind : "")
                   + (Behind.Length > 0 ? ": " + Behind : "");
        }

        /// <summary>Page Up / Page Down: "Explored door, room", "Door, passage", "Explored door to B10", "Item box, empty".</summary>
        public bool NextStep;            // the Obelisk time puzzle's next tile (TimePuzzle, 0.0.98): the only point listed
        public string StepAnswer = "";   // the answer its question needs ("No"), or ""

        /// <summary>"Next step: Time passed quickly", "Next step: Will you go back? Answer No." (Francesco chose "Next step", 20 Oct 2026).</summary>
        private string StepPhrase(bool inSentence)
        {
            string what = Detail.Length > 0 ? Detail : "Story event";
            string answer = StepAnswer.Length == 0 ? "" : (what.EndsWith("?") || what.EndsWith(".") ? " " : ". ") + "Answer " + StepAnswer;
            return (inSentence ? "next step: " : "Next step: ") + what + answer;
        }

        public string Spoken => NextStep ? StepPhrase(false)
            : PlainDoor ? DoorPhrase(false)
            : Label + (State.Length > 0 ? ", " + State : "") + (DoorKind.Length > 0 ? ", " + DoorKind : "");

        /// <summary>The label inside a sentence: game names and "Soul" keep their capitals, the mod's own words do not;
        /// "explored door", "empty item box"; a door with a kind as on Page Up: "explored door, room".</summary>
        public string InSentence => NextStep ? StepPhrase(true)
            : Action ? Spoken   // the game's own words keep their capitals: "Walking to Examine: There's a switch"
            : Kind == PoiKind.Event ? (Story ? "story event" : Name.Length > 0 ? "event, " + Name : "event")
            : PlainDoor ? DoorPhrase(true)
            : DoorKind.Length > 0 || ToWorld ? Spoken.ToLowerInvariant()
            : (State.Length > 0 ? State + " " : "") + (Name.Length > 0 || Soul || Demon ? Label : Label.ToLowerInvariant());

        /// <summary>Something beyond the plain label is known (End says it first).</summary>
        public bool HasDetail => State.Length > 0 || DoorKind.Length > 0 || Destination.Length > 0 || Behind.Length > 0;

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
        /// <summary>An action volume as big as a room (a lift platform: the Obelisk 99F's 01eve_09, 2000 across; 0.0.105): reached,
        /// walked to and arrived at by its box, as a door is.</summary>
        public bool Big;
        internal const float VolumeMargin = 30f;

        /// <summary>
        /// Whether a walk ending at (x, z) reaches the target: within ReachRadius of its centre, or for a door inside its volume
        /// footprint. The Underground Mall stairs (field 17 area 1, 01d_01, 3 Oct 2026) have a 600-wide volume centred on the
        /// stairs' central divider: the nearest walkable cells (65 clear of the divider) lay 105 from the centre and the way
        /// out was "not reachable" (0.0.49).
        /// </summary>
        internal bool NearVolume(float x, float z)
        {
            if ((IsDoorLike || Big) && HasBox && x >= BoxX0 - VolumeMargin && x <= BoxX1 + VolumeMargin && z >= BoxZ0 - VolumeMargin && z <= BoxZ1 + VolumeMargin)
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
        private static readonly Regex DoorName = FieldData.DoorVolume;
        private static readonly Regex EventName = FieldData.EventVolume;
        private const float PadReach = 300f;      // a place pad this close to a door names it
        // An event volume takes a place's name only from a pad it lies on: the place's own triggers sit on it (0 to 32 in all
        // 67 dumps to 11 Oct 2026: the overworld entrance rings, the basement elevator, Ginza), things beside a place lie off
        // it. The Assembly of Nihilo B12 switches (field 20 area 19, 01eve_01, 02, 06, "Examine") lie 180 to 240 from a
        // ladder's pad, took its name and were dropped as copies of the ladder door (11 Oct 2026, 21:28 session); 0.0.68
        // measured from the door instead and would have listed the overworld rings as events.
        private const float EventOnPad = 100f;
        // A door the game jumps down through: door table movetype 4, whose handlers (tobiwarp_label, tobi2warp_label in every
        // field script; fldWap.fldDoor_Find2 case 4) wait on prompt 3, "Jump Off" (ETC_EN PANELMES_L0003).
        private const int JumpMoveType = 4, JumpPrompt = 3, LadderMoveType = 5;

        // Ways a sighted player picks by eye, marked after the game's own prompt (Francesco's words; the others stay unmarked
        // for exploration). The Obelisk 61F to 70F (field 31 area 5): five cubes in a row at z 1250, 01eve_05 to 01eve_13 west to
        // east; each rides its rail (path_hoji_03 to 07) to an end point on 70F (006_01eve_NN: 01pos_06, 08, 10, 12, 14). Only
        // 01eve_07's, 01pos_08 at -250 3250, lies on the platform of the lift to 77F (01eve_26, 6 metres on; dump 20 Oct
        // 21:04:46), the second cube from the left facing north (Francesco, 20 Oct 2026, after much trial and error).
        private static readonly Dictionary<(int Field, int Area, string Volume), string> WayMarks = new Dictionary<(int, int, string), string>
        {
            [(31, 5, "01eve_07")] = "to elevator for next floor",
        };

        /// <summary>Event volumes the last Build left out because their script has nothing to do now (for the log).</summary>
        internal static readonly List<string> Skipped = new List<string>();
        // Larger place polygons are whole rooms, not door pads. Ginza's door pads are 800 x 400 (field 19, 4 Oct 2026: the Terminal,
        // Rag's Jewelry and the Cathedral lost their names and were taken for plain doors, "explored"); Shibuya's were under 700.
        private const float PadMaxSize = 1000f;

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
            string here = Menus.AnnouncementReader.LocationText;   // a learned destination equal to this banner says nothing new
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            Il2CppReferenceArray<putevebox_s> boxes = fldEveHit.gEvnRngBox;
            Il2CppReferenceArray<fldNpc_tag> npcs = fld_Npc.fldNpc;
            int n = ranges == null ? 0 : Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            int npcCount = npcs == null ? 0 : Math.Min(fld_Npc.fldNpcNum, npcs.Length);
            List<CollisionPoly> pads = PlacePads();
            Skipped.Clear();
            for (int i = 0; i < n; i++)
            {
                fldResHead_t h = ranges[i];
                if (h == null || h.sisei == null)
                    continue;
                string name = h.resourceName ?? "";
                putevebox_s box = boxes != null && i < boxes.Length ? boxes[i] : null;
                PoiKind? kind = null;
                Vector4 p = h.sisei.pos;
                string placeName = h.resourceType == 10 ? PlaceName(gb.fieldID, pads, p.x, p.y, p.z, EventName.IsMatch(name) ? EventOnPad : PadReach) : "";
                bool soul = false;
                bool demon = false;
                int takara = box != null && box.TakaraIdx >= 0 ? box.TakaraIdx : -1;
                if (h.resourceType == 10 && DoorName.IsMatch(name))
                    kind = PoiKind.Door;
                else if (h.resourceType == 10 && EventName.IsMatch(name) && placeName.Length > 0)
                    kind = PoiKind.Place;   // the basement elevator; dropped below when a real door already has the name
                else if (h.resourceType == 10 && EventName.IsMatch(name) && !(world && (RoomSized(box) || box != null && box.sizez == 0f)))
                    kind = PoiKind.Event;   // a story volume: Loki and his room's door in Nyx's Lounge (01eve_01 "Talk", 01eve_03 "Open Door"), 0.0.53
                // The overworld's flat event planes are crossed, not gone to (field 3's 01eve_07 to 14 set the encounter table,
                // fldCommand_FLD_SET_ENCOUNT): every one in the dumps was left out before 0.0.100 (as "room-sized", by the padded
                // corners) and stays out.
                else if (box != null && box.TakaraIdx >= 0)
                    kind = PoiKind.ItemBox;
                else if (box != null && box.npcIdx >= 0 && box.npcIdx < npcCount && npcs[box.npcIdx] != null)
                {
                    fldNpc_tag npc = npcs[box.npcIdx];
                    GameObject obj = npc.fldObj;
                    if (!Shown(npc))
                        continue;   // a hidden NPC the story has not placed yet (basement npc_hoji_01, 0.0.24 test)
                    if (NpcSwitchedOff(gb, npc))
                    {
                        Skipped.Add($"{name} gone: the game's NPC switch for held object {npc.hojiID} is off");
                        continue;
                    }
                    kind = npc.Type == 999 ? PoiKind.ItemBox : PoiKind.Person;
                    soul = obj != null && (obj.name ?? "").StartsWith("shinentai", StringComparison.Ordinal);
                    demon = obj == null;
                    if (npc.Type == 999)
                        takara = npc.ItemBoxIdx;
                }
                if (kind == null)
                {
                    if (h.resourceType == 10 && EventName.IsMatch(name))
                        Skipped.Add(RoomSized(box) ? $"{name} room-sized, an area trigger on the overworld" : $"{name} a flat plane on the overworld");   // said since 0.0.100: the 52F tiles went silently
                    continue;
                }
                // A room-sized event volume is an area trigger unless its script is an action (0.0.105, below): the Obelisk 99F's
                // lift 01eve_09 ("Ride", to the next floor) is a platform 2000 across and was left out as a trigger; in every dump
                // it is the only room-sized volume whose script is an action (one other is a story trigger).
                bool roomSized = kind == PoiKind.Event && RoomSized(box);
                // What the volume's own script does now (FieldScript, 0.0.71; Francesco, 12 Oct 2026: "Event" was too vague,
                // switches and jump-offs are actions): an action is spoken with the game's prompt and narration, a story event
                // as such, and one with nothing to do now (played already, or off in the game's on/off table) is left out.
                ScriptVerdict script = null;
                if (kind == PoiKind.Event && !world)
                {
                    if (!FieldScript.VolumeOn(gb.fieldID, gb.areaID, name))
                    {
                        Skipped.Add($"{name} off in the game's on/off table");
                        continue;
                    }
                    script = FieldScript.Classify(gb.areaID, name);
                    if (roomSized && script.Outcome != ScriptOutcome.Action)
                    {
                        Skipped.Add($"{name} room-sized, an area trigger ({script})");
                        continue;
                    }
                    // No procedure for it in the field's script: the game has nothing to run there (0.0.78; Francesco, 14 Oct 2026:
                    // Talk prompts in the Mantra Headquarters prison that did nothing). 01eve_04 of field 24 areas 13 and 14 was kept
                    // as "Event, Talk" from a prompt learned while a prisoner stood beside it; the only such volumes in 92 dumps.
                    // Unless the game's door table lists it: then it is a door with an event's name (0.0.87; Francesco, 16 Oct 2026:
                    // the Ikebukuro Tunnel's stairs were missing). Field 26 area 0's 01eve_01 (movetype 2, walked into, to area 1,
                    // the "Platform"), area 1's 01eve_01 / 02 / 03 (to areas 0, 18, 19). The prison phantoms are not in the table.
                    if (script.Outcome == ScriptOutcome.Missing && script.Reason == "no such procedure"
                        && DoorTable.MoveTypeOf(gb.fieldID, gb.areaID, name) >= 0)
                    {
                        kind = PoiKind.Door;
                        script = null;
                    }
                    else if (script.Outcome == ScriptOutcome.Missing && script.Reason == "no such procedure")
                    {
                        Skipped.Add($"{name} {script}");
                        continue;
                    }
                    // A volume whose prompt the game has shown before stays listed (the old "Event, Examine") even when the dry
                    // run finds nothing to do: a safety net while the dry run is new.
                    if (script != null && script.Outcome == ScriptOutcome.Inactive && ExploredDoors.PromptOf(gb.fieldID, gb.areaID, name).Length > 0)
                        script = new ScriptVerdict { Outcome = ScriptOutcome.Unknown, Procedure = script.Procedure, Reason = "idle by the dry run, but its prompt was seen: " + script.Reason };
                    if (script != null && script.Outcome == ScriptOutcome.Inactive)
                    {
                        Skipped.Add($"{name} {script}");
                        continue;
                    }
                }
                // A door the game jumps down through (door table movetype 4: Assembly of Nihilo B12's 01d_03, 01d_05, 01d_06 land
                // on B13; the game's prompt "Jump Off", 11 Oct 2026, 21:55:14): an action, not a room (Francesco, 12 Oct 2026).
                bool jump = kind == PoiKind.Door && !world && DoorTable.MoveTypeOf(gb.fieldID, gb.areaID, name) == JumpMoveType;
                bool awayWarp = kind == PoiKind.Door && !world && WarpMap.IsAwayWarp(gb, i);
                bool generic = kind == PoiKind.Door && placeName.Length == 0 && !world;   // world map entrances lead to places, not rooms
                int toField = kind == PoiKind.Door && !world ? DoorTable.ToField(gb.fieldID, gb.areaID, name) : -1;
                bool toWorld = toField > 0 && FieldState.WorldMapField(toField);

                VolumeSpan(box, gb.playerY, out float floorY, out float nx, out float nz, out float[] otherFloors);
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
                // Where a plain door leads, and whether it is an escalator (0.0.78).
                string destination = "", behind = "";
                bool destinationIsField = false, escalator = false;
                int escalatorDir = 0;
                if (generic && !toWorld && !jump && !awayWarp)
                {
                    if (toField >= 0 && toField != gb.fieldID)
                        destinationIsField = (destination = WhereAmI.FieldName(toField)).Length > 0;
                    if (destination.Length == 0)
                    {
                        string learned = ExploredDoors.DestinationOf(gb.fieldID, gb.areaID, name);
                        if (!learned.Equals(here, StringComparison.OrdinalIgnoreCase))
                            destination = learned;
                    }
                    escalator = EscalatorAt(gb, name, p.x, p.z, float.IsNaN(floorY) ? p.y + 100f : floorY, out escalatorDir);
                    behind = ExploredDoors.BehindLine(gb.fieldID, gb.areaID, name);
                }
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
                    OtherFloors = world ? Array.Empty<float>() : otherFloors,
                    NX = doorLike ? nx : 0f,
                    NZ = doorLike ? nz : 0f,
                    Distance = (float)Math.Sqrt((p.x - px) * (p.x - px) + (p.z - pz) * (p.z - pz)),
                    Explored = generic && !toWorld && ExploredDoors.IsExplored(gb.fieldID, gb.areaID, name),   // an exit to the overworld is used again and again: never "explored" (Francesco, 0.0.45)
                    Warp = kind == PoiKind.Door && !world && WarpMap.TouchWarp(gb, i) != null,
                    AwayWarp = awayWarp,
                    Empty = kind == PoiKind.ItemBox && Opened(takara),
                    World = world,
                    ToWorld = toWorld,
                    HasBox = hasBox,
                    Big = roomSized,
                    BoxX0 = bx0,
                    BoxX1 = bx1,
                    BoxZ0 = bz0,
                    BoxZ1 = bz1,
                    DoorKind = generic && !toWorld && !jump && !awayWarp ? DoorTable.KindOf(gb.fieldID, gb.areaID, name) : "",
                    Destination = destination,
                    DestinationIsField = destinationIsField,
                    Behind = behind,
                    Escalator = escalator,
                    EscalatorDir = escalatorDir,
                    Jump = jump,
                    Action = script?.Outcome == ScriptOutcome.Action || jump,
                    Story = script?.Outcome == ScriptOutcome.Story,
                    Prompt = (jump ? FieldScript.PanelText(JumpPrompt) : script?.Outcome == ScriptOutcome.Action ? script.PromptText : "")
                             + (WayMarks.TryGetValue((gb.fieldID, gb.areaID, name), out string way) ? " " + way : ""),   // "Ride to elevator for next floor"
                    Detail = script?.Outcome == ScriptOutcome.Action ? script.Detail
                             : script?.Outcome == ScriptOutcome.Story ? FieldScript.FirstNarration(gb.areaID, name) : "",   // 0.0.97: the tile's narration ("Time passed quickly")
                    ScriptNote = script?.ToString() ?? "",
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
                    same.Aliases.Add(q.ResName);
                    same.Explored |= q.Explored;   // either volume of a door listed twice may be the one gone through
                    same.Empty |= q.Empty;
                    same.ToWorld |= q.ToWorld;
                    if (same.DoorKind.Length == 0)
                        same.DoorKind = q.DoorKind;
                    if (same.Destination.Length == 0)
                    {
                        same.Destination = q.Destination;
                        same.DestinationIsField = q.DestinationIsField;
                    }
                    if (!same.Escalator && q.Escalator)
                    {
                        same.Escalator = true;
                        same.EscalatorDir = q.EscalatorDir;
                    }
                    if (same.ToWorld)
                    {
                        same.DoorKind = "";   // "Exit to overworld" already says it
                        same.Explored = false;
                    }
                    continue;
                }
                // A warp's own volume and margin are kept out of the grid: WarpMap.RouteTo below decides for it.
                if (grid && q.AwayWarp)
                    // Its volume and AvoidMargin round it are kept out of the grid (0.0.88): the ring just outside counts.
                    q.Reachable = q.HasBox && WalkGrid.ReachableInRect(px, py, pz, q.BoxX0 - AwayWarpRing, q.BoxZ0 - AwayWarpRing,
                                                                       q.BoxX1 + AwayWarpRing, q.BoxZ1 + AwayWarpRing, q.FloorY);
                else if (grid)
                {
                    // A door-like volume: a reachable cell inside its footprint (plus margin) at its floor decides first. When none
                    // is, another floor inside the volume's height span may be the one walked on to it, and becomes the target
                    // (0.0.94): the Assembly of Nihilo lays one floor sheet at y 0 under whole areas, and doors at the top of ramps
                    // were "beyond doors" (area 24, the Terminal door) or listed but "No path." (area 13, door 01d_03, 16:41:46:
                    // the nearest cell at y 0 was 175 short, beside the ramp). Only then the radius test round the point.
                    bool inRect(float y) => WalkGrid.ReachableInRect(px, py, pz, q.BoxX0 - Poi.VolumeMargin, q.BoxZ0 - Poi.VolumeMargin,
                                                                    q.BoxX1 + Poi.VolumeMargin, q.BoxZ1 + Poi.VolumeMargin, y);
                    bool boxed = (q.IsDoorLike || q.Big) && q.HasBox;
                    q.Reachable = boxed && inRect(q.FloorY);
                    if (!q.Reachable && boxed && !q.Warp)
                        foreach (float alt in q.OtherFloors)
                            if (inRect(alt))
                            {
                                Log.Info($"[Nav:Poi] {q.ResName}: reachable on its floor at y {alt:0} (not at y {q.FloorY:0}).");
                                q.FloorY = alt;
                                q.Reachable = true;
                                break;
                            }
                    if (!q.Reachable)
                        q.Reachable = !q.Warp && WalkGrid.ReachableNear(px, py, pz, q.X, q.FloorY, q.Z, q.ReachRadius);
                }
                kept.Add(q);
            }
            if (!world)
                MarkLadders(gb, kept);
            // Beyond warps (0.0.52): a point no walk reaches may be reached by taking warps (Francesco, 4 Oct 2026: parts of the Amala
            // Network were missing from the list until he stood in them). From 0.0.55 ladders and same-area doors too (the Ginza
            // underpass, 5 Oct 2026: its rooms in one area are joined by ladders, and everything past one was left out).
            if (grid && !world && WarpMap.All(gb.fieldID, gb.areaID).Any(w => w.HasDest))
                foreach (Poi q in kept.Where(q => !q.Reachable))
                {
                    List<Warp> legs = WarpMap.RouteTo(gb, q);
                    if (legs != null && legs.Count > 0 && !q.Landmark)
                        q.HeldLegs = legs;   // a "to exit" door is known only below
                    else if (legs != null)   // through door pairs or warps: landmarks only (0.0.90)
                    {
                        q.Reachable = true;
                        q.ViaWarps = legs.Count;   // 0: walked to (a warp next to the player's part of the area)
                        q.ViaLegs = WarpMap.Phrase(legs);
                    }
                }
            // Beyond other areas (0.0.56, F7): a part of this area joined to the player's only through other areas, by doors the
            // player has gone through (Ginza underpass area 4, 5 Oct 2026). Also learns this area's door links for later routes.
            if (grid && !world)
                AreaRoutes.Mark(gb, kept);
            // Dungeons only: on the world map unreachable entrances stay left out (Francesco, 16 Oct 2026: listing them would fill
            // every overworld zone with places out of reach; 0.0.85 tried it and was reverted in 0.0.86).
            if (!world)
                MarkLaddersAndExits(gb, kept);
            // Exits and doors to exits are landmarks too (Francesco, 17 Oct 2026, 0.0.91): "to exit" is known only now.
            foreach (Poi q in kept.Where(q => !world && !q.Reachable && !q.Redundant && q.Landmark))
            {
                if (q.HeldLegs != null)
                {
                    q.Reachable = true;
                    q.ViaWarps = q.HeldLegs.Count;
                    q.ViaLegs = WarpMap.Phrase(q.HeldLegs);
                }
                else
                    q.Beyond = true;
            }
            if (world)
                MarkSharedEntrances(gb, list);
            foreach (Poi q in kept)
                q.Category = CategoryOf(q, gb);
            return kept.OrderBy(q => q.Distance).ToList();
        }

        /// <summary>
        /// Francesco, 6 Oct 2026 (after Matador):
        /// - ladders are their own kind, said "Ladder up" or "Ladder down" from where their landing lies; the far end of a ladder
        ///   the player can walk to is left out ("Ladder door, here, downstairs, via 1 ladder" was the top of the ladder in front);
        /// - Terminal doors, stairs and other exits, and every named door, are always listed while on this map, even when no way
        ///   there is known yet (Ginza's stairs to the overworld and the underpass Terminal were missing): ", beyond doors";
        /// - a door into a room whose only way on is an exit says so: "Door, to exit".
        /// </summary>
        /// <summary>Ladders: their own kind, up or down (0.0.59); marked before the landmark rule, which leaves ladders out (0.0.90).</summary>
        private static void MarkLadders(fldGlobalWork_t gb, List<Poi> kept)
        {
            foreach (Poi q in kept.Where(q => q.Kind == PoiKind.Door))
            {
                // A ladder into another area is not in WarpMap (same-area moves only): the door table's movetype says ladder
                // (0.0.74; the cube hall's 01d_04, 12 Oct 2026, 21:28: listed as "Door, passage", left out while not reachable,
                // so Francesco did not find it in front of him). Its far end is in the other area: no up or down.
                Warp w = WarpMap.At(gb, q.Index);
                bool ladder = w != null ? w.Ladder : DoorTable.MoveTypeOf(gb.fieldID, gb.areaID, q.ResName) == LadderMoveType;
                if (!ladder)
                    continue;
                q.Ladder = true;
                int way = DoorTable.LadderWay(gb.fieldID, gb.areaID, q.ResName);   // the game's own (union03), any destination
                q.LadderDir = way != 0 ? way : WarpMap.LadderDirection(gb, q.Index);
                q.DoorKind = "";
                q.Explored = false;
            }
        }

        private static void MarkLaddersAndExits(fldGlobalWork_t gb, List<Poi> kept)
        {
            // The far side of a same-area door is the same door (0.0.62): Ginza area 1, 8 Oct 2026, 21:04:00, through 01d_04 into the
            // exit corridor, the Doors list held 01d_05 (2 metres, the door just come through) and 01d_04 "via 1 door" (its other
            // side, in the hall). Left out like a ladder's far end while its own side is walked to.
            foreach (Poi q in kept.Where(q => q.Kind == PoiKind.Door && q.Reachable && q.ViaWarps > 0
                                              && WarpMap.At(gb, q.Index) is Warp w && !w.Touch && w.Partner != null))
            {
                Warp partner = WarpMap.At(gb, q.Index)?.Partner;
                if (partner != null && kept.Any(o => o.Index == partner.Index && o.Reachable && o.ViaWarps == 0 && o.Route == null))
                {
                    q.Reachable = false;
                    q.Redundant = true;
                }
            }
            foreach (Poi q in kept.Where(q => !q.Reachable && !q.Redundant && q.Landmark))
                q.Beyond = true;

            // Same-area doors that land in a room holding an exit the player cannot otherwise walk to (Ginza area 2, 01d_08).
            List<Poi> exits = kept.Where(q => (q.ToWorld || q.DoorKind == "exit") && (q.ViaWarps > 0 || q.Beyond)).ToList();
            if (exits.Count == 0)
                return;
            foreach (Poi q in kept.Where(q => q.Kind == PoiKind.Door && !q.Warp && !q.Ladder && !q.ToWorld && q.DoorKind != "exit"))
            {
                Warp w = WarpMap.At(gb, q.Index);
                if (w == null || !w.HasDest)
                    continue;
                int landing = WalkGrid.RegionAt(w.DestX, w.DestY, w.DestZ);
                if (landing >= 0 && exits.Any(e => WarpMap.TargetRegions(gb, e).Contains(landing)))
                {
                    q.DoorKind = "to exit";
                    q.Explored = false;
                }
            }
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
                    return q.Action ? PoiCategory.Actions : PoiCategory.Events;
            }
            if (q.Jump && q.Prompt.Length > 0)
                return PoiCategory.Actions;
            if (q.Warp || q.AwayWarp)
                return PoiCategory.Warps;
            if (q.Ladder)
                return PoiCategory.Ladders;
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

        private static readonly string[] CategoryNames = { "All", "Doors", "Rooms", "Exits", "Warps", "Ladders", "Souls", "Demons", "Item boxes", "Actions", "Events" };

        /// <summary>
        /// Whether an NPC is in the world: its object (fldNpc_tag.fldObj) is active. The basement keeps npc_hoji_01 (hojiID
        /// 1, a Soul model) with its object inactive: the game never fires its hit, its collision box (Attr 0x80) does not stop
        /// the player, and walking to it ended in a spin (0.0.24 test).
        /// An NPC with no object at all is shown: the talking demons of the annex (14:07 and 14:30 sessions, 2 Oct 2026:
        /// "01npc_01" in area 12, "01npc_02" in area 13, "02npc_01"; tType 1, tCamera 2, fldObj null) were left out as hidden,
        /// while the game spoke them as "Demon".
        /// </summary>
        /// <summary>
        /// A held-object NPC (hojiID above 0) the game has switched off: fldGlobal.fldGbChkNpc(field, area + 1, hojiID) not 0,
        /// as for its collision (CollisionCache). The Mantra Headquarters prison (field 24 area 14, dump 14 Oct 2026, 15:47:32):
        /// the area's start procedure removes the freed prisoners npc_hoji_01 to 03 (no model, collision size 0, "NPCs off [1 2
        /// 3]"), but their NPC records stay and were listed as "Demon"; walks to them ended "Near the Demon." (0.0.78). In 92
        /// dumps no NPC with its object shown is switched off.
        /// </summary>
        private static bool NpcSwitchedOff(fldGlobalWork_t gb, fldNpc_tag npc)
        {
            try
            {
                return npc.hojiID > 0 && fldGlobal.fldGbChkNpc(gb.fieldID, gb.areaID + 1, npc.hojiID) != 0;
            }
            catch
            {
                return false;
            }
        }

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
        private static void VolumeSpan(putevebox_s box, float playerY, out float floorY, out float nx, out float nz, out float[] otherFloors)
        {
            floorY = float.NaN;
            otherFloors = Array.Empty<float>();
            FieldData.BoxFacing(box, out nx, out nz);
            if (box == null)
                return;
            Il2CppStructArray<Vector4> vtx = box.vtx;
            int corners = FieldData.BoxCorners(box);
            if (corners == 0)
                return;
            float lo = float.MaxValue, hi = float.MinValue, spread = 0f, cx = 0f, cz = 0f;
            for (int k = 0; k < corners; k++)
            {
                Vector4 v = vtx[k];
                lo = Math.Min(lo, v.y);
                hi = Math.Max(hi, v.y);
                spread += Math.Abs(v.x) + Math.Abs(v.z);
                cx += v.x / corners;
                cz += v.z / corners;
            }
            if (spread < 1f)
                return;   // no box (size 0 0 0)
            // Y points down: hi is the bottom of the box. Where the volume has a floor at the player's own height, the game's hit
            // can fire where the player stands: that height. Otherwise the volume's own floor, the one nearest the middle of its
            // span; else the bottom (the stairwell's 1F door seen from 2F must stay downstairs). 0.0.74: the player's height
            // counted whenever it lay within 50 of the span, from anywhere: in the Nihilo corridor (field 20 area 29, 12 Oct
            // 21:12:00) the player at y -2, 33 metres away, made the White Treasure Room door's floor -2, nearer the base floor
            // (y 50) under the raised corridor than the corridor itself (y -150): "no known way there yet".
            bool InSpan(float y) => y >= lo - 50f && y <= hi + 50f;
            if (InSpan(playerY) && CollisionCache.FloorUnder(cx, playerY, cz, out float atPlayer, groundOnly: true, upOnly: true) != null
                && Math.Abs(atPlayer - playerY) <= PlayerOnFloor)
                floorY = playerY;
            else if (CollisionCache.FloorUnder(cx, (lo + hi) / 2f, cz, out float own, groundOnly: true, upOnly: true) != null && InSpan(own))
                floorY = own;
            else
                floorY = hi;
            // 0.0.94: every other floor under the volume within its span, tried when the chosen one is not reachable. The
            // Assembly of Nihilo's Marunouchi Entrance (field 20 area 24, dump 19 Oct 17:03:49) lies on one floor sheet at y 0
            // under the whole area (#0); its Terminal door 01d_03 stands at the top of a ramp (#2, about 30 up). From the
            // ring at y 0 the sheet under the door was chosen, whose cells beside the ramp are walled in: "beyond doors".
            float chosen = floorY;
            otherFloors = CollisionCache.FloorsUnder(cx, cz, groundOnly: true, upOnly: true)
                .Where(y => InSpan(y) && Math.Abs(y - chosen) > 20f).Distinct().ToArray();
        }

        /// <summary>
        /// Asakusa's overworld (field 5) has fifteen entrances the game names alike ("Asakusa entrance"), in separate pieces of a
        /// spiral round Mifunashiro (Francesco, 19 Oct 2026: the game's own design). Each gets its direction and distance from
        /// the Mifunashiro entrance (Francesco chose this, 19 Oct): "Asakusa entrance, 10 metres south of Mifunashiro". Where
        /// two would say the same (01d_03 and 01d_04, both 10 metres south), they take the 16-point direction
        /// ("south-south-west"). The anchor is the entrance whose door leads to Mifunashiro's field; its name is the game's.
        /// </summary>
        private static void MarkSharedEntrances(fldGlobalWork_t gb, List<Poi> all)
        {
            Poi anchor = all.FirstOrDefault(q => q.Kind == PoiKind.Door && q.Name.Length > 0
                                                 && DoorTable.ToField(gb.fieldID, gb.areaID, q.ResName) == MifunashiroField);
            if (anchor == null)
                return;
            List<Poi> shared = all.Where(q => q.Kind == PoiKind.Door && q != anchor && q.Name.Length > 0)
                .GroupBy(q => q.Name).Where(g => g.Count() > 2).SelectMany(g => g).ToList();
            string Words(Poi q, bool fine)
            {
                float bearing = Angles.Bearing(anchor.X, anchor.Z, q.X, q.Z);
                string dir = fine ? Fine16[(int)Math.Round(Angles.Mod360(bearing) / 22.5) % 16] : Angles.CompassWord(bearing);
                double metres = Math.Round(Math.Sqrt((q.X - anchor.X) * (q.X - anchor.X) + (q.Z - anchor.Z) * (q.Z - anchor.Z)) / 100.0);
                return $"{metres} {(metres == 1 ? "metre" : "metres")} {dir} of {anchor.Name}";
            }
            foreach (Poi q in shared)
                q.Mark = Words(q, false);
            foreach (var same in shared.GroupBy(q => q.Name + "|" + q.Mark).Where(g => g.Count() > 1))
                foreach (Poi q in same)
                    q.Mark = Words(q, true);
        }

        private const int MifunashiroField = 35;   // door table: Asakusa overworld 01d_19 leads to field 35 (dump 19 Oct 2026, 16:10:50)
        private static readonly string[] Fine16 = { "north", "north-north-east", "north-east", "east-north-east", "east", "east-south-east",
            "south-east", "south-south-east", "south", "south-south-west", "south-west", "west-south-west", "west", "west-north-west",
            "north-west", "north-north-west" };

        private const float PlayerOnFloor = 10f;   // the player stands about 2 above the floor (y -2 on the floor at 0)
        private const float AwayWarpRing = 90f;    // WalkGrid.AvoidMargin 45 plus a cell and a half: the walkable ring round a kept-out warp

        /// <summary>A story volume as big as a room (more than 1500 across): a trigger for walking into an area, not a thing to go to.</summary>
        private static bool RoomSized(putevebox_s box)
            => Footprint(box, out float x0, out float x1, out float z0, out float z1) && (x1 - x0 > 1500f || z1 - z0 > 1500f);

        /// <summary>A volume's box footprint in X and Z (gEvnRngBox.vtx); false without a box (all zero for people).</summary>
        private static bool Footprint(putevebox_s box, out float x0, out float x1, out float z0, out float z1)
        {
            x0 = z0 = float.MaxValue;
            x1 = z1 = float.MinValue;
            Il2CppStructArray<Vector4> vtx = box?.vtx;
            int corners = FieldData.BoxCorners(box);
            if (corners == 0)
                return false;
            for (int k = 0; k < corners; k++)
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
            for (int k = 0; k < FieldData.BoxCorners(box); k++)
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

        /// <summary>
        /// An escalator (0.0.78): a door volume walked into (door table movetype 2) that leads to another area of this field, with
        /// a run of at least EscalatorSteps floor polygons within EscalatorReach of it that are shallow (20 to 50 deep, 80 or more
        /// wide) and steep (median rise over median depth 0.5 or more). Ikebukuro's East Hall escalators (dumps 14 Oct 2026,
        /// 16:34:13 and 16:33:50): steps 36 to 39 deep rising 25 to 28 (0.7), 155 or 670 wide, beside 01d_08 / 01d_09 (B1) and
        /// 01d_02 / 01d_03 (1F). Shibuya's walk-in stairs (field 17, dump 3 Oct 20:49:51) are 54.5 deep rising 18 (0.33), the
        /// Amala Network's walk-in doors have no steps: neither is one. dir: +1 the steps rise from the door (up), -1 they fall.
        /// </summary>
        private static bool EscalatorAt(fldGlobalWork_t gb, string name, float x, float z, float floorY, out int dir)
        {
            dir = 0;
            string key = $"{gb.fieldID}/{gb.areaID}/{name}/{CollisionCache.Version}";
            if (EscalatorCache.TryGetValue(key, out int cached))
            {
                dir = cached;
                return cached != int.MinValue;
            }
            bool found = false;
            try
            {
                if (DoorTable.MoveTypeOf(gb.fieldID, gb.areaID, name) == WalkInMoveType
                    && DoorTable.Leads(gb.fieldID, gb.areaID, name, out int toField, out int toArea)
                    && toField == gb.fieldID && toArea != gb.areaID)
                {
                    var heights = new List<float>();
                    var depths = new List<float>();
                    var near = new List<(float D, float Y)>();   // every step's distance from the door, for the direction
                    foreach (CollisionPoly c in CollisionCache.Polys)
                    {
                        if (!c.IsFloorLike || !c.IsGroundAttr || c.Points.Length < 3)
                            continue;
                        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue, y = 0f;
                        foreach (Vector4 v in c.Points)
                        {
                            x0 = Math.Min(x0, v.x); x1 = Math.Max(x1, v.x);
                            z0 = Math.Min(z0, v.z); z1 = Math.Max(z1, v.z);
                            y += v.y;
                        }
                        float depth = Math.Min(x1 - x0, z1 - z0), width = Math.Max(x1 - x0, z1 - z0);
                        float cx = (x0 + x1) / 2 - x, cz = (z0 + z1) / 2 - z;
                        if (depth < 20f || depth > 50f || width < 80f || cx * cx + cz * cz > EscalatorReach * EscalatorReach)
                            continue;
                        y /= c.Points.Length;
                        near.Add((cx * cx + cz * cz, y));
                        if (heights.Any(h => Math.Abs(h - y) < 3f))
                            continue;   // one height per step (two escalators side by side)
                        heights.Add(y);
                        depths.Add(depth);
                    }
                    if (heights.Count >= EscalatorSteps)
                    {
                        heights.Sort();
                        var rises = new List<float>();
                        for (int k = 1; k < heights.Count; k++)
                            rises.Add(heights[k] - heights[k - 1]);
                        rises.Sort();
                        depths.Sort();
                        float rise = rises[rises.Count / 2], depth = depths[depths.Count / 2];
                        found = rise >= 15f && rise <= 45f && rise / depth >= 0.5f;
                        if (found)
                        {
                            float mean = heights.Average();   // Y points down: steps above the door's floor go up
                            dir = mean < floorY - 20f ? 1 : mean > floorY + 20f ? -1 : 0;
                            if (dir == 0)
                            {
                                // Stairs up and down within reach of the door cancel out (West Hall B1, field 23 area 5, 14 Oct 2026, 20:06:38:
                                // 32 steps, no direction): the steps nearest the door are its own escalator's (0.0.79).
                                float nearMean = near.OrderBy(t => t.D).Take(EscalatorSteps).Average(t => t.Y);
                                dir = nearMean < floorY - 20f ? 1 : nearMean > floorY + 20f ? -1 : 0;
                            }
                        }
                        Log.Info($"[Nav:Poi] {gb.fieldID}/{gb.areaID}/{name}: walk-in door to area {toArea}, {heights.Count} steps, rise {FieldData.F(rise)}, depth {FieldData.F(depth)}: " +
                                 (found ? $"an escalator {(dir > 0 ? "up" : dir < 0 ? "down" : "")}" : "not an escalator") + ".");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav:Poi] Escalator test of {name}: {ex.Message}");
            }
            EscalatorCache[key] = found ? dir : int.MinValue;
            return found;
        }

        private const int WalkInMoveType = 2, EscalatorSteps = 6;
        private const float EscalatorReach = 700f;
        private static readonly Dictionary<string, int> EscalatorCache = new Dictionary<string, int>();

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

        /// <summary>The game's name of the nearest place pad within reach of (x, z) (PadReach for doors, EventOnPad for events) on the door's floor (a door volume
        /// centre is about 100 above its floor; the stairwell has doors above each other), or "".</summary>
        private static string PlaceName(int field, List<CollisionPoly> pads, float x, float y, float z, float reach)
        {
            CollisionPoly best = null;
            float bestD = reach;
            foreach (CollisionPoly c in pads)
            {
                // Within 150 of the span from the volume's centre to 100 under it (0.0.76): most door centres sit 100 above their
                // floor, some on it. Nihilo B14's elevator door 01d_06 (centre y 0 on the floor, its "Elevator" pad at y -60) was
                // 160 off the old single height and stayed a plain "Door" (13 Oct 2026, dump 16:19:45).
                float py = c.Points[0].y, off = py < y ? y - py : py > y + 100f ? py - (y + 100f) : 0f;
                if (off > 150f)
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
                         string.Join("; ", all.Select(q => $"{q.Index} {q.Spoken} [{q.Category}] {q.ResName} at {FieldData.F(q.X)} {FieldData.F(q.Z)} d {FieldData.F(q.Distance)}" +
                                                            $"{(q.ScriptNote.Length > 0 ? ", script " + q.ScriptNote : "")}{(q.Listed ? q.Via : " (not reachable, left out)")}")) +
                         (Skipped.Count > 0 ? "; left out by their scripts: " + string.Join("; ", Skipped) : ""));
            // The Obelisk's time puzzle (0.0.98, Francesco): while a sphere's effect is on, only the next tile, whatever the filter.
            TimePuzzle.Step? step = TimePuzzle.Next(gb);
            string[] options = step.HasValue ? step.Value.Volume.Split('|') : Array.Empty<string>();
            Poi next = all.Where(q => options.Contains(q.ResName)).OrderBy(q => q.Distance).FirstOrDefault();   // "a|b": the nearer exit
            if (next != null)
            {
                next.NextStep = true;
                next.StepAnswer = step.Value.Answer;
                if (log)
                    Log.Info($"[Nav:Puzzle] Pages list: only the next step, {next.ResName}{(next.StepAnswer.Length > 0 ? ", answer " + next.StepAnswer : "")}.");
                return new List<Poi> { next };
            }
            if (step.HasValue && log)
                Log.Info($"[Nav:Puzzle] The next step, {step.Value.Volume}, is not among this area's points: the full list stays.");
            IEnumerable<Poi> list = all.Where(q => q.Listed && (_filter == PoiCategory.All || q.Category == _filter));
            if (Config.PoiMapOrder?.Value ?? true)
                list = list.OrderByDescending(q => (int)Math.Floor(q.Z / MapBand)).ThenBy(q => q.X).ThenBy(q => q.Index);
            return list.ToList();
        }

        private const float MapBand = 300f;
        private static PoiCategory _filter = PoiCategory.All;

        /// <summary>
        /// A new area clears the selection and puts the filter back to All, returning true when a filter was on (Francesco, 5 Oct
        /// 2026: the Ginza stairs to the overworld and Harumi's ladder seemed missing; they were listed, under Exits and Rooms,
        /// while the Doors filter chosen in an earlier area was still on).
        /// </summary>
        private static bool NewArea(fldGlobalWork_t gb)
        {
            if (gb.fieldID == _field && gb.areaID == _area)
                return false;
            _field = gb.fieldID;
            _area = gb.areaID;
            _selected = -1;
            Selected = null;
            bool wasFiltered = _filter != PoiCategory.All;
            _filter = PoiCategory.All;
            return wasFiltered;
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
            string reset = NewArea(gb) ? "All. " : "";

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
            SpeechManager.Say($"{reset}{poi.Spoken}, {Describe(gb.playerX, gb.playerY, gb.playerZ, poi)}{poi.Via}. {at + 1} of {list.Count}.", interrupt: true);
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
            List<Poi> reachable = Build(gb).Where(q => q.Listed).ToList();
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

        /// <summary>Selects a point (AutoWalk, the next door of a route through other areas), so Insert, End and Home use it.</summary>
        internal static void Select(fldGlobalWork_t gb, Poi poi)
        {
            NewArea(gb);
            _selected = poi.Index;
            Selected = poi;
        }

        /// <summary>The selected point of interest, re-read from the game's list (null if none or the area changed).</summary>
        internal static Poi Current(fldGlobalWork_t gb)
        {
            if (gb == null || gb.fieldID != _field || gb.areaID != _area || _selected < 0)
                return null;
            Poi poi = Build(gb).FirstOrDefault(q => q.Index == _selected && q.Listed);
            // The puzzle's next step keeps its mark here too (0.0.104): Home says "Walking to next step: ...", as Page Up does.
            TimePuzzle.Step? step = TimePuzzle.Next(gb);
            if (poi != null && step.HasValue && poi.Names.Any(n => step.Value.Volume.Split('|').Contains(n)))
            {
                poi.NextStep = true;
                poi.StepAnswer = step.Value.Answer;
            }
            return poi;
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
                text += $", {Angles.CompassWord((float)bearing)}";
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
