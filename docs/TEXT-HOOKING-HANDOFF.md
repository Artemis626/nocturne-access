# Text Hooking Design: Handoff for Screen-Reader Mods of Unity Games

Reusable design notes for a screen-reader accessibility mod on another Unity game.
Covers **dialogue, menus and battle**. The code here is a summary, not a copy-paste port.

Sections 1 to 9 come from **Suikoden Access** (BepInEx 6, IL2CPP). Section 10 adds what
**Nocturne Access** (Shin Megami Tensei III Nocturne HD, MelonLoader 0.6.1, IL2CPP, 152
versions from 30 Sept to 4 Nov 2026) learned on top of them. Navigation in 3D dungeons and
world maps has its own document: `NAVIGATION-PLAYBOOK.md` in the Nocturne Access repository.
Last updated 5 Nov 2026.

> **Framework note:** Suikoden Access runs on **BepInEx 6 (IL2CPP)**, Nocturne Access on
> **MelonLoader**. The Harmony patterns carry over unchanged, since MelonLoader ships
> HarmonyX too. The things that differ are listed in §9.

---

## 1. Core philosophy

1. **Hook the method that pushes data to the UI, not the UI polling loop.** Use postfixes on
   `SetText`, `SetCursor`, `MoveCursor`, `OpenWindow` and similar methods. The game tells you
   when something changed, so you don't need to diff the screen every frame.
2. **Two layers:**
   - **Generic layer:** one postfix on `TMP_Text.set_text` that sees every TextMeshPro write
     and sorts it by **GameObject name** (`Txt_Command01`, `Txt_Name`, `Text_Auto`...).
   - **Specific layer:** hand-applied postfixes on the game's own UI classes (cursor moves,
     window opens) for anything where *which item is selected* matters.
3. **Decide what to speak in one place.** Postfixes collect data. A small narration layer
   decides what gets spoken and when (settle timers, label+description merging, dedupe).
4. **Fail per feature, never per mod.** A missing method costs one feature and gets logged
   by name.

---

## 2. Infrastructure

### 2.1 Tolerant PatchAll
`Harmony.PatchAll()` is all-or-nothing. One bad class (for example a bare `[HarmonyPatch]`
with no target) throws, the plugin fails to load, and **nothing** works. Patch class by class:

```csharp
foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
{
    try { harmony.CreateClassProcessor(type).Patch(); }
    catch (Exception e) { Log.Error($"Patch class {type.FullName} FAILED, skipped: {e.Message}"); }
}
```

**Rule:** a class that is patched *by hand* (`h.Patch(...)`) must **not** carry a
`[HarmonyPatch]` attribute, or the attribute scan picks it up with no target and throws.

### 2.2 Deferred, retried hooking
Game UI types often live in assemblies (or scenes) that aren't ready when the plugin loads.
- Install a persistent `MonoBehaviour` heartbeat (`DontDestroyOnLoad`, `HideAndDontSave`).
  On IL2CPP, register it first with `ClassInjector.RegisterTypeInIl2Cpp<T>()` and give it an
  `(IntPtr ptr) : base(ptr)` constructor.
- Each reader has `bool TryHook()`. The heartbeat calls the unfinished ones **about once a
  second, stopping after about 60 tries**. Retrying every frame flooded the log with tens of
  thousands of lines.
- Once every reader is hooked (or the retries run out), log a summary line with each
  feature's status.

### 2.3 Finding types across all interop assemblies
Don't hard-code a list of DLLs. That approach silently missed `UIBattleMain` because it lived
in a DLL that wasn't on the list. Scan **every** interop assembly once, build a
`name → Type` map per DLL, cache the results, and log which DLL each type came from.
Handle `ReflectionTypeLoadException` by using `e.Types` (skipping nulls).

An assembly your code never references directly may never load. If so, load it explicitly
with `Assembly.LoadFrom(<interop dir>/X.dll)`. Find that directory from the location of a
type you *do* reference.

