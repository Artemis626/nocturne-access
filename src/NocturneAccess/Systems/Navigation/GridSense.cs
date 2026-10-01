using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppfield_H;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>What lies one cell away in a direction.</summary>
    internal enum SideKind { Open, Wall, Door }

    /// <summary>Ahead, left and right of a position and facing.</summary>
    internal struct CellView
    {
        public SideKind Ahead, Left, Right;

        public bool SameAs(CellView o) => Ahead == o.Ahead && Left == o.Left && Right == o.Right;
    }

    /// <summary>
    /// Walls and doors around the player, from the game's own data (docs/NAVIGATION-DESIGN.md sections 6 and 9):
    /// - Walls: vertical collision polygons (|normal.y| &lt; 0.2) whose height span covers the player's chest
    ///   (game Y points down; floors at 0, walls 0 to -400). Each is reduced to its segment in X and Z.
    ///   NPC and item-box boxes (Attr 0x80, 100 x 100 around people and boxes) are not walls: in 0.0.16 they made the
    ///   cue say "wall" in front of the Soul. They are to be reported apart later ("person", "box").
    /// - Doors: fldEveHit.gEvnRng entries of type 10 named "NNd_MM" (the "Open Door" volumes, 0.0.12).
    ///   Doors are closed walls in the collision, so a door within one cell wins over the wall behind it.
    /// A side is a Wall when the segment from the player to one cell away crosses a wall.
    /// </summary>
    internal static class GridSense
    {
        private const float ChestHeight = 100f;   // above the floor, game units (Y down)
        private const float DoorReachExtra = 100f;
        private const float DoorHalfWidth = 250f;
        private const uint NpcBoxAttr = 0x80;
        private static readonly Regex DoorName = new Regex(@"^\d+d_\d+$");

        private struct Seg { public float Ax, Az, Bx, Bz; }

        private static readonly List<Seg> Walls = new List<Seg>();
        private static int _wallsVersion = -1;

        /// <summary>Unit direction in game X and Z for a facing in PlayerKakudo degrees (0 south, 90 east, 180 north, 270 west).</summary>
        internal static void Direction(float kakudo, out float dx, out float dz)
        {
            double r = kakudo * Math.PI / 180.0;
            dx = (float)Math.Round(Math.Sin(r), 6);
            dz = (float)Math.Round(-Math.Cos(r), 6);
        }

        internal static int Normalize90(float kakudo)
        {
            int k = (int)Math.Round(kakudo / 90.0) * 90;
            return ((k % 360) + 360) % 360;
        }

        internal static string FacingName(int kakudo90)
        {
            switch (kakudo90)
            {
                case 0: return "south";
                case 90: return "east";
                case 180: return "north";
                default: return "west";
            }
        }

        internal static CellView Look(float x, float y, float z, int facing, float cell)
        {
            return new CellView
            {
                Ahead = Side(x, y, z, facing, cell),
                Left = Side(x, y, z, facing + 90, cell),
                Right = Side(x, y, z, facing - 90, cell),
            };
        }

        internal static SideKind Side(float x, float y, float z, int kakudo, float cell)
        {
            Direction(kakudo, out float dx, out float dz);
            if (DoorWithin(x, z, dx, dz, cell + DoorReachExtra))
                return SideKind.Door;
            return WallCrossed(x, y, z, x + dx * cell, z + dz * cell) ? SideKind.Wall : SideKind.Open;
        }

        private static bool DoorWithin(float x, float z, float dx, float dz, float reach)
        {
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (ranges == null)
                return false;
            int n = Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            for (int i = 0; i < n; i++)
            {
                fldResHead_t h = ranges[i];
                if (h == null || h.resourceType != 10 || h.sisei == null)
                    continue;
                string name = h.resourceName;
                if (name == null || !DoorName.IsMatch(name))
                    continue;
                Vector4 p = h.sisei.pos;
                float rx = p.x - x, rz = p.z - z;
                float along = rx * dx + rz * dz;
                float cross = Math.Abs(rx * dz - rz * dx);
                if (along > 0 && along <= reach && cross <= DoorHalfWidth)
                    return true;
            }
            return false;
        }

        private static bool WallCrossed(float x, float y, float z, float tx, float tz)
        {
            RefreshWalls(y);
            foreach (Seg w in Walls)
                if (Intersect(x, z, tx, tz, w.Ax, w.Az, w.Bx, w.Bz))
                    return true;
            return false;
        }

        private static void RefreshWalls(float playerY)
        {
            int version = CollisionCache.Version;
            if (version == _wallsVersion)
                return;
            _wallsVersion = version;
            Walls.Clear();
            float chest = playerY - ChestHeight;
            foreach (CollisionPoly p in CollisionCache.Polys)
            {
                if (Math.Abs(p.Normal.y) >= 0.2f || p.Points.Length < 2 || (p.Attr & NpcBoxAttr) != 0)
                    continue;
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (Vector4 v in p.Points)
                {
                    minY = Math.Min(minY, v.y);
                    maxY = Math.Max(maxY, v.y);
                }
                if (chest < minY || chest > maxY)
                    continue;

                // The two points farthest apart in X and Z give the wall's footprint.
                Vector4 a = p.Points[0], b = p.Points[0];
                float best = -1;
                for (int i = 0; i < p.Points.Length; i++)
                    for (int j = i + 1; j < p.Points.Length; j++)
                    {
                        float ddx = p.Points[i].x - p.Points[j].x, ddz = p.Points[i].z - p.Points[j].z;
                        float d = ddx * ddx + ddz * ddz;
                        if (d > best)
                        {
                            best = d;
                            a = p.Points[i];
                            b = p.Points[j];
                        }
                    }
                if (best > 1)
                    Walls.Add(new Seg { Ax = a.x, Az = a.z, Bx = b.x, Bz = b.z });
            }
        }

        /// <summary>Segments PQ and AB intersect (touching counts).</summary>
        private static bool Intersect(float px, float pz, float qx, float qz, float ax, float az, float bx, float bz)
        {
            float d1 = Cross(ax, az, bx, bz, px, pz);
            float d2 = Cross(ax, az, bx, bz, qx, qz);
            float d3 = Cross(px, pz, qx, qz, ax, az);
            float d4 = Cross(px, pz, qx, qz, bx, bz);
            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;
            return (d1 == 0 && OnSeg(ax, az, bx, bz, px, pz)) || (d2 == 0 && OnSeg(ax, az, bx, bz, qx, qz))
                || (d3 == 0 && OnSeg(px, pz, qx, qz, ax, az)) || (d4 == 0 && OnSeg(px, pz, qx, qz, bx, bz));
        }

        private static float Cross(float ax, float az, float bx, float bz, float cx, float cz)
            => (bx - ax) * (cz - az) - (bz - az) * (cx - ax);

        private static bool OnSeg(float ax, float az, float bx, float bz, float cx, float cz)
            => Math.Min(ax, bx) - 0.01f <= cx && cx <= Math.Max(ax, bx) + 0.01f
            && Math.Min(az, bz) - 0.01f <= cz && cz <= Math.Max(az, bz) + 0.01f;
    }
}
