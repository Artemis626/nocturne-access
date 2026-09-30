# Nocturne Access: next session handoff

Written 30 Sep 2026, updated in session 2 after the 0.0.8 test, for a fresh chat. Read this first, then `docs/HANDOFF.md` (full project source of truth), `docs/class-index.md` (every verified name and UI path) and `docs/KEYS.md`. `docs/TEXT-HOOKING-HANDOFF.md` holds Francesco's Suikoden Access design notes that the readers follow.

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
| **0.0.9** | Combat version 1: start, command menu, banner, HP changes, F2 status | **Built and deployed, not yet tested** |

Last commits: `7f79c46` (0.0.8), then the 0.0.9 commit.

## What 0.0.8 proved and what 0.0.9 adds

0.0.8 verified (20:07 session): the config screen, the config and title Yes/No, and location names all worked. Francesco: volume notches without numbers can wait; the title cue fired on the Atlus splash, so it is removed; focus on combat. He fought the first battle (Will o' Wisp) with F6 dumps; the battle's text sources are in class-index, "Battle".

0.0.9 (built, not tested) is combat version 1: battle start with the enemies, the command menu with help, the banner (actions, targets with HP, results such as "Dodged the attack!"), HP changes after hits, "Battle results.", and F2 for Press Turns and everyone's HP and MP.

## First thing next session: test 0.0.9

1. Load, walk into the Unknown Realm (or wherever battles happen) until a battle starts. Expect "Battle. " and the enemy names, then the enemy's talk.
2. Command menu: expect "Attack, 1 of 2. Physical attack (low damage)." and each move with its help line.
3. Choose Attack: the target should be spoken with its HP ("Foul Will o' Wisp, HP 30 of 30"); move between targets if there are several.
4. Watch the turn play out: expect "Attack", then the result ("Dodged the attack!") or the target's new HP ("Will o' Wisp, HP 12 of 30."); the enemy's skill name ("Deathtouch") and your HP after its hit.
5. Press F2 at your turn: Press Turns, then enemies and party with HP (and MP). Compare with the screen: the number of Press Turn icons, and whether any are blinking.
6. Win: expect "Battle results.".

Then read the log: `[Battle:Data]` lines show every raw value at each change (turn, phase, activeunit, press4_p, press4_ten, counts, exp, maka, units). Check the Press Turn numbers and the HP against what Francesco saw, and find which field says whose turn it is.

## Open problems and next steps (in order)

1. **Check 0.0.9** against the log: Press Turn meaning, HP numbers, whether party formindex matches the panel slot, what `activeunit`, `turn` and `phase` do, and the results' `exp` and `maka`.
2. **Combat version 2**: whose turn it is ("Demi-fiend's turn") and who acts ("Will o' Wisp: Deathtouch"), damage amounts, weaknesses and resistances (hit results such as Weak, Resist, Null), Press Turns spoken after each action, skills and items menus in battle, targeting with duplicate enemies (via `nbTarSelProcessData_t.nowform` or the acting unit's `nowtform`), negotiation, the results screen's numbers, the level-up stat allocation (`statusUI`).
3. **Numbers system for Magatama, Stats and item lists** (`CounterCtr.value`, see class-index "Save and load"); the config volume notches.
4. A correct press-any-button cue (the Version text appears already during the Atlus splash).
5. Remaining small Phase 2 screens: name entry, difficulty select; the Phase 2 gate (prologue to first save by ear).
6. **Pathfinding and navigation** after combat (Francesco's order).

## Key facts to remember

- All UI text is TextMeshPro under `Canvas_UI/...`; one Unity scene (`xrd773`), screens are `(Clone)` prefabs.
- Dialogue: every line holds its full text from the first write; the typewriter only moves `<alpha=#00>` tags; empty lines hold `<alpha=#00>あああああ</alpha>`.
- Menus mark the highlighted item with a font material: pause and terminal `TMC01` (others `TMC00`), title `TMC00` (others `TMC15`).
- Message engine `itfMesManager`: `SetMessage` never fires; `SwitchSpeaker`, `itfMesMngRequestMessage`, `SelectWindowDispOn`, `ChangeSelection(index, 1)` do.
- `UIManager/Text/Text01000+` are 116 noise components rewritten every frame with a space; the TMP router drops blank writes first.
- Code map: `Core/` (ModMain, Config, Log, Patcher, TmpRouter, TextUtil, UiUtil, LogArchive), `Speech/`, `Capture/` (probes, UI dumper), `Systems/Dialogue/` (DialogueReader, ChoiceReader), `Systems/SpeechOrder.cs` (first line waits behind dialogue or a prompt), `Systems/Menus/` (MenuReader, AnnouncementReader, SaveListReader, SaveChoiceReader, ConfigReader), `Systems/Battle/` (BattleReader), `Input/Hotkeys.cs`.
