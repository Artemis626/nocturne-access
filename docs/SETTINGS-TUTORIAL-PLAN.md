# Settings menu and tutorial: plan (17 Nov 2026)

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
