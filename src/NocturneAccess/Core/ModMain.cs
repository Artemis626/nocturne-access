using MelonLoader;
using NocturneAccess.Core;
using NocturneAccess.Input;
using NocturneAccess.Speech;

// Developer and game name exactly as MelonLoader reports them in Latest.log
// ("Game Developer: アトラス", "Game Name: smt3hd"), so the mod only loads in this game.
[assembly: MelonInfo(typeof(ModMain), ModMain.ModName, ModMain.ModVersion, ModMain.ModAuthor)]
[assembly: MelonGame("アトラス", "smt3hd")]

namespace NocturneAccess.Core
{
    public sealed class ModMain : MelonMod
    {
        internal const string ModName = "Nocturne Access";
        internal const string ModVersion = "0.0.1";
        internal const string ModAuthor = "Francesco";

        public override void OnInitializeMelon()
        {
            Log.Init(LoggerInstance);
            Config.Init();
            Log.Info($"{ModName} {ModVersion} starting. Settings: {Config.FilePath}");

            SpeechManager.Init();
            Hotkeys.Init();
        }

        /// <summary>Runs once Unity is up, so this line proves the mod is live inside the game.</summary>
        public override void OnLateInitializeMelon()
        {
            SpeechManager.Say("Nocturne Access loaded");
        }

        public override void OnUpdate()
        {
            Hotkeys.Update();
        }

        public override void OnDeinitializeMelon()
        {
            SpeechManager.Shutdown();
        }
    }
}
