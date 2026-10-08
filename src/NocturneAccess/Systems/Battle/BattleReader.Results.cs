using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2Cppnewbattle_H;
using Il2Cppnewdata_H;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Battle
{
    // BattleReader: the results screen and the status keys (F2 our side, F3 the enemies).
    internal static partial class BattleReader
    {
        private static void OnResult(TMP_Text component, string raw)
        {
            // The results text is rewritten every frame (20:44 session): once per battle.
            if (_resultsShown)
                return;
            // A boss's death scene hides the battle UI long enough to count as gone (Yaksini, 7 Oct 2026: "Battle UI gone" at
            // 21:38:50, results on screen at 21:38:59, never spoken). Still the same battle while the game's field has not
            // changed since it began (0.0.61).
            if (!_active && (_battleField == int.MinValue || _leftBattleField || CurrentField() != _battleField))
                return;
            _resultsShown = true;
            LogData(force: true);
            string text = "Battle results." + Spoils();
            Log.Info($"{Tag} Results screen: {text}");
            SpeechManager.Say(text, interrupt: false, force: true);
            BattleLog.Add(text);
        }

        /// <summary>
        /// " 9 EXP. 27 macca. Medicine." from the main process data at the results screen: exp and maka matched the screen
        /// (EXP 9, macca 27, 09:22 recording); items from item[] / itemcnt[] named by datItemName.Get (the results screen
        /// asked for item 2, "Medicine"), the count added when more than one. The raw arrays are logged to check.
        /// </summary>
        private static string Spoils()
        {
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            if (d == null)
                return "";
            var parts = new List<string>();
            if (d.exp > 0)
                parts.Add($"{d.exp} EXP.");
            if (d.maka > 0)
                parts.Add($"{d.maka} macca.");
            var items = d.item;
            var counts = d.itemcnt;
            if (items != null)
            {
                Log.Info($"{Tag} Results items: item [{string.Join(" ", items)}] itemcnt [{(counts == null ? "null" : string.Join(" ", counts))}].");
                for (int k = 0; k < items.Length; k++)
                {
                    if (items[k] == 0)
                        continue;
                    string name;
                    try
                    {
                        name = TextUtil.Clean(datItemName.Get(items[k]) ?? "");
                    }
                    catch
                    {
                        continue;
                    }
                    int count = counts != null && k < counts.Length ? counts[k] : 1;
                    if (name.Length > 0)
                        parts.Add(count > 1 ? $"{name}, {count}." : $"{name}.");
                }
            }
            return parts.Count == 0 ? "" : " " + string.Join(" ", parts);
        }

        // ---- Status key ----

        // Short sentences, each ending in a full stop, so the screen reader pauses between them (Francesco, 22:14 session:
        // "Press turns: 1 full, 1 blinking" ran together). Split in two keys (Francesco, 2 Oct 2026: one key gave too much
        // at once): F2 our side, F3 the enemies.

        /// <summary>
        /// F8 (0.0.45): battle verbosity. Turns on or off the HP left spoken after each hit or heal (Config.SpeakHpLeft, kept in
        /// the config file). Only the lines spoken as actions happen; F2, F3 and the battle log (F5) always give HP.
        /// </summary>
        internal static void ToggleHpLeft()
        {
            bool on = !Config.SpeakHpLeft.Value;
            Config.SpeakHpLeft.Value = on;
            Config.Save(Config.SpeakHpLeft);
            Log.Info($"[Battle] HP left after actions: {(on ? "on" : "off")}.");
            SpeechManager.Say(on ? "HP after actions on." : "HP after actions off.", interrupt: true, force: true);
        }

        /// <summary>F2: whose turn, the press turns left, then each party member's HP and MP.</summary>
        internal static void SpeakStatus()
        {
            if (!_active)
            {
                SpeechManager.Say("Not in battle.", interrupt: true, force: true);
                return;
            }
            ReadUnits(speakChanges: false);
            var parts = new List<string>();
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            if (d != null)
            {
                string side = d.activeunit >= FirstEnemy ? "Enemy turn. " : "";
                if (d.activeunit >= 0 && d.activeunit < PartySlots && !string.IsNullOrEmpty(PanelNames[d.activeunit]))
                    side = $"{PanelNames[d.activeunit]}'s turn. ";
                parts.Add(side + TextUtil.Capitalize(PressTurns(d)));
            }
            foreach (Unit u in Units.Values.Where(u => u.Form < FirstEnemy).OrderBy(u => u.Form))
                parts.Add($"{DisplayName(u)}: HP {u.Hp} of {u.MaxHp}, MP {u.Mp} of {u.MaxMp}.");
            LogData(force: true);
            SpeechManager.Say(string.Join(" ", parts), interrupt: true, force: true);
        }

        /// <summary>F3 in battle: each living enemy with HP, and its affinities once Analyze has shown them.</summary>
        internal static void SpeakEnemies()
        {
            if (!_active)
                return;
            ReadUnits(speakChanges: false);
            List<Unit> enemies = Units.Values.Where(u => u.Form >= FirstEnemy && u.Hp > 0).OrderBy(u => u.Form).ToList();
            var parts = new List<string> { enemies.Count == 1 ? "1 enemy." : $"{enemies.Count} enemies." };
            foreach (Unit u in enemies)
            {
                string affinities = BattleAnalyze.AffinitiesOf(u.Name);
                parts.Add($"{DisplayName(u)}: HP {u.Hp} of {u.MaxHp}." + (affinities.Length > 0 ? " " + affinities : ""));
            }
            SpeechManager.Say(string.Join(" ", parts), interrupt: true, force: true);
        }
    }
}
