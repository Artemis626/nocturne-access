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

- Pause menu (`campUIBase/campUI/menu/menu_window/menu_top`, `menu_base02..06`): highlighted `TMC01`, others `TMC00`, same pair as the terminal (Log, 30 Sep 2026 15:48 session: Item → Skill → Party → Magatama → Stats → System, and in the System submenu Config → Load).

- Save/load slot list (`saveUI(Clone)/savelist/savelist_data/savefile_mask/savefile/...`): highlighted slot `TMC01`, others `TMC00`. A filled slot rewrites its three texts (`Text_nameTM`, `Text_locationTM`, `Text_difficultyTM`); an empty slot its `Text_newTM` ("No File") (Log, 30 Sep 2026 16:18 session).
- Item lists (`campUIBase/campUI/menu_itemskill/menu_item_window/menuitem_top`, `menuitem_base02..08/TextTM`): highlighted `TMC01` ("ID Card" under Key Items) (Log, 16:18 session).
- Party member pick ("Who will use a skill?"): `campUIBase/campUI/party/party_status/party_status01/Text_nameTM` turns `TMC01` (Log, 16:18 session).

The material pair differs per screen, so each menu reader must record its own pair.

**Not every cursor is a material.** The save/load Yes/No (`saveannounce_choice/answer01TM`, `answer02TM`) is plain "Yes" / "No" with no tag, and two F6 dumps taken with the cursor on different options are identical (Log, 16:18 session). Its cursor is a separate object (`saveUI._choiceCursor`) driven by `slMain` (see Save and load below).

### Pause menu (Log, 30 Sep 2026 15:48 session, F6 dumps)

| Screen | Path | Content |
| --- | --- | --- |
| Pause menu | `campUIBase/campUI/title_window/title_window_oubei/TextTM` | Screen title: "COMMAND MENU", "ITEM", "PARTY" |
| Pause menu | `campUIBase/campUI/menu/menu_window/menu_top/TextTM`, `menu_base02..06/TextTM` | Items. Top level: Item, Skill, Party, Magatama, Stats, System. System: Config, Load, Suspend, Title Screen, End Game. Item: Use, Discard, Gems, Key Items. Party: Summon, Return to stock, Part with. The same components are reused for each level. |
| Pause menu | `campUIBase/campUI/party/party_status/party_status01/Text_nameTM` | Party member name. |
| Magatama | `magUI(Clone)/magtitle_window/TextTM`, `magname_base/TextTM`, `magnote/Text01TM..`, `magstatus/magstatus_item01..05/TextTM` | "Which will you ingest?", "Marogareh", its notes ("Imparts Physical skills", "Normal resistance"), stat labels. |
| Stats | `campUIBase/statusUI(Clone)/title_window/TextTM`, `sinfo_basic/sstatusname/Text_nameTM`, `sinfo_basic/Text_raceTM_oubei`, `sskill/sskill_await2_01..06/TextTM`, `sfeature/help_mask/TextTM`, `sstatus/Text_stat01TM..05TM` | "STATS", full name, race "Fiend", skills ("Lunge", locked ones "？"), feature line, stat labels. |

**Numbers are not text.** No HP, MP, level or stat value appears in any TMP component on these screens: they are drawn as sprite digits. Under the no-reconstruction rule, numbers must be read from game data (party and demon state), not from the screen.

`System > End Game` asks "> End the game?" through the dialogue engine, with a Yes/No dialogue choice (read correctly by ChoiceReader).

Also seen: `DisconnectCaution/saveannounce_tex/Text_announce01TM`, `Text_announce02TM` ("The input device has been disconnected." / "Please reconnect the device."); `saveUI(Clone)/saveannounce/saveannounce_tex/Text_announce01TM` also carries "Checking data...", "Load File 1?", "Load completed."; `fieldUI/flocation/stretch/TextTM` shows "Underground Facility" and "Terminal".

### Pause menu sub-screens (Log, 30 Sep 2026 16:18 session, probe and F6 dumps)

| Screen | Path | Content |
| --- | --- | --- |
| Item lists | `campUIBase/campUI/title_window/title_window_oubei/TextTM` | Title changes per list: "What will you use?", "What will you discard?", "GEMS", "KEY ITEMS". |
| Item lists | `campUIBase/campUI/menu_itemskill/menu_item_window/menuitem_top/TextTM`, `menuitem_base02..08/TextTM` | Item names ("ID Card"); empty rows hold only a material tag. Highlighted `TMC01`. The pause menu's own items are hidden meanwhile, so MenuReader counts the pause menu as closed. |
| Item lists | `campUIBase/campUI/menu_itemskill/menu_note/Text01TM..03TM` | Description of the highlighted item ("An employee ID/keycard for", "Shinjuku Medical Center."). Same text as `datItemHelp_msg.Get(id)`, which fired alongside (`datItemName.Get(112)` → "ID Card"). |
| Skill | title "Who will use a skill?", `campUIBase/campUI/party/party_status/party_status01/Text_nameTM` | Party member pick; the highlighted member's name turns `TMC01`. |
| Party panel | `campUIBase/campUI/party/party_status/party_status01..04/Text_nameTM`, `Tex_summonedTM`, `stock_status01..08/...` | Party and stock names (empty while no demons). |
| Magatama | `magUI(Clone)/magtitle_window/TextTM`, `magname_base/TextTM` (`TMC19`), `magnote/Text01TM..03TM`, `magstatus/magstatus_item01..05/TextTM` | "Which will you ingest?", "Marogareh", "Imparts Physical skills", "Normal resistance", stat labels. |
| Magatama → Stats | `campUIBase/statusUI(Clone)/title_window/TextTM` = "Which will you ingest?", `sskill/sskill_obtained01..08/TextTM` | Stats screen opened from the Magatama screen; skills to obtain are empty for now. |

