# Nocturne Access keys

## Mod keys

Keys are set in `UserData/NocturneAccess.cfg`, section `[NocturneAccess_Keys]`, using Unity `KeyCode` names such as `F8`, `Backslash` or `Alpha1`, optionally after modifiers (`Shift+F5`, `Ctrl+F5`; since 0.0.20 a plain key does not fire while Shift, Ctrl or Alt is held). Edit the file while the game is closed.

| Key | Action | Since |
| --- | --- | --- |
| V | The game's Toggle POV. Since 0.0.15 the mod makes it work on a first playthrough (in memory only) and says "First person" or "Third person". | 0.0.15 |
| F1 | Repeat whichever came last: the menu item (with position and description), the save or load file, the save Yes/No with its prompt, the config row, the name being entered, the Stats summary, or the dialogue page with its speaker (plus the highlighted option while a choice is open) | 0.0.3, menus 0.0.5, save screen 0.0.7, config 0.0.8, name entry and Stats 0.0.10 |
| F2 | In battle: Press Turns (full and blinking), then each enemy with HP, then each party member with HP and MP, from game data. Outside battle: "Not in battle." | 0.0.9 |
| F8 | Status: says "Nocturne Access running. Speech through NVDA." | 0.0.1 |
| F7 | Debug: pause or resume the text probe (only when `Probe = true` in `[NocturneAccess_Debug]`) | 0.0.2 |
| F6 | Debug: write every text currently visible on screen to the log | 0.0.2 |
| F4 | Blobber mode on or off (on at start). In first person it keeps every game key as it is and only snaps walking to squares: when W is let go, the walk carries on to the centre of the next square (a tap of W walks one square). Says what changed on reaching each square ("Opening left.", "Wall ahead.", "Door ahead.") and the facing after a turn ("North, wall ahead."). Never refuses a move. Nothing in fixed-camera rooms or third person. | 0.0.16, version 2 in 0.0.17 |
| F3 | Blobber mode, first person: walk to the centre of the current square through the game's own moves (side step, then forward; turns round with S and back when the centre is behind), then says "Centred." with the facing and the three sides, for example "Centred. North: wall ahead, opening left, wall right.". Any movement key stops it. | 0.0.18 |
| Page Up | Next point of interest in the area (door, person, item box), nearest first: kind, distance in metres, compass direction, position in the list ("Person, 5 metres, south. 2 of 4."). | 0.0.20 (F5), Page Up since 0.0.21 |
| Page Down | Previous point of interest. | 0.0.20 (Shift+F5), Page Down since 0.0.21 |
| Home (Inizio on an Italian keyboard) | Walk to the selected point of interest through the game's own movement (fixed-camera rooms and third person for now): "Walking to person, 5 metres, south.", then "Arrived." (the game speaks its own prompt), "Near the person.", "Blocked." or "No path.". Any movement key stops it ("Auto-walk stopped."). Never presses confirm: Enter (the game's confirm) interacts as usual. | 0.0.20 (Ctrl+F5), Home since 0.0.21 |
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

**No function key is used by the game.** F1 to F12 are free apart from the reserved keys above (F9 to F11 Graphics Configurator, F12 Steam), so the mod's own keys should stay in the function-key range, plus modifiers if needed. Page Up, Page Down and Home are not game bindings either; Francesco chose them for points of interest (1 Oct 2026) and prefers single keys to Shift or Ctrl combinations.

NocturneModernController binds only P on the keyboard (its other controls are gamepad).

On the opening splash screen, any key (F8 included) is taken as "press any key" by the game and restarts the opening music. Our mod only reads keys, never injects them.

Still to do: identify the two icon-only bindings (Confirm/Action and Punch).
