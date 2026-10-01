using System;
using System.Diagnostics;
using HarmonyLib;
using Il2Cpp;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Whether the player is exploring a field right now, and whether the field update is running.
    ///
    /// fldPlayer.fldPlayerCalc() is the field's per-tick player update (Proxy; the dispatcher to
    /// fldPlayerCalc_Nml / fldPlayerCalc_Nml_syukan, CONFIRMED by NocturneModernController, MIT).
    /// - Exploring: fldPlayerCalc ran within the last 100 ms. It stops in battles, menus, events and
    ///   loading (the Ref's rule, used by four of its mods).
    /// - InPlayerCalc: true only while fldPlayerCalc runs (prefix sets, postfix clears). The first-person
    ///   unlock will answer the gate flag only inside this window. Tick() clears it once per frame too,
    ///   so an exception thrown inside the game's update can never leave it stuck on.
    /// </summary>
    internal static class FieldState
    {
        private const long ExploringWindowMs = 100;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _lastCalcMs = long.MinValue / 2;
        private static int _errors;

        internal static bool InPlayerCalc { get; private set; }

        internal static bool IsExploring => Clock.ElapsedMilliseconds - _lastCalcMs <= ExploringWindowMs;

        /// <summary>fldPlayerCalc calls since start; the probe reads differences.</summary>
        internal static int CalcCount { get; private set; }

        internal static void Install()
        {
            Patcher.Prefix("Navigation", typeof(fldPlayer), nameof(fldPlayer.fldPlayerCalc), Type.EmptyTypes,
                AccessTools.Method(typeof(FieldState), nameof(BeforeCalc)));
            Patcher.Postfix("Navigation", typeof(fldPlayer), nameof(fldPlayer.fldPlayerCalc), Type.EmptyTypes,
                AccessTools.Method(typeof(FieldState), nameof(AfterCalc)));
        }

        internal static void Tick()
        {
            // OnUpdate never runs inside the game's field update.
            InPlayerCalc = false;
        }

        private static void BeforeCalc()
        {
            _lastCalcMs = Clock.ElapsedMilliseconds;
            CalcCount++;
            InPlayerCalc = true;
            try
            {
                BlobberMode.BeforeCalc();
            }
            catch (Exception ex)
            {
                if (++_errors <= 3)
                    Log.Error($"[Nav] Blobber update failed: {ex}");
            }
        }

        private static void AfterCalc()
        {
            InPlayerCalc = false;
        }
    }
}
