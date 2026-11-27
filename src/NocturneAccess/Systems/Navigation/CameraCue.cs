using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// A tone when the game switches the camera (0.0.212; Francesco, 27 Nov 2026: the shift between the free camera and a
    /// fixed-camera room is obvious with sight and invisible without it, and the controls change with it; frequent early in the
    /// game). fldGlobalWork_t.cammode 0 is the free camera, where the mod's first person and blobber mode work; any other value is
    /// a fixed-camera room with the game's own controls (fldPlayer.fldPlayerCalc runs fldPlayerCalc_Nml for cammode 1 or 3,
    /// class index; the logs hold 73 switches each way between 0 and 1). The player's own V switch is spoken already
    /// ("First person" / "Third person") and changes no cammode. Dungeons only (the world map has no fixed camera). Config
    /// CameraSound, settings window Sounds.
    /// </summary>
    internal static class CameraCue
    {
        private static int _last = -1;   // -1: not read yet; 0 free; 1 fixed

        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null || !FieldState.IsExploring || FieldState.WorldMap(gb))
                return;
            int now = gb.cammode != 0 ? 1 : 0;
            if (now == _last)
                return;
            bool first = _last < 0;
            _last = now;
            // At the first reading only a fixed camera is news (a save loaded in a fixed-camera room).
            if (first && now == 0)
                return;
            bool on = Config.CameraSound?.Value ?? true;
            Log.Info($"[Nav] Camera: {(now == 1 ? $"fixed (cammode {gb.cammode})" : "free")} in field {gb.fieldID} area {gb.areaID}: tone{(on ? "" : " (off in the config)")}.");
            if (!on)
                return;
            if (now == 1)
                Earcons.CameraFixed();
            else
                Earcons.CameraFree();
        }
    }
}
