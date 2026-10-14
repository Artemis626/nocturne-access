using Il2Cpp;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// The player's macca (0.0.80; Francesco, 14 Oct 2026: never spoken anywhere). The game draws it only as sprite digits,
    /// so it comes from game data: dds3GlobalWork.DDS3_GBWK.maka (the reference mod BetterNewGamePlus saves and restores
    /// the player's macca through it). Said by Delete (Where am I) and in the protagonist's Stats summary.
    /// </summary>
    internal static class Macca
    {
        private static bool _warned;

        /// <summary>The player's macca, or null when the game's work is not there.</summary>
        internal static int? Amount()
        {
            try
            {
                var gb = dds3GlobalWork.DDS3_GBWK;
                return gb == null ? (int?)null : gb.maka;
            }
            catch (System.Exception ex)
            {
                if (!_warned)
                {
                    _warned = true;
                    Log.Warn($"[Macca] Unreadable: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>"12300 macca", or "" when unreadable.</summary>
        internal static string Spoken()
        {
            int? m = Amount();
            return m.HasValue ? $"{m.Value} macca" : "";
        }

        /// <summary>
        /// Whether the shown stats are the protagonist's: unit 0 of dds3GlobalWork.DDS3_GBWK.unitwork (the player, as in
        /// BattleSkills.StockDetail) has the same level, maximum HP and maximum MP.
        /// </summary>
        internal static bool IsProtagonist(int? level, int? maxHp, int? maxMp)
        {
            if (!level.HasValue || !maxHp.HasValue || !maxMp.HasValue)
                return false;
            try
            {
                var units = dds3GlobalWork.DDS3_GBWK?.unitwork;
                if (units == null || units.Length == 0 || units[0] == null)
                    return false;
                Il2Cppnewdata_H.datUnitWork_t p = units[0];
                return p.level == level.Value && p.maxhp == maxHp.Value && p.maxmp == maxMp.Value;
            }
            catch
            {
                return false;
            }
        }
    }
}
