using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// A tone for damage floors (0.0.201; Francesco, 26 Nov 2026, the Kagutsuchi Tower's upper floors: "cursed tiles", as in the
    /// Labyrinth of Amala). The game marks them as floor-like collision polygons with Attr 0x4000 (field 36 area 0, dump
    /// 26 Nov 2026 18:55:00: 22 of them, overlays 10 above the floor; the Ikebukuro Tunnel's poison swamps carry the same bit),
    /// and stepping on one starts the damage reaction (fldPlayer.fldPlayerAct 52). Each tick while exploring: when one lies
    /// under the player, or under the point AheadDistance along the way the player is moving, within HeightBand of the player's
    /// height, the tone plays once (Earcons.DamageFloor). It plays again only after ClearMs with neither, so walking across a
    /// patch of tiles is one tone. Config DamageFloorSound.
    /// </summary>
    internal static class DamageFloorCue
    {
        private const string Tag = "[Nav:Damage]";
        private const uint DamageAttr = 0x4000;
        private const float AheadDistance = 90f;   // about one step of the walk, so the tone comes before the damage
        private const float HeightBand = 60f;      // the overlays lie 10 above their floor; a floor above or below stays out
        private const float MoveEpsilon = 0.5f;
        private const long ClearMs = 500;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly List<CollisionPoly> Damage = new List<CollisionPoly>();
        private static int _version = -1;
        private static bool _armed = true, _hasLast;
        private static float _lastX, _lastZ, _dirX, _dirZ;
        private static long _clearSince = -1;

        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (!(Config.DamageFloorSound?.Value ?? true) || gb == null || !FieldState.IsExploring || gb.fieldID < 10)
            {
                _hasLast = false;
                return;
            }
            if (_version != CollisionCache.Version)
            {
                _version = CollisionCache.Version;
                Damage.Clear();
                foreach (CollisionPoly p in CollisionCache.Polys)
                    if ((p.Attr & DamageAttr) != 0 && p.IsFloorLike && p.Points.Length >= 3)
                        Damage.Add(p);
                if (Damage.Count > 0)
                    Log.Info($"{Tag} Field {gb.fieldID} area {gb.areaID}: {Damage.Count} damage floor polygons (Attr 0x4000).");
            }
            if (Damage.Count == 0)
                return;

            float x = gb.playerX, y = gb.playerY, z = gb.playerZ;
            if (_hasLast)
            {
                float dx = x - _lastX, dz = z - _lastZ, d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d > MoveEpsilon)
                {
                    _dirX = dx / d;
                    _dirZ = dz / d;
                }
                else if (d == 0f)
                {
                    _dirX = _dirZ = 0f;   // standing: only the floor underfoot counts
                }
            }
            _lastX = x;
            _lastZ = z;
            _hasLast = true;

            CollisionPoly hit = Under(x, y, z);
            bool ahead = false;
            if (hit == null && (_dirX != 0f || _dirZ != 0f))
            {
                hit = Under(x + _dirX * AheadDistance, y, z + _dirZ * AheadDistance);
                ahead = hit != null;
            }
            long now = Clock.ElapsedMilliseconds;
            if (hit == null)
            {
                if (_clearSince < 0)
                    _clearSince = now;
                if (now - _clearSince >= ClearMs)
                    _armed = true;
                return;
            }
            _clearSince = -1;
            if (!_armed)
                return;
            _armed = false;
            Log.Info($"{Tag} Field {gb.fieldID} area {gb.areaID}: damage floor #{hit.Index} {(ahead ? "ahead" : "underfoot")} at {FieldData.F(x)} {FieldData.F(z)}: tone.");
            Earcons.DamageFloor();
        }

        private static CollisionPoly Under(float x, float y, float z)
        {
            foreach (CollisionPoly p in Damage)
            {
                if (!CollisionCache.ContainsXZ(p.Points, x, z))
                    continue;
                Vector4 a = p.Points[0];
                float py = a.y - (p.Normal.x * (x - a.x) + p.Normal.z * (z - a.z)) / p.Normal.y;
                if (Math.Abs(py - y) <= HeightBand)
                    return p;
            }
            return null;
        }
    }
}
