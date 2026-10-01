# Pathfinding: points of interest and auto-walk

Started 1 Oct 2026 (third navigation session). **First goal (Francesco): auto-walk and point-of-interest interaction in fixed-camera rooms** (`fldGb.cammode` 1 or 3, such as the Healing Room). They are a smaller target than the world map or the big areas. Read `docs/NAVIGATION-DESIGN.md` sections 6 to 14 first.

## 1. Scouting (what the game gives us)

Sources: navigation dump `navdump_2026-10-01_12-07-54.txt` (inside the Healing Room, field 15, area 16), the 13:39 and 14:31 logs, and read-only disassembly (`research/native`).

### Points of interest: `fldEveHit.gEvnRng` (one list per area)

The Healing Room's area (16) has 4 entries:

| Index | Name | Type | Position (x, z) | What it is |
| --- | --- | --- | --- | --- |
| 0 | `01d_01` | 10 | -9000, -5821 | door (box 300 x 200 x 100) |
| 1 | `01d_02` | 10 | -5400, -5821 | door: the entrance the player came through |
| 2 | `01npc_01` | 0 | -5400, -6360 | a person: `fld_Npc.fldNpc` 0 ("shinentai", a Soul), talk ball r 180 in `gProgramEveHit` |
| 3 | `program_eve` | 0 | -10503, -6310 | an item box: `TakaraIdx` 8, `fldNpc` 1 Type 999 (`itembox1`), ball r 160 |

So one list gives every interactable thing with its position:

- **Doors:** type 10, named `NNd_MM`.
- **People:** type 0 with an NPC index; the `fldNpc` entry has a Type other than 999.
- **Item boxes:** type 0 with `TakaraIdx` 0 or more, or an NPC of Type 999.
- **Story triggers:** `NNeve_MM`. These are invisible, so they are not points of interest.

The area spans more than one room: door `01d_01` and the item box lie 3600 to 5000 units west of the Healing Room.

### Names

There are no names in the data for people or doors:

- The place-name tables are empty.
- A person's name shows only as the dialogue speaker ("Soul").
- A door's destination shows only in the location banner, once the player stands in the door volume.

The mod speaks its own category words ("Door", "Person", "Item box"), as it already does for "Wall ahead".

### When an interaction is in reach

The corridor logs show the game's own hit index:

- `fldEveHit.gFldEveRngIdx` becomes the `gEvnRng` index of the thing in reach; `OldHitInf` does the same for people, `OldHitDoor` for doors.
- The game's prompt ("Talk", "Open Door") is shown and spoken in the same moment.

The auto-walk therefore ends when `gFldEveRngIdx` equals the target's index. The game itself decides "in reach", so the mod invents no distance or angle.

`fldEveHit.fldEveHit_ChkDir` / `_ChkDir2` (a facing test: angle, and distance 200) are called only by event-script commands (`fldCommand.fldCommand_ACT_WIN_DSP`, `_ACT_WIN_DSP2`, `_DIR_HIT_CNTR`), not by the everyday prompt. The everyday check is `fldEveHit.fldEveHit_ChkEveHit`, which uses `fldEveHit_ChkBoxIn` (boxes, doors) and `fldEveHitPositionInside`. Whether a person must be faced is still unknown; the first test will show it.

### Walking in a fixed-camera room

From the 13:39 trace in the Healing Room:

- **Movement routine:** `fldPlayerCalc_Nml` (third person), which reads `DDS3_ORGMAP_PRESS` U, D, L, R relative to the camera.
- **Direction:** the game snaps the facing goal to world multiples of 45 degrees. 0.0.16 chose the pad combination from the camera yaw and always got the wanted facing.
- **The player walks along the current facing, which eases towards the goal** (about 0.25 to 0.5 s). Example: from facing 21 to goal 45, the movement vector matched the eased facing. Every direction change therefore curves a little.
- **Speed:** about 25 to 29 units per tick.
- **The "fixed" camera pans while the player walks** (yaw 333 to 321). The pad that walks a given world direction can change during a walk, so it must be chosen again every tick from the live camera yaw.
- **Collision:** the player stops about 60 units from walls. Furniture and the NPC boxes are vertical collision polygons too; the room has 155 polygons. The player got stuck at -5039, -6723 against furniture.

## 2. Design, version 1 (0.0.20)

Three parts, each logged so a run can be checked from the log alone.

### 2.1 Points of interest (`PointsOfInterest`): works everywhere in the 3D field

- Built from `gEvnRng` (section 1) when asked, sorted by distance.
- **Page Up:** next point of interest. **Page Down:** previous. (0.0.20 had F5 / Shift+F5; Francesco asked for single keys, 0.0.21.)
  - Speech: the kind, the distance in metres (game units / 100), and the compass direction from the player.
  - Example: "Person, 5 metres, south. 2 of 4."
  - Compass: north = +Z, as on the game's own compass (`NAVIGATION-DESIGN` section 7).
- The selection is kept by `gEvnRng` index, and it resets on an area change.

