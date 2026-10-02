using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Battle
{
    /// <summary>
    /// The Analyze screen (09:16 session, 2 Oct 2026; nothing was spoken): Canvas_UI/banalyze_oubei(Clone) writes
    /// banalyze_name/banalyze_devil_name/banalyze_race_textTM ("Haunt"), .../banalyze_name_textTM ("Preta"),
    /// banalyze_skill/banalyze_skillNN/banalyze_textTM ("Feral Claw") and banalyze_attribute/banalyze_attribute_textTM
    /// ("Null: Dark • Weak: Light/Magic"), all in one frame. Level, HP and MP are sprites, so they come from the unit's data.
    /// Spoken once the texts settle and the name is visible: "Haunt Preta. Level 4. HP 30 of 30. MP 24 of 24. Null: Dark.
    /// Weak: Light/Magic. Skills: Feral Claw." The affinities are remembered by name for the enemy list (F3).
    /// </summary>
    internal static class BattleAnalyze
    {
        private const string Prefix = "Canvas_UI/banalyze_oubei(Clone)/";
        private const string RacePath = Prefix + "banalyze_name/banalyze_devil_name/banalyze_race_textTM";
        private const string NamePath = Prefix + "banalyze_name/banalyze_devil_name/banalyze_name_textTM";
        private const string AttributePath = Prefix + "banalyze_attribute/banalyze_attribute_textTM";
        private const string SkillPrefix = Prefix + "banalyze_skill/banalyze_skill";
        private const long SettleMs = 150;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Dictionary<string, (TMP_Text Component, string Text)> Texts = new Dictionary<string, (TMP_Text, string)>();
        private static readonly Dictionary<string, string> AffinitiesByName = new Dictionary<string, string>();
        private static bool _pending;
        private static bool _shown;   // spoken and still on screen
        private static long _changedAt;

        internal static void Install() => TmpRouter.Register("BattleAnalyze", Classify);

        private static TmpRouter.Handler Classify(string path)
        {
            if (!path.StartsWith(Prefix, StringComparison.Ordinal))
                return null;
            return (component, raw) =>
            {
                string text = TextUtil.Clean(raw);
                if (Texts.TryGetValue(path, out var old) && old.Text == text)
                    return;   // an identical rewrite is not new (it would keep the settle timer from running out)
                Texts[path] = (component, text);
                _pending = true;
                _changedAt = Clock.ElapsedMilliseconds;
            };
        }

        /// <summary>The affinities the Analyze screen showed for an enemy of this name, as speech ("Null: Dark. Weak: Light/Magic."), or "".</summary>
        internal static string AffinitiesOf(string name)
            => AffinitiesByName.TryGetValue(name, out string a) ? a : "";

        /// <summary>Called every frame by the battle reader.</summary>
        internal static void Tick()
        {
            if (_shown && !_pending && !(Texts.TryGetValue(NamePath, out var shown) && IsVisible(shown.Component)))
            {
                // Closed: forget the texts, so analyzing the same enemy again is spoken again.
                _shown = false;
                foreach (string key in Texts.Keys.ToList())
                    Texts[key] = (Texts[key].Component, "");
            }
            if (!_pending || Clock.ElapsedMilliseconds - _changedAt < SettleMs)
                return;
            if (!Texts.TryGetValue(NamePath, out var name) || !IsVisible(name.Component))
            {
                if (Clock.ElapsedMilliseconds - _changedAt > 2000)
                    _pending = false;
                return;
            }
            _pending = false;
            _shown = true;

            string race = Visible(RacePath);
            string attribute = Speakable(Visible(AttributePath));
            List<string> skills = Texts.Where(kv => kv.Key.StartsWith(SkillPrefix, StringComparison.Ordinal) && IsVisible(kv.Value.Component) && kv.Value.Text.Length > 0)
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Value.Text)
                .ToList();

            var parts = new List<string> { (race.Length > 0 ? race + " " : "") + name.Text + "." };
            string data = BattleReader.UnitFigures(name.Text);
            if (data.Length > 0)
                parts.Add(data);
            if (attribute.Length > 0)
            {
                parts.Add(attribute);
                AffinitiesByName[name.Text] = attribute;
            }
            if (skills.Count > 0)
                parts.Add("Skills: " + string.Join(", ", skills) + ".");
            string text = string.Join(" ", parts);
            Log.Info("[Battle] Analyze: " + text);
            SpeechManager.Say(text, interrupt: true, force: true);
        }

        private static string Visible(string path)
            => Texts.TryGetValue(path, out var t) && IsVisible(t.Component) ? t.Text : "";

        /// <summary>"Null: Dark • Weak: Light/Magic" as two sentences: the bullet becomes a full stop, so the reader pauses.</summary>
        private static string Speakable(string attribute)
        {
            if (attribute.Length == 0)
                return "";
            string text = string.Join(". ", attribute.Split('•').Select(s => s.Trim()).Where(s => s.Length > 0));
            return text.EndsWith(".") ? text : text + ".";
        }

        private static bool IsVisible(TMP_Text component)
        {
            try
            {
                return component != null && component.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }
    }
}
