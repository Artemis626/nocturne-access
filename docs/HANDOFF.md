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
    Core/        ModMain.cs, Config.cs, Log.cs
    Speech/      ISpeechBackend.cs, NvdaBackend.cs, TolkBackend.cs, LogOnlyBackend.cs, SpeechManager.cs
    Input/       Hotkeys.cs    (namespace NocturneAccess.Input: use the UnityInput alias for Unity's Input)
    Capture/     TmpTextProbe.cs, UiTreeDumper.cs            (Phase 1)
    Systems/     Dialogue/, Menus/, Field/, Battle/, ...     (Phase 2 onwards)
  tools/         copy-references.ps1 (fills _dlls/ from a local install)
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
- The MelonLoader log is ground truth. Every test ends with Francesco sharing the relevant part of `Latest.log`; Claude Code reads it before the next change.
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

Updated 30 Sep 2026, end of session 1.

**Phase 0: built, deployed, waiting for the in-game gate.**

- Repo scaffolded, GPL-3.0 `LICENSE`, references in `_dlls/` (75 assemblies via `tools/copy-references.ps1`).
- `NocturneAccess.dll` 0.0.1 builds with no warnings and is deployed to `smt3hd\Mods`.
- The mod says "Nocturne Access loaded" in `OnLateInitializeMelon`, and F8 speaks a status line.
- Nothing is verified in game yet.

**Next step:** Francesco runs the Phase 0 test and pastes the `Latest.log` section. If the gate passes, commit "verified in game" and start Phase 1 (reconnaissance: ilspycmd dumps, TMP capture probe, UI tree dumper, class index).

**Known issues and notes:**

- `Latest.log` shows a `MelonStartScreen` exception (`Texture2D` constructor not found on Unity 2019.4.10). Harmless: only the loader's splash screen fails. The `--melonloader.disablestartscreen` launch option removes it.
- `Latest.log` shows a `FileSystemWatcher` exception from MelonLoader's preferences. It comes from the Graphics Configurator's config at the game root, set with a bare file name. Harmless; our config uses an absolute path to avoid it.
- F8 is not yet checked against the game's own keyboard bindings.
- Tolk backend is untested (no `Tolk.dll` installed).

## Sources

Pages opened for this document: [MatthiewNocturne](https://github.com/NocturneMods/MatthiewNocturne), [NocturneModernController PR #7](https://github.com/tanatyucom/NocturneModernController/pull/7). Other facts come from search results linked in the tables above.
