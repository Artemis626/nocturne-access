# Nocturne Access: next session handoff

Written 30 Sep 2026, updated in session 2 after the 0.0.10 test (analysis only), for a fresh chat. Read this first (after `CLAUDE.md`), then `docs/HANDOFF.md` (full project source of truth), `docs/class-index.md` (every verified name and UI path) and `docs/KEYS.md`. `docs/TEXT-HOOKING-HANDOFF.md` holds Francesco's Suikoden Access design notes that the readers follow.

## Next: shops, Fountain and fusion (3 Oct 2026)

**8 Oct 2026: 0.0.60 verified (7 Oct 20:13 session, Ikebukuro); next: the battle Summon list, the Compendium View list and boss results, all silent (F6 dumps in that log, `docs/HANDOFF.md` last entry).** **7 Oct 2026: 0.0.60: the walking map joins stairs, ramps and the floors under them (Ginza underpass platforms and tracks, Ginza exit ramp) and is no longer cut in long tunnels; see `docs/HANDOFF.md`, last entry (6 Oct 21:08 session).** **6 Oct 2026, evening: 0.0.59 (built, not yet tested): Ginza stairs and Terminal doors listed, ladders up/down, Media; see `docs/HANDOFF.md`, last entry.** **6 Oct 2026: 0.0.56 (built, not yet tested): routes through other areas on F7, cleanup; see `docs/HANDOFF.md`, 6 Oct entry.** **5 Oct 2026, night: 0.0.55 (built, not yet tested): ladders in the Ginza underpass; see `docs/HANDOFF.md` 15:21 to 21:18 sessions. 0.0.54 verified.** **5 Oct 2026: 0.0.54, see `docs/HANDOFF.md` 13:35 session; the Amala Network warps (0.0.52 to 0.0.53) still untested.** **4 Oct 2026, night: 0.0.53 (built, not yet tested): Ginza door names, "Event" points (Loki), Rag's Jewelry, warp points (game objects or learned). See `docs/HANDOFF.md`, 21:31 session.** **4 Oct 2026, evening: 0.0.52 (built, not yet tested) handles the Amala Network's hidden warps (`WarpMap`); test with Francesco's save inside the Network. See `docs/HANDOFF.md`, 15:36 session.** **4 Oct 2026, later: 0.0.51 (built, not yet tested) fixes Amala auto-walk, explored doors inside an area, a fixed Page Up order, and adds filters (Shift+Page Up / Down) and Insert; see `docs/HANDOFF.md`, 14:35 session.** **4 Oct 2026: 0.0.49 verified (21:44 session); 0.0.50 built, not yet tested: the Cathedral's second table, levels, result and tabs, shop prices and counts, Fountain HP and fees. Test list and the first look at the Amala Network in `docs/HANDOFF-SHOPS-FUSION.md`.**

Read `docs/HANDOFF-SHOPS-FUSION.md` first: 0.0.49 (built, not yet tested) reads the institution menus (Fountain of Life, Junk Shop, Cathedral of Shadows), the shop lists and the fusion demon table, fixes the Underground Mall stairs and the second "Battle." after recruiting. Main open work: the Cathedral's affinities and levels from game data (images on screen), shop prices, Fountain costs. Overworld navigation is done (`docs/HANDOFF-OVERWORLD.md`).

## Next: the overworld (2 Oct 2026)

Francesco reached the overworld (Shinjuku, field 2) and starts a new chat for it: read `docs/HANDOFF-OVERWORLD.md` (facts from the 20:10 session, its two dumps and the video; the plan, one step per test run). The dungeon tools stay as they are. Small open items: a demon conversation line spoken twice, the Annex 1F ramp item box, custom waypoints later.

