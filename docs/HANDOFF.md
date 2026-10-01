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

**1 Oct 2026, navigation (0.0.12 to 0.0.16):** diagnostics, first person on a first playthrough held in memory (flag 2800, never saved), the game's pad buttons as the movement input, and grid-snapped forward steps are verified in game. Blobber mode version 1 took over A / D and refused steps near walls; Francesco rejected that (keep the game's controls, snap only movement, cues never block; points of interest with auto-walk for fixed-camera rooms later). Read-only IL2CPP analysis of `GameAssembly.dll` is now a standard tool (`tools/native`). State and next steps: `docs/HANDOFF-PATHFINDING.md`; facts: `docs/NAVIGATION-DESIGN.md` sections 6 to 11. **0.0.17** (second navigation chat, verified 14:31: every snap on the grid, cues right at junctions, the Soul reachable; collision with objects such as gurneys slides the player sideways) and **0.0.18** (F3 centring key, verified 14:52; 0.0.19 fixes two edge cases). Next: pathfinding, points of interest and auto-walk in fixed-camera rooms first (`docs/PATHFINDING.md`); 0.0.20 builds that scaffold (not yet tested): blobber mode version 2 keeps every game key and only carries a released W walk on to the next square's centre; reading the first-person routine showed that S turns the player round (not back) and that W with A or D walks diagonally (section 12).

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
