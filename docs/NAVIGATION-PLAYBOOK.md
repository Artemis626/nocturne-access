# Navigation playbook for screen-reader mods of 3D dungeon crawlers

Written 7 Oct 2026, at version 0.0.60 of Nocturne Access, for a future project: another Shin Megami Tensei game, a blobber, or any dungeon crawler RPG made accessible to blind and low-vision players. It collects what navigation in Nocturne HD needed, what failed and why, and the order in which to build things. Every lesson says what happened in Nocturne, so it can be checked against the detailed records:

- `docs/NAVIGATION-DESIGN.md`: first person, blobber mode, the game's input;
- `docs/PATHFINDING.md`: points of interest, the walking map, auto-walk, every test;
- `docs/HANDOFF-PATHFINDING.md`: dungeon navigation summary;
- `docs/HANDOFF-OVERWORLD.md`: the world map;
- `docs/EXPLORED-DOORS-PLAN.md`: explored doors, door table;
- `docs/HANDOFF.md`: per-session results, including warps and ladders.

Nocturne names (classes, fields, flags) appear only as examples. In a new game, find the equivalents; never assume the same names.

## 1. Principles the player set (keep them)

These came from Francesco, a low-vision NVDA user who tested every build. They held up for the whole project.

1. **Never change the game's controls.** A navigation aid adds to the game's input; it does not replace, hide or reinterpret keys. Nocturne's first version of blobber mode reinterpreted A / D as side steps and refused moves into walls. It blocked play: the player could not walk up to a Soul standing against a wall. The working version keeps every key and only snaps movement: when W is released, the walk carries on to the next square's centre.
2. **Cues never block or refuse input.** "Wall ahead", "Opening left", "Door ahead" describe the game's collision. The game decides what is passable, not the mod.
3. **Speak only changes, and give guidance on demand.** Running directions while walking ("Turn right … 5 metres … 10 metres") cluttered the wall cues. Directions are spoken once per key press (End).
4. **The game's own words first.** Place names, door names, prompts ("Open Door", "Talk", "Climb Ladder") come from the game's text and tables. Category words ("Door", "Item box", "Ladder up") are the mod's only where the game has none. Never invent names.
5. **The mod never presses confirm.** It walks the player to a door, a person or a box. The player decides to open, talk or examine.
6. **The game decides "in reach".** Arrival is the game's own hit index or prompt, not a distance the mod picked.
7. **Single keys, few combinations.** Page Up / Page Down (next / previous point), Home (walk there), End (directions), Insert (selected point again), Delete (where am I), function keys for toggles. Combinations only for debug keys and rare toggles (filters on Shift+Page Up / Down were the player's choice).
8. **Toggles with defaults.** Blobber mode is on by default, with one key to turn it off for areas where the grid fits poorly.
9. **Honest reachability, with landmarks always present.** The list must not offer points the player cannot reach, and must not hide what the player can see or reach. Landmarks (save points, exits, named rooms, ladders and stairs) stay listed even when no known way leads there, marked "beyond doors". Hiding them felt like a bug to the player. Routes computed through other areas ("via 1 other area", Nocturne 0.0.56) missed the point and were turned off: the player wants to know what is where and which parts join, not a long computed route.
10. **A mental map, not only a pathfinder.** Even sighted players get lost in these games. What helped:
    - doors gone through say "explored", kept per save file;
    - opened boxes say "empty", read from the game's state;
    - door kinds: "room", "passage", "exit", "to exit";
    - a stable list order: map order, north to south then west to east, so a point keeps its number while the player walks;
    - filters by kind;
    - "Where am I" with the place name;
    - "Warped." when the game moves the player inside an area.

## 2. Research method

### 2.1 Read the game's code offline before guessing at runtime

For a Unity IL2CPP game:

- Cpp2IL / Il2CppInterop proxies give names and signatures.
- A read-only disassembly of `GameAssembly.dll` gives behaviour. In Nocturne: the first-person gate, the movement routines, the keys each routine reads, the door table layout, the game's own "in reach" test.

Runtime probing of the first-person lock went nowhere for three builds (forcing flags that turned out to be the Dante/Raidou mode). One reading of the field update found the real gate in an afternoon. Never touch a DRM-protected executable; read only the game assembly, never load or run it (`tools/native/README.md`).

### 2.2 What to look for (Nocturne found all of it as plain data)

| Need | What it was in Nocturne |
| --- | --- |
| Position, facing, area | A static work struct: `playerX/Y/Z`, facing angle, `fieldID`, `areaID`, camera mode. Units: Unity × 100; **Y points down**; facing in degrees with its own zero |
| Exploring or not | The field update ran in the last 100 ms (Harmony postfix); each movement routine counted separately, since dialogue keeps the field update running but stops movement |
| Geometry | Every collision polygon of the loaded area: points, normal, plane, face attributes (`Attr`), block id, NPC (hoji) id |
| Points of interest | One list of event volumes per area (doors, story triggers, item and NPC ranges) with names (`01d_03`, `01eve_02`) and boxes with a facing |
| Where doors lead | A door warp table per field: source area and door, story flag, destination field / area / arrival point, `movetype` (1 door, 2 touch warp, 5 ladder) |
| Names | Name tables (area, place, floor); small floor pads in front of doors carry the place number that names the room behind |
| In reach | The game's hit index for doors and people, and the prompt text on screen |
| People and boxes | NPC list (hidden NPCs have an inactive object), item box table with an opened phase |
| Switches | The game's own "this block's collision is off" and "this NPC is not placed" checks, which its collision code applies |
| Map | The auto-map's areas, sub-blocks and icons (not needed in the end) |

### 2.3 Log everything, read every log, keep dumps

- A per-second trace line while exploring: position, facing, floor polygon under the player and its height, camera mode, which movement routine ran, keys.
- A change log of the IDs: area, block, hit indexes.
- A navigation dump key (Shift+F6). It writes to a file:
  - every polygon;
  - every event volume with its box;
  - the door table;
  - the points of interest with their reachability;
  - the walking map as text.
- Read the logs yourself after every run, and check the archive (a relaunch overwrites the latest log).

### 2.4 Rebuild the walking map offline from dumps

This was the single most productive tool:

- `tools/navgrid.pl`: one level;
- `tools/worldgrid.pl`: the world map;
- `tools/navregions.pl`: every level, regions, a one-way flood from the player, `NAVMAP` box maps, `NAVCELL` layer listings.

Use them to:

- reproduce a "not reachable" report exactly;
- try a fix;
- run it over every saved dump to make sure nothing gets worse.

**The ground truth is the trace:** every position the player actually stood on must be reachable from where they came. A rule that passes that test can still be wrong the other way (the world map's walls, section 4.4), so also check that walls block.

## 3. Movement

### 3.1 Drive the game's own input, at its own speed

- Find the movement routine and the exact input checks it reads. In Nocturne the keyboard arrived as pad buttons through two functions (`DDS3_ORGMAP_PRESS`, `DDS3_PADCHECK_PRESS`); the logical-action API and the analog stick never saw it.
- Harmony postfixes on those checks answer "pressed" for a held button, only inside the movement routine. The game's collision, doors, slopes, events and encounters then behave exactly as with a real key.
- **Never write positions.** Normal speed only: thin door volumes can be skipped at higher speeds.
- Read the real keys through the same check (with the mod's own postfix ignoring those calls), so any player key stops a walk before the game reads it.

### 3.2 First person and blobber mode

- Nocturne's first person was locked on a first playthrough. The lock (flags 2208 / 2225) and the view state (flag 2800) were found in code.
- The mod answers the view flag in memory only and never writes it, so nothing reaches the save.
- The game's first person was already a near-blobber: 90-degree turns, continuous walking, side steps.
- **Blobber mode is a snap, not a mode switch.** It works as follows:
  - **Release:** on releasing W the mod holds forward until the next square centre (600 units, about 0.7 s).
  - **Stop:** it stops at once on any key, a collision, a facing or area change, or after 3 s.
  - **Centring key (F3):** fixes slides along furniture and slow drift with the game's own side steps and turns. If the centre is behind, it turns round, walks back and turns round again.
  - **Square offset per area:** the best offset is chosen from wall distances. A corridor between walls at 1800 and 2400 has its centre at 2100, half a square off a 600 grid, and every snap there ended against a wall.
- **Cues at square centres:** only what changed ahead, left and right. Door cues within 45 degrees of a direction only. NPC and box collision boxes are not walls.

### 3.3 Fixed-camera rooms

Nocturne's fixed-camera rooms force third person.

- **Turns:** the game snaps the facing goal to world multiples of 45 degrees.
- **Pad choice:** the pad for a world direction is chosen every tick from the live camera yaw, because the "fixed" camera pans.
- **Mod in these rooms:** no blobber mode. Points of interest, auto-walk and directions in screen terms ("Up, 5 metres, then left.") cover them.

### 3.4 World map

- **Different routine:** compass-fixed directions, 16 units per tick, collision radius 30 instead of 60.
- **Terrain:** tilted walls and heights over 2000 units.
- **Separate walking map:** it needs its own rules (section 4.4). Directions there are compass words, which are also the arrow keys.

## 4. The walking map

### 4.1 Construction that worked

- **Cells:** 30 units square (half the collision radius; walking moves 29 per tick).
- **Floors:** horizontal polygons (|normal.y| ≥ 0.5) with plain attributes. Overlays (`Attr` 0x800, 0x1000, 0x8000 in dungeons) reach past walls and are not floors.
  - Each cell keeps several floor heights (layers); heights within 20 merge.
  - Four layers since 0.0.60. Count and log any height left out by the limit.
- **Walls:** vertical polygons. A wall blocks a floor only when it overlaps the body above it: from step height (30) to body height (180), with the game's Y pointing down. Stair steps lower than 30 therefore do not block.
  - A cell is blocked within the clearance (65) of such a wall.
  - Wall distance adds cost near walls, so routes keep to the middle.
- **Moves:** 8 directions (4 in first person, where the game turns 90 degrees), no cutting past a blocked corner.
  - A move joins heights at most 45 apart.
  - **The move must reach every such layer of the neighbour** (section 4.3).
- **Leave out what the game ignores:**
  - collision boxes of hidden NPCs;
  - blocks and NPCs the game's own checks switch off (an opened gate that stays in the collision copy).
- **Rebuild:** when the collision changes, the area changes, or the set of hidden NPCs changes. Anything derived (regions, warp graph) is keyed on a build counter.
- **One flood from the player for reachability:** the same moves as the search, cached per start cell.

### 4.2 Targets and heights

- **Height to reach:** a point's floor height comes from its volume box. Use the player's height when it lies within the box's span (a stairwell with doors stacked on two floors), else the box bottom.
  - On the world map, always use the floor under the volume. A 400-tall entrance volume made the player's own height the target from the low side.
- **Door volumes sit partly in walls:**
  - **Volume behind a wall:** a door whose volume centre lies behind the wall, where the game never fires it, needs a tight reach radius (95), not 150.
  - **Volume in a thick wall:** a door whose volume is entirely inside a thick wall is reached through its partner's landing.
- **Door approach:** walk to a point straight out of the door's centre along the volume's facing, then face into it. Volumes are 300 wide, and approaching along the wall fired the hit at the volume's edge.
- **Person approach (first person):** end on a compass line through the person, inside the talk radius, so the last turn faces them squarely.
- **Arrival:** a person or box only when the game's prompt is on screen; the hit index can fire 60 units before the prompt does.
  - Doors use the door hit.
  - The general hit index can stay stale after leaving.

### 4.3 Pitfalls found the hard way (symptom, cause, fix)

| Symptom | Cause | Fix | Where |
| --- | --- | --- | --- |
| Areas walked through by hand say "not reachable" or "beyond doors"; the list says reachable but auto-walk says "No path" | **A floor polygon runs on under stairs or a ramp** (the Ginza underpass track bed under the platform stairs; a flat floor under the Ginza exit ramp). A cell holds two heights a step apart. A move that takes only the nearest height goes one way: down onto the hidden floor, never back up. Region labelling (undirected) and the search from the player (directed) then disagree | Moves reach every walkable layer of the neighbour within the step height; a target counts only the layer of a cell nearest its own height | 0.0.60, `WalkGrid.Steps`, `AtHeight`; `docs/HANDOFF.md` 6 Oct 21:08 entry |
| One end of a long area is "not reachable" until the player walks there | A per-side window (400 cells) cut a 12 630-unit tunnel | Window only areas that are large in total (the world map) | 0.0.60 |
| Items reachable only after climbing a ramp | Too few layers per cell (suspected), and the one-way moves above | 4 layers, count drops, symmetric moves | 0.0.34 notes, 0.0.60 |
| A room in the same area listed though walled off | The area's event list covers rooms not joined to the player's | Reachability flood; list only what is reachable, log the rest | 0.0.22 |
| The world map's routes go through buildings | The cell test lets cells on both sides of a thin wall be walkable, and floors continue under buildings | Block every move whose segment crosses a wall polygon | 0.0.43 |
| "Blocked." at once beside a person who isn't there | A hidden NPC's collision box stays in the collision copy | Skip boxes of NPCs whose object is inactive | 0.0.25 |
| A gate still blocks after it opened | Its polygons stay; the game skips them by block switch | Apply the game's own switch checks | 0.0.34 |
| A talk event under the floor is missing | Volume centre below the floor, outside the target band | Use the box's span, not the centre | 0.0.27 |
| Snaps end against walls | Square grid offset wrong for the area | Choose the offset per area | 0.0.27 |
| "Arrived." facing away from a door | Facing check skipped within 80 units | Skip it only within 15 for doors | 0.0.26 |
| Spinning at the target | Bearing flips within 20 units | Stop with "Near the …" when close without the game's hit | 0.0.25 |
| A walk "blocked" during dialogue | Field update runs, movement routine does not | Count only ticks where the movement routine ran; an event ends the walk ("Walk interrupted by an event.") | 0.0.19, 0.0.24 |

**Rule of thumb:**

- **Region labelling:** run it with the same directed moves as the search, or make the moves symmetric. Otherwise the list and the walk will disagree.
- **Phantom floors:** any polygon set with a ground plane under everything (tracks, terrain, a basement slab) produces them. Expect them and test stairs and ramps both ways.

### 4.4 The world map needed its own rules

- **Calibration:** clearance 22 measured from traces (13 of 18 positions near walls sat at 29 to 30 from the plane), with sample points inside the cell.
- **Moves across walls:** blocked as above.
- **Region boundaries:** tall `Attr` 0x800 planes are boundaries the marker walks through, not walls.
- **Checks:** all traced positions reachable, and the regions behind cliffs not reachable.

## 5. Points of interest

- **Source:** the area's event volumes, NPCs and boxes. Merge duplicates (doors are often two volumes, one per side) and story volumes that carry a door's place name.
- **Kinds and words:**
  - Door, with the game's room name when a place pad is in front of it ("Terminal door", "Healing Room door").
  - Exit to overworld (the door table leads to a world-map field).
  - Room, passage, exit, to exit (from the door table: a side room is an area whose every door leads back here).
  - Ladder up / down; Soul / Person / Demon; Item box (empty); Event with the game's prompt learned once seen ("Event, Open Door"); Warp.
- **Format of each entry:** kind, explored / empty, distance in metres, compass direction, "upstairs" / "downstairs" when more than 150 above or below, then "N of M". Then "via 1 ladder" or "beyond doors" when the way is not on foot.
- **Order:** map order, north to south then west to east, so numbers stay put; nearest-first is an option. Filters reset to All in every new area.

## 6. Links inside and between areas

- **Door table first.** Before blaming the grid for "not reachable", read the door table. Nocturne joined parts of one area by:
  - doors (movetype 1) to the same area;
  - touch warps (movetype 2, fired by walking in: the Amala Network);
  - ladders (movetype 5: a pair at the same x / z on two floors).
- **Arrival points:**
  - **Where they land:** they were named (`01pos_09`) but not readable in game, so they are learned when the player is moved, and logged. A ladder lands 150 in front of its partner along the partner's facing.
  - **Detecting a move:** horizontal and vertical jumps over a threshold.
  - **Taken with confirm:** after a ladder or door taken with confirm, say nothing; the game's prompt already said it.
  - **Touch warps:** say "Warped." only after them.
  - **Area entry:** ignore the first frames after an area change.
- **Warp graph:**
  - regions of the walking map are nodes;
  - door, ladder and warp pairs are edges (entry regions around each volume, landing region of its partner);
  - routes chain them ("via 2 ladders");
  - auto-walk takes each one ("Arrived. Confirm to take the ladder; the walk then goes on.").
  - Touch-warp volumes are kept out of every other route, with a margin, because players drift off a run.
- **Between areas:** doors gone through and areas visited give a graph, but in Nocturne it never produced a useful route. List landmarks of other parts "beyond doors" instead, and let the player choose.

## 7. Auto-walk and directions

- **Auto-walk (Home):** plan a route of straight runs.
  - First person uses the game's own turns (one tick of the turn pad, wait for the ease to finish and the turn delay to clear), then hold forward to each run's end.
  - Third person picks camera-relative pads every tick.
  - The world map holds compass pads.
- **Stops and re-plans:**
  - **Key:** any movement key stops the walk.
  - **Not exploring:** a battle or event ends it; Home continues afterwards.
  - **No progress:** re-plan; after several re-plans, "Blocked.".
- **Directions (End):** plan at the press and speak one sentence.
  - First person: relative to the facing ("Turn left, 12 metres, then right.").
  - Fixed camera: screen keys.
  - World map: compass words.
  - In reach: "In reach." or "In reach. Turn left to face it.".

## 8. Order to build in a new game

1. **Diagnostic build:** trace, change log and dump key. One walk with a recording.
2. **Offline reading** of the movement routine, the input checks and any view lock.
3. **Input proof:** hold forward for 500 ms and measure the distance; turn once.
4. **Where am I**, place names and the location banner.
5. **Walking map** with an offline replica from the start (section 2.4), checked against traces both ways.
6. **Points of interest** with reachability, then auto-walk, then directions on demand.
7. **Blobber snap and centring**, if the game has first person or a grid feel.
8. **Door table:** door kinds, explored doors per save file, empty boxes.
9. **Same-area links** (ladders, warps) and landmarks "beyond doors".
10. **World map**, with its own walking-map rules.

Each step ends with a test run; record what was verified with the session's log name.

## 9. Code map in Nocturne Access (for reuse)

All under `src/NocturneAccess/Systems/Navigation`:

| Class | What it does |
| --- | --- |
| `FieldState` | Exploring signal; per-routine counters |
| `FieldData` | Readers and log formatting |
| `CollisionCache` | Managed copy of the polygons; the game's switches |
| `WalkGrid` | Walking map, search, regions, floods, square offset, world-map rules |
| `PointsOfInterest` | The Pages list |
| `DoorTable` | The door warp table |
| `ExploredDoors` | Doors gone through, arrivals, "Warped." |
| `WarpMap` | Same-area links, regions graph, landings |
| `AreaRoutes` | Routes through other areas; off by default |
| `AutoWalk` | Home |
| `RouteGuide` | End |
| `WhereAmI` | Delete |
| `BlobberMode`, `CentreWalk` | Snap to squares; centring (F3) |
| `GridSense` | Wall and door cues |
| `PadInput`, `InputInjector` | The game's input |
| `FirstPersonView` | First-person flag answered in memory |

Diagnostics: `Capture/NavDump.cs`, `Capture/NavProbe.cs`, `tools/*.pl`.
