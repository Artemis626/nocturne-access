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
