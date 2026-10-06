using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Switches the discovery probes (TMP text and message engine) as one unit.
    /// The hooks are only installed when Probe = true in NocturneAccess.cfg at startup,
    /// so players who never enable it pay nothing. The toggle key (default Shift+F7 since 0.0.56) then pauses
    /// and resumes logging without restarting the game.
    /// </summary>
    internal static class Probes
    {
        internal static bool Installed { get; private set; }

        internal static void Install()
        {
            if (!Config.ProbeEnabled.Value)
            {
                Log.Info("[Probe] Off (Probe = false in the config). Hooks not installed.");
                return;
            }

            // The TMP probe is fed by Core/TmpRouter's shared hook; only the message engine needs its own hooks.
            MesProbe.Install();
            Installed = true;
            SetEnabled(true);
        }

        internal static void Toggle()
        {
            if (!Installed)
            {
                SpeechManager.Say("Text probe is not installed. Set Probe to true in the config and restart.", interrupt: true, force: true);
                return;
            }

            bool on = !TmpTextProbe.Enabled;
            SetEnabled(on);
            SpeechManager.Say(on ? "Text probe on" : "Text probe off", interrupt: true, force: true);
        }

        internal static void Tick()
        {
            if (Installed)
                TmpTextProbe.Tick();
        }

        internal static void FlushAll()
        {
            if (Installed)
                TmpTextProbe.FlushAll();
        }

        private static void SetEnabled(bool on)
        {
            if (!on)
                TmpTextProbe.FlushAll();
            TmpTextProbe.Enabled = on;
            MesProbe.Enabled = on;
            Log.Info(on ? "[Probe] Logging on." : "[Probe] Logging paused.");
        }
    }
}
