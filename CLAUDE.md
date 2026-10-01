# Nocturne Access

Screen reader accessibility mod for Shin Megami Tensei III Nocturne HD Remaster (Steam, Unity 2019.4.10f1 IL2CPP). One MelonLoader 0.6.1 mod (net6.0, HarmonyX, Il2CppInterop) speaking through NVDA via `nvdaControllerClient.dll`. GPL-3.0.

- Repo: `E:\Claude projects\Nocturne Access`. Game: `E:\SteamLibrary\steamapps\common\smt3hd`.
- Francesco is a low-vision NVDA user: he tests in game and decides design; Claude writes the code and owns the architecture.

## Read first, every session

1. `docs/NEXT-SESSION.md`: where we stopped, what to test, open problems in order.
2. `docs/HANDOFF.md`: project source of truth (roadmap, per-version state and test results, known issues).
3. `docs/class-index.md`: every verified class, method, field and UI path, with its source.
4. `docs/KEYS.md` (mod keys, reserved keys, game bindings) and `docs/TEXT-HOOKING-HANDOFF.md` (Francesco's Suikoden design notes the readers follow).
5. `docs/NAVIGATION-DESIGN.md`: the navigation research and the blobber-mode design (current focus).

## Binding rules

- **Read the logs yourself after every game run**: `smt3hd\MelonLoader\Latest.log` and the archive `smt3hd\UserData\NocturneAccess\Logs\` (a relaunch overwrites Latest.log, so check the archive). Never ask Francesco to paste logs. Start with `bash tools/log-summary.sh`.
- **Never reconstruct text.** Speak the game's own strings (TMP writes, game data). No OCR. Numbers drawn as sprites come from game data (`CounterCtr.value`, unit data), never from images.
- **Never invent class, method or field names.** Every name comes from the proxy decompile (`research/decomp/Assembly-CSharp`), a reference repo (`research/refs`, MIT: MatthiewNocturne, NocturneAddNewSkills, NocturneModernController) or a log line, and is recorded in `docs/class-index.md` before use.
- Explain the reasoning before each step; give Francesco exact test steps and what he should hear.
- **Every reply uses Markdown headings** (NVDA navigation).
- One system per session unless Francesco asks for more; commit at every working state, saying what was verified in game. Update `docs/HANDOFF.md` and `docs/NEXT-SESSION.md` at session end.
- When Francesco says "session is done", read the logs, record what was verified, then continue from the open problems. When he says not to code, only analyse and document.
- Keyboard first; mod keys stay in the function-key range (F9 to F11 belong to Nocturne Graphics Configurator, F12 to Steam).
- Never touch or inspect `smt3hd.exe` (Denuvo).
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
- `Systems/Menus/`: `MenuReader` (spec-driven menus whose highlighted item carries a font material: title, pause and submenus, terminal, battle commands, title/config/name Yes/No), `AnnouncementReader` (prompts, location banner, interaction prompt; options `FollowsDialogue`, `Reshow`), `SaveListReader`, `SaveChoiceReader`, `ConfigReader`, `NameEntryReader`, `StatusReader`.
- `Systems/Battle/BattleReader.cs`: start, banner, targets (`nbTarSelProcess.GetTarSelProcessData().nowform`), HP changes, results, F2 status.
- `Input/Hotkeys.cs`: F1 repeat (most recent reader), F2 battle status, F6 dump, F7 probe toggle, F8 mod status.

## Lessons that apply everywhere

- The game rewrites many texts every frame with identical content: **an identical rewrite must never count as activity** (it keeps settle timers from running out).
- Highlights are font materials, and they differ per screen (`TMC01` pause/terminal/config/battle, `TMC00` title, `TMC08` Yes/No, `TMC12` blinking name-entry letter). Not every cursor is a material: some are objects (save Yes/No, stat allocation, name-entry buttons) and need game data.
- Texts are often written while hidden, and shown later: wait for visibility. Some components only hide and show without a new write: watch visibility.
- Order matters more than speed: when a question and its options arrive together, the question must be spoken first.
