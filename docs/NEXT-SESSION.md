# Nocturne Access: next session handoff

Written 30 Sep 2026, updated in session 2 after the 0.0.9 prologue test, for a fresh chat. Read this first, then `docs/HANDOFF.md` (full project source of truth), `docs/class-index.md` (every verified name and UI path) and `docs/KEYS.md`. `docs/TEXT-HOOKING-HANDOFF.md` holds Francesco's Suikoden Access design notes that the readers follow.

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
| 0.0.8 | Title and config Yes/No, location names, config screen (title cue removed in 0.0.9) | Verified in game (20:07 session) |
| 0.0.9 | Combat version 1: start, command menu, banner, HP changes, F2 status | Verified over the whole prologue (20:44 session) |
| **0.0.10** | Interaction prompts, name entry, elevator order, battle targets, Stats and level-up | **Built and deployed, not yet tested** |

Last commits: `11e737b` (0.0.9 verified over the whole prologue; name entry and level-up mapped), then the 0.0.10 commit.

## What 0.0.9 proved and what 0.0.10 adds

0.0.9 verified (20:44 session): Francesco replayed the whole prologue with his eyes closed, NEW GAME to the first battle and save, and nearly everything worked by ear. His list, all addressed in 0.0.10 (built, not tested):

1. Interaction prompts ("Talk", "Open Door", "Examine", "Enter", "Exit") are now spoken each time they appear.
2. Name entry: cursor letter, typed and deleted letters, character set, confirmation with the whole name, Yes/No.
3. Elevator: the floor ("1Ｆ") now waits behind "> Select a floor.".
4. Combat: identical enemies are told apart while targeting ("Foul Will o' Wisp 2, HP 10 of 10"); the battle start is no longer repeated when an enemy talks; "Battle results." once.
5. Level-up and Stats: a summary with every number from game data, then the stat under the cursor and points left.

## First thing next session: test 0.0.10

A new game (or a load before the prologue's name entry) covers everything:

1. NEW GAME, difficulty, then the name screen. Expect "Enter a name." then "capital A". Move: each letter ("capital B", "r"). Type a letter: its echo; delete one: "Deleted ...". Switch character set (the game's left/right keys): "ÀÁÂ, accented letters". Press F1: the name so far. Finish: "Enter a nickname.", then the confirmation "Full Name. Family: .... Given: .... Nickname: .... > Is this okay?" and "Choice, 2 options. Yes, 1 of 2".
2. Yuko's name screen: the confirmation with "Takao / Yuko" should be read straight away.
3. Walk around Shibuya and the hospital: every "Talk", "Examine", "Open Door", "Enter" when something is in reach, again after walking away and back.
4. The elevator: "> Select a floor." first, then the floor.
5. The first battle (two Will o' Wisps): moving the target cursor says "Foul Will o' Wisp 1, HP ..." and "... 2, HP ..."; no repeated "Battle. ..." when they talk; "Battle results." once.
6. Level up: the Stats summary after "> You leveled up!", then each stat as the cursor moves, its new value when a point is added, and "N points left". Compare the numbers with the screen, and tell me whether the stat the cursor is on matches.
7. The Stats screen from the pause menu: the same summary.

Then read the log: `[Status] Opened: ...` lists every counter under each stat object (check the numbers and which one is the stat value), `[Battle] Target formindex ...`, `[Name] ...`, `[Order] ...`.

## Open problems and next steps (in order)

1. **Check 0.0.10** against the log and Francesco's report (Stats counter mapping, cursor row, name entry pronunciation of accented letters and symbols).
2. **Combat version 2**: whose turn and who acts ("Will o' Wisp: Deathtouch"), damage amounts, weak/resist/null results, Press Turns after each action, skill and item menus in battle, negotiation, the results screen's numbers (EXP, macca).
3. **Numbers system for the Magatama screen and item lists** (`CounterCtr.value`); the config volume notches (`configMenuUI.slideObj` was empty: look for their `CounterCtr` or `soundVol`).
4. A correct press-any-button cue (the version text already shows during the Atlus splash).
5. Difficulty select screen; the Phase 2 gate is essentially passed (prologue by ear).
6. **Pathfinding and navigation** after combat (Francesco's order).

## Key facts to remember

- All UI text is TextMeshPro under `Canvas_UI/...`; one Unity scene (`xrd773`), screens are `(Clone)` prefabs.
- Dialogue: every line holds its full text from the first write; the typewriter only moves `<alpha=#00>` tags; empty lines hold `<alpha=#00>あああああ</alpha>`.
- Menus mark the highlighted item with a font material: pause and terminal `TMC01` (others `TMC00`), title `TMC00` (others `TMC15`).
- Message engine `itfMesManager`: `SetMessage` never fires; `SwitchSpeaker`, `itfMesMngRequestMessage`, `SelectWindowDispOn`, `ChangeSelection(index, 1)` do.
- `UIManager/Text/Text01000+` are 116 noise components rewritten every frame with a space; the TMP router drops blank writes first.
- Code map: `Core/` (ModMain, Config, Log, Patcher, TmpRouter, TextUtil, UiUtil, LogArchive), `Speech/`, `Capture/` (probes, UI dumper), `Systems/Dialogue/` (DialogueReader, ChoiceReader), `Systems/SpeechOrder.cs` (first line waits behind dialogue or a prompt), `Systems/Menus/` (MenuReader, AnnouncementReader, SaveListReader, SaveChoiceReader, ConfigReader, NameEntryReader, StatusReader), `Systems/Battle/` (BattleReader), `Input/Hotkeys.cs`.
