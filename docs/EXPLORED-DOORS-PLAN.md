# Plan: explored doors (and, later, custom waypoints)

Status: **items 1 to 3 implemented in 0.0.35 (2 Oct 2026); item 1 verified in game (16:11 session); item 3 right in the log (the opened Annex 1F boxes listed "Item box, empty", unopened Annex Lobby boxes plain) but not yet heard on a reachable box.** Item 4 (custom waypoints) is recorded for later and must not be built yet. Revert point: commit 4f1f035 (0.0.34).

## Why (Francesco, 2 Oct 2026)

Nocturne is a notoriously hard and dispersive game: even sighted players get lost. For a blind or low-vision player, pathfinding to the nearest point of interest is not enough to build a mental map of identical sets of doors and corridors (the annex: many rooms of the same shape, each behind a plain "Door"). The mod has to remember where the player has been.

## 1. "Explored" for generic doors (built in 0.0.35)

- **Which doors:** only the generic ones, the points of interest spoken as "Door" (gEvnRng type 10 named `NNd_MM` with no place name from the game). Doors and places with the game's own precise name are left out, because the name already tells them apart: "Terminal door", "Healing Room door", "Elevator", and any other door that gets a `FldNameData.GetBasyoName` name (`PointsOfInterest`, `Poi.Name` not empty).
- **What the player hears:** the Page Up / Page Down entry says it, for example "Door, explored, 12 metres, west. 3 of 6." against "Door, 12 metres, west. 4 of 6.". The same word in the End directions and in the Home "Walking to ..." line, so the three keys agree. Wording to confirm with Francesco at the test (short, before the distance).
- **When a door counts as explored** (built in 0.0.35 from the 14:48 session log; `ExploredDoors`):
  - leaving through it: `fldEveHit.OldHitDoor` (the door volume the player stands in) goes back to -1 between 0.2 s and 1.6 s *before* `fldGb.areaID` or `fieldID` changes, so the last door stood in counts when the area changes within 3 s of leaving its volume. An area change with no door just before (stairs, loading zones) marks nothing;
  - arriving through it: after such a change the player stands inside the matching door's volume (`OldHitDoor` -1 to 1 at 0.25 s after the change): that door is marked; with no hit within 1.5 s of regaining control, the nearest door within 300 units is;
  - not by walking past or standing in a door volume without going through (a locked door);
  - every door gone through is recorded (named ones too, for later steps); only generic ones *say* "explored".
- **Identity of a door:** `fieldID`, `areaID` and the volume's `resourceName` ("01d_15"): stable per area (seen unchanged across all dumps of an area). Not the gEvnRng index alone.
- **Kept across sessions, one list per save file (Francesco, 2 Oct 2026; built in 0.0.35):** `UserData/NocturneAccess/Explored/file01.txt` for the game's File 1 (one `door field/area/name` per line). Loading a file reads its list (`slMain.slLoadProc_closeFile`, file = `slMain.gslSelect` + 1); the title screen (`dds3TitleMain.dds3TitleInit`) empties it, so a new game starts with none; a door gone through is written at once to the file in play (a quit without saving keeps it: the player has been there); a new game's marks are held until its first save; saving to a file (`slSaveProc_closeFile` or `slSaveWriteEnd_saveData`, whichever fires: logged) writes the whole list there. Not handled: a quick save or suspend data, if the game has one (to watch in the log).
- **Never blocks or changes input**, as every navigation cue (CLAUDE.md). Logging: `[Nav:Explored] 15/4/01d_15 marked (left through it)` so the test can be checked in the log.
- **Possible extension to propose, not to build unasked:** remember where the door led ("Door to Annex 1F", from the location banner or the area name after the change). It may help the mental map more than "explored" alone; ask Francesco first.
- **Francesco, after the 0.0.35 test:** connecting doors (B1 to the Underground Facility) sound the same as doors of small rooms; "a godsend" if the data told them apart; kept as is for now. Findings: the door volume's `areaname` is the door's own floor block ("01", "02" within an area), not its destination; the destination is in the door's event script (`union` looks like an offset into it; not decoded). Usable without decoding: going through a door gives both ends (`[Nav:Explored]` logs "came in through it from 15/21/01d_04"), so a later step can store the pair and describe what is behind an explored door: a room with one door (dead end), an area with N doors (a corridor or hub), or the area's location banner name. Ask Francesco before building.
- **Decoded (2 Oct 2026, read-only analysis of `GameAssembly.dll`; Francesco asked for it if safe from the Denuvo side):** each field has a **door warp table**, `fldWap.gFldWarpBuff.door_buff`, one entry per door: the source area and door name, a story flag that switches entries (the same door can lead elsewhere later), and the destination field, area and arrival door (`docs/class-index.md`, "Door warp table"). It is ordinary data the game keeps in memory after loading the field, read like the door volumes; its loader runs in protected code and is never called, nor is anything else protected. So, without going through a door first, the mod can know for every door: whether it leaves the field (another dungeon or the world map), which area it leads to, the door you come out of, and from the same table how many doors that area has (one: a room, a dead end; several: a corridor or hub). 0.0.37 writes the table to the Shift+F6 dump only, to check the decoding against doors already gone through (for example Annex 1F area 21 door `01d_04` should lead to area 8, arriving at `01d_05`). Nothing spoken yet: wording and what to say to be decided with Francesco after the check.

## 2. The purpose to keep in mind while building item 1

The flag is a first step towards a mental map: each generic door should become distinguishable over time. Keep the data (door identity, when and how it was marked, the area it led to) in a form later steps can reuse (item 4, and possibly the destination name above).

## 3. "Empty" for opened item boxes (Francesco, 2 Oct 2026; built in 0.0.35)

- An item box the game has opened says it before the player walks to it: Page Up / Page Down "Item box, empty, 3 metres, north. 2 of 5."; Home "Walking to empty item box, ..."; End "Empty item box. Ahead, 3 metres.".
- Read from the game's own state, nothing kept by the mod: the box's `fld_Npc.gfldTakaraWork` entry (matched by `TakaraIdx`, the same number as `gEvnRngBox.TakaraIdx` and `fldNpc_tag.ItemBoxIdx`) has left phase 0. On loading an area the game sets phase 3 for boxes its save marks opened (`fldItemBoxAdd`, read offline); opening one moves it through 1, 2, 3 (seen in game: Annex 1F boxes 8 and 9, 0 then 3). The game's own check, `fldGbChkTakaraOpen`, runs in protected code and is not called.
- "Opened" is what the game records. If a box could be opened and keep its item (a full inventory), it would still say "empty": to watch.

## 4. Custom waypoints (later: record only, do not implement yet)

Francesco's intended later addition: the player marks their own waypoints (a spot in an area, possibly named) that the pathfinder can track like a point of interest: listed with Page Up / Page Down, walked to with Home, directions with End. To design when Francesco asks: keys (single keys preferred, CLAUDE.md), naming (preset words or numbered "Waypoint 1"), storage (same file approach as item 1), and how they mix with the game's points in the list.

## Test (items 1 to 3, Francesco)

The exact steps given with 0.0.35 are in `docs/NEXT-SESSION.md`.
