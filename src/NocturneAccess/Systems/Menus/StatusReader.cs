using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Il2Cpp;
using Il2Cppfacility_H;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// The Stats screen and the level-up allocation (both statusUI).
    ///
    /// Text (20:07 and 20:44 sessions): name, race and the stat labels "Str".."Luc" are text; every number is
    /// a sprite display (CounterCtr) and the allocation cursor is an object: nothing is written while the
    /// player allocates points. So (docs/class-index.md "Battle", statusUI):
    /// - numbers: CounterCtr.value of the counter under statusUI.ObjLV, ObjNowHp, ObjMaxHp, ObjNowMp, ObjMaxMp,
    ///   ObjStatus[i] (one per stat) and ObjPoint (points left), the same source as the save list's numbers;
    /// - level-up mode: statusUI.lvUpBase is shown; the stat under the cursor is the label TextStatus[i] whose
    ///   row is nearest statusUI.stsCursor (vertical distance).
    /// Opening speaks a summary: "Artemis Soul, Fiend. Level 2. HP 34 of 34. MP 14 of 14. Str 3, Mag 2, ...".
    /// In level-up mode each cursor move says "Vit 4", a value change says the new value, and a change of
    /// the points left says "2 points left". The raw readings are logged once per opening, to check the
    /// mapping against the screen.
    /// </summary>
    internal static class StatusReader
    {
        private const string Tag = "[Status]";
        private const string AnchorPath = "Canvas_UI/campUIBase/statusUI(Clone)/sinfo_basic/sstatusname/Text_nameTM";
        private const long PollMs = 100;
        private const long StableMs = 150;      // name and race unchanged this long before the summary
        private const long MaxWaitMs = 600;     // speak anyway (a demon may show no race)
        private const int MaxParentDepth = 8;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static TMP_Text _anchor;
        private static statusUI _ui;
        private static long _lastPoll;
        private static bool _open;
        private static bool _levelUp;
        private static int _cursor = -1;
        private static int _cursorValue = int.MinValue;
        private static int _points = int.MinValue;
        private static int _errors;
        private static long _holdSince = -1;
        private static string _shown = "";      // "name|race" last spoken
        private static string _seen = "";       // "name|race" last read
        private static long _seenSince;
        private static long _waitSince = -1;


        /// <summary>True while the screen is shown and its summary has been spoken.</summary>
        internal static bool IsOpen => _open;

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            TmpRouter.Register("StatusReader", path => path == AnchorPath ? OnAnchor
                : TabPath.IsMatch(path) ? (component, raw) => Tabs[component.GetInstanceID()] = component
                : (TmpRouter.Handler)null);
        }

        // The Cathedral's tabs as the game writes them (0.0.54: looking them up under statusUI found nothing in the 13:37 session).
        private static readonly System.Text.RegularExpressions.Regex TabPath = new System.Text.RegularExpressions.Regex(
            @"/statusUI\(Clone\)/comstatus_parts/commode/commode_menu/commode\d\d/stretch_off/TextTM$", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly Dictionary<int, TMP_Text> Tabs = new Dictionary<int, TMP_Text>();

        private static void OnAnchor(TMP_Text component, string raw)
        {
            if (_anchor != null && _anchor.GetInstanceID() == component.GetInstanceID() && _ui != null)
                return;
            _anchor = component;
            _ui = FindStatusUI(component.transform);
            Log.Info($"{Tag} statusUI {(_ui != null ? "found" : "NOT found")} above the name text.");
        }

        private static statusUI FindStatusUI(Transform t)
        {
            for (int depth = 0; t != null && depth < MaxParentDepth; depth++, t = t.parent)
            {
                statusUI ui = t.GetComponent<statusUI>();
                if (ui != null)
                    return ui;
            }
            return null;
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            if (_ui == null)
                return;
            long now = Clock.ElapsedMilliseconds;
            if (now - _lastPoll < PollMs)
                return;
            _lastPoll = now;

            try
            {
                bool visible = UiUtil.IsVisible(_anchor);
                if (!visible)
                {
                    if (_open)
                        Log.Info($"{Tag} Closed.");
                    _open = false;
                    _levelUp = false;
                    _shown = _seen = "";
                    _skillRow = -1;
                    _skillName = "";
                    _skillPending = false;
                    _waitSince = -1;
                    _inheritOpen = false;
                    return;
                }

                bool levelUp = UiUtil.IsVisible(_ui.lvUpBase);

                // The race is written after the name (22:05 session: "Fiend" missing from the summary), and
                // switching character (L/R) rewrites both: wait until they have been stable a moment.
                // The Cathedral's tab highlight is written a frame after the name (21:48:12, 5 Oct 2026: RESULT drawn TMC01 2 ms
                // after the summary, which had no tab): it is waited for too, as part of what must be stable (0.0.55).
                string race = Race();
                string tab = HighlightedTab(out bool tabsShown);
                string seen = Text(_ui.nameText) + "|" + race + "|" + tab;
                if (seen != _seen)
                {
                    _seen = seen;
                    _seenSince = now;
                }
                bool newCharacter = _open && seen != _shown && !_levelUp;
                if (!_open || newCharacter)
                {
                    if (_waitSince < 0)
                        _waitSince = now;
                    bool raceShown = race.Length > 0;
                    bool stable = now - _seenSince >= StableMs && raceShown && !(tabsShown && tab.Length == 0);
                    if (!stable && now - _waitSince < MaxWaitMs)
                        return;
                    // At a level-up the screen opens just before "> You leveled up!": that page goes first.
                    if (!_open && SpeechOrder.Hold("Stats", ref _holdSince, now))
                        return;
                    _holdSince = -1;
                    _waitSince = -1;
                    _shown = seen;
                    if (!_open)
                    {
                        _open = true;
                        _levelUp = levelUp;
                        _cursor = CursorRow();   // the starting row is in the summary; only moves are spoken
                        _cursorValue = _cursor >= 0 ? StatValue(_cursor) ?? int.MinValue : int.MinValue;
                        _points = Number(_ui.ObjPoint) ?? int.MinValue;
                    }
                    LogRaw();
                    Say(Summary(), interrupt: newCharacter);
                    return;
                }
                else if (levelUp != _levelUp)
                {
                    _levelUp = levelUp;
                    _cursor = CursorRow();
                    _cursorValue = _cursor >= 0 ? StatValue(_cursor) ?? int.MinValue : int.MinValue;
                    Log.Info($"{Tag} Level-up mode {(levelUp ? "on" : "off")}.");
                }

                if (_levelUp)
                    PollLevelUp();
                if (!PollInherit(now))
                    PollSkillCursor(now);
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"{Tag} {ex.Message}");
            }
        }

        private static void PollLevelUp()
        {
            int row = CursorRow();
            if (row < 0)
                return;
            int value = StatValue(row) ?? int.MinValue;
            int points = Number(_ui.ObjPoint) ?? int.MinValue;

            var parts = new List<string>();
            if (row != _cursor)
            {
                _cursor = row;
                _cursorValue = value;
                parts.Add(StatText(row, value));
            }
            else if (value != _cursorValue)
            {
                _cursorValue = value;
                parts.Add(StatText(row, value));
            }
            if (points != _points && points != int.MinValue)
            {
                _points = points;
                parts.Add(points == 1 ? "1 point left" : $"{points} points left");
            }
            if (parts.Count > 0)
                Say(string.Join(". ", parts), interrupt: true);
        }

        /// <summary>
        /// The skill cursor (0.0.71; Francesco, 12 Oct 2026: choosing the skill to give up at a level-up, "Skill slots are full.
        /// Please select a skill to discard.", was silent). UI dump 15:02:35: the rows are obtainedText (sskill_obtained01..08),
        /// the highlighted one drawn TMC21 ("Plead"), the others TMC00; its description in skillHelp (sfeature/skillhelp_mask/
        /// TextTM_full, "Ask for Macca and items."). statusUI.skillCurObj holds one cursor object per row. A move is spoken
        /// once the description has been rewritten (SkillSettleMs), as "Plead. Ask for Macca and items."; the first row only
        /// when a description is shown, so the Stats summary is not followed by a skill each time it opens.
        /// </summary>
        private static void PollSkillCursor(long now)
        {
            int row = SkillCursorRow(out string name);
            if (row != _skillRow || name != _skillName)
            {
                bool first = _skillRow < 0;
                _skillRow = row;
                _skillName = name;
                _skillAt = now;
                _skillPending = row >= 0 && name.Length > 0;
                _skillFirst = first;
                _skillQueue = first;   // the first row waits behind the game's line ("Please select a skill to discard.")
                if (row >= 0 && first)
                    Log.Info($"{Tag} Skill cursor on row {row} ({UiUtil.Escape(name)}); cursor objects {SkillCursorStates()}; help {UiUtil.Escape(Text(_ui.skillHelp))}.");
                return;
            }
            if (!_skillPending || now - _skillAt < SkillSettleMs)
                return;
            string help = Text(_ui.skillHelp);
            // The first row is said only once its description shows, so the Stats summary is not followed by a skill each time
            // it opens; the description comes a moment after the cursor (13 Oct 2026, 14:25:41 and 14:43:01: the new skill at a
            // level-up, row 8, was dropped with "help" still empty), so it is waited for (0.0.76).
            if (_skillFirst && help.Length == 0)
            {
                if (now - _skillAt > FirstHelpWaitMs)
                    _skillPending = false;
                return;
            }
            _skillPending = false;
            Say(help.Length > 0 ? $"{name}. {help}" : name, interrupt: !_skillQueue);
        }

        /// <summary>
        /// The Cathedral's manual skill inheritance (0.0.77; Francesco, 14 Oct 2026: "Select Skills" on the fusion result was
        /// silent). F6 dumps 13:36:22 and 13:36:27: the question in skill_select_parts/commode/TextTM ("Which skill will you
        /// inherit?"), five list rows skill_select/menu_skill/menu_skill_window/menuskill_top and menuskill_base02..05 (TextTM,
        /// the highlighted one TMC01, the window scrolls), the skill's description in skill_select/menu_skill/menu_note/
        /// Text01TM..03TM, the skills chosen so far in the learned rows drawn TMC21 (Tetraja, Diarama). From the game
        /// (GameAssembly.dll, read offline): fclCombineInit.CMB_GBWK.mSkillSelectCur is the window row (0 to 4; 5 and 6 the
        /// scroll arrows, fclCombineUpdate.OnSkillSelectCallback), mSkillSelectPage the scroll, fclCombineCalcCore.
        /// gKeisyoListAllBuf.SkillCnt the list's length; mSkillSelectCount of mSkillSelectMax skills chosen (a choice adds
        /// one, cancel takes the last back; the screen returns when they are equal: cmbUpdateBirthDevilSelectSkill).
        /// Speaks "Which skill will you inherit? Skills left to inherit: 2. Agilao, 1 of 12. Medium Fire damage to one foe.",
        /// each move "Mahama, 2 of 12. ...", each change of the chosen skills "Inherited: Tetraja. Skills left to inherit: 1.",
        /// and back on the result "Skills: ..." with the new ones. Returns true while the list is shown.
        /// </summary>
        private static bool PollInherit(long now)
        {
            Transform root = _ui.transform;
            TMP_Text question = Component(root, "skill_select_parts/commode/TextTM");
            bool shown = question != null && UiUtil.IsVisible(question) && UiUtil.IsVisible(_ui.menu_skill);
            if (!shown)
            {
                if (_inheritOpen)
                {
                    _inheritOpen = false;
                    Log.Info($"{Tag} Inheritance list closed.");
                    List<string> learned = Visible(_ui.obtainedText, out _);
                    if (learned.Count > 0)
                        Say("Skills: " + string.Join(", ", learned) + ".", interrupt: false);
                }
                return false;
            }

            string name = InheritRow(root, out int windowRow);
            string chosen = string.Join(", ", ChosenSkills());
            int left = InheritLeft();
            if (!_inheritOpen)
            {
                _inheritOpen = true;
                _inheritName = name;
                _inheritAt = now;
                _inheritPending = true;
                _inheritFirst = true;
                _inheritChosen = chosen;
                Log.Info($"{Tag} Inheritance list opened: {InheritRaw()}; row {windowRow} \"{UiUtil.Escape(name)}\"; chosen [{chosen}].");
                return true;
            }
            if (chosen != _inheritChosen)
            {
                _inheritQueueNext = true;
                _inheritChosen = chosen;
                Log.Info($"{Tag} Inheritance chosen [{chosen}]: {InheritRaw()}.");
                var parts = new List<string> { chosen.Length > 0 ? $"Inherited: {chosen}" : "Inherited: none" };
                if (left >= 0)
                    parts.Add($"Skills left to inherit: {left}");
                Say(Sentences(parts), interrupt: true);
            }
            if (name != _inheritName)
            {
                _inheritName = name;
                _inheritAt = now;
                _inheritPending = name.Length > 0;
                return true;
            }
            if (!_inheritPending || now - _inheritAt < SkillSettleMs)
                return true;
            _inheritPending = false;

            var said = new List<string>();
            if (_inheritFirst)
            {
                said.Add(TextUtil.Clean(question.text));
                if (left >= 0)
                    said.Add($"Skills left to inherit: {left}");
            }
            (int position, int count) = InheritPosition(windowRow);
            said.Add(position > 0 ? $"{name}, {position} of {count}" : name);
            string help = InheritHelp(root);
            if (help.Length > 0)
                said.Add(help);
            Say(Sentences(said), interrupt: !_inheritFirst && !_inheritQueueNext);
            _inheritFirst = _inheritQueueNext = false;
            return true;
        }

        /// <summary>"Which skill will you inherit? Agilao, 1 of 12. Medium Fire damage to one foe.": a full stop only where none is.</summary>
        private static string Sentences(IEnumerable<string> parts)
            => string.Join(" ", parts.Select(p => p.Trim()).Where(p => p.Length > 0)
                .Select(p => p.EndsWith(".") || p.EndsWith("?") || p.EndsWith("!") ? p : p + "."));

        private static bool _inheritOpen;
        private static bool _inheritPending;
        private static bool _inheritFirst;
        private static bool _inheritQueueNext;   // the next move queues behind "Inherited: ..."
        private static string _inheritName = "";
        private static string _inheritChosen = "";
        private static long _inheritAt;

        private static readonly string[] InheritRows = { "menuskill_top", "menuskill_base02", "menuskill_base03", "menuskill_base04", "menuskill_base05" };

        /// <summary>The list row drawn TMC01 and its window row (0 to 4), or "" and -1.</summary>
        private static string InheritRow(Transform root, out int windowRow)
        {
            windowRow = -1;
            for (int i = 0; i < InheritRows.Length; i++)
            {
                TMP_Text row = Component(root, $"skill_select/menu_skill/menu_skill_window/{InheritRows[i]}/TextTM");
                if (row == null || !UiUtil.IsVisible(row) || !(row.text ?? "").Contains("<material=\"TMC01\">"))
                    continue;
                windowRow = i;
                return TextUtil.Clean(row.text);
            }
            return "";
        }

        /// <summary>The description lines joined: "Medium Fire damage to one foe."</summary>
        private static string InheritHelp(Transform root)
        {
            var lines = new List<string>();
            for (int i = 1; i <= 3; i++)
            {
                TMP_Text line = Component(root, $"skill_select/menu_skill/menu_note/Text{i:D2}TM");
                string text = line != null && UiUtil.IsVisible(line) ? TextUtil.Clean(line.text) : "";
                if (text.Length > 0)
                    lines.Add(text);
            }
            return string.Join(" ", lines);
        }

        /// <summary>The learned rows drawn TMC21 while the list is shown: the skills chosen so far.</summary>
        private static List<string> ChosenSkills()
        {
            var list = new List<string>();
            Il2CppReferenceArray<TextMeshProUGUI> rows = _ui.obtainedText;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                if (UiUtil.IsVisible(rows[i]) && (rows[i].text ?? "").Contains("<material=\"TMC21\">"))
                    list.Add(Text(rows[i]));
            }
            return list;
        }

        /// <summary>mSkillSelectMax - mSkillSelectCount, or -1 when unreadable.</summary>
        private static int InheritLeft()
        {
            try
            {
                cmbGlobalWork_t gb = fclCombineInit.CMB_GBWK;
                if (gb == null || gb.mSkillSelectMax <= 0)
                    return -1;
                return Math.Max(0, gb.mSkillSelectMax - gb.mSkillSelectCount);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>The entry and the list's length from the game's cursor; (0, 0) when they do not fit the row on screen.</summary>
        private static (int, int) InheritPosition(int windowRow)
        {
            try
            {
                cmbGlobalWork_t gb = fclCombineInit.CMB_GBWK;
                int count = fclCombineCalcCore.gKeisyoListAllBuf?.SkillCnt ?? 0;
                if (gb == null || count <= 0 || windowRow < 0 || gb.mSkillSelectCur != windowRow)
                    return (0, 0);
                int entry = gb.mSkillSelectPage + gb.mSkillSelectCur + 1;
                return entry >= 1 && entry <= count ? (entry, count) : (0, 0);
            }
            catch
            {
                return (0, 0);
            }
        }

        private static string InheritRaw()
        {
            try
            {
                cmbGlobalWork_t gb = fclCombineInit.CMB_GBWK;
                int count = fclCombineCalcCore.gKeisyoListAllBuf?.SkillCnt ?? -1;
                return gb == null ? "CMB_GBWK null"
                    : $"Cur {gb.mSkillSelectCur} Page {gb.mSkillSelectPage} Page1 {gb.mSkillSelectPage1} Org {gb.mSkillSelectOrg} Count {gb.mSkillSelectCount} Max {gb.mSkillSelectMax} RandomSkillSw {gb.RandomSkillSw}, list {count}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static TMP_Text Component(Transform root, string path)
        {
            Transform t = root.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private const long SkillSettleMs = 120;
        private static int _skillRow = -1;
        private static string _skillName = "";
        private static long _skillAt;
        private static bool _skillPending;
        private static bool _skillQueue;
        private static bool _skillFirst;
        private const long FirstHelpWaitMs = 3000;

        /// <summary>The highlighted skill row and its name: the shown statusUI.skillCurObj, and the row drawn TMC21 (the
        /// highlight) when there is one; -1 with no cursor shown.</summary>
        private static int SkillCursorRow(out string name)
        {
            name = "";
            Il2CppReferenceArray<GameObject> cursors = _ui.skillCurObj;
            int row = -1;
            for (int i = 0; cursors != null && i < cursors.Length; i++)
            {
                if (cursors[i] != null && cursors[i].activeInHierarchy)
                {
                    row = i;
                    break;
                }
            }
            if (row < 0)
                return -1;
            Il2CppReferenceArray<TextMeshProUGUI> rows = _ui.obtainedText;
            // Past the learned skills: the new skill (0.0.75). F6 dump 13 Oct 2026, 13:54:30, slots full at a level-up: the
            // cursor on row 8 (cursor objects 000000001), the new skill in sskill/sskill_await2_01/TextTM ("Heat Wave", TMC21,
            // the others "？"), its help in skillHelp; the reader looked only at the eight learned rows and said nothing there.
            if (rows != null && row >= rows.Length)
            {
                Transform newRow = _ui.transform.Find($"sskill/sskill_await2_{row - rows.Length + 1:D2}/TextTM");
                name = UiUtil.TextOf(newRow);
                if (name == "？" || name == "?")
                    name = "";
                return row;
            }
            // The row under the cursor object first: on the fusion result the learned rows drawn TMC21 are the inherited skills,
            // not the cursor (F6 dump 14 Oct 2026, 13:36:22: Tetraja and Diarama both TMC21), and its "Current Skills" view moves
            // a cursor over them (fclCombineUpdate.cmbUpdateBirthDevilConfSkill, CMB_GBWK.mSkillConfCur 0 to 7) (0.0.77).
            if (rows != null && row < rows.Length && Text(rows[row]).Length > 0)
            {
                name = Text(rows[row]);
                return row;
            }
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                if (UiUtil.IsVisible(rows[i]) && (rows[i].text ?? "").Contains("<material=\"TMC21\">"))
                {
                    name = Text(rows[i]);
                    return row;
                }
            }
            return row;
        }

        private static string SkillCursorStates()
        {
            Il2CppReferenceArray<GameObject> cursors = _ui.skillCurObj;
            if (cursors == null)
                return "none";
            var s = new System.Text.StringBuilder();
            for (int i = 0; i < cursors.Length; i++)
                s.Append(cursors[i] != null && cursors[i].activeInHierarchy ? '1' : '0');
            return s.ToString();
        }

        private static string StatText(int row, int value)
        {
            string label = Label(row);
            return value == int.MinValue ? label : $"{label} {value}";
        }

        /// <summary>The stat row nearest the cursor object, by vertical distance to the labels.</summary>
        private static int CursorRow()
        {
            GameObject cursor = _ui.stsCursor;
            Il2CppReferenceArray<TextMeshProUGUI> labels = _ui.TextStatus;
            if (cursor == null || labels == null || !cursor.activeInHierarchy)
                return -1;
            float y = cursor.transform.position.y;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < labels.Length; i++)
            {
                if (!UiUtil.IsVisible(labels[i]))
                    continue;
                float d = Math.Abs(labels[i].transform.position.y - y);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>"Artemis Soul, Fiend. Level 2. HP 34 of 34. MP 14 of 14. Str 3, Mag 2, Vit 3, Agi 3, Luc 3. Skills: ... 1 point left"</summary>
        private static string Summary()
        {
            var parts = new List<string>();
            string tab = FusionTab();
            if (tab.Length > 0)
                parts.Add(tab);
            string name = Text(_ui.nameText), race = Race();
            parts.Add(race.Length > 0 ? $"{name}, {race}" : name);
            int? level = Number(_ui.ObjLV);
            if (level.HasValue)
                parts.Add($"Level {level.Value}");
            AddPair(parts, "HP", Number(_ui.ObjNowHp), Number(_ui.ObjMaxHp));
            AddPair(parts, "MP", Number(_ui.ObjNowMp), Number(_ui.ObjMaxMp));

            var stats = new List<string>();
            Il2CppReferenceArray<TextMeshProUGUI> labels = _ui.TextStatus;
            for (int i = 0; labels != null && i < labels.Length; i++)
            {
                if (UiUtil.IsVisible(labels[i]))
                    stats.Add(StatText(i, StatValue(i) ?? int.MinValue));
            }
            if (stats.Count > 0)
                parts.Add(string.Join(", ", stats));
            AddSkills(parts);
            if (_levelUp && _points != int.MinValue)
                parts.Add(_points == 1 ? "1 point left" : $"{_points} points left");
            return string.Join(". ", parts) + ".";
        }

        /// <summary>
        /// "Skills: Dia, Zio, Seduce. Skills to learn: Rakunda, and 2 hidden. Str: Elec." (12:03 session, 2 Oct: the
        /// screen shows the learned skills in obtainedText (sskill_obtained01..08), the ones learned at later levels in
        /// awaitText (sskill_await2_01..), "？" where the game keeps one hidden, and the affinities in aisyoText
        /// (sfeature/help_mask, "Str: Elec").
        /// </summary>
        private static void AddSkills(List<string> parts)
        {
            List<string> learned = Visible(_ui.obtainedText, out _);
            if (learned.Count > 0)
                parts.Add("Skills: " + string.Join(", ", learned));
            List<string> coming = Visible(_ui.awaitText, out int hidden);
            if (hidden > 0)
                coming.Add(coming.Count == 0 ? $"{hidden} hidden" : $"and {hidden} hidden");
            if (coming.Count > 0)
                parts.Add("Skills to learn: " + string.Join(", ", coming));
            // "Drn: Fire • Null: Dark • Str: Phys": the bullet is read aloud by NVDA, so each group is its own sentence.
            string affinity = Text(_ui.aisyoText);
            foreach (string group in affinity.Split('•'))
            {
                string g = group.Trim().TrimEnd('.');
                if (g.Length > 0)
                    parts.Add(g);
            }
        }

        /// <summary>
        /// In the Cathedral of Shadows the screen has tabs (21:49 and 21:54 sessions, 3 Oct 2026): comstatus_parts/commode/
        /// commode_menu/commode01..03, "DEMON 1", "DEMON 2", "RESULT", switched with Q / E; the tab shown has its
        /// stretch_off/TextTM visible and drawn TMC01. "" elsewhere.
        /// </summary>
        private static string FusionTab()
        {
            string tab = HighlightedTab(out bool _, out List<string> seen);
            if (tab.Length > 0)
                return tab;
            if (seen.Count > 0 && !_tabLogged)
            {
                _tabLogged = true;
                Log.Info($"{Tag} Fusion tabs shown, none highlighted: {string.Join(" | ", seen.Select(UiUtil.Escape))}");
            }
            return "";
        }

        private static bool _tabLogged;

        private static string HighlightedTab(out bool shown) => HighlightedTab(out shown, out _);

        /// <summary>The visible tab drawn TMC01, or ""; shown: some tab is visible (the Cathedral's stats screen).</summary>
        private static string HighlightedTab(out bool shown, out List<string> seen)
        {
            seen = new List<string>();
            shown = false;
            string found = "";
            List<int> dead = null;
            foreach (KeyValuePair<int, TMP_Text> kv in Tabs)
            {
                TMP_Text text = kv.Value;
                string raw;
                try
                {
                    if (text == null)
                    {
                        (dead ??= new List<int>()).Add(kv.Key);   // a destroyed copy (each Cathedral visit makes new ones)
                        continue;
                    }
                    if (!UiUtil.IsVisible(text))
                        continue;
                    raw = text.text ?? "";
                }
                catch
                {
                    (dead ??= new List<int>()).Add(kv.Key);
                    continue;
                }
                shown = true;
                seen.Add(raw);
                if (found.Length == 0 && raw.Contains("<material=\"TMC01\">"))
                    found = TextUtil.Clean(raw);
            }
            if (dead != null)
                foreach (int id in dead)
                    Tabs.Remove(id);
            return found;
        }

        /// <summary>The visible, non-empty texts in order; "？" (a skill the game keeps hidden) only counted.</summary>
        private static List<string> Visible(Il2CppReferenceArray<TextMeshProUGUI> texts, out int hidden)
        {
            var list = new List<string>();
            hidden = 0;
            for (int i = 0; texts != null && i < texts.Length; i++)
            {
                string text = Text(texts[i]);
                if (text == "？" || text == "?")
                    hidden++;
                else if (text.Length > 0)
                    list.Add(text);
            }
            return list;
        }

        /// <summary>
        /// The race: raceText_e is the Western version's text (Text_raceTM_oubei, "Fiend"); raceText, read in
        /// the 22:05 session, gave nothing. Both fields: proxy statusUI.
        /// </summary>
        private static string Race()
        {
            string race = Text(_ui.raceText_e);
            return race.Length > 0 ? race : Text(_ui.raceText);
        }

        private static void AddPair(List<string> parts, string label, int? now, int? max)
        {
            if (now.HasValue && max.HasValue)
                parts.Add($"{label} {now.Value} of {max.Value}");
            else if (now.HasValue)
                parts.Add($"{label} {now.Value}");
        }

        private static string Label(int row)
        {
            Il2CppReferenceArray<TextMeshProUGUI> labels = _ui.TextStatus;
            return labels != null && row < labels.Length ? Text(labels[row]) : "";
        }

        private static int? StatValue(int row)
        {
            Il2CppReferenceArray<GameObject> objects = _ui.ObjStatus;
            return objects != null && row < objects.Length ? Number(objects[row]) : null;
        }

        /// <summary>The value of the first visible CounterCtr on or under the object.</summary>
        private static int? Number(GameObject obj)
        {
            if (obj == null || !obj.activeInHierarchy)
                return null;
            CounterCtr c = obj.GetComponentInChildren<CounterCtr>();
            return c != null ? c.value : (int?)null;
        }

        /// <summary>Logs every counter under each object once per opening, to check the mapping against the screen.</summary>
        private static void LogRaw()
        {
            var parts = new List<string>
            {
                "LV " + Counters(_ui.ObjLV),
                "HP " + Counters(_ui.ObjNowHp) + "/" + Counters(_ui.ObjMaxHp),
                "MP " + Counters(_ui.ObjNowMp) + "/" + Counters(_ui.ObjMaxMp),
                "points " + Counters(_ui.ObjPoint),
                "bonus " + Counters(_ui.ObjBonus),
                $"levelup {UiUtil.IsVisible(_ui.lvUpBase)}",
            };
            Il2CppReferenceArray<GameObject> objects = _ui.ObjStatus;
            for (int i = 0; objects != null && i < objects.Length; i++)
                parts.Add($"{Label(i)} {Counters(objects[i])}");
            Log.Info($"{Tag} Opened: {string.Join("; ", parts)}; cursor row {CursorRow()}.");
        }

        private static string Counters(GameObject obj)
        {
            if (obj == null)
                return "none";
            var values = new List<string>();
            foreach (CounterCtr c in obj.GetComponentsInChildren<CounterCtr>(true))
                values.Add(c.value.ToString(CultureInfo.InvariantCulture) + (c.gameObject.activeInHierarchy ? "" : " hidden"));
            return values.Count > 0 ? "[" + string.Join(", ", values) + "]" : "[no counter]";
        }

        private static string Text(TMP_Text component) => UiUtil.IsVisible(component) ? TextUtil.Clean(component.text) : "";

        private static void Say(string text, bool interrupt)
        {
            if (text.Length == 0)
                return;
            RepeatBuffer.Record(text);
            SpeechManager.Say(text, interrupt: interrupt, force: true);
        }
    }
}
