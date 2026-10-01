# Handoff: navigation and pathfinding

Rewritten 1 Oct 2026 at the end of the first navigation chat (versions 0.0.12 to 0.0.16), for the next chat. Read `CLAUDE.md` first (binding rules), then this file, then `docs/NAVIGATION-DESIGN.md` sections 6 to 11 (measured facts, the game's code, test results, Francesco's decisions), `docs/class-index.md` section "Field navigation", `docs/KEYS.md`, `tools/native/README.md`.

## Where things stand

- Installed: **0.0.17** (1 Oct 2026, second navigation chat): blobber mode version 2 per Francesco's decisions, **not yet tested in game**. Design and the code reading behind it: `docs/NAVIGATION-DESIGN.md` section 12. 0.0.16 (`e6f5db5`, tested 13:39) had the rejected input takeover.
- Verified in game across 0.0.12 to 0.0.16 (logs `session_2026-10-01_12-04`, `12-43`, `12-57`, `13-13`, `13-39`; recordings in `%USERPROFILE%\Videos\Captures`):
  - Diagnostics: F5 dump to `UserData/NocturneAccess/NavDumps`, `[Nav:Trace]` / `[Nav:Change]` / `[Nav:Gate]` / `[Nav:Blob]` log lines (config `[NocturneAccess_Navigation] Diagnostics`).
  - **First person on a first playthrough, in memory only** (`FirstPersonView`): V toggles it, "First person" / "Third person" spoken; the game's flag 2800 is answered, never written.
  - **Pad injection** (`PadInput`, postfixes on `DDS3_ORGMAP_PRESS` / `DDS3_PADCHECK_PRESS` inside `fldPlayerCalc`): pad U moves the player through the game's own movement code; no crash.
  - **Grid-snapped forward steps in first-person corridors** (600-unit cells): ends within one tick of the cell centre, drift under 2 units, door cues before doors, the game's "Open Door" prompt after.
- Not working as designed in 0.0.16: injected A / D (they are the game's turns), step refusals near walls (blocked talking to the Soul and nearly all movement in the Healing Room).

## Francesco's decisions (all in force)

- Scope: **3D dungeons only**, not the world map.
- **Blobber mode is a toggle (F4), on by default.**
- **Blobber mode never changes the game's input.** First-person controls stay exactly the game's own: W forward, S turn round (180 degrees; not "back", section 12), A / D 90-degree turns, Q / E side steps, V view, confirm to interact. Only **movement itself** is snapped to the grid (ending on cell centres, useful at intersections).
- **Audio cues never block input**: describe walls, openings, doors (speak only changes), but never refuse or cancel a movement; the game's collision decides.
- **Fixed-camera rooms** (`cammode` 1 or 3; the Healing Room): later, a **points-of-interest system with auto-walk**. Until then, the game's own controls there. They are a small part of the dungeons.
- S as step back: a sound idea, **not now**.
- **IL2CPP read-only analysis** of `GameAssembly.dll` is to be used consistently and safely alongside runtime tests (never `smt3hd.exe`; read the file offline, never load or run it).

## Built in 0.0.17 (design in `docs/NAVIGATION-DESIGN.md` sections 11 and 12)

1. Removed from `BlobberMode` and `PadInput`: hiding any pad button, the mod-facing layer and its Q / E handling, step refusals, "Wall." on blocked steps.
2. Grid snap without hiding any key: the game walks while W is held; on release (after a straight walk), the mod holds pad U to the next cell centre or until the game's collision stops the player. Any other movement key, or W again, ends the snap before the game reads the keys (otherwise A / D would walk diagonally).
3. Cues on reaching each cell centre while moving, and after the game's own turns, only changes. NPC and item boxes (`Attr` 0x80) are no longer walls. The segment test stays: a probe at the next centre alone would miss the wall halfway between centres.
4. Fixed-camera rooms and third person: nothing from blobber mode.

## Next

1. Test 0.0.17 with Francesco (taps, holds, intersections, walking up to the Soul, A / D / S / Q / E during a snap). Read the `[Nav:Blob]` lines.
2. Questions for Francesco after the test:
   - snap side steps (Q / E) too;
   - bump into a wall leaves the player off-centre; a back-step to the centre would be movement he did not ask for, so ask first;
   - speak people and boxes ahead ("person ahead").
3. Then, per the plan: read-only aids (where am I, scanner), points of interest with auto-walk (fixed-camera rooms), grid A*.

## Key facts (details and sources in NAVIGATION-DESIGN sections 6 to 11)

- Game units = Unity x 100; **Y points down**; +Z north, +X east; `PlayerKakudo` in degrees, 0 south, 90 east, 180 north, 270 west (negative values in first person: normalise modulo 360).
- Field update `fldPlayer.fldPlayerCalc` runs 30 times a second; walking 29 units per tick. It uses `fldPlayerCalc_Nml_syukan` (first person) when flag 2800 is on **and** `fldGb.cammode` is 0; cammode 1 or 3 always third person; the camera has the same gate.
- First-person lock: flag 2208 or 2225 (both off on Francesco's save), checked in `fldProcess.ProcSequence`. **Never force 3712 / 3715** (Dante/Raidou, death check).
- Keyboard input arrives as pad buttons in `dds3PadManager.dds3PadMap[1][0]`: first person reads `DDS3_PADCHECK_PRESS` (U, D, L, R with cip 0; L1, R1 with cip 1), third person `DDS3_ORGMAP_PRESS`. Neither `padcheck` nor `GetPadAnalog` carries the keyboard.
- Geometry: axis-aligned, 600-unit corridors centred on multiples of 600 in the Underground Facility; doors are `fldEveHit.gEvnRng` type 10 named `NNd_MM`; place-name tables are empty in this build, the location banner names rooms (and the room behind a door, when entering its volume).

## Working rules that matter most here

- One system per session unless Francesco asks for more; commit at every working state; read the logs yourself after every run (`bash tools/log-summary.sh`, archive in `smt3hd/UserData/NocturneAccess/Logs`); look at Francesco's recordings with ffmpeg frame extraction when he makes one.
- Never invent names: anything not in `docs/class-index.md` is looked up in `research/decomp`, `research/native` (Cpp2IL names and addresses) or `research/refs` and recorded before use.
- Give Francesco exact test steps and what he should hear; every reply uses Markdown headings.
