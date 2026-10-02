# Overworld handoff (the Vortex World map)

Written 2 Oct 2026 at the end of the dungeon chats, for a new chat dedicated to the overworld. Analysis only so far: nothing in this file is built. Read `CLAUDE.md` first (binding rules), then this file, then `docs/HANDOFF-PATHFINDING.md` and `docs/PATHFINDING.md` for the dungeon tools this work reuses.

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

## Open items from the last dungeon session (not overworld)

- A demon conversation in battle (Shikigami, 20:20) spoke two lines twice about half a second apart ("[game line removed]", "You obtained Life Stone x 1."): a reader speaks the same page twice; to look at in the dialogue or announcement reader.
- The Annex 1F ramp item box (`docs/HANDOFF.md`, `WalkGrid` `Layers` = 3 hypothesis).
- Custom waypoints (`docs/EXPLORED-DOORS-PLAN.md` item 4): recorded, not to build until Francesco asks.
