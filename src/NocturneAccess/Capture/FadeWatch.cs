using System;
using System.Diagnostics;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using NocturneAccess.Core;
using NocturneAccess.Systems.Navigation;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Logs the game's screen fade (0.0.79), to explain a picture that went black and stayed black after a battle while the
    /// game itself ran on (14 Oct 2026, 20:53: speech, battle results, the field and the suspend all normal; nothing in the
    /// mod's log or Unity's Player.log). Log only: nothing is spoken and nothing is written to the game.
    /// The fade, read offline from GameAssembly.dll (14 Oct 2026): dds3KernelDraw.dds3FadeCheck is "dds3DrawFlag &amp; 3 is not 0";
    /// dds3FadeGetAlphaF returns fadergba[3] / 128 clamped to 1 while it is, else 0; fadergba[0..2] is the colour,
    /// fadenow / fademax the frame count. A fade that covers the screen for long is logged once.
    /// Restored in 0.0.135 (removed in 0.0.80 when the black screen did not come back): Francesco, 30 Oct 2026, a third black
    /// screen over the whole picture while the game answered, after two fusions at Asakusa's Cathedral (fusion scene field 807
    /// and back, 14:21 and 14:25); the warning no longer waits for the player to be walking.
    /// </summary>
    internal static class FadeWatch
    {
        private const long CoveredWarnMs = 5000;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static int _state = -1;            // 0 clear, 1 partly covered, 2 covered
        private static long _coveredSince;
        private static bool _warned;
        private static bool _failed;

        // ---- The field filter (0.0.136) ----
        // A full-screen layer of its own, drawn by a camera apart from the fade: dds3DefaultMain.FieldFilterCamera, switched by
        // FieldFilterOn / FieldFilterOff and given a strength by FieldFilterAlf(alpC). The fourth black screen (30 Oct 2026, 15:37:36,
        // arriving at the Shinjuku Medical Center by the Amala Link) came with the fade clear and one game warning in Unity's
        // Player.log during the Link's scene: "field filter override table not found, eve:646 idx:91 alp:-90". Log only.
        private static dds3DefaultMain _main;
        private static int _filterActive = -1;     // the camera's activeInHierarchy last logged, -1 unknown
        private static float _lastAlf = float.NaN;
        private static long _lastAlfLogAt = long.MinValue / 2;
        private static int _alfSkipped;
        private static long _lookedAt = long.MinValue / 2;
        private const long AlfLogMs = 250;

        internal static void Install()
        {
            Patcher.Postfix("Screen", typeof(dds3DefaultMain), nameof(dds3DefaultMain.FieldFilterOn), Type.EmptyTypes,
                AccessTools.Method(typeof(FadeWatch), nameof(AfterFilterOn)));
            Patcher.Postfix("Screen", typeof(dds3DefaultMain), nameof(dds3DefaultMain.FieldFilterOff), Type.EmptyTypes,
                AccessTools.Method(typeof(FadeWatch), nameof(AfterFilterOff)));
            Patcher.Postfix("Screen", typeof(dds3DefaultMain), nameof(dds3DefaultMain.FieldFilterAlf), new[] { typeof(float) },
                AccessTools.Method(typeof(FadeWatch), nameof(AfterFilterAlf)));
        }

        private static string Where()
        {
            var gb = Il2Cpp.fldGlobal.fldGb;
            return gb == null ? "no field" : $"field {gb.fieldID} area {gb.areaID}, exploring {FieldState.IsExploring}";
        }

        private static void AfterFilterOn(dds3DefaultMain __instance)
        {
            _main = __instance;
            Log.Info($"[Screen] Field filter on ({Where()}).");
        }

        private static void AfterFilterOff(dds3DefaultMain __instance)
        {
            _main = __instance;
            Log.Info($"[Screen] Field filter off ({Where()}).");
        }

        private static void AfterFilterAlf(dds3DefaultMain __instance, float __0)
        {
            _main = __instance;
            if (__0 == _lastAlf)
                return;
            long now = Clock.ElapsedMilliseconds;
            // A ramp writes a new strength every frame: one line per 250 ms, but a value out of 0 to 1 always.
            bool odd = __0 < 0f || __0 > 1f;
            if (!odd && now - _lastAlfLogAt < AlfLogMs)
            {
                _alfSkipped++;
                _lastAlf = __0;
                return;
            }
            Log.Info($"[Screen] Field filter strength {__0:0.###}{(odd ? " (out of 0 to 1)" : "")}{(_alfSkipped > 0 ? $", {_alfSkipped} changes between" : "")} ({Where()}).");
            _lastAlf = __0;
            _lastAlfLogAt = now;
            _alfSkipped = 0;
        }

        /// <summary>The filter camera's state: logged on change, warned once when on for 5 s while the player walks.</summary>
        private static void TickFilter(long now)
        {
            if (_main == null)
            {
                // Before any filter call: look the game's main object up, at most every 5 s.
                if (now - _lookedAt < 5000)
                    return;
                _lookedAt = now;
                _main = UnityEngine.Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<dds3DefaultMain>())?.TryCast<dds3DefaultMain>();
                if (_main == null)
                    return;
            }
            GameObject cam = _main.FieldFilterCamera;
            int active = cam == null ? 0 : cam.activeInHierarchy ? 1 : 0;
            if (active != _filterActive)
            {
                if (_filterActive >= 0 || active == 1)
                    Log.Info($"[Screen] Field filter camera {(active == 1 ? "active" : "inactive")}{(!float.IsNaN(_lastAlf) ? $", last strength {_lastAlf:0.###}" : "")} ({Where()}).");
                _filterActive = active;
            }
            // 0.0.137: no warning when it stays on: the filter is the fields' own colour layer, on through ordinary walking (30 Oct
            // 2026, 15:56 session: on in every area of the Medical Center and the Labyrinth, strengths -100 to -200).
        }

        internal static void Tick()
        {
            if (_failed)
                return;
            try
            {
                TickFilter(Clock.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _main = null;
                Log.Error($"[Screen] Field filter unreadable, its watch stopped: {ex.Message}");
            }
            try
            {
                int state = Read(out string detail);
                long now = Clock.ElapsedMilliseconds;
                if (state != _state)
                {
                    // Partial steps of a fade change every frame: only the passes between clear, partly and fully covered are logged.
                    if (_state >= 0)
                        Log.Info($"[Screen] Fade {(state == 0 ? "cleared" : state == 3 ? "stopped at full strength" : state == 2 ? "covers the screen" : "partly covers the screen")}: {detail}.");
                    _state = state;
                    _coveredSince = now;
                    _warned = false;
                }
                else if (state == 3 && !_warned && now - _coveredSince > CoveredWarnMs && FieldState.IsExploring)
                {
                    _warned = true;
                    Log.Warn($"[Screen] The fade has stayed at full strength for {(now - _coveredSince) / 1000} s, not running, while exploring (field {Il2Cpp.fldGlobal.fldGb?.fieldID}): {detail}.");
                }
                else if (state == 2 && !_warned && now - _coveredSince > CoveredWarnMs)
                {
                    _warned = true;
                    Log.Warn($"[Screen] The fade has covered the screen for {(now - _coveredSince) / 1000} s (exploring: {FieldState.IsExploring}, field {Il2Cpp.fldGlobal.fldGb?.fieldID}): {detail}.");
                }
            }
            catch (System.Exception ex)
            {
                _failed = true;
                Log.Error($"[Screen] Fade unreadable, watch stopped: {ex.Message}");
            }
        }

        /// <summary>The fade's state now (0 clear, 1 partly, 2 fully covering) and its values, for the log and the dumps.</summary>
        internal static int Read(out string detail)
        {
            uint flag = dds3KernelDraw.dds3DrawFlag;
            var rgba = dds3KernelDraw.fadergba;
            int r = 0, g = 0, b = 0, a = 0;
            if (rgba != null && rgba.Length >= 4)
            {
                r = rgba[0];
                g = rgba[1];
                b = rgba[2];
                a = rgba[3];
            }
            bool active = (flag & 3) != 0;
            detail = $"dds3DrawFlag 0x{flag:X}, colour {r} {g} {b}, alpha {a} of 128, frame {dds3KernelDraw.fadenow} of {dds3KernelDraw.fademax}";
            if (!active)
                return a >= 128 ? 3 : 0;   // a finished fade-out keeps its strength until the fade-in (0.0.136: logged apart)
            if (a <= 0)
                return 0;
            return a >= 128 ? 2 : 1;
        }
    }
}