### 2.2 Walkability grid and A* (`WalkGrid`)

- **Cells:** 30 x 30 units (about one tick of walking), covering the floor polygons of the current area at the player's floor height. Floor polygons are horizontal, `|normal.y|` 0.5 or more, within 60 units of the player's Y, with `Attr` 0 only: the horizontal overlays (`Attr` 0x800, 0x1000, 0x8000) reach past the walls. Checked offline: a Perl rebuild of the grid from the 0.0.12 Healing Room dump matches the room. It shows the walls, the furniture blocks, the Soul inside a blocked bed or table area with walkable space around it, and a walkable doorway.
- **A cell is walkable** when it lies on such a floor and its centre is at least 65 units from every vertical polygon that overlaps the body height (5 to 180 units above the floor). That covers walls, doors (closed walls), furniture and NPC boxes. 65 is the measured stop distance of 60, plus 5.
- **A\*:**
  - 8 directions, matching the game's 45-degree snap;
  - no cutting past corners;
  - a small penalty per direction change, so the path has few turns.
- **The goal cell:**
  - for a door, the walkable cell nearest the door volume's centre;
  - for a person or a box, the walkable cell nearest the object, but at least 120 units from its centre (outside its 100 x 100 box).
- **The path:** reduced to straight runs, one world direction each (multiples of 45).
- Rebuilt when the collision copy changes (`CollisionCache.Version`).

### 2.3 Auto-walk (`AutoWalk`): fixed-camera rooms first

- **Home** (Inizio): walk to the selected point of interest; then Enter interacts as usual. In first person it says "Auto-walk works in fixed-camera rooms for now." (first person needs its own steering with turns).
- **Every field update**, before the game reads the pads, it holds the pad combination whose camera-relative bearing is nearest the current run's world direction. Mapping: U = camera yaw, R +90, D +180, L +270, diagonals in between; compass bearing = 180 - `PlayerKakudo`.
- **Moving between runs:** it switches to the next run when the player is within 20 units of the run's end, to allow for the facing easing.
- **Re-planning:** it plans again from the current position when the player is more than 90 units off the run, or has not moved for 8 ticks (curves, slides along furniture). At most 6 times, then it says "Blocked."
- **It ends:**
  - when `gFldEveRngIdx` equals the target: "Arrived.", and the game speaks its own prompt;
  - at the goal cell. For a person or a box it then keeps walking straight at the target for up to 1 s, so the game's hit check can fire. Facing matters there.
  - after 30 s;
  - **at once when the player presses any movement key**, read before the game reads it: "Auto-walk stopped."
- It never presses confirm: the player chooses to talk, open or examine.
- **Speech:** "Walking to person, 5 metres." at the start; "No path." when A* finds none.

### 2.4 Diagnostics

- **Shift+F6** (the navigation dump moved here from F5) also writes the walkability grid as an ASCII map, one character per cell, to the dump file:
  - `.` walkable, `#` blocked, space no floor;
  - `@` the player, `D` door, `P` person, `B` box;
  - `*` the last planned path.
- **Log `[Nav:Poi]`:** the list at each selection.
- **Log `[Nav:Walk]`:**
  - plan: cells, runs, ms;
  - every run change, with position, camera yaw and pads;
  - every re-plan, with its reason;
  - the end: arrived, blocked, timed out, stopped.

## 3. Later steps

1. Test 0.0.20 in the Healing Room: list, walk to the Soul, the door, the other door; check from the dump whether the grid matches the room.
2. Facing at arrival (people), if the prompt does not appear.
3. Auto-walk in first person (corridors): steering with the game's own 90-degree turns and the snap.
4. Longer routes across areas: the auto-map's sub-blocks as a room graph (`NAVIGATION-DESIGN` section 2.4), doors as links.
5. "Person ahead" and similar cues in blobber mode (Francesco: with pathfinding).

## 4. Results of the 0.0.21 test and the reachability filter (0.0.22, 1 Oct 2026)

Log `session_2026-10-01_15-20-43.log`, dump `navdump_2026-10-01_15-25-07.txt`, recording of the same run. Francesco: "The implementation is already quite solid, the auto walking worked perfectly." Other game screens checked in the same run still read correctly: title, config tabs, load, save, the cylinder choice, Hijiri's dialogue, the command menu and Stats.

**Walks** (no re-plans needed):

- **To the Soul:** one run south. The game's hit index fired after 425 ms: "Arrived.", then the game's "Talk".
- **To the entrance door:** four runs. "Arrived." after 766 ms, then "Open Door".
- **Leaving and coming back** worked both ways.
- **Pad choice:** the pads chosen from the panning camera (yaw 314 to 333) were always within 18 degrees of the wanted direction, and the game snapped to it.

**The problem:** Page Up also listed the item box (51 m west) and door `01d_01` (36 m west). `gEvnRng` covers the whole area (16), and the area includes a separate room west of the Healing Room that cannot be reached from it:

- the live grid shows that room as an unconnected block, with `01d_01` in its north wall;
- the item box lies beyond the grid's west edge, with no loaded floor there.

