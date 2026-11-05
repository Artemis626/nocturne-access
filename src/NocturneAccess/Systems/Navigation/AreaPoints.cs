using System;
using System.Collections.Generic;
using System.IO;
using NocturneAccess.Core;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Arrival points read offline from the game's area files (Data/AreaPoints.txt, embedded; 0.0.153). Francesco, 5 Nov 2026
    /// (13:37 session): in the Amala Temple the game's objects hold no arrival points (WarpMap: "2 of the game's objects searched,
    /// 0 named"), so a same-area door's landing was known only once the player had gone through it. The Red Temple's entrance hall
    /// (field 34 area 11) is a row of cells joined by such doors (01d_06 lands on 01pos_06 at 1500 600, 01d_07 on 01pos_07): the way
    /// on said "beyond doors, no known way yet" until Francesco took them by hand. The points are the area file's own records
    /// (tools/unity/f2pos.pl); the floor under each is found in game, as for a landing in front of a partner door.
    /// </summary>
    internal static class AreaPoints
    {
        private const string Tag = "[Nav:Warp]";
        private static Dictionary<(int Field, int Area, string Name), Vector3> _points;

        internal static bool Find(int field, int area, string name, out Vector4 pos)
        {
            pos = default;
            Load();
            if (!_points.TryGetValue((field, area, name), out Vector3 p))
                return false;
            float y = CollisionCache.FloorUnder(p.x, p.y, p.z, out float floorY, groundOnly: true) != null ? floorY : p.y;
            pos = new Vector4(p.x, y, p.z, 1f);
            return true;
        }

        private static void Load()
        {
            if (_points != null)
                return;
            _points = new Dictionary<(int, int, string), Vector3>();
            try
            {
                using Stream s = typeof(AreaPoints).Assembly.GetManifestResourceStream("AreaPoints.txt");
                if (s == null)
                {
                    Log.Warn($"{Tag} The area points (AreaPoints.txt) are not in the mod.");
                    return;
                }
                using var reader = new StreamReader(s);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#", StringComparison.Ordinal))
                        continue;
                    string[] f = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (f.Length < 6 || !int.TryParse(f[0], out int field) || !int.TryParse(f[1], out int area)
                        || !float.TryParse(f[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                        || !float.TryParse(f[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                        || !float.TryParse(f[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
                        continue;
                    _points[(field, area, f[2])] = new Vector3(x, y, z);
                }
                Log.Info($"{Tag} Area points from the game's files: {_points.Count}.");
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} The area points could not be read: {ex.Message}");
            }
        }
    }
}
