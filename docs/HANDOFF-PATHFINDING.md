# Handoff: navigation and pathfinding

Rewritten 1 Oct 2026, evening, at the end of the second navigation chat (versions 0.0.17 to 0.0.24), for the next chat.

Read `CLAUDE.md` first (binding rules), then this file, then:

- `docs/PATHFINDING.md`: points of interest, walking map, auto-walk; every test result and fix, sections 1 to 6;
- `docs/NAVIGATION-DESIGN.md` sections 6 to 14: blobber mode, first person, the game's input and code;
- `docs/class-index.md` "Field navigation";
- `docs/KEYS.md`;
- `tools/native/README.md`.

## Where things stand

- **Installed: 0.0.24**, built 1 Oct 2026, **not yet tested in game**. It fixes everything the 0.0.23 prologue test showed (`PATHFINDING.md` section 6).
- **Verified in game:**

| Feature | Versions | Result |
| --- | --- | --- |
| Diagnostics, first person in memory (V, flag 2800 never written), pad injection | 0.0.12 to 0.0.16 | Verified |
| Blobber mode version 2 | 0.0.17 | "A significant improvement" |
| F3 centring | 0.0.18 | "Feels and looks great"; 0.0.19 fixed two edge cases |
| Points of interest and auto-walk in the Healing Room | 0.0.20 to 0.0.22 | "Worked perfectly" |
| Basement and Medical Center: first-person walks with the game's own turns, named doors | 0.0.23 | Several walks arrived |

  Blobber mode version 2 keeps every game key; releasing W carries the walk on to the next square's centre; cues speak only changes and never block. The 0.0.23 walks also exposed the problems fixed in 0.0.24.
- **Next test (0.0.24):**
  - redo Francesco's prologue route;
  - the stairwell of the Medical Center: Page Up should say "Door, …, upstairs", and Home should climb the stairs;
  - a walk through a story trigger: "Walk interrupted by an event.";
  - check that door `01d_06` (behind a wall) is no longer listed;
  - Shift+F6 in the stairwell, to check the walking map's levels.

## How it works (code in `src/NocturneAccess/Systems/Navigation`)

- **`PadInput`:** the game's own pad buttons, inside `fldPlayerCalc`. It reads the real keys (`Real`, the same `DDS3_PADCHECK_PRESS` the game asks) and presses buttons (`Hold`). It never hides a key.
- **`BlobberMode` (F4)** and **`CentreWalk` (F3):** first person only, `NAVIGATION-DESIGN` sections 12 to 14.
- **`PointsOfInterest` (Page Up / Page Down):** the area's `fldEveHit.gEvnRng` list.
  - Doors: `NNd_MM`, named with the game's place names from the pads in front of them (`FldNameData.GetBasyoName`).
  - Souls: `shinentai` model. Item boxes. Place events ("Elevator").
  - Only reachable points are listed; others on another floor are marked "upstairs" or "downstairs".
- **`WalkGrid`:** 30-unit cells with up to 3 floor heights each. It has a reachability flood and Dijkstra (8 directions in third person, 4 in first person), with a wall-distance cost and a turn penalty.
- **`AutoWalk` (Home):**
  - Third person: camera-relative pads, chosen again every tick.
  - First person: the game's own 90-degree turns (pad L / R / D, one tick each), then pad U to each run's end.
  - It ends when the game's own "in reach" fires (`OldHitDoor` for doors) and the player faces the target, then Enter interacts.
  - Any movement key stops it; so does an event: "Walk interrupted by an event."
- **Diagnostics:** Shift+F6 dump (with the walking map as text), `[Nav:Poi]` and `[Nav:Walk]` log lines. `tools/navgrid.pl` rebuilds a single-level map offline from a dump.

## Francesco's decisions (all in force)

- **Scope:** 3D dungeons first, not the world map. He tests in new areas on purpose.
- **Blobber mode is a toggle (F4), on by default.**
  - **It never changes the game's input:** first-person keys stay the game's own (W forward, S turn round, A / D 90-degree turns, Q / E side steps, V view, confirm).
  - Only movement is snapped (releasing W walks on to the next square's centre).
  - Q / E are not snapped. S as a step back: not now.
- **Cues never block or refuse input**; they speak only changes.
- **F3 centring** walks to the centre of the current square with the game's own moves.
- **Points of interest:** Page Up / Page Down / Home, then Enter as usual. **Single keys, not Shift or Ctrl combinations**; Shift+F6 is a debug key.
- **The mod never presses confirm**: the player decides to talk, open or examine.
- **Read-only IL2CPP analysis of `GameAssembly.dll`** is a standard tool (never `smt3hd.exe`).

## Open problems, in order

1. Test 0.0.24 (above).
2. **Story triggers on routes.** A walk through an `NNeve` volume stops at the event. Should routes avoid them (risk: blocking progress), or walk on after the event? Ask Francesco.
3. **Locked doors and doors behind walls:** the reach test (95 units) is a measured rule. A dump with `gEvnRngBox` sizes for doors that never fire would make it exact.
4. **Longer routes across areas:** through doors, using the auto-map sub-blocks as a room graph (`NAVIGATION-DESIGN` section 2.4).
5. "Person ahead" and similar cues in blobber mode (Francesco: with pathfinding).
6. Outdoor and big areas (field 11 has 35 000 walkable cells, 7 ms to build: fine so far).

## Key facts

- Game units = Unity x 100; **Y points down**; +Z north, +X east. `PlayerKakudo` in degrees: 0 south, 90 east, 180 north, 270 west. Compass bearing = 180 - `PlayerKakudo`.
- `fldPlayer.fldPlayerCalc` runs 30 times a second; walking moves 29 units per tick.
  - First person (`fldPlayerCalc_Nml_syukan`) needs flag 2800 and `cammode` 0.
  - `cammode` 1 or 3 means a fixed camera and third person (`fldPlayerCalc_Nml`).
  - During events and dialogue `fldPlayerCalc` runs but neither movement routine does.
- First person reads `DDS3_PADCHECK_PRESS`:
  - U walks;
  - D alone turns round;
  - L / R turn 90 degrees;
  - L1 / R1 side-step;
  - U or D with L or R walks diagonally.
- `fldEveHit.gFldEveRngIdx` is the `gEvnRng` index in reach but can stay stale; `OldHitDoor` and `OldHitInf` reset on leaving. Prompts such as "Open Door" also need the player to face the door.
- Place names: `fldGlobal.fldNameData` (`GetBasyoName`, `GetAreaName`, `GetFloorName`); place pads are `Attr` 0x1000 polygons with `Basyo`.
