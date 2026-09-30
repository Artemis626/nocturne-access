# Nocturne Access keys

## Mod keys

Keys are set in `UserData/NocturneAccess.cfg`, section `[NocturneAccess_Keys]`, using Unity `KeyCode` names such as `F8`, `Backslash` or `Alpha1`. Edit the file while the game is closed.

| Key | Action | Since |
| --- | --- | --- |
| F8 | Status: says "Nocturne Access running. Speech through NVDA." | 0.0.1 |

## Reserved keys

Keys other software already uses. The mod must not bind these by default.

| Key | Used by | Source |
| --- | --- | --- |
| F9 | Nocturne Graphics Configurator: UI toggle | `NocturneGraphicsConfiguratorConfig.cfg`, `UIToggleKey` |
| F10 | Nocturne Graphics Configurator: speedhack toggle | same file, `SpeedhackToggleKey` |
| F11 | Nocturne Graphics Configurator: framerate toggle | same file, `FramerateToggleKey` |
| F12 | Steam screenshot | Steam default |
| Shift+Tab | Steam overlay | Steam default |

## Game keyboard bindings

Not recorded yet. The game reads input through its own layer (`InputUtil`, `InputAssign`, `SteamKeyboard`; see `docs/class-index.md`), with logical keys named after pad buttons (`InputAssign.KeyID`: A, B, X, Y, L1, R1, START, SELECT and directions).

To do in Phase 1: record the default keyboard bindings from the in-game keyboard settings screen, and the bindings of NocturneModernController (not installed on the test machine).
