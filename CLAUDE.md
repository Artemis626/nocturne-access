# Nocturne Access

Screen reader accessibility mod for Shin Megami Tensei III Nocturne HD Remaster (Steam, Unity 2019.4.10f1 IL2CPP). One MelonLoader 0.6.1 mod (net6.0, HarmonyX, Il2CppInterop) speaking through NVDA via `nvdaControllerClient.dll`. GPL-3.0.

- Repo: `E:\Claude projects\Nocturne Access`. Game: `E:\SteamLibrary\steamapps\common\smt3hd`.
- Francesco is a low-vision NVDA user: he tests in game and decides design; Claude writes the code and owns the architecture.

## Read first, every session

1. `docs/NEXT-SESSION.md`: where we stopped, what to test, open problems in order.
2. `docs/HANDOFF.md`: project source of truth (roadmap, per-version state and test results, known issues).
3. `docs/class-index.md`: every verified class, method, field and UI path, with its source.
4. `docs/KEYS.md` (mod keys, reserved keys, game bindings) and `docs/TEXT-HOOKING-HANDOFF.md` (Francesco's Suikoden design notes the readers follow).
5. **Pending work:** `docs/EXPLORED-DOORS-PLAN.md` (items 1 to 3 built in 0.0.35: explored doors per save file, empty item boxes; item 4, custom waypoints, recorded for later, not to build yet). Done earlier: `docs/REFACTOR-PLAN.md` (0.0.31).
6. **Shops, Fountain, fusion, Amala, Ginza (from 3 Oct 2026):** `docs/HANDOFF-SHOPS-FUSION.md` and the latest entries of `docs/HANDOFF.md`.
7. Overworld (2 to 3 Oct 2026, done): `docs/HANDOFF-OVERWORLD.md`.

## Current state (6 Oct 2026) and focus

- **Installed: 0.0.59 (6 Oct 2026, committed locally, not pushed, not yet tested; ladders 0.0.55 verified 6 Oct).** Test list: `docs/HANDOFF.md`, last entry (Ginza stairs, Terminal doors "beyond doors", Ladder up/down, Media, snap at doors), and the version table in `docs/NEXT-SESSION.md`. Still untested from earlier: the Amala Network warps (0.0.52 to 0.0.53; Francesco keeps a save inside the Network, File 2).
- **Where Francesco is:** cleared the Amala Network, playing through Ginza (field 19), Harumi Warehouse (field 13), the Ginza underpass (field 22). He plays on and reports; read each session's log, fix, record (`docs/HANDOFF.md`, one entry per session, newest last).
- **Systems built since 3 Oct (details in `docs/HANDOFF.md` and `docs/class-index.md`):** institution menus (Terminal, Fountain, Junk Shop, Cathedral, Rag's Jewelry, Amala Link); `FusionReader` (Cathedral tables from the game's cursor; first table's icon grid in the game's words "OK", "N/A", "Rank Up"... from `Localize` COMBINE_L0030 to L0034); `FacilityReader` (shop prices, owned counts, quantity and sum, Fountain HP/MP/fee via `fclUI.DrawPayMoney`); `RagReader`; `WarpMap` (hidden same-area warps: door table movetype 2; routes avoid them, walks chain them, arrival points from the game's object list or learned when warped); Page Up list in fixed map order, filters (Shift+Page Up / Down: All, Doors, Rooms, Exits, Warps, Souls, Demons, Item boxes, Events; reset to All in every new area), Insert for the selected point; story volumes listed as "Event" with the game's prompt learned per save file; "Warped." on same-area jumps; Where am I with the place name from `FldNameData`; 0.0.59: same-area door and ladder pairs (`WarpMap.PairUp`, landing in front of the partner), landmarks (Terminal, exits, named doors, ladders) always listed ("beyond doors"), Ladders filter ("Ladder up" / "down"), "to exit" doors, party pick reads all highlighted names, blobber snap stops at doors. Francesco uses the Graphics Configurator speedhack (F10) on purpose.
- **Open:** the battle Summon list (needs an F6 dump); the 6 Oct hard freeze at the underpass Terminal door (cause unknown); routes through other areas off by default (Francesco: missed the point); zig-zag routes in diagonal corridors (right-angle runs; 8-direction runs later); warp arrival-point lookup unverified in game; overworld step 6 (explored entrances) when Francesco asks; custom waypoints recorded only (`docs/EXPLORED-DOORS-PLAN.md` item 4); the Annex 1F item box reachable only after a ramp (`docs/HANDOFF.md`).
- **Lessons from this stretch:** a point "not reachable" may be behind a same-area link (ladder, movetype 5) or in a part of the area joined only through other areas: read the dump's door table before blaming the grid; moves by the game can be vertical (ladders); check the `[Nav:Poi]` log line (every point with its category) and the active filter before treating a point as missing; place-name pads differ in size per field (Ginza 800 wide); hooking a small wrapper may never fire (hook the shared function it calls); a reader that resets its "opening" state every frame never speaks; things the game draws as images often have their words in `Localize.GetLocalizeText` ids found in the string literals.
- History by phase: overworld (done) `docs/HANDOFF-OVERWORLD.md`; dungeon navigation `docs/HANDOFF-PATHFINDING.md`, `docs/NAVIGATION-DESIGN.md`, `docs/PATHFINDING.md`; shops and fusion `docs/HANDOFF-SHOPS-FUSION.md`.

## Navigation rules (binding)

- Blobber mode (F4) is a **toggle, on by default**. It **never changes the game's input**: first-person controls stay the game's own (W forward, S turn round, A / D 90-degree turns, Q / E side steps, V, confirm); only movement itself is snapped to the grid (cell centres: on releasing W the walk carries on to the next centre, 0.0.17). F3 walks to the centre of the current square (0.0.18). Q / E are not snapped (Francesco). Audio cues (walls, openings, doors; only changes) **never block or refuse input**: the game's collision decides.
- Fixed-camera rooms (`cammode` 1 or 3) keep the game's own controls; points of interest (Page Up / Page Down), auto-walk (Home) and spoken directions (End) work there and in first person. The mod never presses confirm. S as step back: not now.
- First person (V) is locked on a first playthrough (game flags 2208 / 2225; the view itself is flag 2800). The mod keeps its own first-person state **in memory only**: it answers `evtBitCheck(2800)` while on and never writes the flag (`FirstPersonView`, `docs/NAVIGATION-DESIGN.md` section 8). Never force flags 3712 / 3715 (Dante/Raidou, death check).
- Movement is driven only through the game's own input checks: walking as pad buttons (`dds3PadManager.DDS3_ORGMAP_PRESS` / `DDS3_PADCHECK_PRESS` postfixes, `PadInput`), camera turns as logical actions (`SteamInputAssign.padcheck`, `InputInjector`); only during the field update, at normal speed, so the game's collision, doors and events stay in charge. Never move the player by writing positions.

## Binding rules

- **Read the logs yourself after every game run**: `smt3hd\MelonLoader\Latest.log` and the archive `smt3hd\UserData\NocturneAccess\Logs\` (a relaunch overwrites Latest.log, so check the archive). Never ask Francesco to paste logs. Start with `bash tools/log-summary.sh`.
- **Never reconstruct text.** Speak the game's own strings (TMP writes, game data). No OCR. Numbers drawn as sprites come from game data (`CounterCtr.value`, unit data), never from images.
- **Never invent class, method or field names.** Every name comes from the proxy decompile (`research/decomp/Assembly-CSharp`), a reference repo (`research/refs`, MIT: MatthiewNocturne, NocturneAddNewSkills, NocturneModernController) or a log line, and is recorded in `docs/class-index.md` before use.
- Explain the reasoning before each step; give Francesco exact test steps and what he should hear.
- **Every reply uses Markdown headings** (NVDA navigation).
- One system per session unless Francesco asks for more; commit at every working state, saying what was verified in game. Update `docs/HANDOFF.md` and `docs/NEXT-SESSION.md` at session end.
- When Francesco says "session is done", read the logs, record what was verified, then continue from the open problems. When he says not to code, only analyse and document.
- Keyboard first; mod keys stay in the function-key range (F9 to F11 belong to Nocturne Graphics Configurator, F12 to Steam), except Page Up, Page Down, Home and End for points of interest (Francesco, 1 Oct 2026), and Shift+Page Up / Page Down (filters) and Insert (selected point), which Francesco chose on 4 Oct 2026. F1 to F8 are all taken (F7 since 0.0.56); a new player key needs Francesco's choice. **Prefer single keys: Francesco dislikes Shift or Ctrl combinations in accessibility mods**; keep combinations for debug keys only (Shift+F6).
- Never touch or inspect `smt3hd.exe` (Denuvo). **Use read-only IL2CPP analysis of `GameAssembly.dll` consistently** (Francesco, 1 Oct 2026: it often beats runtime guessing): read the file offline, never load or run it (`tools/native/README.md`, `tools/native/dis.sh`).
- Francesco launches with `--melonloader.hideconsole` (NVDA otherwise reads the console).
- Commit messages end with the attribution line given by the environment.
- **Never push to GitHub unless Francesco gives the order** (Francesco, 2 Oct 2026). Commit locally at every working state; `git push` only when he explicitly orders that push. An order covers that one push, not later ones.

## Build and tools

- `dotnet build NocturneAccess.sln -c Release` builds and deploys to the game's `Mods` folder (the game must be closed). References come from `_dlls/` (`tools/copy-references.ps1`).
- The Windows `python3` alias hangs: use perl or bash. Bump the version in `Core/ModMain.cs` and `NocturneAccess.csproj` together.
- F6 dumps every visible text to the log; the probe (`TextProbe = true` in the config; off by default since 0.0.57) logs every TMP write and message-engine call as `[Probe:TMP]` / `[Probe:Mes]`.

## Architecture (src/NocturneAccess)

- `Core/`: `ModMain` (install and per-frame Tick of every reader), `Config` (`UserData/NocturneAccess.cfg`), `Patcher` (tolerant, logged Harmony postfixes), `TmpRouter` (the single TMP text hook; classifies each component once by hierarchy path; **blank writes never reach readers**), `TextUtil` (tag stripping, `SpeakableCharacter`), `UiUtil`, `LogArchive`.
- `Speech/`: `SpeechManager` (background thread, queue or interrupt, every line mirrored as `[SPEECH]` in the log).
- `Systems/SpeechOrder.cs`: a screen's first line waits (max 1.5 s) while a leader is due: a dialogue page (`DialogueReader.PageDue`), a prompt (`AnnouncementReader.AnyDue`), the name confirmation (`NameEntryReader.ConfirmDue`).
- `Systems/Dialogue/`: `DialogueReader` (pages with speaker), `ChoiceReader` (dialogue choices via `itfMesManager.ChangeSelection`).
- `Systems/Menus/`: `MenuReader` (spec-driven menus whose highlighted item carries a font material: title, pause and submenus, institution menus (Terminal, Fountain, Junk Shop, Cathedral), shop and fusion lists, battle commands, title/config/name Yes/No), `AnnouncementReader` (prompts, location banner, interaction prompt; options `FollowsDialogue`, `Reshow`), `SaveListReader`, `SaveChoiceReader`, `ConfigReader`, `NameEntryReader`, `StatusReader` (Stats: summary with skills), `CampListReader` (item and skill lists, cursor from the game). `FusionReader` (the Cathedral's demon tables, cursor from `cmbDrawStockDevilInfo`), `FacilityReader` (shop and Fountain numbers from their draw calls), `RagReader` (Rag's Jewelry trade lists, 0.0.53).
- `Systems/Battle/`: `BattleReader` (start, banner with the acting enemy's name, targets via `nbTarSelProcess.GetTarSelProcessData().nowform`, side turns, half-turn hits, HP changes with amounts, results, F2 / F3), `BattleSkills` (costs, elements, affinities from the game's tables), `BattleAnalyze` (the Analyze screen), `BattleLog` (F5). `BattleReader` is split into partial files (`.Banner`, `.Turns`, `.Units`, `.Results`). `Capture/BattleDump.cs`: Shift+F2; both dumps write through `Capture/DumpWriter.cs`.
- `Systems/Navigation/`: `FieldState` (exploring signal, prefix runs the per-tick navigation), `FieldData`, `CollisionCache`, `GridSense` (wall and door cues), `FirstPersonView` (flag 2800 in memory), `PadInput` (the game's pad buttons: read, press; never hides a key), `BlobberMode`, `CentreWalk` (F3), `PointsOfInterest` (Page Up / Down; "explored" doors, "empty" boxes), `DoorTable` (room, passage or exit, from the game's door table), `ExploredDoors` (doors gone through, one list per save file in `UserData/NocturneAccess/Explored`; also inside an area, "Warped."), `WarpMap` (warps, ladders and same-area doors and routes through them, 0.0.52 to 0.0.55), `AreaRoutes` (routes through other areas from doors walked and areas visited, F7, 0.0.56), `WalkGrid` (walkability grid and search), `AutoWalk` (Home), `RouteGuide` (End, spoken directions on demand), `WhereAmI` (Delete), `InputInjector` (`padcheck` actions), `AnalogInput` (stick telemetry). `Capture/NavProbe.cs`, `Capture/NavDump.cs`: navigation diagnostics.
- `Input/Hotkeys.cs`: F1 repeat (most recent reader), F2 battle status (our side), Shift+F2 battle dump, F3 centre on the square (in battle: the enemies), F5 battle log, F4 blobber mode, Page Up / Page Down next / previous point of interest, Home walk to it, End directions to it, F6 UI dump, Shift+F6 navigation dump, F7 routes through other areas (0.0.56), Shift+F7 probe toggle, F8 battle verbosity (HP left after actions, 0.0.45; was Status), Delete where am I (0.0.44), Shift+Page Up / Shift+Page Down point-of-interest filter and Insert selected point (0.0.51). Keys accept modifiers (`Shift+`, `Ctrl+`, `Alt+`), matched exactly.

## Lessons that apply everywhere

- The game rewrites many texts every frame with identical content: **an identical rewrite must never count as activity** (it keeps settle timers from running out).
- Highlights are font materials, and they differ per screen (`TMC01` pause/terminal/config/battle, `TMC00` title, `TMC08` Yes/No, `TMC12` blinking name-entry letter). Not every cursor is a material: some are objects (save Yes/No, stat allocation, name-entry buttons) and need game data.
- Texts are often written while hidden, and shown later: wait for visibility. Some components only hide and show without a new write: watch visibility.
- Order matters more than speed: when a question and its options arrive together, the question must be spoken first.
- **Never write regex patterns or other backslash-heavy code through perl or shell one-liners**: 0.0.47's Fountain patterns lost their `\d` and failed silently. Use the Edit tool, then test new patterns against real paths from the log.
