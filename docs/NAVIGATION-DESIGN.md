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

## 8. Read from the game's code (1 Oct 2026, after the 0.0.14 test)

0.0.14 (session 12:57): flags 3712 and 3715 forced, no further flag appeared, V still never read; the stick channels (`GetPadAnalog`) never moved while Francesco walked with W or the arrow keys. Runtime probing had stopped converging, so the field update was read directly: a read-only disassembly of `GameAssembly.dll` (never `smt3hd.exe`), with method names from MelonLoader's own Cpp2IL output (`[Address(RVA)]` on every method). Tools and method: `tools/native/README.md`.

### First person

- `fldPlayer.fldPlayerCalc` (RVA 0x206CCD0): `if (evtBitCheck(2800)) fldPlayerCalc_Nml_syukan(); else fldPlayerCalc_Nml();`. **Flag 2800 is the first-person state**, not a lock. `fldCamera.fldCamMain`, `calcCamMiwatasi`, `fldPlayer.fldPlayerOn` and `fldPlayerCalcForUnity` read it too.
- `fldProcess.ProcSequence` (RVA 0x21FCE20, around 0x21FE374) is the only code that switches it:
  `if ((evtBitCheck(2208) || evtBitCheck(2225)) && fldProcCheckFlag(0x40) == 1 && DDS3_PADCHECK_TRIG(R3, 1) && dds3KernelMain.UIDispCheck(0)) toggle flag 2800 (evtBitOn / evtBitOff)`.
  **The real lock is flag 2208 or 2225** (both false on Francesco's save; checked every frame outside `fldPlayerCalc`, which is why 0.0.12 missed them). V is bound to pad map R3 with cip 1.
- **Flags 3712 and 3715 are not about first person**: 3712 is the Dante/Raidou mode flag (`cmpTest.cmpDbgDanteMode` sets it, `cmpDbgRaidouMode` clears it; `datDevilName.Get`, `datRaceName.raidoCheck`, `nbCameraDANTE_*` read it), and the pair 3712, 3715 is checked by `datCalc.datCheckDying`. 0.0.13 and 0.0.14 forced them inside `fldPlayerCalc` (in memory only); 0.0.15 removes that.
- Design (0.0.15, `FirstPersonView`): the game's own toggle would write flag 2800 into the save. The mod keeps its own state, answers true to every `evtBitCheck(2800)` while it is on, and never writes the flag. V (Unity key) or the game's own `DDS3_PADCHECK_TRIG(R3, 1)` toggles it while exploring; the mod says "First person" / "Third person". If the game itself calls `evtBitOn(2800)` (its gate open), the mod's toggle steps aside; if it calls `evtBitOff(2800)`, the mod's state follows.

### Walking

- `fldPlayerCalc_Nml` (third person, RVA 0x205EA20) reads `GetPadAnalog(0, 0, 0/1, cip)` (left stick) and **`DDS3_ORGMAP_PRESS(L 4, R 5, U 6, D 7, cip 0)`**, plus `DDS3_PADCHECK_PRESS(RD 3, L1 8, R1 10, cip 1)`.
- `fldPlayerCalc_Nml_syukan` (first person, RVA 0x2064C60) reads **`DDS3_PADCHECK_PRESS(L, R, U, D, cip 0)`**, then PRESS / TRIG of L1 (8), R1 (10), RD (3) and others with cip 1 (turning or strafing to measure).
- `DDS3_ORGMAP_PRESS` checks `dds3PadManager.dds3PadMap[1][0]` at the gamepad index (`dds3ConfigGamePadSteam.GetGamePadSDFMAP`), then the keyboard map (`dds3ConfigKeyBoardSteam.GetKeyBoardPadMap`) over the same buffer; `DDS3_PADCHECK_PRESS` goes through `dds3ConfigMainSteam.GetConfigPadMap` over the same buffer. **The keyboard arrives as pad buttons**, which is why `padcheck` and `GetPadAnalog` never saw it.
- Design (0.0.15, `PadInput`): Harmony postfixes on `DDS3_ORGMAP_PRESS` and `DDS3_PADCHECK_PRESS` answer true for a held `SDF_PADMAP` button, only inside `fldPlayerCalc`. NocturneModernController's startup crash came from a raw native hook (`NativeHook` with its own delegate) on `DDS3_PADCHECK_PRESS`, not from Harmony; its mods call `DDS3_PADCHECK_PRESS` directly without trouble. Config `PadInjection = false` turns the hooks off.
- `SteamInputAssign.padcheck(FD_Turn_*)` (0.0.13) stays the camera-orbit path in third person.

## 9. Results of the 0.0.15 test (1 Oct 2026, session 13:13, with Francesco's recording `2026-10-01 13-15-01.mp4`)

- **V works**: "First person" spoken, `fldPlayerCalc_Nml_syukan` ran (`syukan` 7 to 8 per 250 ms), nothing written (the game never called `evtBitOn(2800)`). 58 hooks, no errors, no crash from the pad hooks.
- **First person is a native near-blobber** (Francesco: "the game is already a blobber"): turns snap to exactly 90 degrees (`PlayerKakudoGoal` -90, -180, -270, 0; `PlayerKakudo` eases there in about 0.25 s), no diagonal movement, side steps instead. Measured nuance: **walking is continuous while the key is held** (29 units per tick, straight along the facing, the cross coordinate stays exact), and stops wherever the key is released (z -4139, x -8882, -9056: not on the 600 grid). So a blobber "one step" is: hold forward until the next cell centre (600 units, about 21 ticks, 0.7 s).
- **Step test in first person**: pad U held 500 ms moved exactly 435 units (15 ticks x 29) along the facing: `PadInput` drives first-person walking (`DDS3_PADCHECK_PRESS`). Not re-run in third person this time.
- `PlayerKakudo` in first person runs negative (-90 = 270 = west); normalise modulo 360.
- **Fixed-camera rooms**: entering the Healing Room (area 16) set `cammode` 1; the game ran `fldPlayerCalc_Nml` although the mod still answered flag 2800, and first person came back by itself in the corridor (`cammode` 0). Code: `fldPlayerCalc` reads `fldGb.cammode` (offset 0x24) first: **cammode 1 or 3 always uses the third-person routine**; `fldCamera.fldCamMain` likewise uses the first-person camera only when `cammode` is 0. First person is impossible there by the game's design.
- In the fixed-camera room, third-person movement snaps the facing goal to **world** multiples of 45 degrees (225, 270, 315, 0, 45, 135, 180) even though the camera yaw is 333: the game snaps in world space, so the mod can always choose a pad direction that walks exactly along a world axis.

### Options for fixed-camera rooms (decision for Francesco)

1. **Recommended: the mod's own blobber layer over third-person movement in those rooms.** The mod keeps the facing (north, east, south, west) itself and speaks it; a step presses the pad direction that moves along the facing axis (chosen from the fixed camera's yaw) until the next cell centre; turns change only the mod's facing (spoken, instant). The game still walks, collides and opens doors. Same keys and same speech as first person, so the switch is seamless; first person resumes by itself on leaving the room.
2. Not recommended: fake `cammode` 0 during the field update in those rooms to force the first-person routine. Both the movement and camera code key off `cammode`; the room's fixed camera would break (bad for low vision) and events tied to the camera could misbehave.

