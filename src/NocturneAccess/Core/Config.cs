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
        internal static MelonPreferences_Entry<bool> SpeakHpLeft { get; private set; }

        // Navigation
        internal static MelonPreferences_Entry<bool> NavDiagnostics { get; private set; }
        internal static MelonPreferences_Entry<int> NavTraceIntervalMs { get; private set; }
        internal static MelonPreferences_Entry<bool> UnlockFirstPerson { get; private set; }
        internal static MelonPreferences_Entry<bool> PadInjection { get; private set; }
        internal static MelonPreferences_Entry<bool> BlobberOnAtStart { get; private set; }
        internal static MelonPreferences_Entry<int> BlobberCellSize { get; private set; }
        internal static MelonPreferences_Entry<bool> DoorKinds { get; private set; }

        // Keys (names are UnityEngine.KeyCode values, e.g. F8, Backslash, Alpha1)
        internal static MelonPreferences_Entry<string> KeyRepeat { get; private set; }
        internal static MelonPreferences_Entry<string> KeyWhereAmI { get; private set; }
        internal static MelonPreferences_Entry<string> KeyBattleStatus { get; private set; }
        internal static MelonPreferences_Entry<string> KeyBattleVerbosity { get; private set; }
        internal static MelonPreferences_Entry<string> KeyDumpUi { get; private set; }
        internal static MelonPreferences_Entry<string> KeyToggleProbe { get; private set; }
        internal static MelonPreferences_Entry<string> KeyNavDump { get; private set; }
        internal static MelonPreferences_Entry<string> KeyBattleDump { get; private set; }
        internal static MelonPreferences_Entry<string> KeyBattleEnemies { get; private set; }
        internal static MelonPreferences_Entry<string> KeyBattleLog { get; private set; }
        internal static MelonPreferences_Entry<string> KeyBlobber { get; private set; }
        internal static MelonPreferences_Entry<string> KeyCentre { get; private set; }
        internal static MelonPreferences_Entry<string> KeyPoiNext { get; private set; }
        internal static MelonPreferences_Entry<string> KeyPoiPrevious { get; private set; }
        internal static MelonPreferences_Entry<string> KeyPoiWalk { get; private set; }
        internal static MelonPreferences_Entry<string> KeyPoiGuide { get; private set; }
        internal static MelonPreferences_Entry<string> KeyPoiFilterNext { get; private set; }
        internal static MelonPreferences_Entry<string> KeyPoiFilterPrevious { get; private set; }
        internal static MelonPreferences_Entry<bool> PoiMapOrder { get; private set; }
        internal static MelonPreferences_Entry<bool> AreaRoutes { get; private set; }
        internal static MelonPreferences_Entry<bool> WallBumpSound { get; private set; }
        internal static MelonPreferences_Entry<string> KeyAreaRoutes { get; private set; }

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
            SpeakHpLeft = battle.CreateEntry("SpeakHpLeft", false, "Speak HP left after actions",
                "After each hit or heal, also say the HP left (\"Zhen 1: 27 damage, HP 38 of 65.\"); off: \"Zhen 1: 27 damage.\" A unit brought to 0 says \"0 HP\" either way. F8 toggles it in game. F2, F3 and F5 always give HP.");

            MelonPreferences_Category navigation = MelonPreferences.CreateCategory("NocturneAccess_Navigation", "Navigation");
            navigation.SetFilePath(FilePath, true, false);
            NavDiagnostics = navigation.CreateEntry("Diagnostics", true, "Navigation diagnostics",
                "Development: logs the player's position, facing and pad buttons while exploring, and place and event changes. Needs a restart.");
            // "TraceIntervalMsV2" (1000) replaces the entry "TraceIntervalMs" (250) of 0.0.30 and earlier, so saved configs pick
            // up the new default: the 250 ms trace lines were most of a session log.
            NavTraceIntervalMs = navigation.CreateEntry("TraceIntervalMsV2", 1000, "Trace interval (ms)",
                "With Diagnostics on: at most one position line per this many milliseconds, only when something changed.");
            UnlockFirstPerson = navigation.CreateEntry("UnlockFirstPerson", true, "Unlock first person",
                "Lets V (Toggle POV) switch to first person on a first playthrough. Held in memory only: the game's first-person flag is never written, so saves are unchanged. Needs a restart.");
            PadInjection = navigation.CreateEntry("PadInjection", true, "Pad injection",
                "Lets the mod press the game's own pad buttons during the field update (step test, later blobber moves). Turn off if the game crashes at start. Needs a restart.");
            BlobberOnAtStart = navigation.CreateEntry("BlobberOnAtStart", true, "Blobber mode at start",
                "Blobber mode is on when the game starts (F4 switches it).");
            BlobberCellSize = navigation.CreateEntry("BlobberCellSize", 600, "Blobber cell size",
                "Development: one step in game units (600 = one corridor width in the Underground Facility).");
            DoorKinds = navigation.CreateEntry("DoorKinds", true, "Door kinds",
                "Page Up / Page Down, Home and End say whether a plain door leads to a room, a passage or an exit (from the game's door table). Turn off to go back to plain \"Door\".");
            PoiMapOrder = navigation.CreateEntry("PoiMapOrder", true, "Points of interest in map order",
                "Page Up / Page Down go through an area's points in a fixed order, north to south and west to east, so each keeps its number while you walk. Turn off for the old order, nearest first (it changes as you move).");
            AreaRoutes = navigation.CreateEntry("AreaRoutes", false, "Routes through other areas",
                "Page Up / Page Down also list points of the area reached only by going through other areas, by doors you have gone through (\"via 1 other area\"); Home walks there door by door, End gives the way to the first door. F7 toggles it in game.");
            WallBumpSound = navigation.CreateEntry("WallBumpSound", true, "Wall bump sound",
                "A short thud when you walk into a wall and stop (the game has no sound of its own for it).");

            MelonPreferences_Category keys = MelonPreferences.CreateCategory("NocturneAccess_Keys", "Keys");
            keys.SetFilePath(FilePath, true, false);
            KeyRepeat = keys.CreateEntry("Repeat", "F1", "Repeat key",
                "Repeats whichever came last: the menu item, or the dialogue page (plus the highlighted option while a choice is open).");
            KeyWhereAmI = keys.CreateEntry("WhereAmI", "Delete", "Where am I key",
                "Speaks where you are: the location banner (area, or the district on the world map), the facing in first person, and the nearest point of interest.");
            KeyBattleVerbosity = keys.CreateEntry("BattleVerbosity", "F8", "Battle verbosity key",
                "Turns on or off the HP left spoken after each hit or heal in battle (status keys always give HP).");
            KeyBattleStatus = keys.CreateEntry("BattleStatus", "F2", "Battle status key",
                "In battle: whose turn it is and the press turns left, then each party member with HP and MP.");
            KeyBattleEnemies = keys.CreateEntry("BattleEnemies", "F3", "Battle enemies key",
                "In battle: each enemy with HP, and its affinities once Analyze has shown them. Shares F3 with centring, which works only outside battle.");
            KeyBattleLog = keys.CreateEntry("BattleLog", "F5", "Battle log key",
                "Battle: what happened in the previous side's turn and the current one so far (actions, damage, weakness and critical hits). Kept until the next battle.");
            KeyDumpUi = keys.CreateEntry("DumpUi", "F6", "Dump UI key",
                "Debug: writes every text currently visible on screen to Latest.log.");
            // "ProbeToggle" (Shift+F7) replaces the entry "ToggleProbe" (F7) of 0.0.55 and earlier, so saved configs pick up the new
            // default: F7 became the routes through other areas (0.0.56, Francesco: a function key; debug keys may use Shift).
            KeyToggleProbe = keys.CreateEntry("ProbeToggle", "Shift+F7", "Toggle probe key",
                "Debug: pauses or resumes the text probe (only when TextProbe = true).");
            // "NavigationDump" (Shift+F6) replaces the 0.0.12 entry "NavDump" (F5), so saved configs pick up the new default.
            KeyNavDump = keys.CreateEntry("NavigationDump", "Shift+F6", "Navigation dump key",
                "Debug: writes the navigation data of the current area (position, place names, walls, auto-map, people, boxes, event volumes) to UserData/NocturneAccess/NavDumps, with a summary in Latest.log.");
            KeyBattleDump = keys.CreateEntry("BattleDump", "Shift+F2", "Battle dump key",
                "Debug: writes the battle data of this moment (turn, press turns, every unit with level, status, skills and buffs, formation, target selection) to UserData/NocturneAccess/BattleDumps, with a summary in Latest.log.");
            KeyBlobber = keys.CreateEntry("Blobber", "F4", "Blobber mode key",
                "Turns blobber mode on or off. In first person every game key stays as it is; letting go of W carries the walk on to the centre of the next square, and changes around the player are spoken.");
            KeyCentre = keys.CreateEntry("Centre", "F3", "Centre key",
                "Blobber mode, first person: walks to the centre of the current square through the game's own movement, then says the facing and the walls around.");
            KeyPoiNext = keys.CreateEntry("PoiNext", "PageUp", "Next point of interest key",
                "Selects the next door, person or item box of the area (in map order, or nearest first: PoiMapOrder) and says its distance and compass direction.");
            KeyPoiPrevious = keys.CreateEntry("PoiPrevious", "PageDown", "Previous point of interest key",
                "Selects the previous point of interest.");
            KeyPoiFilterNext = keys.CreateEntry("PoiFilterNext", "Shift+PageUp", "Next point of interest filter key",
                "Shows only one kind of point in the Page Up / Page Down list: All, Doors, Rooms, Exits, Warps, Souls, Demons, Item boxes, Events (kinds with none in the area are skipped).");
            KeyPoiFilterPrevious = keys.CreateEntry("PoiFilterPrevious", "Shift+PageDown", "Previous point of interest filter key",
                "The previous kind of point.");
            KeyPoiWalk = keys.CreateEntry("PoiWalk", "Home", "Walk to point of interest key",
                "Walks to the selected point of interest through the game's own movement (fixed-camera rooms and first person). Any movement key stops it. Enter (the game's confirm) then interacts as usual.");
            KeyPoiGuide = keys.CreateEntry("PoiGuide", "End", "Directions to point of interest key",
                "Names the selected point of interest, then speaks directions to it from where you stand (first person: ahead, left, right; fixed-camera rooms: the key direction on screen). Press again for new directions as you go.");
            KeyAreaRoutes = keys.CreateEntry("AreaRoutes", "F7", "Routes through other areas key",
                "Turns on or off the routes through other areas in the Page Up list, Home and End.");

            MelonPreferences_Category debug = MelonPreferences.CreateCategory("NocturneAccess_Debug", "Debug");
            debug.SetFilePath(FilePath, true, false);
            VerboseLogging = debug.CreateEntry("VerboseLogging", false, "Verbose logging",
                "Writes [DEBUG] lines to Latest.log, including lines dropped by the dedupe window.");
            // "TextProbe" (off) replaces the entry "Probe" (on) of 0.0.56 and earlier, so saved configs pick up the new default
            // (Francesco, 6 Oct 2026: the probe off by default).
            ProbeEnabled = debug.CreateEntry("TextProbe", false, "Text probe",
                "Research tool: logs every UI text and message-engine call to Latest.log. Needs a restart. Off by default; set to true only to research a new screen.");

            // Write the file now so new settings appear with their defaults.
            speech.SaveToFile(false);
            dialogue.SaveToFile(false);
            menus.SaveToFile(false);
            battle.SaveToFile(false);
            navigation.SaveToFile(false);
            keys.SaveToFile(false);
            debug.SaveToFile(false);
        }

        /// <summary>Writes a setting changed in game (F7, F8) to the file; a failure is logged, never thrown.</summary>
        internal static void Save<T>(MelonPreferences_Entry<T> entry)
        {
            try
            {
                entry.Category.SaveToFile(false);
            }
            catch (System.Exception ex)
            {
                Log.Warn($"[Config] Could not save {entry.Identifier}: {ex.Message}");
            }
        }
    }
}