Not visited yet: battle, shops, fusion.

## Save and load

Source: Proxy, Assembly-CSharp (ilspycmd decompile, 30 Sep 2026), unless marked otherwise. `slMain` is the PS2-heritage save/load engine (`sl` = save/load); `saveUI` and `saveFileUI` are the Unity screens it drives.

| Name | Kind | What we know |
| --- | --- | --- |
| `Il2Cpp.slMain` | class, all static | Save/load engine. Process functions take `dds3ProcessID_t` and are run through a process table, so they show CallerCount 0; Harmony postfixes on them still fire (Repo: NocturneAddNewSkills `SaveLoadDiagnostics.cs`, MIT: `slLoadProc_closeFile` fires once per real load). |
| `slMain._saveUI` | static field, `saveUI` | The save/load screen instance. |
| `slMain.slQueryYesNoCursor` | static field, int | Cursor of the save/load Yes/No query. Index-to-option mapping to verify in game. |
| `slMain.slQueryYesNoMsg`, `slQueryYesNoKeyFlag` | static fields, int | Query message and key state. Not yet observed. |
| `slMain.slQueryYesNo(slQueryYesFuncDelegate yesFunc, slQueryNoFuncDelegate noFunc, int yesNoDefault) → Object` | static method, CallerCount 4 | Starts a Yes/No query with a default. |
| `slMain.slContinueYesNo(same parameters) → Object` | static method, CallerCount 0 | Variant, probably the title screen's continue query. |
| `slMain.slQueryYesNoProc(dds3ProcessID_t)`, `slQueryYesNoDraw() → int` | static methods, CallerCount 0 | Per-frame logic and drawing of the query (process table). |
| `slMain.gslSelect`, `gslSelTop`, `gslMode`, `slChooseSaveIndex` | static fields, int | Slot cursor, list scroll top, mode (1 during a load: Repo NocturneAddNewSkills), chosen slot. Not yet observed. |
| `slMain.slLoadProc_closeFile(dds3ProcessID_t)` | static method | Fires once per completed load (Repo: NocturneAddNewSkills, verified there). |
| `slMain.OnChoiceFileCollider(int, SteamColliders, SteamCollider, int)` | static method | Mouse hover on the Yes/No options. |
| `Il2Cpp.saveUI` | MonoBehaviour | Fields only, no methods: `_scrollRect`, `_scrollcontent`, `_saveTop` (GameObject), `_titleMsg` (TMP), `_fileList` (`saveFileUI[]`), `_announceObj` (GameObject), `_announceList` (TMP[]: `Text_announce01TM..04TM`), `_choiceBaseObj` (GameObject), `_choiceObj` (GameObject[]), `_choiceList` (TMP[]: the Yes/No texts), `_choiceCursor` (GameObject: the highlight drawn over the chosen option). |
| `Il2Cpp.saveFileUI` | MonoBehaviour, one per slot | `_fileNum`, `_playerLV`, `_brokenNum`, `_noDataNum`, `_lapNum` (`CounterCtr`), `_playTime` (`CounterCtr[]`), `_playerName`, `_terminalName`, `_difficulty`, `_noDataText` (TMP), `_lvObj`, `_endinglDel`, `_cursor` (GameObject), `_playMode`, `_endingBase`, `_anime` (Animator), `_endingOption`, `_lapObj`, `_loopStartNon` (GameObject[]), `_endingList` (Animator[]); method `SetAnime(int)`. |
| `Il2Cpp.CounterCtr` | MonoBehaviour | **The game's sprite-digit number display.** Field `value` (int) holds the number shown; `blank` (bool); `image` (Image[]), `number`, `numberSpr` (Sprite[]). `Set(int val, Color col, int colidx)` has 48 callers, `Set2` 8, `SetInstitutionColor(int val, int colidx)` 4. This is the data source for every on-screen number (HP, MP, level, play time) under the no-reconstruction rule. |

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
| `Transform.position` (`Vector3` fields `x`, `y`) | Proxy, UnityEngine.CoreModule | `Systems/Menus/SaveChoiceReader.cs` (which option the save Yes/No highlight is over) |
| `Il2CppObjectBase.Pointer`, `Il2CppReferenceArray<T>` (`Length`, indexer) | Il2CppInterop.Runtime | Object identity (`SaveListReader`: which slot owns a text) and game arrays (`saveUI._fileList`, `_choiceList`) |

## Screen reader DLLs

| Name | Source | Notes |
| --- | --- | --- |
| `nvdaControllerClient.dll` exports: `nvdaController_testIfRunning`, `nvdaController_speakText`, `nvdaController_cancelSpeech`, `nvdaController_brailleMessage`, `nvdaController_speakSsml`, `nvdaController_getProcessId`, `nvdaController_setOnSsmlMarkReachedCallback` | PE export table of the DLL in the game folder, 30 Sep 2026 | x64 build, NVDA 2024.1+ controller API. |
| Tolk: `Tolk_Load`, `Tolk_Unload`, `Tolk_DetectScreenReader`, `Tolk_HasSpeech`, `Tolk_Speak`, `Tolk_Braille`, `Tolk_Silence` | Tolk.h (github.com/dkager/tolk) | Not installed; backend untested. |
