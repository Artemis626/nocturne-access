# Nocturne Access: next session handoff

Written 30 Sep 2026 at the end of session 1, for a fresh chat. Read this first, then `docs/HANDOFF.md` (full project source of truth), `docs/class-index.md` (every verified name and UI path) and `docs/KEYS.md`. `docs/TEXT-HOOKING-HANDOFF.md` holds Francesco's Suikoden Access design notes that the readers follow.

## The project in one paragraph

Nocturne Access is a free, GPL-3.0 screen reader mod for Shin Megami Tensei III Nocturne HD Remaster (Steam, Unity 2019.4.10f1 IL2CPP, game folder `E:\SteamLibrary\steamapps\common\smt3hd`, repo `E:\Claude projects\Nocturne Access`). It is one MelonLoader 0.6.1 mod (net6.0, HarmonyX, Il2CppInterop) that speaks through NVDA by calling `nvdaControllerClient.dll` directly. Francesco is a low-vision NVDA user: he tests in game and decides design; Claude writes the code and owns the architecture.

## Working rules (binding)

- **Read the log yourself after every game run**: `smt3hd\MelonLoader\Latest.log` plus `smt3hd\UserData\NocturneAccess\Logs\` (snapshots every 30 s and on quit). Never ask Francesco to paste logs. Start with `bash tools/log-summary.sh`; `perl tools/probe-transcript.pl` condenses probe output.
- **Never reconstruct text.** Read the game's own strings (TMP writes, game data). No OCR. Numbers (HP, MP, stats) are sprite digits, so they must come from game data.
- **Never invent class, method or field names.** Every name comes from the proxy dumps (`research/decomp`, `research/types-Assembly-CSharp.txt`), a reference repo (`research/refs`, all MIT) or a log line, and is recorded in `docs/class-index.md`.
- Explain the reasoning before each step; give exact test steps; replies use Markdown headings (NVDA navigation).
- One system per session; commit at every working state saying what was verified in game; update `docs/HANDOFF.md` at session end.
- Keyboard first; mod keys stay in the function-key range (the game uses no F-keys; F9 to F11 belong to Nocturne Graphics Configurator, F12 to Steam).
- Denuvo: never touch or inspect `smt3hd.exe`.
- Francesco launches with `--melonloader.hideconsole` (otherwise NVDA can read the MelonLoader console, especially with a controller).
- Build: `dotnet build NocturneAccess.sln -c Release` (deploys to `Mods` automatically). References come from `_dlls/` (`tools/copy-references.ps1`). The Windows `python3` alias hangs; use perl or bash.

## State at handoff

| Version | What | Status |
| --- | --- | --- |
| 0.0.1 | NVDA speech layer, config, F8 status | Verified in game |
| 0.0.2 | Probe (TMP + message engine), F6 UI dump, F7 probe toggle, log archive | Verified; probe session mapped title to first save |
| 0.0.3 to 0.0.4 | Dialogue pages with speaker (every page), dialogue choices, F1 repeat, log snapshots | Verified in game (15:48 session) |
| 0.0.5 | Menu reader (pause, terminal, title), save/load prompts | Prompts verified; menus failed (see below) |
| **0.0.6** | Menu settle-timer fix | **Built and deployed, not yet tested** |

Last commits: `14ce42c` (0.0.5), then the 0.0.6 fix commit.

## The 0.0.5 failure and the 0.0.6 fix

The title and pause menus stayed silent and spoke once only as they closed. The log showed correct routing and visible highlight writes. Cause: those menus rewrite the highlighted item, title and description **every frame with identical text**; `MenuReader` restarted its 80 ms settle timer on every write, so it only expired when the menu closed. 0.0.6 restarts it only on a real change. General rule now: an identical rewrite must never count as activity.

## First thing next session: test 0.0.6

1. Title screen: move through every item; expect "LOAD GAME, 1 of 10. Continue your progress from a save file." and so on.
2. Load the save (prompts already work).
3. Pause menu (F): all six items, the Item, Party and System submenus and back; close and reopen (expect "COMMAND MENU" again).
4. Terminal: expect the question, then "Save, 1 of 3" and each move.
5. F1 repeat in a menu and after dialogue.

Then read the log and check positions ("N of M") against the real item counts.

## Open problems and next steps (in order)

1. **Save prompt Yes/No** (`saveUI(Clone)/saveannounce/saveannounce_choice/answer01TM`, `answer02TM`): plain "Yes"/"No" with no highlight material, so the cursor is drawn by another object or class (Francesco saw the same in Suikoden). Next: inspect `saveUI` / `saveFileUI` in `research/decomp` for cursor methods and probe them, or dump the choice's sibling objects.
2. **Save-slot list**: all empty slots are `savefile(Clone)` with identical paths; slots need identity per component (instance or sibling order). Slots use `TMC01` for the highlighted entry.
3. Remaining Phase 2 screens: config values, name entry, difficulty select, the title-screen Yes/No (`titleUI(Clone)/titlemode/talk01_choice/...`).
4. **Phase 2 gate**: new game through the prologue to the first save by ear.
5. **Phase 3 is navigation and pathfinding** (Francesco's priority, moved ahead of battle): start from `fieldUI/flocation` (location name, including the overhead map) and `fieldUI/fannounce` (Talk / Examine / Open Door / Enter / Exit prompts), then `fldPlayer`, `fldAutoMap`. Francesco can record OBS sessions for frame stills when a question is purely visual (map layout, cursor position).

## Key facts to remember

- All UI text is TextMeshPro under `Canvas_UI/...`; one Unity scene (`xrd773`), screens are `(Clone)` prefabs.
- Dialogue: every line holds its full text from the first write; the typewriter only moves `<alpha=#00>` tags; empty lines hold `<alpha=#00>あああああ</alpha>`.
- Menus mark the highlighted item with a font material: pause and terminal `TMC01` (others `TMC00`), title `TMC00` (others `TMC15`).
- Message engine `itfMesManager`: `SetMessage` never fires; `SwitchSpeaker`, `itfMesMngRequestMessage`, `SelectWindowDispOn`, `ChangeSelection(index, 1)` do.
- `UIManager/Text/Text01000+` are 116 noise components rewritten every frame with a space; the TMP router drops blank writes first.
- Code map: `Core/` (ModMain, Config, Log, Patcher, TmpRouter, TextUtil, UiUtil, LogArchive), `Speech/`, `Capture/` (probes, UI dumper), `Systems/Dialogue/` (DialogueReader, ChoiceReader), `Systems/Menus/` (MenuReader, AnnouncementReader), `Input/Hotkeys.cs`.
