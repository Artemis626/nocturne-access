using System;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Angle arithmetic shared by the walking systems. Compass bearings: 0 north (+Z), 90 east (+X), clockwise.
    /// PlayerKakudo: 0 south, 90 east, 180 north, 270 west; compass bearing = 180 - PlayerKakudo.
    /// </summary>
    internal static class Angles
    {
        /// <summary>Signed difference a - b folded into -180 .. 180.</summary>
        internal static float Diff(float a, float b) => ((a - b) % 360 + 540) % 360 - 180;

        internal static float Mod360(float a) => (a % 360 + 360) % 360;

        internal static int Mod360(int a) => (a % 360 + 360) % 360;

        /// <summary>The nearest multiple of 90 (a first-person facing).</summary>
        internal static float Square(float bearing) => Mod360((float)Math.Round(bearing / 90.0) * 90f);

        private static readonly string[] CompassNames = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        /// <summary>The nearest of the eight compass words for a bearing: "north", "south-east". On the world map also the arrow key.</summary>
        internal static string CompassWord(float bearing) => CompassNames[(int)Math.Round(Mod360(bearing) / 45.0) % 8];

        /// <summary>Compass bearing from (x, z) to (tx, tz).</summary>
        internal static float Bearing(float x, float z, float tx, float tz)
            => Mod360((float)(Math.Atan2(tx - x, tz - z) * 180.0 / Math.PI));
    }
}
