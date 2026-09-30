# Class index

Every game, Unity or loader name the mod uses or plans to use, with where it was verified. No name goes into code unless it is listed here first.

Sources:

- **Proxy**: the Il2CppInterop proxy assembly MelonLoader generated (`MelonLoader/Il2CppAssemblies`), read with ilspycmd 8.2. Names and signatures are real; method bodies are stubs.
- **Loader**: `MelonLoader/net6/MelonLoader.dll` (0.6.1), read with ilspycmd.
- **Log**: a line in `MelonLoader/Latest.log`.
- **Repo**: a reference repository, not yet checked against this build.

Game version at time of verification: 1.0.4 (Latest.log).

## Input

| Name | Kind | Source | What we know |
| --- | --- | --- | --- |
| `Il2Cpp.InputUtil` | class | Proxy, Assembly-CSharp, 30 Sep 2026 | The game's input wrapper. Static `GetKey(InputAssign.ControllerID, InputAssign.KeyID, int assign_idx = -1)`, `GetPadKey(ControllerID, AssignCode)`, `GetOneShotKey(int kind = 0)`, `GetCurrentKey(int kind = 0)`, `GetRepeatKey(bool oneShot = true, int kind = 0)` (the last three return `ulong` bitmasks, PS2 pad style), `Init()`, `Update()`. |
| `Il2Cpp.InputAssign` | class | Proxy, Assembly-CSharp, 30 Sep 2026 | Key assignment tables. Nested enums `ControllerID`, `ControllerType`, `KeyID`, `AssignCode`; nested classes `AssignData`, `AssignkeyInfo`. |
| `Il2Cpp.InputAssign.KeyID` | enum | Proxy, Assembly-CSharp, 30 Sep 2026 | Logical keys: A, B, X, Y, R1, L1, START, UP, DOWN, LEFT, RIGHT, UP_LEFT, DOWN_LEFT, DOWN_RIGHT, UP_RIGHT, L_UP, L_DOWN, L_LEFT, L_RIGHT, R_UP, R_DOWN, R_LEFT, R_RIGHT, R3, L3, SELECT, R2, L2, MAX. |
| `Il2Cpp.SteamKeyboard` | MonoBehaviour | Proxy, Assembly-CSharp, 30 Sep 2026 | Keyboard layout detection and virtual-key mapping: `Update()`, `KeyProc(int code, int mode)`, `GetVKCodeName(...)`, `KBNoToVKCode(...)`, `VKCodeToKBNo(...)`, `GetCurrentKeyboardLayout()`. |
| `Il2Cpp.SteamPad` | class | Proxy, Assembly-CSharp, 30 Sep 2026 | Steam Input actions. Nested enums `EActionSets`, `EDigitalActionsInGameControls`, `EAnalogActionsInGameControls`. |
| `Il2Cpp.SteamInputAssign`, `Il2Cpp.SteamInputUtil` | classes | Proxy, Assembly-CSharp, 30 Sep 2026 | Steam Input assignment and helpers. Not yet inspected. |
| `Il2Cpp.dds3PadManager`, `Il2Cpp.sdf_PadManager` | classes | Proxy, Assembly-CSharp, 30 Sep 2026 | PS2-heritage pad managers. Not yet inspected. |
| `Il2Cpp.dds3ConfigKeyBoardSteam`, `Il2Cpp.dds3ConfigGamePadSteam` | classes | Proxy, Assembly-CSharp, 30 Sep 2026 | Keyboard and gamepad settings screens or data. Not yet inspected. |
| `Il2Cpp.SteamWin` | class | Proxy, Assembly-CSharp, 30 Sep 2026 | Win32 wrappers: `WndProc`, `AttachWndProc`, `SendInput`, window placement. |
| `UnityEngine.Input.GetKeyDown(KeyCode)` | static method | Proxy, UnityEngine.InputLegacyModule, 30 Sep 2026 | Present in this build (not stripped). Used by `Input/Hotkeys.cs`. |
| `UnityEngine.KeyCode` | enum | Proxy, UnityEngine.CoreModule | Key names for the config file. |

