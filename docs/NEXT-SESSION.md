# Nocturne Access: next session handoff

Written 30 Sep 2026, updated in session 2 after the 0.0.10 test (analysis only), for a fresh chat. Read this first (after `CLAUDE.md`), then `docs/HANDOFF.md` (full project source of truth), `docs/class-index.md` (every verified name and UI path) and `docs/KEYS.md`. `docs/TEXT-HOOKING-HANDOFF.md` holds Francesco's Suikoden Access design notes that the readers follow.

## Next: the overworld (2 Oct 2026)

Francesco reached the overworld (Shinjuku, field 2) and starts a new chat for it: read `docs/HANDOFF-OVERWORLD.md` (facts from the 20:10 session, its two dumps and the video; the plan, one step per test run). The dungeon tools stay as they are. Small open items: a demon conversation line spoken twice, the Annex 1F ramp item box, custom waypoints later.

**Overworld chat, 2 Oct 2026: step 1 done; 0.0.39 (step 2, Page Up / Page Down on the world map) verified at 21:03; 0.0.40 (battle talk lines no longer doubled, the Continue screen read) and 0.0.41 (step 3: world-map walking map and End) built, not yet tested.** Test both (`docs/HANDOFF.md`, end; `docs/HANDOFF-OVERWORLD.md` "Test (0.0.41)"), then step 4 (Home on the world map). Never push to GitHub unless Francesco gives the order.

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
| 0.0.12 to 0.0.15 | Navigation diagnostics (F5, trace), first person in memory (V), pad injection | Verified 1 Oct 2026 (12:04 to 13:13 sessions) |
| **0.0.16** | Blobber mode version 1 (F4) | Tested 1 Oct 13:39: forward grid steps good; input takeover and step refusals rejected by Francesco. Redesign in `docs/NAVIGATION-DESIGN.md` section 11 |
| **0.0.17** | Blobber mode version 2: game keys untouched, W release snaps to the next square, cues at square centres, NPC boxes not walls | Verified 1 Oct 14:31: "a significant improvement" (Francesco); section 12 and 13 |
| **0.0.18** | F3 centring key (walk to the centre of the current square with the game's own moves) | Verified 1 Oct 14:52 (23 of 25; Francesco: "feels and looks great") |
| **0.0.19** | Centring fixes: ignored during dialogue, diagonal facing squared first | Built 1 Oct 2026, not yet tested; section 14 |
| **0.0.20** | Pathfinding scaffold: points of interest (F5, Shift+F5), walkability grid and search, auto-walk in fixed-camera rooms and third person (Ctrl+F5), modifier keys, navigation dump on Shift+F6 with the grid map | Built 1 Oct 2026, **not yet tested in game**; `docs/PATHFINDING.md` |
| **0.0.21** | Point-of-interest keys: Page Up / Page Down / Home (Francesco: single keys) | Verified 1 Oct 15:20: auto-walk to the Soul and the door "worked perfectly" |
| **0.0.22** | Only reachable points of interest are listed (the item box and door of the unconnected room west of the Healing Room are left out) | Verified 1 Oct 19:07 ("worked very well"); arrival needed facing (fixed in 0.0.23) |
| **0.0.23** | Basement: doors named with the game's place names (Terminal, Healing Room, Elevator), Souls, first-person auto-walk with the game's turns, facing at arrival | Tested 1 Oct 19:57 in the prologue (field 11, Medical Center): several walks arrived; an event, a door behind a wall, a stale hit index, the stairwell's second floor and event volumes listed as doors went wrong (section 6) |
| **0.0.24** | Fixes: walk ends on events, multi-level walking map (stairs, "upstairs"), arrival through `OldHitDoor`, doors behind walls left out, list duplicates merged, door cue within 45 degrees | Built 1 Oct 2026, **not yet tested in game**; `docs/PATHFINDING.md` section 6 |
| **0.0.25** | Basement test of 0.0.24: hidden NPCs (inactive object) left out of the list and the walking map, no spin next to a person, start off the route allowed; **End: spoken directions** to the selected point | Played 20:37: hidden NPC gone, walks fine; End too talkative | 
| **0.0.26** | End speaks directions only when pressed; Home turns to a door even right at it | Played 21:31 (1F, 2F, annex gate): End fine, about 30 walks arrived |
| **0.0.27** | Elevator floor banner not spoken after the question; volume heights from their boxes (Pixie listed); walks end in front of doors and on a line with people; blobber square centres per area (2F corridors) | Played 22:14: elevator banner gone, walks in front of doors, 2F square offset 300 |
| **0.0.28** | Battle: "Pixie's turn" before each command menu; F2 in short sentences with "2 press turns left: 1 full and 1 half"; enemies read by slot, so HP is spoken after others fall; Shift+F2 battle dump. Square offset: middle of the near-best range | Played 2 Oct 09:14: "Pixie's turn" and F2 worked; asked for costs, weakness, Analyze, a split F2, a battle log |
| **0.0.29** | Battle: skill costs in the menu ("Lunge, 4 HP"), weakness and critical hits for both sides, the Analyze screen, "Enemy turn." / "Player turn.", damage and healing amounts, enemy names on their actions, results with EXP, macca and items; F2 our side, F3 the enemies, F5 the battle log | Verified 2 Oct 10:07 (weakness, Analyze, F5, results items) |
| **0.0.30** | Cleanup and debug pass, no behaviour change | Verified 2 Oct 10:07 ("exactly as before") |
| **0.0.31** | `docs/REFACTOR-PLAN.md` items 0 to 5: no HP line before a battle starts (an overflow on the session's first battle); `BattleReader` in partial files; one dump writer; one announcement entry per path; hidden-NPC read every 500 ms in blobber mode; trace once a second | Verified 2 Oct 12:03 (no regression; no HP line before the start) |
| **0.0.32** | Item and skill lists read (cursor from the game, counts, costs, "unavailable", descriptions); Stats adds skills, skills to learn and affinities | Tested 13:20: every entry read as the first row (cursor fields swapped); elevator left out of B1 (walking map window) |
| **0.0.33** | Fixes: list row from `Shift`; "Empty." for an empty list; the walking map keeps whole areas that fit (elevator back in B1) | Verified 2 Oct 13:45 (lists, Stats skills, elevator) |
| **0.0.34** | Talking demons listed ("Demon"); the walking map and wall cues follow the game's own collision switches (annex gate open after the pass); Magatama screen read | Partly tested 2 Oct 14:48: demons listed, collision switches read; gate, demon talk and Magatama not reached |
| **0.0.35** | `docs/EXPLORED-DOORS-PLAN.md` items 1 to 3: generic doors gone through say "explored" (Page Up / Page Down, Home, End), kept per save file in `UserData/NocturneAccess/Explored/fileNN.txt`; item boxes the game has opened say "empty" | Verified 2 Oct 16:11 (marks both sides, End and Home wording, saved per file; suspend data keeps its own list) |
| **0.0.36** | Blobber cues silent while the game moves the player itself (the cutscene before Forneus sounded like a walk) | Built 2 Oct 2026, not yet tested |
| **0.0.37** | Door warp table (where each door leads) decoded and written to the Shift+F6 dump, nothing spoken yet | Verified 2 Oct 17:11 (matches every door gone through); analysis in `docs/EXPLORED-DOORS-PLAN.md`; next: Francesco picks the wording |
| **0.0.38** | Door kinds: a plain door says "room", "passage" or "exit" (Page Up / Page Down, Home, End); off with `DoorKinds = false` in the config, revert point f0621c8 | Verified 2 Oct 20:10 ("Door, explored, passage") |
| **0.0.39** | World map step 2: Page Up / Page Down list entrances ("Yoyogi Park entrance"), people, Souls and item boxes on the world map (fieldID below 10, the game's own test); End and Home say "not available on the world map yet"; no "Grid: no floor" flood | Verified 2 Oct 21:03 (10 points read at Yoyogi Park) |
| **0.0.40** | Battle talk pages no longer spoken twice (identical page only after `ClearMessage` / a request / window off); the title Continue screen reads its slot | Built 2 Oct 2026, not yet tested |
| **0.0.41** | World map step 3: a walking map for the world map (world-map walls, calibrated on the traces), Page Up filters by it, End speaks compass routes there | Built 2 Oct 2026, not yet tested |

Last commits: `9d773ef` (0.0.10), then the analysis-only docs commit of the 22:05 session (includes the new `CLAUDE.md`).

## 0.0.35 test (explored doors, empty boxes)

What to check in the log: `[Nav:Explored]` lines (`File N loaded`, `... marked (left through it ...)`, `... marked (came in through it ...)`, `Saved to file N (method)`), the Page Up list lines (`[Nav:Poi]`, with "explored" / "empty"), and that `[Patch] Explored doors` shows 4 patched.

1. Load your save. The log says `File N loaded: 0 explored doors` (the first time).
2. In the annex, go through a plain door ("Door", not Terminal, Healing Room or Elevator) and come back the same way.
3. Page Up to that door: "Door, explored, ...". On the other side, the door you came in by says "explored" too.
4. Home on it: "Walking to explored door, ...". End: "Explored door. Ahead, ...".
5. Terminal, Healing Room and Elevator never say "explored".
6. An item box you have opened: "Item box, empty, ..." before walking to it; one not yet opened says only "Item box". Open a new one, Page Up again: now "empty".
7. Save at a terminal to the same file, quit to desktop, restart, load it: the doors still say "explored". The log says `Saved to file N` and `File N loaded: K explored doors`.
8. Optional: load a different file: its own doors (none at first), not the first file's.

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

**1 Oct 2026, evening: navigation continues from `docs/HANDOFF-PATHFINDING.md` (rewritten): Francesco is playing through the game. **0.0.31 (the refactor plan) verified; 0.0.34 partly tested. **0.0.35 verified (explored doors per save file); "empty" item boxes right in the log (opened Annex 1F boxes marked, Lobby boxes not) but not yet spoken on a reachable one. 0.0.36: blobber cues silent during scripted movement (test: a cutscene that moves the player should be silent, walking afterwards cues as before). Item 4, custom waypoints, later only.** Open: the Annex 1F ramp item box (`docs/HANDOFF.md`, end); still to test from 0.0.34: annex gate after the pass, talking to a demon via Home, Magatama**. He reports bugs and missing features: read each session's log, fix, record (`docs/HANDOFF-PATHFINDING.md`), then the open problems listed there.**

**1 Oct 2026, end of the first navigation chat: continue navigation from `docs/HANDOFF-PATHFINDING.md` (rewritten): blobber mode must keep the game's own controls, snap only movement, and never block on cues; fixed-camera rooms get points of interest with auto-walk later.**

**Francesco, 1 Oct 2026: 0.0.11 verified; navigation now comes first (3D dungeons, not the world map), in a new chat: start with `docs/HANDOFF-PATHFINDING.md`. Decided: blobber mode is a toggle, on by default. First person (V) is locked to New Game+ and must be unlocked in memory first (`docs/NAVIGATION-DESIGN.md` section 5).**

1. Navigation, `docs/NAVIGATION-DESIGN.md` section 4: diagnostic run, input proof of concept, read-only aids, grid and A*, blobber mode.
2. Name entry control buttons: read `Nme_Draw_EFGIS.cursor_str` (path under the cursor) while Francesco visits forward, back, delete, done.
3. **Combat version 2**: whose turn and who acts ("Will o' Wisp: Deathtouch"), damage amounts, weak/resist/null results, Press Turns after each action, skill and item menus in battle, negotiation, the results screen's numbers (EXP, macca).
4. **Numbers for the Magatama screen and item lists** (`CounterCtr.value`); the config volume notches (`configMenuUI.slideObj` was empty: look for `CounterCtr` or `soundVol`).
5. A correct press-any-button cue (the version text already shows during the Atlus splash).
6. Difficulty select screen.

## Key facts to remember

- All UI text is TextMeshPro under `Canvas_UI/...`; one Unity scene (`xrd773`), screens are `(Clone)` prefabs.
- Dialogue: every line holds its full text from the first write; the typewriter only moves `<alpha=#00>` tags; empty lines hold `<alpha=#00>あああああ</alpha>`.
- Menus mark the highlighted item with a font material: pause and terminal `TMC01` (others `TMC00`), title `TMC00` (others `TMC15`).
- Message engine `itfMesManager`: `SetMessage` never fires; `SwitchSpeaker`, `itfMesMngRequestMessage`, `SelectWindowDispOn`, `ChangeSelection(index, 1)` do.
- `UIManager/Text/Text01000+` are 116 noise components rewritten every frame with a space; the TMP router drops blank writes first.
- Code map: `Core/` (ModMain, Config, Log, Patcher, TmpRouter, TextUtil, UiUtil, LogArchive), `Speech/`, `Capture/` (probes, UI dumper), `Systems/Dialogue/` (DialogueReader, ChoiceReader), `Systems/SpeechOrder.cs` (first line waits behind dialogue or a prompt), `Systems/Menus/` (MenuReader, AnnouncementReader, SaveListReader, SaveChoiceReader, ConfigReader, NameEntryReader, StatusReader), `Systems/Battle/` (BattleReader), `Input/Hotkeys.cs`.
