# Nocturne Access keys

## Mod keys

Keys are set in `UserData/NocturneAccess.cfg`, section `[NocturneAccess_Keys]`, using Unity `KeyCode` names such as `F8`, `Backslash` or `Alpha1`. Edit the file while the game is closed.

| Key | Action | Since |
| --- | --- | --- |
| F8 | Status: says "Nocturne Access running. Speech through NVDA." | 0.0.1 |
| F7 | Debug: pause or resume the text probe (only when `Probe = true` in `[NocturneAccess_Debug]`) | 0.0.2 |
| F6 | Debug: write every text currently visible on screen to the log | 0.0.2 |

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

Not recorded yet. The game reads input through its own layer (`InputUtil`, `InputAssign`, `SteamKeyboard`; see `docs/class-index.md`), with logical keys named after pad buttons (`InputAssign.KeyID`: A, B, X, Y, L1, R1, START, SELECT and directions).

NocturneModernController binds only P on the keyboard (its other controls are gamepad).

On the opening splash screen, any key (F8 included) is taken as "press any key" by the game and restarts the opening music. Our mod only reads keys, never injects them.

Still to do: record the game's default keyboard bindings from its keyboard settings screen (the Phase 1 probe will log that screen's text).