No Rewired and no Unity Input System assembly exists in this build (checked the full proxy list and scanned Assembly-CSharp and Assembly-CSharp-firstpass for their names).

## Text and messages

The game draws its UI text with TextMeshPro: 45 classes in Assembly-CSharp reference TMP types (proxy decompile, 30 Sep 2026). Only `Nme_Draw` also references legacy `UnityEngine.UI.Text`.

| Name | Kind | Source | What we know |
| --- | --- | --- | --- |
| `Il2CppTMPro.TMP_Text` | class | Proxy, Unity.TextMeshPro, 30 Sep 2026 | Base of all TMP text. Setters hooked by the probe: `set_text(string)`, `SetText(string)`, `SetText(string, bool)`. Other writers exist and are not hooked yet: `SetText(string, float...)`, `SetText(StringBuilder)`, `SetCharArray(...)`. Inherits `UnityEngine.UI.MaskableGraphic`, so builds need `UnityEngine.UI.dll`. |
| `Il2CppTMPro.TextMeshProUGUI`, `Il2CppTMPro.TextMeshPro` | classes | Proxy, Unity.TextMeshPro | Concrete TMP types (canvas UI and world-space). |
| TMP users in Assembly-CSharp | classes | Proxy decompile, 30 Sep 2026 | Includes `itfMesManager`, `campMenu`, `campUI`, `dds3TitleMain`, `dds3StartMenuDisp`, `ModeSelect`, `configMenuUI`, `dds3ConfigMainSteam`, `saveUI`, `saveFileUI`, `statusUI`, `devilInfo`, `DHeartsUI`, `autoMapUI`, `flocationUI`, `buttonguideUI`, `fclUI`, `fclShopDraw`, `fclTerminalDraw`, `fclCombineDraw`, `fclRecoverDraw`, `fclRagDraw`, `fldDraw`, `cmpDraw`, `cmpPanel`, `nbCommSelProcess`, `ScrollText`, `MemoryDisp`. Candidates for Phase 2 menu handlers. |
| `Il2Cpp.itfMesManager` | class, all static | Proxy, Assembly-CSharp, 30 Sep 2026 | The message engine ported from PS2 (`itf` = interface, `Mes` = message). Holds dialogue text, speaker, message windows and choice (select) windows. Methods probed in Phase 1 are listed in `Capture/MesProbe.cs`. Key ones: `SetMessage(GameObject obj, int line, string text)` (CallerCount 0), `itfMesMngRequestMessage(int id, int message, int page, int callmode = 0)` (3 callers), `itfMesMngGetStrImmediate(int id, int message, int page) → string`, `WindowDispOn(instanceMes_tag)`, `WindowDispOff(bool sw = false)`, `SwitchSpeaker(bool sw, bool disp = true)`, `GetSpeakerName(instanceMes_tag) → byte[]`, `SelectWindowDispOn/Off(instanceMes_tag)`, `itfMesMngRequestSelect(int id, int select)`, `ChangeSelection(int index, int onoff)` (8 callers), `SelectItemMove(select_tag psel, int direct)` (CallerCount 0), name lookups `DAT_STR_REF_DVNAME/RCNAME/SKNAME/ITNAME/DHNAME(int) → string`. Per-frame (not hooked): `CalcMesFlow`, `CalcWindow`, `CalcMessage`, `CalcSelect`. `ConvertZenkakuNumber` takes `ref string` (not hooked). |
| `Il2Cpp.instanceMes_tag`, `message_tag`, `select_tag`, `Speaker_tag`, `window_tag`, `variable_tag`, `mesflow_tag` | classes | Proxy, Assembly-CSharp | Message engine state (PS2 structs ported as classes). Fields not yet inspected. |
| `Il2Cpp.datSkillName.Get(int id, int Devilid = 0) → string` | static method | Proxy; Repo: NocturneAddNewSkills | Skill name lookup. |
| `Il2Cpp.datItemName.Get(int id) → string` | static method | Proxy; Repo: NocturneAddNewSkills | Item name lookup. |
| `Il2Cpp.datSkillHelp_msg.Get(int id) → string`, `Il2Cpp.datItemHelp_msg.Get(int id) → string` | static methods | Proxy; Repo: NocturneAddNewSkills | Skill and item help text. |
| `Il2Cpp.nbHelpProcess.nbDispText(string text1, string text2, int type, int max, uint col, bool type_skill = false)` | static method | Proxy; Repo: NocturneAddNewSkills | Battle help line. |
| `Il2Cpp.nbNegoProcess.nbDispNegoMessage(ref nbNegoProcessData_t n, int msgid, int msgno, int sel)` | static method | Proxy; Repo: MatthiewNocturne | Negotiation message. Takes a by-ref struct: not hooked (Suikoden handoff §2.6). |
| `Il2Cpp.nbMainProcess.GetBattleUI(int index = 0) → GameObject` | static method | Proxy; Repo: MatthiewNocturne (AnalyzeBosses) | Battle UI roots. Index 5 is the Analyze window, whose rows are `banalyze_skill/banalyze_skillNN/banalyze_textTM` (`TextMeshProUGUI`). TMP objects in this game end in `TM`. |
| `Il2Cpp.slMain.slHitAnyKeyNextFuncDelegate` | delegate | Proxy type list | Opening splash "press any key". Likely why any key (F8 too) restarts the opening music there. |

