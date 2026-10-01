using System;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2Cpplibsdf_H;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// The game's own pad buttons (SDF_PADMAP) during the field update: reads the player's real presses, can hide some
    /// from the game, and can press buttons, so walking goes through the game's movement code, collision, doors and
    /// events exactly as with a real key.
    ///
    /// Disassembly of GameAssembly.dll (docs/NAVIGATION-DESIGN.md section 8): fldPlayerCalc_Nml (third person) reads
    /// walking from dds3PadManager.DDS3_ORGMAP_PRESS(L 4, R 5, U 6, D 7, cip 0); fldPlayerCalc_Nml_syukan (first person)
    /// from DDS3_PADCHECK_PRESS(L, R, U, D, cip 0). Both read the raw pad buffer dds3PadManager.dds3PadMap[1][0] through
    /// the game's button mapping, which is where the keyboard arrives (0.0.15: pad U held 500 ms walked 435 units).
    ///
    /// Postfixes on DDS3_ORGMAP_PRESS and DDS3_PADCHECK_PRESS act only while fldPlayer.fldPlayerCalc runs
    /// (FieldState.InPlayerCalc): menus, battles and events never see a hidden or injected press. Per call:
    /// 1. a true answer is recorded as the player's real press of that button this tick;
    /// 2. a suppressed button answers false (blobber mode takes W, A, D over);
    /// 3. a held button answers true.
    /// BeginTick (called from FieldState before each field update) turns the last tick's real presses into
    /// Down and Pressed (new this tick). [NocturneAccess_Navigation] PadInjection = false disables the hooks.
    /// </summary>
    internal static class PadInput
    {
        internal const int Buttons = (int)SDF_PADMAP.SDF_PADMAP_COUNT;

        private static readonly bool[] Held = new bool[Buttons];
        private static readonly bool[] Suppressed = new bool[Buttons];
        private static readonly bool[] RealThisTick = new bool[Buttons];
        private static readonly bool[] Down = new bool[Buttons];
        private static readonly bool[] Pressed = new bool[Buttons];
        private static readonly bool[] TraceSeen = new bool[Buttons];
        private static bool _anyHeld;
        private static bool _anySuppressed;

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

        /// <summary>Start of a field update: the previous tick's real presses become Down / Pressed.</summary>
        internal static void BeginTick()
        {
            for (int i = 0; i < Buttons; i++)
            {
                Pressed[i] = RealThisTick[i] && !Down[i];
                Down[i] = RealThisTick[i];
                if (RealThisTick[i])
                    TraceSeen[i] = true;
                RealThisTick[i] = false;
            }
        }

        internal static bool IsDown(SDF_PADMAP b) => Down[(int)b];

        internal static bool WasPressed(SDF_PADMAP b) => Pressed[(int)b];

        internal static void Hold(SDF_PADMAP button, bool on)
        {
            int b = (int)button;
            if ((uint)b >= Buttons)
                return;
            Held[b] = on;
            _anyHeld = Array.IndexOf(Held, true) >= 0;
        }

        internal static void ReleaseAll()
        {
            Array.Clear(Held, 0, Held.Length);
            _anyHeld = false;
        }

        internal static void Suppress(SDF_PADMAP button, bool on)
        {
            int b = (int)button;
            if ((uint)b >= Buttons)
                return;
            Suppressed[b] = on;
            _anySuppressed = Array.IndexOf(Suppressed, true) >= 0;
        }

        internal static void SuppressNone()
        {
            Array.Clear(Suppressed, 0, Suppressed.Length);
            _anySuppressed = false;
        }

        /// <summary>Buttons the player really pressed since the last call (for the trace), then reset.</summary>
        internal static string TakeTraceButtons()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Buttons; i++)
            {
                if (!TraceSeen[i])
                    continue;
                TraceSeen[i] = false;
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(((SDF_PADMAP)i).ToString().Replace("SDF_PADMAP_", ""));
            }
            return sb.ToString();
        }

        private static void AfterPress(SDF_PADMAP __0, ref bool __result)
        {
            if (!FieldState.InPlayerCalc)
                return;
            int b = (int)__0;
            if ((uint)b >= Buttons)
                return;
            if (__result)
            {
                RealThisTick[b] = true;
                if (_anySuppressed && Suppressed[b])
                    __result = false;
            }
            if (_anyHeld && Held[b] && !__result)
            {
                __result = true;
                Injected++;
            }
        }
    }
}