The walk to the item box went to the nearest reachable cell, at the Healing Room's west wall, and ended "Near the item box." The live grid map matched the offline rebuild (`tools/navgrid.pl`) cell for cell.

**Fix (0.0.22, not yet tested):**

- Every point of interest is marked reachable when a walkable cell within its reach radius can be reached from the player's cell (`WalkGrid.ReachableNear`: a flood with the same moves as the search, cached until the grid or the player's cell changes). The radius is 150 for doors and 250 for people and boxes; the Soul's goal cell was 135 away.
- Page Up and Page Down list only reachable points; the log lists all of them, marking the others "not reachable, left out". When the area has no grid, all points are listed.
- A walk whose nearest reachable cell is farther than the reach radius says "No path." instead of walking to a wall.
- The navigation dump marks unreachable points "not reachable from here".

## 5. The basement corridors: named doors, Souls, first-person auto-walk (0.0.23, 1 Oct 2026)

### Results of the 0.0.22 test (log `session_2026-10-01_19-07-38.log`)

Francesco: "It worked very well." In the Healing Room, Page Up listed only the door and the Soul (two unreachable points left out), and both walks arrived ("Talk", "Open Door").

One nitpick, in the empty room west of the Healing Room. The player had entered through door `01d_01` and stood inside its volume facing into the room. The game's hit index was already the door, so the walk ended at once ("Arrived."), but the game's "Open Door" prompt came only after a W press turned the player towards the door. **The prompt needs the player to face the door, not only to stand in its volume.**

### Read in the game's code: where room names come from

- `fldGlobal.fldNameData` (`FldNameData`, a ScriptableObject) holds the real name tables; the `fldGlobal` arrays `area_name`, `basyo_name` and `floor_name` are empty in this build. Its methods:
  - `GetAreaName(fld, area)`;
  - `GetBasyoName(fld, basyo)`;
  - `GetFloorName(fld, floor)`;
  - `GetSaveNmlName` / `GetSaveSmlName(terminal)`.
- `fldAutoMap.fldDrawAreaBlockFont` (the auto-map's labels) calls `GetAreaName(fldGb.fieldID, fldGb.nameU)` and `GetBasyoName(fldGb.fieldID, fldGb.nameD)`. `nameU` and `nameD` are `fldGlobalWork_t` fields at 0x74 and 0x78.
- **Place pads.** The corridor area (field 15, area 5) has small horizontal polygons with `Attr` 0x1000 and a `Basyo` number in front of three doors:
  - basyo 10, x -4400..-4000, z 600..900: the elevator, event volume `01eve_04`;
  - basyo 11, x -7400..-7000, z -2900..-2700: door `01d_02`, the Terminal (the save point);
  - basyo 15, x -5600..-5200, z -5700..-5500: door `01d_07`, the Healing Room.

  Inside the Healing Room, one room-sized place polygon (basyo 15) reaches 100 units past its walls. Standing on these polygons is what makes the location banner name the room behind a door.
- **People:** all three corridor NPCs (`01npc_01`, `01npc_02`, `npc_hoji_01`) use the object `shinentai(Clone)`, the model the game's dialogue names "Soul".

### Design (0.0.23, not yet tested)

- **Door names:** a door is named with the game's own text, `GetBasyoName(fieldID, basyo)` of the nearest place pad within 300 units. A pad is a polygon no larger than 700 x 700; room-sized place polygons do not name doors. Type-10 event volumes (`NNeve_MM`) count as doors only when a pad names them: that is the elevator. Speech: "Healing Room door, 30 metres, south-west. 2 of 8."
- **Souls:** a person whose object is `shinentai` is called "Soul".
- **Facing at arrival:** a walk ends "Arrived." only when the game's hit index is the target **and** the player faces it (within 50 degrees). Otherwise it turns towards the target: in third person by pressing towards it, in first person with the game's own turn. After 20 ticks it gives up on the facing.
- **First-person auto-walk** (the corridors). Home now works in first person too.
  - **Search:** 4 directions, a turn penalty of 2, and a wall-distance cost: a step next to a wall costs up to 3 times more than one 300 units away, so routes keep to the middle.
  - **Each run:** the game's own turn (one tick of pad L (+90 `PlayerKakudo`), R (-90) or D (round)), waiting for it to finish, then pad U to the run's end, within half a tick.
  - **The end:** turn towards the target's nearest square direction and walk until the game's hit index fires.
  - **Interruptions:** any movement key, Q and E included, stops the walk; so does a view change.
- **Blobber mode during a walk:** no snap and no cues; its baseline is refreshed afterwards.
- **Checked offline** (corridor dump of 12:05):
  - A square lattice of 600 does not reach the elevator: the passage there runs at x -4000 to -4144.
  - The fine grid reaches every door and Soul in the basement corridor: doors within 29 units, Souls within 95 to 135.
  - The exception is door `01d_01`, 50 m east, where no floor is loaded; the reachability filter leaves it out.