### 2.4 Resolving methods strictly
```csharp
var m = t.GetMethod(name, Public|NonPublic|Instance|DeclaredOnly, null, argTypes, null);
if (m == null) { Log.Warn($"{t.Name}.{name} NOT DECLARED with that signature"); return 0; }
if (m.IsAbstract || !done.Add(m)) return 0;   // patch each MethodInfo once
h.Patch(m, postfix: new HarmonyMethod(typeof(Reader), postfixName));
```
- Use `DeclaredOnly` plus an exact signature. Report misses loudly, because "not found" and
  "never tried" must not look the same in the log.
- Keep a `HashSet<MethodInfo>` so two names that resolve to the same method aren't
  double-patched.
- Give each feature its own Harmony instance id (`<guid>.battle`, `<guid>.mainmenu`...).

### 2.5 Postfix signatures that survive IL2CPP
- Prefer `object __instance, object[] __args` and `Convert.ToInt32(__args[i])` /
  `Convert.ToString(__args[i])`. This avoids binding to interop types in the signature, and
  enum arguments turn into readable names through `ToString()`.
- Positional `__0`, `__1` work well for simple types (`string __0`, `int __0`).
- `MethodBase __originalMethod` lets one postfix serve several hooked methods (for example,
  telling `MoveCursorPartyWindow` and `MoveCursorMonsterWindow` apart).
- Wrap every postfix body in `try { } catch { }`. An exception in a postfix breaks the
  game's own method.

### 2.6 Hot-path safety rule
Never hook a hot-path method (one called every frame, or pathfinding/movement code) that
takes a **by-ref struct** parameter. That crashed or corrupted state. Value-parameter
methods are fine. Cursor methods that fire every frame are fine too, as long as the postfix
dedupes cheaply (§6).

### 2.7 Reflection helpers (`UiText`)
- `Member(obj, name)`: property **or** field, public or non-public, returns null on any
  failure.
- `Element(list, idx)`: uses the `Item` indexer, or enumerates if there isn't one. IL2CPP
  `List<T>` works through this.
- `Visible(obj)`: every `TMP_Text` under a `GameObject`/`Component` with
  `GetComponentsInChildren<TMP_Text>(includeInactive: false)`.
- `All(obj)`: the object's own `.text` if it has one, otherwise visible children, otherwise
  (only if nothing is visible) hidden children as well. UI prefabs often hold several
  alternative layouts at once (a filled save slot *and* an empty one). Reading hidden ones
  gives you placeholder text.

---

## 3. Speech output

- **NVDA first:** P/Invoke `nvdaControllerClient.dll` (**64-bit** for a 64-bit game):
  `nvdaController_testIfRunning() == 0`, `nvdaController_cancelSpeech()`,
  `nvdaController_speakText(LPWStr)`.
- **SAPI fallback:** `System.Speech.Synthesis.SpeechSynthesizer.SpeakAsync` /
  `SpeakAsyncCancelAll`.
- Install a `NativeLibrary.SetDllImportResolver` so the DLL is found **next to the plugin**,
  whatever the working directory is. After the first `DllNotFoundException`, stop probing.
- `Speak(text, interrupt = true)` never throws.
- Say "<Mod> loaded" at startup. Hearing it proves the whole pipeline works.

### 3.1 Narration layer (ordering matters)
Every interrupting `Speak` cancels the previous one, which caused two bugs: a label followed
by its description left only the description audible, and orientation words got clipped.
The fix:
- `Label(s)`: interrupts. Use it for the primary thing (a selected item, a new message).
- `Detail(s)`: queues behind a label if one was spoken less than 0.8 s ago, otherwise
  interrupts.
- `LabelWithDescription(label)`: holds the label for up to 0.2 s so the description line the
  game writes right after can be merged into **one utterance**: `"Potion. Restores 100 HP"`.
  `NoteDescription(desc)` flushes it immediately if a label is waiting. A description with no
  label is spoken alone once, after 0.25 s.
- `LabelAfter(delay, s)`: for messages announced right as a scene loads, which would
  otherwise be cut off.
