using System;
using HarmonyLib;
using Il2Cpp;
using Il2Cpplibsdf_H;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Presses the game's own pad buttons (SDF_PADMAP) during the field update, so walking goes through the game's
    /// movement code, collision, doors and events exactly as with a real key.
    ///
    /// Disassembly of GameAssembly.dll (docs/NAVIGATION-DESIGN.md section 8): fldPlayerCalc_Nml (third person) reads
    /// walking from dds3PadManager.DDS3_ORGMAP_PRESS(L 4, R 5, U 6, D 7, cip 0) and GetPadAnalog; fldPlayerCalc_Nml_syukan
    /// (first person) from DDS3_PADCHECK_PRESS(L, R, U, D, cip 0) and PRESS / TRIG of L1 and R1. Both read the raw pad
    /// buffer dds3PadManager.dds3PadMap[1][0] through the game's button mapping, which is where the keyboard arrives.
    /// (0.0.13: padcheck(CMN_UpFront) moved nothing; 0.0.14: GetPadAnalog never moved with the keyboard.)
    ///
    /// Postfixes on DDS3_ORGMAP_PRESS and DDS3_PADCHECK_PRESS answer true for a held button, only while
    /// fldPlayer.fldPlayerCalc runs (FieldState.InPlayerCalc): menus, battles and events never see an injected press.
    /// These are Harmony postfixes like the mod's other hooks; NocturneModernController's startup crash came from a raw
    /// native hook on DDS3_PADCHECK_PRESS, not from Harmony. [NocturneAccess_Navigation] PadInjection = false disables them.
    /// </summary>
    internal static class PadInput
    {
        private const int Buttons = (int)SDF_PADMAP.SDF_PADMAP_COUNT;
        private static readonly bool[] Held = new bool[Buttons];
        private static bool _any;

        internal static bool Installed { get; private set; }

        internal static long Injected { get; private set; }

        internal static void Install()
        {
            if (!Config.PadInjection.Value)
            {
                Log.Info("[Nav] Pad injection off (PadInjection = false in the config).");
                return;
            }

            Type[] args = { typeof(SDF_PADMAP), typeof(int) };
            bool a = Patcher.Postfix("Navigation pad", typeof(dds3PadManager), nameof(dds3PadManager.DDS3_ORGMAP_PRESS), args,
                AccessTools.Method(typeof(PadInput), nameof(AfterPress)));
            bool b = Patcher.Postfix("Navigation pad", typeof(dds3PadManager), nameof(dds3PadManager.DDS3_PADCHECK_PRESS), args,
                AccessTools.Method(typeof(PadInput), nameof(AfterPress)));
            Installed = a || b;
        }

        internal static void Hold(SDF_PADMAP button, bool on)
        {
            int b = (int)button;
            if ((uint)b >= Buttons)
                return;
            Held[b] = on;
            _any = Array.IndexOf(Held, true) >= 0;
        }

        internal static void ReleaseAll()
        {
            Array.Clear(Held, 0, Held.Length);
            _any = false;
        }

        private static void AfterPress(SDF_PADMAP __0, ref bool __result)
        {
            if (!_any || __result || !FieldState.InPlayerCalc)
                return;
            int b = (int)__0;
            if ((uint)b >= Buttons || !Held[b])
                return;
            __result = true;
            Injected++;
        }
    }
}
