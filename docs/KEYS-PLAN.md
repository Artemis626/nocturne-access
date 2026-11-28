# Key layout and in-game remapping: plan (27 Nov 2026)

Study only (Francesco, 27 Nov 2026): nothing is built yet. Both items come before the zipped release. Every choice marked "Francesco" is his.

## 1. Today's keys, and why they feel random

Player keys on the function row (`Input/Hotkeys.cs`):

- F1 repeat (everywhere)
- F2 party status (battle)
- F3 shared: enemies in battle, centre on the square in the field
- F4 shared: blobber mode in the field, autopilot while falling in the Labyrinth minigame
- F5 battle log
- F7 key help (everywhere)
- F8 HP left after actions (battle)
- F6 and the Shift combinations are debug keys: Shift+F2 battle dump, F6 UI dump, Shift+F6 navigation dump, Shift+F7 probe.

Outside the function row: Page Up and Page Down (points), Shift+Page Up and Shift+Page Down (filters), Home (walk), End (directions), Shift+End (way on), Delete (where am I), Shift+F (settings). Insert is free.

Each key was added when its feature was built (F2 in 0.0.9, F4 in 0.0.15, F5 the battle log, F7 the key help in 0.0.172). So battle keys sit at F2, F3, F5 and F8, navigation keys at F3 and F4, and the general keys at F1 and F7.

**Limits:** F9 to F11 belong to Nocturne Graphics Configurator and F12 to Steam (`docs/KEYS.md`), so the player keys must fit in **F1 to F8**. Eight player keys need a place: repeat, key help, party status, enemies, battle log, HP after actions, blobber mode, centre. That is exactly eight, so every key can have one job and no sharing is needed.

## 2. Layout options

### Option A: Francesco's example, battle first

