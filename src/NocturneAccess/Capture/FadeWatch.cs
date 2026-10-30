using System.Diagnostics;
using Il2Cpp;
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

        internal static void Tick()
        {
            if (_failed)
                return;
            try
            {
                int state = Read(out string detail);
                long now = Clock.ElapsedMilliseconds;
                if (state != _state)
                {
                    // Partial steps of a fade change every frame: only the passes between clear, partly and fully covered are logged.
                    if (_state >= 0)
                        Log.Info($"[Screen] Fade {(state == 0 ? "cleared" : state == 2 ? "covers the screen" : "partly covers the screen")}: {detail}.");
                    _state = state;
                    _coveredSince = now;
                    _warned = false;
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
            if (!active || a <= 0)
                return 0;
            return a >= 128 ? 2 : 1;
        }
    }
}
