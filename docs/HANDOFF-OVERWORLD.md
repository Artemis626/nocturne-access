# Overworld handoff (the Vortex World map)

Written 2 Oct 2026 at the end of the dungeon chats, for a new chat dedicated to the overworld. Step 1 (the offline reading) is done and step 2 is built as 0.0.39: see the sections after the plan. Read `CLAUDE.md` first (binding rules), then this file, then `docs/HANDOFF-PATHFINDING.md` and `docs/PATHFINDING.md` for the dungeon tools this work reuses.

## Start here (state on 3 Oct 2026, after the 14:04 session)

- **Installed: 0.0.42 (step 4, built 3 Oct 2026 after the compact, not yet tested: "Step 4 built" below).** Before it, 0.0.41. Verified: 0.0.39 (Page Up / Page Down on the world map), 0.0.41 (world-map walking map, End in compass words: "solid enough to play the overworld", Francesco), and from 0.0.40 the title Continue screen. Not yet exercised: 0.0.40's battle-talk fix (no demon talk since).
- **Next: Francesco tests 0.0.42** (auto-walk on the world map, Shibuya from the south): read the log, then step 5.
- Then: step 5 the moon phase; step 6 explored entrances.
- Evidence: logs `session_2026-10-02_20-10-19`, `_21-03-43`, `session_2026-10-03_14-04-02` (`UserData/NocturneAccess/Logs`); world-map dumps `navdump_2026-10-02_20-26-27`, `_21-08-02`, `_21-11-41` (`NavDumps`); offline checker `tools/worldgrid.pl`; analysis helpers `tools/native/README.md` step 4.

## Where we are

- Francesco played the prologue and the Shinjuku Medical Center through to the overworld (2 Oct 2026, session `session_2026-10-02_20-10-19.log`), using the dungeon tools: points of interest (Page Up / Page Down), auto-walk (Home), directions (End), blobber mode (F4), explored doors, door kinds. All verified in dungeons (`docs/HANDOFF.md`, 0.0.17 to 0.0.38).
- He reached the overworld (Shinjuku), crossed it to Yoyogi Park with sighted-style trial and error, and asked for a dedicated chat: **make the overworld playable**.
- Material for that chat: navigation dump `UserData/NocturneAccess/NavDumps/navdump_2026-10-02_20-26-27.txt` (overworld, near the Medical Center), dump `navdump_2026-10-02_20-28-28.txt` (Yoyogi Park entrance, an ordinary dungeon), the session log above, and a video `%USERPROFILE%\Videos\Captures\2026-10-02 20-26-43.mp4` (117 s, overworld; frames can be pulled with `ffmpeg -ss T -i ... -frames:v 1`).

## What the overworld is (from the log, the dump and the video)

