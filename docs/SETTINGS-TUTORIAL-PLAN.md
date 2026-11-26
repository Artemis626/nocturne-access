# Settings menu and tutorial: plan (17 Nov 2026, decisions 18 Nov 2026)

**Spoiler warning:** this document names places, bosses and events of the game that some players may consider spoilers.

## Change of plan (25 Nov 2026): a simple window, built now

Francesco replaced the settings-menu decision below:

- **A simple visible window,** small, with generic easy-to-render text. A game-like menu inside the Config screen would serve only sighted players, who are a small part of the mod's users. The settings menu is no longer tied to the end of the playthrough; the tutorial still is.
- **Key: Shift+F** (F is the game's own menu key). It opens only when the game is active: the field, the overworld, a battle. Never inside a game menu, to avoid confusion.
- **First test with one or two options;** the full feature list once the design and behaviour are approved.
- **Built as 0.0.194** (`Systems/Menus/ModSettings.cs`): wall bump sound and door tone. IMGUI is stripped from this game's build, so the window is uGUI with the game's font. While it is open the game sees no key (postfixes on `SteamInputAssign.keycheck` and `InputUtil.GetKey`). Details and test checks: `docs/HANDOFF.md`, 25 Nov entry.
- **Next, if approved:** the player settings listed in part 1 below (true/false first, then numbers and choices, key rebinding last), each marked "now" or "after restart".

- **0.0.194 verified 25 Nov (Francesco: "clean and simple"); 0.0.195 groups 14 player settings under Speech, Battle, Navigation and Sounds.** Kept in the cfg file only, by Francesco's choice: content changes (the Raidou chase, Geis, falling-minigame damage), restart-only switches that could silence the mod, debug entries, the keys.

## Francesco's decisions (18 Nov 2026): study only, build later

Nothing below is to be built now. These decisions replace the open choices in the 17 Nov sections where they differ.

### When: after the playthrough and the public release

- Build and test these features only **after Francesco's full playthrough is finished**. At that point the mod drops its "work in progress / game in progress" label, is confirmed as mostly accessible and playable from start to finish, and is **made public** (only on his explicit word: the repository's visibility is never changed without it).
- The current priority doesn't change: the playthrough, its fixes, and key help pages for the remaining screens come first.

### Tutorial: reactive, limited to the opening areas

- **First-time tips across the whole game are rejected as a first goal.** Testing them properly would mean replaying a large part of the game, at least half of it, and Francesco wants to move on to other projects after this one.
- **Instead: a well-built reactive tutorial in the game's first two stretches:**
  1. **Tokyo before the Conception.** It has no threat or fail state, so it's the safe place to teach the field keys: Page Up / Page Down, Home, End, Delete, F1, F4 blobber mode, Shift+Page Up / Page Down filters, F7 key help.
  2. **The first areas after the Conception.** They bring light combat and interaction with low risk. They teach the battle keys (F2, F3, F5, F8), the first menus and party picks, doors and item boxes, and End before Home.
- **After that the tutorial ends.** The exceptions are a few places with mechanics of their own. Francesco named **the Labyrinth of Amala**, for example the way on (Shift+End), the falling minigame autopilot, and the cues of the later Kalpas. Each would be a single lesson triggered on arrival, chosen case by case.
- **Why this works:** testing needs only a new game played to just past the Conception, plus one visit to each exception. A fresh save file reaches every lesson.
- **What to study before building:**
  - The field and area numbers of Tokyo before the Conception and of the first post-Conception areas, read offline (door table, area files, story flags). These decide where each lesson starts and ends.
  - The scripted scenes in those areas. Lessons must wait for dialogue and never talk over a cutscene (`SpeechOrder`).
  - Which lesson a key belongs to, so that no key is taught before it can be used. For example, End and Home need points in the list, and F2 needs a battle.
  - The flag that ends the tutorial region, so the tutorial stops by itself when the player passes it.
- The event layer and the lesson steps described further down stay as planned; only the scope shrinks to these areas.
- Every lesson line is the mod's own wording and needs Francesco's approval.

### Settings menu: inside the game's own Config screen

