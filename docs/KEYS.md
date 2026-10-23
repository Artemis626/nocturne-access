# Nocturne Access keys

## Mod keys

Keys are set in `UserData/NocturneAccess.cfg`, section `[NocturneAccess_Keys]`, using Unity `KeyCode` names such as `F8`, `Backslash` or `Alpha1`, optionally after modifiers (`Shift+F5`, `Ctrl+F5`; since 0.0.20 a plain key does not fire while Shift, Ctrl or Alt is held). Edit the file while the game is closed.

| Key | Action | Since |
| --- | --- | --- |
| V | The game's Toggle POV. Since 0.0.15 the mod makes it work on a first playthrough (in memory only) and says "First person" or "Third person". | 0.0.15 |
| F1 | Repeat whichever came last: the menu item (with position and description), the save or load file, the save Yes/No with its prompt, the config row, the name being entered, the Stats summary, or the dialogue page with its speaker (plus the highlighted option while a choice is open) | 0.0.3, menus 0.0.5, save screen 0.0.7, config 0.0.8, name entry and Stats 0.0.10 |
| F2 | In battle, in short sentences: whose turn it is and the press turns left ("Pixie's turn. 2 press turns left: 1 full and 1 half."), then each party member with HP and MP, from game data. Outside battle: "Not in battle." | 0.0.9; wording 0.0.28; enemies moved to F3 in 0.0.29 |
| F3 in battle | Each living enemy with HP ("2 enemies. Preta 2: HP 4 of 20."), plus its affinities once Analyze has shown them ("Null: Dark. Weak: Light/Magic."). Outside battle F3 is centring (below). | 0.0.29 |
| F5 | Battle log: what happened in the previous side's turn and the current one so far ("Enemy turn: Preta: Feral Claw. Pixie: 7 damage, HP 25 of 36. Player turn: ..."): actions, results, damage, healing, skill costs, weakness and critical hits, results. Kept until the next battle. | 0.0.29 |
| Shift+F2 | Debug: battle dump. Writes the battle data of this moment (turn, phase, press turns, every formindex with level, HP, MP, status, stats, skills with names, buffs, the formation and the target selection) to `UserData/NocturneAccess/BattleDumps`, with the same lines in the log as `[Battle:Dump]`. "Battle data saved." | 0.0.28 |
| F8 | Battle verbosity: turns on or off the HP left spoken after each hit or heal ("Zhen 1: 27 damage, HP 38 of 65." or "Zhen 1: 27 damage."); says "HP after actions on." or "off.", kept in the config (`SpeakHpLeft`, off by default). A unit brought to 0 says "0 HP" either way. F2, F3 and the battle log (F5) always give HP. Until 0.0.43 F8 was Status. | 0.0.45 |
| F7 | Routes through other areas on or off ("Routes through other areas on." / "off."; kept in the config, `AreaRoutes`, off by default since 0.0.59: Francesco found it missed the point; Terminal doors, exits, named doors and ladders are listed anyway, "beyond doors", when no way is known). When on, the Page Up list also has points of the area that can only be reached by going out through a door and back in through other areas, using only doors you have gone through and areas you have visited ("Door, passage, 12 metres, east, via 1 other area."). Home walks to the first door ("Arrived. Go through; the route goes on in the next area."), and after you go through each door the walk goes on by itself in the next area ("Next, door, room, 12 metres, west."), then to the point. End: "Via 1 other area. First Door, passage: ahead, 12 metres." | 0.0.56 (Francesco) |
| Shift+F7 | Debug: pause or resume the text probe (only when `TextProbe = true` in `[NocturneAccess_Debug]`; off by default since 0.0.57). F7 until 0.0.55. | 0.0.2, Shift+F7 since 0.0.56 |
| F6 | Debug: write every text currently visible on screen to the log | 0.0.2 |
| F4 | Blobber mode on or off (on at start). In first person it keeps every game key as it is and only snaps walking to squares: when W is let go, the walk carries on to the centre of the next square (a tap of W walks one square). Says what changed on reaching each square ("Opening left.", "Wall ahead.", "Door ahead.") and the facing after a turn ("North, wall ahead."). Never refuses a move. Nothing in fixed-camera rooms or third person. While falling in the Labyrinth's minigame (field 999), F4 instead switches the autopilot off and on ("Autopilot off." / "Autopilot on."; on by default, Francesco, 23 Oct 2026). | 0.0.16, version 2 in 0.0.17; minigame 0.0.112 |
| F3 | Outside battle. Blobber mode, first person: walk to the centre of the current square through the game's own moves (side step, then forward; turns round with S and back when the centre is behind), then says "Centred." with the facing and the three sides, for example "Centred. North: wall ahead, opening left, wall right.". Any movement key stops it. | 0.0.18 |
| Page Up | Next point of interest in the area (since 0.0.44 a door that leads to the world map is "Exit to overworld", never "explored" (0.0.45); door, person, item box; since 0.0.23 doors carry the game's own room name when it has one, "Healing Room door", "Terminal door", and Souls are called "Soul"), nearest first: kind, distance in metres, compass direction, position in the list ("Person, 5 metres, south. 2 of 4."). On the world map (0.0.39) entrances are named by place: "Yoyogi Park entrance, 52 metres, south-west." | 0.0.20 (F5), Page Up since 0.0.21; world map 0.0.39 |
| Page Down | Previous point of interest. Since 0.0.51 both go through the area's points in a fixed map order (north to south, west to east), so a point keeps its number while you walk (config `PoiMapOrder = false` gives nearest first again). | 0.0.20 (Shift+F5), Page Down since 0.0.21; map order 0.0.51 |
| Shift+Page Up / Shift+Page Down | Filter of the Page Up list (back to All in every new area since 0.0.54, said as "All." before the first point): All, Doors, Rooms (doors to rooms, named rooms such as Terminal and Healing Room), Exits (to another field, "Exit to overworld"), Warps (0.0.52: door volumes that move you elsewhere in the same area as soon as you walk in), Ladders (0.0.59: "Ladder up" / "Ladder down"; the far end of a ladder you can walk to is left out), Events (0.0.53: story spots such as Loki in Nyx's Lounge, with the game's prompt once you have seen it: "Event, Open Door"), Souls (and other people), Demons, Item boxes; kinds with nothing here are skipped. Says "Rooms, 3." | 0.0.51 (Francesco) |
| Insert | The selected point of interest again, from where you stand: "Door, explored, 12 metres, north-east. 3 of 7." (plus the filter when it is not All), or "Nothing selected." When Insert is the NVDA key, NVDA passes it to the game on a quick double press; the key can be changed in the config (`PoiCurrent`). | 0.0.51 (Francesco) |
| Home (Inizio on an Italian keyboard) | Walk to the selected point of interest through the game's own movement (fixed-camera rooms and third person; first person since 0.0.23, with the game's own turns; it ends facing the target): "Walking to person, 5 metres, south.", then "Arrived." (the game speaks its own prompt), "Near the person.", "Blocked." or "No path.". On the world map (0.0.42) it walks with the arrow keys fixed to the compass and ends as soon as the game offers "Enter" or "Talk" ("Arrived."); a random battle ends it. Any movement key stops it ("Auto-walk stopped."). Since 0.0.44 a person or item box counts as reached only when the game's own prompt ("Talk", "Examine") is on screen; until then the walk keeps stepping towards it. Never presses confirm: Enter (the game's confirm) interacts as usual. | 0.0.20 (Ctrl+F5), Home since 0.0.21 |
| End (Fine on an Italian keyboard) | Directions from where you stand to the selected point of interest, spoken once per press (0.0.26; 0.0.25 spoke every change while walking, too much): on the world map (0.0.41) compass words, which are the arrow keys there ("South-west, 12 metres, then south."); first person "Turn left, 12 metres, then right." / "Ahead, 12 metres, then right.", fixed-camera rooms the key direction on screen ("Up, 5 metres, then left."). In reach: "In reach." or "In reach. Turn left to face it.". Press again for new directions as you go. | 0.0.25 |
| Delete (Canc on an Italian keyboard) | Where am I (since 0.0.54 the place's name from the game's name table first, when the banner is only a floor such as "B1"): the location banner's last text (the area in dungeons, "Hachiko Intersection"; on the world map "Overworld" and the district, "Overworld, Yoyogi Kamizonocho"), the facing in first person ("Facing north"), and the nearest reachable point of interest ("Nearest: Soul, 6 metres, north-west."). Since 0.0.46 it ends with the moon phase as the HUD shows it in the Vortex World, dungeons included ("Moon 3 eighths, waxing.", "Moon half, waning.", "Moon full.", "Moon new."; in eighths since 0.0.47). F8 (Status) was removed in the same version (Francesco); F8 is battle verbosity from 0.0.45. | 0.0.44 |
| Shift+F6 | Debug: navigation dump (was F5 until 0.0.19). Writes the current area's navigation data (position, facing, place names, collision polygons, auto-map, people, item boxes, event volumes, points of interest, the walkability grid as a map, input and story-flag tables) to `UserData/NocturneAccess/NavDumps/navdump_[time].txt`, with a summary in the log. Says "Navigation data saved.". | 0.0.12, Shift+F6 since 0.0.20 |