| Fact | Evidence |
| --- | --- |
| Shinjuku overworld is `fldGb.fieldID` 2, `areaID` 0. A battle started there switches to field 202 and back. Entering a place changes field (Medical Center 15, Yoyogi Park 16 area 17). | `[Nav:Change]` 20:26:18, 20:26:55, 20:27:48 |
| The mod treats "fieldID below 10" as the world map and turns its navigation off there: Page Up says "No points of interest here.", End "Directions are not available here.", Home refuses, blobber mode leaves. | `PointsOfInterest.Cycle`, `RouteGuide.Speak`, `AutoWalk` (two checks), `BlobberMode.BeforeCalc` |
| Movement is the overworld routine `fldPlayer.fldPlayerCalc_Wm2()` (also `_Wm3`, `_Wm4`), not `fldPlayerCalc_Nml` / `_Nml_syukan`: the trace shows `calc nml 0 syukan 0` while walking, and `fldPlayerCalc` still runs (so `FieldState.IsExploring` is true). Speed constant 16 per tick (NocturneModernController's dash mod, RVA 0x028C9E30). | `[Nav:Trace]` 20:26:46 onwards; Proxy `fldPlayer`; `research/refs/NocturneModernController/mods/NocturneModernDash/DashSpeedPatch.cs` |
| The player is a marker object `mark_wpc(Clone)`, seen from above. The camera never turns: yaw 359.909 in every trace line, so **screen up is north** and the game's compass (+Z north) matches the screen. | Dump "Player"; trace; video frames (top-down view, blue marker) |
| The ground is not flat: heights from y -2120 to -117 (Y points down), floor normals tilted (n.y about -0.91 with x and z components). 1158 collision polygons: 370 floor-like, 666 wall-like (buildings, edges), 5 blocks. | Dump "Collision" |
| Place names are spoken already: the location banner `fieldUI/flocation/stretch/TextTM` changes as the marker moves ("Shinjuku", "Shinjuku Gyoen", "West Shinjuku", "Yoyogi", "Yoyogi Kamizonocho", "Hatsudai", "Yoyogi Park", "Shinjuku Medical Center"); prompts "Enter" and "Talk" come through `fannounce`. | `[SPEECH]` 20:26:18 to 20:27:45 |
| **Entrances are door volumes, named by the game:** `PointsOfInterest.Build` (run by the dump) already lists them with their place names: "Shinjuku Medical Center door" (`01d_01`), "Yoyogi Park door" (`01d_02` and `01d_04`), "Shibuya door" (`01d_03`), "Gaienmae Station door" (`01d_05`); Souls (`01npc_01` to `_04`, model `shinentai_m`); an item box (`program_eve`, `TakaraIdx` 231, takara type 3). | Dump "Points of interest and walk grid" |
| **The door warp table works here too:** `01d_01` to field 15 (Medical Center), `01d_02` to field 16 area 17 and `01d_04` to field 16 area 20 (Yoyogi Park), `01d_03` to field 17 (Shibuya), `01d_05` to field 29 (Gaienmae Station). | Dump "Door warp table" (`DoorTable`, `docs/class-index.md`) |
| Big event volumes `01eve_07`, `01eve_08` (2000 x 1000, `mode2D` 2, facing each other) and `01eve_09` (230 x 1000) south-west: probably region boundaries or story triggers; not yet known. | Dump "Event volumes" |
| The walking map (`WalkGrid`, 229 x 270 cells of 30, one level at the player's height) marks every overworld point "not reachable from here": it was built for flat dungeon floors with steps of at most 45 units. | Dump "walk grid" |
| Kagutsuchi's moon phase is shown top left ("1/8", then "HALF") and is not read. Candidates (Proxy names, to verify and record before use): `evtMoon.evtGetAgeOfMoon()`, `evtGetAgeOfMoon16()`, `evtMoonAddMove(float leng)` (the moon advances with distance walked), `moonUI`, `dds3DefaultMain.SetMoonUI`. | Video frames; `research/native/symmap.txt` |

## Constraints that still apply (CLAUDE.md)

- Never move the player by writing positions; movement only through the game's own input checks, during the field update, at normal speed. The mod never presses confirm.
- Never touch `smt3hd.exe`. Read `GameAssembly.dll` offline only; some functions jump into protected code (`.sbss`): read the data they leave in memory instead, never call them.
- Never reconstruct text; never invent names (record every name in `docs/class-index.md` first). Single keys for player features; the existing keys (Page Up / Page Down, Home, End) should simply start working on the overworld.
- Cues never block or change input. Speech on demand (End), not running commentary (Francesco's rule from the dungeons).

## Proposed plan (one step per test run; Francesco decides the order)

1. **Read `fldPlayerCalc_Wm2` offline** (`tools/native/dis.sh 'fldPlayer.fldPlayerCalc_Wm2('`): which input it reads (`dds3PadManager` pad buttons as in dungeons, or analog only), its collision checks, and how the marker's facing works. This decides whether auto-walk can drive the overworld with `PadInput` as it does in third person.
2. **Points of interest on the overworld:** lift the "fieldID below 10" gate in `PointsOfInterest.Cycle` (and the dump already shows the list is right). Reachability: the dungeon grid fails on slopes; either a grid for the overworld (a step limit following the slope, or the floor polygons' own connectivity) or, first, list everything with straight-line distance and compass direction. Door kinds there: each entrance says where it leads (the table gives the field; the place name is already in the label).
3. **Directions (End) on the overworld:** with the camera fixed north-up, compass directions equal screen directions ("Up" = north), so End can speak "North-west, 40 metres" even before a walking map exists.
4. **Auto-walk (Home)** once step 1 shows how movement is read, with a walking map that follows the terrain.
5. **The moon phase** (a key or the banner), from game data, not the image.
6. Later: explored entrances, Souls and boxes the same way as in dungeons; the big event volumes once understood.

## Step 1 done: `fldPlayerCalc_Wm2` read offline (2 Oct 2026, overworld chat)

Read-only disassembly of `GameAssembly.dll` (never `smt3hd.exe`); new helpers in `tools/native/README.md` step 4 name the static classes and fields. Rows in `docs/class-index.md` "Field navigation".

- **The game's own world-map test is `fldGb.fieldID` below 10.** `fldPlayerCalc` runs `fldPlayerCalc_Wm2` then, instead of the dungeon routines. Two world maps so far: field 1, the prologue's Tokyo map (Francesco's 1 Oct 19:57 session: "Yoyogi-koen Station", Jinnan, Harajuku, people with "Talk", "Enter" at Yoyogi Park and the Medical Center; 290 collision polygons, nearly flat), and field 2, Shinjuku in the Vortex World.
- **Input: the first person's pad buttons.** `DDS3_PADCHECK_PRESS(L, R, U, D, cip 0)`, no analog stick. `PadInput` already reads and presses exactly these. Input is ignored while `fldGb.NoInpPlCnt` is above 0 or `fldPlayer.fldWmWarpMode` is not 0 (entering a place).
- **Directions are fixed to the world, not to the camera:** Up = north, Down = south, Right = east, Left = west, two keys = the diagonal in between (45 degrees). The camera never turns, so screen directions and compass directions are the same thing.
- **Movement is immediate:** 16 units per tick (about 4.8 of the mod's metres a second), straight in the key's direction, no turning or easing as in fixed-camera rooms.
- **Collision:** `fldHit.fldHitChkUnitVWM` with a radius of **30** on the world map (the dungeon walking map assumes 60 to 65), pushing the marker out of walls; slopes are followed through the floor normals.
- **Why the dungeon walking map failed there:** besides the radius, the world map's ground includes `Attr` 0x800 floors (the polygon under the player at the Medical Center is one; in dungeons 0x800 overlays reach past walls), many walls are tilted (|n.y| 0.2 to 0.5, ignored as walls by `WalkGrid`), and the terrain spans 2000 units of height.

Consequences for the plan: auto-walk on the world map is the simplest of the three modes (hold the pads of the compass direction, chosen again every tick, no camera to follow), and directions can name compass directions, which are also the arrow keys. Both need a walking map built for the world map's terrain and radius (step 3).

## Step 2 built: 0.0.39 (2 Oct 2026; verified 21:03 session)

- **Page Up / Page Down work on the world map** (fields 1 and 2): entrances, people and Souls, item boxes, nearest first, with straight-line distance and compass direction, as in dungeons.
- **Entrances** are named by the game's place pads, with the mod's word "entrance": "Yoyogi Park entrance, 52 metres, south-west. 5 of 10." Without a place pad: "Entrance". The big event volumes (`01eve_07` to `_09`) have no place pad and are not listed.
- **No "upstairs" / "downstairs"** on the world map: heights there are the terrain's slopes.
- **Everything is listed** there: the dungeon walking map would mark every point unreachable.
- End says "Directions are not available on the world map yet."; Home says "Auto-walk is not available on the world map yet."
- Also fixed: an area with no floor (field 15 area 19, outside the Medical Center) rebuilt the walking map and logged "Grid: no floor" on every frame in blobber mode (5426 of 7616 lines of the 20:10 log).

### Result (21:03 session, `session_2026-10-02_21-03-43.log`): verified

- Page Up / Page Down at the Yoyogi Park entrance (field 2), 10 points: "Yoyogi Park entrance, here. 1 of 10.", "Soul, 6 metres, north-west.", "Yoyogi Park entrance, 14 metres, east.", "Soul, 21 metres, south.", "Shibuya entrance, 28 metres, south-east.", "Soul, 33 metres, south-east.", "Gaienmae Station entrance, 51 metres, east.", "Soul, 51 metres, north-east.", "Item box, 58 metres, east.", "Shinjuku Medical Center entrance, 62 metres, north-east."; Page Down wraps to 10 of 10.
- End and Home spoke their "not available on the world map yet" lines.
- Dungeon tools unchanged in the Medical Center annex (Page Up, Home walks arrived, explored doors, empty boxes).
- Two world-map dumps for step 3: `navdump_2026-10-02_21-08-02.txt` (outside the Medical Center) and `navdump_2026-10-02_21-11-41.txt` (near Yoyogi Park). The 20:26 trace and the 21:11 walk (Medical Center to Yoyogi Park, `[Nav:Trace]` 21:11:18 to 21:11:44) are the ground truth for the walking map.
- Not reached: the prologue map (field 1).

### Test (0.0.39)

1. Load a save on the Shinjuku world map (or leave the Medical Center). Press Page Up: "Shinjuku Medical Center entrance, 2 metres, north-west. 1 of 10." (the numbers depend on where you stand).
2. Page Up again through the list: a Soul, an item box, "Gaienmae Station entrance", two "Yoyogi Park entrance" (two different entrances of the park), "Shibuya entrance", more Souls. Page Down goes back.
3. Pick "Yoyogi Park entrance" and walk with the arrow keys or W A S D in the direction it names (Up = north, Right = east, two keys for diagonals). Page Down then Page Up (back to it) to hear the new distance and direction.
4. End: "Directions are not available on the world map yet." Home: "Auto-walk is not available on the world map yet."
5. Enter a place and come out: the list works again on the world map, and inside dungeons Page Up, End and Home work as before.
6. If you reach the prologue map again (a new game), Page Up there too: entrances and "Person".
7. Shift+F6 once on the world map, so the dump shows the list as spoken.

## Step 3 built: 0.0.41 (2 Oct 2026; verified 3 Oct 14:04 session): a walking map for the world map, End

Checked offline first (`tools/worldgrid.pl`, the Shinjuku dumps of 20:26 and 21:11 and the 92 positions of the 20:26 and 21:11 walks):

- **Why the dungeon map failed:** it counted the tall vertical `Attr 0x800` planes as walls. They are invisible region boundaries (the marker passed 3 units from them) and cut Shinjuku into pieces. Its clearance (65) is also twice the marker's.
- **The game's rule, measured:** the marker stays about 30 from a wall's plane (13 of 18 close positions at 29 to 30), but rounds block corners closer and squeezes through lanes: west of the Medical Center your route runs down a lane whose free band is a few units wide, passed at 26 to 27.
- **The world-map walls in `WalkGrid` (fieldID below 10):** every non-floor polygon of `Attr` 0 (and NPC boxes), tilted ones included; a 30-unit cell is walkable when its centre or a point 6 or 12 off it clears every wall by 22, measured 15 above the floor. Floors, levels and steps as in dungeons.
- **Calibration:** 22 puts all 92 traced positions on reachable cells and every Page Up point within 6 to 35 units of a reachable cell (Gaienmae Station and the second Yoyogi Park entrance included). 24 already breaks your lane; with the plain centre test even 15 left the south cut off.
- **Page Up / Page Down** use this map again on the world map: only reachable points are listed, as in dungeons (0.0.39 listed everything).
- **End** on the world map: the route spoken in compass words, which are the arrow keys there: "South-west, 12 metres, then south." A sidestep shorter than 90 units between two runs of one direction (a slanted lane drawn on the grid) is folded into them; holding the main key slides along the wall. "In reach." needs no facing on the world map. The first-person setting kept from dungeons is ignored there.
- Home still says "Auto-walk is not available on the world map yet." (step 4).

### Test (0.0.41)

1. On the Shinjuku world map, Page Up: the same 10 points as before should still be listed (if one is missing, the log says "not reachable, left out": that is a calibration miss).
2. Select "Yoyogi Park entrance" (the nearer one when you stand at the Medical Center) and press End: "West, N metres, then ..." or similar. Walk with the arrow key it names; press End again whenever you like: the route is planned again from where you stand.
3. Follow End to the entrance until it says "In reach." and the game says "Enter".
4. Try End towards a Soul and towards Shibuya.
5. Shift+F6 once on the way: the dump shows the world map as text, with the planned route.
6. In a dungeon, End and Home work as before.

### Result (3 Oct 2026, `session_2026-10-03_14-04-02.log`, 0.0.41): verified, one miss

- Francesco: "directions are solid enough already to play the overworld". End led him from the Yoyogi Park entrance to Soul `01npc_04` ("North, 1 metre, then north-west." ... "In reach.", then "Talk"), back to the entrance ("South-east, 5 metres, then south." ... "In reach.", then "Enter"), and 21 metres south to Soul `01npc_02` ("South, 11 metres, then south-west." ... "In reach.").
- World-map walking map built in 10 to 21 ms (229 x 270 cells, 749 world-map walls, 31 191 walkable places). Page Up kept all 10 points from the Yoyogi Park entrance.
- **Miss (to fix with step 4): Shibuya entrance "not reachable, left out" at 14:10:28** when standing at Soul `01npc_02` (-2957 -3378, y about -1420). Likely cause: `PointsOfInterest.VolumeSpan` takes the player's height as the floor to reach when it lies within the volume's height span (made for the Medical Center stairwell). Shibuya's volume `01d_03` is 400 tall (box 300 x 400 x 300), so from the low south the target floor became the player's height, about 400 below the floor at the entrance (offline: nearest reachable cell 8 from it, height +94 over the volume centre + 100), outside `TargetBand` 200. Fix: on the world map use the floor under the volume (the box bottom, or `CollisionCache.FloorUnder` at the volume centre), never the player's height. Check offline with `tools/worldgrid.pl` first.
- Seen, not a bug: a first leg of 1 metre is spoken ("North, 1 metre, then north-west.") when the player stands just off the route; fine for now.
- Also verified in this session (0.0.40): the title Continue screen reads its slot ("Art, Level 8, Yoyogi Park-West Plaza, NORMAL"). The doubled battle-talk fix was not exercised (no demon talk).

## Step 4 plan: auto-walk (Home) on the world map (built as 0.0.42, see "Step 4 built")

Francesco (3 Oct 2026): with no footsteps or other audio feedback on the world map, auto-walk is even more essential there than in dungeons; build it from the information available. Everything below is already known; no new game names are needed except the ones listed (all in the Proxy and `docs/class-index.md`).

**Facts it rests on** (step 1, `docs/class-index.md` "Field navigation"):
- `fldPlayer.fldPlayerCalc_Wm2()` (static, returns int; Proxy `NativeMethodInfoPtr_fldPlayerCalc_Wm2_Private_Static_Int32_0`) is the world map's movement; `fldPlayerCalc` calls it only when movement is allowed (`fldProcess.fldProcCheckFlag(0x40)`) and `fldGb.fieldID` is below 10.
- It reads `DDS3_PADCHECK_PRESS(L, R, U, D, cip 0)`: exactly the buttons `PadInput.Hold` already presses (the `DDS3_PADCHECK_PRESS` postfix, active only inside `fldPlayerCalc`). No analog stick.
- **Directions are fixed to the world:** U = north (+Z), R = east (+X), D = south, L = west; two buttons give the 45-degree diagonal (D+R south-east, U+R north-east, U+L north-west, D+L south-west). No camera, no facing, no easing: 16 units per tick straight in that direction. So the pad for a compass bearing b is simply the combination for round(b / 45): the existing `Combos` table indexed by bearing / 45 with camera yaw 0 (the world-map camera yaw is 359.9, so `Press(bearing)` already picks the right pads; still, do not depend on the camera there: use yaw 0).
- Pads are ignored while `fldGb.NoInpPlCnt` is above 0 or `fldPlayer.fldWmWarpMode` is not 0 (entering a place): the walk must not count that as "blocked".
- Collision slides the marker along walls (radius 30); the walking map (0.0.41) keeps routes 22 clear and folds no jogs for walking (the runs are the grid's 8-direction runs).
- Arrival is the game's own: doors through `fldEveHit.OldHitDoor` (it went -1 -> index on entering the volume, "Enter" prompt), people through `OldHitInf` / `gFldEveRngIdx` ("Talk"). No facing on the world map (the marker has none; prompts fire on entering range: logs 1 Oct 19:57 and 3 Oct 14:07).

**Changes to make (in `AutoWalk`, `FieldState`):**
1. `FieldState`: count `fldPlayerCalc_Wm2` runs (`WmCount`, a prefix like `BeforeNml`, `Type.EmptyTypes`); record it in `docs/class-index.md`. `AutoWalk`'s idle test (`moves = SyukanCount + NmlCount`) must add `WmCount`, otherwise every world-map walk ends after 3 ticks with "Walk interrupted by an event.". When Wm2 stops running (an event, the "Will you leave the area?" or "Enter" dialogue, a battle) the walk ends as in dungeons.
2. `AutoWalk.TryStart`: drop the world-map refusal ("Auto-walk is not available on the world map yet."). `_firstPerson` must be false on the world map (the mod's in-memory first-person state carries over from dungeons; `RouteGuide` already does `!world && ...`).
3. `AutoWalk.BeforeCalc`: the stop test `gb.areaID != _area || FieldState.WorldMap(gb)` must become a field or area change (`_field` too): a random battle switches field 2 -> 202 and must end the walk ("area change"), coming back must not resume it silently.
4. World-map pressing: a `PressWorld(bearing)` holding `Combos[round(bearing / 45) mod 8]` (no camera), used by runs, the off-run correction (the neighbouring 45, as in third person) and `Approach`. `NextRunLead` 20 is fine (no easing: could be 8, one half tick).
5. Arrival on the world map: in reach (`InReach`) ends the walk at once, "Arrived.", no facing step (`FaceTowards` must never run there; it would press towards the target and walk past). Doors: the front logic (`HasFront`, `Lateral`) can stay, it only picks the aim point.
6. Still and off-run: keep `StillTicks` 8 and re-plans, but do not count ticks where `fldGb.NoInpPlCnt > 0` or `fldPlayer.fldWmWarpMode != 0` as still (read both; they are Proxy names, offsets 0x80 and static 0xB8).
7. Timeout: 30 s is short for the world map (Shinjuku is about 70 x 80 metres, 4.8 metres a second; Medical Center to Yoyogi Park took about 26 s). Use 90 s on the world map.
8. Speech as in dungeons: "Walking to Yoyogi Park entrance, 52 metres, south-west." at the start, "Arrived." (then the game's "Enter" / "Talk"), "Auto-walk stopped." on any arrow / WASD key (`RealMovementKey` already reads U, D, L, R with cip 0), "Walk interrupted by an event.", "Blocked.", "No path.".
9. Fix the Shibuya miss above in the same version (world-map target floor).
10. Log `[Nav:Walk]` lines as in dungeons (plan, runs, pads, end), so one test run is enough to check.

**Test for step 4:** Page Up to "Yoyogi Park entrance" from the Medical Center, Home: "Walking to ...", the marker walks the lanes without a key, "Arrived.", "Enter". Then Home to a Soul, Home to Shibuya from the south, Home and press an arrow key midway ("Auto-walk stopped."), Home through a random battle (the walk ends; Home again afterwards). Dungeons: Home as before.

## Step 4 built: 0.0.42 (3 Oct 2026; not yet tested)

As planned above, with these details:

- `FieldState`: a prefix on `fldPlayer.fldPlayerCalc_Wm2` counts its runs (`WmCount`); `MoveCount` (all three movement routines) is `AutoWalk`'s idle test. Without it every world-map walk would have ended after 3 ticks as "interrupted by an event".
- `AutoWalk.TryStart`: no world-map refusal; `FirstPersonNow` is false on the world map (also for the "view changed" check, so a first-person state kept from a dungeon cannot stop the walk).
- `AutoWalk.Press`: on the world map the camera is not read (yaw 0), so the pads are `Combos[round(bearing / 45)]`, U = north. Runs, the off-run correction (the neighbouring 45) and the final approach all go through it.
- Arrival on the world map: `InReach` (the game's hit index is the target: `OldHitDoor` for entrances, `OldHitInf` / `gFldEveRngIdx` for Souls) ends the walk at once with "Arrived."; the facing step never runs there.
- Stop: a field or area change ("field change" in the log; a random battle 2 to 202). Ticks with `fldGb.NoInpPlCnt` above 0 or `fldPlayer.fldWmWarpMode` not 0 are not counted as still. Timeout 90 s on the world map (30 s in dungeons).
- Log: `[Nav:Walk] Plan (start, world map) ...`, the pad lines say "camera yaw 0".
- **Shibuya fix** (`PointsOfInterest`): on the world map an entrance's target floor is the ground under its volume centre (`CollisionCache.FloorUnder(..., groundOnly: true)`, the `Attr` 0 floors the walking map uses, the one nearest the centre's height), else the box bottom; never the player's height. Offline (`tools/worldgrid.pl` with `FLOOR=under`, dump `navdump_2026-10-02_21-11-41`, 133 traced positions of the 20:10, 21:03 and 14:04 sessions, all on reachable cells): ground under the centre vs the nearest reachable cell: `01d_01` +8, `01d_02` +1, `01d_03` (Shibuya) +2, `01d_04` +2, `01d_05` -8. The box bottom alone was 85 off at `01d_04`, so the ground is the better target.

### Test (0.0.42)

1. Load a save on the Shinjuku world map. Page Up to "Yoyogi Park entrance" (or any entrance), then Home: "Walking to Yoyogi Park entrance, N metres, west." (your numbers). Do not touch the keys: the marker walks by itself; when it gets there: "Arrived.", then the game's "Enter". Confirm with Enter as usual if you want to go in.
2. Page Up to a Soul, Home: "Walking to Soul, N metres, ..." then "Arrived." and "Talk".
3. Go south to the Soul there (or anywhere south), Page Up: "Shibuya entrance" must now be in the list. Home to it: "Arrived.", "Enter".
4. Start a long walk with Home and press an arrow key midway: "Auto-walk stopped." and you control the marker again at once.
5. If a random battle starts during a walk, it ends the walk silently; after the battle press Home again to carry on.
6. Shift+F6 once on the world map during the session.
7. In a dungeon: Home as before (third person and first person).

If a walk says "Blocked." or "Walk interrupted by an event." on open ground, say where: the log has every pad and re-plan.

## Plan from here (one step per test run)

3. Done in 0.0.41 (above).
4. **Auto-walk (Home)**: built as 0.0.42, to test ("Step 4 built").
5. **The moon phase** from game data (the HUD draws it as images; no text in the log): candidates `evtMoon.evtGetAgeOfMoon()`, `evtGetAgeOfMoon16()`; to read offline first, never call protected code.
6. Later: explored entrances, the big event volumes.

## Open items from the last dungeon session (not overworld)

- A demon conversation in battle (Shikigami, 20:20) spoke two lines twice about half a second apart ("[game line removed]", "You obtained Life Stone x 1."): a reader speaks the same page twice; to look at in the dialogue or announcement reader.
- The Annex 1F ramp item box (`docs/HANDOFF.md`, `WalkGrid` `Layers` = 3 hypothesis).
- Custom waypoints (`docs/EXPLORED-DOORS-PLAN.md` item 4): recorded, not to build until Francesco asks.