**Overworld chat, 2 to 3 Oct 2026: steps 1 to 3 done and verified (0.0.39 Page Up / Page Down, 0.0.41 world-map walking map and End; 0.0.40 Continue screen verified, its battle-talk fix not yet exercised). Step 4 built as 0.0.42 (auto-walk on the world map, Shibuya fix); tested 14:31: routes went through buildings and cliffs, fixed in 0.0.43 (verified 15:13). 0.0.44 (verified 15:34): "Exit to overworld", Where am I on Delete, arrival on the game's prompt. **Overworld navigation is done (Francesco).** 0.0.45 (not yet tested): exits to the overworld never "explored"; F8 battle verbosity (HP left after each hit or heal, off by default). 0.0.46 (verified 16:13): the moon phase at the end of Where am I (Delete). 0.0.47 (not yet tested): Fountain of Life menus, Shikigami double line, moon in eighths; 0.0.48 reverts its battle help-line silence (`docs/HANDOFF.md`, 16:13 session). Next: what Francesco reports while playing; overworld step 6 (explored entrances) when he asks. Then step 5, the moon phase.** Never push to GitHub unless Francesco gives the order.

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
| **0.0.40** | Battle talk pages no longer spoken twice (identical page only after `ClearMessage` / a request / window off); the title Continue screen reads its slot | Continue screen verified 3 Oct 14:04; battle-talk fix not yet exercised |
| **0.0.41** | World map step 3: a walking map for the world map (world-map walls, calibrated on the traces), Page Up filters by it, End speaks compass routes there | Verified 3 Oct 14:04 (End to two Souls and an entrance); miss: Shibuya "not reachable" from the south |
| **0.0.42** | World map step 4: auto-walk (Home) on the world map (pads fixed to the compass, arrival on the game's "Enter" / "Talk", 90 s timeout, a battle ends it); Shibuya fix: world-map entrances aim at the ground under the volume | Tested 3 Oct 14:31: Home walks, arrival, stop key and battles work; routes went through buildings and cliffs (walking map fault) |
| **0.0.43** | World map: no move through a wall (the walking map tests every move against the wall polygons); auto-walk re-plans when it makes no progress for 1.5 s | Verified 3 Oct 15:13 ("navigation worked very well for every target given"; Page Up 7 points as predicted) |
| **0.0.44** | People and boxes reached only when the game's prompt shows (Shibuya demon); dungeon doors to the world map say "Exit to overworld"; Where am I on Delete (banner, facing, nearest point); F8 Status removed | Verified 3 Oct 15:34 (Exit to overworld, Where am I); prompt arrival not yet exercised |
| **0.0.45** | Exits to the overworld never "explored"; F8 battle verbosity: HP left after each hit or heal on or off (off by default; F2, F3, F5 keep HP) | Built 3 Oct 2026, not yet tested |
| **0.0.46** | The moon phase (Kagutsuchi) at the end of Where am I (Delete), from game data: "Moon 3/8, waxing." | Verified 3 Oct 16:13 |
| **0.0.47** | Fountain of Life menu and heal list read; same dialogue page within a second not repeated (Shikigami); no bare "Battle." after a talk; moon in eighths | Partly verified 3 Oct 20:37 (page de-duplication, silent zero-enemy return, moon); its Fountain menus failed (fixed in 0.0.49) |
| **0.0.48** | Reverts the 0.0.47 battle help-line silence (help lines spoken with each command again; the drawn heading "FIGHT" / "TALK" was never spoken) | Verified 3 Oct 20:37 |
| **0.0.49** | Institution menus everywhere (Fountain fixed, Junk Shop, Cathedral), shop lists, fusion demon table; doors reached inside their volume (Underground Mall stairs); a door that moves the player says "Arrived."; no second "Battle." after a long recruiting talk | Verified 3 Oct 21:44 (Fountain, Junk Shop, Cathedral menu and first table, Mall stairs, one battle start); second fusion table silent |
| **0.0.50** | Fusion tables from the game's cursor (both tables, levels, result, icon in the legend's words); fusion screen tab (RESULT, DEMON 1, DEMON 2); help lines read live (no stale line); shop price, owned count, quantity and sum; Fountain HP and fee; real positions in long shop lists | Built 4 Oct 2026, not yet tested |
| **0.0.51** | Auto-walk no longer stops itself after a slow route search (Amala); Page Up list in a fixed map order; doors gone through inside an area marked explored (Amala); Shift+Page Up / Down filters (All, Doors, Rooms, Exits, Souls, Demons, Item boxes); Insert says the selected point | Verified 4 Oct 15:36 (walks no longer stopped by the route search, filters, explored inside an area); Amala warps still broke walks |
| **0.0.52** | Warps (`WarpMap`): routes keep clear of warps, walks go through warps when needed, unplanned warps re-planned, points beyond warps listed ("through 2 warps"), "Warp" label and filter, End to the first warp, "Warped." | Tested 4 Oct 21:31 in Ginza only: warp destinations not found (no resource tables) |
| **0.0.53** | Ginza door names (pads up to 1000); story volumes listed as "Event" with the game's prompt learned (Loki); Rag's Jewelry lists read (jewel costs, owned counts); warp points from the game's object list or learned on use | Verified 5 Oct 13:35 (names, events, fusion, shop); Rag lists silent, filter hid exits |
| **0.0.54** | Filter back to All in a new area; events faced on arrival; Amala Link destinations; Rag's Jewelry fixed; fusion grid icons in the game's words, levels fixed, RESULT / DEMON tab; shop sum and Fountain fee through fclUI.DrawPayMoney, Fountain MP; place title in Where am I | Verified 5 Oct (15:21 to 21:18 sessions): filter "All.", event facing, Amala Link, fusion grid, levels and results, shop sum, Fountain HP, MP and fee, place titles. Not verified: RESULT tab (fixed in 0.0.55), Fountain count, Rag (not visited) |
| **0.0.55** | Ladders and same-area doors in routes and the list ("via 1 ladder"), ladder landings from the partner ladder, auto-walk waits at a ladder for confirm and goes on; ladder moves seen (vertical jumps); no "Warped." after events, ladders or entering an area; RESULT tab waited for; Fountain cursor fallback; text ids never spoken in Where am I | Built 5 Oct 2026, not yet tested |
| **0.0.56** | Routes through other areas (F7, AreaRoutes): points of a split area listed "via 1 other area" from doors walked and areas visited; Home walks door by door; End to the first door. Probe toggle on Shift+F7. Cleanup: log archive (one copy at a time, appended), capped probe and save-list maps, speech queue cap, shared door patterns | Built 6 Oct 2026, not yet tested |
| **0.0.57** | Text probe off by default (config entry renamed `TextProbe`, so saved configs with `Probe = true` no longer turn it on) | Built 6 Oct 2026 |
| **0.0.58** | Ring round a warp volume (partial fix for the Ginza stairs) | Did not fix it (6 Oct sessions) |
| **0.0.59** | Door and ladder pairs, landmarks always listed ("beyond doors"), Ladders filter ("Ladder up" / "down"), "to exit" doors, Media party pick reads all names, snap stops at doors, routes through other areas off by default | Mostly verified 6 Oct 21:08 (ladders up / down, Media "All 4"); stairs and Terminal still "beyond doors" in the underpass, see 0.0.60 |
| **0.0.60** | Walking map: moves reach every floor layer within a step (stairs and ramps over hidden floors joined), points use the floor nearest their height, no window for areas under 400 x 400 cells, 4 heights per cell; no "Warped." after ladder doors to another area | Verified 7 Oct 20:13 (Ikebukuro, three bosses: 47 walks arrived, nothing "beyond doors") |

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
