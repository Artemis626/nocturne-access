using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// Speaks the name entry screen (player, Yuko, classmate): the letter under the cursor, each letter
    /// typed or deleted, the character set, and the confirmation with the whole name.
    ///
    /// Sources (20:44 session, docs/class-index.md "Name entry"), all under Canvas_UI/nameUI_oubei(Clone)/:
    /// - Letter grid namelist/name_alpabet/name_alphabet_box/namelist_boxNN/Text_letterNNTM: the letter
    ///   under the cursor blinks, rewritten with material TMC12 about every other frame. A TMC12 write on a
    ///   different letter (or a new letter in the same place) is a cursor move.
    /// - Typed letters nameinput/namefull|namenick/stretch/nameinput_letterNN/TextTM (family 01-10, given
    ///   11-20, nickname 01-10). A deleted letter is written blank, which the router never passes on, so the
    ///   known letter components are read every 100 ms to hear deletions too.
    /// - Character sets namemode/namelr_menu/namemodeNN/stretch_on/Text: the visible one is selected.
    /// - Confirmation nameinput/nameconfirm/...: "Full Name", "Nickname", the labels and the letters (two
    ///   components per letter, one blank), with the question namechoice/talk_window/Text01TM. Its Yes/No is
    ///   the MenuReader spec "Name prompt", which waits behind this summary (ConfirmDue, Systems/SpeechOrder.cs).
    /// "Enter a name." / "Enter a nickname." are AnnouncementReader prompts.
    /// The whole name is built by joining the game's own letter components in screen order.
    /// </summary>
    internal static class NameEntryReader
    {
        private const string Tag = "[Name]";
        private const string Root = "Canvas_UI/nameUI_oubei(Clone)/";
        private const string Cursor = "<material=\"TMC12\">";
        private const long SettleMs = 60;
        private const long LetterPollMs = 100;
        private const long OpeningQuietMs = 600;
        private const long FirstCursorDelayMs = 400;
        private const long ConfirmSettleMs = 150;
        private const long ClosePollMs = 500;

        private static readonly Regex GridPath = new Regex(
            @"^Canvas_UI/nameUI_oubei\(Clone\)/namelist/name_alpabet/name_alphabet_box/namelist_box\d\d/Text_letter\d\dTM$", RegexOptions.Compiled);
        private static readonly Regex InputPath = new Regex(
            @"^Canvas_UI/nameUI_oubei\(Clone\)/nameinput/(?<field>namefull|namenick)/stretch/nameinput_letter(?<n>\d\d)/TextTM$", RegexOptions.Compiled);
        private static readonly Regex ModePath = new Regex(
            @"^Canvas_UI/nameUI_oubei\(Clone\)/namemode/namelr_menu/namemode(?<n>\d\d)/stretch_on/Text$", RegexOptions.Compiled);
        private static readonly Regex ConfirmLetterPath = new Regex(
            @"^Canvas_UI/nameUI_oubei\(Clone\)/nameinput/nameconfirm/(?<field>namefull|namenick)/stretch/nameinput_letter(?<n>\d\d)/TextTM$", RegexOptions.Compiled);

        private sealed class Letter
        {
            public TMP_Text Component;
            public string Field;   // "family", "given", "nick"
            public int Index;      // position within its field, from 1
            public string Last = "";
        }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Dictionary<int, Letter> Inputs = new Dictionary<int, Letter>();                 // by component ID
        private static readonly List<(string Field, int Index, TMP_Text Component)> ConfirmLetters = new List<(string, int, TMP_Text)>();
        private static readonly Dictionary<int, TMP_Text> Modes = new Dictionary<int, TMP_Text>();              // by set number
        private static readonly Dictionary<string, TMP_Text> Labels = new Dictionary<string, TMP_Text>();       // confirm texts by path end

        private static bool _open;
        private static long _openedAt;
        private static long _lastLetterPoll;
        private static long _lastClosePoll;
        private static TMP_Text _cursorComponent;
        private static string _cursorText = "";
        private static TMP_Text _pendingCursor;
        private static string _pendingCursorText;
        private static long _pendingCursorAt;
        private static bool _cursorAnnounced;
        private static long _holdSince = -1;
        private static int _mode = -1;
        private static TMP_Text _confirmTitle;
        private static bool _confirmPending;
        private static bool _confirmSpoken;
        private static long _confirmAt;
        private static bool _confirmWasVisible;
        private static long _lastBlink;
        private static string _buttonData = "";

        /// <summary>The confirmation summary is written but not spoken yet: its Yes/No waits behind it.</summary>
        internal static bool ConfirmDue => _confirmPending && Clock.ElapsedMilliseconds - _confirmAt < SpeechOrder.MaxHoldMs;

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            TmpRouter.Register("NameEntryReader", Classify);
        }

        private static TmpRouter.Handler Classify(string path)
        {
            if (!path.StartsWith(Root, StringComparison.Ordinal))
                return null;

            if (GridPath.IsMatch(path))
                return OnGrid;

            Match m = InputPath.Match(path);
            if (m.Success)
            {
                int n = int.Parse(m.Groups["n"].Value);
                string field = m.Groups["field"].Value == "namenick" ? "nick" : n <= 10 ? "family" : "given";
                int index = field == "given" ? n - 10 : n;
                return (component, raw) =>
                {
                    Opened();
                    int id = component.GetInstanceID();
                    if (!Inputs.ContainsKey(id))
                        Inputs[id] = new Letter { Component = component, Field = field, Index = index };
                };
            }

            m = ModePath.Match(path);
            if (m.Success)
            {
                int n = int.Parse(m.Groups["n"].Value);
                return (component, raw) => { Opened(); Modes[n] = component; };
            }

            m = ConfirmLetterPath.Match(path);
            if (m.Success)
            {
                int n = int.Parse(m.Groups["n"].Value);
                string field = m.Groups["field"].Value == "namenick" ? "nick" : n <= 10 ? "family" : "given";
                int index = field == "given" ? n - 10 : n;
                return (component, raw) =>
                {
                    if (!ConfirmLetters.Any(c => c.Component.GetInstanceID() == component.GetInstanceID()))
                        ConfirmLetters.Add((field, index, component));
                };
            }

            string end = path.Substring(Root.Length);
            switch (end)
            {
                case "nameinput/nameconfirm/Text_fullTM":
                    return (component, raw) =>
                    {
                        Opened();
                        _confirmTitle = component;
                        Labels[end] = component;
                        if (!_confirmPending && !_confirmSpoken)
                        {
                            _confirmPending = true;
                            _confirmAt = Clock.ElapsedMilliseconds;
                        }
                    };
                case "nameinput/namefull/stretch/stretch_sur/TextTM":    // "Family:" beside the typed name (22:05 session)
                case "nameinput/namefull/stretch/stretch_name/TextTM":   // "Given:"
                case "nameinput/nameconfirm/Text_nickTM":
                case "nameinput/nameconfirm/namefull/stretch/stretch_sur/TextTM":
                case "nameinput/nameconfirm/namefull/stretch/stretch_name/TextTM":
                case "namechoice/talk_window/Text01TM":
                    return (component, raw) => Labels[end] = component;
                default:
                    return null;
            }
        }

        private static void Opened()
        {
            if (_open)
                return;
            _open = true;
            _openedAt = Clock.ElapsedMilliseconds;
            _cursorComponent = null;
            _cursorText = "";
            _cursorAnnounced = false;
            _mode = -1;
            _confirmPending = false;
            _confirmSpoken = false;
            Log.Info($"{Tag} Opened.");
        }

        private static void OnGrid(TMP_Text component, string raw)
        {
            if (!raw.Contains(Cursor))
                return;
            Opened();
            _lastBlink = Clock.ElapsedMilliseconds;
            string text = TextUtil.Clean(raw);
            if (text.Length == 0)
                return;
            bool same = _pendingCursor != null
                ? _pendingCursor.GetInstanceID() == component.GetInstanceID() && _pendingCursorText == text
                : _cursorComponent != null && _cursorComponent.GetInstanceID() == component.GetInstanceID() && _cursorText == text;
            if (same)
                return;   // the blink rewrites the same letter every other frame
            _pendingCursor = component;
            _pendingCursorText = text;
            _pendingCursorAt = Clock.ElapsedMilliseconds;
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            if (!_open)
                return;
            long now = Clock.ElapsedMilliseconds;
            try
            {
                if (_pendingCursor != null && now - _pendingCursorAt >= SettleMs)
                    AnnounceCursor(now);

                if (now - _lastLetterPoll >= LetterPollMs)
                {
                    _lastLetterPoll = now;
                    PollLetters(now);
                    PollMode(now);
                    LogButtonData(now);
                    bool confirmVisible = UiUtil.IsVisible(_confirmTitle);
                    if (confirmVisible && !_confirmWasVisible && !_confirmPending && !_confirmSpoken)
                    {
                        _confirmPending = true;   // shown again after "No": no new write, so watch it
                        _confirmAt = now;
                    }
                    _confirmWasVisible = confirmVisible;
                }

                if (_confirmPending && now - _confirmAt >= ConfirmSettleMs && UiUtil.IsVisible(_confirmTitle))
                    AnnounceConfirmation();

                if (now - _lastClosePoll >= ClosePollMs)
                {
                    _lastClosePoll = now;
                    if (_confirmSpoken && !UiUtil.IsVisible(_confirmTitle))
                        _confirmSpoken = false;   // back to editing, or done
                    if (!AnyVisible())
                    {
                        _open = false;
                        Inputs.Clear();
                        ConfirmLetters.Clear();
                        Log.Info($"{Tag} Closed.");
                    }
                }
            }
            catch (Exception ex)
            {
                _pendingCursor = null;
                Log.Error($"{Tag} {ex.Message}");
            }
        }

        private static void AnnounceCursor(long now)
        {
            if (!UiUtil.IsVisible(_pendingCursor))
                return;
            bool first = !_cursorAnnounced;
            // The cursor starts blinking about 200 ms before "Enter a name." is written (20:44 session).
            if (first && now - _openedAt < FirstCursorDelayMs)
                return;
            if (first && SpeechOrder.Hold("Name entry", ref _holdSince, now))
                return;
            _holdSince = -1;

            _cursorComponent = _pendingCursor;
            _cursorText = _pendingCursorText;
            _pendingCursor = null;
            _cursorAnnounced = true;
            string letter = TextUtil.SpeakableCharacter(_cursorText);
            string prefilled = first ? PrefilledName() : "";
            Say(prefilled.Length > 0 ? $"{prefilled}. {letter}" : letter, interrupt: !first);
        }

        /// <summary>
        /// A name already filled in when the screen opens (Yuko's "Takao" "Yuko", 22:05 session), with the
        /// game's labels beside the fields: "Family: Takao. Given: Yuko". Empty when nothing is typed.
        /// </summary>
        private static string PrefilledName()
        {
            var parts = new List<string>();
            AddField(parts, LabelText("nameinput/namefull/stretch/stretch_sur/TextTM"), TypedWord("family"));
            AddField(parts, LabelText("nameinput/namefull/stretch/stretch_name/TextTM"), TypedWord("given"));
            AddField(parts, "Nickname", TypedWord("nick"));
            return string.Join(". ", parts);
        }

        /// <summary>
        /// Diagnostic for the control buttons (forward, back, delete, done): image buttons that write no text
        /// (22:05 session). Logs the name-entry data whenever it changes, to find which value says which button
        /// has the cursor: funccursorPos, box_num, letter_num, cursor_str of Nme_Draw_EFGIS (the Western
        /// variant) and Nme_Draw, each button's active state and scale, and whether the letter grid still
        /// blinks (it should stop while a button has the cursor). All names: proxy Nme_Draw, Nme_Draw_EFGIS.
        /// </summary>
        private static void LogButtonData(long now)
        {
            string data;
            try
            {
                data = $"blink {(now - _lastBlink < 300 ? "yes" : "no")}; "
                     + $"EFGIS func {Nme_Draw_EFGIS.funccursorPos} box {Nme_Draw_EFGIS.box_num} letter {Nme_Draw_EFGIS.letter_num} str \"{Nme_Draw_EFGIS.cursor_str}\" "
                     + $"right {Button(Nme_Draw_EFGIS.namelist_rightbutton)} left {Button(Nme_Draw_EFGIS.namelist_leftbutton)} "
                     + $"delete {Button(Nme_Draw_EFGIS.namelist_deletebutton)} end {Button(Nme_Draw_EFGIS.namelist_endbutton)} "
                     + $"func image {Button(Nme_Draw_EFGIS.functionImage)}; "
                     + $"Draw func {Nme_Draw.funccursorPos} box {Nme_Draw.box_num} letter {Nme_Draw.letter_num} str \"{Nme_Draw.cursor_str}\"";
            }
            catch (Exception ex)
            {
                data = "error " + ex.Message;
            }
            if (data == _buttonData)
                return;
            _buttonData = data;
            Log.Info($"{Tag} Data: {data}");
        }

        private static string Button(UnityEngine.GameObject obj)
        {
            if (obj == null)
                return "none";
            UnityEngine.Transform t = obj.transform;
            return $"({(obj.activeInHierarchy ? "on" : "off")} scale {t.localScale.x:0.00} at {t.position.x:0},{t.position.y:0})";
        }

        /// <summary>Reads the known typed-letter components: a new letter is echoed, a vanished one said as deleted.</summary>
        private static void PollLetters(long now)
        {
            bool quiet = now - _openedAt < OpeningQuietMs;   // letters filled in when the screen opens (Yuko's default name)
            foreach (Letter l in Inputs.Values)
            {
                string text = UiUtil.IsVisible(l.Component) ? TextUtil.Clean(l.Component.text) : l.Last;
                if (text == l.Last)
                    continue;
                string old = l.Last;
                l.Last = text;
                if (quiet)
                    continue;
                if (text.Length > 0)
                    Say(TextUtil.SpeakableCharacter(text), interrupt: true);
                else if (old.Length > 0)
                    Say("Deleted " + TextUtil.SpeakableCharacter(old), interrupt: true);
            }
        }

        private static void PollMode(long now)
        {
            foreach (KeyValuePair<int, TMP_Text> kv in Modes)
            {
                if (!UiUtil.IsVisible(kv.Value) || kv.Key == _mode)
                    continue;
                bool first = _mode < 0;
                _mode = kv.Key;
                if (first)
                    return;   // the set at opening is not news
                string text = TextUtil.Clean(kv.Value.text);
                string kind = kv.Key == 1 ? "letters" : kv.Key == 2 ? "accented letters" : kv.Key == 3 ? "symbols" : "";
                Say(kind.Length > 0 ? $"{text}, {kind}" : text, interrupt: true);
                return;
            }
        }

        /// <summary>"Full Name. Family: Soul. Given: Artemis. Nickname: Art. > Is this okay?"</summary>
        private static void AnnounceConfirmation()
        {
            _confirmPending = false;
            _confirmSpoken = true;

            var parts = new List<string>();
            string full = LabelText("nameinput/nameconfirm/Text_fullTM");
            if (full.Length > 0)
                parts.Add(full);
            AddField(parts, LabelText("nameinput/nameconfirm/namefull/stretch/stretch_sur/TextTM"), ConfirmWord("family"));
            AddField(parts, LabelText("nameinput/nameconfirm/namefull/stretch/stretch_name/TextTM"), ConfirmWord("given"));
            string nick = ConfirmWord("nick");
            if (nick.Length > 0)
                AddField(parts, LabelText("nameinput/nameconfirm/Text_nickTM"), nick);
            string question = LabelText("namechoice/talk_window/Text01TM");
            if (question.Length > 0)
                parts.Add(question);

            string text = string.Join(". ", parts.Select(p => p.TrimEnd('.')));
            Log.Info($"{Tag} Confirmation.");
            Say(text, interrupt: false);
        }

        private static void AddField(List<string> parts, string label, string value)
        {
            if (value.Length == 0)
                return;
            label = label.TrimEnd(':');
            parts.Add(label.Length > 0 ? $"{label}: {value}" : value);
        }

        /// <summary>A field of the confirmation, joined from its visible, non-blank letter components in order.</summary>
        private static string ConfirmWord(string field)
        {
            var sb = new StringBuilder();
            foreach (IGrouping<int, (string Field, int Index, TMP_Text Component)> slot in
                     ConfirmLetters.Where(c => c.Field == field).GroupBy(c => c.Index).OrderBy(g => g.Key))
            {
                string letter = slot.Where(c => UiUtil.IsVisible(c.Component)).Select(c => TextUtil.Clean(c.Component.text))
                                    .FirstOrDefault(t => t.Length > 0) ?? "";
                sb.Append(letter);
            }
            return sb.ToString();
        }

        /// <summary>A field being typed, joined from its letter components in order.</summary>
        private static string TypedWord(string field)
        {
            var sb = new StringBuilder();
            foreach (Letter l in Inputs.Values.Where(l => l.Field == field && UiUtil.IsVisible(l.Component)).OrderBy(l => l.Index))
                sb.Append(TextUtil.Clean(l.Component.text));
            return sb.ToString();
        }

        private static string CurrentState()
        {
            var parts = new List<string>();
            string family = TypedWord("family"), given = TypedWord("given"), nick = TypedWord("nick");
            parts.Add($"Family: {(family.Length > 0 ? family : "empty")}");
            parts.Add($"Given: {(given.Length > 0 ? given : "empty")}");
            if (nick.Length > 0)
                parts.Add($"Nickname: {nick}");
            if (_cursorText.Length > 0)
                parts.Add("Cursor on " + TextUtil.SpeakableCharacter(_cursorText));
            return string.Join(". ", parts) + ".";
        }

        private static string LabelText(string key)
            => Labels.TryGetValue(key, out TMP_Text c) && UiUtil.IsVisible(c) ? TextUtil.Clean(c.text) : "";

        private static void Say(string text, bool interrupt)
        {
            RepeatBuffer.RecordLive(() => _open ? CurrentState() : "");   // the name so far and the letter under the cursor
            SpeechManager.Say(text, interrupt: interrupt, force: true);
        }

        private static bool AnyVisible()
        {
            if (UiUtil.IsVisible(_cursorComponent) || UiUtil.IsVisible(_confirmTitle))
                return true;
            return Inputs.Values.Any(l => UiUtil.IsVisible(l.Component)) || Modes.Values.Any(m => UiUtil.IsVisible(m));
        }
    }
}