- A `Tick()` from the heartbeat drives all the timers.

---

## 4. Dialogue

### 4.1 Hook: `TMP_Text.set_text` postfix
```csharp
[HarmonyPatch(typeof(TMP_Text), "set_text")]
static class TmpSetTextPatch {
    [HarmonyPostfix] static void Postfix(TMP_Text __instance, string __0) {
        if (__instance == null || __0 == null) return;
        var name = __instance.name;
        if (name == "Txt_Name") { Dlg.NameLabel = __instance; return; }   // speaker label
        Dlg.OnSetText(name, __0, Time.realtimeSinceStartup);
    }
}
```
Use the **GameObject name** as the router. Find the names with a one-off logging pass (log
every `name='text'` pair while you play a scene).

### 4.2 Assembling a message (the typewriter problem)
Dialogue lines are separate TMP objects (`Txt_Command01..N`), and the typewriter effect
rewrites each one many times as it grows.
- Keep an ordinal-sorted `Dictionary<lineName, currentText>`.
- On each write, ignore an unchanged value. If line 01 gets text that **doesn't start with**
  its old text, a new page has begun, so clear all lines.
- **Settle timer:** speak only when nothing has changed for **0.25 s**. Then join the lines,
  collapse whitespace and newlines, and prefix `Speaker: ` from the name label, but only if
  that label's `gameObject.activeInHierarchy` is true (a hidden label holds a stale name).
- The settle `Tick` is driven by a postfix on the message window's `Update`, so it only runs
  while dialogue exists.

### 4.3 Filters learned the hard way
- Battle damage popups reused the dialogue components. Drop bodies that are ≤5 characters
  and all digits (with an optional `!`), or you hear "Commander: 12".
- Strip TMP rich-text tags (`<color=...>`) before speaking.

### 4.4 Dialogue choices
Two approaches, depending on the game:
- **State on the window (Suikoden 2):** in the window's `Update` postfix, read
  `isChoicesSelecting`, `selectChoicesNum` and `choiceStartPosition` through reflection. The
  option text is `Lines[sel - start]`. When choosing starts, say
  `"Choose a dialogue option. <opt>"`. After that, speak only when the selection changes.
- **Event interpreter (Suikoden 1):** postfix `WindowManager.SetChoicesCursor(int)` for
  cursor moves, and the "choice confirmed" methods to end the choice. Also add a **timeout**
  (15 s without cursor activity), because an end signal can be missed.
- While a choice is active, suppress normal dialogue narration. When it ends, reset the
  line buffer so the options aren't read again as dialogue.

---

## 5. Menus

### 5.1 Pattern: gate plus cursor hooks
For each menu screen:
- **Gate:** postfix on `Open()` / `Close()` (zero-argument methods are the safest to hook) to
  track open/closed state and forget the last selection. **Speak nothing on open.** The
  first cursor announcement follows a moment later and interrupts anyway, so an "opened"
  word would just be a clipped syllable.
- **Cursor:** postfix on `SetCursor(int)` / `SetSelectCell(int)` / `SelectMenuButton(x)`.
  Read the label at that index from the window's own data (a list field of names), falling
  back to the visible `TMP_Text` children.
- **Labels arriving in bursts:** some screens call `Item.SetText(s)` once per entry in index
  order when they build. Collect them, and start a new list when more than 0.5 s passes
  between calls. Announce the game's **display string**, never the internal enum name (the
  enum `Quit` displays as "Back").

### 5.2 Description lines (generic set_text layer)
A second `TMP_Text.set_text` postfix handles the help/description line under menus:
- **Allowlist** component names (`Text_Auto`, `Text_Auto02`, `index_0`...) instead of
  guessing by shape. Explicitly skip button captions and reflection/shadow copies
  (`active_txt`, `inactive_txt`, `menu_txt`, `reflect_txt`) and the dialogue names.
- Dedupe per `GetInstanceID()` (same text written twice). Also dedupe **the same text from a
  different component** moments later, because games draw shadow copies of labels.
