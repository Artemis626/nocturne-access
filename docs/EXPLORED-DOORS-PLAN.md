# Plan: explored doors (and, later, custom waypoints)

Status: **requested by Francesco on 2 Oct 2026, not yet implemented.** Items 1 and 2 are the next implementation; item 3 is recorded for later and must not be built yet. Revert point: the commit that added this file (0.0.34).

## Why (Francesco, 2 Oct 2026)

Nocturne is a notoriously hard and dispersive game: even sighted players get lost. For a blind or low-vision player, pathfinding to the nearest point of interest is not enough to build a mental map of identical sets of doors and corridors (the annex: many rooms of the same shape, each behind a plain "Door"). The mod has to remember where the player has been.

## 1. "Explored" for generic doors (next)

- **Which doors:** only the generic ones, the points of interest spoken as "Door" (gEvnRng type 10 named `NNd_MM` with no place name from the game). Doors and places with the game's own precise name are left out, because the name already tells them apart: "Terminal door", "Healing Room door", "Elevator", and any other door that gets a `FldNameData.GetBasyoName` name (`PointsOfInterest`, `Poi.Name` not empty).
- **What the player hears:** the Page Up / Page Down entry says it, for example "Door, explored, 12 metres, west. 3 of 6." against "Door, 12 metres, west. 4 of 6.". The same word in the End directions and in the Home "Walking to ..." line, so the three keys agree. Wording to confirm with Francesco at the test (short, before the distance).
- **When a door counts as explored** (proposal, from the game data the mod already reads):
  - leaving through it: the player is in its volume (`fldEveHit.OldHitDoor` = its gEvnRng index, as auto-walk's arrival uses) and then the area or field changes (`fldGb.areaID` / `fieldID`, already logged as `[Nav:Change]`): that door, in the old area, is explored;
  - arriving through one: right after the change, the generic door of the new area nearest the player (within about 300 units, the player stands in front of it) is explored too, so both sides of a passage are marked.
  - Not by merely walking past or standing in a door volume without going through.
- **Identity of a door:** `fieldID`, `areaID` and the volume's `resourceName` ("01d_15"): stable per area (seen unchanged across all dumps of an area). Not the gEvnRng index alone.
- **Kept across sessions:** a small file in `UserData/NocturneAccess/` (for example `explored.txt`, one `field/area/name` per line), read at start, appended when a door is marked. Open question for Francesco: one list for everything (simple; a new game or another save would inherit marks), or one per save slot (needs the save slot being played, not yet read by the mod). Proposal: one list, plus a way to clear it later if needed.
- **Never blocks or changes input**, as every navigation cue (CLAUDE.md). Logging: `[Nav:Explored] 15/4/01d_15 marked (left through it)` so the test can be checked in the log.
- **Possible extension to propose, not to build unasked:** remember where the door led ("Door to Annex 1F", from the location banner or the area name after the change). It may help the mental map more than "explored" alone; ask Francesco first.

## 2. The purpose to keep in mind while building item 1

The flag is a first step towards a mental map: each generic door should become distinguishable over time. Keep the data (door identity, when and how it was marked, the area it led to) in a form later steps can reuse (item 3, and possibly the destination name above).

## 3. Custom waypoints (later: record only, do not implement yet)

Francesco's intended later addition: the player marks their own waypoints (a spot in an area, possibly named) that the pathfinder can track like a point of interest: listed with Page Up / Page Down, walked to with Home, directions with End. To design when Francesco asks: keys (single keys preferred, CLAUDE.md), naming (preset words or numbered "Waypoint 1"), storage (same file approach as item 1), and how they mix with the game's points in the list.

## Test (item 1, Francesco)

1. In the annex, go through a plain door and come back: Page Up on that door says "explored"; on the other side, the door you came in by says "explored" too.
2. Named doors (Terminal, Healing Room, Elevator) never say it.
3. Quit and restart, load the save: the doors stay explored.
4. Nothing else changes: list order, distances, Home and End.
