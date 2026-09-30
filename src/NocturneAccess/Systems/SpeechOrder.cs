using NocturneAccess.Core;
using NocturneAccess.Systems.Dialogue;
using NocturneAccess.Systems.Menus;

namespace NocturneAccess.Systems
{
    /// <summary>
    /// Keeps a screen's first line behind the text that introduces it.
    ///
    /// A leader (a dialogue page, or a prompt such as "Load File 1?") can arrive after the menu or
    /// choice it introduces has already settled. Terminal, 0.0.6 test (30 Sep 2026): the menu's first
    /// highlight came at 08.079, the engine requested the question at 08.117, but the question's lines
    /// were only written about 400 ms later, so "Save, 1 of 3" was spoken before the question.
    ///
    /// A follower (a menu, the save list, the save Yes/No) holds its first, queued announcement while a
    /// leader is due, then queues behind it. MaxHoldMs guards against a leader that never speaks.
    /// Cursor moves never wait.
    /// </summary>
    internal static class SpeechOrder
    {
        internal const long MaxHoldMs = 1500;

        private const string Tag = "[Order]";

        /// <summary>
        /// True while the follower should keep waiting. holdSince is the follower's own state:
        /// -1 when not holding; set it back to -1 after announcing.
        /// </summary>
        internal static bool Hold(string follower, ref long holdSince, long now)
        {
            string leader = DialogueReader.PageDue ? "dialogue page" : AnnouncementReader.AnyDue ? "prompt" : null;
            if (leader == null)
                return false;

            if (holdSince < 0)
            {
                holdSince = now;
                Log.Info($"{Tag} {follower} waits for the {leader}.");
                return true;
            }
            if (now - holdSince < MaxHoldMs)
                return true;

            Log.Info($"{Tag} {follower} stopped waiting for the {leader} after {MaxHoldMs} ms.");
            return false;
        }
    }
}