- Send the text to `Narration.NoteDescription`, which merges it with the pending cursor label.
- **Loading screens:** tips often reuse the description component. Use the presence of a
  distinctive sibling (`TipsText`) as the signal, say "Now loading" once, and mute
  descriptions for a few seconds.
- Typewriter fields: only let the settle timer speak the finished line. One field read a
  save's name aloud one letter at a time before this was fixed.
- Placeholder captions on **empty** slots (`Txt_Invalid`...) get written when the list
  builds, so leave them off the allowlist.

---

## 6. Battle

Hooks on the battle UI classes (the names are Suikoden's, but the *roles* are generic):

| Hook | Purpose |
|---|---|
| `UIBattleMain.MoveCursor(WindowType, int)` | Main cursor. `__args[0].ToString()` gives the window name, `__args[1]` the row. |
| `MoveCursorPartyWindow(int)` / `MoveCursorMonsterWindow(int)` | Target cursors. One postfix, told apart by `__originalMethod.Name`. |
| `UIBattleManager.CreateBattleUI` | "Battle started". Debounced over 5 s. |
| `UIBattleManager.OpenDialogWindowUI(string)` | In-battle message/confirmation box. The message is the argument. |
| `UIBattleDialog.OpenWindow` | Dialog box. Prefer the visible text under its `WindowObject`, falling back to the argument. |
| `UIBattleMain.OpenWindow(WindowType)` | Logs which window opened. Also detects "Command window opened", so the next Unit row reads as **"<Name>'s turn"**. |
| `SetCustomCursor(TargetSelectType, ...)` | Speaks "All enemies", "All allies", "Whole row" or "Self" when an action hits more than one target. Spoken only when the type changes. |

**Reading a window's rows:**
1. Keyed lookup first. The main UI has a `Dictionary<WindowType, Window>`, so index it with
   the **original enum argument** through the `Item` indexer. This is better than a
   hand-written map of field names, where two wrong names ("Monster"/"Party" instead of
   "Enemy"/"Unit") silently returned nothing.
2. Fallback: a named field map.
3. Inside the window, use `itemNameList` if it exists, otherwise the visible TMP labels.

**Details:**
- **Fold quantities:** in visible-label mode, a pure-number label belongs to the name before
  it (`"Medicine, 6"`). Otherwise the cursor lands on "6" instead of the second item.
- **Duplicate names:** three identical enemies get `"Slime 1"`, `"Slime 2"`, `"Slime 3"`
  (their position among the duplicates).
- **Per-frame cursor calls:** `MoveCursor` runs every frame while a window is open. Dedupe on
  the key `window#index#text` within 0.4 s, and log *after* the dedupe (logging first wrote
  the same line thousands of times).
  Key on the row, not just the text, so moving between two same-named enemies still gets
  announced.
- Use `LabelWithDescription` so the command and its help line come out as one utterance.

---

## 7. Logging discipline (what made this debuggable)
- Prefix every line with a feature tag: `[Dialogue]`, `[Battle]`, `[Menu]`...
- Log every hook result: patched, NOT DECLARED, failed (with the reason). Finish with a
  summary line.
- Log spoken text *after* dedupe.
- Log the first time each window's label source is used (`via m_windowListDict/itemNameList`).
- Keep a per-feature enable flag so one reader can be switched off while you investigate.

## 8. Discovery workflow for a new game
1. Dump the interop assemblies (Il2CppDumper / Cpp2IL output, or dnSpy on the interop DLLs)
   and search for `UI*Window`, `*Menu*`, `*Battle*`, `*Message*`, `SetCursor`, `SetText`,
   `OpenWindow`.
2. Temporarily log **every** `TMP_Text.set_text` as `name='text'` while you play one screen.
   This gives you the component names to route on.
3. Temporarily postfix candidate methods and log their arguments to confirm which ones fire.
4. Only then write the specific readers, and delete the probes.
5. If the game uses legacy `UnityEngine.UI.Text` instead of TMP, hook `Text.set_text` the
   same way.

## 9. BepInEx IL2CPP → MelonLoader differences
| BepInEx 6 IL2CPP | MelonLoader |
|---|---|
| `BasePlugin.Load()` | `MelonMod.OnInitializeMelon()` |
| Heartbeat MonoBehaviour + `ClassInjector.RegisterTypeInIl2Cpp` | Override `OnUpdate()` in the mod class. No injected component needed. |
| `new Harmony(id)` / manual `PatchAll` | `HarmonyInstance` is provided. MelonLoader auto-applies `[HarmonyPatch]` classes (disable with `[assembly: HarmonyDontPatchAll]` and use the tolerant loop in §2.1). |
| `ManualLogSource` | `MelonLogger` / `LoggerInstance` |
| Interop DLLs in `BepInEx/interop` | `MelonLoader/Il2CppAssemblies`. Types are usually prefixed with the `Il2Cpp` namespace (`Il2CppTMPro.TMP_Text`) under ML 0.6+. |
| Scene-ready hooks via retry timer | `OnSceneWasInitialized` can trigger `TryHook` retries too |

On a **Mono** (non-IL2CPP) game, none of the interop concerns apply: types are referenced
directly, and `__args` / reflection work as usual.

---

## 10. What Nocturne Access added (MelonLoader, 2026)

Nocturne names (classes, fields, materials) appear only as examples. In a new game, find the
equivalents; never assume the same names.

### 10.1 Rules the player set (keep them)
- **Never reconstruct text.** Speak the game's own strings: TMP writes, message-engine
  calls, game data tables. No OCR, no guessing from images.
- **Icon-only information** may use a word cut from one of the game's own lines (a status
  shown only as an icon is named from the cure item's help: "Cures Mute" gives "Mute"). A
  word the mod writes itself needs the player's consent, and is marked so in the code.
- **Never invent class, method or field names.** Each comes from the decompile, a reference
  repository or a log line, and is written into a class index (name, kind, source) before
  it is used. One wrong guessed name costs a whole session.
- **Every reply to the player uses Markdown headings** (NVDA users jump between them).
- **Keyboard first, single keys preferred** over Shift or Ctrl combinations; keep
  combinations for debug keys. Keep a key list (mod keys, keys the game uses, keys other
  tools reserve, such as a graphics configurator or Steam).
- **Content changed for accessibility is told in the README**, each change with its config
  switch to play the original. Bosses are never skipped. Prefer cues, then automation, and
  skip a part only when neither can work (a real-time chase).

### 10.2 Infrastructure on MelonLoader
- **One tolerant patcher**: every postfix is applied by hand, one at a time, each logged as
  patched or failed with the reason. A missing method costs one feature.
- **One TMP hook for everything** (`TmpRouter`): a single `TMP_Text.set_text` postfix
  classifies each component **once** by its hierarchy path (cached by instance id) and hands
  the text to the reader that owns that path. Readers never hook TMP themselves.
- **Blank writes never reach readers.** Consequence learned later: a row the game empties
  keeps its last text in the reader, so re-read the component itself before trusting a
  highlighted slot.
- **A per-frame `Tick` from `OnUpdate`** runs every reader. A reader may be ticked several
  times per game frame: time every "still for a while" signal in **milliseconds**, never in
  ticks (a three-tick wait fired every frame).
- **No allocations in per-frame paths**: no closures, no string keys built every frame, list
  rows built only when drawn.
- **Config** in `UserData/<Mod>.cfg`, every option with a comment and a safe default.
- **Log archive**: a relaunch overwrites `MelonLoader/Latest.log`, so copy every session to
  `UserData/<Mod>/Logs/session_<date>_<time>.log`. A summary script reads the latest one.

### 10.3 Speech
- **A background speech thread** with a queue: `Say(text, interrupt, force)`. Every spoken
  line is mirrored to the log as `[SPEECH]`, so a session's log shows exactly what the
  player heard and when. Most bug reports were solved from these lines alone.
- **A leader goes first:** a screen's first line waits (up to 1.5 s) while a dialogue page,
  a prompt or a confirmation is due, so the question is said before its options.
- **Held lines:** a line said while the player is pressing keys gets cut by their next key.
  `SayHeld(text, ms)` protects it from interrupts for a few seconds. Lines that come as a
  scene loads also wait until the player has control and no dialogue page is due.
- **Earcons without files:** short tones (a wall thud, a "way on" chime) synthesised in
  memory as a WAV and played with `winmm` `PlaySound` (`SND_MEMORY | SND_ASYNC`, buffer
  pinned). Low, soft and distinct: the player rejected high-pitched tones.

### 10.4 Text and menus
- **An identical rewrite is never activity.** Many games rewrite the same text every frame;
  counting it as a change keeps settle timers from ever running out.
- **Highlights are often font materials**, and they differ per screen (`TMC01` in pause
  and battle menus, `TMC00` on the title, `TMC08` on Yes/No, `TMC12` a blinking letter).
  One material can mean something else on another screen. A spec per menu (component
  paths, highlight material, opening prefix) beats one generic reader.
- **Not every cursor is a material**: some are objects or game data (save Yes/No, stat
  allocation, a dead row drawn greyed under the cursor). Read the draw function offline to
  find the cursor field, and check every branch of it (draw modes differ).
- **Texts are written while hidden** and shown later: wait for visibility. Some components
  only hide and show without a new write: watch visibility too.
- **Numbers drawn as sprites** come from game data (a counter component's value, unit
  data), never from images.
- **Prefer game data over UI rows** whenever the game has it: a battle panel lagged behind
  a summon and named the wrong unit; the unit data was right.

### 10.5 Battle
- **Read the battle's own state struct** (active unit, press turns, targets, unit HP and
  statuses) and speak its changes, rather than reading the drawn UI.
