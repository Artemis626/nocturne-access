using System;
using System.Collections.Generic;
using MelonLoader;
using NocturneAccess.Capture;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using NocturneAccess.Systems;
using NocturneAccess.Systems.Battle;
using NocturneAccess.Systems.Dialogue;
using NocturneAccess.Systems.Menus;
using NocturneAccess.Systems.Navigation;
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
            public bool Shift, Ctrl, Alt;
            public Action Action;

            public string Text => (Ctrl ? "Ctrl+" : "") + (Alt ? "Alt+" : "") + (Shift ? "Shift+" : "") + Key;
        }

        private static readonly List<Binding> Bindings = new List<Binding>();
        private static bool _disabled;

        internal static void Init()
        {
            Bindings.Clear();
            // 0.0.204, the layout by blocks (Francesco's option C, docs/KEYS-PLAN.md): F1 key help, F2 repeat; F3 party status (in
            // battle and in the field), F4 enemies, F5 battle log; F6 centre on the square; F7 and F8 free. Blobber mode, the falling
            // minigame's autopilot and HP after actions are rows of the settings window; their keys stay, unbound by default. Debug
            // keys on Ctrl. The fallbacks below are used only when a saved key cannot be read.
            Add("key help", Config.KeyHelp, KeyCode.F1, KeyHelp.Speak);
            Add("repeat", Config.KeyRepeat, KeyCode.F2, RepeatLast);
            Add("party status", Config.KeyBattleStatus, KeyCode.F3, BattleReader.SpeakStatus);
            Add("battle enemies", Config.KeyBattleEnemies, KeyCode.F4, BattleReader.SpeakEnemies);
            Add("battle log", Config.KeyBattleLog, KeyCode.F5, BattleLog.Speak);
            Add("centre on the square", Config.KeyCentre, KeyCode.F6, () => { if (!BattleReader.Active) BlobberMode.RequestCentre(); });
            Add("mod settings", Config.KeyModSettings, KeyCode.F, ModSettings.Toggle, shift: true);
            // Unbound by default since 0.0.204; while falling, the Blobber key (if bound) still switches the autopilot.
            Add("blobber mode", Config.KeyBlobber, KeyCode.None, () => { if (PipePilot.Active) PipePilot.Toggle(); else BlobberMode.Toggle(); });
            Add("battle verbosity", Config.KeyBattleVerbosity, KeyCode.None, BattleReader.ToggleHpLeft);
            Add("routes through other areas", Config.KeyAreaRoutes, KeyCode.None, AreaRoutes.Toggle);
            Add("battle dump", Config.KeyBattleDump, KeyCode.F3, BattleDump.Dump, ctrl: true);
            Add("dump UI", Config.KeyDumpUi, KeyCode.F6, UiTreeDumper.Dump, ctrl: true);
            Add("navigation dump", Config.KeyNavDump, KeyCode.F6, NavDump.Dump, shift: true, ctrl: true);
            Add("toggle probe", Config.KeyToggleProbe, KeyCode.F7, Probes.Toggle, ctrl: true);
            Add("next point of interest", Config.KeyPoiNext, KeyCode.PageUp, () => PointsOfInterest.Cycle(1));
            Add("previous point of interest", Config.KeyPoiPrevious, KeyCode.PageDown, () => PointsOfInterest.Cycle(-1));
            Add("next point of interest filter", Config.KeyPoiFilterNext, KeyCode.PageUp, () => PointsOfInterest.CycleFilter(1), shift: true);
            Add("previous point of interest filter", Config.KeyPoiFilterPrevious, KeyCode.PageDown, () => PointsOfInterest.CycleFilter(-1), shift: true);
            Add("walk to point of interest", Config.KeyPoiWalk, KeyCode.Home, AutoWalk.Request);
            Add("directions to point of interest", Config.KeyPoiGuide, KeyCode.End, RouteGuide.Speak);
            Add("way on", Config.KeyWayOn, KeyCode.End, WayOn.Speak, shift: true);
            Add("where am I", Config.KeyWhereAmI, KeyCode.Delete, WhereAmI.Speak);
        }

        internal static void Update()
        {
            if (_disabled)
                return;
            // While the mod settings window is open it takes every key itself (0.0.194).
            if (ModSettings.Update())
                return;

            bool shift = false, ctrl = false, alt = false;
            bool modifiersRead = false;
            foreach (Binding binding in Bindings)
            {
                bool pressed;
                try
                {
                    pressed = UnityInput.GetKeyDown(binding.Key);
                    if (pressed && !modifiersRead)
                    {
                        shift = UnityInput.GetKey(KeyCode.LeftShift) || UnityInput.GetKey(KeyCode.RightShift);
                        ctrl = UnityInput.GetKey(KeyCode.LeftControl) || UnityInput.GetKey(KeyCode.RightControl);
                        alt = UnityInput.GetKey(KeyCode.LeftAlt) || UnityInput.GetKey(KeyCode.RightAlt);
                        modifiersRead = true;
                    }
                    // Modifiers must match exactly: plain F6 does not fire while Shift is held.
                    pressed = pressed && shift == binding.Shift && ctrl == binding.Ctrl && alt == binding.Alt;
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
                    Log.Debug($"Key {binding.Text} ({binding.Name})");
                    binding.Action();
                }
                catch (Exception ex)
                {
                    Log.Error($"Key {binding.Text} ({binding.Name}) failed: {ex}");
                }
            }
        }

        private static void RepeatLast()
        {
            // Whichever screen spoke last (RepeatBuffer): a menu, a list, a prompt with its options, or dialogue.
            string latest = RepeatBuffer.Text;
            if (!RepeatBuffer.LastIsDialogue && latest.Length > 0)
            {
                SpeechManager.Say(latest, interrupt: true, force: true);
                return;
            }

            string page = DialogueReader.LastUtterance;
            string option = ChoiceReader.CurrentOption;
            string text = page.Length == 0 ? "No dialogue yet." : page;
            if (option.Length > 0)
                text += " Selected: " + option;
            SpeechManager.Say(text, interrupt: true, force: true);
        }

        /// <summary>The key bound to the named action ("Shift+End"), or "" when it has none (0.0.172, the key help).</summary>
        internal static string KeyText(string name)
        {
            foreach (Binding b in Bindings)
            {
                if (b.Name == name)
                    return b.Text.Replace("PageUp", "Page Up").Replace("PageDown", "Page Down");
            }
            return "";
        }

        /// <summary>A key value written the way the bindings write it ("Ctrl+Shift+F6"), or "" for None or an unreadable value (0.0.213, remapping).</summary>
        internal static string Canonical(string value)
        {
            var b = new Binding();
            if (!Parse(value, b) || b.Key == KeyCode.None)
                return "";
            return b.Text;
        }

        /// <summary>A key value as spoken: "Page Up", "Shift+End"; "no key" when unbound.</summary>
        internal static string Spoken(string value)
        {
            string c = Canonical(value);
            return c.Length == 0 ? "no key" : c.Replace("PageUp", "Page Up").Replace("PageDown", "Page Down");
        }

        /// <summary>The named binding's key went down this frame with its modifiers (0.0.204: the settings window's repeat and key help follow the bindings).</summary>
        internal static bool IsDown(string name)
        {
            foreach (Binding b in Bindings)
            {
                if (b.Name != name || !UnityInput.GetKeyDown(b.Key))
                    continue;
                bool shift = UnityInput.GetKey(KeyCode.LeftShift) || UnityInput.GetKey(KeyCode.RightShift);
                bool ctrl = UnityInput.GetKey(KeyCode.LeftControl) || UnityInput.GetKey(KeyCode.RightControl);
                bool alt = UnityInput.GetKey(KeyCode.LeftAlt) || UnityInput.GetKey(KeyCode.RightAlt);
                if (shift == b.Shift && ctrl == b.Ctrl && alt == b.Alt)
                    return true;
            }
            return false;
        }

        private static void Add(string name, MelonPreferences_Entry<string> entry, KeyCode fallback, Action action,
            bool shift = false, bool ctrl = false, bool alt = false)
        {
            var binding = new Binding { Name = name, Key = fallback, Shift = shift, Ctrl = ctrl, Alt = alt, Action = action };
            if (!Parse(entry.Value, binding))
            {
                Log.Warn($"Unknown key '{entry.Value}' for {entry.Identifier}, using {binding.Text}.");
                binding.Key = fallback;
                binding.Shift = shift;
                binding.Ctrl = ctrl;
                binding.Alt = alt;
            }
            if (binding.Key == KeyCode.None)
                return;

            Bindings.Add(binding);
            Log.Info($"Hotkeys: {name} = {binding.Text}.");
        }

        /// <summary>A Unity KeyCode name, optionally after modifiers: "F5", "Shift+F5", "Ctrl+Shift+F6".</summary>
        private static bool Parse(string value, Binding binding)
        {
            string[] parts = (value ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || !Enum.TryParse(parts[parts.Length - 1], true, out KeyCode key))
                return false;
            bool shift = false, ctrl = false, alt = false;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].ToLowerInvariant())
                {
                    case "shift": shift = true; break;
                    case "ctrl": case "control": ctrl = true; break;
                    case "alt": alt = true; break;
                    default: return false;
                }
            }
            binding.Key = key;
            binding.Shift = shift;
            binding.Ctrl = ctrl;
            binding.Alt = alt;
            return true;
        }
    }
}
