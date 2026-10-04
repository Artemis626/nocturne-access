using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2Cppfield_H;
using Il2Cppkernel_H;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader.Utils;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Doors the player has gone through, kept per save file (docs/EXPLORED-DOORS-PLAN.md; Francesco, 2 Oct 2026: pathfinding
    /// alone does not give a mental map of identical doors and corridors). PointsOfInterest speaks "explored" for generic doors
    /// only (no place name); every door gone through is recorded, so later steps (destination names, waypoints) can reuse it.
    ///
    /// When a door counts as explored (from the 14:48 session log, 2 Oct 2026):
    /// - leaving: fldEveHit.OldHitDoor holds the door volume the player stands in; it goes back to -1 between 0.2 and 1.6 s
    ///   BEFORE fldGb.areaID / fieldID change. So the last door stood in counts when the area changes within LeaveWindowMs of
    ///   leaving its volume. An area change with no door just before (stairs, loading zones) marks nothing;
    /// - arriving through a door: the player is placed inside the matching door's volume (OldHitDoor -1 -> 1 at 0.25 s after
    ///   areaID 5 -> 16): that door is marked once exploring resumes; without a hit within ArriveHitMs, the nearest door
    ///   within ArriveReach is.
    /// Identity: "field/area/resourceName" (the volume name, "01d_15"), stable across visits of an area.
    ///
    /// Per save file (Francesco): UserData/NocturneAccess/Explored/file01.txt for the game's File 1 (slMain.gslSelect + 1).
    /// - Loading a file (slMain.slLoadProc_closeFile) reads its list; the title screen (dds3TitleMain.dds3TitleInit) empties the
    ///   list, so a new game starts with none.
    /// - A door gone through is written at once to the file in play, so a crash or a quit without saving keeps it (the player
    ///   has been there); in a new game not yet saved it is held until the first save.
    /// - Saving to a file (slSaveProc_closeFile, slSaveWriteEnd_saveData) writes the whole list there: that file now holds this
    ///   game.
    /// Never blocks or changes input (CLAUDE.md).
    /// </summary>
    internal static class ExploredDoors
    {
        private const string Tag = "[Nav:Explored]";
        private const long LeaveWindowMs = 3000;   // seen: 0.2 s and 1.6 s (Terminal door) from leaving the volume to the area change
        private const long ArriveWindowMs = 15000; // loading and the fade-in, until the player can move
        private const long ArriveHitMs = 1500;     // seen: OldHitDoor set 0.25 s after the change
        private const float ArriveReach = 300f;
        private const float CrossReach = 120f;     // beyond a door's plane: gone through it (0.0.51)
        private const float JumpReach = 300f;      // one frame's move beyond this: the game moved the player (walking is 16 a tick)
        private const float PairReach = 500f;      // a door's other volume (Amala 01d_02 and 01d_03: 360 apart)
        private static readonly Regex DoorName = new Regex(@"^\d+d_\d+$");
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private static readonly HashSet<string> Doors = new HashSet<string>();
        private static int _file = -1;            // slMain.gslSelect of the file in play, -1 for a new game not saved yet

        private static int _field = int.MinValue, _area = int.MinValue;
        private static int _hit = -1;
        private static string _leaving;           // the last door volume stood in, as its key
        private static long _leftMs = long.MinValue / 2;   // when the player was last inside it
        private static string _cameFrom;          // set at an area change through a door, until the arrival door is marked
        private static long _changeMs, _exploringSinceMs = -1;
        private static string _crossing;          // the door volume just left in this area, until it is known whether the player went through
        private static float _doorX, _doorZ, _entryX, _entryZ, _normalX, _normalZ;
        private static float _lastX, _lastZ;
        private static bool _hasLast;
        private static long _jumpMs = long.MinValue / 2;

        private static string Folder => Path.Combine(MelonEnvironment.UserDataDirectory, "NocturneAccess", "Explored");

        internal static string Key(int field, int area, string resName) => $"{field}/{area}/{resName}";

        internal static bool IsExplored(int field, int area, string resName) => Doors.Contains(Key(field, area, resName));

        internal static void Install()
        {
            var proc = new[] { typeof(dds3ProcessID_t) };
            Patcher.Postfix("Explored doors", typeof(slMain), nameof(slMain.slLoadProc_closeFile), proc,
                AccessTools.Method(typeof(ExploredDoors), nameof(AfterLoad)));
            Patcher.Postfix("Explored doors", typeof(slMain), nameof(slMain.slSaveProc_closeFile), proc,
                AccessTools.Method(typeof(ExploredDoors), nameof(AfterSaveClose)));
            Patcher.Postfix("Explored doors", typeof(slMain), nameof(slMain.slSaveWriteEnd_saveData), Type.EmptyTypes,
                AccessTools.Method(typeof(ExploredDoors), nameof(AfterSaveWriteEnd)));
            Patcher.Postfix("Explored doors", typeof(dds3TitleMain), nameof(dds3TitleMain.dds3TitleInit), new[] { typeof(int) },
                AccessTools.Method(typeof(ExploredDoors), nameof(AfterTitleInit)));
        }

        // ---- Save files ----

        private static void AfterLoad()
        {
            try
            {
                int file = slMain.gslSelect;
                Doors.Clear();
                _file = file;
                ResetTracking();
                string path = PathOf(file);
                if (File.Exists(path))
                    foreach (string line in File.ReadAllLines(path))
                        if (line.StartsWith("door ", StringComparison.Ordinal))
                            Doors.Add(line.Substring(5).Trim());
                Log.Info($"{Tag} File {file + 1} loaded (gslSelect {slMain.gslSelect}, gslSelTop {slMain.gslSelTop}, slChooseSaveIndex {slMain.slChooseSaveIndex}): {Doors.Count} explored doors.");
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} The explored list could not be read: {ex.Message}");
            }
        }

        private static void AfterSaveClose() => Saved("slSaveProc_closeFile");

        private static void AfterSaveWriteEnd() => Saved("slSaveWriteEnd_saveData");

        private static void Saved(string how)
        {
            try
            {
                int file = slMain.gslSelect;
                _file = file;
                WriteAll(file);
                Log.Info($"{Tag} Saved to file {file + 1} ({how}; gslSelect {slMain.gslSelect}, gslSelTop {slMain.gslSelTop}, slChooseSaveIndex {slMain.slChooseSaveIndex}): {Doors.Count} explored doors written.");
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} The explored list could not be saved: {ex.Message}");
            }
        }

        private static void AfterTitleInit()
        {
            if (Doors.Count > 0 || _file >= 0)
                Log.Info($"{Tag} Title screen: the explored list of file {_file + 1} is put away ({Doors.Count} doors); a new game starts with none.");
            Doors.Clear();
            _file = -1;
            ResetTracking();
        }

        private static string PathOf(int file) => Path.Combine(Folder, $"file{file + 1:00}.txt");

        private static void WriteAll(int file)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(PathOf(file), Doors.OrderBy(d => d, StringComparer.Ordinal).Select(d => "door " + d));
        }

        // ---- Going through doors ----

        private static void ResetTracking()
        {
            _field = _area = int.MinValue;
            _hit = -1;
            _leaving = null;
            _cameFrom = null;
            _crossing = null;
            _hasLast = false;
            _exploringSinceMs = -1;
        }

        /// <summary>Every frame (OnUpdate), also while loading: the area change is seen when it happens.</summary>
        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;
            long now = Clock.ElapsedMilliseconds;
            int field = gb.fieldID, area = gb.areaID;
            if (_field == int.MinValue)
            {
                _field = field;
                _area = area;
                return;
            }

            if (field != _field || area != _area)
            {
                _field = field;
                _area = area;
                _hit = -1;
                _exploringSinceMs = -1;
                if (_leaving != null && now - _leftMs <= LeaveWindowMs)
                {
                    Mark(_leaving, $"left through it, {now - _leftMs} ms before the change to {field}/{area}");
                    _cameFrom = _leaving;
                    _changeMs = now;
                }
                else
                {
                    _cameFrom = null;
                }
                _leaving = null;
                _crossing = null;
                _hasLast = false;
                return;
            }

            CheckCrossing(gb, now);

            // The door volume the player stands in: its key is taken while exploring, when the area's own volumes are surely
            // loaded; the time keeps running while the door's transition (no longer exploring) starts.
            int hit = fldEveHit.OldHitDoor;
            if (hit < 0)
            {
                if (_hit >= 0 && _leaving != null)
                    _crossing = _leaving;   // left the volume: did the player come out on its other side?
                _hit = -1;
            }
            else
            {
                if (hit != _hit && FieldState.IsExploring)
                {
                    _hit = hit;
                    string key = DoorKey(field, area, hit);
                    _leaving = key;
                    _crossing = null;
                    if (key != null && DoorCentre(hit, out _doorX, out _doorZ, out _normalX, out _normalZ))
                    {
                        _entryX = gb.playerX - _doorX;
                        _entryZ = gb.playerZ - _doorZ;
                    }
                    if (key != null && _cameFrom != null)
                    {
                        Mark(key, $"came in through it from {_cameFrom}");
                        _cameFrom = null;
                    }
                }
                if (hit == _hit)
                    _leftMs = now;
            }

            if (_cameFrom == null)
                return;
            if (now - _changeMs > ArriveWindowMs)
            {
                Log.Info($"{Tag} No door to mark on arriving from {_cameFrom}: the player did not take control in time.");
                _cameFrom = null;
                return;
            }
            if (!FieldState.IsExploring)
                return;
            if (_exploringSinceMs < 0)
                _exploringSinceMs = now;
            if (now - _exploringSinceMs < ArriveHitMs)
                return;
            string near = NearestDoor(gb, out float d);
            if (near != null)
                Mark(near, $"came in through it from {_cameFrom} (nearest door, {FieldData.F(d)} away)");
            else
                Log.Info($"{Tag} No door within {ArriveReach} of the arrival point from {_cameFrom}.");
            _cameFrom = null;
        }

        /// <summary>
        /// Doors gone through without an area change (0.0.51). The Amala Network's doors (field 18, 4 Oct 2026) open and the
        /// game walks the player through, or move them to a marker elsewhere in the same area: only fldGb's block numbers
        /// change, so the two Soul rooms entered at 14:44 and 14:47 were never marked. After leaving a door volume (within
        /// LeaveWindowMs) the door counts as gone through when the player stands CrossReach beyond its plane on the other side
        /// from where they entered (the side along the volume's facing, gEvnRngBox.boxnml, or along the entry direction when it
        /// has none), or when the player jumped more than JumpReach in one frame (moved by the game). A door volume close behind
        /// it on the far side (doors are often two volumes, one per side: 01d_02 and 01d_03, 360 apart) is marked with it, as
        /// "came in through it" does after an area change. Never used across an area change, which the code above handles.
        /// </summary>
        private static void CheckCrossing(fldGlobalWork_t gb, long now)
        {
            float px = gb.playerX, pz = gb.playerZ;
            bool jumpedNow = _hasLast && (px - _lastX) * (px - _lastX) + (pz - _lastZ) * (pz - _lastZ) > JumpReach * JumpReach;
            _lastX = px;
            _lastZ = pz;
            _hasLast = true;
            if (jumpedNow)
            {
                // Said for every move by the game inside the area (Francesco, 4 Oct 2026: the Amala Network's warps are not
                // shown, and the player must know the place changed). The floor banner the game shows again follows it.
                _jumpMs = now;
                SpeechManager.Say("Warped.", interrupt: false);
                Log.Info($"{Tag} Moved by the game to {FieldData.F(px)} {FieldData.F(pz)} in the same area.");
            }
            bool jumped = now - _jumpMs <= 500;
            if (_crossing == null)
                return;
            if (now - _leftMs > LeaveWindowMs)
            {
                _crossing = null;
                return;
            }
            float ax = _entryX, az = _entryZ;
            if (_normalX * _normalX + _normalZ * _normalZ > 0.25f)
            {
                ax = _normalX;
                az = _normalZ;
            }
            float alen = (float)Math.Sqrt(ax * ax + az * az);
            if (alen < 1f && !jumped)
                return;
            float entrySide = alen < 1f ? 0f : (_entryX * ax + _entryZ * az) / alen;
            float side = alen < 1f ? 0f : ((px - _doorX) * ax + (pz - _doorZ) * az) / alen;
            bool through = entrySide != 0f && Math.Sign(side) != Math.Sign(entrySide) && Math.Abs(side) >= CrossReach;
            if (!through && !jumped)
                return;
            string key = _crossing;
            _crossing = null;
            Mark(key, jumped ? "moved by the game after leaving it, same area" : $"came out {FieldData.F(Math.Abs(side))} beyond it, same area");
            if (jumped)
            {
                // A warp's arrival point lies beside its way back (area 3: 01d_03 leads to 01pos_02, beside 01d_02, which leads
                // back): that door counts as come in through, as after an area change (0.0.52; Francesco: standard areas mark
                // both sides at once, the Amala Network only the side walked into).
                string near = NearestDoor(gb, out float d);
                if (near != null && near != key)
                    Mark(near, $"arrived beside it from {key} ({FieldData.F(d)} away)");
                return;
            }
            if (!through)
                return;
            // Its other side: a door volume within PairReach, beyond the plane.
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            int n = ranges == null ? 0 : Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            for (int i = 0; i < n; i++)
            {
                string other = DoorKey(gb.fieldID, gb.areaID, i);
                if (other == null || other == key || !DoorCentre(i, out float ox, out float oz, out _, out _))
                    continue;
                float dx = ox - _doorX, dz = oz - _doorZ;
                float otherSide = (dx * ax + dz * az) / alen;
                if (dx * dx + dz * dz <= PairReach * PairReach && Math.Sign(otherSide) == Math.Sign(side) && Math.Abs(otherSide) >= 50f)
                    Mark(other, $"the other side of {key}");
            }
        }

        /// <summary>gEvnRng[index]'s centre in X and Z and its volume's facing (gEvnRngBox.boxnml, 0 0 without one).</summary>
        private static bool DoorCentre(int index, out float x, out float z, out float nx, out float nz)
        {
            x = z = nx = nz = 0f;
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (ranges == null || index < 0 || index >= Math.Min(fldEveHit.gEvnRngNum, ranges.Length) || ranges[index]?.sisei == null)
                return false;
            x = ranges[index].sisei.pos.x;
            z = ranges[index].sisei.pos.z;
            Il2CppReferenceArray<putevebox_s> boxes = fldEveHit.gEvnRngBox;
            putevebox_s box = boxes != null && index < boxes.Length ? boxes[index] : null;
            if (box != null)
            {
                UnityEngine.Vector4 v = box.boxnml;
                float len = (float)Math.Sqrt(v.x * v.x + v.z * v.z);
                if (len > 0.5f && Math.Abs(v.y) < 0.5f)
                {
                    nx = v.x / len;
                    nz = v.z / len;
                }
            }
            return true;
        }

        /// <summary>The key of gEvnRng[index] when it is a door volume ("NNd_MM", type 10), else null.</summary>
        private static string DoorKey(int field, int area, int index)
        {
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (ranges == null || index >= Math.Min(fldEveHit.gEvnRngNum, ranges.Length))
                return null;
            fldResHead_t h = ranges[index];
            string name = h?.resourceName ?? "";
            return h != null && h.resourceType == 10 && DoorName.IsMatch(name) ? Key(field, area, name) : null;
        }

        private static string NearestDoor(fldGlobalWork_t gb, out float best)
        {
            best = ArriveReach;
            string found = null;
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            int n = ranges == null ? 0 : Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            for (int i = 0; i < n; i++)
            {
                fldResHead_t h = ranges[i];
                if (h == null || h.sisei == null || h.resourceType != 10 || !DoorName.IsMatch(h.resourceName ?? ""))
                    continue;
                float dx = h.sisei.pos.x - gb.playerX, dz = h.sisei.pos.z - gb.playerZ;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                if (dist <= best && Math.Abs(h.sisei.pos.y + 100f - gb.playerY) < 200f)
                {
                    best = dist;
                    found = Key(gb.fieldID, gb.areaID, h.resourceName);
                }
            }
            return found;
        }

        private static void Mark(string key, string how)
        {
            if (!Doors.Add(key))
            {
                Log.Info($"{Tag} {key} again ({how}).");
                return;
            }
            Log.Info($"{Tag} {key} marked ({how}).");
            if (_file < 0)
                return;   // a new game: written at the first save
            try
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllLines(PathOf(_file), new[] { "door " + key });
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} {key} could not be written: {ex.Message}");
            }
        }
    }
}
