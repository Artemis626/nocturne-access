using System;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2Cpplibsdf_H;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// The game's own pad buttons (SDF_PADMAP) during the field update: reads the player's real presses and can press
    /// buttons, so walking goes through the game's movement code, collision, doors and events exactly as with a real key.
    /// It never hides a real press (Francesco, 1 Oct 2026: blobber mode must not change the game's input).
    ///
    /// Disassembly of GameAssembly.dll (docs/NAVIGATION-DESIGN.md sections 8 and 12): fldPlayerCalc_Nml (third person)
    /// reads walking from dds3PadManager.DDS3_ORGMAP_PRESS(L 4, R 5, U 6, D 7, cip 0); fldPlayerCalc_Nml_syukan (first
    /// person) from DDS3_PADCHECK_PRESS(L, R, U, D, cip 0) and (RD, L1, R1, cip 1). Both read the raw pad buffer
    /// dds3PadManager.dds3PadMap[1][0] through the game's button mapping, which is where the keyboard arrives.
    /// DDS3_PADCHECK_PRESS only reads (dds3ConfigMainSteam.GetConfigPadMap, then the gamepad and keyboard maps; the
    /// keyboard map writes only for R2, never read here), so Real can ask it the same question the game will ask.
    ///
    /// Postfixes on DDS3_ORGMAP_PRESS and DDS3_PADCHECK_PRESS act only while fldPlayer.fldPlayerCalc runs
    /// (FieldState.InPlayerCalc), or while the falling minigame reads its keys (InPipeKey, 0.0.112): menus, battles and
    /// events never see an injected press. A true answer is noted for
    /// the trace; a held button answers true. [NocturneAccess_Navigation] PadInjection = false disables the hooks.
    /// </summary>
    internal static class PadInput
    {
        internal const int Buttons = (int)SDF_PADMAP.SDF_PADMAP_COUNT;

        private static readonly bool[] Held = new bool[Buttons];
        private static readonly bool[] TraceSeen = new bool[Buttons];
        private static bool _anyHeld;
        private static bool _readingReal;
        private static bool _realFailed;

        internal static bool Installed { get; private set; }

        /// <summary>True only while the falling minigame reads its keys (pipeSection.ActionKey, PipePilot's prefix and postfix, 0.0.112).</summary>
        internal static bool InPipeKey;

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

        /// <summary>
        /// The player's real press of a button, asked exactly as fldPlayerCalc_Nml_syukan asks it
        /// (DDS3_PADCHECK_PRESS), without the mod's held buttons. False if the call ever fails.
        /// </summary>
        internal static bool Real(SDF_PADMAP button, int cip)
        {
            if (_realFailed)
                return false;
            _readingReal = true;
            try
            {
                return dds3PadManager.DDS3_PADCHECK_PRESS(button, cip);
            }
            catch (Exception ex)
            {
                _realFailed = true;
                Log.Warn($"[Nav] Reading the game's pad buttons failed; blobber mode cannot see keys: {ex.Message}");
                return false;
            }
            finally
            {
                _readingReal = false;
            }
        }

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
            if (_readingReal || !FieldState.InPlayerCalc && !InPipeKey)
                return;
            int b = (int)__0;
            if ((uint)b >= Buttons)
                return;
            if (__result)
                TraceSeen[b] = true;
            else if (_anyHeld && Held[b])
                __result = true;
        }
    }
}
