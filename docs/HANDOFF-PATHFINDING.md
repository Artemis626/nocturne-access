# Handoff: navigation and pathfinding

Rewritten 1 Oct 2026, evening, at the end of the second navigation chat (versions 0.0.17 to 0.0.24), for the next chat.

Read `CLAUDE.md` first (binding rules), then this file, then:

- `docs/PATHFINDING.md`: points of interest, walking map, auto-walk; every test result and fix, sections 1 to 9;
- `docs/NAVIGATION-DESIGN.md` sections 6 to 14: blobber mode, first person, the game's input and code;
- `docs/class-index.md` "Field navigation";
- `docs/KEYS.md`;
- `tools/native/README.md`.

## Where things stand

- **Installed: 0.0.31** (battle work in 0.0.28 and 0.0.29, cleanup in 0.0.30, the refactor plan in 0.0.31, `docs/HANDOFF.md`; navigation: the square offset takes the middle of the near-best range). 0.0.27 verified at 22:14 (section 9: elevator floor banner, volume heights from their boxes so Pixie is listed, walks end in front of doors and on a line with people, blobber square centres per area). 0.0.26 was played at 21:31 through the Medical Center 1F and 2F to the annex gate: about 30 walks arrived, End one sentence per press. 0.0.25 (hidden NPCs left out, directions on End) was played at length at 20:37: Francesco finds the systems "mature enough for a low-vision player to progress through the game with only minor friction" and is now playing through, reporting bugs and missing features. 0.0.26: End speaks only when pressed; Home turns to a door even right at it (section 8).
- **Verified in game:**

| Feature | Versions | Result |
| --- | --- | --- |
| Diagnostics, first person in memory (V, flag 2800 never written), pad injection | 0.0.12 to 0.0.16 | Verified |
| Blobber mode version 2 | 0.0.17 | "A significant improvement" |
| F3 centring | 0.0.18 | "Feels and looks great"; 0.0.19 fixed two edge cases |
| Points of interest and auto-walk in the Healing Room | 0.0.20 to 0.0.22 | "Worked perfectly" |
| Basement and Medical Center: first-person walks with the game's own turns, named doors | 0.0.23 | Several walks arrived |
| Basement walks to the Terminal door and a Soul, `OldHitDoor` / `OldHitInf` arrival | 0.0.24 | Arrived (20:20) |
| Hidden NPCs left out; long walks in field 45; event interruptions | 0.0.25 | Verified (20:37) |
| End on demand; Home at doors; the Medical Center 1F and 2F | 0.0.26 | Verified (21:31) |
| Elevator floor banner; walks in front of doors; square centres per area (2F offset 300) | 0.0.27 | Verified (22:14) |

  Blobber mode version 2 keeps every game key; releasing W carries the walk on to the next square's centre; cues speak only changes and never block. The 0.0.23 walks also exposed the problems fixed in 0.0.24.
- **Next test (0.0.27), during normal play:**
  - the elevator: "> Select a floor." with no floor after it, then the options;
  - Home to doors approached along the wall: the walk ends straight in front, facing the door;
  - blobber mode in the 2F corridors: snaps and F3 end in the middle ("Centred."); log line "Square centres ... offset 300 ...";
  - a talk event like Pixie should be in the Pages list (Francesco's name for the Page Up / Page Down list);
  - still from 0.0.24:
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
  - Hidden NPCs (object inactive) are left out, and their collision boxes do not block the walking map.
  - Only reachable points are listed; others on another floor are marked "upstairs" or "downstairs".
- **`WalkGrid`:** 30-unit cells with up to 3 floor heights each. It has a reachability flood and Dijkstra (8 directions in third person, 4 in first person), with a wall-distance cost and a turn penalty.
- **`RouteGuide` (End):** the same route, spoken instead of walked, **once per press** (Francesco: running announcements cluttered the wall and corridor cues): first person relative to the facing ("Turn left, 12 metres, then right."), fixed camera as the key direction on screen ("Up, 5 metres, then left."). The player walks; the mod presses nothing.
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
- **Points of interest:** Page Up / Page Down / Home, then Enter as usual. **End gives spoken directions** instead of walking, **only when pressed** (Francesco, 0.0.25 play session: movement cues come first). NPCs that are not Souls are listed as "Person". **Single keys, not Shift or Ctrl combinations**; Shift+F6 is a debug key.
- **The mod never presses confirm**: the player decides to talk, open or examine.
- **Read-only IL2CPP analysis of `GameAssembly.dll`** is a standard tool (never `smt3hd.exe`).

## Open problems, in order

1. Francesco plays through the game and reports bugs and missing features: read each session's log, fix, record here. Test 0.0.27 (above).
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