### Message engine: what fired in probe session 1

Source: Log, probe session 1 (30 Sep 2026, title screen to the first save in Shinjuku Medical Center, `UserData/NocturneAccess/Logs/session_2026-09-30_14-07-16.log`).

- **Fired:** `SwitchSpeaker` (276), `ClearMessage` (177), `itfMesMngRequestMessage` (167), `WindowDispOn(instanceMes_tag)` (146), `OpenCheckSpeakerWindow` (143), `ChangeSelection` (126), `GetSpeakerName` (124), `OpenMessageWindow` (122), `SetVar` and `itfMesMngSetVarImmediate` (44 each), `itfMesMngRequestSelect` and `SelectWindowDispOn` (23 each), `WindowDispOff` (13), `ForceWindowDispOff` (1).
- **Never fired:** `SetMessage` (as its CallerCount 0 suggested), `itfMesMngGetStrImmediate`, `WindowDispOn(int)`, `ClearSpeaker`, `SelectWindowDispOff`, `itfMesMngSetSelect`, `SelectItemMove`. The name and help lookups (`DAT_STR_REF_*`, `dat*.Get`, `nbDispText`) had no chance to fire: no menu with skills or items and no battle were visited.
- `itfMesMngRequestMessage(id, message, page, 1)` starts a message, but one message can span several pages that the player advances. Pages are detected from the text lines, not from this call.
- `ChangeSelection(index, onoff)`: when a choice opens, each option is called with `onoff = 0`, then the highlighted one with `onoff = 1`. Each cursor move calls the old option with 0 and the new one with 1.

## UI object paths

Source: Log, probe session 1 (30 Sep 2026). All paths are under `Canvas_UI/`, all components are `TextMeshProUGUI`. The whole game runs in one Unity scene (`xrd773`, after `Boot`), so screens are prefabs instantiated as `(Clone)` under `Canvas_UI`, and scene events cannot detect screen changes. `oubei` (欧米) marks the Western-language layout.

**Text format.** Dialogue and many labels carry TMP tags: `<material="MsgFont0">`, `<material="TMC09">` and others choose fonts; the typewriter hides characters not yet shown with `<alpha=#00>` before each one. **Every write already contains the full line**: the typewriter only moves alpha tags. An empty line holds the invisible placeholder `<alpha=#00>あああああ</alpha>`. Stripping tags gives the game's exact string.

