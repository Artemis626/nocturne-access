# Nocturne Access: next session handoff

Written 30 Sep 2026, updated in session 2 after the 0.0.7 test, for a fresh chat. Read this first, then `docs/HANDOFF.md` (full project source of truth), `docs/class-index.md` (every verified name and UI path) and `docs/KEYS.md`. `docs/TEXT-HOOKING-HANDOFF.md` holds Francesco's Suikoden Access design notes that the readers follow.

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
| 0.0.7 | Save/load Yes/No and file list; speech order (terminal race) | Verified in game (16:49 session) |
| **0.0.8** | Title and config Yes/No, location names, press-any-button cue, config screen | **Built and deployed, not yet tested** |

Last commits: `c90e8a9` (0.0.7 verified; title, splash and config mapped), then the 0.0.8 commit.

## What 0.0.7 proved and what 0.0.8 adds

0.0.7 verified (16:49 session): the terminal says its question first; loading and saving are fully usable by ear (file list with numbers and levels, the load and overwrite Yes/No). Francesco then asked for: the title screen's EXIT Yes/No (silent), a cue on the press-any-button screen, the map name when a map loads, and the config, Magatama and Stats screens (F6 dumps taken). After those, combat, then pathfinding.

0.0.8 (built, not tested):

- **Title EXIT and config Yes/No**: options highlighted with `TMC08`; the question is spoken first, then "Choice, 2 options. Yes, 1 of 2" and each move. Covers the difficulty confirmation too (same paths under `titlemode`), not yet seen in a log.
- **Location name**: `fieldUI/flocation` is spoken when it appears or changes.
- **Press-any-button cue**: "Title screen. Press any button." (the game's prompt is an image, so these are the mod's words).
- **Config screen**: tab, row, value (text, bound keys or volume slider), position and description; value changes spoken alone.

## First thing next session: test 0.0.8

1. Launch: at the press-any-button screen expect "Title screen. Press any button."
2. Title menu, CONFIG. Expect "AUDIO tab. CONFIRM, OK, 1 of 7. Apply changed settings." Move down: each row with its value and description. On BGM VOLUME press left/right: expect the new number alone. Switch tabs (the game's tab keys): expect "GAME tab. ..." and so on through KEYBOARD+MOUSE, where rows should say their key ("Command Menu, F, ...").
3. Leave config without changing anything: expect "Returning without changing settings. Will you proceed?" then "Choice, 2 options. ...", each move, then Yes.
4. Title menu, EXIT: expect "> End the game?" then "Choice, 2 options. Yes, 1 of 2", move, choose No; then later EXIT and Yes to quit.
5. Load the save: after "Load completed." expect "Underground Facility". Walk to the terminal room door: expect "Terminal". Open and close the pause menu: tell me if the location is spoken again each time (it should not be).
6. F1 on a config row.

Then read the log: `[Config] Slider on BGM VOLUME: value ..., min ..., max ...` gives the volume range; `[Config] No configMenuUI on ...` lines mean a row's value was not found; `[TitleCue]` confirms the cue.

## Open problems and next steps (in order)

1. **Check 0.0.8** against the log (slider range and wording, config positions against the screen, whether the location repeats).
2. **Numbers system, then Magatama, Stats and item lists** (last Phase 2 screens Francesco asked for): these screens' real content is sprite digits (stats, HP, MP, level). Read them from `CounterCtr.value` (verified on the save list: `_fileNum`, `_playerLV`, `_playTime`), found through the screen's own classes (`statusUI`, `magUI`, `campUI` party panel) or by routing `CounterCtr.Set(int, ...)` writes by path like the TMP router. Text parts are mapped in class-index ("Pause menu sub-screens"): Magatama name `magname_base` (`TMC19`) with `magnote` lines; Stats name, race, skills, feature; item lists (`menu_itemskill`, `TMC01`, description `menu_note`); skill user pick (party name `TMC01`).
3. Remaining small Phase 2 screens: name entry, difficulty select (`titlemode_item02..03`, `titlemode_note`).
4. **Phase 2 gate**: new game through the prologue to the first save by ear.
5. **Combat** (Francesco's order: after these screens, before pathfinding).
6. **Pathfinding and navigation**: `fieldUI/fannounce` (Talk / Examine / Open Door / Enter / Exit), `fldPlayer`, `fldAutoMap`, the overhead map. OBS stills from Francesco when a question is purely visual.

## Key facts to remember

- All UI text is TextMeshPro under `Canvas_UI/...`; one Unity scene (`xrd773`), screens are `(Clone)` prefabs.
- Dialogue: every line holds its full text from the first write; the typewriter only moves `<alpha=#00>` tags; empty lines hold `<alpha=#00>あああああ</alpha>`.
- Menus mark the highlighted item with a font material: pause and terminal `TMC01` (others `TMC00`), title `TMC00` (others `TMC15`).
- Message engine `itfMesManager`: `SetMessage` never fires; `SwitchSpeaker`, `itfMesMngRequestMessage`, `SelectWindowDispOn`, `ChangeSelection(index, 1)` do.
- `UIManager/Text/Text01000+` are 116 noise components rewritten every frame with a space; the TMP router drops blank writes first.
- Code map: `Core/` (ModMain, Config, Log, Patcher, TmpRouter, TextUtil, UiUtil, LogArchive), `Speech/`, `Capture/` (probes, UI dumper), `Systems/Dialogue/` (DialogueReader, ChoiceReader), `Systems/SpeechOrder.cs` (first line waits behind dialogue or a prompt), `Systems/Menus/` (MenuReader, AnnouncementReader, SaveListReader, SaveChoiceReader, ConfigReader, TitleCue), `Input/Hotkeys.cs`.
