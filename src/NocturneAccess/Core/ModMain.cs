using MelonLoader;
using NocturneAccess.Capture;
using NocturneAccess.Core;
using NocturneAccess.Input;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Battle;
using NocturneAccess.Systems.Dialogue;
using NocturneAccess.Systems.Menus;
using NocturneAccess.Systems.Navigation;

// Developer and game name exactly as MelonLoader reports them in Latest.log
// ("Game Developer: アトラス", "Game Name: smt3hd"), so the mod only loads in this game.
[assembly: MelonInfo(typeof(ModMain), ModMain.ModName, ModMain.ModVersion, ModMain.ModAuthor)]
[assembly: MelonGame("アトラス", "smt3hd")]
// Hooks are applied one by one through Patcher, so one bad hook cannot stop the others.
[assembly: HarmonyDontPatchAll]

namespace NocturneAccess.Core
{
    public sealed class ModMain : MelonMod
    {
        internal const string ModName = "Nocturne Access";
        internal const string ModVersion = "0.0.189";
        internal const string ModAuthor = "Francesco";

        public override void OnInitializeMelon()
        {
            Log.Init(LoggerInstance);
            Config.Init();
            Log.Info($"{ModName} {ModVersion} starting. Settings: {Config.FilePath}");

            SpeechManager.Init();

            Patcher.Init(HarmonyInstance);
            TmpRouter.Install();
            DialogueReader.Install();
            ChoiceReader.Install();
            MenuReader.Install();
            AnnouncementReader.Install();
            SaveListReader.Install();
            SaveChoiceReader.Install();
            ConfigReader.Install();
            NameEntryReader.Install();
            StatusReader.Install();
            CampListReader.Install();
            FusionReader.Install();
            FacilityReader.Install();
            RagReader.Install();
            BattleReader.Install();
            BattleAnalyze.Install();
            FieldState.Install();
            FirstPersonView.Install();
            PadInput.Install();
            PipePilot.Install();
            ChaseCues.Install();
            ExploredDoors.Install();
            GameFlagWatch.Install();
            ScriptWarps.Install();
            // Capture.FadeWatch.Install(): off since 0.0.182 (Francesco, 21 Nov 2026: the black screen comes from moving through doors
            // faster than the game's transition, with the way on and Home chaining doors; on 0.0.178 to 0.0.181; Capture/FadeWatch.cs kept).
            BlobberMode.Init();
            NavProbe.Install();
            Probes.Install();
            Patcher.LogSummary();

            Hotkeys.Init();
        }

        /// <summary>Runs once Unity is up, so this line proves the mod is live inside the game.</summary>
        public override void OnLateInitializeMelon()
        {
            SpeechManager.Say("Nocturne Access loaded");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            Log.Info($"[Scene] Initialized: {sceneName} (build index {buildIndex})");
        }

        // Every system updated each frame, in this order. Each one runs guarded, so an exception in one cannot skip the
        // others for that frame, and a repeating one is logged a few times instead of every frame.
        private static readonly (string Name, System.Action Tick)[] Updates =
        {
            ("hotkeys", Hotkeys.Update),
            ("dialogue", DialogueReader.Tick),
            ("menus", MenuReader.Tick),
            ("announcements", AnnouncementReader.Tick),
            ("save list", SaveListReader.Tick),
            ("save choice", SaveChoiceReader.Tick),
            ("config", ConfigReader.Tick),
            ("name entry", NameEntryReader.Tick),
            ("status", StatusReader.Tick),
            ("camp lists", CampListReader.Tick),
            ("party status", PartyStatus.Tick),
            ("fusion tables", FusionReader.Tick),
            ("facility numbers", FacilityReader.Tick),
            ("rag lists", RagReader.Tick),
            ("battle", BattleReader.Tick),
            ("field state", FieldState.Tick),
            ("first person", FirstPersonView.Tick),
            ("navigation probe", NavProbe.Tick),
            ("explored doors", ExploredDoors.Tick),
            ("plane shift", PlaneShift.Tick),
            ("fourth kalpa cues", FourthKalpaCues.Tick),
            ("time puzzle", TimePuzzle.Tick),
            // ("pipe probe", PipeProbe.Tick): off since 0.0.151 (the falling minigame's autopilot was built from it and verified 23 Oct 2026; Capture/PipeProbe.cs kept)
            // ("chase probe", ChaseProbe.Tick): off since 0.0.148 (Francesco, 3 Nov 2026; the chase is off by default; Capture/ChaseProbe.cs kept for a later accessible chase)
            ("chase cues", ChaseCues.Tick),
            ("prize gifts", Systems.PrizeGifts.Tick),
            ("script warps", ScriptWarps.Tick),
            ("pipe autopilot", PipePilot.Tick),
            ("way-on tone", WayOn.Tick),
            ("alarm cue", AlarmCue.Tick),
            ("area routes", AreaRoutes.Tick),
            ("blobber exploring", BlobberMode.CheckExploring),
            ("blobber", BlobberMode.Tick),
            ("wall bump", WallBump.Tick),
            ("auto-walk", AutoWalk.Tick),
            // ("screen fade", Capture.FadeWatch.Tick): off since 0.0.182
            ("log archive", LogArchive.Tick),
            ("probes", Probes.Tick),
        };

        private static readonly int[] UpdateErrors = new int[Updates.Length];

        public override void OnUpdate()
        {
            for (int i = 0; i < Updates.Length; i++)
            {
                try
                {
                    Updates[i].Tick();
                }
                catch (System.Exception ex)
                {
                    if (++UpdateErrors[i] <= 3)
                        Log.Error($"[Update] {Updates[i].Name} failed: {ex}");
                }
            }
        }

        public override void OnDeinitializeMelon()
        {
            // Only the first 3 errors of each system are written as they happen: the totals show one that kept failing (0.0.56).
            for (int i = 0; i < Updates.Length; i++)
                if (UpdateErrors[i] > 3)
                    Log.Warn($"[Update] {Updates[i].Name} failed {UpdateErrors[i]} times this session.");
            Probes.FlushAll();
            SpeechManager.Shutdown();
            LogArchive.ArchiveCurrentSession();
        }
    }
}
