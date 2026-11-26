# Nocturne Access: player's guide

**Spoiler warning:** this document names places, bosses and events of the game that some players may consider spoilers.

Draft, started 26 Nov 2026. Written with AI help, to be reviewed by Francesco before the public release. Only the settings chapter is written so far; the other chapters list what they will cover.

## About this guide

- **For whom:** players who use the mod, mostly blind and with NVDA. Developers read `README.md`, `docs/KEYS.md` and `docs/TECHNICAL-OVERVIEW.md` instead.
- **How it is written:** one chapter per thing you do in the game, in the order you meet it. Short sections under headings, so you can move with NVDA's heading keys. No tables. Each key is named with what you hear when you press it.
- **What it is not:** a walkthrough of the game. It says how the mod helps, not where to go next.

## 1. Installing

To write: what to download (the game, MelonLoader 0.6.1, the .NET 6 Desktop Runtime, the NVDA controller client), where each file goes, the launch option that hides the MelonLoader console, and how to check it works: "Nocturne Access loaded" at start. Recommended mode: Chronicle. Taken from the README's Requirements, written as steps.

## 2. First steps

To write: the keys you need from the very start: F1 to repeat, F7 for the keys of the screen you are on, Delete for where you are. Then the title screen, a new game, name entry and the first save, with what you hear on each.

## 3. Hearing the game

To write: dialogue and speaker names, choices, prompts and the location banner, menus and their positions, and how the mod waits so the question comes before the options. When to press F1.

## 4. Exploring

To write, in this order:

- **The Pages list:** Page Up and Page Down, what a point says (kind, name, distance, direction, "explored", "empty"), the fixed map order, and the filters with Shift+Page Up and Shift+Page Down.
- **Getting there:** Home walks for you; End gives the directions so you walk yourself; how a walk stops and what "Walk interrupted by an event" means.
- **Blobber mode:** F4, first person with the game's own controls, grid steps, F3 to centre, the wall and opening cues.
- **Sounds you will hear:** wall bump, door knock, way-on tone and the others, with how to switch each off (chapter 9).
- **The overworld map:** entrances, and why some are listed only once you can walk there.
- **Doors you have been through:** what "explored" and "leads to unexplored doors" mean.

## 5. The way on

To write: Shift+End, where it has data (the hardest dungeons only), what it says, how Home then walks there, why it is a last resort, and how to switch it off (chapter 9).

## 6. Battles

To write: the battle start, whose turn it is and the Press Turns, choosing skills and targets, what is said after each action, F2 (your party), F3 (the enemies), F5 (the battle log), F8 (HP left after actions), summoning, Analyze, results.

## 7. Menus, shops and fusion

To write: the pause menu, items and skills, Stats, the Demon Compendium, the Junk Shop, the Fountain of Life, the Terminal and Amala Link, Rag's Jewelry, and the Cathedral of Shadows (both fusion tables, skill inheritance).

## 8. Special places

To write, short sections: the Obelisk's time puzzles ("Next step"), the Labyrinth of Amala (the falling minigame autopilot, the stat and demon doors, the cursed areas, the Fourth Kalpa without a map), the Amala Temple, the Diet Building statue room, the Tower of Kagutsuchi's Reason doors.

## 9. Settings

Press **Shift+F** in the field, on the overworld or in battle. A small window appears in the middle of the screen and every row is spoken. It does not open inside a game menu: there Shift+F says "Mod settings open only in the field or in battle."

While the window is open the game does not react to any key, so nothing you press there moves you or opens a menu.

### Moving in the window

The window has two levels.

- **The categories:** Speech, Battle, Navigation and Sounds. W and S, or the up and down arrows, move. Enter, Space, A, D or the left and right arrows open one. C, Escape or Backspace close the window.
- **The settings of a category:** W and S move. A, D, Enter, Space or the arrows switch the setting on or off. C, Escape or Backspace go back to the categories.
- **Anywhere:** Shift+F closes the window, F1 repeats the row you are on, F7 says these keys.

The list does not wrap round: at the first or last row, the same row is said again. Each category remembers the row you left it on.

### What you hear

- On opening: "Mod settings. W and S: move. Enter: open. C: back. Speech, 2 settings, 1 of 4."
- Opening a category: its name, then the first row: "Sounds. Wall bump sound, On, 1 of 6."
- Changing a setting: "Wall bump sound, Off."
- Closing: "Mod settings closed."

Every change is saved at once and takes effect straight away. The one exception says so when you change it: blobber mode at start applies the next time you start the game (F4 switches blobber mode now).

### The settings

- **Speech:**
  - Speaker on every page: the speaker's name before every dialogue page; off, only when the speaker changes.
  - Braille output: every spoken line also goes to your braille display.
- **Battle:**
  - Speak HP changes: the HP of a unit after each hit or heal.
  - Speak HP left after actions: also the HP left ("27 damage, HP 38 of 65"). F8 switches it too.
- **Navigation:**
  - Blobber mode at start.
  - Door kinds: whether a door leads to a room, a passage or an exit.
  - Points of interest in map order: off, nearest first (the order then changes as you move).
  - Routes through other areas: the Pages list also lists points reached only through doors you have been through.
  - Way on: off, Shift+End only says "Way on is off", and the way-on tone is not played. For players who prefer to find their own way.
- **Sounds:**
  - Wall bump sound, door tone, way-on tone, alarm tones (the Amala Temple), secret area and section exit tones (the Labyrinth), chase pulse (the Labyrinth's chase, when it is on).

### What is not in the window

Some settings are changed only in `UserData/NocturneAccess.cfg`, with the game closed:

- The content changes for accessibility, such as the Third Kalpa chase, the Geis gift and the falling minigame's damage (chapter 10).
- Switches that could leave the mod silent or need a restart, such as reading dialogue, menus or battles.
- Debug settings, and the keys.

## 10. Content changed for accessibility

To write: the README's list of changes, each with what it changes, why, and how to switch it back to the game's original.

## 11. When something goes wrong

To write: where the log is, what to send when reporting a problem, the black screen after fast door changes, what to do if speech stops, and the debug dumps (Shift+F6, Shift+F2) a developer may ask for.

## Still to decide

- Wording of the chapters, and whether the guide is one file or one file per chapter.
- Whether it also goes on the website as a page.
- Translation, later.