- F1 party status, F2 enemies, F3 battle log, F4 HP after actions (battle)
- F5 blobber mode (and the falling minigame's autopilot), F6 centre on the square (navigation)
- F7 repeat, F8 key help (general)

Clear blocks, but the most used key, repeat, moves far from the left edge.

### Option B: general keys first (recommended)

- F1 key help, F2 repeat (general: they work on every screen, so they come first; F1 for help is a common convention)
- F3 party status, F4 enemies, F5 battle log, F6 HP after actions (battle)
- F7 blobber mode (and the falling minigame's autopilot), F8 centre on the square (navigation)

Three blocks in the order a new player needs them: help, then battle, then the first-person extras. The navigation keys that matter most are not on the function row at all (Page Up, Page Down, Home, End, Delete), so the function row's navigation block is small in both options.

### Option C (Francesco, 27 Nov 2026): Option B with toggles moved into the settings window

Francesco's proposal: switches that are set once and left (blobber mode, the falling minigame's autopilot, HP after actions) become settings-window rows only, and lose their function keys. The function row keeps what a menu does badly: hearing things again, and status at a glance.

- **Keys that go:** F4 blobber mode and the autopilot (new settings rows: "Blobber mode" switched live, replacing "Blobber mode at start", and "Falling minigame autopilot", on by default); F8 HP after actions (already a row). Routes through other areas already has no key.
- **What remains:** key help, repeat, party status, enemies, battle log, centre on the square: six keys, so two are left free.
- **Suggested layout:** F1 key help, F2 repeat, F3 party status (in battle as now; in the field the party's HP, MP and statuses, new), F4 enemies (battle), F5 battle log, F6 centre on the square; F7 and F8 kept free for later status keys (to choose: for example the moon and macca, now on Delete).
- **Points to settle:**
  - The autopilot cannot be switched mid-fall unless the window opens during the minigame (today it opens only in the field and in battle). With the row set beforehand, and a real direction key still taking over at once, a fall-time key may not be needed.
  - Switching blobber mode now takes four presses instead of one. Fine if it is set once; worth Francesco's check if he switches it often (fixed-camera rooms, the overworld).
  - The spoken lines that name F4 change to name the setting ("Blobber mode: set it in the mod settings").
- **Effect on remapping:** fewer keys to list in the Keys category, and two free keys for players who want to remap.

### Option D (was C): one key per question, shared by context

The same key gives "status" in battle and "where am I" in the field, and so on. It uses fewer keys, but a key's meaning depends on the screen. F3 and F4 are shared today and that is part of what feels random. Not recommended.

### Debug keys in any option

They leave the player's row: debug dumps and the probe move to **Ctrl+** combinations (Ctrl+F2 battle dump, Ctrl+F6 UI dump, Ctrl+Shift+F6 navigation dump, Ctrl+F7 probe, or similar). Combinations stay acceptable for debug keys (Francesco's rule), and a player pressing a plain F key never triggers a dump by accident. Debug keys are left out of the key help except as one line at the end.

## 3. Moving to the new layout

- **Saved configs keep old values.** MelonPreferences writes every default into `NocturneAccess.cfg`, so a changed default does not reach a file that already exists. The fix used before is a new entry name ("TraceIntervalMsV2"). For a whole layout, a single marker is cleaner: a `KeyLayout` entry. When it is below 2, every key entry is reset to the new default once, and it is set to 2. A player who later sets their own keys is never reset again. Only Francesco's file has old values today, since there is no release yet.
- **Key names written into spoken lines** must come from the live bindings (`Hotkeys.KeyText`), not literals, or they go wrong after a change or a remap. Lines found on 27 Nov: `ModSettings` ("F4 switches blobber mode now", its own F1 and F7 hints), `BlobberMode` ("F4 toggles it"), `PipePilot` ("F4 for autoplay"), `WayOn` ("Shift+F"), `AutoWalk` and `RouteGuide` ("with Page Up"). Inside the settings window, F1 (repeat) and F7 (keys) are hard-coded and should follow the bindings too.
- **Documents to update in the same change:** `docs/KEYS.md`, README "Keys" and "Settings", `docs/USER-GUIDE.md`, `KeyHelp` wording.
- **Test:** every key once in the field, in battle and while falling; F7 on each to hear the new names; a remapped key heard in the lines above.

## 4. Remapping keys in the settings window

### What the player gets

A fifth category, **Keys**, listing each player action with its key: "Repeat, F2, 1 of 16." The list holds the eight function keys, the points of interest keys, Home, End, the way on, where am I, and the settings key itself.

- **Enter on a row:** "Press the new key for Repeat. Escape cancels." The next key pressed, with Shift, Ctrl or Alt if held, becomes the binding: "Repeat, F9." The change is saved at once and works at once.
- **A key already used by another action:** "F3 is used by Party status. Enter swaps them, Escape cancels." Swapping keeps every action reachable.
- **A key the game or other software uses** (W, A, S, D, Q, E, C, F, V, M, H, I, J, K, L, Tab, Enter, Space, Escape, the plus key; F9 to F12; Shift+Tab): refused, "W is a game key." Arrows are checked against the game's bindings before they are allowed.
- **A last row, "Reset all keys":** asks for Enter to confirm, then restores the layout's defaults.

### How it works inside

- `Hotkeys` builds its bindings once, in `Init`, from the config. A remap writes the config entry and calls a new `Hotkeys.Rebuild`; the key help already reads the live bindings, so it follows.
- **Capture mode:** while the window waits for a key, its own keys (W, S, Enter, C) are off and the first key down from an allowed list is taken (every `KeyCode` checked each frame is wasteful; a list of function keys, the navigation block, digits and letters not used by the game is enough). Modifiers are read at the moment of the press, as `Hotkeys.Update` does now. The game already sees no keys while the window is open, so nothing leaks.
- **The window's own keys are not remappable** (W, S, A, D, Enter, C, Escape): they mirror the game's menus and must always work.
- **Locking yourself out:** if the settings key itself is remapped, the new key must be pressed twice to confirm, and the cfg file keeps `ModSettings` as a way back. "Reset all keys" restores Shift+F.
- **Debug keys** stay in the cfg file only, like the other debug settings.
- **Wording** (prompts, refusals, the swap question) is the mod's: Francesco approves it.

### Not covered

Gamepad buttons: the mod's keys are keyboard only. A gamepad layout would be a separate study.

## Built

- **0.0.204 (27 Nov 2026):** option C (section 2) with the one-time reset and the spoken key names from the bindings (section 3). Remapping (section 4) is next.
- **0.0.205 (27 Nov 2026), verified 27 Nov:** the reset moved after the last `SetFilePath`; Francesco's cfg now has the new keys. Section 4 (remapping) waits for his go-ahead.
- **0.0.213 (28 Nov 2026), built, untested:** section 4 as planned: the Keys category (19 rows: 18 player keys and "Reset all keys"), capture with modifiers, refusals (game keys except Shift+F for the settings row, window keys, F9 to F12 and P, debug keys), swap on Enter, the settings key pressed twice, reset with Enter twice; `Hotkeys.Init` rebuilds the bindings at once; the category scrolls 10 rows. Mod words to confirm.

## 5. Order of work

1. Francesco chooses the layout (A, B or his own).
2. Build the new layout with the `KeyLayout` reset and the spoken key names taken from the bindings; test.
3. Build the Keys category on top of it; test remapping, swapping, refusals and reset.
4. Update `docs/KEYS.md`, the README and the user guide.
