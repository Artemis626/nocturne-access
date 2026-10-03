# Shops, Fountain and fusion handoff (Shibuya and Yoyogi Park, from 3 Oct 2026)

Written 3 Oct 2026 at the end of the overworld chat, for a new chat. Read `CLAUDE.md` first (binding rules), then this file, then `docs/NEXT-SESSION.md`. The overworld is done (`docs/HANDOFF-OVERWORLD.md`); Francesco now plays on through Shibuya and reports what is silent or wrong.

## Start here

- **Installed: 0.0.49 (built 3 Oct 2026, not yet tested).** Last verified: 0.0.48 and earlier (`docs/HANDOFF.md`).
- **First:** read the next test log against "0.0.49 test" below.
- **Then the main open work, in Francesco's order of importance:** the Cathedral of Shadows (fusion) beyond the first list, above all the affinities, which are images ("crucial for understanding fusion"); then prices and counts in the Junk Shop, costs in the Fountain.
- Evidence: log `UserData/NocturneAccess/Logs/session_2026-10-03_20-37-10.log` (Shibuya: Underground Mall, Cathedral of Shadows field 800, Fountain of Life field 801, Junk Shop field 802, field 804 not dumped); F6 dumps in that log at 21:14:59 (Fountain), 21:19:52, 21:20:37, 21:20:50 (Cathedral), 21:21:43, 21:21:50, 21:21:56 (Junk Shop); navigation dumps `NavDumps/navdump_2026-10-03_20-49-51.txt`, `_21-04-21`, `_21-12-05` (Underground Mall, field 17 area 1). Earlier Fountain (Yoyogi Park): `session_2026-10-03_16-13-07.log`, dumps 16:22:07 and 16:22:20.

## The 20:37 session: Francesco's four reports and what was found

1. **Fountain of Life in Shibuya not read (and in Yoyogi Park only "Heal").** 0.0.47 added its menus, but my edit lost the backslashes of the patterns (`basedd`, `rcv_row(dd)` instead of `\d\d`): only `insmenu_top` ("Heal") matched, so the menu opened as "Heal, 1 of 1" and never followed the cursor; the heal list never matched. Same screen in both parks. **Fixed in 0.0.49**, and every new pattern was tested against the real paths of the log. Lesson: never write regex patterns through perl one-liners; use the Edit tool and test the patterns.
2. **Junk Shop and Cathedral of Shadows not read.** Both are the game's "institution" layout (like the Terminal): `Canvas_UI/<screen>/institutionUI/institution_menu/insmenu_window/insmenu_top|baseNN/TextTM`, highlighted `TMC01`. **0.0.49:** one general "Institution menu" spec for every screen (Terminal, Fountain, Junk Shop, Cathedral and any later one), "Shop list" (BUY / SELL title, item names, the help text) and "Fusion list" (the Cathedral's demon table: name and race, title "Select the 1st demon"). Not read yet: shop prices and counts, Fountain costs, Cathedral levels and affinities (all images), and the Cathedral screens after the first list (never reached in the log).
3. **Underground Mall stairs not listed for the way out.** They were listed but "not reachable, left out": door `01d_01` (field 17 area 1, at -550 0) has a 600-wide volume centred on the stairs' central divider; the nearest walkable cells (65 clear of the divider) lay 105 from its centre, outside the 95 that counted. **0.0.49:** a door counts as reached when a walkable cell lies inside its volume's footprint (`Poi.NearVolume`, `WalkGrid.ReachableInRect`; doors behind walls stay unreachable, since cells keep 65 from walls). Also "Walk interrupted by an event" on entering the Mall: those stairs move the player as soon as their hit fires; now "Arrived." when the target door's hit is active.
4. **"Battle" after recruiting.** It was "Battle. Kodama." at 21:24:22: after Kodama joined, the battle screen came back for the one enemy left after a talk longer than the 20 s resume window, and was announced as a new battle. **0.0.49:** the same battle while the game's field (217 in Shibuya, 202 on the world map) has not changed since it began. The 0.0.47 fixes held: four repeated pages suppressed ("Same page again after 567 ms"), and the zero-enemy returns were silent.

Also verified in this session: arrival on the game's "Talk" prompt (0.0.44: "hit at 252 units, no prompt yet: stepping closer", then "Arrived." and "Talk"); recruiting through Seduce, Talk and Beseech; the moon in eighths.

## 0.0.49 test

1. Fountain of Life (Shibuya or Yoyogi Park): "Heal, 1 of 5", moving says "Revive, 2 of 5" and so on; Heal: "HEAL. Art, 1 of 4" (the names to heal).
2. Junk Shop: "Buy, 1 of 4" ...; Buy: "BUY. Medicine, 1 of 7. Fixed HP recovery for one ally."; Sell the same with "SELL".
3. Cathedral of Shadows: "Fuse, 1 of 3"; Talk topics "Fusion Basics, 1 of 6" ...; Fuse: "Select the 1st demon. Shikigami, Brute, 2 of 3". Then go on to the second demon and the result and press F6 on each screen (the next chat needs them).
4. Underground Mall: Page Up lists the stairs door (no longer "not reachable"); Home walks to it and says "Arrived.".
5. Recruit a demon while others remain in the battle: no "Battle. <name>." after the talk.

## Plan for the Cathedral (fusion), from the game's data (read offline first)

- **Affinities:** `datAisyo.tbl[demon id][element]` (`datAisyo.Get(id, attr)`), the same values the battle copies to `nbParty_t.aisyo` (bit 31 weak, 0x64 normal, 0x10000 null; dump 09:16, 2 Oct 2026); element names in `datAisyoName.txt`. The game draws them in `cmpDrawStatus.cmpDrawAisyoHelp` for a stock demon (`datUnitWork_t`). Read that function offline to learn the exact encoding and the order of elements before speaking anything; the words must be the game's (`datAisyoName`), never made up. The Stats screen's text ("Str: Elec", `statusUI.aisyoText`) shows the format the game uses.
- **Levels and other numbers:** `fclCombineDraw.CCtrObjs` (named `CounterCtr`s, read their `.value` as the Stats screen does) and `fclCombineDraw.TextObjs` (named texts).
- **Which demon is shown:** the selected row's name maps to a stock demon (`dds3GlobalWork.DDS3_GBWK.unitwork` / `stocklist`); the result demon from the fusion tables (`fclCombineTable`) or from what `cmbDrawSelDevInfo` / `cmbDrawCombineResultIcon` draw. Needs the F6 dumps of the second-demon and result screens.
- Suggested speech: the list item "Shikigami, Brute, level 4" and a key (F1 repeat, or a detail key Francesco chooses) for the affinities: "Weak: Fire. Null: Elec.", in the game's words.

## Other open items

- Junk Shop prices and owned counts (images): `CampListReader` already reads item counts from `DDS3_GBWK.item` (the item list's "have 9"); prices need the shop's data (to find offline).
- Fountain costs and HP in the heal list (images): HP from the party's unit data, as F2 does in battle.
- Field 804 (visited twice in Shibuya, not dumped): unknown screen; press F6 there.
- From the overworld: step 6 (explored entrances) when Francesco asks; a key to customise battle speech (help lines, HP) later (Francesco).
