using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Il2Cpp;
using Il2Cpplibsdf_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;
// Inside NocturneAccess, a bare "Input" means the NocturneAccess.Input namespace.
using UnityInput = UnityEngine.Input;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// First person (V, "Toggle POV") on a first playthrough, held in memory only.
    ///
    /// What the game does (disassembly of GameAssembly.dll, docs/NAVIGATION-DESIGN.md section 8):
    /// - fldPlayer.fldPlayerCalc runs fldPlayerCalc_Nml_syukan (first-person movement) instead of fldPlayerCalc_Nml
    ///   when EventBit.evtBitCheck(2800) is true; fldCamera.fldCamMain, calcCamMiwatasi, fldPlayerOn and
    ///   fldPlayerCalcForUnity read the same flag. Flag 2800 IS the first-person state.
    /// - fldProcess.ProcSequence toggles flag 2800 with evtBitOn / evtBitOff only when story flag 2208 or 2225 is set,
    ///   fldProcCheckFlag(0x40) holds, DDS3_PADCHECK_TRIG(R3, 1) (the V binding) fires and the interface is shown.
    ///   On this save 2208 and 2225 are false, so V never reaches the toggle.
    ///
    /// The game's own toggle would write flag 2800 into the save. Instead the mod keeps its own state and answers true
    /// whenever the game asks about flag 2800 (evtBitCheck postfix). It never calls evtBitOn: nothing is saved.
    /// V (Unity key) or the game's own R3 trigger toggles the state while exploring. If the game itself ever sets the
    /// flag (its gate open, for example on New Game+), the mod's toggle steps aside for the session.
    /// </summary>
    internal static class FirstPersonView
    {
        internal const int ViewFlag = 2800;
        private const long DebounceMs = 300;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Queue<string> Pending = new Queue<string>();
        private static readonly object Lock = new object();
        private static bool _installed;
        private static bool _gameOwnsFlag;
        private static bool _padReadFailed;
        private static long _lastToggleMs = long.MinValue / 2;

        internal static bool On { get; private set; }

        /// <summary>The mod can switch first person (hooks in, and the game has not taken the flag over).</summary>
        internal static bool Available => _installed && !_gameOwnsFlag;

        /// <summary>The player turned first person off with V: blobber mode does not switch it back on by itself.</summary>
        internal static bool UserTurnedOff { get; private set; }

        internal static void SetOn(bool on, bool speak)
        {
            if (!Available || On == on)
                return;
            On = on;
            Log.Info($"[Nav] First person {(on ? "on" : "off")} (blobber mode).");
            if (speak)
                SpeechManager.Say(on ? "First person" : "Third person", interrupt: true);
        }

        internal static void ClearUserChoice() => UserTurnedOff = false;

        internal static void Install()
        {
            if (!Config.UnlockFirstPerson.Value)
            {
                Log.Info("[Nav] First person off (UnlockFirstPerson = false in the config).");
                return;
            }

            Type i = typeof(int);
            bool ok = Patcher.Postfix("First person", typeof(EventBit), nameof(EventBit.evtBitCheck), new[] { i },
                AccessTools.Method(typeof(FirstPersonView), nameof(AfterCheck)));
            Patcher.Postfix("First person", typeof(EventBit), nameof(EventBit.evtBitOn), new[] { i },
                AccessTools.Method(typeof(FirstPersonView), nameof(AfterOn)));
            Patcher.Postfix("First person", typeof(EventBit), nameof(EventBit.evtBitOff), new[] { i },
                AccessTools.Method(typeof(FirstPersonView), nameof(AfterOff)));
            _installed = ok;
            if (ok)
                Log.Info($"[Nav] First person available: V toggles it while exploring; flag {ViewFlag} is answered in memory, never written.");
        }

        internal static void Tick()
        {
            if (!_installed)
                return;

            lock (Lock)
            {
                while (Pending.Count > 0)
                    Log.Info(Pending.Dequeue());
            }

            if (_gameOwnsFlag || !FieldState.IsExploring)
                return;

            bool key = UnityInput.GetKeyDown(KeyCode.V);
            bool pad = ReadPadToggle();
            if (!key && !pad)
                return;

            long now = Clock.ElapsedMilliseconds;
            if (now - _lastToggleMs < DebounceMs)
                return;
            _lastToggleMs = now;

            On = !On;
            UserTurnedOff = !On;
            Log.Info($"[Nav] First person {(On ? "on" : "off")} (by {(key ? "key V" : "pad R3 trigger")}).");
            SpeechManager.Say(On ? "First person" : "Third person", interrupt: true);
        }

        /// <summary>The game's own V check: DDS3_PADCHECK_TRIG(R3, 1), as fldProcess.ProcSequence calls it.</summary>
        private static bool ReadPadToggle()
        {
            if (_padReadFailed)
                return false;
            try
            {
                return dds3PadManager.DDS3_PADCHECK_TRIG(SDF_PADMAP.SDF_PADMAP_R3, 1);
            }
            catch (Exception ex)
            {
                _padReadFailed = true;
                Log.Warn($"[Nav] Reading the game's V trigger failed, using the V key only: {ex.Message}");
                return false;
            }
        }

        private static void AfterCheck(int __0, ref bool __result)
        {
            if (On && __0 == ViewFlag)
                __result = true;
        }

        private static void AfterOn(int __0)
        {
            if (__0 != ViewFlag || _gameOwnsFlag)
                return;
            _gameOwnsFlag = true;
            Queue($"[Nav] The game itself turned flag {ViewFlag} on (its own first-person toggle is open): the mod's V toggle steps aside.");
        }

        private static void AfterOff(int __0)
        {
            if (__0 != ViewFlag || !On)
                return;
            On = false;
            Queue($"[Nav] The game turned flag {ViewFlag} off: first person off.");
        }

        private static void Queue(string line)
        {
            try
            {
                lock (Lock)
                    Pending.Enqueue(line);
            }
            catch
            {
                // an exception here would reach the game
            }
        }
    }
}
