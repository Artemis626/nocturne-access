using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Unlocks first person (V, FD_Subjectivity), which the game keeps for New Game+. In memory only.
    ///
    /// Evidence (docs/NAVIGATION-DESIGN.md sections 6 and 7):
    /// - 0.0.12: padcheck is never asked about FD_Subjectivity; inside fldPlayer.fldPlayerCalc the game checks
    ///   only story flags 3712 (once per tick) and 2800 (three per tick), both false.
    /// - 0.0.13: with 3712 answering true there, the game started checking flag 3715 once per tick (false),
    ///   and still never asked for V. The gate is a chain of flags.
    ///
    /// A postfix on EventBit.evtBitCheck answers true for the flags in [NocturneAccess_Navigation]
    /// FirstPersonFlags, only while fldPlayerCalc runs (FieldState.InPlayerCalc), so story scripts and other
    /// New Game+ content still see the real values. Nothing is written: evtBitOn is never called.
    /// Technique: MIT reference mod EarlyCompendium (MatthiewNocturne), which answers true for flag 40.
    ///
    /// Auto-chain (FirstPersonAutoChain, development): until the game asks padcheck about FD_Subjectivity, a
    /// flag checked inside fldPlayerCalc that is neither forced nor in FirstPersonChainIgnore (2800, the one
    /// other flag seen there on a normal walk) and answers false is added to the forced set, at most 4 times
    /// per session, each logged. This finds the rest of the chain in one run.
    /// </summary>
    internal static class FirstPersonUnlock
    {
        private const int MaxAutoAdded = 4;

        private static readonly HashSet<int> Forced = new HashSet<int>();
        private static readonly HashSet<int> Ignore = new HashSet<int>();
        private static readonly Queue<string> Pending = new Queue<string>();
        private static readonly object Lock = new object();
        private static bool _autoChain;
        private static int _autoAdded;
        private static bool _loggedFirst;
        private static bool _loggedGateOpen;

        internal static void Install()
        {
            if (!Config.UnlockFirstPerson.Value)
            {
                Log.Info("[Nav] First-person unlock off (UnlockFirstPerson = false in the config).");
                return;
            }

            foreach (int f in ParseList(Config.FirstPersonFlags.Value))
                Forced.Add(f);
            foreach (int f in ParseList(Config.FirstPersonChainIgnore.Value))
                Ignore.Add(f);
            _autoChain = Config.FirstPersonAutoChain.Value;

            if (Patcher.Postfix("First person", typeof(EventBit), nameof(EventBit.evtBitCheck), new[] { typeof(int) },
                    AccessTools.Method(typeof(FirstPersonUnlock), nameof(AfterEvtBitCheck))))
                Log.Info($"[Nav] First-person unlock on: flags {string.Join(", ", Forced)} read true inside fldPlayerCalc only, in memory. " +
                         $"Auto-chain {(_autoChain ? "on" : "off")}, ignoring {string.Join(", ", Ignore)}.");
        }

        /// <summary>Writes the log lines the hook queued, and the moment the game first asks for V.</summary>
        internal static void Tick()
        {
            lock (Lock)
            {
                while (Pending.Count > 0)
                    Log.Info(Pending.Dequeue());
            }

            if (!_loggedGateOpen && InputInjector.SubjectivityAsked)
            {
                _loggedGateOpen = true;
                Log.Info($"[Nav] First-person gate open: the game now asks padcheck about FD_Subjectivity (V). Forced flags: {string.Join(", ", Forced)}.");
            }
        }

        private static void AfterEvtBitCheck(int __0, ref bool __result)
        {
            if (__result || !FieldState.InPlayerCalc)
                return;

            try
            {
                lock (Lock)
                {
                    if (!Forced.Contains(__0))
                    {
                        if (!_autoChain || InputInjector.SubjectivityAsked || _autoAdded >= MaxAutoAdded || Ignore.Contains(__0))
                            return;
                        Forced.Add(__0);
                        _autoAdded++;
                        Pending.Enqueue($"[Nav] First-person auto-chain: flag {__0} was checked inside fldPlayerCalc and answered false; " +
                                        $"it now answers true there ({_autoAdded} of {MaxAutoAdded} additions).");
                    }

                    __result = true;
                    if (!_loggedFirst)
                    {
                        _loggedFirst = true;
                        Pending.Enqueue($"[Nav] First-person unlock: flag {__0} answered true inside fldPlayerCalc for the first time.");
                    }
                }
            }
            catch
            {
                // an exception here would reach the game
            }
        }

        private static IEnumerable<int> ParseList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Enumerable.Empty<int>();
            return value.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => int.TryParse(p, out int n) ? n : -1)
                        .Where(n => n >= 0);
        }
    }
}
