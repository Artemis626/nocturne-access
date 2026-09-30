# Nocturne Access: next session handoff

Written 30 Sep 2026, updated in session 2 after the 0.0.6 test, for a fresh chat. Read this first, then `docs/HANDOFF.md` (full project source of truth), `docs/class-index.md` (every verified name and UI path) and `docs/KEYS.md`. `docs/TEXT-HOOKING-HANDOFF.md` holds Francesco's Suikoden Access design notes that the readers follow.

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
| 0.0.5 | Menu reader (pause, terminal, title), save/load prompts | Prompts verified; menus failed |
| 0.0.6 | Menu settle-timer fix | Verified in game (16:18 session) |
| **0.0.7** | Save/load Yes/No and file list; speech order (terminal race) | **Built and deployed, not yet tested** |

Last commits: `b60ef2a` (0.0.6 verified, save/load classes mapped), then the 0.0.7 commit.

## What 0.0.6 proved and what 0.0.7 adds

0.0.6 fixed the silent menus (16:18 session): title, terminal and pause menus are spoken on every move. Rule kept from it: an identical rewrite must never count as activity.

0.0.7 (built, not tested) finishes the save/load screen and fixes the terminal race:

- **Terminal race**: the question was spoken after "Save, 1 of 3", because the question's lines arrive about 400 ms after the menu settles. Now a screen's first line waits (at most 1.5 s) while a dialogue page or prompt is due (`Systems/SpeechOrder.cs`).
- **Save/load Yes/No**: the options have no highlight material (two F6 dumps identical). The cursor is `slMain.slQueryYesNoCursor`, read every frame, indexing the game's own `saveUI._choiceList` texts (`SaveChoiceReader`).
- **File list**: slot identity from `saveUI._fileList`, numbers from `CounterCtr.value` (`SaveListReader`).

## First thing next session: test 0.0.7

1. Title screen, LOAD GAME. Expect "Checking data...", "Select a file to load.", then "File 1. AAAA, Level 1, Shinjuku Medical Center, NORMAL" (Francesco's save; the level is whatever the screen shows).
2. Move down: expect "File 2. No File", "File 3. No File"; then back up to File 1.
3. Confirm File 1: expect "Load File 1?" then "Choice, 2 options. Yes, 1 of 2" (or No, if the game defaults to No). Move between Yes and No: each move spoken. Choose No first (back to the list, nothing lost), then confirm File 1 again and choose Yes.
4. In game, go to the terminal: expect the question "> There is a large cylinder. What will you do?" first, then "Save, 1 of 3".
5. Choose Save: expect "Select a save location." and the list; pick an empty file (File 2), expect the Yes/No if the game asks, then "Save completed.". Or pick File 1 to hear the overwrite prompt with its Yes/No.
6. F1 on the list and on the Yes/No: expect the file again, and "Load File 1? Yes, 1 of 2".

Then read the log: `[SaveChoice] Opened: ...` gives the option order, the cursor value and the default; `[SaveList] Slot N: fileNum ...` gives the raw numbers to check against the screen (file number, level, play time); `[Order] ... waits for ...` confirms the terminal fix. A `[SaveChoice] Cursor field says option X, highlight is over option Y` warning means the cursor mapping needs another look.

## Open problems and next steps (in order)

1. **Check 0.0.7's assumptions against the log**: `slQueryYesNoCursor` index 0 = `_choiceList[0]` ("Yes"); `_fileNum` / `_noDataNum` is the number shown on each slot; `_playerLV` is the level. Decide how to speak play time once its `CounterCtr` values are seen.
2. **Pause menu sub-screens are silent** (mapped in class-index, "Pause menu sub-screens"): item lists (`menu_itemskill/menu_item_window/menuitem_top`, `menuitem_base02..08`, highlight `TMC01`, title from `title_window_oubei`, description `menu_note/Text01TM..03TM`) fit MenuReader as a new spec; the skill user pick (party name turns `TMC01`); Magatama and Stats need numbers from `CounterCtr`.
3. Remaining Phase 2 screens: config values, name entry, difficulty select, the title-screen Yes/No (`titleUI(Clone)/titlemode/talk01_choice/...`).
4. **Phase 2 gate**: new game through the prologue to the first save by ear.
5. **Phase 3 is navigation and pathfinding** (Francesco's priority, moved ahead of battle): start from `fieldUI/flocation` (location name, including the overhead map) and `fieldUI/fannounce` (Talk / Examine / Open Door / Enter / Exit prompts), then `fldPlayer`, `fldAutoMap`. Francesco can record OBS sessions for frame stills when a question is purely visual (map layout, cursor position).

Numbers everywhere (HP, MP, level, stats, play time) can come from `CounterCtr.value` / `CounterCtr.Set(int, ...)` (48 callers): the sprite-digit display holds the number as data, which respects the no-reconstruction rule.

## Key facts to remember

- All UI text is TextMeshPro under `Canvas_UI/...`; one Unity scene (`xrd773`), screens are `(Clone)` prefabs.
- Dialogue: every line holds its full text from the first write; the typewriter only moves `<alpha=#00>` tags; empty lines hold `<alpha=#00>あああああ</alpha>`.
- Menus mark the highlighted item with a font material: pause and terminal `TMC01` (others `TMC00`), title `TMC00` (others `TMC15`).
- Message engine `itfMesManager`: `SetMessage` never fires; `SwitchSpeaker`, `itfMesMngRequestMessage`, `SelectWindowDispOn`, `ChangeSelection(index, 1)` do.
- `UIManager/Text/Text01000+` are 116 noise components rewritten every frame with a space; the TMP router drops blank writes first.
- Code map: `Core/` (ModMain, Config, Log, Patcher, TmpRouter, TextUtil, UiUtil, LogArchive), `Speech/`, `Capture/` (probes, UI dumper), `Systems/Dialogue/` (DialogueReader, ChoiceReader), `Systems/SpeechOrder.cs` (first line waits behind dialogue or a prompt), `Systems/Menus/` (MenuReader, AnnouncementReader, SaveListReader, SaveChoiceReader), `Input/Hotkeys.cs`.
