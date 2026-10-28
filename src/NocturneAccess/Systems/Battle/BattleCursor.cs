using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2Cppnewbattle_H;
using Il2CppTMPro;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Battle
{
    /// <summary>
    /// The battle menu's cursor, read the way the game draws it (0.0.125; GameAssembly.dll read offline, 27 Oct 2026):
    /// nbCommSelProcess.DispCommandList2 draws rows drawlisty .. drawlisty + visible - 1 of commlist[type]; the row at
    /// drawcursor is TMC01, unless commdisable[type][row] is set: then it is TMC02 like every other unusable row. So an
    /// unusable skill under the cursor carries no highlight, and the battle menu fell silent on it (Francesco, 27 Oct 2026,
    /// the Specter fight: MP drained to 0, "the actions just go blank"; moving back to Attack was silent too, since the menu
    /// still had Attack as its current item). MenuReader asks for this cursor only while no row carries the highlight.
    /// </summary>
    internal static class BattleCursor
    {
        private const string Tag = "[Battle]";
        private const string Unusable = "Unavailable";   // Francesco's word (27 Oct 2026)

        /// <summary>The battle menu slot (bmenu_commandNN, 1-based) under the game's cursor, or -1.</summary>
        internal static int MenuSlot(Func<IList<(int Slot, string Text, TMP_Text Component)>> rows, out string detail)
        {
            detail = "";
            // Asked every frame while no row is lit, battle or not: the game's data only while a battle menu row is shown (the call
            // throws outside battle: 14:10:06, 28 Oct 2026; 0.0.126).
            IList<(int Slot, string Text, TMP_Text Component)> shown = rows();
            if (!shown.Any(r => r.Component != null && !string.IsNullOrEmpty(r.Text) && UiUtil.IsVisible(r.Component)))
                return -1;
            nbCommSelProcessData_t s = Data();
            if (s == null)
            {
                detail = "no command selection";
                return -1;
            }
            int slot = s.drawcursor - s.drawlisty + 1;
            var row = shown.FirstOrDefault(r => r.Slot == slot);
            if (row.Component == null || string.IsNullOrEmpty(row.Text) || !UiUtil.IsVisible(row.Component))
            {
                detail = $"cursor {s.drawcursor}, top {s.drawlisty}, list {s.drawtype}: row {slot} not shown";
                return -1;
            }
            detail = $"cursor {s.drawcursor}, top {s.drawlisty}, list {s.drawtype}, {(Disabled(s) ? "unusable" : "usable")}";
            return slot;
        }

        /// <summary>"Unavailable" before the item when the row under the game's cursor cannot be used now, else "".</summary>
        internal static string UnusablePrefix(bool opening)
        {
            nbCommSelProcessData_t s = Data();
            return s != null && Disabled(s) ? Unusable : "";
        }

        private static bool Disabled(nbCommSelProcessData_t s)
        {
            try
            {
                int type = s.drawtype, row = s.drawcursor;
                var lists = s.commdisable;
                if (lists == null || type < 0 || type >= lists.Length)
                    return false;
                var flags = lists[type];
                return flags != null && row >= 0 && row < flags.Length && flags[row] != 0;
            }
            catch
            {
                return false;
            }
        }

        private static int _errors;

        private static nbCommSelProcessData_t Data()
        {
            try
            {
                return nbCommSelProcess.GetCommSelProcessData();
            }
            catch (Exception ex)
            {
                // Gone while a battle closes, as the target data is (0.0.123).
                if (_errors++ < 3)
                    Log.Info($"{Tag} Command selection data not readable: {ex.Message}");
                return null;
            }
        }
    }
}
