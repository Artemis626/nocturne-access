using System;
using Il2Cpp;
using Il2CppTMPro;
using NocturneAccess.Core;
using UnityEngine;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// Details for the Cathedral's Compendium list (MenuReader spec "Compendium", 0.0.61):
    /// "VIEW. By Race. Ame-no-Uzume, Megami, level 20, 1 of 23."
    ///
    /// Sources:
    /// - Rows (F6 dump 7 Oct 2026, 20:23:05): combUI_oubei/dlistsum/dlistsum_row/dlistsum_rowNN/Text_nameTM, Text_raceTM;
    ///   the title combUI_oubei/comtitle/Text_comtitleTM ("VIEW"); sort tabs dlistsum/dlistsumlr_base/dlistsortNN/Text_sortTM
    ///   ("By Race", "By Level"), the current one TMC01.
    /// - Levels are sprite digits, as in the fusion tables: a CounterCtr under the row (FusionReader.Levels), "lvnum".
    /// - The summon cost (0.0.80) too: the row's "moneynum" CounterCtr (row prefab in the e800 bundle: Text_nameTM, Text_raceTM,
    ///   lvnum, hatena, moneynum with six digits, dlisttex_summakka00 / 01), "Eligor, Fallen, level 29, 3200 macca".
    /// - The cursor: fclCombineInit.CMB_GBWK (cmbGlobalWork_t).ListWindow (lstListWindow_t).CursorInfo.CursorPos, Index the
    ///   list's top, Shift the row, ListNums the count, as in the shops (0.0.66; ZensyoSelInfo, used in 0.0.61, is the
    ///   Compendium menu). Used only when its row matches the highlighted one; logged on every change so the next session
    ///   can verify it.
    /// - The Register list (0.0.66) shares RowDetail and Prefix (title "REGISTER"); its rows are counted on screen.
    /// </summary>
    internal static class CompendiumReader
    {
        private const string Tag = "[Compendium]";
        private const string RootName = "combUI_oubei";

        private static Transform _root;
        private static string _lastSort = "";
        private static string _lastCursor = "";

        /// <summary>Race and level of the highlighted row: "Megami, level 20".</summary>
        internal static string RowDetail(TMP_Text name, string text)
        {
            Transform row = name == null ? null : name.transform.parent;
            if (row == null)
                return "";
            if (_root == null)   // Unity's null test: a destroyed screen counts as null
                _root = FindRoot(row);
            string race = Text(row.Find("Text_raceTM"));
            int? level = null, cost = null;
            foreach (CounterCtr c in row.GetComponentsInChildren<CounterCtr>(false))
            {
                string n = c.gameObject.name;
                if (level == null && n.StartsWith("lv", StringComparison.Ordinal))
                    level = c.value;
                else if (cost == null && n == "moneynum")
                    cost = c.value;
            }
            string detail = race;
            if (level.HasValue)
                detail = detail.Length > 0 ? $"{detail}, level {level.Value}" : $"level {level.Value}";
            // The summon cost (0.0.80): the row's "moneynum" counter, set by fclEncyc.DrawReadListwinElem (CounterCtr.Set) and
            // drawn as sprite digits; hidden (inactive) when the game draws no cost. Register rows have none.
            if (cost.HasValue)
                detail = detail.Length > 0 ? $"{detail}, {cost.Value} macca" : $"{cost.Value} macca";
            return detail;
        }

        /// <summary>The entry and count from the game's cursor when it is on this row, else null (the menu counts the rows).</summary>
        internal static (int, int)? Position(int slot)
        {
            try
            {
                // ZensyoSelInfo (0.0.61) is the Compendium menu (Register, View, Quit): Index 0 Shift 1 ListNums 3 on View, so
                // row 2 of the list read "2 of 3" (10 Oct 2026, 11:23). ListWindow is the list window the shops use for their
                // lists (fclShopInit.GBWK.ListWindow); unverified here, so the same row check guards it and every change is logged.
                Il2Cppcamp_H.cmpCursorPos_t pos = fclCombineInit.CMB_GBWK?.ListWindow?.CursorInfo?.CursorPos;
                if (pos == null)
                    return null;
                string cursor = $"Index {pos.Index} Shift {pos.Shift} ListNums {pos.ListNums} ShiftMax {pos.ShiftMax}";
                if (cursor != _lastCursor)
                {
                    _lastCursor = cursor;
                    Log.Info($"{Tag} Cursor {cursor}; highlighted row {slot}.");
                }
                if (pos.Shift + 1 != slot || pos.ListNums == 0)
                    return null;
                return (pos.Index + pos.Shift + 1, pos.ListNums);
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} Cursor: {ex.Message}");
                return null;
            }
        }

        /// <summary>On opening the screen's title and the sort ("VIEW. By Race"); later the sort only when it changed.</summary>
        internal static string Prefix(bool opening)
        {
            if (_root == null)
                return "";
            string sort = "";
            for (int k = 1; k <= 9; k++)
            {
                Transform t = _root.Find($"dlistsum/dlistsumlr_base/dlistsort{k:D2}/Text_sortTM");
                if (t == null)
                    break;
                TMP_Text tmp = t.GetComponent<TMP_Text>();
                if (tmp != null && t.gameObject.activeInHierarchy && tmp.text.Contains("<material=\"TMC01\">"))
                {
                    sort = TextUtil.Clean(tmp.text);
                    break;
                }
            }
            string result = "";
            if (opening)
            {
                string title = Text(_root.Find("comtitle/Text_comtitleTM"));
                result = Join(title, sort);
            }
            else if (sort.Length > 0 && sort != _lastSort)
            {
                result = sort;
            }
            _lastSort = sort;
            return result;
        }

        private static string Join(string a, string b) => a.Length == 0 ? b : b.Length == 0 ? a : $"{a}. {b}";

        private static Transform FindRoot(Transform t) => UiUtil.FindAncestor(t, RootName);

        private static string Text(Transform t) => UiUtil.TextOf(t);
    }
}
