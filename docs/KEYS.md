# Nocturne Access keys

**Spoiler warning:** this document names places, bosses and events of the game that some players may consider spoilers.

## Mod keys

**Remapping since 0.0.213:** every player key can be changed in game: Shift+F, category Keys (see README "Settings" and `docs/USER-GUIDE.md` chapter 9). Debug keys stay in the cfg file.

**Layout since 0.0.204** (Francesco's option C, 27 Nov 2026, `docs/KEYS-PLAN.md`): the keys are grouped by job. The table further down keeps each feature's history and still names the keys it had before 0.0.204; read it with this list.

- **General:** F1 key help; F2 repeat; Shift+F the mod settings window.
- **Battle:** F3 party status (outside battle, since 0.0.204: each party member with HP, MP and statuses); F4 enemies ("Not in battle." outside); F5 battle log.
- **Navigation:** F6 centre on the square (first person); Page Up and Page Down points; Shift+Page Up and Shift+Page Down filters; Home walk; End directions; Shift+End way on; Delete where am I.
- **Free:** F7 and F8 (kept for later status keys).
- **Moved to the settings window (no key by default):** blobber mode (Navigation, "Blobber mode", switched at once and kept for the next start), the falling minigame's autopilot (Navigation, "Falling minigame autopilot", read at the start of each fall), HP after actions (Battle). Their cfg entries `Blobber` and `BattleVerbosity` can still be given a key; a bound Blobber key also switches the autopilot during a fall.
- **Debug, on Ctrl:** Ctrl+F3 battle dump; Ctrl+F6 UI dump; Ctrl+Shift+F6 navigation dump; Ctrl+F7 text probe.
- **Moving an old file:** the first start of 0.0.204 resets every key entry in `NocturneAccess.cfg` to the new defaults once (entry `KeyLayout` = 2) and logs "[Config] Keys moved to the 0.0.204 layout".

Keys are set in `UserData/NocturneAccess.cfg`, section `[NocturneAccess_Keys]`, using Unity `KeyCode` names such as `F8`, `Backslash` or `Alpha1`, optionally after modifiers (`Shift+F5`, `Ctrl+F5`; since 0.0.20 a plain key does not fire while Shift, Ctrl or Alt is held). Edit the file while the game is closed.

| Key | Action | Since |
| --- | --- | --- |
| V | The game's Toggle POV. Since 0.0.15 the mod makes it work on a first playthrough (in memory only) and says "First person" or "Third person". | 0.0.15 |
| Shift+F | The mod settings window (0.0.194; categories since 0.0.195: Speech, Battle, Navigation, Sounds, opened with Enter, C goes back, Francesco, 25 Nov 2026; F is the game's menu key, and the game does not see it while Shift is held). Opens only in the field, on the overworld and in battle. A small window on screen, every row spoken: W / S or the arrows move, A / D, the arrows, Enter or Space change, C, Escape or Shift+F close, F1 repeats the row, F7 says these keys. While it is open the game sees no key. First test: two settings (wall bump sound, door tone). Config `ModSettings` in `[NocturneAccess_Keys]`. | 0.0.194 |
| F1 | Repeat whichever came last: the menu item (with position and description), the save or load file, the save Yes/No with its prompt, the config row, the name being entered, the Stats summary, or the dialogue page with its speaker (plus the highlighted option while a choice is open) | 0.0.3, menus 0.0.5, save screen 0.0.7, config 0.0.8, name entry and Stats 0.0.10 |
| F2 | In battle, in short sentences: whose turn it is and the press turns left ("Pixie's turn. 2 press turns left: 1 full and 1 half."), then each party member with HP and MP, from game data. Outside battle: "Not in battle." | 0.0.9; wording 0.0.28; enemies moved to F3 in 0.0.29 |
| F3 in battle | Each living enemy with HP ("2 enemies. Preta 2: HP 4 of 20."), plus its affinities once Analyze has shown them ("Null: Dark. Weak: Light/Magic."). Outside battle F3 is centring (below). | 0.0.29 |
| F5 | Battle log: what happened in the previous side's turn and the current one so far ("Enemy turn: Preta: Feral Claw. Pixie: 7 damage, HP 25 of 36. Player turn: ..."): actions, results, damage, healing, skill costs, weakness and critical hits, results. Kept until the next battle. | 0.0.29 |
| Shift+F2 | Debug: battle dump. Writes the battle data of this moment (turn, phase, press turns, every formindex with level, HP, MP, status, stats, skills with names, buffs, the formation and the target selection) to `UserData/NocturneAccess/BattleDumps`, with the same lines in the log as `[Battle:Dump]`. "Battle data saved." | 0.0.28 |
| F8 | Battle verbosity: turns on or off the HP left spoken after each hit or heal ("Zhen 1: 27 damage, HP 38 of 65." or "Zhen 1: 27 damage."); says "HP after actions on." or "off.", kept in the config (`SpeakHpLeft`, off by default). A unit brought to 0 says "0 HP" either way. F2, F3 and the battle log (F5) always give HP. Until 0.0.43 F8 was Status. | 0.0.45 |
| F7 | Key help: the keys of the current screen. First the game's own button guide where the screen has one (Demon Compendium: "Game keys: W, S, A, D: Select / Page. Q, E: Toggle Sort. C: Cancel. Confirm."; a key drawn only as a picture says just its action), then the mod's keys for a menu, the field or a battle, named as bound. Until 0.0.171 F7 switched routes through other areas; that switch now has no key (`AreaRoutesToggle`, default None; the routes themselves stay `AreaRoutes` in the Navigation section, off). | 0.0.172 (Francesco chose F7, 17 Nov 2026) |
| Shift+F7 | Debug: pause or resume the text probe (only when `TextProbe = true` in `[NocturneAccess_Debug]`; off by default since 0.0.57). F7 until 0.0.55. | 0.0.2, Shift+F7 since 0.0.56 |
| F6 | Debug: write every text currently visible on screen to the log | 0.0.2 |
| F4 | Blobber mode on or off (on at start). In first person it keeps every game key as it is and only snaps walking to squares: when W is let go, the walk carries on to the centre of the next square (a tap of W walks one square). Says what changed on reaching each square ("Opening left.", "Wall ahead.", "Door ahead.") and the facing after a turn ("North, wall ahead."). Never refuses a move. Nothing in fixed-camera rooms or third person. While falling in the Labyrinth's minigame (field 999), F4 instead switches the autopilot off and on ("Autopilot off." / "Autopilot on."; on by default, Francesco, 23 Oct 2026). | 0.0.16, version 2 in 0.0.17; minigame 0.0.112 |
| F3 | Outside battle. Blobber mode, first person: walk to the centre of the current square through the game's own moves (side step, then forward; turns round with S and back when the centre is behind), then says "Centred." with the facing and the three sides, for example "Centred. North: wall ahead, opening left, wall right.". Any movement key stops it. | 0.0.18 |
| Page Up | Next point of interest in the area (on the world map since 0.0.187 "Entrance to Asakusa" and the filter "Entrances", Francesco; since 0.0.44 a door that leads to the world map is "Exit to overworld", never "explored" (0.0.45); door, person, item box; since 0.0.23 doors carry the game's own room name when it has one, "Healing Room door", "Terminal door", and Souls are called "Soul"), nearest first: kind, distance in metres, compass direction, position in the list ("Person, 5 metres, south. 2 of 4."). On the world map (0.0.39) entrances are named by place: "Yoyogi Park entrance, 52 metres, south-west." | 0.0.20 (F5), Page Up since 0.0.21; world map 0.0.39 |
| Page Down | Previous point of interest. Since 0.0.51 both go through the area's points in a fixed map order (north to south, west to east), so a point keeps its number while you walk (config `PoiMapOrder = false` gives nearest first again). | 0.0.20 (Shift+F5), Page Down since 0.0.21; map order 0.0.51 |
| Shift+Page Up / Shift+Page Down | Filter of the Page Up list (back to All in every new area since 0.0.54, said as "All." before the first point): All, Doors, Rooms (doors to rooms, named rooms such as Terminal and Healing Room), Exits (to another field, "Exit to overworld"), Warps (0.0.52: door volumes that move you elsewhere in the same area as soon as you walk in), Ladders (0.0.59: "Ladder up" / "Ladder down"; the far end of a ladder you can walk to is left out), Events (0.0.53: story spots such as Loki in Nyx's Lounge, with the game's prompt once you have seen it: "Event, Open Door"), Souls (and other people), Demons, Item boxes; kinds with nothing here are skipped. Says "Rooms, 3." | 0.0.51 (Francesco) |
| Home (Inizio on an Italian keyboard) | Walk to the selected point of interest through the game's own movement (fixed-camera rooms and third person; first person since 0.0.23, with the game's own turns; it ends facing the target): "Walking to person, 5 metres, south.", then "Arrived." (the game speaks its own prompt), "Near the person.", "Blocked." or "No path.". On the world map (0.0.42) it walks with the arrow keys fixed to the compass and ends as soon as the game offers "Enter" or "Talk" ("Arrived."); a random battle ends it. Any movement key stops it ("Auto-walk stopped."). Since 0.0.44 a person or item box counts as reached only when the game's own prompt ("Talk", "Examine") is on screen; until then the walk keeps stepping towards it. Never presses confirm: Enter (the game's confirm) interacts as usual. | 0.0.20 (Ctrl+F5), Home since 0.0.21 |
| End (Fine on an Italian keyboard) | The selected point named first, then directions from where you stand (0.0.117: always named, "Door. Ahead, 12 metres."; Insert, which said the selection again, removed on Francesco's choice, 26 Oct 2026, and is free) to the selected point of interest, spoken once per press (0.0.26; 0.0.25 spoke every change while walking, too much): on the world map (0.0.41) compass words, which are the arrow keys there ("South-west, 12 metres, then south."); first person "Turn left, 12 metres, then right." / "Ahead, 12 metres, then right.", fixed-camera rooms the key direction on screen ("Up, 5 metres, then left."). In reach: "In reach." or "In reach. Turn left to face it.". Press again for new directions as you go. | 0.0.25 |
| Shift+End | The way on (0.0.118, Francesco's design, 26 Oct 2026): "Way on." first (so a mispress is heard at once), then the area's onward point is selected and End's line follows (name, directions); Home then walks there. From walkthroughs matched to the game's data (`Data/WayOn.txt`); "Way on. None known for this area." where there is none (the places with data are listed in the README, "Where the way on has data"). The Pages list is unchanged, except that the point chosen stays listed (across rooms) until you leave the area. Config `WayOn`. | 0.0.118 |
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
