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
6. **Navigation:** `docs/HANDOFF-PATHFINDING.md` (start here in a navigation chat: state, Francesco's decisions, the first-person unlock), then `docs/PATHFINDING.md` (points of interest, walking map, auto-walk: every test and fix) and `docs/NAVIGATION-DESIGN.md` (research, blobber mode, the game's input and code).

## Current focus: navigation and pathfinding (from 1 Oct 2026)

- 3D dungeons only (not the world map). Plan in `docs/NAVIGATION-DESIGN.md` section 4, one step per test run.
- **State (1 Oct 2026):** blobber mode version 2 and the F3 centring key are verified in game (0.0.17 to 0.0.18; 0.0.19 fixes two centring edge cases, sections 12 to 14). **Next goal (Francesco): pathfinding, starting with points of interest and auto-walk in fixed-camera rooms** (a smaller first target than the overworld or big areas): `docs/PATHFINDING.md`. Fixed-camera rooms verified (0.0.20 to 0.0.22); 0.0.23 extends points of interest and auto-walk to first person, with doors named by the game's own place names; **0.0.24** fixes the prologue test (walks end on events, the walking map has several levels, arrival uses `OldHitDoor`; basement walks verified); **0.0.25** leaves out hidden NPCs and adds spoken directions on End (verified in a long play session: Francesco now plays through the game and reports bugs); **0.0.26** speaks directions only when End is pressed (verified); **0.0.27** (verified 22:14: elevator floor banner, walks in front of doors, square centres per area); **0.0.28**: battle, whose turn, clearer F2, enemy HP after others fall, Shift+F2 battle dump (verified 2 Oct); **0.0.29**: skill costs, weakness and critical hits for both sides, Analyze, side turns, damage amounts, results; F2 our side, F3 enemies, F5 battle log (verified 2 Oct); **0.0.30**: cleanup and debug pass, no behaviour change (verified); **0.0.31**: `docs/REFACTOR-PLAN.md` items 0 to 5 (verified); **0.0.32**: item and skill lists, skills on the Stats screen; **0.0.33**: its fixes and the walking map keeping whole areas (verified); **0.0.34**: talking demons listed, the game's collision switches (annex gate), Magatama screen (partly tested); **0.0.35**: `docs/EXPLORED-DOORS-PLAN.md` items 1 to 3, generic doors gone through say "explored" (kept per save file), opened item boxes say "empty" (verified 2 Oct; Francesco: pathfinding alone does not give a mental map of identical doors and corridors); **0.0.36**: blobber cues silent while the game moves the player itself (cutscenes). **0.0.37**: the game's door table decoded (where each door leads; verified). **0.0.38**: plain doors say "room", "passage" or "exit" (config `DoorKinds`, revert point f0621c8). Open: an Annex 1F item box reachable only after a ramp (`docs/HANDOFF.md`). State and open problems: `docs/HANDOFF-PATHFINDING.md`.
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
- Keyboard first; mod keys stay in the function-key range (F9 to F11 belong to Nocturne Graphics Configurator, F12 to Steam), except Page Up, Page Down, Home and End for points of interest (Francesco, 1 Oct 2026). **Prefer single keys: Francesco dislikes Shift or Ctrl combinations in accessibility mods**; keep combinations for debug keys only (Shift+F6).
- Never touch or inspect `smt3hd.exe` (Denuvo). **Use read-only IL2CPP analysis of `GameAssembly.dll` consistently** (Francesco, 1 Oct 2026: it often beats runtime guessing): read the file offline, never load or run it (`tools/native/README.md`, `tools/native/dis.sh`).
- Francesco launches with `--melonloader.hideconsole` (NVDA otherwise reads the console).
- Commit messages end with the attribution line given by the environment.

## Build and tools

- `dotnet build NocturneAccess.sln -c Release` builds and deploys to the game's `Mods` folder (the game must be closed). References come from `_dlls/` (`tools/copy-references.ps1`).
- The Windows `python3` alias hangs: use perl or bash. Bump the version in `Core/ModMain.cs` and `NocturneAccess.csproj` together.
- F6 dumps every visible text to the log; the probe (`Probe = true` in the config) logs every TMP write and message-engine call as `[Probe:TMP]` / `[Probe:Mes]`.

