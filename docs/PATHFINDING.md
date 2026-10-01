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