- **Check what an index indexes.** "The active unit" was an index into the party array on
  both sides, not a formation slot. The two agree until a summon reorders the entries, then
  the wrong unit is named. Every reader using it had to be fixed (party turns, enemy
  actions, half turns).
- **An action can hit many units**: a "target" field is stale for all-target actions.
  Track every unit whose HP dropped within the action's window and judge each one
  (weakness hits on several party members at once).
- **Some picks are never shown** (an all-target action runs its target process for 100 ms
  with nothing on screen): log first, then decide there is nothing to speak.
- **Before fixing a reported wrong name, find the exact line the player heard** in the
  `[SPEECH]` log. One "wrong name" was a menu redraw spoken 40 ms before a correct line.

### 10.6 Research method
- **Read the game's code offline before guessing at runtime.** IL2CPP: the interop proxy
  decompile gives names and field offsets; disassembling `GameAssembly.dll` read-only
  (never loaded, never run) shows what a function does. It settled questions that runtime
  probing had not. Never touch a DRM-protected executable.
- **The game's data files are readable offline**: asset bundles (prefabs, tables), field
  scripts, localization. Read what a thing does (a prompt, a flag gate, a door's target)
  from its script rather than from its name.
- **Probes are temporary.** A log-only probe answers one question, then goes (or is switched
  off) once the finding is written down. Three research hooks sat on per-frame calls for
  weeks; a probe for a rare bug wrote 900 lines a session.
- **Hooking a small wrapper may never fire**: hook the shared function it calls.
- **Never write regex or other backslash-heavy code through shell or perl one-liners**:
  patterns lost their `\d` and failed silently. Edit the file, then test patterns against
  real strings from the log.

### 10.7 Working with the player
- **Read the logs yourself after every run**; never ask the player to paste them.
- **When the player asks "is this an error?", answer it**, and change nothing unless it is
  one. A change made for an unasked fix had to be reverted.
- Give exact test steps and what the player should hear. Record each session's results
  (verified, not yet tested) in a handoff file, newest last, plus a short "next session"
  file saying where things stopped.
- Commit at every working state, saying what was verified; bump the version in the mod and
  the project file together, with an entry per version in the handoff.
