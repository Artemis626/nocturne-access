# The Fourth Kalpa (field 44): research and proposals (12 Nov 2026)

Francesco, 11 Nov 2026: the Fourth Kalpa disables the map, even for sighted players, to make them feel lost. Before any way on
he asked: (2) is there cleaner data than the area names ("Area 22", "Hell's Maze", which seem random), and (3) how can the mod
help once every door in the Pages list says "Explored"? Nothing here is built yet: these are findings and options for him to
choose from.

Sources: the field's bundle `f044` read offline (`research/gamedata/f044/`: `F044.WAP` door table via `tools/unity/wap.pl`,
area files `f044_AAA.f2` via `tools/unity/f2pos.pl`, script `f044.bf`), the name tables in `common_en`, the auto-map section
of the Shift+F6 dumps 11 Nov 21:34:51, 21:35:06, 21:46:13, and the session log of 11 Nov 21:04.

## 1. Where the names come from

- **"Area 22" is the mod's own fallback**, not the game's: a door says "Door to area N" (the game's area number) until the
  mod has heard the banner of the area it leads to (0.0.109, 0.0.149). Areas whose banner never shows keep their number.
- **The game has nine names for the whole Kalpa** (`AREA44_L0001` to `L0009`): 1F, B1, B2, Burial Chamber, The Road to Hell,
  Hell's Vault, Hell's Maze, Hell's Hall, Wicked King's Hall. It has 31 areas. A banner names a floor or a section, so
  several areas share one name. (Corrected 12 Nov, after A1 was built: the game shows the banner on every arrival; the mod only
  leaves a repeated banner unsaid, so "Hell's Maze" was heard once, and the doors learned it for areas 21, 22 and 19 on first
  arrival. "Area 21", "area 22", "area 19" were said only for doors into areas not yet entered.)
- Place names (`BASYO44`): Peephole, Hall of Candles, Hole, Ladder, Switch, Burial Chamber, Wicked King's Hall (pads on the
  floor, as elsewhere).

So the names are not random, but they are coarse: one name per section, and the numbers are loading chunks.

## 2. The cleaner data that exists

### The auto-map (upper part only)
The game keeps a map record per area even though it hides the map (`nfldAmArea`, in every dump): areas 0 to 10 and 27 to 29
each have a map floor and their rooms and corridors ("sub-blocks") as boxes in one coordinate frame per floor. **Areas 11 to
26, the deep sections, have no map record at all**: that is where the map is truly gone.

### Hell's Maze (areas 19 to 22) is one grid
Reading every door and landing of areas 19 to 22 on a 1800-unit grid (rooms are 1800 apart):
- The four areas are **one grid of about 5 by 6 rooms**, split into loading chunks: no room belongs to two areas. Area 20
  holds the west columns, 21 the middle, 22 the east, 19 two corridor ends outside the grid.
- **Every door inside the grid is one way, north or east** (each lands just past its wall, with no door back on that side).
  That is the trap: an "Explored" door can never be walked back through, and the way round is long.
- Area 19 is a corridor joining the grid's east edge (column 5, row 1) back to its west edge (column -1, row 5): the way to
  loop round.
- A beam at the north-east corner (area 22, column 4, row 6) sends the player to the grid's south-west corner (area 20, row 0).
- Area 21 has a hidden warp (`01d_16`, movetype 2) back to B1 (area 10).

### The other deep sections
- **Areas 14 to 18 and 30:** a section of beams (walk-in warps, movetype 10). Most beams send the player back to area 14's
  entry points (14 `01pos_02`, `01pos_03`): traps that reset the section.
- **Areas 11 to 13:** hidden warps (movetype 2) between 11 and 12, a door to 13, and one back to B1.
- **Areas 2/3, 4/5 and 23 to 26:** pairs of areas with the same layout in two states, switched by story flag 276 (the doors
  from area 0, 6 and 9 lead to one or the other). Area 24 and 25 are the same place before and after the flag.
- B1 (area 10) joins the sections through scripted moves after events (door rows with `after` procedures: `010_spec_after`,
  `010_saka_after`, `014_arus_after`, `013_ska_after`, `014_arub_after`), and every deep section has a way back to B1.

What is still unknown: the exact goal of each section and how the sections chain (the scripted moves from B1 need reading),
and whether the game still records which map rooms the player has seen while the map is off. Neither is needed for the
proposals below.

## 3. Proposals (Francesco chooses; none spoils the route unless he says so)

### A. Say places in the game's own structure, not area numbers
1. A door to an area whose banner is not known yet says "Door, passage" with no number (no more "area 22"); once a banner is
   heard in a section, every area the game shows it for takes it. Which table maps areas to the nine names is still to read
   (the per-area lookup `GetAreaName` gives `AREA44_L0020` for area 20, a key that does not exist; the banner may come from the
   map floor): until then, names are learned as heard. *Uses only the game's names.*
2. In Hell's Maze, every door is said as what it is: "One-way door, north" / "One-way door, east". *A sighted player sees a
   door and learns, once through, that it does not open back; this says it before.* Francesco may prefer it only after the
   door has been used once.
