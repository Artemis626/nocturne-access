using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2Cppcamp_H;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// The Cathedral of Shadows' demon tables (0.0.50): "Select the 1st demon" and "Select the 2nd demon".
    /// "Select the 2nd demon. Jack Frost, Fairy, level 7, 2 of 3. Result: Aeros, Element, level 9."
    ///
    /// Sources (docs/class-index.md "Cathedral of Shadows"):
    /// - fclCombineDraw.cmbDrawStockDevilInfo(SelPos, SelCol, pCursorInfo, Type) draws the whole table every frame with
    ///   its cursor (read offline: it calls cmbDrawDevilInfo for each row and, in the second table, the result's
    ///   cmbDrawDevilInfo and cmbDrawCombineResultIcon). The entry under the cursor is CursorPos.Index + Shift and the row
    ///   is Shift, as in the camp lists (CampListReader). The cursor is needed: the demon already chosen stays in the
    ///   second table drawn TMC02 with "CANNOT FUSE", so the highlight material cannot follow the cursor there.
    /// - Row texts (21:49 and 21:54 sessions, 3 Oct 2026): comtable01_rowNN/Text_nameTM, Text_raceTM; comtable02_rowNN/
    ///   Text_name01TM, Text_race01TM (the second demon), Text_name02TM, Text_race02TM (the result, "CANNOT FUSE").
    /// - Levels are sprite digits: the CounterCtr under the row (cmbDrawDevilInfo sets it with
    ///   datCalc.datGetLevelForDraw); in the second table lvnum01 is the demon's and lvnum02 the result's (object names
    ///   from the game's string literals).
    /// - The result icon: cmbDrawDevilIcon / cmbDrawCombineResultIcon switch on one child of the row's icon object, named
    ///   from fclCombineDraw.cmbIconName (comtable_oblique, _no, _ok, _rdown, _rup, _pwup, _majin; read offline). The legend's
    ///   texts stayed Japanese placeholders (13:37 session, 5 Oct 2026), so from 0.0.54 the words are the game's own English
    ///   texts COMBINE_L0030 to L0034 ("OK", "N/A", "Rank Up", "Rank Down", "Power Up", Localize.GetLocalizeText), matched to
    ///   the sprite names. The first table's rows hold one icon per demon of the list (a fusion grid): see Partners.
    /// </summary>
    internal static class FusionReader
    {
        private const string Tag = "[Fusion]";
        private const string RootName = "combUI_oubei";
        private const long DrawnWithinMs = 200;
        private const long OpenDelayMs = 150;
        private const long SettleMs = 80;

        private static readonly Regex LegendIcon = new Regex(@"^comguide_icon(?<n>\d\d)$", RegexOptions.Compiled);
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private static cmpCursorInfo_t _cursor;
        private static byte _type;
        private static long _drawnAt = long.MinValue / 2;
        private static Transform _root;

        private static bool _open;
        private static long _openedAt;
        private static int _table;            // 1 or 2, the table last announced
        private static int _key = -1;         // table * 1000 + entry, last announced
        private static int _pendingKey = -1;
        private static long _pendingSince;
        private static bool _covered;          // the Stats screen was over the table
        private static long _holdSince = -1;
        private static bool _vocabularyLogged;
        private static readonly HashSet<int> RowsLogged = new HashSet<int>();
        private static int _errors;

        internal static string LastUtterance { get; private set; } = "";
        internal static long LastUtteranceAt { get; private set; } = long.MinValue;   // Environment.TickCount64

        /// <summary>True while a demon table is drawn.</summary>
        internal static bool IsOpen => Clock.ElapsedMilliseconds - _drawnAt <= DrawnWithinMs;

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            TmpRouter.Register("FusionReader", Classify);
            Patcher.Postfix("Fusion tables", typeof(fclCombineDraw), nameof(fclCombineDraw.cmbDrawStockDevilInfo),
                new[] { typeof(sbyte), typeof(uint), typeof(cmpCursorInfo_t), typeof(byte) },
                AccessTools.Method(typeof(FusionReader), nameof(AfterStockDevilInfo)));
            Patcher.Postfix("Fusion tables", typeof(fclCombineDraw), nameof(fclCombineDraw.cmbDrawCombineResultIcon),
                new[] { typeof(GameObject), typeof(int), typeof(sbyte) },
                AccessTools.Method(typeof(FusionReader), nameof(AfterResultIcon)));
        }

        /// <summary>Only finds the screen's root: the texts themselves are read when the cursor settles.</summary>
        private static TmpRouter.Handler Classify(string path)
        {
            // The title (rewritten every frame while a table is up) and the tables; the screen's institution menu stays
            // the MenuReader's (registered first: the first classifier that claims a path takes it).
            if (!path.StartsWith("Canvas_UI/" + RootName + "/comtitle/", StringComparison.Ordinal)
                && !path.StartsWith("Canvas_UI/" + RootName + "/combmenu/", StringComparison.Ordinal))
                return null;
            return (component, raw) =>
            {
                if (_root == null)
                    _root = FindRoot(component.transform);
            };
        }

        private static Transform FindRoot(Transform t)
        {
            for (int depth = 0; t != null && depth < 12; depth++, t = t.parent)
            {
                if (t.name == RootName)
                    return t;
            }
            return null;
        }

        private static void AfterStockDevilInfo(cmpCursorInfo_t __2, byte __3)
        {
            _cursor = __2;
            _type = __3;
            _drawnAt = Clock.ElapsedMilliseconds;
        }

        private static readonly Dictionary<string, sbyte> ResultIconTypes = new Dictionary<string, sbyte>();

        /// <summary>Kept for the log only: which icon type the game drew for which row object.</summary>
        private static void AfterResultIcon(GameObject __0, sbyte __2)
        {
            try
            {
                string name = __0 == null ? "(null)" : __0.name;
                if (!ResultIconTypes.TryGetValue(name, out sbyte old) || old != __2)
                {
                    ResultIconTypes[name] = __2;
                    Log.Info($"{Tag} Result icon type {__2} on {(__0 == null ? "(null)" : UiUtil.PathOf(__0.transform))}.");
                }
            }
            catch
            {
            }
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            long now = Clock.ElapsedMilliseconds;
            try
            {
                if (now - _drawnAt > DrawnWithinMs || _cursor == null || _root == null)
                {
                    if (_open)
                        Log.Info($"{Tag} Table closed.");
                    _open = false;
                    _key = _pendingKey = -1;
                    _covered = false;
                    return;
                }
                if (StatusReader.IsOpen)
                {
                    // E (Stats) shows the demon over the table: say the table's cursor again when it closes.
                    _covered = true;
                    return;
                }

                cmpCursorPos_t pos = _cursor.CursorPos;
                if (pos == null)
                    return;
                int table = UiUtil.IsVisible(Find("combmenu/comtable02")) ? 2 : 1;
                int entry = pos.Index + pos.Shift;
                int key = table * 1000 + entry;
                if (!_open)
                {
                    _open = true;
                    _openedAt = now;
                    _table = 0;
                    _key = -1;
                    RowsLogged.Clear();
                    Log.Info($"{Tag} Table opened: type {_type}, Index {pos.Index} Shift {pos.Shift} ListNums {pos.ListNums} ShiftMax {pos.ShiftMax}.");
                    LogVocabulary();
                }
                if (key != _pendingKey)
                {
                    _pendingKey = key;
                    _pendingSince = now;
                }
                if (now - _openedAt < OpenDelayMs || now - _pendingSince < SettleMs || (key == _key && !_covered))
                    return;

                bool opening = table != _table || _covered;
                if (opening && SpeechOrder.Hold("Fusion table", ref _holdSince, now))
                    return;
                string text = Describe(table, pos.Shift + 1, entry, pos.ListNums, pos.Index);
                if (text.Length == 0)
                    return;   // the row is not written yet
                _holdSince = -1;
                _key = key;
                if (opening)
                {
                    string title = Text(Find("comtitle/Text_comtitleTM"));
                    if (title.Length > 0 && (table != _table))
                        text = Sentence(title) + " " + text;
                }
                _table = table;
                _covered = false;
                Say(text, interrupt: !opening);
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"{Tag} {ex.Message}");
            }
        }

        /// <summary>"Jack Frost, Fairy, level 7, 2 of 3. Result: Aeros, Element, level 9." or "" while the row is blank.</summary>
        private static string Describe(int table, int row, int entry, int count, int top)
        {
            string rowPath = $"combmenu/comtable0{table}/comtable_row/comtable0{table}_row{row:D2}";
            Transform rowObj = Find(rowPath);
            if (!UiUtil.IsVisible(rowObj?.gameObject))
                return "";
            LogRow(table, row, rowObj);

            string name = Text(rowObj.Find(table == 1 ? "Text_nameTM" : "Text_name01TM"));
            if (name.Length == 0)
                return "";
            string race = Text(rowObj.Find(table == 1 ? "Text_raceTM" : "Text_race01TM"));
            Levels(rowObj, out int? level, out int? resultLevel);

            var first = new List<string> { name };
            if (race.Length > 0)
                first.Add(race);
            if (level.HasValue)
                first.Add($"level {level.Value}");
            if (count > 0 && entry >= 0 && entry < count)
                first.Add($"{entry + 1} of {count}");
            string text = string.Join(", ", first) + ".";

            if (table == 2)
            {
                string result = Text(rowObj.Find("Text_name02TM"));
                string resultRace = Text(rowObj.Find("Text_race02TM"));
                if (result.Length > 0 && resultRace.Length == 0)
                {
                    text += " " + Sentence(result);   // "CANNOT FUSE"
                }
                else if (result.Length > 0)
                {
                    var second = new List<string> { result, resultRace };
                    if (resultLevel.HasValue)
                        second.Add($"level {resultLevel.Value}");
                    // The result icon (comtable_icon): only Rank Up, Rank Down and Power Up add to what is said; OK is every
                    // fusable row and N/A comes with "CANNOT FUSE".
                    string icon = IconWord(SpriteName(ActiveChild(rowObj.Find("comtable_icon"))));
                    if (icon.Length > 0 && icon != Word("ok") && icon != Word("no"))
                        second.Add(icon);
                    text += " Result: " + string.Join(", ", second) + ".";
                }
            }
            else
            {
                string partners = Partners(rowObj, entry, top);
                if (partners.Length > 0)
                    text += " " + partners;
            }
            return text;
        }

        /// <summary>
        /// The first table's grid (13:37 session, 5 Oct 2026): each row has comtable_icon01..NN, one per demon of the list in
        /// order, each showing one sprite: comtable_oblique on the demon itself, comtable_ok, comtable_no, comtable_rup,
        /// comtable_rdown, comtable_pwup. Said grouped in the game's own words (Localize COMBINE_L0030 to L0034, "OK", "N/A",
        /// "Rank Up", "Rank Down", "Power Up"), the partners named from the list's rows: "OK: Jack Frost, Lilim. N/A: Hua Po."
        /// </summary>
        private static string Partners(Transform rowObj, int entry, int top)
        {
            var groups = new List<(string word, List<string> names)>();
            for (int k = 1; k <= 30; k++)
            {
                Transform cell = rowObj.Find($"comtable_icon{k:D2}");
                if (cell == null)
                    break;
                if (!cell.gameObject.activeInHierarchy)
                    continue;
                string word = IconWord(SpriteName(ActiveChild(cell)));
                if (word.Length == 0 || k - 1 == entry)
                    continue;
                // Column k is list entry k - 1; its name is on the row showing that entry, when it is on screen.
                string partner = Text(Find($"combmenu/comtable01/comtable_row/comtable01_row{k - top:D2}/Text_nameTM"));
                if (partner.Length == 0)
                    partner = $"number {k}";
                var g = groups.FirstOrDefault(x => x.word == word);
                if (g.names == null)
                    groups.Add((word, new List<string> { partner }));
                else
                    g.names.Add(partner);
            }
            return string.Join(" ", groups.Select(g => $"{g.word}: {string.Join(", ", g.names)}."));
        }

        private static Transform ActiveChild(Transform t)
        {
            if (t == null)
                return null;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                if (c.gameObject.activeInHierarchy && SpriteName(c).Length > 0)
                    return c;
            }
            return null;
        }

        /// <summary>The game's word for an icon sprite (COMBINE_L0030 "OK" ... L0034 "Power Up"), "" for the demon itself or another.</summary>
        private static string IconWord(string sprite)
        {
            if (!sprite.StartsWith("comtable_", StringComparison.Ordinal))
                return "";
            return Word(sprite.Substring("comtable_".Length));
        }

        private static readonly Dictionary<string, string> Words = new Dictionary<string, string>();

        private static string Word(string icon)
        {
            if (Words.TryGetValue(icon, out string w))
                return w;
            int id = icon == "ok" ? 30 : icon == "no" ? 31 : icon == "rup" ? 32 : icon == "rdown" ? 33 : icon == "pwup" ? 34 : -1;
            w = "";
            if (id >= 0)
            {
                try
                {
                    string t = Localize.GetLocalizeText($"<COMBINE_L{id:D4}>") ?? "";
                    w = t.StartsWith("COMBINE_", StringComparison.Ordinal) ? "" : TextUtil.Clean(t);
                }
                catch
                {
                    w = "";
                }
            }
            Words[icon] = w;
            return w;
        }

        /// <summary>The level counters under the row (CounterCtr on lvnum, lvnum01 the demon's, lvnum02 the result's; 13:37 log).</summary>
        private static void Levels(Transform rowObj, out int? level, out int? resultLevel)
        {
            level = resultLevel = null;
            foreach (CounterCtr c in rowObj.GetComponentsInChildren<CounterCtr>(false))
            {
                string own = c.gameObject.name;
                if (own == "lvnum02")
                    resultLevel ??= c.value;
                else if (own == "lvnum" || own == "lvnum01")
                    level ??= c.value;
            }
        }

        private static string SpriteName(Transform t)
        {
            try
            {
                Image image = t.GetComponent<Image>();
                return image != null && image.sprite != null ? image.sprite.name : "";
            }
            catch
            {
                return "";
            }
        }

        private static TMP_Text FindText(Transform root, string name)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.gameObject.name == name)
                    return text;
            }
            return null;
        }

        /// <summary>Once per table opening and row: the row's active objects, counters and sprites, to check the mapping.</summary>
        private static void LogRow(int table, int row, Transform rowObj)
        {
            if (!RowsLogged.Add(table * 100 + row))
                return;
            var parts = new List<string>();
            foreach (Transform t in rowObj.GetComponentsInChildren<Transform>(false))
            {
                if (t == rowObj)
                    continue;
                string part = t.name;
                CounterCtr c = t.GetComponent<CounterCtr>();
                if (c != null)
                    part += "=" + c.value.ToString(CultureInfo.InvariantCulture);
                string sprite = SpriteName(t);
                if (sprite.Length > 0)
                    part += " [" + sprite + "]";
                parts.Add(part);
            }
            Log.Info($"{Tag} Table {table} row {row:D2}: {string.Join("; ", parts)}");
        }

        /// <summary>
        /// Once per session: the legend (texts, icons) and the game's own fusion texts (Localize "COMBINE_L" ids from
        /// fclCombineDraw's string literals), so the next session can name what the screen shows.
        /// </summary>
        private static void LogVocabulary()
        {
            if (_vocabularyLogged)
                return;
            _vocabularyLogged = true;
            try
            {
                foreach (Transform t in _root.GetComponentsInChildren<Transform>(true))
                {
                    Match m = LegendIcon.Match(t.name);
                    if (!m.Success)
                        continue;
                    var names = t.GetComponentsInChildren<Transform>(true).Select(d => d.name + (SpriteName(d) is var s && s.Length > 0 ? $" [{s}]" : "")).ToList();
                    TMP_Text label = FindText(_root, "Text_comicon" + m.Groups["n"].Value + "TM");
                    Log.Info($"{Tag} Legend {m.Groups["n"].Value}: \"{(label == null ? "(no text)" : TextUtil.Clean(label.text))}\"; {string.Join(", ", names)}");
                }
                for (int i = 0; i <= 40; i++)
                {
                    foreach (string id in new[] { $"<COMBINE_L{i:D4}>", $"COMBINE_L{i:D4}" })
                    {
                        string text;
                        try
                        {
                            text = Localize.GetLocalizeText(id);
                        }
                        catch (Exception ex)
                        {
                            text = "(" + ex.GetType().Name + ")";
                        }
                        if (!string.IsNullOrEmpty(text) && text != id)
                        {
                            Log.Info($"{Tag} Text {id} = \"{UiUtil.Escape(text)}\"");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} Vocabulary: {ex.Message}");
            }
        }

        private static Transform Find(string path) => _root == null ? null : _root.Find(path);

        private static string Text(Transform t)
        {
            if (t == null || !t.gameObject.activeInHierarchy)
                return "";
            TMP_Text text = t.GetComponent<TMP_Text>();
            return text == null ? "" : TextUtil.Clean(text.text);
        }

        private static string Sentence(string text)
        {
            char last = text[text.Length - 1];
            return last == '.' || last == '?' || last == '!' ? text : text + ".";
        }

        private static void Say(string text, bool interrupt)
        {
            LastUtterance = text;
            LastUtteranceAt = Environment.TickCount64;
            SpeechManager.Say(text, interrupt: interrupt, force: true);
        }
    }
}
