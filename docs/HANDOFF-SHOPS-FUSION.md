# Shops, Fountain and fusion handoff (Shibuya and Yoyogi Park, from 3 Oct 2026)

Written 3 Oct 2026 at the end of the overworld chat, for a new chat. Read `CLAUDE.md` first (binding rules), then this file, then `docs/NEXT-SESSION.md`. The overworld is done (`docs/HANDOFF-OVERWORLD.md`); Francesco now plays on through Shibuya and reports what is silent or wrong.

## Start here

- **Installed: 0.0.51 (built 4 Oct 2026, not yet tested).** Last verified: 0.0.49 (21:44 session, 3 Oct; below). 0.0.50's shop and fusion fixes are still untested (the 14:35 session stayed in Amala); 0.0.51 fixes Amala auto-walk, explored doors inside an area, a fixed Page Up order, and adds filters and Insert (`docs/HANDOFF.md`, 14:35 session).
- **First:** read the next test log against "0.0.50 test" below. Look for the `[Fusion]` lines (`Table N row NN: ...` lists every row object, counter and sprite; `Legend NN: ...`; `Text <COMBINE_L00NN> = ...`; `Icon ...: legend "..."`) and the `[Facility]` lines (`Shop row "...": item N, price N`, `Quantity N`, `Fountain fee N`, `Fountain row ...`). They say whether the guesses below hold.
- **Then:** whatever 0.0.50 got wrong; the fusion skill choice ("Select Skills", F "Random Skills") and the compendium when Francesco reaches them; the Amala Network (below: what was hard there is Francesco's to say).

## The 21:44 session (3 Oct 2026): the 0.0.49 test

Log `session_2026-10-03_21-44-05.log`. Worked: Fountain menu and heal list (each confirm heals one member at once and removes the row, so "Shikigami, 1 of 2" after High Pixie was right); Junk Shop menu, BUY, buying (quantity and total from the dialogue), SELL; Cathedral menu, Talk topics, the first table, a fusion read on the Stats-like result screen (affinities as text: "Drn: Ice • Weak: Fire"); the Mall stairs walked; one "Battle." after recruiting. **What the Cathedral really shows** (21:49 and 21:54, F6 dumps): the second table `comtable02_rowNN` gives the candidate (`Text_name01TM`, `Text_race01TM`) and the result (`Text_name02TM`, `Text_race02TM`) on one row; the first demon stays in it drawn TMC02 with "CANNOT FUSE"; confirming a candidate, or E (Stats) on any row, opens `statusUI` with tabs DEMON 1, DEMON 2, RESULT (Q / E), the result's skills and affinities as text, and F "Random Skills" / "Select Skills". So the affinities are not images: the game writes them on that screen, and `StatusReader` speaks them. Images left: levels (counters), the result icon and the legend (M).

**0.0.50 (built 4 Oct):** `FusionReader` (both tables from the game's cursor, `cmbDrawStockDevilInfo`; levels from the row counters; result icon in the legend's words when the legend shows the same sprite); `StatusReader` says the tab ("RESULT. Jack Frost, Fairy. Level 7 ...") and speaks each affinity group as its own sentence (no "bullet"); `MenuReader` reads help lines live (no stale "for one ally."), takes positions from the game's cursor in the shop and Fountain lists; `FacilityReader`: shop price and owned count per row, the quantity and sum while choosing how many, Fountain HP and fee per row. Names in `docs/class-index.md` ("Cathedral of Shadows, Junk Shop and Fountain of Life").

## 0.0.50 test

1. Cathedral, Fuse: "Select the 1st demon. High Pixie, Fairy, level 10, 1 of 3." Moving: "Jack Frost, Fairy, level 7, 2 of 3."
2. Confirm one: "Select the 2nd demon. High Pixie, Fairy, level 10, 1 of 3. CANNOT FUSE." on the demon already chosen; on another: "Jack Frost, Fairy, level 7, 2 of 3. Result: Aeros, Element, level N." (maybe an icon word at the end).
3. Press M (legend) once in the second table, then F6. Press E (Stats) on a row: "RESULT. Choronzon, Haunt. Level 11 ... Drn: Fire. Null: Dark. Str: Phys. Weak: Force/Light." Q / E: "DEMON 2. Datsue-Ba ...", "DEMON 1. ...". Back to the table: the row is said again.
4. Junk Shop, Buy: "BUY. Medicine, 50 Macca, have 3, 1 of 7. Fixed HP recovery for one ally." (the number is the game's). Iyomante: "Support-type Magatama." alone. On an item, confirm: after "How many do you want, ho?" you hear "1, 2000 Macca"; up: "2, 4000 Macca".
5. Sell with more than 9 kinds of item: the last says "11 of 11", not "9 of 9".
6. Fountain, Heal with someone hurt: "HEAL. Shikigami, HP 20 of 48, 30 Macca, 1 of 2."
7. Magatama screen: no "(Panic/Charm/Sleep）" left over on the next Magatama.

## The Amala Network, first look (21:44 session, field 18, dumps 22:21 and 22:23)

- Field 18 ("B3" banner, no area name). Its doors (`01d_02` to `01d_10`, door table movetype 1 or 2, target field 0 = this field) teleport the player to a marker in the same field (`01posNN`), changing `AmSBlock`; three are turned 45 degrees (`01d_04`, `01d_05`, `01d_06`), so corridors run diagonally. `01d_10` is a "Stairs door", `01eve_01` an event volume at the far end.
- What worked: Page Up listed 9 points (doors, a Soul, a Mystical Chest as "Item box"), Home walked to three doors and arrived ("the game's hit index is the target"); a battle interrupted one walk, Home resumed it. Diagonal corridors are walked as short right-angle runs (the walking grid treats the huge floor planes as floor and only the 41 wall polygons as walls: 126133 of 131200 cells walkable, which is fine as long as the walls close the corridors).
- 14:35 session (4 Oct): Francesco's reports fixed in 0.0.51 (walks stopped by the 100 ms route search, doors inside the area never "explored", list order). Still open: zig-zag in diagonal corridors (right-angle runs; 8-direction runs later), blobber cues there, whether a door says where it leads, "B3" after every teleport.

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

## Plan for the Cathedral (3 Oct, before the 21:44 session; partly superseded: the affinities are text on the result screen, see above)

- **Affinities:** `datAisyo.tbl[demon id][element]` (`datAisyo.Get(id, attr)`), the same values the battle copies to `nbParty_t.aisyo` (bit 31 weak, 0x64 normal, 0x10000 null; dump 09:16, 2 Oct 2026); element names in `datAisyoName.txt`. The game draws them in `cmpDrawStatus.cmpDrawAisyoHelp` for a stock demon (`datUnitWork_t`). Read that function offline to learn the exact encoding and the order of elements before speaking anything; the words must be the game's (`datAisyoName`), never made up. The Stats screen's text ("Str: Elec", `statusUI.aisyoText`) shows the format the game uses.
- **Levels and other numbers:** `fclCombineDraw.CCtrObjs` (named `CounterCtr`s, read their `.value` as the Stats screen does) and `fclCombineDraw.TextObjs` (named texts).
- **Which demon is shown:** the selected row's name maps to a stock demon (`dds3GlobalWork.DDS3_GBWK.unitwork` / `stocklist`); the result demon from the fusion tables (`fclCombineTable`) or from what `cmbDrawSelDevInfo` / `cmbDrawCombineResultIcon` draw. Needs the F6 dumps of the second-demon and result screens.
- Suggested speech: the list item "Shikigami, Brute, level 4" and a key (F1 repeat, or a detail key Francesco chooses) for the affinities: "Weak: Fire. Null: Elec.", in the game's words.

## Other open items

- Junk Shop prices and owned counts (images): `CampListReader` already reads item counts from `DDS3_GBWK.item` (the item list's "have 9"); prices need the shop's data (to find offline).
- Fountain costs and HP in the heal list (images): HP from the party's unit data, as F2 does in battle.
- Field 804 is the Amala device ("> There is a large cylinder. What will you do?", Save, Talk, Exit), read already (21:44 session).
- From the overworld: step 6 (explored entrances) when Francesco asks; a key to customise battle speech (help lines, HP) later (Francesco).
