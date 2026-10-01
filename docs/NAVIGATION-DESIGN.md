# Navigation and pathfinding: research and design

Written 1 Oct 2026 for Francesco, analysis only (no code). Scope: 3D dungeons (field), not the world map. Every name below comes from the proxy decompile (`research/decomp/Assembly-CSharp`, marked Proxy) or the MIT reference NocturneModernController (marked Ref); none is used in code yet, and each meaning marked "to verify" needs one diagnostic run.

## 1. Verdict

- **The game exposes everything a navigation system needs as data**: the player's position and facing, the area and place it is in with the game's own place names, every wall and floor polygon of the loaded area, the auto-map's room graph with icons and explored flags, every NPC, item box and event volume (doors, triggers) with positions.
- **Movement can be driven natively.** NocturneModernController injects the game's own logical actions (`FD_Turn_Left` / `FD_Turn_Right`) through `SteamInputAssign.padcheck` and confirmed it live in dungeons. The game's collision, doors and events then behave exactly as with a real key.
- **First person exists but is locked to New Game+** (Francesco, 1 Oct 2026: V, "Toggle POV" (`FD_Subjectivity`), does nothing on a first playthrough). The field code has a separate first-person movement routine (`fldPlayerCalc_Nml_syukan`, "syukan" = subjective view). The mod must unlock it (section 5); what it allows (walking or only looking) is tested after that.
- **A blobber mode is feasible**, as a layer over the free 3D movement rather than a rewrite of it: the mod lays a grid on the game's own collision, walks the player cell to cell with injected input, and turns in 90 degree steps. A* on that grid then matches every instruction exactly ("forward 3, turn right, forward 2"). The main risk is geometry that is not grid-shaped (diagonal or curved rooms, later Amala areas); the design below handles it with waypoint cells instead of a strict tile map.

## 2. What the game gives us

### 2.1 Player position, facing, state

| Source | What | Origin |
| --- | --- | --- |
| `fldGlobal.fldGb` (`fldGlobalWork_t`, static) | `playerX`, `playerY`, `playerZ`; `playerOldX/Y/Z`, `playerNewX/Y/Z`; `PlayerKakudo` (facing angle), `PlayerKakudoGoal`; `PlayerSaka` (slope) | Proxy |
| same | `fieldID`, `areaID`, `blockID`, `fieldOldID`, `areaOldID`; `AmSBlock`, `AmFloor` (auto-map block and floor); `ElvIdx` (elevator); `cammode`, `cammode2`, `syukan_cam_reset_flg`; `NoEncountCnt`, `EncPID` | Proxy |
| `fldPlayer` (static) | `fldPlayerObj` (GameObject), `playerRun`, `fldPlayerAct`, `gKeyInputDir`, `fldCamKakudoGoal`, `gFldPlayerSyukanDly`; `fldPlayerCalc()` (field update, dispatches to `fldPlayerCalc_Nml()` or `fldPlayerCalc_Nml_syukan()`), `fldPlayerCalc360_4(float)` and `fldPlayerCalc360_8(float)` (angle to 4 or 8 directions) | Proxy; dispatch Ref (CONFIRMED there) |
| Exploration signal | "Exploring" = `fldPlayer.fldPlayerCalc` ran within the last 100 ms (Harmony postfix) | Ref, used by four of its mods |
| `fldCamera` (static) | `fldCameraObj`, `CameraMoveDir`, `fldCamDirLockRotY`, `calcCamSyukan()`, `fldCamSyukanSuriawase()`, `fldHitChkCamera` user | Proxy |

To verify: the unit of `PlayerKakudo` (degrees or radians) and its zero direction; `cammode` values in third and first person; whether `fldGb.playerX/Z` equals `fldPlayerObj.transform.position`.

### 2.2 Place names (game text, so they can be spoken)

