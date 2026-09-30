using System;
using MelonLoader;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;
// Inside the NocturneAccess.Input namespace, a bare "Input" means this namespace, not Unity's class.
using UnityInput = UnityEngine.Input;

namespace NocturneAccess.Input
{
    /// <summary>
    /// On-demand keys, polled once per frame from ModMain.OnUpdate.
    ///
    /// Uses Unity's legacy UnityEngine.Input.GetKeyDown(KeyCode), which is present in this build
    /// (UnityEngine.InputLegacyModule proxy). The game itself reads input through its own
    /// InputUtil / InputAssign layer, so our keys must avoid the game's keyboard bindings;
    /// see docs/KEYS.md for reserved keys.
    /// </summary>
    internal static class Hotkeys
    {
        private static KeyCode _statusKey = KeyCode.None;
        private static bool _disabled;

        internal static void Init()
        {
            _statusKey = ParseKey(Config.KeyStatus, KeyCode.F8);
            Log.Info($"Hotkeys: status = {_statusKey}.");
        }

        internal static void Update()
        {
            if (_disabled)
                return;

            try
            {
                if (_statusKey != KeyCode.None && UnityInput.GetKeyDown(_statusKey))
                    SpeechManager.Say($"Nocturne Access running. Speech through {SpeechManager.BackendName}.", interrupt: true, force: true);
            }
            catch (Exception ex)
            {
                // Called every frame: log once and stop, rather than flooding the log.
                _disabled = true;
                Log.Error("Hotkeys disabled after an error: " + ex);
            }
        }

        private static KeyCode ParseKey(MelonPreferences_Entry<string> entry, KeyCode fallback)
        {
            string value = entry.Value?.Trim();
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out KeyCode key))
                return key;

            Log.Warn($"Unknown key '{entry.Value}' for {entry.Identifier}, using {fallback}.");
            return fallback;
        }
    }
}
