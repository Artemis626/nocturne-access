using System;
using System.Diagnostics;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Navigation;

namespace NocturneAccess.Systems
{
    /// <summary>
    /// Prizes behind minigames the mod cannot make accessible, given for another challenge (Francesco, 20 and 21 Nov 2026; memory
    /// "inaccessible-prizes"). 0.0.181: the Puzzle Boy's Geis Magatama once Metatron is beaten.
    ///
    /// Read offline (21 Nov 2026): Asakusa's script f027, procedure 007_end_pzl, gives the prize when flag 1477 (puzzle cleared) is on
    /// and flag 1478 (prize taken) is off: prm_DH_PLUS(13) (script command 0x122, scrParameterCommand.prm_DH_PLUS, which calls
    /// datCalc.datAddHearts(id) and nothing else), then evtBitOn(1478). Magatama 13 is "Geis" (common_en HEARTS_L0013,
    /// datHeartsName). datAddHearts returns at once when the Magatama is already owned. Flag 277 is the game's own mark of
    /// Metatron beaten: it opens the Fifth Kalpa's area 19 door 01d_01 (F045.WAP). The mod does the script's two steps, once, while
    /// exploring; with 1478 set the Puzzle Boy no longer gives it. Config GeisGift (on by default).
    /// </summary>
    internal static class PrizeGifts
    {
        private const string Tag = "[Prize]";
        private const int MetatronBeaten = 277, PuzzlePrizeTaken = 1478, Geis = 13;
        private const long CheckMs = 1000;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _lastCheck = long.MinValue / 2;
        private static bool _failed;

        internal static void Tick()
        {
            if (_failed || !(Config.GeisGift?.Value ?? true) || !FieldState.IsExploring)
                return;
            long now = Clock.ElapsedMilliseconds;
            if (now - _lastCheck < CheckMs)
                return;
            _lastCheck = now;
            try
            {
                fldGlobalWork_t gb = fldGlobal.fldGb;
                if (gb == null || gb.fieldID <= 0 || gb.fieldID >= 100)
                    return;
                if (!EventBit.evtBitCheck(MetatronBeaten) || EventBit.evtBitCheck(PuzzlePrizeTaken))
                    return;
                int added = datCalc.datAddHearts(Geis);
                EventBit.evtBitOn(PuzzlePrizeTaken);
                string name = TextUtil.Clean(datHeartsName.Get(Geis) ?? "");
                if (name.Length == 0)
                    name = "Geis";
                Log.Info($"{Tag} Metatron beaten (flag {MetatronBeaten}): Magatama {Geis} ({name}) given, datAddHearts returned {added}; flag {PuzzlePrizeTaken} set.");
                SpeechManager.SayHeld($"Metatron defeated: the Puzzle Boy's prize is yours. Obtained the {name} Magatama.", 4000);
            }
            catch (Exception ex)
            {
                _failed = true;
                Log.Warn($"{Tag} Geis gift: {ex.Message}");
            }
        }
    }
}