## Reserved keys

Keys other software already uses. The mod must not bind these by default.

| Key | Used by | Source |
| --- | --- | --- |
| F9 | Nocturne Graphics Configurator: UI toggle | `NocturneGraphicsConfiguratorConfig.cfg`, `UIToggleKey` |
| F10 | Nocturne Graphics Configurator: speedhack toggle | same file, `SpeedhackToggleKey` |
| F11 | Nocturne Graphics Configurator: framerate toggle | same file, `FramerateToggleKey` |
| F12 | Steam screenshot | Steam default |
| P | NocturneModernController (NocturneModernDash): dash keep, if installed | its `README_EN.md` |
| Shift+Tab | Steam overlay | Steam default |

## First-person controls (the game's own, measured 1 Oct 2026)

In first person (V) the game reads (log 1 Oct 2026 and its code, `docs/NAVIGATION-DESIGN.md` section 12): W = pad U, forward while held; **S = pad D, turn round (180 degrees)**; A / D = pad L / R, a 90-degree turn per press; Q / E = pad L1 / R1, side steps while held; W or S together with A or D walk diagonally (45 or 135 degrees to the facing). Blobber mode must leave these unchanged.

## Game keyboard bindings

Defaults read from the game's own CONFIG > KEYBOARD+MOUSE screen by the probe (UI dump, 30 Sep 2026). The game reads them through its own layer (`InputUtil`, `InputAssign`, `SteamKeyboard`; see `docs/class-index.md`).