- **The chosen anchor is option 1:** rows that look and behave exactly like the game's Config menu, opened from it. It's the best visual anchor, a large accessibility gain, and a strong portfolio piece.
- **How it opens:** through the game's own Config entry, from the field menu or the title menu. No new key; this settles the "how it opens" question.
- **Build in small steps, each tested before the next:**
  1. **One new tab in the game's Config screen with one option.** A good candidate is a true/false sound setting such as `WallBumpSound`: it takes effect at once and is easy to hear. Checks for this step:
     - the tab is visible and drawn like the game's own
     - `ConfigReader` speaks it
     - the cursor moves into it and back out
     - changing the value writes `NocturneAccess.cfg`
     - the game's own settings save is unchanged
     - it works from both the field menu and the title menu
  2. **More true/false settings,** then numbers and choices.
  3. **Key rebinding last,** since it needs key capture and conflict checks.
- **Study before step 1, offline:**
  - The game's Config screen classes: how it builds its tabs and rows, counts them, moves the cursor, changes values and saves. Read the decompile in `research/decomp` and `GameAssembly.dll`, and record every name in `docs/class-index.md`.
  - The row and tab prefabs in the asset bundles (`tools/unity/`).
  - Whether the title-menu Config and the field-menu Config are the same screen.
  - The highlight material and cursor of that screen, already known to `ConfigReader`.
- **Risk:** our rows must never reach the game's own settings save, and the game's tab or row count must not break when a tab is added. Step 1 exists to prove both.

### Long-term goals (no roadmap, no test plan)

These are recorded as wishes. They come after the settings menu and the tutorial are built and tested, not before.

- **Screen readers other than NVDA.** Today speech goes only through `nvdaControllerClient.dll` (`Speech/SpeechManager`). Possible routes:
  - a multi-reader library such as Tolk or the newer UniversalSpeech-style wrappers (JAWS, Narrator, NVDA)
  - Windows SAPI or OneCore voices as a fallback when no reader runs
  - a `SpeechBackend` config entry, which already exists as a string and could select the output

  Braille output (`BrailleOutput`) would need checking per reader. Study first which libraries are maintained, which licences fit GPL-3.0, and how each handles interrupt versus queue, which the mod relies on.
- **Audio description for certain cutscenes.** A sighted player sees things in some scenes that no dialogue says. Questions to study:
  - Which scenes matter for understanding the story or the next step.
  - How to detect a scene's start and its moments, from the event script's procedure and its commands (`FieldScript`, the field scripts read offline), or from camera and animation commands.
  - Where the description text comes from. The mod can't reconstruct the game's text, so descriptions are the mod's own writing. Each one needs Francesco's consent and is labelled as the mod's, never presented as the game's. They should come from the scene's content, not from a walkthrough's copyrighted text.
  - Timing: say a description between dialogue pages, never over voiced lines.
  - A setting to switch descriptions off, in the settings menu above.

## 17 Nov 2026 analysis

Status: **ideas only, nothing to build yet** (Francesco, 17 Nov 2026: "experimental thoughts that don't need coding right now"). This records what could be achieved and how, so a later session can start from it. Every design choice marked "Francesco" below is still his to make.

## 1. The config file as an in-game menu

### Goal

Change the mod's settings in game instead of editing `UserData/NocturneAccess.cfg`. Common in accessibility mods (P4G Access does it). Francesco's reservation: such menus often have no visual display to "anchor" the interaction; a sighted helper should see what the player hears.

### What exists

- `Core/Config.cs`: about 55 `MelonPreferences_Entry<T>` entries (bool, int, string keys), with `Config.Save<T>(entry)`.
- Most readers read `.Value` at the point of use, so a change applies at once. Some apply only at startup and must be marked "after restart": `BlobberOnAtStart`, `PadInjection` (patch install), `UnlockFirstPerson` (check each one before building).
- `ConfigReader` already speaks the game's own Config screen.
- `_dlls` has `UnityEngine.IMGUIModule.dll`, `UnityEngine.UIModule.dll`, `Unity.TextMeshPro.dll`.

### What the menu holds

Curated, not all entries:

- Player settings: sounds (`WallBumpSound`, `WayOnSound`, `DoorSound`, `AlarmSound`, `SecretSound`, `ChaseSound`), content switches (`ChaseOff`, `PipeNoDamage`; keep the README section "Content changed or skipped for accessibility" in step), `SpeakerEveryPage`, `SpeakHpChanges`, `SpeakHpLeft` (F8), `BlobberOnAtStart`.
- Key bindings: choose an action, press the new key; the mod confirms or names the action that already has it.
- Hidden (file only): `NavDiagnostics`, `NavTraceIntervalMs`, `ProbeEnabled`, `VerboseLogging`, `BlobberCellSize`, `DedupeWindowMs`, debug keys.

