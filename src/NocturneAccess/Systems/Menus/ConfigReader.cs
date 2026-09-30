using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// Speaks the config screen (title CONFIG and System > Config): tab, row, value, position, description.
    ///
    /// Sources (16:49 session, docs/class-index.md "Title screen and config"):
    /// - Rows: configUI_steam(Clone)/cfgmenu/scrollveiw_TAB/content/cfgmenuset_ROW/..., one scroll view per
    ///   tab; only the selected tab's rows are visible. The highlighted row's name (cgfmenu_tilte/TextTM)
    ///   is written with material TMC01.
    /// - Description: cfgmenu_note01/TextTM. Value: cgfmenu_tilte/setting/Text (TMP), only when it carries a
    ///   material tag (on key rows the same path holds an untagged column label). Key rows: the bound keys
    ///   in cgfmenu_tilte/key/Image_keyNN/Text (TMP).
    /// - Tab names: tab/tab_button/tab_TAB/Text (TMP). The current tab is taken from the highlighted
    ///   visible row, because the tab texts flicker while the screen builds.
    /// - Volume rows have no value text: the value is the row's slider (configMenuUI.slideObj), polled
    ///   while such a row is highlighted.
    ///
    /// Announcements: opening "AUDIO tab. CONFIRM, OK, 1 of 7. Apply changed settings." (queued behind
    /// any prompt); tab change the same, interrupting; row change without the tab; a value change says
    /// only the new value. Identical rewrites never count as a change.
    /// </summary>
    internal static class ConfigReader
    {
        private const string Tag = "[Config]";
        private const string Highlight = "<material=\"TMC01\">";
        private const long SettleMs = 80;
        private const long SliderPollMs = 100;
        private const long VisibilityPollMs = 250;

        private static readonly Regex TabPath = new Regex(
            @"^Canvas_UI/configUI_steam\(Clone\)/tab/tab_button/tab_(?<tab>[a-z]+)/Text \(TMP\)$", RegexOptions.Compiled);

        private static readonly Regex RowPath = new Regex(
            @"^Canvas_UI/configUI_steam\(Clone\)/cfgmenu/scrollveiw_(?<tab>[a-z]+)/content/cfgmenuset_(?<row>[^/]+)/"
            + @"(?:(?<title>cgfmenu_tilte/TextTM)|(?<note>cfgmenu_note01/TextTM)|(?<value>cgfmenu_tilte/setting/Text \(TMP\))|cgfmenu_tilte/key/Image_key(?<key>\d\d)/Text \(TMP\))$",
            RegexOptions.Compiled);

        private sealed class Row
        {
            public string Tab;
            public string Id;
            public TMP_Text TitleComponent;
            public string Title = "";
            public bool Highlighted;
            public long HighlightedAt;
            public string Note = "";
            public string Value = "";
            public readonly SortedDictionary<int, string> Keys = new SortedDictionary<int, string>();
            public bool SliderLogged;
            public bool HasSlider;      // an active slider was found; only then is it polled
        }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Dictionary<string, Row> Rows = new Dictionary<string, Row>();
        private static readonly Dictionary<string, string> TabNames = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> TabRaw = new Dictionary<string, string>();

        private static bool _dirty;
        private static long _lastChange;
        private static long _holdSince = -1;
        private static long _lastSliderPoll;
        private static long _lastVisibilityPoll;
        private static Row _current;
        private static string _currentTab;
        private static string _spokenValue;

        /// <summary>The current row with its tab and description, for the repeat key.</summary>
        internal static string LastUtterance { get; private set; } = "";
        internal static long LastUtteranceAt { get; private set; } = long.MinValue;   // Environment.TickCount64

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            TmpRouter.Register("ConfigReader", Classify);
        }

        private static TmpRouter.Handler Classify(string path)
        {
            Match tab = TabPath.Match(path);
            if (tab.Success)
            {
                string key = tab.Groups["tab"].Value;
                return (component, raw) =>
                {
                    // A tab switch rewrites only the tab texts (TMC01 moves), so a material change is a change too.
                    if (TabRaw.TryGetValue(key, out string old) && old == raw)
                        return;
                    TabRaw[key] = raw;
                    TabNames[key] = TextUtil.Clean(raw);
                    Changed();
                };
            }

            Match m = RowPath.Match(path);
            if (!m.Success)
                return null;

            string rowKey = m.Groups["tab"].Value + "/" + m.Groups["row"].Value;
            if (!Rows.TryGetValue(rowKey, out Row row))
                Rows[rowKey] = row = new Row { Tab = m.Groups["tab"].Value, Id = m.Groups["row"].Value };

            if (m.Groups["title"].Success)
                return (component, raw) => OnTitle(row, component, raw);
            if (m.Groups["note"].Success)
                return (component, raw) => Set(ref row.Note, TextUtil.Clean(raw));
            if (m.Groups["value"].Success)
                return (component, raw) => Set(ref row.Value, raw.Contains("<material=") ? TextUtil.Clean(raw) : "");
            int keySlot = int.Parse(m.Groups["key"].Value);
            return (component, raw) =>
            {
                string text = TextUtil.Clean(raw);
                if (!row.Keys.TryGetValue(keySlot, out string old) || old != text)
                {
                    row.Keys[keySlot] = text;
                    Changed();
                }
            };
        }

        private static void OnTitle(Row row, TMP_Text component, string raw)
        {
            row.TitleComponent = component;
            Set(ref row.Title, TextUtil.Clean(raw));
            bool highlighted = raw.Contains(Highlight);
            if (highlighted == row.Highlighted)
                return;
            row.Highlighted = highlighted;
            if (highlighted)
                row.HighlightedAt = Clock.ElapsedMilliseconds;
            Changed();
        }

        private static void Set(ref string field, string text)
        {
            if (field == text)
                return;
            field = text;
            Changed();
        }

        private static void Changed()
        {
            _dirty = true;
            _lastChange = Clock.ElapsedMilliseconds;
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            if (Rows.Count == 0)
                return;
            long now = Clock.ElapsedMilliseconds;
            try
            {
                if (_dirty && now - _lastChange >= SettleMs)
                {
                    Evaluate(now);
                }
                else if (_current != null && now - _lastSliderPoll >= SliderPollMs)
                {
                    _lastSliderPoll = now;
                    // Only rows where a slider was found (none were in the 20:08 session), and only while shown:
                    // polling a row of a closed screen touched a destroyed object (NullReferenceException at 20:12:25).
                    if (_current.HasSlider && _current.Value.Length == 0 && _current.Keys.Count == 0 && IsVisible(_current.TitleComponent))
                    {
                        string slider = SliderValue(_current);
                        if (slider.Length > 0 && slider != _spokenValue)
                            SpeakValue(slider);
                    }
                }

                if (_current != null && !_dirty && now - _lastVisibilityPoll >= VisibilityPollMs)
                {
                    _lastVisibilityPoll = now;
                    if (!IsVisible(_current.TitleComponent))
                    {
                        _current = null;
                        _currentTab = null;
                        _spokenValue = null;
                        Log.Info($"{Tag} Closed.");
                    }
                }
            }
            catch (Exception ex)
            {
                _dirty = false;
                Log.Error($"{Tag} {ex.Message}");
            }
        }

        private static void Evaluate(long now)
        {
            Row row = Rows.Values
                .Where(r => r.Highlighted && r.Title.Length > 0 && IsVisible(r.TitleComponent))
                .OrderByDescending(r => r.HighlightedAt)
                .FirstOrDefault();
            if (row == null)
            {
                _dirty = false;
                return;
            }

            bool opening = _current == null;
            if (opening && SpeechOrder.Hold("Config", ref _holdSince, now))
                return;
            _holdSince = -1;
            _dirty = false;

            string value = ValueOf(row);
            if (!opening && row == _current && row.Tab == _currentTab)
            {
                if (value != _spokenValue && value.Length > 0)
                    SpeakValue(value);
                return;
            }

            bool newTab = row.Tab != _currentTab;
            _current = row;
            _currentTab = row.Tab;
            _spokenValue = value;

            string description = Describe(row, value);
            string tab = TabNames.TryGetValue(row.Tab, out string name) && name.Length > 0 ? $"{name} tab. " : "";
            LastUtterance = tab + description;
            LastUtteranceAt = Environment.TickCount64;
            if (opening)
                Log.Info($"{Tag} Opened.");
            SpeechManager.Say(newTab ? tab + description : description, interrupt: !opening, force: true);
        }

        private static void SpeakValue(string value)
        {
            _spokenValue = value;
            if (_current != null)
            {
                string tab = TabNames.TryGetValue(_current.Tab, out string name) && name.Length > 0 ? $"{name} tab. " : "";
                LastUtterance = tab + Describe(_current, value);
                LastUtteranceAt = Environment.TickCount64;
            }
            SpeechManager.Say(value, interrupt: true, force: true);
        }

        /// <summary>"BGM SELECT, Default, 3 of 7. Revert the BGM for the Vortex World map."</summary>
        private static string Describe(Row row, string value)
        {
            string head = value.Length > 0 ? $"{row.Title}, {value}" : row.Title;
            List<Row> rows = RowsOfTab(row.Tab);
            int position = rows.IndexOf(row) + 1;
            if (position > 0)
                head += $", {position} of {rows.Count}";
            return row.Note.Length > 0 ? $"{head}. {row.Note}" : head;
        }

        /// <summary>The row's value: its setting text, its bound keys, or its slider.</summary>
        private static string ValueOf(Row row)
        {
            if (row.Value.Length > 0)
                return row.Value;
            string keys = string.Join(" or ", row.Keys.Values.Where(k => k.Length > 0));
            if (keys.Length > 0)
                return keys;
            return row.Keys.Count > 0 ? "" : SliderValue(row);
        }

        /// <summary>Selectable rows of a tab (headings excluded), in screen order.</summary>
        private static List<Row> RowsOfTab(string tab)
        {
            return Rows.Values
                .Where(r => r.Tab == tab && r.Title.Length > 0 && !r.Id.StartsWith("category", StringComparison.Ordinal)
                            && IsVisible(r.TitleComponent))
                .OrderBy(r => SiblingIndex(r))
                .ToList();
        }

        private static int SiblingIndex(Row row)
        {
            Transform rowObject = RowObject(row);
            return rowObject != null ? rowObject.GetSiblingIndex() : int.MaxValue;
        }

        /// <summary>The cfgmenuset_ROW object: the title text is at cfgmenuset_ROW/cgfmenu_tilte/TextTM.</summary>
        private static Transform RowObject(Row row) => row.TitleComponent?.transform.parent?.parent;

        /// <summary>The value of the row's visible slider, from the row's configMenuUI, or "".</summary>
        private static string SliderValue(Row row)
        {
            Transform rowObject = RowObject(row);
            if (rowObject == null)
                return "";
            configMenuUI menu = rowObject.GetComponent<configMenuUI>() ?? row.TitleComponent.transform.parent.GetComponent<configMenuUI>();
            if (menu == null)
            {
                if (!row.SliderLogged)
                {
                    row.SliderLogged = true;
                    Log.Info($"{Tag} No configMenuUI on {row.Id}: no value to read.");
                }
                return "";
            }
            Il2CppReferenceArray<Slider> sliders = menu.slideObj;
            if (sliders == null)
                return "";

            for (int i = 0; i < sliders.Length; i++)
            {
                Slider s = sliders[i];
                if (s == null || !s.gameObject.activeInHierarchy)
                    continue;
                row.HasSlider = true;
                float v = s.value;
                if (!row.SliderLogged)
                {
                    row.SliderLogged = true;
                    Log.Info($"{Tag} Slider on {row.Title}: value {v}, min {s.minValue}, max {s.maxValue}, whole numbers {s.wholeNumbers}.");
                }
                // Whole-number sliders are spoken as the number; 0-to-1 sliders as a percentage.
                if (s.maxValue <= 1f && !s.wholeNumbers)
                    return $"{Math.Round(v * 100f)} percent";
                return Math.Round(v).ToString(CultureInfo.InvariantCulture);
            }
            return "";
        }

        private static bool IsVisible(TMP_Text component)
        {
            try
            {
                return component != null && component.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }
    }
}
