using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2Cppnewbattle_H;
using Il2Cppnewdata_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Battle
{
    // BattleReader: the units (HP and MP from the game's unit data), their names and numbers, the data log.
    internal static partial class BattleReader
    {
        private sealed class Unit
        {
            public int Form;
            public ushort Id;
            public int Hp, MaxHp, Mp, MaxMp;
            public string Name = "";
            public int Number;   // among units of the same name and side, fixed for the battle
        }

        // ---- Data ----

        private static void ReadUnits(bool speakChanges)
        {
            nbMainProcessData_t data = nbMainProcess.nbGetMainProcessData();
            if (data == null)
                return;

            var seen = new HashSet<int>();
            // Party: only slots that show a name on the battle party panel (formindex = panel slot, to verify in the log).
            for (int form = 0; form < PartySlots; form++)
            {
                if (!string.IsNullOrEmpty(PanelNames[form]))
                    ReadUnit(form, speakChanges, seen);
            }
            // Enemies keep their formindex when others fall while enemypcnt counts the living (22:14 session: with Pretas 1
            // and 2 dead, enemypcnt 1 and Preta 3 at formindex 6 was no longer read, so its target and damage had no HP).
            // Read every slot up to the highest one used this battle; a fallen enemy's slot returns null.
            int living = Math.Min(Math.Max((int)data.enemypcnt, 0), MaxEnemies);
            int found = 0;
            for (int k = 0; k < MaxEnemies; k++)
            {
                if (k >= _enemySlots && found >= living)
                    break;   // further slots only while fewer living enemies were found than enemypcnt says (a newcomer)
                if (ReadUnit(FirstEnemy + k, speakChanges, seen))
                {
                    found++;
                    _enemySlots = Math.Max(_enemySlots, k + 1);
                }
            }

            foreach (int gone in Units.Keys.Where(k => !seen.Contains(k)).ToList())
                Units.Remove(gone);

            // Enemies that joined, once no more have come for a moment (Gathering brings one every 200 ms): the whole side, as at
            // the start (Francesco, 27 Oct 2026: after "Specter appeared!" six Specters stood and nothing said so; 0.0.125).
            if (_joinedAt >= 0 && Clock.ElapsedMilliseconds - _joinedAt >= JoinSettleMs)
            {
                _joinedAt = -1;
                string list = EnemyList();
                Log.Info($"{Tag} Enemies joined: {list}.");
                if (list.Length > 0)
                    SpeechManager.Say(list + ".", interrupt: false, force: true);
            }
        }

        private const long JoinSettleMs = 1000;
        private static long _joinedAt = -1;

        /// <summary>"6 enemies: Specter 1, Specter 2, ..." with the numbers the damage lines use; one enemy: its name alone.</summary>
        private static string EnemyList()
        {
            List<Unit> enemies = Units.Values.Where(u => u.Form >= FirstEnemy).OrderBy(u => u.Form).ToList();
            string names = string.Join(", ", enemies.Where(u => u.Name.Length > 0).Select(DisplayName));
            if (names.Length == 0)
                return "";
            return enemies.Count == 1 ? names : $"{enemies.Count} enemies: {names}";
        }

        /// <summary>Reads one formindex; false when the slot is empty or fallen (null).</summary>
        private static bool ReadUnit(int form, bool speakChanges, HashSet<int> seen)
        {
            datUnitWork_t w = nbMainProcess.nbGetUnitWorkFromFormindex(form);
            if (w == null || w.maxhp == 0)
                return false;
            seen.Add(form);

            if (!Units.TryGetValue(form, out Unit u) || u.Id != w.id)
            {
                u = new Unit { Form = form, Id = w.id, Hp = w.hp, MaxHp = w.maxhp, Mp = w.mp, MaxMp = w.maxmp, Name = form < PartySlots ? PartyName(form) : NameOf(w.id) };
                Number(u);
                Units[form] = u;
                if (speakChanges && form >= FirstEnemy)
                    _joinedAt = Clock.ElapsedMilliseconds;   // an enemy that came after the start (the Specter's Gathering)
                return true;
            }

            int oldHp = u.Hp, oldMp = u.Mp;
            u.Hp = w.hp;
            u.MaxHp = w.maxhp;
            u.Mp = w.mp;
            u.MaxMp = w.maxmp;
            // The party's MP taken by an enemy (Mana Drain) or given back (Chakra Drop): Francesco, 21 and 27 Oct 2026 (0.0.125).
            // A drop while the party acts is its own skill's cost, which the menu already said.
            if (speakChanges && form < PartySlots && u.Mp != oldMp && (u.Mp > oldMp || (_sideKnown && _sideEnemy)))
            {
                string mp = u.Mp < oldMp ? $"{DisplayName(u)}: {oldMp - u.Mp} MP drained" : $"{DisplayName(u)}: {u.Mp - oldMp} MP restored";
                string full = $"{mp}, MP {u.Mp} of {u.MaxMp}.";
                SpeechManager.Say((Config.SpeakHpLeft?.Value ?? true) ? full : mp + ".", interrupt: false, force: true);
                BattleLog.Add(full);
            }
            if (speakChanges && u.Hp != oldHp)
            {
                // The amount, as the game's damage numbers show it ("7" over Pixie, 09:22 recording); an HP drop equal to
                // the acting unit's own skill cost is the cost (Lunge: 4 HP), said once.
                string who = DisplayName(u);
                string what;
                if (u.Hp < oldHp && form == _actionActor && _actionHpCost > 0 && oldHp - u.Hp == _actionHpCost)
                {
                    what = $"{_actionHpCost} HP cost";
                    _actionHpCost = 0;
                }
                else
                {
                    what = u.Hp < oldHp ? $"{oldHp - u.Hp} damage" : $"{u.Hp - oldHp} healed";
                }
                string text = u.Hp == 0 ? $"{who}: {what}, 0 HP." : $"{who}: {what}, HP {u.Hp} of {u.MaxHp}.";
                // Francesco (3 Oct 2026): the HP left after every hit is too chatty; F8 turns it on and off (Config.SpeakHpLeft,
                // off by default). A unit brought to 0 still says "0 HP"; the battle log (F5) and F2 / F3 always keep HP.
                string spoken = u.Hp == 0 || (Config.SpeakHpLeft?.Value ?? true) ? text : $"{who}: {what}.";
                SpeechManager.Say(spoken, interrupt: false, force: true);
                BattleLog.Add(text);
            }
            return true;
        }

        /// <summary>
        /// A party slot's name from the game's unit data (0.0.88): the demon's name (datDevilName) of the unit at that formindex, the
        /// party panel's text only for the protagonist (id 0, the player's chosen name) or when the data has none. The panel's
        /// row can lag behind a summon (Francesco, 16 Oct 2026, the Kin-Ki fight: the turn was said for the wrong unit after
        /// summoning; 21:19:19, Nue summoned into formindex 1, "Valkyrie's turn").
        /// </summary>
        internal static string PartyName(int form)
        {
            string panel = form >= 0 && form < PartySlots ? PanelNames[form] ?? "" : "";
            try
            {
                datUnitWork_t w = nbMainProcess.nbGetUnitWorkFromFormindex(form);
                string data = w != null && w.id != 0 ? NameOf(w.id) : "";
                if (data.Length == 0)
                    return panel;
                if (panel.Length > 0 && panel != data)
                    Log.Info($"{Tag} Party slot {form}: the panel says \"{panel}\", the unit data \"{data}\" (id {w.id}): the data is said.");
                return data;
            }
            catch
            {
                return panel;
            }
        }

        private static string NameOf(ushort id)
        {
            try
            {
                return TextUtil.Clean(datDevilName.Get(id));
            }
            catch
            {
                return "";
            }
        }

        /// <summary>The unit's name, numbered when several units of this battle share it ("Will o' Wisp 2").</summary>
        private static string DisplayName(Unit u)
        {
            string name = u.Name.Length > 0 ? u.Name : (u.Form >= FirstEnemy ? "Enemy" : "Ally");
            // Numbered when the battle had several of this name; the number stays when one falls.
            return u.Form >= FirstEnemy && NameCounts.TryGetValue(NameKey(u), out int count) && count > 1 ? $"{name} {u.Number}" : name;
        }

        private static string NameKey(Unit u) => (u.Form >= FirstEnemy ? "e:" : "p:") + u.Name;

        private static void Number(Unit u)
        {
            string key = NameKey(u);
            NameCounts.TryGetValue(key, out int count);
            NameCounts[key] = ++count;
            u.Number = count;
        }

        /// <summary>Logs the raw battle state when it changes, to check the field meanings against the screen.</summary>
        private static void LogData(bool force)
        {
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            if (d == null)
                return;
            string units = string.Join("; ", Units.Values.OrderBy(u => u.Form)
                .Select(u => $"{u.Form} {u.Name} id {u.Id} HP {u.Hp}/{u.MaxHp} MP {u.Mp}/{u.MaxMp}"));
            string line = $"turn {d.turn}, phase {d.phase}, activeunit {d.activeunit}, press4_p {d.press4_p}, press4_ten {d.press4_ten}, "
                          + $"hansya {d.press4_hansya}, miss {d.press4_miss}, playerpcnt {d.playerpcnt}, enemypcnt {d.enemypcnt}, "
                          + $"TarSelType {d.TarSelType}, exp {d.exp}, maka {d.maka} | {units}";
            if (!force && line == _lastData)
                return;
            _lastData = line;
            Log.Info($"{DataTag} {line}");
        }

        /// <summary>For the Analyze screen, whose level, HP and MP are images: "Level 4. HP 30 of 30. MP 24 of 24." for the
        /// enemy of that name (the one targeted when several share it), or "".</summary>
        internal static string UnitFigures(string name)
        {
            if (!_active)
                return "";
            List<Unit> matches = Units.Values.Where(u => u.Form >= FirstEnemy && u.Name == name).ToList();
            Unit unit = matches.Count == 1 ? matches[0] : null;
            if (unit == null && matches.Count > 1)
            {
                nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
                datUnitWork_t actor = d == null || d.activeunit < 0 ? null : nbMainProcess.nbGetUnitWorkFromFormindex(d.activeunit);
                if (actor != null)
                    unit = matches.FirstOrDefault(u => u.Form == actor.nowtform);
            }
            if (unit == null)
                return "";
            datUnitWork_t w = nbMainProcess.nbGetUnitWorkFromFormindex(unit.Form);
            string level = w != null ? $"Level {w.level}. " : "";
            return $"{level}HP {unit.Hp} of {unit.MaxHp}. MP {unit.Mp} of {unit.MaxMp}.";
        }
    }
}
