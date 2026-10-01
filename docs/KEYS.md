# Nocturne Access keys

## Mod keys

Keys are set in `UserData/NocturneAccess.cfg`, section `[NocturneAccess_Keys]`, using Unity `KeyCode` names such as `F8`, `Backslash` or `Alpha1`. Edit the file while the game is closed.

| Key | Action | Since |
| --- | --- | --- |
| F1 | Repeat whichever came last: the menu item (with position and description), the save or load file, the save Yes/No with its prompt, the config row, the name being entered, the Stats summary, or the dialogue page with its speaker (plus the highlighted option while a choice is open) | 0.0.3, menus 0.0.5, save screen 0.0.7, config 0.0.8, name entry and Stats 0.0.10 |
| F2 | In battle: Press Turns (full and blinking), then each enemy with HP, then each party member with HP and MP, from game data. Outside battle: "Not in battle." | 0.0.9 |
| F8 | Status: says "Nocturne Access running. Speech through NVDA." | 0.0.1 |
| F7 | Debug: pause or resume the text probe (only when `Probe = true` in `[NocturneAccess_Debug]`) | 0.0.2 |
| F6 | Debug: write every text currently visible on screen to the log | 0.0.2 |
| F3 | Debug, temporary (0.0.13): step test. Since 0.0.14 it replays the stick input learned while you walked forward with W, for half a second, then says "Step test: moved N." or "did not move." (before any W walk: "walk forward with W first") Will become a navigation key later. | 0.0.13 |
| F4 | Debug, temporary (0.0.13): turn test. Presses the game's turn-right action (`FD_Turn_Right`) until the player or camera turned 90 degrees (at most 1.5 s), then says both angles. Will become a navigation key later. | 0.0.13 |
| F5 | Debug: navigation dump. Writes the current area's navigation data (position, facing, place names, collision polygons, auto-map, people, item boxes, event volumes, input and story-flag tables) to `UserData/NocturneAccess/NavDumps/navdump_[time].txt`, with a summary in the log. Says "Navigation data saved." May move to a modifier combination when navigation needs more keys. | 0.0.12 |

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

**No function key is used by the game.** F1 to F12 are free apart from the reserved keys above (F9 to F11 Graphics Configurator, F12 Steam), so the mod's own keys should stay in the function-key range, plus modifiers if needed.

NocturneModernController binds only P on the keyboard (its other controls are gamepad).

On the opening splash screen, any key (F8 included) is taken as "press any key" by the game and restarts the opening music. Our mod only reads keys, never injects them.

Still to do: identify the two icon-only bindings (Confirm/Action and Punch).
