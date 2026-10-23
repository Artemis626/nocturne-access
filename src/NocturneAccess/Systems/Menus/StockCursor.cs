using System.Collections.Generic;
using Il2Cpp;
using Il2Cppcamp_H;
using Il2CppTMPro;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// The party and stock pick's cursor, read the way the game draws it (GameAssembly.dll, read offline, 12 Oct 2026):
    /// - cmpDrawStock.cmpdrawStock draws while cmpDrawStock.GBWK.SeqInfo.Flag has 0x2 (not 0x20), calling
    ///   cmpDrawStockInfo(GBWK.pStockInfo, 0).
    /// - cmpDrawStockInfo picks the highlighted rows by pStockInfo.DrawMode through cmpMisc.cmpGetStockInfoIndex
    ///   (LocalStock[target].ListIdx[SelPos[target].CursorPos.Index + Shift]) and cmpChkStockCursorSel; what ListIdx holds
    ///   is not a panel row (0.0.74, see PartyPickSlot), so only the cursor position is used; which SelPos: CursorOf.
    /// The name's material alone misses rows: cmpDrawStockPanel draws a dead (unit flag 0x2) or summoned (0x10) demon
    /// unhighlighted under the cursor (TMC05 dead Unicorn, 12 Oct 2026, 16:02:28 dump), so the pick fell silent there.
    /// MenuReader asks for this cursor only while no row carries the highlight. Read only.
    /// </summary>
    internal static class StockCursor
    {
        /// <summary>
        /// The Party pick slot under the game's cursor, or -1. rows: MenuReader's slots in order (1 to 4 party, 11 and up stock)
        /// with their text and component.
        ///
        /// SelPos[0].CursorPos.Index + Shift counts the names the pick can select, in screen order, skipping the stock list's
        /// copies of summoned demons ("Summoned" beside them): 21:05:12 dump, 12 Oct 2026, Stats pick, DrawMode 1: positions
        /// 0 to 4 were Art, Dís, Unicorn, Zouchouten (party) and Raiju (stock 03, after summoned Unicorn and Zouchouten).
        /// ListIdx[position] is not a panel row (0, 2, 3, 5, 4 for those five): 0.0.72 mapped it as one and added a wrong
        /// second name ("All 2: Art, Unicorn"). DrawMode 1 seen in game (Skill user, Stats picks); the target picks' SelPos[1]
        /// (0.0.77) is counted the same way, to verify in game.
        /// </summary>
        internal static int PartyPickSlot(System.Func<IList<(int Slot, string Text, TMP_Text Component)>> rows, out string detail)
        {
            detail = "";
            if (_failed)
                return -1;
            try
            {
                return Read(rows, out detail);
            }
            catch (System.Exception ex)
            {
                _failed = true;   // read every frame: say it once, then leave the pick to the materials
                Core.Log.Error($"[Menu] Party pick cursor unreadable, materials only from now: {ex.Message}");
                return -1;
            }
        }

        private static bool _failed;

        /// <summary>
        /// Which cursor the pick moves, by DrawMode (cmpDrawStockInfo, read offline 14 Oct 2026): 1 draws SelPos[0], the
        /// first pick (who uses a skill, Stats); 3 and 5 draw SelPos[1], the target pick (3 with the user's SelPos[0] lit beside
        /// it: a skill's target; 5 alone: an item's target). cmpUpdate.cmpUpdateSequence calls cmpUpdateDevilSelect(0) and
        /// cmpUpdateDevilSelect(1), which move SelPos[BufIdx] (cmpMisc.cmpMoveStockCursor). 0.0.74 read DrawMode 1 only, so the
        /// Items target pick fell silent on a dead ally (14 Oct 2026, 13:46:50, Revival Bead on Raiju, drawn TMC05).
        /// </summary>
        private static int CursorOf(int drawMode)
        {
            // DrawMode 2 lights only the chosen user while its skill or item list is open: no pick (14 Oct 2026, 16:08:07 and
            // 16:08:12: read as one, backing out said "What will you use?. Zouchouten, 2 of 11"; 0.0.78).
            switch (drawMode)
            {
                case 1:
                    return 0;
                case 3:
                case 5:
                    return 1;
                default:
                    return -1;
            }
        }

        private static int Read(System.Func<IList<(int Slot, string Text, TMP_Text Component)>> rows, out string detail)
        {
            detail = "";
            cmpDataStock_t gb = cmpDrawStock.GBWK;
            if (gb == null)
                return -1;
            byte flag = gb.SeqInfo?.Flag ?? 0;
            if ((flag & 0x20) != 0 || (flag & 0x2) == 0)
            {
                detail = $"not drawn, flag 0x{flag:X}";
                return -1;
            }
            cmpStockInfo_t info = gb.pStockInfo;
            if (info == null)
                return -1;
            int target = CursorOf(info.DrawMode);
            detail = $"DrawMode {info.DrawMode}";
            var selPos = info.SelPos;
            if (target < 0 || selPos == null || selPos.Length <= target)
                return -1;
            cmpCursorPos_t cursor = selPos[target]?.CursorPos;
            if (cursor == null)
                return -1;
            int at = cursor.Index + cursor.Shift;
            detail = $"DrawMode {info.DrawMode}, cursor {target}, Index {cursor.Index} Shift {cursor.Shift}, position {at}";
            if (at < 0)
                return -1;
            int n = 0;
            foreach (var (slot, text, component) in rows())
            {
                if (text.Length == 0 || !UiUtil.IsVisible(component))
                    continue;
                if (slot > 10 && UiUtil.TextOf(component.transform.parent?.Find("Tex_summonedTM")).Length > 0)
                    continue;   // a summoned demon's copy in the stock list: the pick counts its party row
                if (n++ == at)
                    return slot;
            }
            return -1;
        }
    }
}
