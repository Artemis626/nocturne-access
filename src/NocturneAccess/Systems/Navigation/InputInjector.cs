using System;
using HarmonyLib;
using Il2Cpp;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Presses the game's own logical actions, so the game's collision, doors and events stay in charge.
    ///
    /// SteamInputAssign.padcheck(pad, action, type) is asked every field tick about the movement and turn
    /// actions (0.0.12 diagnostic). A postfix turns the answer true for a held action, pad 0, press type DOWN,
    /// and only while exploring (FieldState): menus, battles and events never see an injected press.
    /// Technique: NocturneModernController (MIT), live-confirmed there for FD_Turn_Left / FD_Turn_Right.
    /// </summary>
    internal static class InputInjector
    {
        private static readonly bool[] Held = new bool[(int)SIActionName.MAX];
        private static bool _any;

        internal static long Injected { get; private set; }

        /// <summary>True once the game has asked padcheck about FD_Subjectivity (V): the first-person gate is open.</summary>
        internal static bool SubjectivityAsked { get; private set; }

        internal static void Install()
        {
            Patcher.Postfix("Navigation input", typeof(SteamInputAssign), nameof(SteamInputAssign.padcheck),
                new[] { typeof(int), typeof(SIActionName), typeof(SIPressType) },
                AccessTools.Method(typeof(InputInjector), nameof(AfterPadcheck)));
        }

        internal static void Hold(SIActionName action, bool on)
        {
            int a = (int)action;
            if ((uint)a >= Held.Length)
                return;
            Held[a] = on;
            _any = Array.IndexOf(Held, true) >= 0;
        }

        internal static void ReleaseAll()
        {
            Array.Clear(Held, 0, Held.Length);
            _any = false;
        }

        private static void AfterPadcheck(int __0, SIActionName __1, SIPressType __2, ref bool __result)
        {
            if (__1 == SIActionName.FD_Subjectivity && !SubjectivityAsked)
                SubjectivityAsked = true;
            if (!_any || __result || __0 != 0 || __2 != SIPressType.DOWN)
                return;
            int a = (int)__1;
            if ((uint)a >= Held.Length || !Held[a] || !FieldState.IsExploring)
                return;
            __result = true;
            Injected++;
        }
    }
}