| Screen | Path | Role |
| --- | --- | --- |
| Dialogue | `talkUI/talk01/talk_window/Text01TM`, `Text02TM`, `Text03TM` | The three lines of the current page. At the start of a page all lines are written at once with full (hidden) text; a page change first resets them to the placeholder. |
| Dialogue | `talkUI/name01/name_window/TextTM` | Speaker name ("Woman's Voice", "Yuko", "Station Staff", "Message"). Shown or hidden by `itfMesManager.SwitchSpeaker`. |
| Dialogue choice | `talkUI/talk01_choice/choice_answer_oubei01/answer01TM`, `..._oubei02/...` | Option texts ("I will.", "I don't understand.", "Yes", "No"). Cursor: `ChangeSelection(index, 1)`. |
| Title menu | `titleUI(Clone)/titlemain/titlemainmenu/stretch/titlemain_item01..09/slide/Text_TM` | LOAD GAME, NEW GAME, NEW GAME: CHRONICLE, NEW GAME: MANIAX, CONTINUE, CONFIG, DOWNLOADABLE CONTENTS, LICENSE INFORMATION, LOCALIZATION CREDITS. Cursor source not found yet. |
| Title menu | `titleUI(Clone)/titlemain/titlemain_note/Text_mainnote01TM..05TM` | Description of the highlighted item, one line per component, typewriter-revealed. |
| Title prompt | `titleUI(Clone)/titlemain/talk01_choice/choice_answer01/answer01TM`, `02` | Title-screen Yes/No choice (separate from the dialogue choice). |
| Difficulty | `titleUI(Clone)/titlemode/titlemodeselect/titlemode_item02..03/Text_mode_g`, `titlemode/titlemode_note/Text_modenote01TM` | NORMAL, HARD and the description. |
| Difficulty confirm | `titleUI(Clone)/titlemode/namechoice/talk_window/Text01TM`, `titlemode/talk01_choice/choice_answer01..02/answer01TM` | "The difficulty can be changed at any time..." with Yes / No. |
| Config | `configUI_steam(Clone)/tab/tab_button/tab_<name>/Text (TMP)` | Tabs (e.g. KEYBOARD+MOUSE). |
| Config | `configUI_steam(Clone)/cfgmenu/scrollveiw_<tab>/content/cfgmenuset_<item>/cgfmenu_tilte/TextTM` and `.../cfgmenu_note01/TextTM` | Option name and description for each option on each tab (audio, game, graphics, display, gamepad, keyboard). Current values not located yet. |
| Config keys | `.../cfgmenuset_keyboardconfig<NN>/cgfmenu_tilte/key/Image_key01/Text (TMP)` | Bound key per action; see `docs/KEYS.md`. |
| Name entry | `nameUI_oubei(Clone)/nameinput/nameannounce/stretch/TextTM`, `.../namefull/stretch/stretch_name/TextTM`, `stretch_sur/TextTM`, `nameinput_letter01..NN/TextTM` | "Enter a name.", "Given:", "Family:", the letters typed so far. |
| Name entry | `nameUI_oubei(Clone)/namelist/name_alpabet/name_alphabet_box/namelist_box0N/Text_letter0NTM`, `namemode/namelr_menu/namemode01..03/stretch_on|stretch_off/Text` | Letter grid and character sets (ABC, ÀÁÂ, #+=). Rewritten constantly; cursor source not found yet. |
| Field | `fieldUI/flocation/stretch/TextTM` | Current location name, on the overworld map and in dungeons ("Yoyogi Park", "Harajuku", "Shinjuku Medical Center", "Exit", "Shutter"). Key input for navigation. |
| Field | `fieldUI/fannounce/guidetext/TextTM` | Interaction prompt when something is in reach: "Talk", "Examine", "Open Door", "Enter", "Exit". Key input for navigation. |
| Terminal | `terminalUI/tmnlcurrent/stretch/TextTM`, `terminalUI/institutionUI/institution_menu/insmenu_window/insmenu_top/TextTM`, `insmenu_base02..03/TextTM` | Terminal location, menu items (Save, Talk, Exit). |
| Save | `saveUI(Clone)/savelist/savelist_data/Text_announceTM`, `.../savefile_mask/savefile/savefile/savefile_data/Text_nameTM`, `Text_locationTM`, `Text_difficultyTM`, `.../Text_newTM` | "Select a save location.", slot name, location, difficulty, "New File". |
| Save | `saveUI(Clone)/saveannounce/saveannounce_tex/Text_announce01TM`, `saveannounce_choice/answer01TM`, `answer02TM` | Overwrite prompt, Yes / No, "Save completed." |
| Automap | `automapUI/autm_frame_oubei/autmlocation/Text_locationTM`, `.../floor_mask/autm_floor/Text_floorTM`, `autmupdown/autmupdown_up|down/stretch_on/TextTM` | Map location, floor ("2F"), UP / DOWN. |
| Button guide | `buttonguide01/guide/buttonguide_set0N/button0N/Text1TM`, `.../guidetext/Text_buttonTM` | Key glyphs and captions ("Select", "Confirm"). Not worth speaking. |
| Magatama (preloaded) | `magUI(Clone)/magname_base/TextTM`, `magstatus/magstatus_item0N/TextTM`, `magnote/Text0NTM` | Written hidden at startup; not visited yet. |
| **Noise** | `UIManager/Text/Text01000` onward (116 components) | Hold a single space and are rewritten every frame. No content. Main cause of probe stutter; must be discarded before any work. |
| **Noise** | `BrightTextMesh/MsgFont0..5`, `BrightTextMesh/TMC00..25` | Static font warm-up meshes ("m0", "00"...). |

**Menu highlight is a font material** (Log, session of 30 Sep 2026 15:40). When the cursor moves, the game rewrites the old and new items with different `<material=...>` tags:

- Terminal menu (`insmenu_top`, `insmenu_base02..03`): highlighted item `TMC01`, others `TMC00` (Save → Talk → Exit observed).
- Title menu (`titlemain_item0N/slide/Text_TM`): highlighted item `TMC00`, others `TMC15`; the note (`titlemain_note`) is rewritten with the highlighted item's description at the same moment. A tenth item, `titlemain_item10` "EXIT", exists.

The material pair differs per screen, so each menu reader must record its own pair.

Also seen: `DisconnectCaution/saveannounce_tex/Text_announce01TM`, `Text_announce02TM` ("The input device has been disconnected." / "Please reconnect the device."); `saveUI(Clone)/saveannounce/saveannounce_tex/Text_announce01TM` also carries "Checking data...", "Load File 1?", "Load completed."; `fieldUI/flocation/stretch/TextTM` shows "Underground Facility" and "Terminal".

Not visited yet: the pause menu (`campUI`, `campMenu`), battle, shops, fusion.

## Field

| Name | Kind | Source | What we know |
| --- | --- | --- | --- |
| `Il2Cpp.fldPlayer` | class | Proxy, Assembly-CSharp, 30 Sep 2026; Repo: NocturneModernController PR #7 | Field player logic. |
| `Il2Cpp.fldPlayer.fldPlayerCalc() → int` | static method | Proxy decompile, 30 Sep 2026; Repo: NocturneModernController (patched there) | Variants on the same class: `fldPlayerCalc_Hit()`, `fldPlayerCalc_WarpRun()`, `fldPlayerCalcHasigo() → int` (ladder), `fldPlayerCalc_Nml() → int`, `fldPlayerCalc_WarpStart()`, `fldPlayerCalc_FastStart()`, `fldPlayerCalc_Nml_syukan() → int`, `fldPlayerCalcForUnity()`. |
| `Il2Cpp.fldCamera.fldCamMain() → Il2CppSystem.Object` | static method | Proxy; Repo: NocturneModernController | Field camera update. |
| `Il2Cpp.fldAutoMap` | class | Proxy, Assembly-CSharp, 30 Sep 2026 | Automap. Candidate for layout data. Not yet inspected. |

## MelonLoader 0.6.1 API used by the mod

| Name | Source | Used in |
| --- | --- | --- |
| `MelonBase.OnInitializeMelon`, `OnLateInitializeMelon`, `OnDeinitializeMelon`, `LoggerInstance` | Loader | `Core/ModMain.cs` |
| `MelonMod.OnUpdate` | Loader | `Core/ModMain.cs` |
| `MelonGameAttribute(developer, name)`: loads only when both match exactly | Loader (`IsCompatible` body) | `Core/ModMain.cs` |
| Developer `アトラス`, game name `smt3hd` | Log | `Core/ModMain.cs` |
| `MelonEnvironment.GameRootDirectory`, `UserDataDirectory`, `UserLibsDirectory`, `MelonLoaderDirectory` | Loader | `Core/Config.cs`, `Core/LogArchive.cs`, `Speech/*Backend.cs` |
| `MelonPreferences.CreateCategory`, `MelonPreferences_Category.SetFilePath(path, autoload, printmsg)`, `CreateEntry<T>`, `SaveToFile` | Loader | `Core/Config.cs` |
| `HarmonyDontPatchAllAttribute` (assembly), `MelonBase.HarmonyInstance` | Loader | `Core/ModMain.cs`, `Core/Patcher.cs` |
| `MelonMod.OnSceneWasInitialized(int buildIndex, string sceneName)` | Loader | `Core/ModMain.cs` |
| Launch options `--melonloader.maxwarnings` / `--melonloader.maxerrors` (default 100 per log) | Loader docs (`MelonLoader/Documentation/README.md`) | Past 100 warnings or errors MelonLoader stops logging them: keep warnings rare. |
| No log history: only `MelonLoader/Latest.log`, overwritten each launch | Observed 30 Sep 2026 | Reason for `Core/LogArchive.cs`. |

## Unity API used by the mod

| Name | Source | Used in |
| --- | --- | --- |
| `Resources.FindObjectsOfTypeAll<T>()` | Proxy, UnityEngine.CoreModule | `Capture/UiTreeDumper.cs` |
| `Object.GetInstanceID()`, `Object.name`, `Component.gameObject`, `Component.transform`, `Transform.parent`, `GameObject.activeInHierarchy`, `Behaviour.isActiveAndEnabled` | Proxy, UnityEngine.CoreModule | `Capture/*`, `Core/UiUtil.cs` |
| `Il2CppSystem.Object.GetIl2CppType()` | Proxy, Il2Cppmscorlib | Real runtime type of a component (the managed wrapper may only know the declared type). |

## Screen reader DLLs

| Name | Source | Notes |
| --- | --- | --- |
| `nvdaControllerClient.dll` exports: `nvdaController_testIfRunning`, `nvdaController_speakText`, `nvdaController_cancelSpeech`, `nvdaController_brailleMessage`, `nvdaController_speakSsml`, `nvdaController_getProcessId`, `nvdaController_setOnSsmlMarkReachedCallback` | PE export table of the DLL in the game folder, 30 Sep 2026 | x64 build, NVDA 2024.1+ controller API. |
| Tolk: `Tolk_Load`, `Tolk_Unload`, `Tolk_DetectScreenReader`, `Tolk_HasSpeech`, `Tolk_Speak`, `Tolk_Braille`, `Tolk_Silence` | Tolk.h (github.com/dkager/tolk) | Not installed; backend untested. |
