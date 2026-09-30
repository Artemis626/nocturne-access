using MelonLoader;
using NocturneAccess.Capture;
using NocturneAccess.Core;
using NocturneAccess.Input;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Dialogue;
using NocturneAccess.Systems.Menus;

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
        internal const string ModVersion = "0.0.6";
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

        public override void OnUpdate()
        {
            Hotkeys.Update();
            DialogueReader.Tick();
            MenuReader.Tick();
            AnnouncementReader.Tick();
            LogArchive.Tick();
            Probes.Tick();
        }

        public override void OnDeinitializeMelon()
        {
            Probes.FlushAll();
            SpeechManager.Shutdown();
            LogArchive.ArchiveCurrentSession();
        }
    }
}
