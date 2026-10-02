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

        /// <summary>
        /// fldPlayerCalc_Nml_syukan (first-person movement) calls since start. It does not run during dialogue and events
        /// although fldPlayerCalc does (0.0.18 log, the Soul's dialogue): a change means the game took movement.
        /// </summary>
        internal static int SyukanCount { get; private set; }

        /// <summary>fldPlayerCalc_Nml (third-person movement) calls since start; with SyukanCount, a change means the game took movement.</summary>
        internal static int NmlCount { get; private set; }

        internal static void Install()
        {
            Patcher.Prefix("Navigation", typeof(fldPlayer), nameof(fldPlayer.fldPlayerCalc), Type.EmptyTypes,
                AccessTools.Method(typeof(FieldState), nameof(BeforeCalc)));
            Patcher.Postfix("Navigation", typeof(fldPlayer), nameof(fldPlayer.fldPlayerCalc), Type.EmptyTypes,
                AccessTools.Method(typeof(FieldState), nameof(AfterCalc)));
            Patcher.Prefix("Navigation", typeof(fldPlayer), nameof(fldPlayer.fldPlayerCalc_Nml_syukan), Type.EmptyTypes,
                AccessTools.Method(typeof(FieldState), nameof(BeforeSyukan)));
            Patcher.Prefix("Navigation", typeof(fldPlayer), nameof(fldPlayer.fldPlayerCalc_Nml), Type.EmptyTypes,
                AccessTools.Method(typeof(FieldState), nameof(BeforeNml)));
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
            // Each system guarded on its own: one failing must not stop the other's update. Pad buttons held by a system
            // that failed are let go, so a repeating error cannot leave the player walking on their own.
            try
            {
                BlobberMode.BeforeCalc();
            }
            catch (Exception ex)
            {
                PadInput.ReleaseAll();
                if (++_errors <= 3)
                    Log.Error($"[Nav] Blobber update failed: {ex}");
            }
            try
            {
                AutoWalk.BeforeCalc();
            }
            catch (Exception ex)
            {
                PadInput.ReleaseAll();
                try
                {
                    AutoWalk.Stop("error", null);
                }
                catch
                {
                    // Nothing may reach the game's field update from this prefix.
                }
                if (++_errors <= 3)
                    Log.Error($"[Nav] Auto-walk update failed: {ex}");
            }
        }

        private static void BeforeSyukan() => SyukanCount++;

        private static void BeforeNml() => NmlCount++;

        private static void AfterCalc()
        {
            InPlayerCalc = false;
        }
    }
}