| Source | What | Origin |
| --- | --- | --- |
| `fldGlobal.area_name` (`fldAreaName_t`: `_fld`, `_area`, `_name`), `basyo_name` (`fldBasyoName_t`: `_fld`, `_basyo`, `_name`), `floor_name` (`fldFloorName_t`: `_fld`, `_floor`, `_name`), `field_name` | Area, place ("basyo") and floor names by ID | Proxy |
| `fieldUI/flocation` | The location banner the mod already speaks ("Underground Facility", "Terminal") | Log |
| Collision faces carry `Basyo` (place ID) | So every floor polygon maps to a game place name | Proxy, to verify |

### 2.3 Geometry: walls and floors

| Source | What | Origin |
| --- | --- | --- |
| `fldHit.gfldCollision` (`collision_s[]`), `gfldCollsionNum`, `gfldCollsionBlkNum` | Every collision polygon of the loaded area | Proxy |
| `collision_s` | `mode2D`, `numedges`, `normal`, `num` (plane), `e_normal[]`, `e_num[]` (edge planes), `min[]`, `max[]` (bounding box), `point[]`, `elv`, `blockID`, `hitflag`, `resID`, `pFace`, `npcID` | Proxy |
| `fldResCollisionPolyFace_t` (`pFace`) | `Attr`, `Myuka`, `Se` (footstep sound), `Basyo`, `Amap0`, `Amap1` (auto-map flags), `EncType`, `EncIdx` | Proxy |
| `fldHit.fldHitChkCamera(Vector4 eyepos, Vector4 disppos, ref Vector4 hitpos)` | The game's own segment-against-walls test (it keeps the camera out of walls): "is there a wall between A and B, and where" | Proxy |
| `fldHit.HitPositionInside2/3(...)`, `fldHitChkUnit(Vector4)` | Point-in-polygon with radius; the player's own collision step | Proxy |

To verify: which polygons are walls and which are floors (`mode2D`, `Myuka`, the normal's Y), and whether calling `fldHitChkCamera` from the mod is safe (it may use the game's per-frame state). Plain C# over `collision_s` (bounding boxes, edge planes) is the fallback and needs no native call.

### 2.4 The auto-map: a ready room graph

| Source | What | Origin |
| --- | --- | --- |
| `fldAutoMap.pfldAmArea` (`fldAmAreaData_t[]`: `pName`, `pSB`, `nSB`, `pPos`), `nfldAmArea` | Areas of the current field | Proxy |
| `fldAmSBlock_t` | Sub-block: `pName`, `floor`, `pPos0`, `pPos1` (bounding box), `pIcon[]` / `nIcon` | Proxy |
| `fldAmIcon_t` | `type`, `pPos` (x, y, z): the auto-map icons (doors, terminals, stairs and so on; type values to map) | Proxy |
| `fldAutoMap.fldAutoMapChkOpen(fld, area, block)`, `gAmap_NowField`, `gAmap_NowArea`, `gAmap_NowFloor` | Explored flags and where the map is | Proxy |

This gives "rooms" (sub-blocks with boxes) and their connections (shared edges, door icons) for free: the coarse level of the path planner, and good spoken landmarks.

### 2.5 Entities

| Source | What | Origin |
| --- | --- | --- |
| `fld_Npc.fldNpc` (`fldNpc_tag[]`: `x`, `y`, `z`, `rot`, `name`, `Type`, `ItemBoxIdx`, `fldObj`), `fldNpcNum` | NPCs, with position | Proxy |
| `fldTakaraWork_tag` (`x`, `y`, `z`, `r`, `type`, `TakaraIdx`), `FldHitData._fldItemBoxTbl` | Item boxes ("cube boxes") | Proxy |
| `fldEveHit.gEvnRng` / `gEvnRngNum`, `gEve` / `gEveNum` (`fldResHead_t`: `resourceName`, `sisei.pos`, `resourceLink` with `data[].name`, `areaname`), `gEvnRngBox` (`putevebox_s`: box vertices, `type`, `TakaraIdx`), `gProgramEveHit` | Event volumes: doors, area exits, story triggers, item and NPC ranges | Proxy |
| `fieldUI/fannounce` | "Open Door", "Talk", "Examine", "Enter", "Exit": spoken already, the confirmation that the player is in front of something | Log |

