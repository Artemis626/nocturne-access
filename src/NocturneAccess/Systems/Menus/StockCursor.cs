using Il2Cpp;
using Il2Cppcamp_H;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// The party and stock pick's cursor, read the way the game draws it (GameAssembly.dll, read offline, 12 Oct 2026):
    /// - cmpDrawStock.cmpdrawStock draws while cmpDrawStock.GBWK.SeqInfo.Flag has 0x2 (not 0x20), calling
    ///   cmpDrawStockInfo(GBWK.pStockInfo, 0).
    /// - cmpDrawStockInfo picks the highlighted rows by pStockInfo.DrawMode: 1 and 2 a stock row (target 0), 3 and 5 a party
    ///   row (target 1); 0, 4 and others none. The row is cmpMisc.cmpGetStockInfoIndex(info, target, 1):
    ///   LocalStock[target].ListIdx[SelPos[target].CursorPos.Index + Shift].
    /// - cmpChkStockCursorSel matches it against the party row (cmpDrawPartyPanel, type 1) or the stock draw position
    ///   (cmpDrawStockPanel, type 0, counted from 0).
    /// The name's material alone misses rows: cmpDrawStockPanel draws a dead (unit flag 0x2) or summoned (0x10) demon
    /// unhighlighted under the cursor (TMC05 dead Unicorn, 12 Oct 2026, 16:02:28 dump), so the pick fell silent there.
    /// Read only, with bounds checks: the game's own helper throws on a bad index.
    /// </summary>
    internal static class StockCursor
    {
        /// <summary>The Party pick slot under the game's cursor (1 to 4 party, 11 and up stock, as MenuReader numbers them), or -1.</summary>
        internal static int PartyPickSlot(out string detail)
        {
            detail = "";
            if (_failed)
                return -1;
            try
            {
                return Read(out detail);
            }
            catch (System.Exception ex)
            {
                _failed = true;   // read every frame: say it once, then leave the pick to the materials
                Core.Log.Error($"[Menu] Party pick cursor unreadable, materials only from now: {ex.Message}");
                return -1;
            }
        }

        private static bool _failed;

        private static int Read(out string detail)
        {
            detail = "";
            cmpDataStock_t gb = cmpDrawStock.GBWK;
            if (gb == null)
                return -1;
            byte flag = gb.SeqInfo?.Flag ?? 0;
            if ((flag & 0x20) != 0 || (flag & 0x2) == 0)
                return -1;
            cmpStockInfo_t info = gb.pStockInfo;
            if (info == null)
                return -1;

            int mode = info.DrawMode;
            int target;
            bool party;
            switch (mode)
            {
                case 1:
                case 2:
                    target = 0;
                    party = false;
                    break;
                case 3:
                case 5:
                    target = 1;
                    party = true;
                    break;
                default:
                    return -1;
            }

            var selPos = info.SelPos;
            var local = info.LocalStock;
            if (selPos == null || local == null || target >= selPos.Length || target >= local.Length)
                return -1;
            cmpCursorPos_t cursor = selPos[target]?.CursorPos;
            var listIdx = local[target]?.ListIdx;
            if (cursor == null || listIdx == null)
                return -1;
            int at = (sbyte)(byte)(cursor.Index + cursor.Shift);
            if (at < 0 || at >= listIdx.Length)
                return -1;
            int row = listIdx[at];
            detail = $"DrawMode {mode}, target {target}, Index {cursor.Index} Shift {cursor.Shift}, row {row}";
            if (row < 0)
                return -1;
            return party ? row + 1 : 11 + row;
        }
    }
}
