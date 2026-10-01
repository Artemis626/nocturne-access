using System;
using HarmonyLib;
using Il2Cpp;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Unlocks first person (V, FD_Subjectivity), which the game keeps for New Game+. In memory only.
    ///
    /// The 0.0.12 diagnostic (docs/NAVIGATION-DESIGN.md section 6) showed that padcheck is never asked about
    /// FD_Subjectivity on a first playthrough, and that inside fldPlayer.fldPlayerCalc the game checks only two
    /// story flags, both false: 3712 once per tick and 2800 three times per tick. The gate is taken to be flag
    /// [NocturneAccess_Navigation] FirstPersonFlag (default 3712).
    ///
    /// A postfix on EventBit.evtBitCheck answers true for that flag only while fldPlayerCalc runs
    /// (FieldState.InPlayerCalc), so story scripts and other New Game+ content still see the real value.
    /// Nothing is written: evtBitOn is never called, so the save is untouched. Technique: MIT reference mod
    /// EarlyCompendium (MatthiewNocturne), which answers true for flag 40 the same way.
    /// </summary>
    internal static class FirstPersonUnlock
    {
        private static int _flag;
        private static bool _loggedFirst;

        internal static long Forced { get; private set; }

        internal static void Install()
        {
            if (!Config.UnlockFirstPerson.Value)
            {
                Log.Info("[Nav] First-person unlock off (UnlockFirstPerson = false in the config).");
                return;
            }

            _flag = Config.FirstPersonFlag.Value;
            if (Patcher.Postfix("First person", typeof(EventBit), nameof(EventBit.evtBitCheck), new[] { typeof(int) },
                    AccessTools.Method(typeof(FirstPersonUnlock), nameof(AfterEvtBitCheck))))
                Log.Info($"[Nav] First-person unlock on: story flag {_flag} reads true inside fldPlayerCalc only, in memory.");
        }

        private static void AfterEvtBitCheck(int __0, ref bool __result)
        {
            if (__result || __0 != _flag || !FieldState.InPlayerCalc)
                return;

            __result = true;
            Forced++;
            if (_loggedFirst)
                return;
            _loggedFirst = true;
            try
            {
                Log.Info($"[Nav] First-person unlock: flag {_flag} answered true inside fldPlayerCalc for the first time.");
            }
            catch (Exception)
            {
                // logging only
            }
        }
    }
}
