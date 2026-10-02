using System;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppnewbattle_H;
using Il2Cppnewdata_H;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Battle
{
    /// <summary>
    /// Skill data for the battle reader, from the game's tables (docs/class-index.md "Battle"):
    /// - Cost, as the command menu draws it (cmpDrawSkill.cmpSkillNameCostDraw, read offline, 2 Oct 2026):
    ///   datNormalSkill.tbl[id].costtype 0 = no cost shown; 1 = HP: maxhp * cost / 100 + costbase (integer division);
    ///   otherwise MP: cost. Lunge showed "4 HP" for Art (48 max HP), Analyze "2 MP" (recording 09:22, 2 Oct 2026).
    /// - Element: datSkill.tbl[id].skillattr (MatthiewNocturne: 15 = utility), the index into a unit's affinities,
    ///   nbParty_t.aisyo[element]: the Preta's entries 1 to 4 and 6 have bit 31 set where its Analyze says
    ///   "Weak: Light/Magic", entry 7 is 0x10000 where it says "Null: Dark" (dump 09:16, 2 Oct 2026). A normal attack is
    ///   physical, element 0.
    /// </summary>
    internal static class BattleSkills
    {
        internal const int Physical = 0;
        private const uint WeakBit = 0x80000000;

        /// <summary>The id of the skill named 'name' among the unit's skills, or -1.</summary>
        internal static int SkillId(int formindex, string name)
        {
            datUnitWork_t w = nbMainProcess.nbGetUnitWorkFromFormindex(formindex);
            Il2CppStructArray<int> skills = w?.skill;
            if (skills == null || string.IsNullOrEmpty(name))
                return -1;
            int count = Math.Min(Math.Max(w.skillcnt, 0), skills.Length);
            for (int k = 0; k < count; k++)
            {
                int id = skills[k];
                string n;
                try
                {
                    n = TextUtil.Clean(datSkillName.Get(id, w.id) ?? "");
                }
                catch
                {
                    continue;
                }
                if (n == name)
                    return id;
            }
            return -1;
        }

        /// <summary>"4 HP", "2 MP", or "" (no cost, or not a skill of the unit acting now).</summary>
        internal static string CostOfActing(string itemText)
        {
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            if (d == null || d.activeunit < 0)
                return "";
            int id = SkillId(d.activeunit, itemText);
            if (id < 0)
                return "";
            return Cost(id, nbMainProcess.nbGetUnitWorkFromFormindex(d.activeunit), out string text) ? text : "";
        }

        /// <summary>The cost the menu draws for skill 'id' used by 'w'; false when the game draws none.</summary>
        internal static bool Cost(int id, datUnitWork_t w, out string text)
        {
            text = "";
            Il2CppReferenceArray<datNormalSkill_t> tbl = datNormalSkill.tbl;
            if (w == null || tbl == null || id < 0 || id >= tbl.Length || tbl[id] == null)
                return false;
            datNormalSkill_t s = tbl[id];
            if (s.costtype == 0)
                return false;
            if (s.costtype == 1)
            {
                int hp = w.maxhp * s.cost / 100 + s.costbase;
                text = $"{hp} HP";
            }
            else
            {
                text = $"{s.cost} MP";
            }
            return true;
        }

        /// <summary>HP the skill costs this unit, or 0 (MP costs and free skills).</summary>
        internal static int HpCost(int id, datUnitWork_t w)
        {
            Il2CppReferenceArray<datNormalSkill_t> tbl = datNormalSkill.tbl;
            if (w == null || tbl == null || id < 0 || id >= tbl.Length || tbl[id] == null || tbl[id].costtype != 1)
                return 0;
            return w.maxhp * tbl[id].cost / 100 + tbl[id].costbase;
        }

        /// <summary>The skill's element (datSkill.tbl[id].skillattr), or -1.</summary>
        internal static int Element(int id)
        {
            Il2CppReferenceArray<datSkill_t> tbl = datSkill.tbl;
            if (tbl == null || id < 0 || id >= tbl.Length || tbl[id] == null)
                return -1;
            return tbl[id].skillattr;
        }

        /// <summary>The target's affinity word for that element ("weak" or ""), and its raw value for the log.</summary>
        internal static bool IsWeak(int targetForm, int element, out uint raw)
        {
            raw = 0;
            if (element < 0)
                return false;
            nbParty_t p = nbMainProcess.nbGetPartyFromFormindex(targetForm);
            Il2CppStructArray<uint> aisyo = p?.aisyo;
            if (aisyo == null || element >= aisyo.Length)
                return false;
            raw = aisyo[element];
            return (raw & WeakBit) != 0;
        }
    }
}
