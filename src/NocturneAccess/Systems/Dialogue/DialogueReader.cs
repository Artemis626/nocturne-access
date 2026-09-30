using System;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Dialogue
{
    /// <summary>
    /// Speaks dialogue pages as they appear, from the game's own strings.
    ///
    /// Sources (probe session 1, docs/class-index.md):
    /// - Lines: Canvas_UI/talkUI/talk01/talk_window/Text01TM..Text03TM. All lines of a page are
    ///   written at once with their full text; the typewriter only moves &lt;alpha=#00&gt; tags, so
    ///   the cleaned text is complete from the first write. Empty lines hold a hidden placeholder.
    /// - Speaker: Canvas_UI/talkUI/name01/name_window/TextTM.
    /// - itfMesManager.SwitchSpeaker(bool sw, bool disp): the last call before a page says whether
    ///   the speaker box is shown for it.
    /// - itfMesManager.WindowDispOff(bool): the message window closed.
    /// - itfMesManager.itfMesMngRequestMessage(id, message, page, callmode): a message was requested;
    ///   its lines arrive a few hundred ms later. Until its page is spoken, PageDue is true, so a menu
    ///   opening at the same moment (the terminal) waits behind it (Systems/SpeechOrder.cs).
    ///
    /// A page is spoken once no line or speaker change has arrived for SettleMs, as one
    /// interrupting utterance. The speaker is named when it changes, after the window reopens,
    /// or on every page if SpeakerEveryPage is set.
    /// </summary>
    internal static class DialogueReader
    {
        private const string Tag = "[Dialogue]";
        private const long SettleMs = 150;
        private const long RequestDueMs = 1500;
        private const string LinePathPrefix = "Canvas_UI/talkUI/talk01/talk_window/Text0";
        private const string SpeakerPath = "Canvas_UI/talkUI/name01/name_window/TextTM";

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly string[] Lines = { "", "", "" };
        private static readonly TMP_Text[] LineComponents = new TMP_Text[3];

        private static string _speaker = "";
        private static bool _speakerOn;
        private static bool _dirty;
        private static long _changedAt;
        private static bool _clearedSinceSpoken = true;
        private static bool _windowReopened = true;
        private static string _lastPage = "";
        private static string _lastSpeakerSpoken = "";
        private static long _requestedAt = -1;   // Clock ms of a requested message whose page is not spoken yet

        /// <summary>A page is on its way: lines changed and not yet settled, or a message was just requested.</summary>
        internal static bool PageDue
            => _dirty || (_requestedAt >= 0 && Clock.ElapsedMilliseconds - _requestedAt < RequestDueMs);

        /// <summary>The last page with its speaker, for the repeat key.</summary>
        internal static string LastUtterance { get; private set; } = "";
        internal static long LastUtteranceAt { get; private set; } = long.MinValue;   // Environment.TickCount64

        internal static void Install()
        {
            if (!Config.DialogueEnabled.Value)
            {
                Log.Info($"{Tag} Off in the config.");
                return;
            }

            TmpRouter.Register("DialogueReader", Classify);

            Type mes = typeof(itfMesManager);
            Patcher.Postfix("Dialogue", mes, "SwitchSpeaker", new[] { typeof(bool), typeof(bool) },
                AccessTools.Method(typeof(DialogueReader), nameof(AfterSwitchSpeaker)));
            Patcher.Postfix("Dialogue", mes, "WindowDispOff", new[] { typeof(bool) },
                AccessTools.Method(typeof(DialogueReader), nameof(AfterWindowOff)));
            Type i = typeof(int);
            Patcher.Postfix("Dialogue", mes, "itfMesMngRequestMessage", new[] { i, i, i, i },
                AccessTools.Method(typeof(DialogueReader), nameof(AfterRequestMessage)));
        }

        private static TmpRouter.Handler Classify(string path)
        {
            if (path == SpeakerPath)
                return OnSpeaker;

            // Text01TM, Text02TM, Text03TM
            if (path.Length == LinePathPrefix.Length + 3 && path.StartsWith(LinePathPrefix, StringComparison.Ordinal)
                && path.EndsWith("TM", StringComparison.Ordinal))
            {
                int line = path[LinePathPrefix.Length] - '1';
                if (line >= 0 && line < Lines.Length)
                    return (component, raw) => OnLine(line, component, raw);
            }
            return null;
        }

        private static void OnLine(int index, TMP_Text component, string raw)
        {
            LineComponents[index] = component;
            string text = TextUtil.IsPlaceholder(raw) ? "" : TextUtil.Clean(raw);
            if (text == Lines[index])
                return;

            Lines[index] = text;
            if (Lines.All(l => l.Length == 0))
                _clearedSinceSpoken = true;
            _dirty = true;
            _changedAt = Clock.ElapsedMilliseconds;
        }

        private static void OnSpeaker(TMP_Text component, string raw)
        {
            string name = TextUtil.Clean(raw);
            if (name == _speaker)
                return;
            _speaker = name;
            // Keep a pending page waiting a little longer so a name written just after its lines is included.
            if (_dirty)
                _changedAt = Clock.ElapsedMilliseconds;
        }

        private static void AfterSwitchSpeaker(bool __0)
        {
            _speakerOn = __0;
        }

        private static void AfterWindowOff()
        {
            _windowReopened = true;
            _clearedSinceSpoken = true;
        }

        private static void AfterRequestMessage()
        {
            _requestedAt = Clock.ElapsedMilliseconds;
        }

        /// <summary>Called every frame: speaks a page once it has settled.</summary>
        internal static void Tick()
        {
            if (!_dirty || Clock.ElapsedMilliseconds - _changedAt < SettleMs)
                return;
            _dirty = false;

            try
            {
                SpeakPage();
            }
            catch (Exception ex)
            {
                Log.Error($"{Tag} failed to speak a page: {ex.Message}");
            }
        }

        private static void SpeakPage()
        {
            string page = string.Join(" ", Lines.Where(l => l.Length > 0));
            if (page.Length == 0)
                return;
            if (page == _lastPage && !_clearedSinceSpoken)
                return;

            if (!AnyLineVisible())
            {
                _requestedAt = -1;
                Log.Info($"{Tag} Skipped a page written while the window is hidden: \"{page}\"");
                return;
            }

            string speaker = _speakerOn ? _speaker : "";
            bool nameIt = speaker.Length > 0
                          && (Config.SpeakerEveryPage.Value || speaker != _lastSpeakerSpoken || _windowReopened);

            LastUtterance = speaker.Length > 0 ? $"{speaker}: {page}" : page;
            LastUtteranceAt = Environment.TickCount64;
            _lastPage = page;
            _lastSpeakerSpoken = speaker;
            _clearedSinceSpoken = false;
            _windowReopened = false;
            _requestedAt = -1;

            Log.Info($"{Tag} Page. Speaker box {(_speakerOn ? "on" : "off")}, name \"{_speaker}\", named {(nameIt ? "yes" : "no")}.");
            SpeechManager.Say(nameIt ? $"{speaker}: {page}" : page, interrupt: true, force: true);
        }

        private static bool AnyLineVisible()
        {
            foreach (TMP_Text component in LineComponents)
            {
                try
                {
                    if (component != null && component.gameObject.activeInHierarchy)
                        return true;
                }
                catch
                {
                    // Destroyed component: ignore it.
                }
            }
            return false;
        }
    }
}
