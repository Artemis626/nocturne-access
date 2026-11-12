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
  several areas share one name, and the banner shows only when the name changes: in the 11 Nov session "Hell's Maze" was heard
  entering area 20, then areas 21, 22 and 19 were silent and stayed "area 21", "area 22", "area 19".
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
