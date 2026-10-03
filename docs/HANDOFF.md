# Nocturne HD Access — Claude Code Handoff

Sep 30, 2026 · @Francesco

## Purpose and goals

The goal is a free, open-source screen-reader mod that makes Shin Megami Tensei III Nocturne HD Remaster (Steam, app 1413480) fully playable by blind players, speaking through NVDA via nvdaControllerClient64.dll. Feasibility is high: the Steam build is Unity IL2CPP, and a mature MelonLoader + Harmony modding scene already patches its game logic despite Denuvo.

Working name: **Nocturne HD Access** (repo slug suggestion: `nocturne-access`). Final shape: one MelonLoader mod DLL in `Mods/`, the NVDA controller client beside the game exe, a README with key bindings, released on GitHub.

What "done" means, in priority order:

1. Every menu, dialogue line, choice and prompt is spoken without the player asking.
2. Battles (Press Turn system) are fully playable: turn icons, targets, skills, affinities, results.
3. Menu-heavy systems are playable: demon negotiation, Cathedral of Shadows fusion, shops, Magatama, Compendium.
4. Field exploration is navigable: position, walls, exits, NPCs, chests, and eventually the puzzle dungeons.

Design rule carried over from Francesco's other projects: keyboard-first. On-demand keys per information category, not continuous narration. Pointer or mouse virtualisation only after keyboard options are exhausted.

Rules Francesco set on 30 Sep 2026, binding for every phase:

- **Never reconstruct text.** Dialogue, menus and popups are always read from the game's real text source (the string the game hands to its text component, or the game data behind it). No OCR, no character-by-character reconstruction from glyphs or sprites. Francesco can share the Markdown guidelines from his Suikoden 1&2 HD accessibility mod, which follow this rule on Unity.
- **Pathfinding comes right after text.** Once dialogue and menus let Francesco move between game sections, navigation is priority #1, especially the overhead map sections that use a 2D space with a cursor. The roadmap below reflects this.
- Francesco is a low-vision NVDA user: replies use proper Markdown headings.

## Verified technical facts

Everything below was confirmed from public repos and guides on 30 Sep 2026. Items marked *to verify* must be checked on Francesco's machine in Phase 0.

