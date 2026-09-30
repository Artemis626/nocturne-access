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
