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

## Field

| Name | Kind | Source | What we know |
| --- | --- | --- | --- |
| `Il2Cpp.fldPlayer` | class | Proxy, Assembly-CSharp, 30 Sep 2026; Repo: NocturneModernController PR #7 | Field player logic. Not yet inspected. |
| `fldPlayerCalc` | method name | Proxy string table, 30 Sep 2026; Repo: NocturneModernController PR #7 | A method, not a class. Variants in the same table: `fldPlayerCalcForUnity`, `fldPlayerCalc_Hit`, `fldPlayerCalc_Nml`, `fldPlayerCalc_FastStart`, `fldPlayerCalcHasigo`. Owning class to confirm in Phase 1. |
| `Il2Cpp.fldAutoMap` | class | Proxy, Assembly-CSharp, 30 Sep 2026 | Automap. Candidate for layout data. Not yet inspected. |

## MelonLoader 0.6.1 API used by the mod

| Name | Source | Used in |
| --- | --- | --- |
| `MelonBase.OnInitializeMelon`, `OnLateInitializeMelon`, `OnDeinitializeMelon`, `LoggerInstance` | Loader | `Core/ModMain.cs` |
| `MelonMod.OnUpdate` | Loader | `Core/ModMain.cs` |
| `MelonGameAttribute(developer, name)`: loads only when both match exactly | Loader (`IsCompatible` body) | `Core/ModMain.cs` |
| Developer `アトラス`, game name `smt3hd` | Log | `Core/ModMain.cs` |
| `MelonEnvironment.GameRootDirectory`, `UserDataDirectory`, `UserLibsDirectory` | Loader | `Core/Config.cs`, `Speech/*Backend.cs` |
| `MelonPreferences.CreateCategory`, `MelonPreferences_Category.SetFilePath(path, autoload, printmsg)`, `CreateEntry<T>`, `SaveToFile` | Loader | `Core/Config.cs` |

## Screen reader DLLs

| Name | Source | Notes |
| --- | --- | --- |
| `nvdaControllerClient.dll` exports: `nvdaController_testIfRunning`, `nvdaController_speakText`, `nvdaController_cancelSpeech`, `nvdaController_brailleMessage`, `nvdaController_speakSsml`, `nvdaController_getProcessId`, `nvdaController_setOnSsmlMarkReachedCallback` | PE export table of the DLL in the game folder, 30 Sep 2026 | x64 build, NVDA 2024.1+ controller API. |
| Tolk: `Tolk_Load`, `Tolk_Unload`, `Tolk_DetectScreenReader`, `Tolk_HasSpeech`, `Tolk_Speak`, `Tolk_Braille`, `Tolk_Silence` | Tolk.h (github.com/dkager/tolk) | Not installed; backend untested. |
