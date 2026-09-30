using System;
using System.Collections.Generic;
using MelonLoader;
using NocturneAccess.Capture;
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
        private sealed class Binding
        {
            public string Name;
            public KeyCode Key;
            public Action Action;
        }

        private static readonly List<Binding> Bindings = new List<Binding>();
        private static bool _disabled;

        internal static void Init()
        {
            Bindings.Clear();
            Add("status", Config.KeyStatus, KeyCode.F8, SpeakStatus);
            Add("dump UI", Config.KeyDumpUi, KeyCode.F6, UiTreeDumper.Dump);
            Add("toggle probe", Config.KeyToggleProbe, KeyCode.F7, Probes.Toggle);
        }

        internal static void Update()
        {
            if (_disabled)
                return;

            foreach (Binding binding in Bindings)
            {
                bool pressed;
                try
                {
                    pressed = UnityInput.GetKeyDown(binding.Key);
                }
                catch (Exception ex)
                {
                    // Called every frame: log once and stop, rather than flooding the log.
                    _disabled = true;
                    Log.Error("Hotkeys disabled: reading the keyboard failed: " + ex);
                    return;
                }

                if (!pressed)
                    continue;

                try
                {
                    Log.Debug($"Key {binding.Key} ({binding.Name})");
                    binding.Action();
                }
                catch (Exception ex)
                {
                    Log.Error($"Key {binding.Key} ({binding.Name}) failed: {ex}");
                }
            }
        }

        private static void SpeakStatus()
        {
            SpeechManager.Say($"Nocturne Access running. Speech through {SpeechManager.BackendName}.", interrupt: true, force: true);
        }

        private static void Add(string name, MelonPreferences_Entry<string> entry, KeyCode fallback, Action action)
        {
            KeyCode key = ParseKey(entry, fallback);
            if (key == KeyCode.None)
                return;

            Bindings.Add(new Binding { Name = name, Key = key, Action = action });
            Log.Info($"Hotkeys: {name} = {key}.");
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
