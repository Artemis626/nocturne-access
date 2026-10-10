using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Logs every hit, NPC and gimmick flag the game sets, and makes the collision copy read its switches at once (0.0.66).
    ///
    /// The setters (fldGlobal.fldGbSetHit, fldGbSetNpc, fldGbSetGimic; disassembly 10 Oct 2026) are called by the event
    /// commands fldCommand_FLD_ATARI_ON / _OFF, FLD_NPC_ON / _OFF, FLD_GIMIC_HOJI and evtCommand_OBJ_PATH_MOVE, PATH_DIR,
    /// PATH_RATE. The Nihilo cube hall switch (field 20 area 17, 10 Oct 2026, 11:30) showed a camera cut and no change in the
    /// hit flags the mod polls: these lines show what a switch event really sets, and for which area.
    /// A repeated call with the same value is logged once.
    /// </summary>
    internal static class GameFlagWatch
    {
        private static readonly Dictionary<string, int> Last = new Dictionary<string, int>();

        internal static void Install()
        {
            Type i = typeof(int);
            Type[] args = { i, i, i, i };
            Patcher.Postfix("Game flags", typeof(fldGlobal), nameof(fldGlobal.fldGbSetHit), args,
                AccessTools.Method(typeof(GameFlagWatch), nameof(AfterSetHit)));
            Patcher.Postfix("Game flags", typeof(fldGlobal), nameof(fldGlobal.fldGbSetNpc), args,
                AccessTools.Method(typeof(GameFlagWatch), nameof(AfterSetNpc)));
            Patcher.Postfix("Game flags", typeof(fldGlobal), nameof(fldGlobal.fldGbSetGimic), args,
                AccessTools.Method(typeof(GameFlagWatch), nameof(AfterSetGimic)));
        }

        private static void AfterSetHit(int fld, int area, int blk, int onoff) => Report("hit", fld, area, blk, onoff);

        private static void AfterSetNpc(int fld, int area, int hojiID, int onoff) => Report("NPC", fld, area, hojiID, onoff);

        private static void AfterSetGimic(int fld, int area, int hojiID, int toplast) => Report("gimmick", fld, area, hojiID, toplast);

        private static void Report(string kind, int fld, int area, int id, int value)
        {
            try
            {
                CollisionCache.SwitchesChanged();
                string key = $"{kind} {fld} {area} {id}";
                if (Last.TryGetValue(key, out int old) && old == value)
                    return;
                Last[key] = value;
                fldGlobalWork_t gb = fldGlobal.fldGb;
                string here = gb == null ? "" : $" (now in field {gb.fieldID} area {gb.areaID}, flag area {gb.areaID + 1})";
                Log.Info($"[Nav] The game sets the {kind} flag: field {fld}, area {area}, id {id}, value {value}{here}.");
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav] Game flag watch: {ex.Message}");
            }
        }
    }
}
