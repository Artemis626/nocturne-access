using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Input;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Battle;
using NocturneAccess.Systems.Navigation;
using UnityEngine;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// The key help (0.0.172; Francesco, 17 Nov 2026: a key that says every key usable on the current screen, the game's and the
    /// mod's, needed before the public release; first case the Demon Compendium; F7, his choice, was routes through other areas).
    ///
    /// The game's keys come from its own button guide, drawn at the bottom of many screens: Canvas_UI/buttonguide01/guide/
    /// buttonguide_setNN, each set with its key labels (buttonNN/Text1TM or Text2TM) and its action (guidetext/Text_buttonTM).
    /// The Compendium's (UI dump 17 Nov 2026 15:55:00): "W S A D: Select / Page", "Q E: Toggle Sort", "C: Cancel", and
    /// "Confirm", whose key is drawn as a picture (its label is blank), so only the action is said. Then the mod's keys for the
    /// context (a menu, the field, a battle), named from the live bindings so a remapped key is said as set.
    /// </summary>
    internal static class KeyHelp
    {
        private const string Tag = "[KeyHelp]";
        private const string GuideMark = "/guide/buttonguide_set";

        internal static void Speak()
        {
            try
            {
                string game = GameKeys();
                string mod = ModKeys();
                string text = game.Length > 0 ? $"Game keys: {game} Mod keys: {mod}" : $"Mod keys: {mod}";
                Log.Info($"{Tag} {text}");
                SpeechManager.Say(text, interrupt: true, force: true);
            }
            catch (Exception ex)
            {
                Log.Error($"{Tag} {ex.Message}");
            }
        }

        /// <summary>The visible button guide as sentences, "W, S, A, D: Select / Page. Confirm.", or "".</summary>
        private static string GameKeys()
        {
            // set path -> (keys, action)
            var sets = new SortedDictionary<string, (List<string> Keys, string Action)>(StringComparer.Ordinal);
            foreach (TMP_Text t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null || !UiUtil.IsVisible(t))
                    continue;
                string path = UiUtil.PathOf(t.transform);
                int at = path.IndexOf(GuideMark, StringComparison.Ordinal);
                if (at < 0)
                    continue;
                int end = path.IndexOf('/', at + GuideMark.Length);
                if (end < 0)
                    continue;
                string set = path.Substring(0, end);
                string rest = path.Substring(end + 1);
                string text = TextUtil.Clean(t.text).Trim();
                if (!sets.TryGetValue(set, out var entry))
                    entry = (new List<string>(), "");
                if (rest.StartsWith("guidetext/", StringComparison.Ordinal))
                    entry.Action = text;
                else if (rest.StartsWith("button", StringComparison.Ordinal) && text.Length > 0)
                    entry.Keys.Add(rest + "\t" + text);   // sorted below by button name
                sets[set] = entry;
            }
            var parts = new List<string>();
            foreach (var s in sets.Values)
            {
                if (s.Action.Length == 0)
                    continue;
                List<string> keys = s.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => k.Substring(k.IndexOf('\t') + 1)).ToList();
                parts.Add(keys.Count > 0 ? $"{string.Join(", ", keys)}: {s.Action}." : $"{s.Action}.");
            }
            return string.Join(" ", parts);
        }

        /// <summary>The mod's keys for where the player is (mod's words, from the bindings).</summary>
        private static string ModKeys()
        {
            var parts = new List<string>();
            void Add(string binding, string what)
            {
                string key = Hotkeys.KeyText(binding);
                if (key.Length > 0)
                    parts.Add($"{key}: {what}.");
            }

            if (BattleReader.Active)
            {
                Add("battle status", "party status");
                Add("battle enemies", "enemies");
                Add("battle log", "battle log");
                Add("battle verbosity", "HP after actions on or off");
            }
            else if (FieldState.IsExploring)
            {
                Add("next point of interest", "next point");
                Add("previous point of interest", "previous point");
                Add("next point of interest filter", "next filter");
                Add("previous point of interest filter", "previous filter");
                Add("walk to point of interest", "walk to the point");
                Add("directions to point of interest", "directions to the point");
                Add("way on", "the way on");
                Add("where am I", "where you are");
                Add("centre on the square", "centre on the square");
                Add("blobber mode", "blobber mode on or off");
            }
            else
            {
                Add("where am I", "your macca");
            }
            Add("repeat", "repeat the last line");
            Add("key help", "these keys");
            return string.Join(" ", parts);
        }
    }
}
