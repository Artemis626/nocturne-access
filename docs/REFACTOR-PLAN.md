# Refactor plan (approved by Francesco, 2 Oct 2026)

Status: **approved, not yet implemented.** Revert point: the commit that added this file (0.0.30 verified in game).

## Rules for this work

- **No change in what the mod says or does**, except item 0 (a bug fix Francesco asked for). Every key, hook, announcement, its wording, order and timing stays as in 0.0.30.
- One item per commit, each built, so any one can be reverted alone. Bump the version once for the batch (0.0.31).
- Names only from the proxy decompile, the reference repos or logs (`docs/class-index.md`), as always.
- After the batch: record what was done in `docs/HANDOFF.md` and `docs/NEXT-SESSION.md`, mark this file done, and give Francesco the test steps below.

## 0. Bug: an HP line before the battle starts

- **Seen:** `session_2026-10-02_10-07-39.log`, first battle of the session: `[Battle] Battle UI up.` at 10:10:14.356, then 5 ms later `Target formindex 0 (Art).` and "Art, HP 48 of 48", one second before "Battle. Preta.". The second battle did not do it.
- **Cause:** `BattleReader.Tick` calls `PollTarget` from the moment the party panel appears, before `_started` (the start announcement, `StartDelayMs` = 1000). ~~A target-help call during the battle setup set `_targetCallAt`~~ Corrected while implementing: no target call came before it (no banner in the log). `_targetCallAt` started at `long.MinValue`, and `now - long.MinValue` overflows to a negative number, so the 300 ms target window counted as open until the first real target call of the session. `PollTarget` read `nbTarSelProcess` (`nowform` 0, Art) and spoke it. That is why only the session's first battle did it.
- **Fix:** `_targetCallAt` starts at `long.MinValue / 2`, as the other timers in the mod do, so it cannot overflow. As approved, `Tick` also polls targets and speaks target banners only once `_started` is true; a target seen before that is dropped. Everything after the start is unchanged.

## 1. Split `BattleReader` (about 780 lines)

`src/NocturneAccess/Systems/Battle/BattleReader.cs` holds the banner, targets, sides and press turns, units and HP, results, F2 / F3 and the turn announcement. Split into partial classes or small static classes in `Systems/Battle/`, keeping `BattleReader` as the entry point (`Install`, `Tick`, `Active`, `SpeakStatus`, `SpeakEnemies`, `TurnAnnouncement`, `UnitFigures`, used by `ModMain`, `Hotkeys`, `MenuReader` and `BattleAnalyze`):

- `BattleReader.cs`: install, the TMP classifier, start and end of a battle, `Tick` (the order of calls inside `Tick` must not change);
- banner and targets (`AfterDispText`, `OnBanner`, `SpeakPendingBanner`, `TargetHp`, `PollTarget`, `NoteAction`);
- turns (`TurnAnnouncement`, `ObserveSide`, `ObservePress`, `HalfTurn`, `PressTurns`);
- units (`Unit`, `ReadUnits`, `ReadUnit`, names and numbering, `LogData`, `UnitFigures`);
- results (`OnResult`, `Spoils`) and the status keys (`SpeakStatus`, `SpeakEnemies`).

Shared state stays in one place (the partial class's fields). Pure move: no logic edits.

## 2. One writer for the two dumps

`Capture/NavDump.cs` (Shift+F6) and `Capture/BattleDump.cs` (Shift+F2) each have their own `StringBuilder`, `Section(name, body)` (catches and logs a section's failure), file naming, folder creation, file write and summary logging. Move that into one small `Capture/DumpWriter` (start, line to file only, line to file and log summary, section, save to a folder with a prefix, speak "… data saved." / "… dump failed."). The files and log lines must come out byte for byte as now (same headers, same `[Nav:Dump]` / `[Battle:Dump]` tags, same file names: `navdump_yyyy-MM-dd_HH-mm-ss.txt`, `battledump_...`).

## 3. Prune `AnnouncementReader` entries

`Systems/Menus/AnnouncementReader.cs` `Classify` adds a new `Entry` to `Entries` for every component of a listed path, each time the game re-creates it (every save screen, every field UI rebuild); old entries stay in the list forever and `Tick` walks all of them. Keep one entry per path: when a path is classified again, reuse its entry with the new component, resetting the entry's state exactly as a new one starts (`Text`, `Spoken` empty, `Pending` false, `WasVisible` false, `WaitedForDialogue` false), so speech is the same as with a fresh entry. Check `AnyDue` and the `Reshow` logic still see the same states.

## 4. Lighter per-tick navigation upkeep

In first person `BlobberMode.BeforeCalc` calls `WalkGrid.Lattice` every field update (30 a second), which calls `WalkGrid.Ensure`: `CollisionCache.Refresh` (cheap) and `ReadHiddenNpcs` (loops the NPC table through IL2CPP calls). Francesco accepted a small timing change here: check hidden NPCs at most every 500 ms (keep the last key in between), so a newly shown or hidden NPC reaches the walking map up to half a second later. Area changes and collision changes must still rebuild at once (they do not depend on the NPC check). Points of interest, auto-walk and End must call `Ensure` as before (no throttle on their explicit calls if simpler: throttle only the hidden-NPC read).

## 5. Smaller logs

`[Nav:Trace]` writes a line every 250 ms while exploring (`Config.NavTraceIntervalMs`, section `NocturneAccess_Navigation`, entry `TraceIntervalMs`), most of a 1 MB log. Raise the default to 1000 ms. A saved config keeps its own value, so also rename the entry (for example `TraceIntervalMsV2`, as `NavigationDump` replaced `NavDump`) or document that Francesco should delete the line. Nothing spoken changes.

## Test after the batch (Francesco)

1. A battle right after loading a save: no HP line before "Battle. <enemies>."; targets, F2, F3, F5, Analyze, weakness hits and results as before.
2. Shift+F2 in battle and Shift+F6 in the field: "Battle data saved." / "Navigation data saved.", files in `BattleDumps` / `NavDumps` as before.
3. Save at the terminal and load: every prompt ("Checking data...", "Save completed.", "Load File 2?") spoken once, as before.
4. Blobber mode, F3 centring, Page Up, Home, End in the basement and on 2F: as before.
5. The session log is smaller (trace lines once a second).