3. In Hell's Maze, Delete (Where am I) adds the room in the grid: "Hell's Maze, room 3 east, 4 north." *Stronger: a sighted
   player has no grid coordinates either, only what they remember of the rooms.* Offered as a separate switch.

### B. When every door is "Explored": tell what lies behind it (built from what the player has seen)
4. **"Leads on" marks.** An explored door's line adds what the player's own exploration shows behind it: "Explored door,
   leads to unexplored doors" (a door behind it, in rooms already reached, has not been used), "Explored door, dead end"
   (nothing new behind it), "Explored door, back to the entrance" (it landed the player on a known start, e.g. the reset beams
   of areas 14 to 18). Only doors already walked and landings already learned count, so nothing beyond the player's own
   knowledge is told. *This answers point 3 directly in every dungeon, not just the Kalpa.*
5. **A "Leads" filter** (one more step of Shift+Page Up / Down): only the unexplored doors that can be reached through doors
   already walked, anywhere in the field, nearest first, "via N doors"; Home walks the chain. It is the list a careful
   mapper keeps on paper.
6. **Loop warning.** On entering an area: "Been here 3 times" after the third visit in one session, and in Hell's Maze "Back
   in this room." *Tells the player a loop is happening, the cause of most lost time in one-way mazes.*

### C. Guidance only on request, in steps
7. A graded way on for the Kalpa (Shift+End): first press gives only a hint ("Go east", "Use the corridor round"), a second
   press within a few seconds gives the full way on. Francesco keeps the challenge and can still get out.
8. Or no way on in the Fourth Kalpa at all, with A and B only. *Keeps the game's intended confusion closest.*

### Suggested order
A1 and B4 first (small, no spoilers, useful everywhere), then B5, then A2 if Francesco wants it; A3, B6 and C last, each on
his word.

## Built

- 12 Nov 2026, 0.0.164: A1 and B4 (untested). B4 says "nothing new behind it" rather than "dead end" (a door can loop back).

## The Third Kalpa holds the key (13 Nov 2026, from Francesco's 12 Nov session and the scripts)

Francesco solved the Fourth Kalpa's maze by himself, eyes closed (12 Nov). His verdict: the Kalpa is meant to be solved without a
map, so **no way on for the Fourth Kalpa** (none is built; keep it that way unless he asks). The B2 Soul (area 27): "[game line removed] [game line removed]"

How the two Kalpas join (no door joins them; holes run the falling minigame, door-table rows of warptype 3 that run an event):
- Third Kalpa area 1, hole `01eve_02` (procedure `002_01eve_02`, event 80, row to pipe event 1006): the normal way down, landing in
  the Fourth Kalpa's area 0 (1F).
- **Third Kalpa area 28, hole `01eve_01`** ("There is a shaft leading downward", procedure `029_01eve_01`, event 84, pipe 1010): a
  second way down. Area 28 is a small room off **area 27 (B1F) through door `01d_16`** (not gated). Pipe 1010's other end is the
  Fourth Kalpa's **area 29**, whose `01eve_01` is the shaft back up ("Return to the previous Kalpa?", `030_01eve_01`, event 85).
- Fourth Kalpa area 29's door `01d_01` lands at area 27's `01pos_03` (-2124 300): **the west part of B2, beside the switch**
  `01eve_01` (-1500 100, `028_01eve_01`, "There is a switch. Will you activate it?", sets flags 2004 and 302), on the far side of
  the blocked path `01d_06` ("The path is blocked...") that the main route meets.

### Two sets of way-on data for the Third Kalpa (Francesco's question 1)
Possible with what exists: way-on candidates are already gated by story flags (`name+F`, `name-F`, `WayOn.Gated`). The first
traversal's data stays; the second needs only two lines, used while flag 2003 is on and 2004 off:
- area 27: `01d_16+2003-2004` before its present candidates (the side room, not the ladder down);
- area 28: `01eve_01+2003-2004` (the shaft).
The rest of the way back to area 27 (entrance, hub, stat route) is the same as the first traversal's. Flag 2003 is set by
`002_door` (after the scripted move into area 1 at the Kalpa's end, with 1982); to confirm in game (a log line of the way on, or a
dump) before trusting it as "first traversal done".

- 13 Nov 2026, 0.0.165: the Third Kalpa's second way-on set built (untested).

- 13 Nov 2026: 0.0.165 failed in game: flag 2003 is off after the Third Kalpa is done, so the gate `+2003` never holds; the right flag is still to find.

- 13 Nov 2026, 0.0.166: the gate is flag **1984** (`+1984-2004`), read offline: 2003 is set only by the Third Kalpa's `002_door`
  on its field call to field 44 while flag 286 is on (a path the normal route never takes), so it stays off. 1984 is set by the
  Fourth Kalpa's `001_start` (f044) the first time the player lands in its area 0, with the opening camera `01cam_04`; no script
  in f043 or f044 clears it, and field 44 has no Maniax variant. Francesco's logs: the first landing in field 44 area 0 was 11 Nov
  15:35:03 (from the falling minigame, field 999); field 44 area 29 and field 43 area 28 never visited, so 2004 is off.