## 10. Blobber mode, version 1 (0.0.16, 1 Oct 2026)

Francesco's decisions (1 Oct 2026, after 0.0.15): fixed-camera rooms use **option 1** (the mod's own facing over third-person walking); speech **only on changes**; S stays the game's own for now (step back later); IL2CPP read-only analysis is to be used consistently.

- `BlobberMode` (F4, on at start, config `BlobberOnAtStart`, `BlobberCellSize` 600) runs at the start of every field update (`FieldState` prefix, before the game reads the pad). Off on the world map (fieldID below 10).
- Layers: **first person** where `cammode` is 0 (blobber mode switches first person on unless the player turned it off with V; turns stay the game's own 90-degree turns, spoken when they finish); **mod facing** in fixed-camera rooms (`cammode` 1 or 3) or with first person off (Q / E turn the mod's facing; the facing is spoken on entry and after each turn).
- Steps: `PadInput` hides the real pad U, L, R from the game (recording them as the player's presses) and holds the pad button(s) until the next multiple of 600 along the axis (at least 100 ahead). First person: U forward, L / R side steps. Mod facing: the pad direction or diagonal pair whose camera-relative bearing is nearest the wanted world bearing (U = camera yaw, R +90, D +180, L +270; the game snaps to world 45). Ends within half a tick, when blocked 3 ticks ("Wall." unless a door is there), after 3 s, on area change or when exploring stops. Held keys keep stepping. A step into a wall is refused with "Wall." (once per press).
- Speech: after a step only what changed ahead / left / right ("wall ahead", "door ahead", "opening left", "wall right"); after a turn "North, wall ahead." (`GridSense`: vertical collision polygons at chest height as walls, `NNd_MM` event boxes as doors).
- Checked in the code: no routine called inside `fldPlayerCalc` other than the two movement routines reads the pad (`fldPlayerCalcHasigo` ladders, `_Hit`, `_WarpRun`, `ForUnity`, `MirChk`), so hiding W cannot break them.
- Log tag `[Nav:Blob]`: every step with distance, drift across the axis, ticks, the pads used, and in fixed-camera rooms the camera yaw and the game's facing goal (a warning if it differs from the wanted facing). The trace now lists the real pad buttons pressed (`pads`), to identify the Q / E buttons.
- Known limits: the 600 grid fits the corridors; rooms whose walls sit on 300 offsets (Healing Room, x -6000..-4800) get steps that end at a wall (spoken "Wall"). Third-person steps curve while the game eases the facing (drift logged). Gamepad sticks bypass blobber mode (keyboard first).

## 11. Results of the 0.0.16 test and Francesco's revised design (1 Oct 2026, session 13:39, recording `2026-10-01 13-41-36.mp4`)

### What the run showed (log `session_2026-10-01_13-39-*.log`)

- **Forward steps in first-person corridors worked**: 580 to 667 units per step (20 to 23 ticks), ending within one tick of the 600 grid, drift across the axis under 2 units. "Door ahead" was spoken before both doors; the step into a door stopped at the door wall and the game's "Open Door" prompt followed. Holding W chained steps.
- **First-person native controls (from the trace's real pad buttons)**: W / S = pad U / D (forward, back, continuous); **A / D = pad L / R = the game's 90-degree turns** (not side steps); **Q / E = pad L1 / R1 = side steps** (continuous). 0.0.16 assumed A / D were side steps: its injected "step left / right" turned the player instead, moved 0, and was reported as a blocked step ("Wall."), several times per press.
- **Refusing steps blocked play**: a step was refused whenever the segment one full cell ahead crossed any vertical collision polygon. NPC boxes (the Soul, `Attr` 0x80, 100 x 100) and room furniture count, so Francesco could not walk up to the Soul in the corridor to talk to it, and in the Healing Room (fixed camera) nearly every step was refused ("Wall."): he turned blobber mode off and used his sight to leave. The game's facing goal always matched the wanted facing in the mod-facing layer (no warning logged), so the pad choice from the camera yaw was right; the refusals were the problem.
- No errors; 58 hooks.

### Francesco's decisions (1 Oct 2026), replacing section 10's input handling

1. **Blobber mode must not change the game's input.** First-person controls stay exactly the game's own (W / S, A / D turns, Q / E side steps, V, confirm). The only change is the handling of **movement itself**: a forward (and later backward and side) movement is snapped to the grid, ending on the next cell centre; especially useful at intersections. To re-test.
2. **Audio cues describe walls and limits but never block input**: "wall ahead", "opening left", "door ahead" and so on stay (speak only changes), but no step is refused or cancelled because of them; the game's collision decides. This keeps interaction with things against walls (the Soul) possible.
3. **Fixed-camera rooms** (`cammode` 1 or 3): no blobber movement there. The plan is a **points-of-interest system with auto-walk** (choose a target such as a door, a person, a box; the mod walks there through the game's input). Fixed-camera rooms are a small part of the dungeons, so this can come later. Until then the game's own controls apply unchanged in those rooms.
4. S as a step back: sound idea, **not now**.
5. IL2CPP read-only analysis (`tools/native`) is to be used consistently, safely, alongside runtime tests.

### Consequences for the next build (design, not yet implemented)

- Remove from `BlobberMode`: the pad L / R hiding and injection (they are turns), the mod-facing layer and Q / E handling, the step refusal and the "Wall." on blocked steps, and the speech queue entries that came from refusals.
- Keep: first-person auto-on, speech only on changes after a movement and after the game's own turns, door cues, logs.
- **Grid-snapped forward movement without hiding the key**: let the game walk while W is held (native), and when W is released, keep pad U pressed until the next cell centre (or until the game's collision stops the player), so every walk ends on a cell centre. This changes only movement, not input: tapping W still moves at least to the next centre. Option to discuss with Francesco if needed: the same for S (pad D) and Q / E side steps (pad L1 / R1, continuous) once the forward behaviour is confirmed.
- In fixed-camera rooms: blobber movement off (the game's own third-person controls), cues limited to the facing and the location banner, until the points-of-interest system exists.
- Open technical point: wall cues should ignore NPC and item-box boxes (`Attr` 0x80) as "wall" and report them separately later ("person ahead"), and furniture in rooms needs a better test than "anything within one cell" (probe a point at the cell centre with the player radius 60 instead of the whole segment).