| Action | Key |
| --- | --- |
| Character/Cursor forward, back, left, right | W, S, A, D |
| Movement front, back, left, right | W, S, A, D |
| Confirm/Action | shown as an icon, not text (likely Enter or Space) |
| Punch | shown as an icon, not text |
| Cancel | C |
| Center Camera | C |
| Command Menu, Menu | F |
| UI On/Off | + |
| Camera up, down, left, right | I, K, J, L |
| Rotate Camera left, right; Rotate Map left, right | Q, E |
| Pass | E |
| Toggle POV, Angle | V |
| Auto Map | M |
| Skill Help On/Off | H |
| Auto Battle, Fast Forward Text, Toggle Scroll | Tab |

**No function key is used by the game.** F1 to F12 are free apart from the reserved keys above (F9 to F11 Graphics Configurator, F12 Steam), so the mod's own keys should stay in the function-key range, plus modifiers if needed. Page Up, Page Down, Home and End are not game bindings either; Francesco chose them for points of interest (1 Oct 2026) and prefers single keys to Shift or Ctrl combinations.

NocturneModernController binds only P on the keyboard (its other controls are gamepad).

On the opening splash screen, any key (F8 included) is taken as "press any key" by the game and restarts the opening music. Our mod only reads keys, never injects them.

Still to do: identify the two icon-only bindings (Confirm/Action and Punch).
