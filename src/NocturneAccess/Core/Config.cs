using System.IO;
using MelonLoader;
using MelonLoader.Utils;

namespace NocturneAccess.Core
{
    /// <summary>
    /// User settings in UserData/NocturneAccess.cfg (MelonPreferences, TOML format).
    /// The file is created with defaults on first launch and can be edited while the game is closed.
    /// The path is absolute on purpose: a bare file name makes MelonLoader's file watcher throw
    /// ("The directory name is invalid"), as seen in Latest.log for another mod's config.
    /// </summary>
    internal static class Config
    {
        internal const string FileName = "NocturneAccess.cfg";

        internal static string FilePath { get; private set; }

        // Speech
        internal static MelonPreferences_Entry<string> SpeechBackend { get; private set; }
        internal static MelonPreferences_Entry<bool> BrailleOutput { get; private set; }
        internal static MelonPreferences_Entry<int> DedupeWindowMs { get; private set; }

        // Keys (names are UnityEngine.KeyCode values, e.g. F8, Backslash, Alpha1)
        internal static MelonPreferences_Entry<string> KeyStatus { get; private set; }

        // Debug
        internal static MelonPreferences_Entry<bool> VerboseLogging { get; private set; }

        internal static bool VerboseLoggingEnabled => VerboseLogging?.Value ?? false;

        internal static void Init()
        {
            FilePath = Path.Combine(MelonEnvironment.UserDataDirectory, FileName);

            // Order matters: SetFilePath before CreateEntry, so each entry picks up the saved value.
            MelonPreferences_Category speech = MelonPreferences.CreateCategory("NocturneAccess_Speech", "Speech");
            speech.SetFilePath(FilePath, true, false);
            SpeechBackend = speech.CreateEntry("Backend", "auto", "Speech backend",
                "auto, nvda, tolk or log. auto uses NVDA directly, then Tolk if Tolk.dll is present, then log only.");
            BrailleOutput = speech.CreateEntry("Braille", true, "Braille output",
                "Also send each spoken line to the braille display.");
            DedupeWindowMs = speech.CreateEntry("DedupeWindowMs", 250, "Dedupe window (ms)",
                "An identical line repeated within this many milliseconds is spoken once. Hotkeys always speak.");

            MelonPreferences_Category keys = MelonPreferences.CreateCategory("NocturneAccess_Keys", "Keys");
            keys.SetFilePath(FilePath, true, false);
            KeyStatus = keys.CreateEntry("Status", "F8", "Status key",
                "Speaks whether the mod is running and which speech backend it uses.");

            MelonPreferences_Category debug = MelonPreferences.CreateCategory("NocturneAccess_Debug", "Debug");
            debug.SetFilePath(FilePath, true, false);
            VerboseLogging = debug.CreateEntry("VerboseLogging", false, "Verbose logging",
                "Writes [DEBUG] lines to Latest.log, including lines dropped by the dedupe window.");

            // Write the file now so new settings appear with their defaults.
            speech.SaveToFile(false);
            keys.SaveToFile(false);
            debug.SaveToFile(false);
        }
    }
}