## Architecture (src/NocturneAccess)

- `Core/`: `ModMain` (install and per-frame Tick of every reader), `Config` (`UserData/NocturneAccess.cfg`), `Patcher` (tolerant, logged Harmony postfixes), `TmpRouter` (the single TMP text hook; classifies each component once by hierarchy path; **blank writes never reach readers**), `TextUtil` (tag stripping, `SpeakableCharacter`), `UiUtil`, `LogArchive`.
- `Speech/`: `SpeechManager` (background thread, queue or interrupt, every line mirrored as `[SPEECH]` in the log).
- `Systems/SpeechOrder.cs`: a screen's first line waits (max 1.5 s) while a leader is due: a dialogue page (`DialogueReader.PageDue`), a prompt (`AnnouncementReader.AnyDue`), the name confirmation (`NameEntryReader.ConfirmDue`).
- `Systems/Dialogue/`: `DialogueReader` (pages with speaker), `ChoiceReader` (dialogue choices via `itfMesManager.ChangeSelection`).
- `Systems/Menus/`: `MenuReader` (spec-driven menus whose highlighted item carries a font material: title, pause and submenus, terminal, battle commands, title/config/name Yes/No), `AnnouncementReader` (prompts, location banner, interaction prompt; options `FollowsDialogue`, `Reshow`), `SaveListReader`, `SaveChoiceReader`, `ConfigReader`, `NameEntryReader`, `StatusReader` (Stats: summary with skills), `CampListReader` (item and skill lists, cursor from the game).
- `Systems/Battle/`: `BattleReader` (start, banner with the acting enemy's name, targets via `nbTarSelProcess.GetTarSelProcessData().nowform`, side turns, half-turn hits, HP changes with amounts, results, F2 / F3), `BattleSkills` (costs, elements, affinities from the game's tables), `BattleAnalyze` (the Analyze screen), `BattleLog` (F5). `BattleReader` is split into partial files (`.Banner`, `.Turns`, `.Units`, `.Results`). `Capture/BattleDump.cs`: Shift+F2; both dumps write through `Capture/DumpWriter.cs`.
- `Systems/Navigation/`: `FieldState` (exploring signal, prefix runs the per-tick navigation), `FieldData`, `CollisionCache`, `GridSense` (wall and door cues), `FirstPersonView` (flag 2800 in memory), `PadInput` (the game's pad buttons: read, press; never hides a key), `BlobberMode`, `CentreWalk` (F3), `PointsOfInterest` (Page Up / Down; "explored" doors, "empty" boxes), `DoorTable` (room, passage or exit, from the game's door table), `ExploredDoors` (doors gone through, one list per save file in `UserData/NocturneAccess/Explored`), `WalkGrid` (walkability grid and search), `AutoWalk` (Home), `RouteGuide` (End, spoken directions on demand), `InputInjector` (`padcheck` actions), `AnalogInput` (stick telemetry). `Capture/NavProbe.cs`, `Capture/NavDump.cs`: navigation diagnostics.
- `Input/Hotkeys.cs`: F1 repeat (most recent reader), F2 battle status (our side), Shift+F2 battle dump, F3 centre on the square (in battle: the enemies), F5 battle log, F4 blobber mode, Page Up / Page Down next / previous point of interest, Home walk to it, End directions to it, F6 UI dump, Shift+F6 navigation dump, F7 probe toggle, F8 mod status. Keys accept modifiers (`Shift+`, `Ctrl+`, `Alt+`), matched exactly.

## Lessons that apply everywhere

- The game rewrites many texts every frame with identical content: **an identical rewrite must never count as activity** (it keeps settle timers from running out).
- Highlights are font materials, and they differ per screen (`TMC01` pause/terminal/config/battle, `TMC00` title, `TMC08` Yes/No, `TMC12` blinking name-entry letter). Not every cursor is a material: some are objects (save Yes/No, stat allocation, name-entry buttons) and need game data.
- Texts are often written while hidden, and shown later: wait for visibility. Some components only hide and show without a new write: watch visibility.
- Order matters more than speed: when a question and its options arrive together, the question must be spoken first.
