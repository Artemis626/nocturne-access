# Nocturne Access: next session handoff

Written 30 Sep 2026, updated in session 2 after the 0.0.10 test (analysis only), for a fresh chat. Read this first (after `CLAUDE.md`), then `docs/HANDOFF.md` (full project source of truth), `docs/class-index.md` (every verified name and UI path) and `docs/KEYS.md`. `docs/TEXT-HOOKING-HANDOFF.md` holds Francesco's Suikoden Access design notes that the readers follow.

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
| 0.0.10 | Interaction prompts, name entry, elevator order, battle targets, Stats and level-up | Tested 22:05: prompts and Stats summary work; party pick, pre-filled names and name buttons missing; battle targets and level-up untested |
| **0.0.11** | Party pick, Stats race, pre-filled names, prompt dedupe, name button diagnostic | **Verified 1 Oct 2026 (09:47 session); level-up allocation verified too** |

Last commits: `9d773ef` (0.0.10), then the analysis-only docs commit of the 22:05 session (includes the new `CLAUDE.md`).

## What 0.0.10's test showed (22:05 session, analysis only, no code)

- **Interaction prompts work perfectly** ("Open Door", "Talk"); occasionally spoken twice within 100 ms.
- **Stats screen summary works** (name, level, HP, MP, five stats from `CounterCtr`). Missing: the race (spoken before the race text was written) and **the party pick "Art"** before the Stats screen (a `TMC01` highlight on `party_status01/Text_nameTM` under the title "STATS", not covered by any menu spec). Level-up allocation not tested yet.
- **Name entry letters read very well.** Missing: **pre-filled names** at opening (Yuko's "Takao"/"Yuko"), and **the control buttons forward, back, delete, done** (image buttons, no text).

## Next session: fix these, then test

1. **Party pick** (Stats, Skill "Who will use a skill?", and later Party screens): a MenuReader spec for `campUIBase/campUI/party/party_status/party_status0N/Text_nameTM` (and `stock_status0N`) with highlight `TMC01` and the title from `title_window_oubei`. Also the stale "STATS" title when returning to the pause menu.
2. **Stats summary**: wait until the race text is written (settle about 300 ms after the last `statusUI` write, or read `Text_raceTM_oubei` / `raceText_e`), then speak.
3. **Name entry pre-filled name**: when the screen opens with letters already filled, say them after "Enter a name." ("Family: Takao. Given: Yuko.").
4. **Name entry control buttons**: find which button has the cursor from `Nme_Draw` (all static; `Nme_Draw_EFGIS` Western variant): log `funccursorPos`, `box_num`, `letter_num` and the active state of `namelist_rightbutton`, `namelist_leftbutton`, `namelist_deletebutton`, `namelist_endbutton` while Francesco moves onto them, then speak the button (the mod's own words: "Forward", "Back", "Delete", "Done"; their images have no text).
5. **Prompt spoken twice**: dedupe the reshow check against a write in the same 100 ms window.
6. Test the level-up allocation (cursor row, value changes, points left) at the first battle.

## Open problems and next steps (in order)

1. The fixes above (items 1 to 6).
2. **Combat version 2**: whose turn and who acts ("Will o' Wisp: Deathtouch"), damage amounts, weak/resist/null results, Press Turns after each action, skill and item menus in battle, negotiation, the results screen's numbers (EXP, macca).
3. **Numbers for the Magatama screen and item lists** (`CounterCtr.value`); the config volume notches (`configMenuUI.slideObj` was empty: look for `CounterCtr` or `soundVol`).
4. A correct press-any-button cue (the version text already shows during the Atlus splash).
5. Difficulty select screen.
6. **Pathfinding and navigation** after combat (Francesco's order).

## Key facts to remember

- All UI text is TextMeshPro under `Canvas_UI/...`; one Unity scene (`xrd773`), screens are `(Clone)` prefabs.
- Dialogue: every line holds its full text from the first write; the typewriter only moves `<alpha=#00>` tags; empty lines hold `<alpha=#00>あああああ</alpha>`.
- Menus mark the highlighted item with a font material: pause and terminal `TMC01` (others `TMC00`), title `TMC00` (others `TMC15`).
- Message engine `itfMesManager`: `SetMessage` never fires; `SwitchSpeaker`, `itfMesMngRequestMessage`, `SelectWindowDispOn`, `ChangeSelection(index, 1)` do.
- `UIManager/Text/Text01000+` are 116 noise components rewritten every frame with a space; the TMP router drops blank writes first.
- Code map: `Core/` (ModMain, Config, Log, Patcher, TmpRouter, TextUtil, UiUtil, LogArchive), `Speech/`, `Capture/` (probes, UI dumper), `Systems/Dialogue/` (DialogueReader, ChoiceReader), `Systems/SpeechOrder.cs` (first line waits behind dialogue or a prompt), `Systems/Menus/` (MenuReader, AnnouncementReader, SaveListReader, SaveChoiceReader, ConfigReader, NameEntryReader, StatusReader), `Systems/Battle/` (BattleReader), `Input/Hotkeys.cs`.