| Fact | Status | Source |
| --- | --- | --- |
| Steam build is Unity IL2CPP (`GameAssembly.dll` + `global-metadata.dat`) | Confirmed | [NocturneAddNewSkills](https://github.com/tanatyucom/NocturneAddNewSkills) |
| Community standard loader is MelonLoader 0.6.x (0.6.1 Open-Beta) | Confirmed | [NocturneSkillEvolution](https://github.com/tanatyucom/NocturneSkillEvolution), [Nexus bundle](https://www.nexusmods.com/shinmegamitensei3nocturnehdremaster/mods/3) |
| Mods build against net6 MelonLoader + Il2CppInterop; old Mono toolchains fail | Confirmed | [NocturneAddNewSkills](https://github.com/tanatyucom/NocturneAddNewSkills) |
| MelonLoader needs .NET Windows Desktop Runtime 6.0.x (6.0.36 cited) | Confirmed | [Steam guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3622285991) |
| Game folder is `smt3hd`; generated proxies land in `smt3hd\MelonLoader\Il2CppAssemblies\` | Confirmed | [MatthiewNocturne README](https://github.com/NocturneMods/MatthiewNocturne) |
| UI text uses TextMeshPro (`Unity.TextMeshPro.dll` is a build reference) | Confirmed | [MatthiewNocturne README](https://github.com/NocturneMods/MatthiewNocturne) |
| Harmony patches on game methods work; MelonLoader auto-applies `[HarmonyPatch]` classes | Confirmed | [NocturneModernController PR #7](https://github.com/tanatyucom/NocturneModernController/pull/7) |
| Field player logic lives in `fldPlayer` / `fldPlayerCalc`, patchable with a Prefix | Confirmed | [NocturneModernController PR #7](https://github.com/tanatyucom/NocturneModernController/pull/7) |
| Community mods already read battle state (buff levels, Press Turn icons) | Confirmed | [MatthiewPurple](https://github.com/MatthiewPurple) |
| Remaster runs the original PS2 game logic inside a Unity wrapper | Community claim, consistent with PS2-style class names | [Steam discussion](https://steamcommunity.com/app/1413480/discussions/0/3108027565746511976/?ctp=2) |
| Denuvo still present on the Steam build | Not inspected, by design: the project never opens `smt3hd.exe`. The Phase 0 gate checks the game still launches normally with our mod | [Steam discussion](https://steamcommunity.com/app/1413480/discussions/0/3454843419539261602) |
| Unity version is 2019.4.10f1; game version 1.0.4 | Confirmed 30 Sep 2026 | `Latest.log` |
| MelonLoader 0.6.1 Open-Beta runs; Il2CppInterop 1.4.5; Cpp2IL 2022.1.0-pre-release.10; runtime net6 | Confirmed 30 Sep 2026 | `Latest.log` |
| MelonLoader 0.7.x | Not tested and not planned: 0.6.1 is the community standard | — |
| .NET 6 Desktop Runtime 6.0.36 and SDKs 6.0.428 / 7.0.306 installed on the dev machine | Confirmed 30 Sep 2026 | `dotnet --list-runtimes` |
| Input: the game's own layer (`InputUtil`, `InputAssign`, `SteamKeyboard`, `SteamPad`) over Steamworks.NET Steam Input. No Rewired, no Unity Input System. `UnityEngine.Input.GetKeyDown(KeyCode)` exists in the build, so mod hotkeys use it | Confirmed 30 Sep 2026 | Proxy dump, see `docs/class-index.md` |
| NVDA controller DLL in the game folder is `nvdaControllerClient.dll`, x64, NVDA 2024.1+ API | Confirmed 30 Sep 2026 | PE header and export table |

**Why the PS2-heritage point matters.** Class names like `fldPlayer` look like a direct port of the original C code, not idiomatic Unity. Expect game state in big data structs and static managers rather than MonoBehaviours with readable fields. That is good news: state is centralised and readable once found, and it maps to decades of PS2 Nocturne hacking knowledge (demon IDs, skill IDs, affinity tables).

**The blind developer.** No public trace was found of the earlier, unreleased blind developer's project. Treat it as proof of feasibility only. If Francesco can reach them through community channels, their notes on class names would save weeks.

## Corrections to starting assumptions

Three of the initial assumptions need adjusting; none of them block the project.

**1. Denuvo does not cover the code we need.** In an IL2CPP build, all C# game logic is compiled into `GameAssembly.dll`, and every class, method and field name sits in `global-metadata.dat`. Denuvo wraps the launcher exe, not those two files. The NocturneAddNewSkills author reports doing static analysis of both files directly. So the plan never touches `smt3hd.exe`. What Denuvo may still affect: attaching a native debugger (avoid it) and possibly timing checks. Harmony patching is proven safe by the existing mods.

**2. dnSpy alone will not show method bodies.** The `Il2CppAssemblies` MelonLoader generates are interop proxies: type names, fields and signatures are real, but the bodies are stubs that call native code. They are excellent for finding *what exists* and *what to patch*, and useless for reading *what a method does*. For logic, use Cpp2IL's analysis output or Ghidra with Il2CppDumper's script on `GameAssembly.dll`. In practice, runtime observation (log what a method receives and returns) will answer most questions faster than disassembly.

**3. MelonLoader, not BepInEx, for this game.** Both can load IL2CPP mods, and Francesco's Suikoden project uses BepInEx 6. Here, every public Nocturne mod targets MelonLoader 0.6.x, and players will want to run the accessibility mod alongside those QoL mods. Two loaders in one game is a support burden. MelonLoader also bundles Harmony, so the stack is MelonLoader + HarmonyX + Il2CppInterop, one DLL.

**Kept as stated:** the modding scene proves Denuvo is not a blocker, and an earlier blind developer's partial success proves the screen-reader goal is reachable.

**Corrections found in Phase 0 (30 Sep 2026):**

4. **The NVDA DLL is `nvdaControllerClient.dll`, not `nvdaControllerClient64.dll`.** NVDA 2024.1 and later ship the 64-bit client under that name in the x64 folder of the controller package. The NVDA backend tries both names. Tolk still looks for the old name, one more reason to call NVDA directly.
5. **`fldPlayerCalc` is a method, not a class.** `fldPlayer` is a class. The owning class of `fldPlayerCalc` (and its variants `_Hit`, `_Nml`, `_FastStart`, `ForUnity`, `Hasigo`) is to be confirmed in Phase 1.
6. **HD Ears is not a MelonLoader mod.** `Mods/` holds only `Nocturne Framerate Mod.dll` (Nocturne Graphics Configurator 1.74) before ours; HD Ears must replace audio files directly. Compatibility with it is therefore automatic.

## Reference projects

Read these before writing code. The Nocturne repos give real class names; the screen-reader repos give a proven architecture on the same loader.

| Project | Why it matters | License |
| --- | --- | --- |
| [NocturneModernController](https://github.com/tanatyucom/NocturneModernController) | Field/exploration patches, input binding API, investigation sources; built with Claude Opus 5.5, so conventions will feel familiar | Check repo |
| [NocturneAddNewSkills](https://github.com/tanatyucom/NocturneAddNewSkills) | `docs/investigation-log.md` records native analysis of `GameAssembly.dll` / metadata; skill and demon data access | Check repo |
| [NocturneSkillEvolution](https://github.com/tanatyucom/NocturneSkillEvolution) | Same author; settings-file and install-layout conventions for Nocturne mods | Check repo |
| [MatthiewNocturne](https://github.com/NocturneMods/MatthiewNocturne) | \~35 small mods in one solution: battle, Press Turns, buffs, Analyze, Compendium, shops. Best map of battle-side classes | MIT |
| [MatthiewPurple / display-buffs-06](https://github.com/MatthiewPurple) | Reads buff/debuff levels of a selected target: exactly the data a blind player needs on demand | Check repo |
| [bladestorm360 FF1–FF5 Screen Readers](https://github.com/bladestorm360/FF4-Screen-Reader) | MelonLoader IL2CPP + Tolk/NVDA; entity scanner, pathfinding, waypoints, battle status keys | Check repo |
| [BlindGuyNW / FF6ScreenReader](https://github.com/BlindGuyNW/FF6ScreenReader) | Original of the FF PR family; category cycling (chests, NPCs, exits) with path announcements | Check repo |
| [Lirin111 / the-drifter-accessibility-mod](https://github.com/Lirin111/the-drifter-accessibility-mod) | MelonLoader mod calling NVDA's controller API directly, no Tolk | Check repo |
| [zersiax / DuelLinksAccess](https://github.com/zersiax/DuelLinksAccess/releases) | Bundles Tolk + NVDA client with license notices; test suite + CI; good release template | Check repo |

Licenses marked *check repo* were not confirmed. Code may only be copied from MIT or compatible licenses, with attribution; everything else is for reading and learning patterns.

The [blind-accessible games list](https://github.com/Molitvan/blind-accessible-games-list/) is where the finished mod should be announced.

## Proposed architecture

One MelonLoader mod with three layers: a speech layer, a text-capture layer that works everywhere, and semantic hooks that replace it screen by screen.

**Stack.** C# targeting net6.0, MelonLoader 0.6.1 (match the community), HarmonyX (bundled), Il2CppInterop. Build references copied from the local install into a gitignored `_dlls/` folder, following the MatthiewNocturne convention. Game-generated assemblies are never committed or redistributed.

**Speech layer.** A small `ISpeechBackend` interface with `Speak(text, interrupt)`, `Stop()` and `IsAvailable`.

- Primary backend: direct calls into the NVDA controller client (`nvdaController_testIfRunning`, `nvdaController_speakText`, `nvdaController_cancelSpeech`, `nvdaController_brailleMessage`). The DLL is loaded by full path with `NativeLibrary.TryLoad`, trying `nvdaControllerClient.dll` then `nvdaControllerClient64.dll`, in the game folder then `UserLibs`. Direct calls avoid Tolk initialisation issues, as the Drifter mod notes.
- Optional second backend: Tolk, for JAWS and other readers. Swappable, not required. Built but untested.
- Fallback when no reader is running: log only, so testing never crashes.
- A speech manager on top: deduplicates identical lines within a short window (`DedupeWindowMs`, default 250; on-demand keys pass `force`), supports interrupt vs queue, and mirrors every utterance to the MelonLoader log with a `[SPEECH]` prefix. That log is how Claude Code sees what a player would hear.
- All screen reader calls, including backend selection, run on one background thread (STA) fed by a queue. A slow or frozen screen reader can never stall the game's frame loop.
- Settings live in `UserData/NocturneAccess.cfg`, sections `[NocturneAccess_Speech]`, `[NocturneAccess_Keys]`, `[NocturneAccess_Debug]`. The path is absolute on purpose: a bare file name makes MelonLoader's file watcher throw, as the Graphics Configurator's config shows in `Latest.log`.

**Capture layer (discovery and fallback).** A Harmony Postfix on TextMeshPro's text setters records every string the UI displays, with the GameObject hierarchy path and the calling component type. In probe mode it logs everything; in play mode it speaks only whitelisted paths. This finds the right classes fast and covers screens that have no semantic hook yet.

**Semantic layer (the real mod).** Per-system handlers that patch game classes directly: message window, menu cursor movement, battle command and target selection, negotiation, fusion. They read game state and build clean sentences instead of scraping labels. Each handler lives in its own file and can be disabled in config.

**On-demand keys.** A key-binding table read from `UserData/NocturneAccess.cfg` (MelonPreferences). Examples: current HP/MP of party, Press Turn icons left, target affinities, current area, macca. Check first whether the game reads input through Unity's legacy `Input`, the new Input System or Rewired, and avoid keys the game already uses.

**Research tooling, gitignored.** `ilspycmd` dumps of the proxy `Assembly-CSharp.dll` into `research/decomp/`, plus a `docs/class-index.md` Claude Code maintains as it learns what each class does. Optional: Cpp2IL or Il2CppDumper output for method logic.

```
Nocturne Access/                (repo root; GitHub slug suggestion: nocturne-access)
  NocturneAccess.sln
  Directory.Build.props         shared paths; imports NocturneAccess.local.props
  NocturneAccess.local.props    (gitignored) GamePath; builds auto-deploy to GamePath\Mods
  src/NocturneAccess/
    Core/        ModMain.cs, Config.cs, Log.cs, Patcher.cs, UiUtil.cs, LogArchive.cs
    Speech/      ISpeechBackend.cs, NvdaBackend.cs, TolkBackend.cs, LogOnlyBackend.cs, SpeechManager.cs
    Input/       Hotkeys.cs    (namespace NocturneAccess.Input: use the UnityInput alias for Unity's Input)
    Capture/     Probes.cs, TmpTextProbe.cs, MesProbe.cs, UiTreeDumper.cs   (Phase 1 research tools)
    Systems/     Dialogue/, Menus/, Field/, Battle/, ...     (Phase 2 onwards)
  tools/         copy-references.ps1 (fills _dlls/ from a local install), log-summary.sh (log overview)
  research/refs/ (gitignored) clones of the MIT reference repos
  _dlls/         (gitignored game + loader references)
  research/      (gitignored decomp dumps, type lists, logs)
  docs/          HANDOFF.md, class-index.md, KEYS.md
  LICENSE  README.md  THIRD_PARTY_NOTICES.md
```

## Game systems and difficulty

Most of Nocturne is menus and text, which is the tractable part. The hard part is 3D field exploration and a handful of puzzle dungeons.

| System | What a blind player needs | Approach | Difficulty |
| --- | --- | --- | --- |
| Title, options, save/load, suspend | Item names, values, slot info | Menu cursor hooks; capture layer as fallback | Low |
| Dialogue and cutscene text | Speaker + line, spoken as it appears; repeat key | Message window hook | Low |
| Yes/no and multi-choice prompts | Options and current selection | Choice window cursor hook | Low |
| Main menu (Skills, Items, Stock, Status, Magatama, Map, Analyze) | Lists, stats, skill descriptions, costs | Per-screen handlers reading data, not labels | Medium |
| Battle: commands and targeting | Whose turn, Press Turn icons left, enemies by name and position, target's affinities once known | Battle command/target hooks; state readers from Matthiew's mods | Medium |
| Battle: results | Damage, weak/resist/null/repel/drain, crits, misses, kills, status | Hook battle messages or damage events | Medium |
| Buffs/debuffs | Current levels per unit, on demand | Port the display-buffs data path | Low once battle is mapped |
| Demon negotiation | Demon lines, answer choices, requests (items, macca, HP) | Dialogue + choice hooks | Low–Medium |
| Cathedral of Shadows fusion | Pair lists, result preview, skill inheritance, Kagutsuchi effects | Fusion menu handlers; rich data per entry | Medium |
| Shops, Fountains, Rag's jewels | Item, price, owned count; heal costs | Menu handlers | Low |
| Compendium | Registered demons, level, cost | Menu handler | Low |
| Kagutsuchi moon phase | Current phase, on demand | Static state reader | Low |
| Field: orientation | Area name, facing, position, nearby walls | `fldPlayer`/`fldPlayerCalc` + map data; audio cues later | High |
| Field: points of interest | Exits, NPCs, chests, save points, doors, with direction and distance | Entity scanner, FF6 Screen Reader pattern | High |
| Field: navigation | Path to a chosen target | Pathfinding over walkable data, or step-guidance | Very high |
| Automap | Explored layout as text | Read automap data | High |
| Puzzles (Amala Labyrinth dark zones, Mifunashiro, Puzzle Boy, Obelisk) | Case by case | Later phase; Puzzle Boy maps are already documented as Tiled data by the community | Very high |

Random encounters in the field and the moon phase affect every exploration session, so the field phase must speak encounter starts clearly and never drop battle-transition text.

## Phased roadmap

Each phase ends at a gate Francesco verifies in game. No phase starts until the previous gate passes.

1. **Phase 0 — Environment and hello world.** Install MelonLoader 0.6.1 and .NET 6 Desktop Runtime; first launch generates proxies. Record Unity version, loader version and input system in `docs/HANDOFF.md`. Scaffold the solution, copy references to `_dlls/`, build a mod that says "Nocturne Access loaded" through NVDA.
   - Gate: the line is spoken by NVDA and appears in `Latest.log`; the game still runs with Denuvo intact.
2. **Phase 1 — Reconnaissance.** Dump proxy assemblies with ilspycmd. Read the reference repos for class names. Build the TMP capture probe and a UI-tree dumper bound to a debug key. Start `docs/class-index.md`.
   - Gate: a probe session from title screen to first save produces a log of every displayed string with its path and source class.
3. **Phase 2 — Text and menus.** Dialogue, choice prompts, title/options/save menus, main pause menu screens.
   - Gate: new game through the prologue to the first save terminal in Shinjuku Medical Center is playable by ear, with no visual help needed for any text.
4. **Phase 3 — Navigation and pathfinding.** Moved up from Phase 5 at Francesco's request (30 Sep 2026). Overhead map sections with a 2D cursor first, then the 3D field: area and facing announcements, entity scanner by category, direction and distance, then pathfinding. Audio cues for walls if speech alone is not enough.
   - Gate: Shinjuku Medical Center completed without sighted help, plus travel between known locations on the overhead map.
   - Dependency: random encounters happen during this gate. Until Phase 4, battles rely on Francesco's residual vision and the capture layer.
5. **Phase 4 — Battle.** Commands, targeting, Press Turn count, results, affinities, buffs, status on demand.
   - Gate: Francesco wins several random battles and the Forneus boss without looking.
6. **Phase 5 — Demon systems.** Negotiation, fusion, Compendium, shops, Fountains, Magatama.
   - Gate: recruit, fuse and heal entirely by ear.
7. **Phase 6 — Puzzles and polish.** Dungeon-specific helpers, localisation of mod strings, config UI, docs.
8. **Phase 7 — Release.** Packaging, notices, README with keys, GitHub release, announcement.

**Order change (Francesco, 30 Sep 2026, after the 0.0.7 test): once the remaining Phase 2 screens (config, Magatama, Stats and similar) work, combat (Phase 4) comes next, then navigation and pathfinding (Phase 3).**

The field gate is deliberately small: Shinjuku Medical Center is the first dungeon, so it tests the whole field stack on simple geometry before the Amala puzzles.

## Licensing and release

Recommendation: **GPL-3.0**. It matches the stated goal: anyone may fork and modify, and every published fork must stay open source. This is general information, not legal advice.

| Option | Effect | When to prefer it |
| --- | --- | --- |
| GPL-3.0 | Forks and redistributions must stay GPL and share source | Default: protects the mod from closed, paid re-releases |
| MIT | Anyone can reuse the code, including in closed projects | If folding code into MIT community modpacks (e.g. Matthiew's) matters more |
| MPL-2.0 | File-level copyleft; changed files stay open, rest can be combined freely | Middle ground if both matter |

Rules that hold whichever license is chosen:

- Never commit or ship game files, `GameAssembly.dll` output, or MelonLoader-generated `Il2CppAssemblies`.
- Bundling `nvdaControllerClient64.dll` (LGPL, from NVDA's controller client package) and optionally `Tolk.dll` (LGPL) unmodified is fine; include their license texts in `THIRD_PARTY_NOTICES.md`, as DuelLinksAccess does.
- Link MelonLoader only to its official GitHub releases. The FF5 Screen Reader had to correct a README link that pointed to a malware site.
- Credit borrowed MIT code (e.g. MatthiewNocturne) in `THIRD_PARTY_NOTICES.md`; MIT code can be included in a GPL project.
- Release zip layout mirrors the game folder: `Mods/NocturneAccess.dll` plus the controller DLL at the root beside `smt3hd.exe`. A GitHub Actions workflow builds it on tag, but building needs local references, so CI may only lint unless references can be stubbed.

## Collaboration rules and open questions

Claude Code owns code and architecture; Francesco owns in-game testing, design decisions and investigative research.

- Explain the reasoning for each step before doing it: what is being patched, why that class, what the test will show.
- Never invent class, method or field names. Every name must come from the dumps, a reference repo, or a log line, and is recorded in `docs/class-index.md` with its source.
- The MelonLoader log is ground truth. **Rule (Francesco, 30 Sep 2026): after every game run, Claude Code reads the log itself, directly from the game folder** (`smt3hd\MelonLoader\Latest.log`, plus sessions archived in `smt3hd\UserData\NocturneAccess\Logs\`). Francesco never has to copy, paste or parse logs. `tools/log-summary.sh` gives a quick overview. MelonLoader 0.6.1 overwrites `Latest.log` on every launch and keeps no history, so the mod archives each session's log when the game closes.
- One system per session. Commit at every working state with a message that says what was verified in game.
- Give Francesco exact test steps: where to go in the game, which keys to press, what should be heard.
- Keep `docs/HANDOFF.md` current at the end of each session: state, next step, known bugs.

Open questions for Phase 0:

- [x] Exact Unity version: 2019.4.10f1. MelonLoader 0.7.x: not pursued, staying on 0.6.1.
- [ ] Is Denuvo still present on the current Steam build? Not inspected by design; irrelevant as long as the game runs.
- [x] Which input system does the game use? Its own `InputUtil` / `InputAssign` layer over Steam Input; no Rewired, no Input System. Legacy `UnityEngine.Input` is available for mod hotkeys.
- [ ] Which keyboard keys are already bound by the game and by NocturneModernController? Partly answered: F9, F10, F11 are taken by Nocturne Graphics Configurator. Game defaults still to record (`docs/KEYS.md`).
- [ ] Does the message window use one shared class for dialogue, battle and negotiation text?
- [ ] Where are demon, skill and affinity tables held at runtime?
- [ ] Is walkable-area or automap data readable for pathfinding?
- [ ] Can the earlier blind developer be contacted through community channels?
- [x] Final license choice: GPL-3.0 (Francesco, 30 Sep 2026).

## Current state

**1 Oct 2026, navigation (0.0.12 to 0.0.16):** diagnostics, first person on a first playthrough held in memory (flag 2800, never saved), the game's pad buttons as the movement input, and grid-snapped forward steps are verified in game. Blobber mode version 1 took over A / D and refused steps near walls; Francesco rejected that (keep the game's controls, snap only movement, cues never block; points of interest with auto-walk for fixed-camera rooms later). Read-only IL2CPP analysis of `GameAssembly.dll` is now a standard tool (`tools/native`). State and next steps: `docs/HANDOFF-PATHFINDING.md`; facts: `docs/NAVIGATION-DESIGN.md` sections 6 to 11. **0.0.17** (second navigation chat, verified 14:31: every snap on the grid, cues right at junctions, the Soul reachable; collision with objects such as gurneys slides the player sideways) and **0.0.18** (F3 centring key, verified 14:52; 0.0.19 fixes two edge cases). Next: pathfinding, points of interest and auto-walk in fixed-camera rooms first (`docs/PATHFINDING.md`); 0.0.20 to 0.0.21 build that scaffold, verified 15:20 (auto-walk to the Soul and the door worked); 0.0.22 lists only reachable points of interest (verified 19:07); 0.0.23 extends it to the basement corridors with named doors and first-person walking; tested in the prologue at 19:57, fixed in 0.0.24 (events, stairs, stale hit index, doors behind walls); the basement test at 20:20 showed a hidden NPC listed as a Soul, fixed in 0.0.25, which also adds spoken directions on End; after a long play session at 20:37 (Francesco: mature enough to progress through the game with minor friction) 0.0.26 speaks directions only when End is pressed and turns the player to a door even right at it; the 21:31 session (Medical Center 1F, 2F, annex gate) led to 0.0.27: elevator floor banner, Pixie listed, walks in front of doors, blobber square centres per area (not yet tested). Next: `docs/HANDOFF-PATHFINDING.md`: blobber mode version 2 keeps every game key and only carries a released W walk on to the next square's centre; reading the first-person routine showed that S turns the player round (not back) and that W with A or D walks diagonally (section 12).

Updated 30 Sep 2026, end of session 1.

**Phase 1: gate passed in substance (30 Sep 2026).** Probe session 1 ran from the title screen through the Conception prologue to the first save at the Shinjuku Medical Center terminal. All 34 hooks patched, 0 failed; no crash. Log archived as `UserData/NocturneAccess/Logs/session_2026-09-30_14-07-16.log`; condensed transcript via `tools/probe-transcript.pl` (kept in `research/`). Every screen visited is mapped in `docs/class-index.md` (UI object paths).

Findings:

- **All UI text is TextMeshPro and carries the game's exact strings.** The "never reconstruct" rule holds everywhere seen.
- **Dialogue:** three line components per page plus a speaker component. The typewriter only moves `<alpha=#00>` tags; **each line holds its full text from the first write, and all lines of a page are written at once when the page starts.** A page can be spoken whole, immediately, by stripping tags.
- **`itfMesManager.SetMessage` never fires.** Useful engine signals: `itfMesMngRequestMessage` (message start), `SwitchSpeaker` (speaker label on/off), `SelectWindowDispOn` / `itfMesMngRequestSelect` (choice opens), `ChangeSelection(index, 1)` (choice cursor).
- **Navigation-relevant text exists:** `fieldUI/flocation` (location name as you move, overworld included) and `fieldUI/fannounce` (Talk / Examine / Open Door / Enter / Exit prompts).
- **Menus** (title, difficulty, config, name entry, terminal, save) have clean item and description texts; the title and name-entry cursors still need a source.
- **Game keyboard defaults recorded** in `docs/KEYS.md`; no function key is used by the game.
- One Unity scene for the whole game: screens are `(Clone)` prefabs under `Canvas_UI`.

Gaps, not blocking:

- The probe compared raw strings, so every typewriter frame looked like new text: dialogue line 1 was muted after the first page. The mechanism is fully understood from lines 2 and 3 and the UI dumps.
- `UIManager/Text/Text01000+` (116 components holding a space, rewritten every frame) flood the TMP hook: the main cause of the stutter Francesco noticed in menus.
- Not visited: pause menu (`campUI`), battle, shops. Francesco now has a save at the first terminal, so these can be captured from there.

What 0.0.2 adds:

- `Core/Patcher.cs`: tolerant, logged patching (`[assembly: HarmonyDontPatchAll]`, strict signatures, patched / NOT DECLARED / FAILED lines and a summary).
- `Capture/TmpTextProbe.cs`: postfixes on `TMP_Text.set_text`, `SetText(string)`, `SetText(string, bool)`; logs `[Probe:TMP] path [type] = "text"` once a text settles (200 ms), with write counts and hidden flags, muting components that change over 30 times a second.
- `Capture/MesProbe.cs`: postfixes on 31 message-engine and lookup methods; logs `[Probe:Mes] Class.Method(args) -> result`, repeat-limited and muted if noisy.
- `Capture/UiTreeDumper.cs`: F6 writes every visible TMP text to the log. F7 pauses or resumes the probe. `Probe = true` in `[NocturneAccess_Debug]` (turn the default off before any player release).
- `[Scene]` lines on each scene load. `Core/LogArchive.cs`: each session's log copied to `UserData/NocturneAccess/Logs/` on quit.
- `tools/log-summary.sh`: quick overview Claude runs after every game run.

**Phase 2 step 1 built (0.0.3), waiting for the in-game test.**

- `Core/TmpRouter.cs`: the single TMP hook. Blank writes return before any call into the game (kills the `UIManager/Text` noise); each component is classified once by path and cached by instance ID. The probe is fed from here and now compares text without typewriter tags.
- `Core/TextUtil.cs`: tag stripping and placeholder detection.
- `Systems/Dialogue/DialogueReader.cs`: speaks each page (150 ms settle) as one interrupting utterance; speaker from `SwitchSpeaker` state, named on change or window reopen (`SpeakerEveryPage` option).
- `Systems/Dialogue/ChoiceReader.cs`: "Choice, N options. Text, 1 of N" (queued after the question), then each highlighted option (interrupting). Option count comes from options written since the last choice ended, because the option objects are still inactive at the first highlight.
- F1 repeats the last page (and the highlighted option). Config section `[NocturneAccess_Dialogue]`.
- `Patcher` now allows several features to add postfixes to the same method.

**Phase 2 step 1 verified in game with 0.0.4 (30 Sep 2026, 15:48 session, controller, console hidden):** every dialogue page spoken whole with the speaker named (Hijiri, Soul); narration pages ("> There is a large cylinder. What will you do?") spoken without a stale name; the "> End the game?" Yes/No choice announced ("Choice, 2 options. No, 2 of 2") and every cursor move read. No garbled speech; the 30-second log snapshots worked. Pause-menu screens mapped (class-index).

**1 Oct 2026, 10:23 session (`session_2026-10-01_10-23-24.log`, short, no errors):** Francesco pressed V (Toggle POV) in the Underground Facility: nothing happens, first person is locked to New Game+. Decisions: blobber mode as a toggle, on by default; navigation continues in a new chat from `docs/HANDOFF-PATHFINDING.md`. Unlock plan (event-flag postfix, memory only): `docs/NAVIGATION-DESIGN.md` section 5.

**0.0.11 tested (1 Oct 2026, `session_2026-10-01_09-47-54.log`): everything built works; Francesco calls the scaffolding reliable enough to play by ear, and moves on to navigation.** 44 hooks, no errors. Verified in the log:

- Party pick: "STATS. Art, 1 of 1"; back from Stats the pick is announced again ("Pause menu gives way to the Party pick"); cancel says "COMMAND MENU. Stats, 5 of 6".
- Stats summary includes the race ("Artemis Soul, Fiend. Level 3 ...") from `raceText_e`.
- **Level-up allocation verified** (first time): summary with "1 point left" after "> You leveled up!", cursor moves "Str 4" .. "Luc 3", "Agi 4. 0 points left", then the Yes/No.
- Pre-filled name: "Enter a name." then "Family: Takao. Given: Yuko. capital A".
- "Open Door" spoken once per approach (no doubles in four approaches).
- Battle targets with two Kodama: "Jirae Kodama 1", "Jirae Kodama 2"; the survivor is "Jirae Kodama".
- Name buttons: Francesco did not move onto them. The diagnostic showed `Nme_Draw_EFGIS.cursor_str` holds **the path of the object under the cursor** ("namelist/name_alpabet/name_alphabet_box/namelist_box01/Text_letter01TM"), the likely source for the buttons too; `funccursorPos`, `box_num`, `letter_num` never changed (not live); `functionImage` sits at the forward button's position. Still open.

**Next (Francesco, 1 Oct 2026): navigation and pathfinding in 3D dungeons (not the overworld yet), analysis first, no code.** See `docs/NAVIGATION-DESIGN.md`.

**0.0.10 tested (30 Sep 2026, 22:05 session, `session_2026-09-30_22-05-36.log`; Francesco asked for analysis only, no code, this session).** 44 hooks, no errors. Francesco's report and the log:

1. **Interaction prompts work perfectly** ("Open Door", "Talk"). Minor: a prompt is sometimes spoken twice within 100 ms (22:08:15.401 and .493, 22:09:26.633 and .725), likely the write and the reshow check both marking it pending.
2. **The Stats reader works well on the Stats screen**: "Artemis Soul. Level 1. HP 30 of 30. MP 12 of 12. Str 4, Mag 3, Vit 4, Agi 3, Luc 3." (raw counters: one `CounterCtr` per object, as expected). Not heard: the race (the summary was spoken at 12.724, the visible race `Text_raceTM_oubei` "Fiend" was written at 12.911; `statusUI.raceText` may not be that component, `raceText_e` exists), and **the party pick before it ("Art", `party_status01/Text_nameTM` highlighted `TMC01` under the title "STATS") is never read**. Level-up allocation not tested this session.
3. **Name entry: letters read very well** (cursor letters, digits, typed-letter echoes, character set, confirmation, Yes/No; Yuko's confirmation "Full Name. Family: Takao. Given: Yuko."). Not read: **pre-filled names** (Yuko's "Takao"/"Yuko" are written at opening but only the confirmation speaks them) and **the control buttons forward, back, delete, done**: image buttons that write no text; data candidates in `Nme_Draw` (`funccursorPos`, `namelist_rightbutton`, `_leftbutton`, `_deletebutton`, `_endbutton` with Animators).

Also seen: the elevator was not reached; combat not reached in this run.

**0.0.10 (built earlier the same day): Francesco's prologue list.**

- `AnnouncementReader` per-path options. `FollowsDialogue`: the location banner (and the interaction prompt) waits, at most 1.5 s, while a dialogue page is due (the elevator's "1Ｆ" before "> Select a floor."). `Reshow`: the interaction prompt `fieldUI/fannounce` ("Talk", "Open Door", "Examine", "Enter", "Exit") is spoken every time it reappears, by watching its visibility every 100 ms. "Enter a name." / "Enter a nickname." added.
- `Systems/Menus/NameEntryReader.cs`: the letter under the blinking `TMC12` cursor ("capital A", "r"), each typed letter, "Deleted X" (typed letters are re-read every 100 ms because deletions are blank writes), the character set ("ÀÁÂ, accented letters"), and the confirmation "Full Name. Family: Soul. Given: Artemis. Nickname: Art. > Is this okay?" before its Yes/No (MenuReader spec "Name prompt"; `NameEntryReader.ConfirmDue` is a SpeechOrder leader). F1 says the name so far. `TextUtil.SpeakableCharacter` names capitals and punctuation.
- `BattleReader`: the target comes from `nbTarSelProcess.GetTarSelProcessData().nowform` while the target help is shown ("Foul Will o' Wisp 2, HP 10 of 10"); enemy numbers are fixed per battle; the party panel hiding while an enemy talks no longer restarts the battle (resumes within 20 s unless results were shown); "Battle results." once.
- `Systems/Menus/StatusReader.cs`: the Stats screen and level-up allocation (`statusUI`). Summary on opening (name, race, level, HP, MP, stats, points left), waiting behind a due dialogue page; in level-up mode the stat under `stsCursor` (nearest label row) with its value, value changes, and "N points left". Numbers from the `CounterCtr` under `ObjLV`, `ObjNowHp`, `ObjMaxHp`, `ObjNowMp`, `ObjMaxMp`, `ObjStatus[i]`, `ObjPoint`; the raw counters are logged once per opening (`[Status] Opened: ...`) to check the mapping.

**0.0.9 verified in game (30 Sep 2026, 20:44 session, `session_2026-09-30_20-44-36.log`): Francesco replayed the whole prologue with his eyes closed**, from NEW GAME to the first battle and the first save, about an hour. 44 hooks, no errors. Dialogue, choices, locations, the elevator's floor choice, the terminal, saving, title and pause menus all worked. Combat: "Battle. Will o' Wisp, Will o' Wisp.", the command menu, "Attack", "Dodged the attack!", HP changes ("Will o' Wisp 1, HP 4 of 10."), F2 ("Press turns: 1. Will o' Wisp 1, HP 10 of 10. Will o' Wisp 2, HP 10 of 10. Art, HP 30 of 30, MP 12 of 12."). Francesco's list of what still needs work:

1. Interaction prompts ("Open Door", "Talk", "Examine") are not read; the location names are a great orientation tool and stay as they are.
2. Name entry is silent (player, Yuko, classmate); F6 dumps taken.
3. Elevator: the floor ("1Ｆ") was spoken before "> Select a floor." (the location banner is written 80 ms after the message request, the question 300 ms later).
4. Combat: moving the target cursor between two identical enemies is silent after the first; also found in the log: "Battle. ..." repeated whenever the party panel hides while an enemy talks, and "Battle results." repeated about 60 times (the results text is rewritten every frame).
5. The level-up allocation is silent (no text is written; values and cursor are objects).

**0.0.9 (built earlier the same day): combat, version 1.** Francesco (20:07 session, 30 Sep 2026): the config screen works "extremely well"; the volume notches have no numbers yet, which can wait; remove the title cue (it fired during the Atlus splash, 20:07:58, not on the title screen); focus on combat. He fought the first battle (Will o' Wisp) with F6 dumps.

- Removed `TitleCue` (a better signal will come later). `ConfigReader` polls a slider only on rows where one was found: no slider was ever found (no `[Config] Slider` line), and polling a closed screen's row caused a caught NullReferenceException (20:12:25).
- `MenuReader` spec "Battle menu" (`bmenuset(Clone)/bmenu/*/bmenu_command/bmenu_commandNN/bmenu_text01TM`, `TMC01`) with the help line as description. A spec's description may now be a single component without a line number.
- `Systems/Battle/BattleReader.cs`: battle start ("Battle. Will o' Wisp.", 1 s after the party panel appears, enemies from formindex 4 up to `enemypcnt`); the banner (`bannounce`), spoken on every `nbHelpProcess.nbDispText` call: a target (text2 set) interrupts and adds its HP when the name is unique, others queue; HP changes of every unit, polled every 200 ms ("Will o' Wisp, HP 12 of 30."); "Battle results." on the results screen; F2 status. Party units only for slots showing a name on the party panel, named as shown. Every data change is logged as `[Battle:Data]` (turn, phase, activeunit, Press Turn fields, counts, exp, macca, each unit) to check the field meanings. Config `[NocturneAccess_Battle] Enabled`, `SpeakHpChanges`; key `BattleStatus = F2`.

**0.0.8 verified in game (20:07 session): config screen, config and title Yes/No, location names ("Underground Facility", "Elevator", "Unknown Realm"). Built as:**

- `MenuReader` specs "Title prompt" (`titleUI(Clone)/(titlemain|titlemode)/talk01_choice/choice_answerNN/answer01TM`) and "Config prompt" (`configUI_steam(Clone)/ChoiceKernel/talk01_choice/...`), highlight `TMC08`; a choice spec opens with "Choice, N options.". Their questions (`namechoice/talk_window/Text01TM`, `configUI_steam(Clone)/talk_window/Text01TM`) are prompts in `AnnouncementReader`, so the options queue behind them.
- Location: `fieldUI/flocation/stretch/TextTM` is a prompt too: spoken when it appears or changes ("Underground Facility", "Terminal").
- `Systems/Menus/TitleCue.cs`: "Title screen. Press any button." when the title's Version text is shown and the title menu is still closed 500 ms later; once per appearance. The mod's own words: the game's prompt is an image.
- `Systems/Menus/ConfigReader.cs`: the config screen. Current row = highlighted (`TMC01`) visible row; tab from that row's scroll view, tab name from the tab text; value from the tagged setting text, the bound keys, or the row's `configMenuUI.slideObj` slider (polled every 100 ms; logs its range once); position among the tab's rows by sibling order, headings excluded; description from `cfgmenu_note01`. Opening "AUDIO tab. CONFIRM, OK, 1 of 7. Apply changed settings.", row moves without the tab, value changes alone.

**0.0.7 verified in game (30 Sep 2026, 16:49 session, `session_2026-09-30_16-49-26.log`).** 43 hooks, no errors or warnings. The terminal now says the question first, then "Save, 1 of 3" (`[Order] Terminal menu waits for the dialogue page.`). Load and save by ear: "File 1. AAAA, Level 1, Shinjuku Medical Center, NORMAL", "File 2. No File" (loading) / "New File" (saving), all 10+ files with scrolling; the load and overwrite prompts with "Choice, 2 options. No, 2 of 2" (the game defaults to No) and every move; a new save into File 2 then read back. The cursor field mapping held (0 = Yes, 1 = No); the highlight-position fallback never triggered. Francesco's feedback: the title screen's EXIT Yes/No is silent; wanted cues for the press-any-button screen and for the map name when a map loads. F6 dumps of the config screen (all tabs), Magatama and Stats.

**0.0.7 (built earlier the same day): the save/load screen and speech order.**

- `Systems/SpeechOrder.cs`: a screen's first line (menu, save list, save Yes/No) waits while a leader is due, at most 1.5 s, then queues behind it. Leaders: a dialogue page (`DialogueReader.PageDue`: lines changing, or `itfMesMngRequestMessage` fired and its page not spoken yet) and a prompt (`AnnouncementReader.AnyDue`: changed in the last 1.5 s, not spoken). Logs `[Order] X waits for the dialogue page.` Fixes the terminal race.
- `Systems/Menus/SaveChoiceReader.cs`: the save/load Yes/No. Armed by `slMain.slQueryYesNo` / `slContinueYesNo` (postfix, logs the default) or by writes to the option texts; while the options are visible, reads `slMain.slQueryYesNoCursor` every frame and speaks `slMain._saveUI._choiceList[cursor]` ("Choice, 2 options. Yes, 1 of 2", then each move). Cross-check: the option nearest `_choiceCursor` is logged; a mismatch logs a warning, and only if the highlight moves between options while the cursor field never moves is the highlight used instead. F1 repeats "Load File 1? Yes, 1 of 2".
- `Systems/Menus/SaveListReader.cs`: the file list. Trigger: `TMC01` on a slot's texts; identity: nearest parent that is one of `slMain._saveUI._fileList`; numbers from `CounterCtr.value` of `_fileNum` / `_noDataNum` (whichever is shown) and `_playerLV`. Says "File 1. AAAA, Level 1, Shinjuku Medical Center, NORMAL" or "File 2. No File". Each announcement logs the raw numbers (file number, level, play time, `gslSelect`, `gslSelTop`) to check against the screen; play time is logged only, until its format is known.

**0.0.6 verified in game (30 Sep 2026, 16:18 session, archived as `session_2026-09-30_16-18-35.log`).** Title menu spoken on every move with its description ("LOAD GAME, 1 of 8. Continue your progress from a save file."; 8 items are visible, NEW GAME: MANIAX and CONTINUE are hidden in Francesco's copy). Terminal menu ("Save, 1 of 3", Talk, Exit), pause menu with title on entry ("COMMAND MENU. Item, 1 of 6"), System and Item submenus ("ITEM. Use, 1 of 4"), reopening. Load prompts and the End Game choice still work. 40 hooks, 0 failed, no errors. Two issues found:

- Terminal race: "Save, 1 of 3" was spoken before the question "> There is a large cylinder. What will you do?". The menu's first highlight (08.079) and the engine's `itfMesMngRequestMessage(5, 0, 0, 1)` (08.117) come together, but the question's lines are only written about 400 ms later, so the menu settled and spoke first.
- Silent sub-screens, mapped from Francesco's F6 dumps (class-index, "Pause menu sub-screens"): item lists, the skill user pick, Magatama, Stats. The save slot list and the save Yes/No are silent too.

**0.0.5 test (30 Sep 2026, 16:08 session): menus failed, fixed in 0.0.6.** Save/load prompts were spoken ("Checking data...", "Select a file to load.", "Load File 1?", "Load completed."), dialogue and the "Return to the title screen?" choice worked. But the title and pause menus stayed silent, speaking only once as they closed ("COMMAND MENU. Title Screen, 4 of 5"; "EXIT, 8 of 8"). Diagnosis from the log: classification and highlight writes were correct and visible, but these menus **rewrite the highlighted item, title and description every frame with identical text** (the probe hides identical rewrites), and MenuReader restarted its settle timer on every write, so it only ran out when the menu closed. 0.0.6 restarts the timer only on a real text change. Lesson for every reader: never let an identical rewrite count as activity.

Still open from that test: the save prompt's Yes/No (`saveUI(Clone)/saveannounce/saveannounce_choice/answer01TM`, `answer02TM`) carries no highlight material; the cursor must come from another source (probe `saveUI`/`saveFileUI` class methods next). Francesco notes the same pattern in Suikoden 1&2 HD, where several classes drove identical-looking UI elements.

**Phase 2 step 2 built (0.0.5).**

- `Systems/Menus/MenuReader.cs`: generic reader for menus that mark the cursor with a font material. Specs: pause menu and its submenus (`TMC01`, title from `title_window_oubei`), terminal menu (`TMC01`), title menu (`TMC00`, description from `titlemain_note` lines). Says "Title. Item, N of M. Description"; waits until the item is visible; the first announcement after opening is queued, later moves interrupt; a menu whose highlighted item disappears counts as closed.
- `Systems/Menus/AnnouncementReader.cs`: save/load prompts (`saveannounce_tex/Text_announce01TM`, `savelist_data/Text_announceTM`) and the controller-disconnect notice, spoken once visible, queued.
- F1 repeats whichever came last (menu or dialogue). Config `[NocturneAccess_Menus] Enabled`.
- The pause menu has no description text: its right side is the party panel (names; HP/MP are sprite digits). The title menu's notes are its descriptions.
- Not yet: the save-slot list (all empty slots are `savefile(Clone)` with identical paths, so slots need identity by component, not path) and the save prompt's Yes/No (no highlight marker seen yet); config screen values; name entry.

**Plan for step 2 (kept for reference):** One reader for "highlighted item" menus using the font-material signal (pause menu and submenus, terminal menu: `TMC01` highlighted), speaking the screen title on entry and the item on each move; then the title menu (`TMC00` highlighted plus its note), the save/load list and prompts. Numbers (HP, MP, stats) come later from game data, since they are not text.

**First test of 0.0.3 (30 Sep 2026, around 15:20–15:41) was untestable: garbled technical speech on every input.** Troubleshooting:

- The mod's own speech was clean: every utterance is mirrored as `[SPEECH]`, and the surviving log has only 4, all correct ("> There is a large cylinder. What will you do?", "Hijiri: First things first, try and make it outside. Alive."). Dialogue and speaker detection worked.
- NVDA's log (`%TEMP%\nvda.log`) shows a device appearing at 15:21 (braille auto-detect rescans: the controller connecting) and repeated freezes of up to 5 s at 15:27–15:29 (flooded with text).
- Cause, by elimination: **NVDA reading the MelonLoader console window**, which prints every log line; the probe writes lines on almost every input. With a keyboard the game window must have focus, so NVDA never reads the console; a controller keeps working while the console has focus. Closing that console also kills the game without a clean shutdown, which is why that session's log was never archived and was then overwritten.
- Fix: launch option `--melonloader.hideconsole` (MelonLoader docs; the log file is still written). 0.0.4 also snapshots the log to the archive every 30 seconds, so an unclean exit no longer loses it.
- Francesco asked for the speaker on every page: `SpeakerEveryPage` now defaults to true, and his config was updated.

Plan this was built from:

1. Make the TMP hook cheap: decide per component once (by pointer) whether it matters, and return immediately for noise such as `UIManager/Text`. Make the probe tag-aware (compare stripped text) so the typewriter no longer triggers mutes.
2. Dialogue reader: route `talk_window/Text0NTM` and `name_window/TextTM` writes, strip tags, detect page changes from the lines, speak "Speaker: line 1 line 2 line 3" as one interrupting utterance. Repeat key for the last page.
3. Choice reader: on choice open, speak the options; on `ChangeSelection(index, 1)` speak the highlighted option from `talk01_choice/choice_answer_oubei0N/answer01TM`.
4. Test from Francesco's save: talk to NPCs, answer a choice; in the same run open the pause menu with the probe on to map `campUI`.
5. Then title, config, name entry, save and terminal menus (Phase 2 gate: prologue to first save by ear).

**Phase 0: gate passed (30 Sep 2026).** Verified in game and in `Latest.log`: both mods load ("2 Mods loaded"), the NVDA backend found `nvdaControllerClient.dll` with NVDA running, "Nocturne Access loaded" is spoken at startup, and F8 speaks the status line on every press. No crash, and no conflict with HD Ears or Graphics Configurator (still 120 fps). The game launches normally.

**Known issues and notes:**

- F8 pressed on the opening splash screen restarts the opening music; on the title menu it does nothing but our status line. Almost certainly the game's "press any key" handler on the splash (the type list has `slMain.slHitAnyKeyNextFuncDelegate`): any key would do the same. Harmless; our mod only reads keys and never injects them.

- `Latest.log` shows a `MelonStartScreen` exception (`Texture2D` constructor not found on Unity 2019.4.10). Harmless: only the loader's splash screen fails. The `--melonloader.disablestartscreen` launch option removes it.
- `Latest.log` shows a `FileSystemWatcher` exception from MelonLoader's preferences. It comes from the Graphics Configurator's config at the game root, set with a bare file name. Harmless; our config uses an absolute path to avoid it.
- F8 is not yet checked against the game's own keyboard bindings.
- Tolk backend is untested (no `Tolk.dll` installed).

## Sources

Pages opened for this document: [MatthiewNocturne](https://github.com/NocturneMods/MatthiewNocturne), [NocturneModernController PR #7](https://github.com/tanatyucom/NocturneModernController/pull/7). Other facts come from search results linked in the tables above.

**0.0.28, battle (1 Oct 2026, 22:14 session, `session_2026-10-01_22-14-18.log`).** Francesco fought the Pretas in Room A203 (with Pixie) and two Kodama. Reported:

1. **Whose turn was never said** ("Fiend's turn", "Pixie's turn"). `activeunit` is the acting unit's formindex and changes about 50 ms before its command menu opens, so a separate announcement would come after the menu. Fix: `MenuSpec.OpeningPrefix`; the battle menu's opening line starts with `BattleReader.TurnAnnouncement()`, read at that moment: "Pixie's turn. Attack, 1 of 4. Physical attack (low damage).". Said once per unit and press-turn state (backing out of a submenu does not repeat it; a unit acting again after an extra press turn does).
2. **F2 unclear** ("Press turns: 1 full, 1 blinking" ran together). Now short sentences: "Art's turn. 2 press turns left: 1 full and 1 half. 2 enemies. Preta 2: HP 4 of 20. Preta 3: HP 20 of 20. Party. Art: HP 42 of 42, MP 18 of 21. Pixie: HP 36 of 36, MP 18 of 24." Fallen enemies are left out. The press-turn values are verified in this log (class-index, Battle).
3. **Enemy HP sometimes missing.** Not a key press: enemies were read from formindex 4 up to `4 + enemypcnt`, but `enemypcnt` counts only the living while the survivors keep their slots. With Pretas 1 and 2 fallen only slot 4 was read, so Preta 3 (slot 6) was unknown: "Target formindex 6: no unit known", the target without HP and no HP after the hit. Fix: every slot used this battle is read; further slots only while fewer living enemies were found than `enemypcnt`.
4. **Shift+F2 battle dump** (Francesco asked whether a dump would help scope the missing pieces): every field of the main process data, each formindex's unit (level, HP, MP, `badstatus`, `param`, skills with names, `nbParty_t.count` buffs), the formation and the target selection, to `UserData/NocturneAccess/BattleDumps`. Next: affinities, buffs, ailments and skill costs from these dumps.

Also verified in this log: 0.0.27's elevator fix (the current floor "Ｂ1", "Ｒ", "2Ｆ" not spoken with "> Select a floor."), walks aimed in front of doors, square-centre offsets (basement 0 0, 2F 300 0). One rebuild of the 2F map chose 240 instead of 300 (the score is flat near the best): 0.0.28 takes the middle of the run of offsets within 3 % of the best.

**0.0.29, battle (2 Oct 2026, 09:14 session, `session_2026-10-02_09-14-01.log`, recording `2026-10-02 09-22-07.mp4`, battle dumps 09:16:35 and 09:16:46).** 0.0.28 verified: "Art's turn" / "Pixie's turn" opened each command menu; F2 worked ("1 press turn left: 0 full and 1 half"). Francesco asked for:

1. **Skill costs** ("Lunge 4 HP, Analyze 2 MP" are images in the menu). The game's rule, read offline from `cmpDrawSkill.cmpSkillNameCostDraw`: `costtype` 1 = HP, `maxhp * cost / 100 + costbase`; otherwise MP, `cost`; 0 = none. `MenuSpec.ItemDetail`: "Lunge, 4 HP, 2 of 4. Low Physical damage to one foe." The skill is found by name among the acting unit's skills (`activeunit`).
2. **Weakness hits not announced, for both sides.** No text is written; the press turns show it: Pixie's Zio on the Preta turned the last full icon into a half one (1/1 to 0 full of 1). `ObservePress`: while one unit acts, `press4_p` one less and `press4_ten` the same = a half-turn hit; the actor's `nowcommand` / `nowindex` (skill id) give the element (`datSkill.tbl[id].skillattr`, attack = physical), the target `nowtform`, and its `aisyo[element]` bit 31 = weak: "Weakness hit on Preta, only half a turn used."; otherwise "Critical hit, only half a turn used."; other commands (Pass) "Half a turn used.". Enemies too.
3. **Analyze said nothing.** Its screen writes race, name, skills and the affinity line as text (`banalyze_oubei`); `BattleAnalyze` speaks "Haunt Preta. Level 4. HP 30 of 30. MP 24 of 24. Null: Dark. Weak: Light/Magic. Skills: Feral Claw." (level, HP, MP from data) and remembers the affinities by name for F3.
4. **F2 split:** F2 = whose turn, press turns, the party; F3 in battle = the enemies (with known affinities). F3 still centres outside battle.
5. **Cross-check with the recording:** the screen also shows "ENEMY TURN" / "PLAYER TURN" (now spoken: "Enemy turn." at once, "Player turn." before the next party menu), damage numbers (now "Pixie: 7 damage, HP 25 of 36."; healing "20 healed"; Lunge's own HP "4 HP cost"), which enemy acts (now "Preta: Feral Claw"), and the results' EXP, macca and item (now "Battle results. 9 EXP. 27 macca. Medicine."). Not yet: each member's EXP to the next level, the moon phase, the AUTO battle toggle.
6. **Battle log (F5):** the previous side's turn and the current one so far, kept until the next battle.

Not yet verified: the item arrays at the results (logged), skillattr for Zio (logged at the half-turn), Pass's `nowcommand`.

## Code map for the debug and efficiency pass (2 Oct 2026)

Francesco plans a debug and efficiency pass over everything so far. Where the work happens and what costs most:

- **Per frame (`ModMain.OnUpdate`):** every reader's `Tick`. Cheap ones poll at 80 to 500 ms; `BattleReader` reads every unit every 200 ms in battle; `AnnouncementReader` checks visibility every 100 to 250 ms.
- **TMP hook (`TmpRouter`):** one postfix on every TMP text write; each component classified once by its hierarchy path (cached by instance id). Readers must ignore identical rewrites.
- **Field update prefix (`FieldState`, 30 Hz):** `BlobberMode.BeforeCalc` (calls `WalkGrid.Lattice`, which calls `WalkGrid.Ensure` every tick: `CollisionCache.Refresh` is a pointer and count check; `ReadHiddenNpcs` loops the NPC table, on this path at most every 500 ms since 0.0.31), `AutoWalk.BeforeCalc`.
- **`WalkGrid`:** `Build` 0 to 7 ms on area change (field 11: 35 000 cells); `Plan` (Dijkstra over the whole grid, allocates three arrays the size of the grid, about 1.4 MB in the basement) 1 to 10 ms per call. `AutoWalk.Route` calls it up to 4 times for a person in first person, and `PointsOfInterest.Build` calls `ReachableNear` (a cached flood) per point. `RouteGuide` plans once per End press. Candidates: reuse the arrays, stop the search at the goal (A*), cache the route per target.
- **`PointsOfInterest.Build`** runs at each Page Up / Page Down and at each `AutoWalk` start and re-plan (`Current` rebuilds the list).
- **Battle menu cursor:** `BattleSkills.CostOfActing` looks the item up by name among the actor's skills (`datSkillName.Get` per skill) at each cursor move.
- **Logs:** `[Nav:Trace]` at most once a second while exploring (every 250 ms until 0.0.30; config entry `TraceIntervalMsV2`), `[Battle:Data]` on change, `[Probe:*]` only with `Probe = true`.
- **Known rough edges:** `AutoWalk` is about 550 lines; the `MenuSpec` list in `MenuReader` grows per screen. (`BattleReader`, about 780 lines, was split into partial files in 0.0.31.)

**0.0.30, cleanup and debug pass (2 Oct 2026, no change in what the mod says or does).** Applied:

- Shared helpers: `UiUtil.IsVisible` (was copied in 9 readers), `Navigation/Angles` (`Diff`, `Mod360`, `Square`, `Bearing`, copied in `AutoWalk`, `RouteGuide`, `BlobberMode`, `CentreWalk`), `TextUtil.Capitalize` (3 copies). Same arithmetic, same results.
- `ModMain.OnUpdate`: each system's update runs guarded (`[Update] <name> failed`, logged 3 times per system), so one exception no longer skips the others for that frame.
- `FieldState`: blobber mode and auto-walk guarded separately; on an exception the pad buttons are let go (and the walk ends), so a repeating error cannot leave the player walking.
- `LogArchive`: the session file name is worked out once (`Process.GetCurrentProcess()` was called and never disposed every 30 s).
- `TmpRouter`: the per-component classification cache is cleared at 20 000 entries (new UI copies every battle and save screen made it grow all session).
- `WalkGrid.Plan`: the search arrays and queue are reused between calls (reset each time), instead of about 1.4 MB allocated per search.

Proposed and **approved** (Francesco, 2 Oct 2026): `docs/REFACTOR-PLAN.md`, items 1 to 5, plus item 0, a bug fix. Done in 0.0.31 (below).

**0.0.29 and 0.0.30 verified in game (2 Oct 2026, 10:07 session, `session_2026-10-02_10-07-39.log`).** Francesco: everything "looks and feels exactly as before" after the cleanup; no `[ERROR]` or `[Update]` lines. Confirmed in the log: Analyze spoken ("Haunt Preta. Level 4. HP 30 of 30. MP 24 of 24. Null: Dark. Weak: Light/Magic. Skills: Feral Claw."; a Will o' Wisp's line adds "Str: Phys"); weakness hits (`Half turn: actor 1, nowcommand 1, nowindex 13, element 3` = Zio is Elec, the Preta's and Will o' Wisp's `aisyo[3]` 0x80000096); F5 log; results items (`item [2 0 0 0] itemcnt [1 0 0 0]`: "19 EXP. 67 macca. Medicine."); "Enemy turn.", "Preta: Feral Claw", damage amounts. One bug: an HP line ("Art, HP 48 of 48") before "Battle. Preta." in the first battle of the session, from the target poll running before the battle start (`docs/REFACTOR-PLAN.md` item 0).

**0.0.31, the refactor plan (2 Oct 2026, built, not yet tested in game).** `docs/REFACTOR-PLAN.md` items 0 to 5, one commit each after the revert point `aa9c7c2`:

- **0, the bug** (`dfec866`): the real cause was an overflow, not a target call. `_targetCallAt` started at `long.MinValue`, and `now - long.MinValue` wraps to a negative number, so the 300 ms target window read as open until the session's first real target call: `PollTarget` spoke `nowform` 0 (Art) 5 ms after the battle UI came up. It now starts at `long.MinValue / 2`, like the mod's other timers (none of them subtracts from an unhalved `MinValue`). As approved, targets are also polled and target banners spoken only once the start is announced.
- **1** (`c74f100`): `BattleReader` split into partial files: `BattleReader.cs` (install, start and end, `Tick`), `.Banner.cs`, `.Turns.cs`, `.Units.cs`, `.Results.cs`. Pure move, checked line by line.
- **2** (`1840d84`): `Capture/DumpWriter` holds the file, log summary, guarded sections and save of both dumps; files, log lines and speech as before.
- **3** (`d10c492`): `AnnouncementReader` keeps one entry per path; a new copy of a component (each save screen) takes it over, reset as a new entry and moved to the end of the list, so prompts are spoken as before. Writes from a replaced copy are ignored.
- **4** (`5ec125c`): `WalkGrid.Ensure` from `Lattice` (every field update) reads hidden NPCs at most every 500 ms; area, collision and window changes rebuild at once with a fresh read; the key-driven callers read them every time, as before.
- **5** (`51b790f`): `[Nav:Trace]` at most once a second by default, new config entry `TraceIntervalMsV2` (the old `TraceIntervalMs = 250` line stays in the file, unused).

**0.0.31 verified in game (2 Oct 2026, 12:03 session, `session_2026-10-02_12-03-02.log`).** Francesco: no regression, battle "big improvements". Log: the session's first battle said "Battle. Will o' Wisp, Kodama, Will o' Wisp." with no HP line before it (item 0); `[Nav:Trace]` once a second (120 lines in 10 minutes); save prompts once each ("Checking data...", "Save completed."); Home walk to the Terminal door arrived; no `[ERROR]` or `[Update]` lines. The dumps (Shift+F2, Shift+F6) were not used. Francesco also raised the game's internal rendering resolution to 2560x1440 with Nocturne Graphics Configurator: nothing in the log changed (the mod reads no screen coordinates or pixels; the `MelonStartScreen` and `FileSystemWatcher` exceptions at start are MelonLoader's and were there before).

**0.0.32 (2 Oct 2026, built, not yet tested in game): item and skill lists, skills on the Stats screen.**

- **Item and skill lists** (`Systems/Menus/CampListReader.cs`): Item (Use, Discard, Gems, Key Items) and Skill were silent. An entry that cannot be used now is drawn `TMC02` and keeps it under the cursor, so the highlight material cannot give the cursor; the reader takes it from the game: postfixes on `cmpDrawItem.cmpDrawItemList` / `cmpDrawSkill.cmpDrawSkillList` (called every frame while shown) give the list's cursor, the entry is `CursorPos.Index + Shift` (`cmpGetCursorIndex`, read offline). Spoken: "What will you use? Medicine, have 3, 1 of 3. Fixed HP recovery for one ally." (title on opening, count from `DDS3_GBWK.item[id]` except key items, "unavailable" for `TMC02` entries, skills with their cost as in battle, the game's help text). The name is the row's text; the id's table name must match it, or count, cost and description are left out (logged once). Raw cursor data is logged at each opening (`[CampList] ... list opened`) to check `ListNums` / `ShiftMax` and the item ids. Known gap: coming back to the list from the target pick says nothing until the cursor moves.
- **Stats** (`StatusReader`): the summary adds "Skills: Dia, Zio, Seduce. Skills to learn: Rakunda, and 2 hidden. Str: Elec." (`statusUI.obtainedText`, `awaitText`, `aisyoText`).

**0.0.32 tested (2 Oct 2026, 13:20 session, `session_2026-10-02_13-20-58.log`).** Two problems:

- **Every item read as the first row** ("Medicine", "ID Card"). The cursor fields were swapped: `CursorPos.Shift` is the visible row and `Index` the first entry shown (Index stayed 0 while Shift went 0, 1, 2). The entry (`Index + Shift`) and the ids were right (2 Medicine, 3 Life Stone, 21 Maragi Rock; 112 ID Card, 113 Annex Gate Pass), and the name check caught the mismatch, so no wrong count or description was spoken. Worked: the first entry ("What will you use? Medicine, have 5, 1 of 3. Fixed HP recovery for one ally."), "KEY ITEMS. ID Card, 1 of 2. ...", the Stats summary ("Skills: Lunge, Analyze. Skills to learn: Life Bonus, and 3 hidden. Normal resistance."). An empty Gems list (`ItemCnt 0`) said nothing.
- **The elevator left the B1 Page Up list** ("Elevator 01eve_04 ... (not reachable, left out)"). Not a 0.0.31 or 0.0.32 change: the walking map is limited to 400 cells per side and was always centred on the player, so it cut areas that fit. Back in B1 from the Healing Room (z -5650) the map ended at z 350 (`242 x 209 cells from -10515 -5685`, against 221 rows before) and the elevator (z 850) fell outside it. Earlier sessions entered B1 from elsewhere.

**0.0.33 (2 Oct 2026, built, not yet tested):** the list reads the row at `Shift`; an empty item list says "GEMS. Empty."; `WalkGrid.Build` only narrows an area larger than the window, sliding the window to hold the player inside the area (`Window`).

**0.0.33 verified (2 Oct 2026, 13:45 session, `session_2026-10-02_13-45-14.log`, long play through the annex with 8 navigation dumps).** Francesco: item and skill lists, the Stats skills and the B1 elevator work. Three findings:

- **Talking demons missing from Page Up** (annex areas 12 and 13, "Demon: [game line removed]"): their `fld_Npc` entry has no field object at all (`fldObj` null), and `PointsOfInterest.Shown` counted that as hidden, like the basement's `npc_hoji_01` (whose object exists but is inactive).
- **Annex gate**: everything on both sides is loaded (one area); the gate is collision block 1, which the game stops checking once the Annex Gate Pass is used (`fldHit.fldHitChkUnit` skips a polygon when `fldGbChkHit(field, area + 1, blockID)` is not 0, read offline). The walking map kept it as a wall, so the Soul and door past it were "not reachable, left out", and blobber mode said "Wall ahead" at the open gate.
- **Magatama screen** said nothing ("Which will you ingest?", "Marogareh", "Imparts Physical skills", "Normal resistance").

**0.0.34 (2 Oct 2026, built, not yet tested):** an NPC with no object is shown and listed as "Demon" (the game's speaker name for them); `CollisionCache` asks the game's two checks (`fldGbChkHit` per block, `fldGbChkNpc` per hoji id) after each rebuild and at most every 500 ms, leaves out what the game ignores in the walking map and the wall cues, and counts a change as a collision change (log: `[Nav] Collision switches in field F area A: blocks off [...], held NPCs off [...]`); a `MenuReader` spec "Magatama" speaks the title, the magatama's name and its notes ("Which will you ingest? Marogareh. Imparts Physical skills. Normal resistance."), with no position (one slot) and each note as a sentence. The stat bonuses are images, not read yet.

**0.0.34 partly tested (2 Oct 2026, 14:48 session, `session_2026-10-02_14-48-48.log`, annex play, 2 navigation dumps).** No errors. Seen working: the talking demon `02npc_01` is now a point of interest labelled "Demon" (dump 15:05:38); `[Nav] Collision switches` reads the game sensibly (B1: "held NPCs off [1]", the basement's hidden Soul `npc_hoji_01`; other areas: nothing off). Not reached this time (Francesco keeps playing forward): talking to a demon through Page Up / Home, the annex gate after the pass, the Magatama screen.

**Open problem: an item box "unlocked" only after walking a ramp** (field 15 area 4, Annex 1F; dumps 15:05:38 and 15:06:19). Item box gEvnRng 17 (`program_eve` at -900 4800, floor y 0) was "not reachable, left out" while the player stood on the lower floor #86 (y 100, z 2200 to 3330) and became reachable once the player walked up ramp #90 (y 100 at z 3100 to y 0 at z 3330, normal -0.917 in y, about 13 units per cell, so within MaxStep) onto floor #81 (y 0). The walking map should join the ramp. Leading hypothesis to check first: `WalkGrid` keeps at most `Layers` = 3 floor heights per cell, and area 4 stacks many (8228 cells with more than one level; ceilings with normal +y count as floors too), so the ramp's layer is dropped where the cell is full. Check with the 15:05:38 dump (the map at level y 98 shows the ramp cells as `.` up to z 3315 and the y 0 floor as `:`), then fix (ignore ceilings, raise Layers, or keep the layers nearest the walkable band).

**Next work (Francesco, 2 Oct 2026): `docs/EXPLORED-DOORS-PLAN.md`.** Item 1: an "explored" mark for generic doors (not the named ones: Terminal, Healing Room, Elevator), kept across sessions, because pathfinding alone does not give a mental map of the annex's identical doors and corridors (item 2, the reason). Item 3 (added by Francesco): opened item boxes say "empty". Item 4, custom waypoints the pathfinder can track, is recorded for later and must not be built yet.

**0.0.35 (2 Oct 2026, built, not yet tested): items 1 to 3.** New `Systems/Navigation/ExploredDoors.cs`: every frame it follows `fldEveHit.OldHitDoor` and `fldGb.fieldID` / `areaID`; a door volume left within 3 s before an area change is marked ("left through it"), and after the change the door volume the player stands in (or, with no hit 1.5 s after regaining control, the nearest door within 300) is marked too ("came in through it"); log `[Nav:Explored]`. One list per save file (Francesco: one per save unless blocked; nothing blocked it): `UserData/NocturneAccess/Explored/fileNN.txt` (`door field/area/name` lines; file = `slMain.gslSelect` + 1), read after `slLoadProc_closeFile`, emptied at `dds3TitleInit` (new game), appended at once when a door is marked in a loaded file, rewritten whole at a save (`slSaveProc_closeFile` and `slSaveWriteEnd_saveData`; which one fires is logged). `PointsOfInterest`: `Poi.Explored` (generic doors only; a merged duplicate passes it on), `Poi.Empty` (an item box whose `fld_Npc.gfldTakaraWork` entry, matched by `TakaraIdx`, has left phase 0: read offline from `fldItemBoxAdd` and `fldItemBoxCalcTask`, seen in the 15:05 / 15:06 dumps), `Spoken` ("Door, explored") for Page Up / Page Down, `InSentence` ("explored door") for Home, and End prefixes "Explored door." / "Empty item box.". The game's `fldGbChkTakaraOpen` runs in protected code (`.sbss`) and is not called. To watch: whether `gslSelect` counts from the list's scroll top (files beyond the first screen), a quick save if the game has one, and whether an opened box can keep its item. Test steps: `docs/NEXT-SESSION.md`, "0.0.35 test".

**0.0.35 verified (2 Oct 2026, 15:49 and 16:11 sessions, `session_2026-10-02_16-11-39.log`; Francesco: "solid and reliable"; first boss, Forneus, won and saved).** Explored doors marked on both sides through the annex (both "left through it" and "came in through it", most arrivals by the nearest door at 200 units, some by the game's hit); End spoke "Explored door. Ahead, 8 metres."; Home "Walking to explored door". Saving: both `slSaveProc_closeFile` and `slSaveWriteEnd_saveData` fire on a terminal save (only `slSaveProc_closeFile` once, 16:24:44, File 2: 7 doors written). **Suspend data:** the title's CONTINUE ("Continue using this suspend data?") loads as `gslSelect` 20, so it keeps its own list (`file21.txt`); the suspend save writes it too, and both save hooks also fire about 200 ms after a suspend load (the game deletes the suspend data); a terminal save then copies the list to the chosen file. Behaviour is right; the name is only cosmetic. Limitation (Francesco): connecting doors (B1 to the Underground Facility) are generic doors like small rooms'; the door volumes' `areaname` is the door's own floor block ("01", "02"), not its destination, so nothing in the loaded data tells them apart. Kept as is for now; idea recorded in `docs/EXPLORED-DOORS-PLAN.md` item 1.

**Bug: "the pathfinder triggered" before Forneus (navdump 16:40:41).** Not the pathfinder: no `[Nav:Walk]` plan, keys and pads idle. After "Will you enter?" the game's own scripted walk carried the player across the Annex Lobby (area 1; positions jumping, `fldPlayerCalc_Nml_syukan` 0 calls a second against 30 under control), and blobber mode spoke its cues on that movement ("East, open ahead", "South, open ahead", "Opening left"), which sounded like a walk.

**0.0.36 (2 Oct 2026, built, not yet tested):** `BlobberMode` speaks turn and centre cues only on ticks where the game's first-person movement routine ran (`gameMoved`, the existing `SyukanCount` check); when the game moves or turns the player itself the cue cell is reset silently. The facing already spoken is kept, so after a cutscene the facing is spoken only if the game turned the player, and a new area still announces it.

**0.0.37 (2 Oct 2026, built, diagnostic only):** the door warp table decoded offline (`docs/EXPLORED-DOORS-PLAN.md` item 1, `docs/class-index.md` "Door warp table") and written to the Shift+F6 navigation dump ("Door warp table": entries of the current area in the log, the whole field in the file). To verify: in an area whose doors were gone through in the 16:11 session (for example Annex 1F area 21), Shift+F6 and compare `to field / area+1 / at` with the `[Nav:Explored]` pairs (21/01d_04 -> 8/01d_05, 21/01d_09 -> 6/01d_10, 21/01d_06 -> 6/01d_02, 21/01d_11 -> 9/01d_04, 21/01d_05 -> 9/01d_01).

**0.0.37 verified (17:11 session, dumps 17:11:53 and 17:13:57):** the door warp table matches every door pair gone through; analysis (rooms and passages of field 15, kinds, story flags, banners per area) in `docs/EXPLORED-DOORS-PLAN.md`, "Door warp table: verified and analysed". Next: Francesco chooses what a door should say.

**0.0.38 (2 Oct 2026, built, not yet tested): door kinds (Francesco: option 1).** New `Systems/Navigation/DoorTable.cs`; `Poi.DoorKind` ("room", "passage", "exit") on generic doors, spoken after "explored" on Page Up / Page Down, Home and End (details and expected doors: `docs/EXPLORED-DOORS-PLAN.md`). Config `Navigation.DoorKinds` (default true) turns it off without a rebuild; revert point f0621c8. Log: `[Nav:Doors] Door table of field N: K entries.`

**2 Oct 2026, 20:10 session (`session_2026-10-02_20-10-19.log`): 0.0.38 verified; the overworld reached.** Door kinds heard ("Door, explored, passage", Page Up and Home); File 1 loaded with its 20 explored doors; item boxes opened and then listed "Item box, empty" (item 3 heard in game); the Medical Center's "Exit door" named by the game. Francesco left the Medical Center for the overworld (Shinjuku, field 2), walked to Yoyogi Park (field 16) and made two navigation dumps (20:26:27 overworld, 20:28:28 Yoyogi Park) and a video. Seen in the log: a demon conversation (Shikigami) spoke two lines twice about 0.5 s apart (to fix later). **Next: the overworld, in its own chat: `docs/HANDOFF-OVERWORLD.md`.** The project is now on GitHub, private (`origin` Artemis626/nocturne-access).

**0.0.39 (2 Oct 2026, overworld chat, built, not yet tested): the world map, step 2.** Step 1 first: `fldPlayer.fldPlayerCalc_Wm2` read offline (`docs/HANDOFF-OVERWORLD.md` "Step 1 done", `docs/class-index.md`): the world map reads the first person's pad buttons, its directions are fixed to the world (Up = north), 16 units a tick, collision radius 30, and `fieldID` below 10 is the game's own test (field 1 = the prologue's Tokyo map). Built: `FieldState.WorldMap`; Page Up / Page Down list the world map's entrances ("Yoyogi Park entrance"), people, Souls and item boxes with straight-line distance and compass direction, no height words, no reachability filter; End and Home say they are not available on the world map yet. Fix: an area with no floor no longer rebuilds the walking map and logs on every frame. New read-only tools: `tools/native/typeinfo.pl`, `statics.pl`, `fields.pl`, `consts.pl`, `annot-ti.pl`. New binding rule (CLAUDE.md): never push to GitHub unless Francesco gives the order.

**2 Oct 2026, 21:03 session (`session_2026-10-02_21-03-43.log`): 0.0.39 verified** (world map Page Up / Page Down, `docs/HANDOFF-OVERWORLD.md` "Result"). Francesco also reported two old issues, fixed in **0.0.40 (built, not yet tested)**:
- **Battle talk lines spoken twice** (Shikigami, 20:10 session: "[game line removed]", "[game line removed]", "You obtained Life Stone x 1.", each twice 0.56 s apart). The game blanks the page with its placeholder and writes it again about 0.4 s later with no message call; `DialogueReader` counted the blank as a new page. Now an identical page is spoken again only after `itfMesManager.ClearMessage`, a message request or the window closing. Checked against every archived log: the three doubles had none of these, the 20 real repeats (cylinder question, "Soul: *mumble*") each had one.
- **Continue screen silent** (title CONTINUE, one slot: the suspend data). Its prefab `continueUI(Clone)` is the same `saveUI` as the load list (`slMain.SetupUI`, read offline), so `SaveListReader` now accepts its path: "Art, Level 8, Yoyogi Park-West Plaza, NORMAL" (no file number).
- Seen and left as they are: "Exit" spoken twice at the Annex Lobby exit is two game texts (the location banner names the place "Exit", then the prompt "Exit"); "Talk" twice is the game hiding and re-showing its prompt (crossing a Soul's talk radius, or an auto-walk turn).

**0.0.41 (2 Oct 2026, built, not yet tested; 0.0.40 also still to test): the world map, step 3, a walking map and End.** Calibrated offline with the new `tools/worldgrid.pl` against the Shinjuku dumps and the 92 traced positions (`docs/HANDOFF-OVERWORLD.md` "Step 3 built"): the dungeon map had counted the vertical `Attr 0x800` region planes as walls; on the world map `WalkGrid` now takes every non-floor `Attr 0` / `0x80` polygon as a wall, and a cell is walkable when its centre or a point 6 or 12 off it clears every wall by 22 (the game keeps about 30 but squeezes through lanes at 26 to 27). Page Up filters by reachability again there; End speaks compass words, which are the arrow keys on the world map ("South-west, 12 metres, then south."), with short sidesteps folded in and no facing check. Home stays off on the world map (step 4).

**3 Oct 2026, 14:04 session (`session_2026-10-03_14-04-02.log`): 0.0.41 verified, and the Continue screen of 0.0.40.** End on the world map led Francesco to two Souls and the Yoyogi Park entrance, each ending "In reach." with the game's "Talk" / "Enter"; his verdict: directions are "solid enough already to play the overworld". The title Continue screen read "Art, Level 8, Yoyogi Park-West Plaza, NORMAL". Not exercised: the battle-talk fix (no demon talk). One miss: from the south Soul the Shibuya entrance was "not reachable, left out" (a tall door volume makes the player's height the target floor; fix planned). **Next (Francesco): auto-walk (Home) on the world map, "more essential than in dungeons" since the world map has no footsteps or other audio feedback.** Design analysed and written down, no code this session: `docs/HANDOFF-OVERWORLD.md` "Step 4 plan".

**0.0.42 (3 Oct 2026, built, not yet tested): the world map, step 4, auto-walk (Home), and the Shibuya fix.** Built to `docs/HANDOFF-OVERWORLD.md` "Step 4 plan" (details and test steps in "Step 4 built"). `FieldState` counts `fldPlayerCalc_Wm2` runs (`WmCount`), so a world-map walk is not taken for an event; `AutoWalk` presses the pads fixed to the compass there (yaw 0), ends "Arrived." as soon as the game's hit index is the target (no facing), ends on any field or area change (a random battle, 2 to 202), does not count ticks where the game ignores the pads (`NoInpPlCnt`, `fldWmWarpMode`) as still, and times out after 90 s instead of 30. Shibuya: on the world map an entrance's target floor is the ground (`Attr` 0 floor) under its volume centre, never the player's height; checked offline (`tools/worldgrid.pl`, new `FLOOR=under` option): all five Shinjuku entrances within 8 units of a reachable cell, all 133 traced positions reachable. Dungeons unchanged (the stop test now also checks the field; the first-person state and camera are read as before).

**3 Oct 2026, 14:31 session (`session_2026-10-03_14-31-09.log`, recordings `2026-10-03 14-36-58.mp4` and `14-37-33.mp4`): 0.0.42 tested.** Auto-walk itself works on the world map (Home to a Soul arrived with the game's "Talk"; an arrow key stops it; battles end it and Home carries on), and the dungeon walks of the session arrived as before. But Francesco: walks and End "fail to track walls", "navigation impossible": the routes went through buildings and cliffs (stuck against wall #20 west of the Medical Center for 10 s, then cliffs in Yoyogi). Cause: the world-map walking map of 0.0.41 tested cells, not moves; with sample points 12 off a cell centre, the cells on both sides of a thin wall were walkable (floors continue under buildings). **0.0.43 (built, not yet tested):** `WalkGrid.BlockWorldMoves` blocks every move whose centre-to-centre segment crosses a wall polygon (offline, `tools/worldgrid.pl EDGE=1`: all 190 traced positions stay reachable; Yoyogi Park's east entrance, Gaienmae Station and the item box lie behind cliffs and drop out of Page Up); `AutoWalk` re-plans after 1.5 s without progress on the world map. Details and test steps: `docs/HANDOFF-OVERWORLD.md` "0.0.43".

**3 Oct 2026, 15:13 session (`session_2026-10-03_15-13-07.log`, dumps `navdump_2026-10-03_15-15-42` and `_15-16-42`): 0.0.43 verified.** Francesco: "Navigation worked very well for every target given": Home across the world map to the Medical Center, Yoyogi Park and Shibuya without a key, 0 re-plans; Page Up 7 points, the three left out exactly as predicted offline. Three requests after entering Shibuya, built in **0.0.44 (not yet tested)**: (1) people and item boxes count as reached only when the game's interaction prompt is on screen (a demon's hit fired at 232 units, its "Talk" at 172); (2) dungeon doors leading to the world map say "Exit to overworld" (`DoorTable.ToField`); (3) Where am I on Delete (the location banner, the facing in first person, the nearest reachable point of interest); F8 Status removed (Francesco). Details: `docs/HANDOFF-OVERWORLD.md` "0.0.44".

**3 Oct 2026, 15:34 session (`session_2026-10-03_15-34-27.log`): 0.0.44 verified; overworld navigation done (Francesco).** "Exit to overworld" listed and walked to in the Annex Lobby; Delete spoke "Annex 1F. Facing east. Nearest: ...", "Overworld, Shinjuku Medical Center. Nearest: Shinjuku Medical Center entrance, here."; walks across the world map again without a key. **0.0.45 (built, not yet tested):** an exit to the overworld never says "explored" (Francesco: it is used again and again; added to the doors and things without an "explored" or "empty" tag); **F8 = battle verbosity**: the HP left after each hit or heal ("Zhen 1: 27 damage, HP 38 of 65.") is spoken only when on (`SpeakHpLeft`, off by default, saved to the config; "HP after actions on." / "off."); off: "Zhen 1: 27 damage."; a unit brought to 0 still says "0 HP". Only the lines spoken as actions happen change: F2, F3, target selection and the battle log (F5) always give HP.

**0.0.46 (3 Oct 2026, built, not yet tested): the moon phase on Delete** (overworld step 5; Francesco: at the end of Where am I, also the numbers shown in dungeons, which are the same moon display). Read offline: `evtGetAgeOfMoon16()` returns `dds3GlobalWork.DDS3_GBWK.Moon.Age` (0 to 15); the HUD shows min(Age, 16 - Age) as "n/8" (4 "HALF", 8 "FULL") wherever `fldMain.fldChkVortex()` is non-zero. Spoken last: "Moon 3/8, waxing." Details: `docs/HANDOFF-OVERWORLD.md` "Step 5 built", `docs/class-index.md` "Moon phase".