Each entry needs a spoken label, a short description and its "takes effect now / after restart" flag: all mod words, so Francesco approves the wording.

### Visual anchor, best to cheapest

1. **Rows added to the game's own Config screen** (a "Nocturne Access" section at its end, rows cloned from the game's row prefab). Native look, `ConfigReader` speaks it, no new key. Hardest: first read offline (decompile, `GameAssembly.dll`) how the config list counts rows, moves its cursor and saves, so our rows never reach the game's own settings save. Record every class and field in `docs/class-index.md` before use.
2. **A mod panel in Unity UI (uGUI) with the game's TMP font.** Native-like, independent of the game's menu logic. Moderate.
3. **An IMGUI box (OnGUI).** Quick and plain; still a visible anchor. Check that IMGUI works under Il2CppInterop in this game first.
4. Speech only: ruled out (Francesco's reservation).

### Constraints

- **Opening it.** F1 to F8 are taken; Francesco prefers single keys. Best: entered from the game's Config screen (option 1) or the pause menu. Alternative: from F7 key help ("press F7 again for settings"). Francesco chooses.
- **Input.** While open, arrows and confirm must not move the player or the game's cursor. Opening it only inside a game screen that already pauses avoids the problem. Opening it on the field would need the mod to withhold the game's input, against the rule that `PadInput` never hides a key: do not do this without Francesco's decision.

## 2. An in-game tutorial with live feedback

### Goal

Teach the mod's keys in play, with the mod reacting to what the player does (as Hearthstone Access and Factorio Access do).

### Why it fits

The readers already know the state: screen open (`MenuReader`, `FieldState`), exploring or battle (`BattleReader`), Pages list moves (`PointsOfInterest`), walks (`AutoWalk`), directions (`RouteGuide`), door crossings (`ExploredDoors`), wall bumps (`WallBump`), way on (`WayOn`). A tutorial only listens and speaks; it never blocks input (binding rule: cues never block).

### Building blocks

- **An event layer** (new): readers publish what just happened (`PoiMoved(direction)`, `WalkEnded(reachedTarget)`, `DoorCrossed`, `WallBumped`, `FilterChanged`, `BattleTurnStarted`, `ScreenOpened(name)` ...). Today each system keeps its own state; this is the main code work, and it also helps `KeyHelp` and later features.
- **Lessons as lists of steps.** A step has: an instruction line; a done-check (a predicate on events); a hint after a timeout or a wrong attempt ("That was Page Down, which goes back. Try Page Up."). F1 repeats the step; a step can be skipped; the whole tutorial can be switched off (a setting in part 1).
- **Speech order.** Lesson lines wait for dialogue pages and prompts through `Systems/SpeechOrder.cs`; never over a cutscene.
- **Progress** kept per player (one file under `UserData/NocturneAccess/`), not per save file, so a new game does not repeat it.

### Where it runs

No practice mode exists and custom rooms or forced battles are risky, so no Factorio-style sandbox. Two forms:

1. **First-time tips:** the first time the player meets something (first field, door, item box, battle, shop, fusion, an area with way-on data), one short lesson with a check.
2. **Guided opening:** the starting hospital (safe, linear) teaches Page Up / Down, Home, End, Delete, F4 blobber mode, Shift+Page Up / Down filters; the first real battle teaches F2, F3, F5, F8 (battles are turn based: the tutorial can wait).

Both replayable from a lessons list (part 1's panel).

### Costs and risks

- Content is the bulk: every lesson line is mod wording, each needs Francesco's consent; translation later.
- Annoyance: the off switch and skip are required.
- A sighted helper reads the lesson best in the same on-screen panel as the menu.

## Recommended order

1. The on-screen panel and the settings menu (smaller; useful for the public release).
2. The event layer.
3. First-time tips, a few lessons, tested in play.
4. The guided opening and the lessons list.

## Open decisions for Francesco

- Which anchor (rows in the game's Config screen, mod panel, plain box).
- How the menu opens (game Config screen, pause menu, F7 twice).
- Which settings appear, and their spoken labels.
- Tutorial form first (first-time tips or guided opening) and its wording.
