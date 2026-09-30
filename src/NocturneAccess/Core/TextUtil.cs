using System.Text.RegularExpressions;

namespace NocturneAccess.Core
{
    /// <summary>
    /// Cleans the game's own TextMeshPro strings for speech. Nothing is reconstructed:
    /// the text is the exact string the game wrote, minus formatting tags.
    ///
    /// Formats seen in probe session 1 (docs/class-index.md, UI object paths):
    /// - Font tags such as &lt;material="MsgFont0"&gt; and &lt;material="TMC09"&gt;.
    /// - The typewriter hides characters not yet shown with &lt;alpha=#00&gt; before each one;
    ///   the full line is present from the first write.
    /// - Empty dialogue lines hold the invisible placeholder &lt;alpha=#00&gt;あああああ&lt;/alpha&gt;.
    /// </summary>
    internal static class TextUtil
    {
        private const string HiddenPlaceholderText = "あああああ";
        private const string TypewriterTag = "<alpha=#00>";

        private static readonly Regex Tags = new Regex("<[^<>]{1,80}>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>True for the invisible placeholder the game writes into an empty line.</summary>
        internal static bool IsPlaceholder(string raw)
            => raw != null && raw.Contains(TypewriterTag) && raw.Contains(HiddenPlaceholderText);

        /// <summary>Removes only the typewriter's alpha tags, keeping other formatting (for the probe).</summary>
        internal static string RemoveTypewriter(string raw)
            => raw?.Replace(TypewriterTag, "").Replace("</alpha>", "") ?? string.Empty;

        /// <summary>
        /// A single character as it should be heard: "capital A" for upper-case letters, a name for
        /// punctuation a screen reader may skip at low punctuation levels. Longer text is returned unchanged.
        /// </summary>
        internal static string SpeakableCharacter(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length != 1)
                return text ?? string.Empty;
            char c = text[0];
            if (char.IsLetter(c) && char.IsUpper(c))
                return "capital " + text;
            switch (c)
            {
                case ' ': return "space";
                case '.': return "period";
                case ',': return "comma";
                case '-': return "dash";
                case '\'': return "apostrophe";
                case '"': return "quote";
                case '!': return "exclamation mark";
                case '?': return "question mark";
                case '&': return "ampersand";
                case '#': return "number sign";
                case '+': return "plus";
                case '=': return "equals";
                case '/': return "slash";
                case ':': return "colon";
                case ';': return "semicolon";
                case '(': return "left paren";
                case ')': return "right paren";
                case '*': return "star";
                case '@': return "at";
                case '_': return "underscore";
                case '~': return "tilde";
                default: return text;
            }
        }

        /// <summary>Removes every rich-text tag and collapses whitespace and line breaks.</summary>
        internal static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;
            string text = Tags.Replace(raw, "");
            return Spaces.Replace(text, " ").Trim();
        }
    }
}
