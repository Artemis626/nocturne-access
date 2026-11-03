using System;
using Il2Cpp;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Battle
{
    internal static partial class BattleReader
    {
        // ---- The stock pick: a dead demon to revive (0.0.149) ----
        //
        // Francesco, 3 Nov 2026, 15:12:39 (F6 dump): a Balm of Rising on the party's turn lists the dead demons of the stock on the
        // party panel itself (bparty_window01..04 rewritten: "Hell Biker", "Okuninushi", "Setanta", "Shadow"), with no highlight
        // material and no help banner, and the pick was silent. Read offline (nbPanelProcess.nbPanelStockSelect, RVA 0x23950F0):
        // each frame it draws the panel's rows with nbPanelDrawPP, lighting row r when r equals nbPanelProcess.StockIDListCursor,
        // the entries counted from StockIDListTop of StockIDList (StockIDListCount of them). The row under the cursor is said with
        // the name the game wrote on that panel row.

        private static int _stockCursor = int.MinValue, _stockTop = int.MinValue;
        private static string _stockName = "";
        private static bool _stockOpen;

        private static void PollStockPick()
        {
            int run, count, cursor, top;
            try
            {
                run = nbPanelProcess.nbPanelStockRun;
                count = nbPanelProcess.StockIDListCount;
                cursor = nbPanelProcess.StockIDListCursor;
                top = nbPanelProcess.StockIDListTop;
            }
            catch (Exception)
            {
                return;
            }
            bool open = run != 0 && count > 0 && cursor >= 0 && cursor < PartySlots;
            if (open != _stockOpen)
            {
                _stockOpen = open;
                Log.Info($"{Tag} Stock pick {(open ? "open" : "closed")}: run {run}, count {count}, cursor {cursor}, top {top}.");
                _stockCursor = _stockTop = int.MinValue;
                _stockName = "";
            }
            if (!open)
                return;
            string name = PanelNames[cursor] ?? "";
            if (cursor == _stockCursor && top == _stockTop && name == _stockName)
                return;
            _stockCursor = cursor;
            _stockTop = top;
            _stockName = name;
            if (name.Length == 0)
                return;
            Log.Info($"{Tag} Stock pick: row {cursor}, entry {top + cursor} of {count}, \"{name}\".");
            SpeechManager.Say(name, interrupt: true, force: true);
        }
    }
}
