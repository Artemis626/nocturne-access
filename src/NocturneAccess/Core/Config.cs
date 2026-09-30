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

        // Dialogue
        internal static MelonPreferences_Entry<bool> DialogueEnabled { get; private set; }
        internal static MelonPreferences_Entry<bool> SpeakerEveryPage { get; private set; }
        internal static MelonPreferences_Entry<bool> ChoicesEnabled { get; private set; }

        // Menus
        internal static MelonPreferences_Entry<bool> MenusEnabled { get; private set; }

        // Battle
        internal static MelonPreferences_Entry<bool> BattleEnabled { get; private set; }
        internal static MelonPreferences_Entry<bool> SpeakHpChanges { get; private set; }

        // Keys (names are UnityEngine.KeyCode values, e.g. F8, Backslash, Alpha1)
        internal static MelonPreferences_Entry<string> KeyRepeat { get; private set; }
        internal static MelonPreferences_Entry<string> KeyStatus { get; private set; }
        internal static MelonPreferences_Entry<string> KeyBattleStatus { get; private set; }
        internal static MelonPreferences_Entry<string> KeyDumpUi { get; private set; }
        internal static MelonPreferences_Entry<string> KeyToggleProbe { get; private set; }

        // Debug
        internal static MelonPreferences_Entry<bool> VerboseLogging { get; private set; }
        internal static MelonPreferences_Entry<bool> ProbeEnabled { get; private set; }

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

            MelonPreferences_Category dialogue = MelonPreferences.CreateCategory("NocturneAccess_Dialogue", "Dialogue");
            dialogue.SetFilePath(FilePath, true, false);
            DialogueEnabled = dialogue.CreateEntry("Enabled", true, "Read dialogue",
                "Speaks each dialogue page as it appears. Needs a restart.");
            SpeakerEveryPage = dialogue.CreateEntry("SpeakerEveryPage", true, "Speaker on every page",
                "Say the speaker's name before every page (default). When false, it is said only when the speaker changes or the window reopens.");
            ChoicesEnabled = dialogue.CreateEntry("Choices", true, "Read choices",
                "Speaks dialogue choices and the highlighted option. Needs a restart.");

            MelonPreferences_Category menus = MelonPreferences.CreateCategory("NocturneAccess_Menus", "Menus");
            menus.SetFilePath(FilePath, true, false);
            MenusEnabled = menus.CreateEntry("Enabled", true, "Read menus",
                "Speaks the highlighted item of the pause, terminal and title menus, the save/load file list, prompts and Yes/No. Needs a restart.");

            MelonPreferences_Category battle = MelonPreferences.CreateCategory("NocturneAccess_Battle", "Battle");
            battle.SetFilePath(FilePath, true, false);
            BattleEnabled = battle.CreateEntry("Enabled", true, "Read battles",
                "Speaks the battle start with the enemies, actions, targets and results, and HP changes. Needs a restart.");
            SpeakHpChanges = battle.CreateEntry("SpeakHpChanges", true, "Speak HP changes",
                "After each hit or heal, say the unit's HP (from game data).");

            MelonPreferences_Category keys = MelonPreferences.CreateCategory("NocturneAccess_Keys", "Keys");
            keys.SetFilePath(FilePath, true, false);
            KeyRepeat = keys.CreateEntry("Repeat", "F1", "Repeat key",
                "Repeats whichever came last: the menu item, or the dialogue page (plus the highlighted option while a choice is open).");
            KeyStatus = keys.CreateEntry("Status", "F8", "Status key",
                "Speaks whether the mod is running and which speech backend it uses.");
            KeyBattleStatus = keys.CreateEntry("BattleStatus", "F2", "Battle status key",
                "In battle: Press Turns, then every enemy and party member with HP and MP.");
            KeyDumpUi = keys.CreateEntry("DumpUi", "F6", "Dump UI key",
                "Debug: writes every text currently visible on screen to Latest.log.");
            KeyToggleProbe = keys.CreateEntry("ToggleProbe", "F7", "Toggle probe key",
                "Debug: pauses or resumes the text probe (only when Probe = true).");

            MelonPreferences_Category debug = MelonPreferences.CreateCategory("NocturneAccess_Debug", "Debug");
            debug.SetFilePath(FilePath, true, false);
            VerboseLogging = debug.CreateEntry("VerboseLogging", false, "Verbose logging",
                "Writes [DEBUG] lines to Latest.log, including lines dropped by the dedupe window.");
            ProbeEnabled = debug.CreateEntry("Probe", true, "Text probe",
                "Research tool: logs every UI text and message-engine call to Latest.log. Needs a restart. On during development; will default to off for players.");

            // Write the file now so new settings appear with their defaults.
            speech.SaveToFile(false);
            dialogue.SaveToFile(false);
            menus.SaveToFile(false);
            battle.SaveToFile(false);
            keys.SaveToFile(false);
            debug.SaveToFile(false);
        }
    }
}
