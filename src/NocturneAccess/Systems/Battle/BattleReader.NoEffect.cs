using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppnewbattle_H;
using Il2Cppnewdata_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Battle
{
    /// <summary>
    /// An ailment or instakill that does not take (0.0.176; Francesco, 20 Nov 2026: "No effect on X.", his words). The game draws
    /// no word for it: Binding Cry on all foes (20 Nov 13:28:48) left no popup and no status, so nothing was said.
    ///
    /// When an action banner lands for a skill (actor nowcommand 1) whose datNormalSkill entry carries an ailment chance
    /// (badlevel) or a death type (deadtype) and no HP effect (hpn and hpbase 0), its targets are kept: every living unit of the
    /// other side when the game's help for the skill (datSkillHelp_msg.Get) says "all", else the actor's nowtform. At the next
    /// action banner, or CheckMs later, each target that gained no status, kept its HP, did not fall and showed no word popup
    /// (Block, Missed) is named: "No effect on Mot, Aciel 2."
    /// </summary>
    internal static partial class BattleReader
    {
        private const long CheckMs = 6000;

        private sealed class AilmentCheck
        {
            public string Skill;
            public long At;
            public readonly Dictionary<int, (int Bad, int Hp)> Targets = new Dictionary<int, (int, int)>();
        }

        private static AilmentCheck _ailment;
        private static readonly Dictionary<int, long> PopupAt = new Dictionary<int, long>();

        /// <summary>Called by NoteAction before the new action is read: the previous ailment's result, then this action's.</summary>
        private static void NoteAilment(datUnitWork_t w, int actor)
        {
            FinishAilment();
            try
            {
                if (w == null || w.nowcommand != 1 || actor < 0)
                    return;
                int id = w.nowindex;
                Il2CppReferenceArray<datNormalSkill_t> tbl = datNormalSkill.tbl;
                if (tbl == null || id < 0 || id >= tbl.Length || tbl[id] == null)
                    return;
                datNormalSkill_t s = tbl[id];
                if ((s.badlevel == 0 && s.deadtype == 0) || s.hpn != 0 || s.hpbase != 0)
                    return;
                string help = "";
                try
                {
                    help = TextUtil.Clean(datSkillHelp_msg.Get(id) ?? "");
                }
                catch
                {
                }
                bool all = help.IndexOf(" all ", StringComparison.OrdinalIgnoreCase) >= 0;
                bool enemyActs = actor >= FirstEnemy;
                var check = new AilmentCheck { Skill = help, At = Clock.ElapsedMilliseconds };
                foreach (Unit u in Units.Values)
                {
                    bool otherSide = enemyActs ? u.Form < FirstEnemy : u.Form >= FirstEnemy;
                    if (!otherSide || u.Hp <= 0)
                        continue;
                    if (all || u.Form == w.nowtform)
                        check.Targets[u.Form] = (u.Bad, u.Hp);
                }
                Log.Info($"{Tag} Ailment check: skill {id} (badlevel {s.badlevel}, deadtype {s.deadtype}, targetarea {s.targetarea}, " +
                         $"help \"{help}\"), {(all ? "all" : $"target {w.nowtform}")}: forms [{string.Join(" ", check.Targets.Keys)}].");
                if (check.Targets.Count > 0)
                    _ailment = check;
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} Ailment check: {ex.Message}");
            }
        }

        private static void NotePopup(int form) => PopupAt[form] = Clock.ElapsedMilliseconds;

        /// <summary>Every frame: the check runs out CheckMs after its action.</summary>
        private static void TickAilment(long now)
        {
            if (_ailment != null && now - _ailment.At >= CheckMs)
                FinishAilment();
        }

        private static void FinishAilment()
        {
            AilmentCheck c = _ailment;
            _ailment = null;
            if (c == null || !_active)
                return;
            try
            {
                var untouched = new List<string>();
                foreach (var kv in c.Targets)
                {
                    if (!Units.TryGetValue(kv.Key, out Unit u))
                        continue;
                    bool popup = PopupAt.TryGetValue(kv.Key, out long at) && at >= c.At;
                    bool gained = (u.Bad & ~kv.Value.Bad) != 0;
                    if (popup || gained || u.Hp != kv.Value.Hp)
                        continue;
                    string name = DisplayName(u);
                    if (name.Length > 0)
                        untouched.Add(name);
                }
                Log.Info($"{Tag} Ailment check done: no effect on [{string.Join(", ", untouched)}].");
                if (untouched.Count == 0)
                    return;
                string text = $"No effect on {string.Join(", ", untouched)}.";
                SpeechManager.Say(text, interrupt: false, force: true);
                BattleLog.Add(text);
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} Ailment result: {ex.Message}");
            }
        }

        private static void ClearAilment()
        {
            _ailment = null;
            PopupAt.Clear();
        }
    }
}
