# Sanity check of the mod (plan, 21 Nov 2026)

Francesco's request (21 Nov 2026, after the Labyrinth of Amala was finished with 0.0.181): "a general sanity check of the mod, since
we're nearing completion", to run in the session after a compact. Installed then: 0.0.182. This is a review, not a feature pass:
find what is wrong, stale or risky, report it with headings, fix only what is clearly a bug, and ask Francesco about anything that
changes what he hears. Commit and push each working fix.

## 1. Build and start-up
- `dotnet build NocturneAccess.sln -c Release`: 0 warnings, 0 errors.
- The last log's `[Patch] Summary` ("N patched, 0 not declared, 0 failed"); every hook still installed and used.
- `bash tools/log-summary.sh` on the newest session: no errors, exceptions or warnings from the mod.
- The version is the same in `Core/ModMain.cs` and `NocturneAccess.csproj`.

## 2. Probes and log volume before a public release
- Still on: `NavProbe` (`[Nav:Trace]`, `[Nav:Change]`), `[Battle:Data]`, `GameFlagWatch`. Decide with Francesco which stay
  (they made most fixes possible) and which go behind a config switch for players.
- Off but kept in the code: `Capture/FadeWatch.cs` (off since 0.0.182), `ChaseProbe`, `PipeProbe`, the text probe (config). Delete
  or keep each, and say so in `docs/HANDOFF.md`.
- Log size per hour of play; the log archive folder's growth.

## 3. Code health
- Dead code: methods, config entries, data lines no longer read; `TODO`-like comments; duplicated helpers (the 0.0.67 rule: one
  copy of each helper).
- Every `activeunit` use goes through `BattleReader.ActiveForm` (0.0.150, 0.0.180: the cost lookup was missed for weeks).
- Every per-frame reader catches its errors and caps its log lines; nothing allocates per frame needlessly (0.0.113).
- Every class, method and field name used is in `docs/class-index.md`.

## 4. Data files
- `Data/WayOn.txt`: every volume name exists in that field's door table or area file (`tools/unity/wap.pl`, `f2pos.pl`); flag gates
  match the game's flags; each field has its README row with sources.
- `Data/AreaPoints.txt`: no duplicate lines; field 21 added in 0.0.181.
- `Data/ScriptNames.txt`: still only field 34.

## 5. Player-facing behaviour
- `README.md`: keys, features, "Content changed or skipped for accessibility" (chase, falling minigame, Geis gift), game modes,
  way-on table; nothing claims what the mod no longer does.
- `docs/KEYS.md` against `Input/Hotkeys.cs` and F7's key help (`KeyHelp`).
- `UserData/NocturneAccess.cfg`: every entry's description true, every default the intended one.
- Mod words (not the game's) listed for Francesco's review: "Way on", "No effect on X.", "Mitama bonuses not counted", the Geis
  line, "Insufficient macca", "Block", "Missed", "Dead", status words, ...

## 6. Open items to put to Francesco (not to build without his choice)
- The black screen: caused by taking doors faster than the game's transition (Francesco, 21 Nov 2026). A possible fix: Home and
  the way on wait for the game's fade to clear before a chained walk goes on (`dds3KernelDraw.dds3DrawFlag` & 3, as `FadeWatch`
  read it).
- Key help pages for more screens (memory `key-help-f7`), the settings tab and tutorial (`docs/SETTINGS-TUTORIAL-PLAN.md`),
  other screen readers: after the playthrough.
- The Steam achievement tied to the Puzzle Boy (does the Geis gift fire it?).
- `CLAUDE.md` length: its "Current state" holds every resume line since October; propose moving the history to
  `docs/HANDOFF.md` and keeping only the latest state and the binding rules.

## Save backups
Before any change that writes to the game's save state (as 0.0.181's Geis gift does), run `bash tools/backup-saves.sh <label>`
with the game closed: it copies `%APPDATA%/Sega/smt3hd` (`SMT3HDDATA.sav` holds every save file and the suspend data) and the
mod's `Explored` files into `save-backups/<date>_<label>/` (git-ignored). First backup: `save-backups/2026-11-21_22-05_before-0.0.181`.
