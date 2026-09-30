using MelonLoader;

namespace NocturneAccess.Core
{
    /// <summary>
    /// Thin wrapper over the mod's MelonLogger instance so every file logs the same way.
    /// Everything ends up in MelonLoader/Latest.log, which is the ground truth for testing.
    /// </summary>
    internal static class Log
    {
        private static MelonLogger.Instance _logger;

        internal static void Init(MelonLogger.Instance logger) => _logger = logger;

        internal static void Info(string message) => _logger?.Msg(message);

        internal static void Warn(string message) => _logger?.Warning(message);

        internal static void Error(string message) => _logger?.Error(message);

        /// <summary>Only written when VerboseLogging is on in NocturneAccess.cfg.</summary>
        internal static void Debug(string message)
        {
            if (Config.VerboseLoggingEnabled)
                _logger?.Msg("[DEBUG] " + message);
        }

        /// <summary>Mirror of an utterance. Grep Latest.log for [SPEECH] to see what the player heard.</summary>
        internal static void Speech(string text) => _logger?.Msg("[SPEECH] " + text);
    }
}
