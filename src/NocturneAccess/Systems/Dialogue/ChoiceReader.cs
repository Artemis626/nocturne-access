using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Dialogue
{
    /// <summary>
    /// Speaks dialogue choices (answers to questions in conversations).
    ///
    /// Sources (probe session 1, docs/class-index.md):
    /// - Option texts: Canvas_UI/talkUI/talk01_choice/choice_answer_oubei0N/answer01TM, N = 1, 2, ...
    ///   written before the choice opens.
    /// - itfMesManager.SelectWindowDispOn(instanceMes_tag): a choice opens.
    /// - itfMesManager.ChangeSelection(int index, int onoff): onoff = 1 marks the highlighted option
    ///   (index 0 is choice_answer_oubei01). The first one comes right after the choice opens.
    /// - itfMesManager.itfMesMngRequestMessage / WindowDispOff: the conversation moved on, the choice is over
    ///   (SelectWindowDispOff exists but never fired in session 1).
    ///
    /// The first announcement is queued, so it follows the question instead of cutting it off.
    /// Later cursor moves interrupt.
    /// </summary>
    internal static class ChoiceReader
    {
        private const string Tag = "[Choice]";
        private const int MaxOptions = 8;

        private static readonly Regex OptionPath =
            new Regex(@"^Canvas_UI/talkUI/talk01_choice/choice_answer_oubei(\d\d)/answer01TM$", RegexOptions.Compiled);

        private static readonly string[] Options = new string[MaxOptions];
        private static readonly TMP_Text[] OptionComponents = new TMP_Text[MaxOptions];

        private static bool _active;
        private static int _writtenMask;   // options written since the last choice ended
        private static bool _announced;
        private static int _current = -1;

        /// <summary>The highlighted option while a choice is open, for the repeat key; otherwise empty.</summary>
        internal static string CurrentOption => _active && _current >= 0 ? Describe(_current) : "";

        internal static void Install()
        {
            if (!Config.ChoicesEnabled.Value)
            {
                Log.Info($"{Tag} Off in the config.");
                return;
            }

            TmpRouter.Register("ChoiceReader", Classify);

            Type mes = typeof(itfMesManager);
            Type i = typeof(int);
            Patcher.Postfix("Choice", mes, "SelectWindowDispOn", new[] { typeof(instanceMes_tag) },
                AccessTools.Method(typeof(ChoiceReader), nameof(AfterOpen)));
            Patcher.Postfix("Choice", mes, "ChangeSelection", new[] { i, i },
                AccessTools.Method(typeof(ChoiceReader), nameof(AfterChangeSelection)));
            Patcher.Postfix("Choice", mes, "itfMesMngRequestMessage", new[] { i, i, i, i },
                AccessTools.Method(typeof(ChoiceReader), nameof(AfterEnd)));
            Patcher.Postfix("Choice", mes, "WindowDispOff", new[] { typeof(bool) },
                AccessTools.Method(typeof(ChoiceReader), nameof(AfterEnd)));
        }

        private static TmpRouter.Handler Classify(string path)
        {
            Match m = OptionPath.Match(path);
            if (!m.Success)
                return null;

            int index = int.Parse(m.Groups[1].Value) - 1;
            if (index < 0 || index >= MaxOptions)
                return null;
            return (component, raw) =>
            {
                OptionComponents[index] = component;
                Options[index] = TextUtil.Clean(raw);
                _writtenMask |= 1 << index;
            };
        }

        private static void AfterOpen()
        {
            _active = true;
            _announced = false;
            _current = -1;
            Log.Info($"{Tag} Opened.");
        }

        private static void AfterChangeSelection(int __0, int __1)
        {
            try
            {
                if (__1 != 1)
                    return;
                if (!_active)
                {
                    Log.Debug($"{Tag} ChangeSelection({__0}, 1) outside a dialogue choice.");
                    return;
                }
                if (__0 < 0 || __0 >= MaxOptions || (__0 == _current && _announced))
                    return;

                _current = __0;
                if (!_announced)
                {
                    _announced = true;
                    SpeechManager.Say($"Choice, {VisibleCount(__0)} options. {Describe(__0)}", interrupt: false, force: true);
                }
                else
                {
                    SpeechManager.Say(Describe(__0), interrupt: true, force: true);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"{Tag} failed: {ex.Message}");
            }
        }

        private static void AfterEnd()
        {
            _writtenMask = 0;
            if (!_active)
                return;
            _active = false;
            Log.Info($"{Tag} Closed.");
        }

        private static string Describe(int index)
        {
            string text = Options[index];
            if (string.IsNullOrEmpty(text))
            {
                Log.Warn($"{Tag} No text captured for option {index + 1}.");
                text = $"Option {index + 1}";
            }
            return $"{text}, {index + 1} of {VisibleCount(index)}";
        }

        /// <summary>
        /// Options in this choice. The option objects are still inactive when the first highlight
        /// arrives (session 1: visible about 200 ms later), so count the options written since the
        /// last choice ended; fall back to visible components; never fewer than the highlighted index.
        /// </summary>
        private static int VisibleCount(int atLeastIndex)
        {
            int count = 0;
            for (int i = 0; i < MaxOptions; i++)
            {
                if ((_writtenMask & (1 << i)) != 0)
                {
                    count = i + 1;
                    continue;
                }
                try
                {
                    if (_writtenMask == 0 && OptionComponents[i] != null && OptionComponents[i].gameObject.activeInHierarchy)
                        count = i + 1;
                }
                catch
                {
                    // Destroyed component: not visible.
                }
            }
            return Math.Max(count, atLeastIndex + 1);
        }
    }
}
