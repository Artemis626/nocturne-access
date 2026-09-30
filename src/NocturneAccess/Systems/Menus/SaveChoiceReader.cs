using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// Speaks the save/load Yes/No ("Load File 1?", "Will you overwrite the file?").
    ///
    /// The options carry no highlight material: two F6 dumps with the cursor on different options are
    /// identical (16:18 session, 30 Sep 2026). The cursor comes from the save/load engine instead
    /// (docs/class-index.md, "Save and load"):
    /// - slMain.slQueryYesNo(yesFunc, noFunc, yesNoDefault) starts a query. Writes to the option texts
    ///   (saveannounce_choice/answer0NTM) also arm the reader, in case another path opens the query.
    /// - slMain.slQueryYesNoCursor is read every frame while the options are visible.
    /// - slMain._saveUI._choiceList holds the option texts as the game wrote them; the cursor indexes them.
    /// - slMain._saveUI._choiceCursor (the highlight object) and _choiceObj (the options): the option
    ///   nearest the highlight is logged as a cross-check. If the cursor field never moves while the
    ///   highlight does, the highlight position is used instead, with a log line saying so.
    ///
    /// The first announcement is queued behind the prompt (Systems/SpeechOrder.cs); moves interrupt.
    /// </summary>
    internal static class SaveChoiceReader
    {
        private const string Tag = "[SaveChoice]";
        private const long ArmTimeoutMs = 10_000;
        private const long HighlightSettleMs = 120;

        private static readonly Regex OptionPath =
            new Regex(@"^Canvas_UI/saveUI\(Clone\)/saveannounce/saveannounce_choice/answer\d\dTM$", RegexOptions.Compiled);

        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private static bool _armed;
        private static long _armedAt;
        private static int _queryDefault = -1;
        private static bool _open;
        private static bool _announced;
        private static long _holdSince = -1;
        private static int _index = -1;          // option considered highlighted
        private static int _spoken = -1;         // option last spoken
        private static int _lastCursor = int.MinValue;
        private static bool _cursorMoved;        // the cursor field changed during this query
        private static bool _highlightMoved;     // the highlight object moved during this query
        private static int _lastNearest = -1;
        private static long _nearestChangedAt;
        private static bool _nearestPending;
        private static bool _mismatchLogged;
        private static int _errors;

        /// <summary>The prompt with the highlighted option, for the repeat key.</summary>
        internal static string LastUtterance { get; private set; } = "";
        internal static long LastUtteranceAt { get; private set; } = long.MinValue;   // Environment.TickCount64

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;

            TmpRouter.Register("SaveChoiceReader", path => OptionPath.IsMatch(path) ? OnOptionText : null);

            Type[] query = { typeof(slMain.slQueryYesFuncDelegate), typeof(slMain.slQueryNoFuncDelegate), typeof(int) };
            MethodInfo after = AccessTools.Method(typeof(SaveChoiceReader), nameof(AfterQuery));
            Patcher.Postfix("SaveChoice", typeof(slMain), "slQueryYesNo", query, after);
            Patcher.Postfix("SaveChoice", typeof(slMain), "slContinueYesNo", query, after);
        }

        private static void AfterQuery(MethodBase __originalMethod, int __2)
        {
            _queryDefault = __2;
            Log.Info($"{Tag} {__originalMethod.Name}(default {__2}), cursor {SafeCursor()}.");
            Arm();
        }

        private static void OnOptionText(TMP_Text component, string raw) => Arm();

        private static void Arm()
        {
            _armedAt = Clock.ElapsedMilliseconds;
            _armed = true;
        }

        /// <summary>Called every frame; does nothing until a query or its option texts are seen.</summary>
        internal static void Tick()
        {
            if (!_armed)
                return;
            try
            {
                Update(Clock.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"{Tag} {ex.Message}");
                Reset();
                _armed = false;
            }
        }

        private static void Update(long now)
        {
            saveUI ui = slMain._saveUI;
            Il2CppReferenceArray<TextMeshProUGUI> options = ui?._choiceList;
            if (options == null || options.Length == 0 || !AnyVisible(options))
            {
                if (_open)
                {
                    Log.Info($"{Tag} Closed.");
                    Reset();
                    _armedAt = now;   // stay armed briefly in case the same query shows again
                }
                else if (now - _armedAt > ArmTimeoutMs)
                {
                    _armed = false;
                }
                return;
            }

            int cursor = slMain.slQueryYesNoCursor;
            int nearest = NearestOption(ui);

            if (!_open)
            {
                _open = true;
                _lastCursor = cursor;
                _lastNearest = nearest;
                _nearestChangedAt = now;
                _nearestPending = true;   // cross-check (log only) once the highlight has settled
                _index = InRange(cursor, options) ? cursor : nearest;
                Log.Info($"{Tag} Opened: {DescribeOptions(options)}; cursor {cursor}, default {_queryDefault}, highlight over option {nearest}.");
            }

            // Primary source: the engine's cursor field.
            if (cursor != _lastCursor)
            {
                _lastCursor = cursor;
                _cursorMoved = true;
                if (InRange(cursor, options))
                    _index = cursor;
                Log.Info($"{Tag} Cursor {cursor}, highlight over option {nearest}.");
            }

            // Cross-check and fallback: the option the highlight object is drawn over.
            if (nearest != _lastNearest)
            {
                if (_lastNearest >= 0 && nearest >= 0)
                    _highlightMoved = true;   // from one option to another, not just appearing
                _lastNearest = nearest;
                _nearestChangedAt = now;
                _nearestPending = true;
            }
            if (_nearestPending && now - _nearestChangedAt >= HighlightSettleMs)
            {
                _nearestPending = false;
                if (nearest >= 0 && nearest != _index)
                {
                    if (!_mismatchLogged)
                    {
                        _mismatchLogged = true;
                        Log.Warn($"{Tag} Cursor field says option {_index}, highlight is over option {nearest}.");
                    }
                    // At opening the cursor field is trusted; only a highlight that moves while the field stays still overrides it.
                    if (_highlightMoved && !_cursorMoved)
                    {
                        Log.Info($"{Tag} Using the highlight position: option {nearest}.");
                        _index = nearest;
                    }
                }
            }

            if (_index < 0 || _index == _spoken)
                return;

            if (!_announced && SpeechOrder.Hold("Save Yes/No", ref _holdSince, now))
                return;
            _holdSince = -1;

            string option = Describe(options, _index);
            _spoken = _index;
            LastUtterance = Join(PromptText(ui), option);
            LastUtteranceAt = Environment.TickCount64;

            if (!_announced)
            {
                _announced = true;
                SpeechManager.Say($"Choice, {VisibleCount(options)} options. {option}", interrupt: false, force: true);
            }
            else
            {
                SpeechManager.Say(option, interrupt: true, force: true);
            }
        }

        private static void Reset()
        {
            _open = false;
            _announced = false;
            _holdSince = -1;
            _index = -1;
            _spoken = -1;
            _lastCursor = int.MinValue;
            _cursorMoved = false;
            _highlightMoved = false;
            _lastNearest = -1;
            _nearestPending = false;
            _mismatchLogged = false;
            _queryDefault = -1;
        }

        private static string Describe(Il2CppReferenceArray<TextMeshProUGUI> options, int index)
        {
            string text = TextOf(options[index]);
            if (text.Length == 0)
            {
                Log.Warn($"{Tag} Option {index} has no text.");
                text = $"Option {index + 1}";
            }
            return $"{text}, {index + 1} of {Math.Max(VisibleCount(options), index + 1)}";
        }

        /// <summary>The prompt lines currently shown ("Load File 1?"), for the repeat key.</summary>
        private static string PromptText(saveUI ui)
        {
            var lines = new List<string>();
            Il2CppReferenceArray<TextMeshProUGUI> announce = ui._announceList;
            if (announce == null)
                return "";
            for (int i = 0; i < announce.Length; i++)
            {
                TextMeshProUGUI line = announce[i];
                if (IsVisible(line))
                {
                    string text = TextOf(line);
                    if (text.Length > 0)
                        lines.Add(text);
                }
            }
            return string.Join(" ", lines);
        }

        /// <summary>Index of the visible option whose centre is nearest the highlight object, or -1.</summary>
        private static int NearestOption(saveUI ui)
        {
            GameObject highlight = ui._choiceCursor;
            Il2CppReferenceArray<GameObject> objects = ui._choiceObj;
            if (highlight == null || objects == null || !highlight.activeInHierarchy)
                return -1;

            Vector3 c = highlight.transform.position;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject o = objects[i];
                if (o == null || !o.activeInHierarchy)
                    continue;
                Vector3 p = o.transform.position;
                float dx = p.x - c.x, dy = p.y - c.y;
                float distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        private static string DescribeOptions(Il2CppReferenceArray<TextMeshProUGUI> options)
        {
            var parts = new List<string>();
            for (int i = 0; i < options.Length; i++)
            {
                TextMeshProUGUI o = options[i];
                string name = o == null ? "null" : o.gameObject.name;
                parts.Add($"{i} = {name} \"{TextOf(o)}\"{(IsVisible(o) ? "" : " (hidden)")}");
            }
            return string.Join(", ", parts);
        }

        private static int VisibleCount(Il2CppReferenceArray<TextMeshProUGUI> options)
        {
            int count = 0;
            for (int i = 0; i < options.Length; i++)
            {
                if (IsVisible(options[i]) && TextOf(options[i]).Length > 0)
                    count++;
            }
            return count;
        }

        private static bool AnyVisible(Il2CppReferenceArray<TextMeshProUGUI> options)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (IsVisible(options[i]))
                    return true;
            }
            return false;
        }

        private static bool InRange(int index, Il2CppReferenceArray<TextMeshProUGUI> options)
            => index >= 0 && index < options.Length;

        private static string TextOf(TMP_Text component) => component == null ? "" : TextUtil.Clean(component.text);

        private static string Join(string prompt, string option) => prompt.Length > 0 ? $"{prompt} {option}" : option;

        private static string SafeCursor()
        {
            try
            {
                return slMain.slQueryYesNoCursor.ToString();
            }
            catch
            {
                return "unreadable";
            }
        }

        private static bool IsVisible(Component component)
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