To verify: which event names or types are doors and exits, and where a link leads (`resourceLink` names against `area_name`). Spoken labels use the game's text when it exists (place names, prompts) and the mod's own category words otherwise ("door", "box", "person"), never invented names.

### 2.6 Driving movement

| Source | What | Origin |
| --- | --- | --- |
| `SIActionName` | `CMN_UpFront`, `CMN_DownBack`, `CMN_Left`, `CMN_Right`, `CMN_EnterAction`, `FD_Turn_Left`, `FD_Turn_Right`, `FD_Return_Front`, `FD_Subjectivity`, `FD_Automap`, `FD_Camera_*` | Proxy |
| `SteamInputAssign.padcheck(int pad, SIActionName action, SIPressType type)` | Logical input check; a postfix that turns the result true for an action is a native-equivalent press | Ref (live-confirmed for `FD_Turn_*` in dungeons; the mod's earlier native-hook attempt crashed and was dropped) |
| `SteamInputUtil.PRESS / TRIG / REP(SIActionName)` | The same check for the current pad | Ref |
| Movement step constants 29 and 20 (dungeon), 16 (world map) per field tick | Speed; the Ref notes that dungeon doors are thin volumes and that more than 1.6 times the speed can skip through them | Ref |

To verify: whether walking (`CMN_UpFront`) goes through `padcheck` like turning (keyboard W likely does; an analog stick may not), and what A and D do in first person (strafe or turn).

### 2.7 Not general, but worth knowing

`fldTest` holds a grid route table for one scripted chase (`FroFtoI`, `FroItoF`, `FroGetRouteIdx(sx, sz, gx, gz)`, `FRO_ROUTEDATA_t`), loaded only for that map. It shows the developers themselves discretised a dungeon into a grid for an AI; it is not usable as our general path data.

## 3. Two ways to build navigation

### 3.1 Assistive layer over free movement (lower risk)

The player keeps the game's normal controls. The mod adds:

1. **Where am I**: place name (from `Basyo` and `basyo_name`), floor, facing as a compass direction ("Medical Center lobby, facing north").
2. **Wall sense**: distance to the nearest wall ahead, left, right and behind, from the collision polygons; optional audio cue as a wall gets close.
3. **Scanner**: nearby doors, boxes, people, exits, terminals, with direction relative to facing and distance ("door, ahead left, 6 steps").
4. **Path guidance**: A* to a chosen target; spoken directions updated as the player moves ("go forward, then turn right").

Weakness: directions against free analog movement are approximate ("slightly left"), and Francesco has to steer by ear.

### 3.2 Blobber mode (the ambitious option)

Like 90s Shin Megami Tensei or Strange Journey: the world moves in cells and 90 degree turns, seen in first person.

- **Grid**: cells of a fixed size (to measure: about one corridor width, likely close to the walk speed times a few ticks) laid over the area. A cell is walkable when the player's collision circle fits there (same test as the game: polygons with their edge planes). Edges between cells are open when the segment between their centres hits no wall (`fldHitChkCamera` or our own test). Cells snap to the room boxes of the auto-map where possible, so corridors line up.
- **Moves**: W = one cell forward, S = one cell back (or turn around, Francesco's choice), A / D = turn 90 degrees, optional strafe. Each move is executed by holding the game's own logical actions through `padcheck` until the player reaches the next cell centre or faces the new direction (`fldPlayerCalc360_4` gives the native 4-direction snap), then stops. The game's collision, slopes, doors and events stay in charge, so nothing can be skipped (normal speed only).
- **The game's own keys are reinterpreted only while blobber mode is on** (the real W press is suppressed in `padcheck` and replaced by the step); a mod F-key toggles the mode, so the free controls are always one key away.
- **First person view** (`FD_Subjectivity`) is switched on with the mode, for players with some sight; speech does not depend on it.
- **Each step speaks** what changed: "corridor", "door ahead", "opening on the left", "wall" (a bump sound when a move is blocked), the place name when it changes.
- **A* on the same grid** gives instructions that are exactly executable ("forward 3, turn right, forward 2"), and an auto-walk can execute them step by step, stopping for battles, dialogue, prompts or any key press.

Why it fits Nocturne: its dungeons (Shinjuku Medical Center, Ginza, Ikebukuro tunnels, Amala) are mostly corridors and rectangular rooms, the auto-map is already block-based, and doors and events are point volumes reached by facing them.

Risks and answers:

| Risk | Answer |
| --- | --- |
| Diagonal or curved geometry; open areas | Waypoint cells: a cell is a point the player walks to, not a tile; diagonal edges allowed where the straight line is clear. Fall back to free movement with guidance (3.1) where the grid is poor. |
| Thin door and event volumes | Normal speed only; a step that crosses an event volume behaves exactly like walking by hand. |
| Random encounters and events mid-walk | Exploration signal stops: the step or auto-walk aborts and resumes only on a key press. |
| Analog movement not routed through `padcheck` | The test below decides; the fallback is steering by angle while holding forward. |
| Several floors, slopes, stairs, elevators | Grid per floor (`AmFloor`, `playerY`); stairs and elevators are graph edges announced by their prompt. |
| Moving NPCs and pushable objects | Re-check the next cell before each step. |

## 4. Plan (each step ends with a test run)

1. **Diagnostic run, read only (next session).** A key dumps the navigation data to the log: player position, facing, `cammode`, IDs and resolved place names; counts and samples of collision polygons with their face flags; auto-map areas, sub-blocks and icons; NPCs, boxes and event volumes with names and positions. Francesco walks a known route in Shinjuku Medical Center, turns in place, presses V once so the first-person gate is logged (section 5, step 1), and opens a door.
2. **First-person unlock (section 5, step 2), then input proof of concept.** Inject `CMN_UpFront` for 0.5 s and `FD_Turn_Right` until 90 degrees, only while exploring; confirm collision and doors behave natively, in third and first person.
3. **Read-only aids** (3.1 items 1 to 3): where am I, wall sense, scanner. Useful alone, and they validate the geometry reading.
4. **Grid and A*** with spoken directions (no auto-walk).
5. **Blobber mode** with step moves, then auto-walk.

**Decisions (Francesco, 1 Oct 2026):** blobber mode is a **toggle, on by default**, switched with one key, so later areas and personal preference can fall back to free movement. Still open: S as step back or turn around; how much speech per step; the toggle key (F3 and F4 are free; F5 is the navigation dump since 0.0.12).

## 5. Unlocking first person (New Game+ only)

Francesco's test (1 Oct 2026, `session_2026-10-01_10-23-24.log`): pressing V in the Underground Facility on a first playthrough does nothing; the game keeps first person for New Game+. The mod unlocks it in memory only, never in the save.

Leads (Proxy and Ref):

- `EventBit.evtBitCheck(int no)` (bool), `evtBitOn(int)`, `evtBitOff(int)`: the game's story-flag check. The MIT reference mod EarlyCompendium unlocks the Compendium early with a Harmony postfix on `evtBitCheck` that returns true for flag 40 only; nothing is written to the save. Same technique here.
- `dds3GlobalWork` (static): `NewGame2_SYS_ENDING0` .. `NewGame2_SYS_ENDING7`, `NewGame2_SYS_ZENSHO_ALL`, `NewGame2_EV_ZENSYO_ALL` (int: likely the flag numbers of "ending N seen"), `NewGame2_endcnt` (short: endings count), `NewGame2Push()`, `NewGame2Pop()`. Reference BetterNewGamePlus patches the last two.
- `dds3ConfigMain.cfgGetBit(uint)`: config bits (Ref AnalyzeBosses reads `cfgGetBit(9)`), a possible second gate.

Method (two short runs):

1. **Find the gate.** Postfix on `evtBitCheck` that logs each flag number checked (with its result) during the field update right after V is pressed, plus the values of the `NewGame2_*` fields at load. The flag that answers false only at that moment is the gate. If no flag is checked, log `NewGame2_endcnt` and `cfgGetBit` calls the same way.
2. **Force it, narrowly.** Return true for that flag only while `fldPlayer.fldPlayerCalc` runs (a prefix sets a flag, the postfix clears it), so other New Game+ content (bosses, items, Dante) stays locked. If the gate is a field (`NewGame2_endcnt`), set it in the prefix and restore it in the postfix. Never call `evtBitOn` (it would be saved). A config option `[NocturneAccess_Navigation] UnlockFirstPerson = true`.

Then test: V switches to first person; walk, turn, open a door; note what A and D do.

## 6. Verified by the 0.0.12 diagnostic (1 Oct 2026, session 12:04, Underground Facility, field 15)

Source: `session_2026-10-01_12-04-33.log` and `UserData/NocturneAccess/NavDumps/navdump_2026-10-01_12-05-56.txt` .. `12-07-54.txt` (corridor, in front of the Terminal door, back from the Terminal, in front of the Healing Room door, inside the Healing Room).

### Space and facing

- `fldGb.playerX/Z` = player object `transform.position` x 100, same signs. **Y points down** in game coordinates: floors at y 0 have normal (0, -1, 0), walls run from y -400 to 0; `playerY` is -2 (Unity y 0).
- `PlayerKakudo` is in **degrees**: 0 faces -Z, 90 faces +X, 180 faces +Z, 270 faces -X. Forward vector in game XZ = (sin k, -cos k). Unity yaw = 180 - k (mod 360).
- Keyboard movement is camera-relative and snapped to 8 directions: `PlayerKakudoGoal` jumps to a multiple of 45 and `PlayerKakudo` eases to it in about 0.5 s.
- `fldPlayer.gKeyInputDir`: -1 none, 0 forward, 2 right, 4 back, 6 left (1, 3, 5, 7 the diagonals), relative to the camera. `fldPlayerAct`: 0 idle, 1 moving, 5 at a door. `playerRun` true while moving.
- The field update runs 30 times a second; walking moves 29 units per tick (about 232 per 250 ms), the Ref's step constant 29. The player stops 60 units from a wall (collision radius 60).

### Geometry

- 212 collision polygons in the corridor area, 155 in the Healing Room, all quads; reading them takes 0 to 5 ms. `gfldCollision` has 2048 slots; `gfldCollsionNum` is the live count.
- `mode2D` 0 = wall facing X, 2 = wall facing Z, 1 = horizontal. **Everything is axis-aligned on a 300-unit lattice**: corridors are 600 wide with walls at centre ± 300; corridor and door centres sit on multiples of 600 (x -7200, -5400, -9000, z -4200). Natural blobber cell: 600, centres on multiples of 600.
- Real floors and walls have `Attr` 0. `Attr` 0x800, 0x1000, 0x8000 are horizontal overlays at y -10 or -43 (not walls; meaning to find, `Amap0` 2..9 and `Spc` 1/2 on some). `Attr` 0x80 with `npcID` 0..1 are 100 x 100 boxes around NPCs and item boxes. `hitflag` 1 marks the polygons last touched. `Myuka`, `Se`, `EncType` are 0 everywhere; `Basyo` is 0 on real floors.
- Doors are closed walls in the collision: a doorway is crossed only by the door event (prompt "Open Door", confirm, area load).

### Names

- `fldGlobal.area_name`, `basyo_name`, `floor_name` have 400, 336 and 224 slots but **every entry is null** in this build; `field_name[15]` is null too. Spoken place names must come from the location banner (`fieldUI/flocation`), which the mod already speaks. The banner names the **destination** as soon as the player enters a door volume ("Terminal", "Healing Room", just before "Open Door").

### Auto-map

- `pfldAmArea` holds 22 areas for field 15 (`pName` "001".."022"); `gAmap_NowArea` = `areaID` (5). Sub-block boxes are relative to the area: **world box = sub-block box - area `pPos`** (area 5 pos 3600, 0, 2700; sub-block "s06" -3900..-3300 x, -1200..0 z = the corridor -7500..-6900, -3900..-2700). `AmSBlock` = sub-block index + 1; `blockID` matches the sub-block index here. Sub-block `floor` is a floor number; names are "s01".."s16" (not speakable). Some sub-blocks carry only an icon and a garbage box (1.3e10). Icon types 1 to 10 seen; meanings to map.

### Entities

- `fldEveHit.gEvnRng` (12 in the corridor area): **type 10 with names `NNd_MM` are doors**: boxes 300 wide, 100 deep, 200 tall standing in the doorway, `boxnml` pointing into the room, `sisei.pos` at the door centre. `NNeve_MM` are story triggers (same shape, some rotated 45 degrees). Type 0 entries with `npcIdx` are NPC talk ranges. `resourceLink` is empty: the destination is not in the data (the banner gives its name).
- Entering a door volume sets `gFldEveRngIdx` and `OldHitDoor` to its index; leaving resets them to -1.
- `fld_Npc.fldNpc`: people (`shinentai(Clone)` objects, positions in game units, `csize` 30) and **item boxes** (Type 999, `ItemBoxIdx`, object `itembox1(Clone)`); `gfldTakaraWork` stayed empty. `fldEveHit.gProgramEveHit` holds the NPC talk balls (r 160 to 180). `gEve` entries all sit at the origin: not positional.

### Input

- `SteamInputAssign.padcheck` (pad 0) is asked **every field tick** about `CMN_UpFront`, `CMN_DownBack`, `CMN_Left`, `CMN_Right`, `FD_Turn_Left/Right`, `FD_Camera_*`, `FD_Return_Front`, `FD_Automap`, but it **never answered true** for Francesco's keyboard (W, A, S, D, Q, E): the keyboard reaches the game another way. So the player's own input is read from `gKeyInputDir`, not from padcheck. Injection through padcheck is still the plan (the game asks every tick); step 2 proves it.
- Q and E rotate the camera in third person (camera yaw changes, player still).

### First-person gate

- **`FD_Subjectivity` is never asked for**, before or after V: the gate sits before the input check.
- Inside `fldPlayerCalc` the game checks exactly two story flags, both false: **3712 once per field tick** and 2800 three times per tick (2800 is also checked outside). `cfgGetBit(4)` is checked occasionally. `NewGame2_*` statics are all 0 on a first playthrough.
- Candidate gate: **flag 3712** (one check per tick, the shape of `if (flag && padcheck(FD_Subjectivity))`); 2800 second.

## 7. Results of the 0.0.13 input test (1 Oct 2026, session 12:43, with Francesco's OBS recording)

Source: `session_2026-10-01_12-43-*.log` (Latest.log 12:46) and `%USERPROFILE%\Videos\Captures\2026-10-01 12-45-29.mp4` (frames read with ffmpeg).

- **Compass (bottom right of the dungeon HUD)**: N up, green arrow = player facing, purple cone = camera view. It confirms **game +Z = north, +X = east**. `PlayerKakudo` 0 = south, 90 = east, 180 = north, 270 = west; compass bearing = Unity yaw = (180 - `PlayerKakudo`) mod 360.
- **Step test failed**: 15 injected `padcheck(CMN_UpFront, DOWN)` answers in 500 ms, the player did not move (`gKeyInputDir` stayed -1). Walking does not come from `padcheck`. Next lead: `dds3PadManager.GetPadAnalog` (left stick channel), learned from Francesco's own W press in 0.0.14.
- **Turn test**: `padcheck(FD_Turn_Right, DOWN)` injection works, but in third person it **orbits the camera only** (player facing unchanged), smoothly at about 105 degrees per second with about 10 degrees of easing after release: each test ended at 98 to 101 degrees, so the compass cone stopped between cardinal points (what Francesco saw). Turning the player in third person means walking in a direction; in first person `FD_Turn_*` is still to test.
- **V still locked**: flag 3712 answered true inside `fldPlayerCalc` (2289 times), and the game then started checking **flag 3715** once per tick (false). `FD_Subjectivity` was still never asked. The gate is a chain: 0.0.14 forces 3712 and 3715 and auto-adds up to 4 further new flags.
