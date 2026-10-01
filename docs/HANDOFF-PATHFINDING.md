# Handoff: navigation and pathfinding (new chat)

Written 1 Oct 2026 at the end of the menus-and-battle chat, for the chat that starts navigation. Read `CLAUDE.md` first (binding rules), then this file, then `docs/NAVIGATION-DESIGN.md` (the research and the design), `docs/class-index.md` section "Field navigation", `docs/KEYS.md`.

## Where things stand

- Mod version **0.0.11**, verified in game (1 Oct 2026, 09:47 session): dialogue, choices, every menu screen met so far, save/load, config, name entry (letters and pre-filled names), Stats and level-up, interaction prompts, location names, combat version 1. Francesco calls it reliable enough to play the prologue by ear.
- Last commits: `6498be9` (0.0.11 verified), `727dd39` (navigation research), then the commit adding this file.
- Last game run: `session_2026-10-01_10-23-24.log` (short: load, config, pause menu; no errors, 44 hooks). Francesco pressed V in the Underground Facility: nothing happens on a first playthrough.
- **0.0.12 diagnostic verified in game (1 Oct 2026, 12:04 session)**: F5 dumps, walk trace, change log and V capture all work; results in `docs/NAVIGATION-DESIGN.md` section 6 (game units = Unity x 100, Y down, facing in degrees, 600-unit grid, doors are `NNd_MM` event boxes, keyboard does not show in padcheck, V is gated before the input check, gate candidate flag 3712).

## Francesco's decisions

- Scope: **3D dungeons only**, not the world map (he has not reached it).
- Order: the plan in `docs/NAVIGATION-DESIGN.md` section 4: diagnostic run, first-person unlock and input proof of concept, read-only aids (where am I, wall sense, scanner), grid and A* with spoken directions, blobber mode and auto-walk.
- **Blobber mode is a toggle, on by default**, switched with one key, so later areas (curved or open geometry) and personal preference can fall back to free movement.
- Still to ask him when it comes up: S as step back or turn around; speech per step (changes only, or a short description every step); the toggle key (F3 and F4 are free; F1 repeat, F2 battle status, F5 navigation dump (debug, 0.0.12), F6 dump, F7 probe, F8 status are taken; F9 to F12 reserved).

## Must do: force-unlock first person

**First person ("Toggle POV", V, logical action `FD_Subjectivity`) is locked to New Game+.** Francesco confirmed V does nothing on his save. The blobber mode is designed around the first-person view, so the mod has to "bruteforce" its availability, in memory only:

1. Find the gate: Harmony postfix on `EventBit.evtBitCheck(int no)` logging the flags checked during the field update right after V is pressed, plus the `dds3GlobalWork.NewGame2_*` values (`NewGame2_SYS_ENDING0..7`, `NewGame2_endcnt`). If no flag is checked, look at `NewGame2_endcnt` and `dds3ConfigMain.cfgGetBit`.
2. Force it narrowly: return true for that flag only while `fldPlayer.fldPlayerCalc` runs (prefix sets a flag, postfix clears it), so other New Game+ content stays locked. Never call `evtBitOn` (it would be saved). Config `[NocturneAccess_Navigation] UnlockFirstPerson = true`.

Precedent: the MIT reference mod `research/refs/MatthiewNocturne/EarlyCompendium` unlocks the Compendium with exactly this postfix (flag 40). Details: `docs/NAVIGATION-DESIGN.md` section 5.

## Key facts for the first runs

- Exploring = `fldPlayer.fldPlayerCalc` ran within the last 100 ms (Ref NocturneModernController, used by four of its mods).
- Player: `fldGlobal.fldGb.playerX/Y/Z`, `PlayerKakudo` (unit and zero direction to measure), `fieldID`, `areaID`, `blockID`, `AmSBlock`, `AmFloor`, `cammode`.
- Walls: `fldHit.gfldCollision` (`collision_s[]`, count `gfldCollsionNum`) with face flags in `pFace` (`fldResCollisionPolyFace_t`: `Basyo`, `Amap0`, `Amap1`, `Myuka`); the game's segment test `fldHit.fldHitChkCamera(eye, target, ref hit)`.
- Room graph: `fldAutoMap.pfldAmArea` (areas, sub-blocks with boxes, icons).
- Entities: `fld_Npc.fldNpc`, `fldTakaraWork_tag`, `fldEveHit.gEvnRng` / `gEve` (event volumes with names, positions, links).
- Place names: `fldGlobal.area_name`, `basyo_name`, `floor_name` (game text).
- Moving the player: postfix on `SteamInputAssign.padcheck(int, SIActionName, SIPressType)` turning the result true for `FD_Turn_Left` / `FD_Turn_Right` (live-confirmed by the Ref) and, to test, `CMN_UpFront`. Only while exploring; normal speed only (thin door volumes can be skipped above 1.6 times speed, per the Ref).

## Working rules that matter most here

- One system per session, commit at every working state, and read the logs yourself after every run (`bash tools/log-summary.sh`, archive in `smt3hd/UserData/NocturneAccess/Logs`).
- Never invent names: anything not in `docs/class-index.md` is looked up in `research/decomp` or `research/refs` and recorded before use.
- Spoken text: game strings where they exist (place names, prompts); the mod's own short words for categories ("door", "box", "person", "wall"), never invented names.
- Give Francesco exact test steps and what he should hear; every reply uses Markdown headings.
