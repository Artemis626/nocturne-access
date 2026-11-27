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
        internal static MelonPreferences_Entry<string> KeyWayOn { get; private set; }
        internal static MelonPreferences_Entry<string> KeyPoiFilterNext { get; private set; }
        internal static MelonPreferences_Entry<string> KeyPoiFilterPrevious { get; private set; }
        internal static MelonPreferences_Entry<bool> PoiMapOrder { get; private set; }
        internal static MelonPreferences_Entry<bool> AreaRoutes { get; private set; }
        internal static MelonPreferences_Entry<bool> WallBumpSound { get; private set; }
        internal static MelonPreferences_Entry<bool> WayOnEnabled { get; private set; }
        internal static MelonPreferences_Entry<bool> WayOnSound { get; private set; }
        internal static MelonPreferences_Entry<bool> AlarmSound { get; private set; }
        internal static MelonPreferences_Entry<bool> DoorSound { get; private set; }
        internal static MelonPreferences_Entry<bool> SecretSound { get; private set; }
        internal static MelonPreferences_Entry<bool> CameraSound { get; private set; }
        internal static MelonPreferences_Entry<bool> PipeNoDamage { get; private set; }
        internal static MelonPreferences_Entry<bool> ChaseSound { get; private set; }
        internal static MelonPreferences_Entry<bool> ChaseOff { get; private set; }
        internal static MelonPreferences_Entry<bool> GeisGift { get; private set; }
        internal static MelonPreferences_Entry<string> KeyAreaRoutes { get; private set; }
        internal static MelonPreferences_Entry<string> KeyHelp { get; private set; }
        internal static MelonPreferences_Entry<string> KeyModSettings { get; private set; }
        internal static MelonPreferences_Entry<int> KeyLayout { get; private set; }
        internal static MelonPreferences_Entry<bool> PipeAutopilot { get; private set; }
        /// <summary>True when this start moved the saved keys to the 0.0.204 layout (logged once by ModMain).</summary>
        internal static bool LayoutReset { get; private set; }

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
            BlobberOnAtStart = navigation.CreateEntry("BlobberOnAtStart", true, "Blobber mode",
                "Blobber mode on or off (kept for the next start). Switched in the mod settings window (Shift+F, Navigation) since 0.0.204; the Blobber key has no default.");
            BlobberCellSize = navigation.CreateEntry("BlobberCellSize", 600, "Blobber cell size",
                "Development: one step in game units (600 = one corridor width in the Underground Facility).");
            DoorKinds = navigation.CreateEntry("DoorKinds", true, "Door kinds",
                "Page Up / Page Down, Home and End say whether a plain door leads to a room, a passage or an exit (from the game's door table). Turn off to go back to plain \"Door\".");
            PoiMapOrder = navigation.CreateEntry("PoiMapOrder", true, "Points of interest in map order",
                "Page Up / Page Down go through an area's points in a fixed order, north to south and west to east, so each keeps its number while you walk. Turn off for the old order, nearest first (it changes as you move).");
            AreaRoutes = navigation.CreateEntry("AreaRoutes", false, "Routes through other areas",
                "Page Up / Page Down also list points of the area reached only by going through other areas, by doors you have gone through (\"via 1 other area\"); Home walks there door by door, End gives the way to the first door. Switched in the mod settings window (Shift+F, Navigation).");
            WallBumpSound = navigation.CreateEntry("WallBumpSound", true, "Wall bump sound",
                "A short thud when you walk into a wall and stop (the game has no sound of its own for it).");
            WayOnEnabled = navigation.CreateEntry("WayOnEnabled", true, "Way on",
                "Shift+End, the way on: says and selects the area's way onward where the mod has one. Off: Shift+End only says it is off, and the way-on tone is not played (0.0.198, for players who prefer to find the way themselves).");
            WayOnSound = navigation.CreateEntry("WayOnSound", true, "Way on tone",
                "A soft two-note tone, once, on reaching an area where Shift+End has a way on (after loading a save, and the first time you enter each such area).");
            AlarmSound = navigation.CreateEntry("AlarmSound", true, "Alarm tones",
                "In the Amala Temple's Red Temple: two low falling notes when a shadow sets off the alarm (the warp devices stop), two rising notes when a reset tile switches it off.");
            DoorSound = navigation.CreateEntry("DoorSound", true, "Door tone",
                "Two soft taps, like a knock, when the game shows a door prompt (\"Open Door\", \"Enter\", \"Exit\"): the door can be gone through from where you stand.");
            CameraSound = navigation.CreateEntry("CameraSound", true, "Camera change tones",
                "When the game switches the camera: a soft swish and a low note on entering a fixed-camera room (the game's own controls; first person and blobber mode wait), the swish and a higher note on returning to the free camera.");
            SecretSound = navigation.CreateEntry("SecretSound", true, "Secret area and section exit tones",
                "Three soft rising notes with \"Secret area reached via warp.\", when a warp takes you somewhere only sight would tell you is special (the Fourth Kalpa's Twelve Meters of Eternity at new moon); and three soft notes returning home when a section beyond it ends and drops you back in B1.");
            PipeNoDamage = navigation.CreateEntry("PipeNoDamage", true, "No damage in the falling minigame",
                "The Labyrinth of Amala falling minigame takes no HP from the party for the objects hit (the game takes it at the end, down to 1 HP each). Macca lost to hits is unchanged.");
            PipeAutopilot = navigation.CreateEntry("PipeAutopilot", true, "Falling minigame autopilot",
                "The mod steers the Labyrinth of Amala's falling minigame (a direction key takes over at once). Read at the start of each fall (0.0.204; it was F4 during the fall).");
            ChaseSound = navigation.CreateEntry("ChaseSound", true, "Chase pulse",
                "In the Labyrinth's Third Kalpa chase: a soft low pulse while Raidou (or Dante) comes nearer within 30 metres, or is within 12 metres; faster as he closes in. He catches you within 2.5 metres.");
            ChaseOff = navigation.CreateEntry("ChaseOff", true, "Third Kalpa chase off",
                "Accessibility change (Francesco, 2 Nov 2026: Raidou, or Dante, is too fast to avoid by ear). In the Labyrinth's Third Kalpa chase the pursuer stands still and never starts a battle; the stages, doors and story go on as the game's. On by default. The boss fight at the end is not affected. Set to false for the game's chase, with the mod's pulse and Insert key.");
            GeisGift = navigation.CreateEntry("GeisGift", true, "Geis Magatama after Metatron",
                "Accessibility change (Francesco, 21 Nov 2026): the Puzzle Boy minigame in Asakusa is visual only, and its prize, the Geis Magatama, is needed for later challenges. Once Metatron is beaten in the Labyrinth of Amala, the mod gives Geis the way the Puzzle Boy does (and marks his prize as taken). On by default; false leaves it to the minigame.");

            MelonPreferences_Category keys = MelonPreferences.CreateCategory("NocturneAccess_Keys", "Keys");
            keys.SetFilePath(FilePath, true, false);
            KeyRepeat = keys.CreateEntry("Repeat", "F2", "Repeat key",
                "Repeats whichever came last: the menu item, or the dialogue page (plus the highlighted option while a choice is open).");
            KeyWhereAmI = keys.CreateEntry("WhereAmI", "Delete", "Where am I key",
                "Speaks where you are: the location banner (area, or the district on the world map), the facing in first person, and the nearest point of interest.");
            KeyBattleVerbosity = keys.CreateEntry("BattleVerbosity", "None", "Battle verbosity key",
                "Turns on or off the HP left spoken after each hit or heal in battle (status keys always give HP).");
            KeyBattleStatus = keys.CreateEntry("BattleStatus", "F3", "Party status key",
                "In battle: whose turn it is and the press turns left, then each party member with HP and MP. Outside battle (0.0.204): each party member with HP, MP and statuses.");
            KeyBattleEnemies = keys.CreateEntry("BattleEnemies", "F4", "Battle enemies key",
                "In battle: each enemy with HP, and its affinities once Analyze has shown them.");
            KeyBattleLog = keys.CreateEntry("BattleLog", "F5", "Battle log key",
                "Battle: what happened in the previous side's turn and the current one so far (actions, damage, weakness and critical hits). Kept until the next battle.");
            KeyDumpUi = keys.CreateEntry("DumpUi", "Ctrl+F6", "Dump UI key",
                "Debug: writes every text currently visible on screen to Latest.log.");
            // "ProbeToggle" (Shift+F7) replaces the entry "ToggleProbe" (F7) of 0.0.55 and earlier, so saved configs pick up the new
            // default: F7 became the routes through other areas (0.0.56, Francesco: a function key; debug keys may use Shift).
            KeyToggleProbe = keys.CreateEntry("ProbeToggle", "Ctrl+F7", "Toggle probe key",
                "Debug: pauses or resumes the text probe (only when TextProbe = true).");
            // "NavigationDump" (Shift+F6) replaces the 0.0.12 entry "NavDump" (F5), so saved configs pick up the new default.
            KeyNavDump = keys.CreateEntry("NavigationDump", "Ctrl+Shift+F6", "Navigation dump key",
                "Debug: writes the navigation data of the current area (position, place names, walls, auto-map, people, boxes, event volumes) to UserData/NocturneAccess/NavDumps, with a summary in Latest.log.");
            KeyBattleDump = keys.CreateEntry("BattleDump", "Ctrl+F3", "Battle dump key",
                "Debug: writes the battle data of this moment (turn, press turns, every unit with level, status, skills and buffs, formation, target selection) to UserData/NocturneAccess/BattleDumps, with a summary in Latest.log.");
            KeyBlobber = keys.CreateEntry("Blobber", "None", "Blobber mode key",
                "Turns blobber mode on or off. In first person every game key stays as it is; letting go of W carries the walk on to the centre of the next square, and changes around the player are spoken.");
            KeyCentre = keys.CreateEntry("Centre", "F6", "Centre key",
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
            KeyWayOn = keys.CreateEntry("WayOn", "Shift+End", "Way on key",
                "Says \"Way on.\", selects the area's way onward (from walkthroughs, where the mod has one) and speaks its directions as End does; Home then walks there.");
            // 0.0.172: F7 is the key help (Francesco, 17 Nov 2026); the routes switch has no key by default (a new entry, so a saved
            // "AreaRoutes = F7" no longer binds it); AreaRoutes in the Navigation section still turns the routes on.
            KeyAreaRoutes = keys.CreateEntry("AreaRoutesToggle", "None", "Routes through other areas key",
                "Turns on or off the routes through other areas in the Page Up list, Home and End (no key by default since 0.0.172).");
            KeyHelp = keys.CreateEntry("KeyHelp", "F1", "Key help",
                "Says the keys of the current screen: the game's own button guide (\"Q, E: Toggle Sort.\"), then the mod's keys for a menu, the field or a battle.");
            // 0.0.194: the mod settings window (Francesco, 25 Nov 2026: Shift+F, F being the game's own menu key; field, overworld and battle only).
            KeyModSettings = keys.CreateEntry("ModSettings", "Shift+F", "Mod settings key",
                "Opens or closes the mod settings window (in the field, on the overworld and in battle; never inside a game menu). While it is open the game sees no keys.");
            // 0.0.204: the key layout by blocks (Francesco's option C, docs/KEYS-PLAN.md): F1 key help, F2 repeat, F3 to F5 battle,
            // F6 centre; blobber mode and HP after actions moved to the settings window; debug keys on Ctrl. A saved file keeps its
            // old key values, so every key entry is reset to its default once, when this marker is below 3 (see the reset below the Debug section).
            KeyLayout = keys.CreateEntry("KeyLayout", 1, "Key layout version",
                "Do not change: the mod uses it to move saved keys to a new default layout once.");
            MelonPreferences_Category debug = MelonPreferences.CreateCategory("NocturneAccess_Debug", "Debug");
            debug.SetFilePath(FilePath, true, false);
            VerboseLogging = debug.CreateEntry("VerboseLogging", false, "Verbose logging",
                "Writes [DEBUG] lines to Latest.log, including lines dropped by the dedupe window.");
            // "TextProbe" (off) replaces the entry "Probe" (on) of 0.0.56 and earlier, so saved configs pick up the new default
            // (Francesco, 6 Oct 2026: the probe off by default).
            ProbeEnabled = debug.CreateEntry("TextProbe", false, "Text probe",
                "Research tool: logs every UI text and message-engine call to Latest.log. Needs a restart. Off by default; set to true only to research a new screen.");

            // After every SetFilePath: each one reloads the whole file into every category (MelonPreferences.LoadFileAndRefreshCategories),
            // so a reset done earlier was undone by the Debug section's load (0.0.204: the log said "Keys moved", the keys stayed; 0.0.205).
            // The marker is 3: 0.0.204 had already written 2.
            if (KeyLayout.Value < 3)
            {
                foreach (MelonPreferences_Entry<string> e in new[] { KeyRepeat, KeyWhereAmI, KeyBattleVerbosity, KeyBattleStatus,
                             KeyBattleEnemies, KeyBattleLog, KeyDumpUi, KeyToggleProbe, KeyNavDump, KeyBattleDump, KeyBlobber, KeyCentre,
                             KeyPoiNext, KeyPoiPrevious, KeyPoiFilterNext, KeyPoiFilterPrevious, KeyPoiWalk, KeyPoiGuide, KeyWayOn,
                             KeyAreaRoutes, KeyHelp, KeyModSettings })
                    e.ResetToDefault();
                KeyLayout.Value = 3;
                LayoutReset = true;
            }

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
