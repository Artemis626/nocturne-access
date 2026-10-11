using System;

namespace NocturneAccess.Systems
{
    /// <summary>
    /// What the repeat key (F1) says: the last announcement of whichever reader spoke last (0.0.67; until then each reader kept
    /// its own copy with a time stamp and the key compared eleven of them). A dialogue page only marks that dialogue spoke last:
    /// the key then says the page with the highlighted choice option (DialogueReader, ChoiceReader).
    /// </summary>
    internal static class RepeatBuffer
    {
        private static string _text = "";
        private static Func<string> _live;

        /// <summary>The last thing spoken was a dialogue page.</summary>
        internal static bool LastIsDialogue { get; private set; }

        /// <summary>The text to repeat, "" when none (or a live reader whose screen has closed).</summary>
        internal static string Text => _live?.Invoke() ?? _text;

        internal static void Record(string text)
        {
            _text = text ?? "";
            _live = null;
            LastIsDialogue = false;
        }

        /// <summary>A reader whose current state is read again at each press (the name entry screen).</summary>
        internal static void RecordLive(Func<string> live)
        {
            _text = "";
            _live = live;
            LastIsDialogue = false;
        }

        internal static void RecordDialogue()
        {
            _text = "";
            _live = null;
            LastIsDialogue = true;
        }
    }
}
