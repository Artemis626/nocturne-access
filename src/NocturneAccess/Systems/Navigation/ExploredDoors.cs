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
        private const float EventDoorReach = 250f;   // 0.0.191: a door taken through gFldEveRngIdx counts within this of its centre
        private const float CrossReach = 120f;     // beyond a door's plane: gone through it (0.0.51)
        private const float JumpReach = 300f;      // one frame's move beyond this: the game moved the player (walking is 16 a tick)
        private const float PairReach = 500f;      // a door's other volume (Amala 01d_02 and 01d_03: 360 apart)
        private static readonly Regex DoorName = FieldData.DoorVolume;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private static readonly HashSet<string> Doors = new HashSet<string>();

        // Event volumes' prompts as the game showed them (0.0.53): "field/area/01eve_03" -> "Open Door". Kept with the doors.
        private static readonly Dictionary<string, string> Prompts = new Dictionary<string, string>();
        private const long PromptStableMs = 300;
        private static int _promptIdx = -1;
        private static long _promptIdxMs;
        private static readonly Regex EventName = FieldData.EventVolume;

        // Warp arrival points as the player landed on them (0.0.53): "field/area/01pos_02" -> position. WarpMap uses them when the
        // game's own data could not be read; every warp leading to that point then has its destination. Kept with the doors.
        private static readonly Dictionary<string, UnityEngine.Vector4> Arrivals = new Dictionary<string, UnityEngine.Vector4>();

        /// <summary>Where the player landed on the warp arrival point named pointName in (field, area), when known.</summary>
        internal static bool LearnedArrival(int field, int area, string pointName, out UnityEngine.Vector4 pos)
            => Arrivals.TryGetValue(Key(field, area, pointName), out pos);

        // A door with a follow-up warp (0.0.139, the White Temple): its own landing comes first, then the game moves the player
        // on to this point (2 Nov 2026, 15:29:02 at 01pos_23, 15:29:04 at 01pos_51): learned at that second move.
        private const long FollowWindowMs = 4000;
        private static string _followPoint, _followDoor;
        private static long _followMs;

        private static void LearnArrival(fldGlobalWork_t gb, string leftKey)
        {
            string resName = leftKey.Substring(leftKey.LastIndexOf('/') + 1);
            if (!DoorTable.SameAreaWarp(gb.fieldID, gb.areaID, resName, out _, out string point, out string first))
                return;
            if (first.Length > 0)
            {
                _followPoint = point;
                _followDoor = leftKey;
                _followMs = Clock.ElapsedMilliseconds;
                point = first;
            }
            LearnPoint(gb, leftKey, point);
        }

        private static void LearnPoint(fldGlobalWork_t gb, string leftKey, string point)
        {
            string key = Key(gb.fieldID, gb.areaID, point);
            var pos = new UnityEngine.Vector4(gb.playerX, gb.playerY, gb.playerZ, 1f);
            bool known = Arrivals.ContainsKey(key);
            Arrivals[key] = pos;
            if (known)
                return;
            Log.Info($"{Tag} Warp {leftKey} lands on {point} at {FieldData.F(pos.x)} {FieldData.F(pos.y)} {FieldData.F(pos.z)}: learned.");
            WarpMap.Invalidate();
            Append(ArrivalLine(key, pos), $"the arrival {key}");
        }

        /// <summary>
        /// Adds one line to the file in play, at once (a crash or a quit without saving keeps it): for map facts only, never explored
        /// doors (0.0.162: those wait for the game's save). A new game not yet saved has no file: everything is written at its first
        /// save (WriteAll).
        /// </summary>
        internal static void Append(string line, string what)
        {
            if (_file < 0)
                return;
            try
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllLines(PathOf(_file), new[] { line });
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} {what} could not be written: {ex.Message}");
            }
        }

        private static string ArrivalLine(string key, UnityEngine.Vector4 p)
            => string.Format(System.Globalization.CultureInfo.InvariantCulture, "arrival {0} {1} {2} {3}", key, p.x, p.y, p.z);

        private static void ReadArrival(string line)
        {
            string[] parts = line.Split(' ');
            if (parts.Length != 5)
                return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (float.TryParse(parts[2], System.Globalization.NumberStyles.Float, inv, out float x)
                && float.TryParse(parts[3], System.Globalization.NumberStyles.Float, inv, out float y)
                && float.TryParse(parts[4], System.Globalization.NumberStyles.Float, inv, out float z))
                Arrivals[parts[1]] = new UnityEngine.Vector4(x, y, z, 1f);
        }

        // Where each door led (0.0.78; Francesco, 14 Oct 2026: doors should say where they go, as landmarks do): the location
        // banner the game showed first after going through it, "field/area/01d_09" -> "East Hall 1F". Kept with the doors.
        private static readonly Dictionary<string, string> Destinations = new Dictionary<string, string>();
        private static string _destKey;           // the door just gone through, until the banner shows in the new area
        private static int _destShows, _destField, _destArea;
        private static long _destMs;

        /// <summary>The banner shown on arriving through door resName of (field, area), or "" when never gone through.</summary>
        internal static string DestinationOf(int field, int area, string resName)
            => Destinations.TryGetValue(Key(field, area, resName), out string d) ? d : "";

        /// <summary>
        /// The banner learned through any door of this field that the game's door table leads into (field, area), or "" (0.0.149:
        /// the Labyrinth's doors name the area they lead to before going through them, by what another door into it showed).
        /// </summary>
        internal static string BannerOfArea(int field, int area)
        {
            string prefix = field + "/";
            foreach (KeyValuePair<string, string> d in Destinations)
            {
                if (!d.Key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                string[] parts = d.Key.Split('/');
                if (parts.Length == 3 && int.TryParse(parts[1], out int from) && from != area
                    && DoorTable.Leads(field, from, parts[2], out int lf, out int la) && lf == field && la == area)
                    return d.Value;
            }
            return "";
        }

        /// <summary>Every frame after going through a door: the first location banner shown in the new area is where it leads.</summary>
        private static void LearnDestination(long now)
        {
            if (_destKey == null)
                return;
            if (now - _destMs > ArriveWindowMs && _destFirst != null)
            {
                Learn(_destFirst, "the floor's banner, nothing after it");
                return;
            }
            if (now - _destMs > ArriveWindowMs)
            {
                // 0.0.164 (Francesco's A1): in the Labyrinth a door with no banner after it takes the name of the area left, the
                // last banner shown (a fallback: in the 11 Nov 2026 session the game showed its banner on every arrival, the mod only
                // leaves a repeated one unsaid, which is why "Hell's Maze" was heard once; "area 22" came from areas not yet entered).
                string same = PointsOfInterest.Labyrinth(_destField) ? Menus.AnnouncementReader.LocationShownText.Trim() : "";
                if (same.Length > 0 && !(Destinations.TryGetValue(_destKey, out string had) && had == same))
                {
                    Destinations[_destKey] = same;
                    Log.Info($"{Tag} {_destKey} leads to \"{same}\" (no banner on arriving in {_destField}/{_destArea}: the name of the area left).");
                    Append($"dest {_destKey}\t{same}", $"The destination of {_destKey}");
                }
                else if (same.Length == 0)
                    Log.Info($"{Tag} No banner on arriving in {_destField}/{_destArea} through {_destKey}: no destination learned.");
                _destKey = null;
                return;
            }
            if (Menus.AnnouncementReader.LocationShows == _destShows)
            {
                // 0.0.172: the floor's name came first and nothing followed within FollowMs: it stands.
                if (_destFirst != null && now - _destFirstMs > FollowMs)
                    Learn(_destFirst, "the floor's banner, nothing after it");
                return;
            }
            _destShows = Menus.AnnouncementReader.LocationShows;
            string text = Menus.AnnouncementReader.LocationShownText.Trim();
            if (text.Length == 0)
                return;
            // 0.0.172 (Francesco, 17 Nov 2026): the Great Underpass of Ginza shows the floor's banner, then the place's ("B2" at
            // 16:09:57, "Terminal" 1.6 s later, entering the Terminal room 22/9 from 22/8): the door learned "B2", the same as the
            // floor left, which the Pages list leaves unsaid. A first banner equal to the one of the area left waits FollowMs for
            // a place banner, which wins.
            if (_destFirst == null && text.Equals(_destLeft, StringComparison.OrdinalIgnoreCase))
            {
                _destFirst = text;
                _destFirstMs = now;
                return;
            }
            Learn(text, _destFirst != null ? $"after the floor's \"{_destFirst}\"" : "the banner");
        }

        private const long FollowMs = 4000;
        private static string _destLeft, _destFirst;
        private static long _destFirstMs;

        private static void Learn(string text, string why)
        {
            string key = _destKey;
            _destKey = null;
            _destFirst = null;
            if (key == null || text.Length == 0 || (Destinations.TryGetValue(key, out string old) && old == text))
                return;
            Destinations[key] = text;
            Log.Info($"{Tag} {key} leads to \"{text}\" ({why} on arriving in {_destField}/{_destArea}).");
            Append($"dest {key}\t{text}", $"The destination of {key}");
        }

        /// <summary>The prompt the game showed for event volume resName, or "".</summary>
        internal static string PromptOf(int field, int area, string resName)
            => Prompts.TryGetValue(Key(field, area, resName), out string p) ? p : "";

        /// <summary>
        /// Every frame: when the game's hit index (gFldEveRngIdx) has been an event volume ("NNeve_MM") for PromptStableMs and
        /// its interaction prompt is on screen, that prompt is the volume's (Nyx's Lounge, 21:57: 01eve_01 "Talk" is Loki,
        /// 01eve_03 "Open Door" his room). The wait keeps the previous volume's prompt (a Soul's "Talk") from being taken.
        /// </summary>
        private static void LearnPrompt(fldGlobalWork_t gb, long now)
        {
            int i = fldEveHit.gFldEveRngIdx;
            if (i != _promptIdx)
            {
                _promptIdx = i;
                _promptIdxMs = now;
                return;
            }
            if (i < 0 || now - _promptIdxMs < PromptStableMs)
                return;
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (ranges == null || i >= Math.Min(fldEveHit.gEvnRngNum, ranges.Length))
                return;
            fldResHead_t h = ranges[i];
            if (h?.sisei == null || h.resourceType != 10 || !EventName.IsMatch(h.resourceName ?? ""))
                return;
            float dx = h.sisei.pos.x - gb.playerX, dz = h.sisei.pos.z - gb.playerZ;
            if (dx * dx + dz * dz > 400f * 400f)
                return;
            string text = Menus.AnnouncementReader.InteractionPromptText;
            if (text.Length == 0)
                return;
            string key = Key(gb.fieldID, gb.areaID, h.resourceName);
            if (Prompts.TryGetValue(key, out string old) && old == text)
                return;
            Prompts[key] = text;
            Log.Info($"{Tag} Event {key}: prompt \"{text}\".");
            Append($"prompt {key}\t{text}", $"The prompt of {key}");
        }

        // ---- What waits behind a door (0.0.84) ----
        // Francesco, 15 Oct 2026, Kabukicho Prison: once the Digging Manikin has the spoon, a door on the real plane shows a hole;
        // a sighted player sees it, a blind one hears the same door as before. The door is area 3's 01d_08 into the cell area 12,
        // whose arrival event 01eve_01 fires as the player lands (21:39:28: 238 ms after the change, before any key) and says
        // "No one is here." (sending the player back) until flag 1430 is set, then "This is the hole that the Manikin jumped
        // into." and "Jump in?". So: the event volume the game puts the player in on arriving through a door is kept with the
        // door ("behind 25/3/01d_08<TAB>12/01eve_01"), and the door's point says that event's first narration as the field
        // script gives it with the flags of now (FieldScript.FirstNarration): the room's change is heard before going in.
        private const long BehindAfterControlMs = 500;   // an arrival event is hit at once; later the player may have walked on
        private static readonly Dictionary<string, (int Area, string Volume)> Behind = new Dictionary<string, (int, string)>();
        private static string _behindKey;            // the door just gone through, until an event volume is hit in the new area
        private static long _behindMs, _behindControlMs;
        private static int _behindIdx0;
        private static bool _behindSawNone;

        /// <summary>The game's narration of the event that greets the player behind door resName of (field, area), with the flags
        /// as they are now ("No one is here", "This is the hole that the Manikin jumped into"), or "".</summary>
        internal static string BehindLine(int field, int area, string resName)
        {
            if (!Behind.TryGetValue(Key(field, area, resName), out var b) || !FieldScript.VolumeOn(field, b.Area, b.Volume))
                return "";
            return FieldScript.FirstNarration(b.Area, b.Volume);
        }

        private static void StartBehind(string doorKey, long now)
        {
            _behindKey = doorKey;
            _behindMs = now;
            _behindControlMs = -1;
            _behindIdx0 = fldEveHit.gFldEveRngIdx;
            _behindSawNone = false;
        }

        /// <summary>Every frame after going through a door within the field: the first event volume the game's hit index takes
        /// in the new area, before the player can have walked on, is what waits behind the door.</summary>
        private static void LearnBehind(fldGlobalWork_t gb, long now)
        {
            if (_behindKey == null)
                return;
            if (FieldState.IsExploring && _behindControlMs < 0)
                _behindControlMs = now;
            if (now - _behindMs > ArriveWindowMs || (_behindControlMs >= 0 && now - _behindControlMs > BehindAfterControlMs))
            {
                _behindKey = null;
                return;
            }
            int i = fldEveHit.gFldEveRngIdx;
            if (i < 0)
            {
                _behindSawNone = true;
                return;
            }
            if (i == _behindIdx0 && !_behindSawNone)
                return;   // the hit of the area left, not yet reset
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (ranges == null || i >= Math.Min(fldEveHit.gEvnRngNum, ranges.Length))
                return;
            fldResHead_t h = ranges[i];
            string volume = h?.resourceName ?? "";
            if (h == null || h.resourceType != 10 || !EventName.IsMatch(volume))
                return;   // the arrival door's own volume: wait for an event
            string key = _behindKey;
            _behindKey = null;
            if (!key.StartsWith(gb.fieldID + "/", StringComparison.Ordinal))
                return;   // another field: another script
            if (Behind.TryGetValue(key, out var old) && old.Area == gb.areaID && old.Volume == volume)
                return;
            Behind[key] = (gb.areaID, volume);
            Log.Info($"{Tag} Behind {key}: event {volume} of area {gb.areaID} greets the player (now \"{FieldScript.FirstNarration(gb.areaID, volume)}\").");
            Append($"behind {key}\t{gb.areaID}/{volume}", $"What waits behind {key}");
        }

        private static void ReadBehind(string line)
        {
            int tab = line.IndexOf('\t'), slash = line.LastIndexOf('/');
            if (tab <= 7 || slash <= tab || !int.TryParse(line.Substring(tab + 1, slash - tab - 1), out int area))
                return;
            Behind[line.Substring(7, tab - 7)] = (area, line.Substring(slash + 1).Trim());
        }

        // ---- Jump Off ----
        // Kabukicho Prison's "Jump Off" volumes drop the player to the floor below, another area. 0.0.82 learned where each led;
        // removed in 0.0.83 (Francesco, 15 Oct 2026: the Umugi Stone's mirage changes which floor lies below, so a learned
        // destination can be wrong). Only the test stays, for PlaneShift.
        private const int JumpOffPrompt = 3;      // fldPanel.inputpanel_mes0[3], "Jump Off" (PANELMES_L0003)

        /// <summary>Event volume resName of the current area waits on the game's "Jump Off" prompt.</summary>
        internal static bool IsJumpOff(fldGlobalWork_t gb, string resName)
        {
            string jumpText = FieldScript.PanelText(JumpOffPrompt);
            if (jumpText.Length > 0 && PromptOf(gb.fieldID, gb.areaID, resName) == jumpText)
                return true;
            ScriptVerdict v = FieldScript.Classify(gb.areaID, resName);
            return v != null && v.Outcome == ScriptOutcome.Action && v.Prompt == JumpOffPrompt;
        }

        private static int _file = -1;           // slMain.gslSelect of the file in play, -1 for a new game not saved yet

        private static int _field = int.MinValue, _area = int.MinValue;
        private static int _hit = -1;
        private static string _leaving;           // the last door volume stood in, as its key
        private static long _leftMs = long.MinValue / 2;   // when the player was last inside it
        private static string _cameFrom;          // set at an area change through a door, until the arrival door is marked
        private static long _changeMs, _exploringSinceMs = -1;
        private static string _crossing;          // the door volume just left in this area, until it is known whether the player went through
        private static float _doorX, _doorZ, _entryX, _entryZ, _normalX, _normalZ;
        private static float _lastX, _lastY, _lastZ;
        private static bool _hasLast, _settled;
        private static long _areaMs = long.MinValue / 2;
        private const long SettleMs = 1000;   // jumps count from this long after an area change, once the player has control
        private static long _jumpMs = long.MinValue / 2;

        private static string Folder => Path.Combine(MelonEnvironment.UserDataDirectory, "NocturneAccess", "Explored");

        internal static string Key(int field, int area, string resName) => $"{field}/{area}/{resName}";

        /// <summary>The game runs every battle in a field 200 to 299 (logs 10 to 14 Oct 2026: 85 changes, 202 on the world map, 217 to 223 in
        /// the dungeons, each followed by the battle UI).</summary>
        internal static bool BattleField(int field) => field >= 200 && field < 300;

        internal static bool IsExplored(int field, int area, string resName) => Doors.Contains(Key(field, area, resName));

        /// <summary>The same by its key "field/area/name" (AreaRoutes.LeadsOf, 0.0.164).</summary>
        internal static bool IsExploredKey(string key) => Doors.Contains(key);

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
                ClearAll();
                _file = file;
                string path = PathOf(file);
                if (File.Exists(path))
                    foreach (string line in File.ReadAllLines(path))
                        if (line.StartsWith("door ", StringComparison.Ordinal))
                            Doors.Add(line.Substring(5).Trim());
                        else if (line.StartsWith("prompt ", StringComparison.Ordinal) && line.IndexOf('\t') > 7)
                            Prompts[line.Substring(7, line.IndexOf('\t') - 7)] = line.Substring(line.IndexOf('\t') + 1).Trim();
                        else if (line.StartsWith("arrival ", StringComparison.Ordinal))
                            ReadArrival(line);
                        else if (line.StartsWith("dest ", StringComparison.Ordinal) && line.IndexOf('\t') > 5)
                            Destinations[line.Substring(5, line.IndexOf('\t') - 5)] = line.Substring(line.IndexOf('\t') + 1).Trim();
                        else if (line.StartsWith("behind ", StringComparison.Ordinal))
                            ReadBehind(line);
                        else
                            AreaRoutes.ReadLine(line);
                WarpMap.Invalidate();
                Log.Info($"{Tag} File {file + 1} loaded (gslSelect {slMain.gslSelect}, gslSelTop {slMain.gslSelTop}, slChooseSaveIndex {slMain.slChooseSaveIndex}): {Doors.Count} explored doors, {Prompts.Count} event prompts, {Arrivals.Count} warp arrivals, {Destinations.Count} door destinations, {Behind.Count} rooms behind doors, {AreaRoutes.Summary}.");
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
            ClearAll();
            _file = -1;
            WarpMap.Invalidate();
        }

        /// <summary>Everything kept per save file, emptied before a file is read or a new game starts.</summary>
        private static void ClearAll()
        {
            WayOn.ForgetHeard();   // the way-on tone plays again on reaching each area (0.0.122)
            ChaseCues.ForgetHeard();   // the "chase disabled" line once per load (0.0.146)
            Doors.Clear();
            Prompts.Clear();
            Arrivals.Clear();
            Destinations.Clear();
            Behind.Clear();
            AreaRoutes.Clear();
            ResetTracking();
        }

        private static string PathOf(int file) => Path.Combine(Folder, $"file{file + 1:00}.txt");

        private static void WriteAll(int file)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(PathOf(file), Doors.OrderBy(d => d, StringComparer.Ordinal).Select(d => "door " + d)
                .Concat(Prompts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"prompt {p.Key}\t{p.Value}"))
                .Concat(Arrivals.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => ArrivalLine(p.Key, p.Value)))
                .Concat(Destinations.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"dest {p.Key}\t{p.Value}"))
                .Concat(Behind.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"behind {p.Key}\t{p.Value.Area}/{p.Value.Volume}"))
                .Concat(AreaRoutes.Lines()));
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
            _settled = false;
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
                _destKey = null;   // another change before the banner (a battle): nothing learned
                _behindKey = null;
                if (_leaving != null && now - _leftMs <= LeaveWindowMs && BattleField(field))
                {
                    // An encounter is not a door (0.0.77): 14 Oct 2026, 13:32:00, walking away from 01d_08 in Ginza 1F, the battle
                    // (field 219) 1941 ms after its hit was logged as "left through it" (already explored, so no harm that time).
                    Log.Info($"{Tag} {_leaving} not marked: the change to {field}/{area} is a battle.");
                    _cameFrom = null;
                }
                else if (_leaving != null && now - _leftMs <= LeaveWindowMs)
                {
                    Mark(_leaving, $"left through it, {now - _leftMs} ms before the change to {field}/{area}");
                    _cameFrom = _leaving;
                    _changeMs = now;
                    _destKey = _leaving;
                    _destField = field;
                    _destArea = area;
                    _destMs = now;
                    _destShows = Menus.AnnouncementReader.LocationShows;
                    _destLeft = Menus.AnnouncementReader.LocationShownText.Trim();
                    _destFirst = null;
                    StartBehind(_leaving, now);
                }
                else
                {
                    _cameFrom = null;
                }
                _leaving = null;
                _crossing = null;
                _hasLast = false;
                _settled = false;
                _areaMs = now;
                return;
            }

            LearnDestination(now);
            LearnBehind(gb, now);
            CheckCrossing(gb, now);
            LearnPrompt(gb, now);

            // The door volume the player stands in: its key is taken while exploring, when the area's own volumes are surely
            // loaded; the time keeps running while the door's transition (no longer exploring) starts.
            int hit = fldEveHit.OldHitDoor;
            // 0.0.191: a door the game runs through the event volume index instead (gFldEveRngIdx): the Diet Building's wing doors
            // unlocked from the far side (33/10/01d_04 to Central Tower 2F, 33/15/01d_06 to 1F) were never marked, either way.
            // Only within EventDoorReach of the door's centre: the event index can keep its last value after the player leaves.
            if (hit < 0)
            {
                int ev = fldEveHit.gFldEveRngIdx;
                if (ev >= 0 && DoorKey(field, area, ev) != null && DoorCentre(ev, out float ex, out float ez, out _, out _)
                    && (gb.playerX - ex) * (gb.playerX - ex) + (gb.playerZ - ez) * (gb.playerZ - ez) <= EventDoorReach * EventDoorReach)
                    hit = ev;
            }
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
                    // Only a door at the arrival point: within ArriveHitMs the player may already have walked into another one,
                    // which would teach AreaRoutes a wrong way through (0.0.56).
                    bool atArrival = _exploringSinceMs < 0
                                     || (_doorX - _arriveX) * (_doorX - _arriveX) + (_doorZ - _arriveZ) * (_doorZ - _arriveZ) <= ArriveReach * ArriveReach;
                    if (key != null && _cameFrom != null && atArrival)
                    {
                        Arrived(key, $"came in through it from {_cameFrom}");
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
            {
                // Where the game put the player: by ArriveHitMs they may have walked on (underpass area 4, 20:56:11: 900 north
                // of the door within 1.5 s, "No door within 300 of the arrival point").
                _exploringSinceMs = now;
                _arriveX = gb.playerX;
                _arriveY = gb.playerY;
                _arriveZ = gb.playerZ;
                AreaRoutes.StartLanding(_cameFrom, gb.fieldID, gb.areaID, _arriveX, _arriveZ);   // 0.0.164: the doors walkable from here
            }
            if (now - _exploringSinceMs < ArriveHitMs)
                return;
            string near = NearestDoor(gb, _arriveX, _arriveY, _arriveZ, out float d);
            if (near != null)
                Arrived(near, $"came in through it from {_cameFrom} (nearest door, {FieldData.F(d)} away)");
            else
                Log.Info($"{Tag} No door within {ArriveReach} of the arrival point from {_cameFrom}.");
            _cameFrom = null;
        }

        private static float _arriveX, _arriveY, _arriveZ;

        /// <summary>The door the player came in through after an area change: marked, and the way through learned (AreaRoutes).</summary>
        private static void Arrived(string key, string how)
        {
            Mark(key, how);
            AreaRoutes.LearnPass(_cameFrom, key);
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
            float px = gb.playerX, py = gb.playerY, pz = gb.playerZ;
            // Up or down counts too (0.0.55): a ladder moves the player 220 across and 800 up (Ginza underpass, 21:36 session).
            bool jumpedNow = _hasLast && _settled
                             && ((px - _lastX) * (px - _lastX) + (pz - _lastZ) * (pz - _lastZ) > JumpReach * JumpReach || Math.Abs(py - _lastY) > JumpReach);
            _lastX = px;
            _lastY = py;
            _lastZ = pz;
            _hasLast = true;
            // After an area change the player is put at the new area's entrance a few frames later: not a move inside the area
            // ("Warped." on entering the Collector Manikin's room, 21:37:09). Jumps count again once the player has control.
            if (FieldState.IsExploring && now - _areaMs >= SettleMs)
                _settled = true;
            if (jumpedNow && _followPoint != null && now - _followMs <= FollowWindowMs)
            {
                // The follow-up warp after a door's own landing (0.0.139): the door's prompt said nothing of it.
                _jumpMs = now;
                SpeechManager.Say("Warped.", interrupt: false);
                Log.Info($"{Tag} Moved by the game to {FieldData.F(px)} {FieldData.F(py)} {FieldData.F(pz)} in the same area (the follow-up warp of {_followDoor}).");
                LearnPoint(gb, _followDoor, _followPoint);
                _followPoint = null;
                return;
            }
            if (jumpedNow)
            {
                _jumpMs = now;
                // Said for every move by the game inside the area (Francesco, 4 Oct 2026: the Amala Network's warps are not
                // shown, and the player must know the place changed). The floor banner the game shows again follows it.
                // 0.0.55: only after leaving a door volume (a story event moved the player beside Loki for its scene, 20:42:45:
                // "Warped."), and not after a ladder or door the player took with confirm, whose prompt already said it.
                string took = _crossing ?? (_leaving != null && now - _leftMs <= LeaveWindowMs ? _leaving : null);
                string res = took?.Substring(took.LastIndexOf('/') + 1);
                // 0.0.60: any door the game does not fire by walking in, also one to another area: the underpass's ladder doors
                // (field 22 area 10, 01d_01 to area 13) lift the player 270 up the ladder before the area changes ("Descend
                // Ladder", then "Warped.", 22:02:58).
                int moveType = res == null ? -1 : DoorTable.MoveTypeOf(gb.fieldID, gb.areaID, res);
                bool chosen = moveType >= 0 && !WarpMap.IsTouchMoveType(moveType);
                if (took != null && !chosen)
                    SpeechManager.Say("Warped.", interrupt: false);
                if (res != null)
                    WayOn.TakenInArea(gb.fieldID, gb.areaID, res);
                Log.Info($"{Tag} Moved by the game to {FieldData.F(px)} {FieldData.F(py)} {FieldData.F(pz)} in the same area" +
                         (took == null ? " (no door left just before: an event, not said)" : chosen ? $" ({res}, taken with confirm)" : $" (after {res})") + ".");
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
            AreaRoutes.StartLanding(key, gb.fieldID, gb.areaID, gb.playerX, gb.playerZ);   // 0.0.164: the doors walkable from its landing
            if (jumped)
            {
                // A warp's arrival point lies beside its way back (area 3: 01d_03 leads to 01pos_02, beside 01d_02, which leads
                // back): that door counts as come in through, as after an area change (0.0.52; Francesco: standard areas mark
                // both sides at once, the Amala Network only the side walked into).
                LearnArrival(gb, key);
                string near = NearestDoor(gb, gb.playerX, gb.playerY, gb.playerZ, out float d);
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
        internal static bool DoorCentre(int index, out float x, out float z, out float nx, out float nz)
        {
            x = z = nx = nz = 0f;
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (ranges == null || index < 0 || index >= Math.Min(fldEveHit.gEvnRngNum, ranges.Length) || ranges[index]?.sisei == null)
                return false;
            x = ranges[index].sisei.pos.x;
            z = ranges[index].sisei.pos.z;
            Il2CppReferenceArray<putevebox_s> boxes = fldEveHit.gEvnRngBox;
            FieldData.BoxFacing(boxes != null && index < boxes.Length ? boxes[index] : null, out nx, out nz);
            return true;
        }

        /// <summary>
        /// A door volume's name: "NNd_MM", or an event-named volume the door table makes a door now (0.0.188: the Diet Building's
        /// Central Tower 1F 01eve_02 to West Wing 1F, flag 1672, was never marked explored; the Ikebukuro Tunnel's 01eve stairs).
        /// </summary>
        private static bool IsDoorName(int field, int area, string name) =>
            DoorName.IsMatch(name) || (name.Length > 0 && !FieldState.WorldMapField(field) && DoorTable.MoveTypeOf(field, area, name) >= 0);


        /// <summary>The key of gEvnRng[index] when it is a door volume ("NNd_MM", type 10), else null.</summary>
        private static string DoorKey(int field, int area, int index)
        {
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            if (ranges == null || index >= Math.Min(fldEveHit.gEvnRngNum, ranges.Length))
                return null;
            fldResHead_t h = ranges[index];
            string name = h?.resourceName ?? "";
            return h != null && h.resourceType == 10 && IsDoorName(field, area, name) ? Key(field, area, name) : null;
        }

        /// <summary>The door volume nearest (x, y, z) within ArriveReach on that floor, as its key, or null.</summary>
        private static string NearestDoor(fldGlobalWork_t gb, float x, float y, float z, out float best)
        {
            best = ArriveReach;
            string found = null;
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            int n = ranges == null ? 0 : Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            for (int i = 0; i < n; i++)
            {
                fldResHead_t h = ranges[i];
                if (h == null || h.sisei == null || h.resourceType != 10 || !IsDoorName(gb.fieldID, gb.areaID, h.resourceName ?? ""))
                    continue;
                float dx = h.sisei.pos.x - x, dz = h.sisei.pos.z - z;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                if (dist <= best && Math.Abs(h.sisei.pos.y + 100f - y) < 200f)
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
            // 0.0.162: written only when the game saves (WriteAll), never at once: an explored door is progress of this save, and a
            // run lost to a game over or a quit without saving must not keep it. Francesco, 8 Nov 2026: after the Archangels' game
            // over (7 Nov 22:29) he reloaded File 2, saved at 22:12 with 1248 doors, and it loaded 1256: the eight doors of
            // Mifunashiro's lower strata walked after that save said "Explored". Map facts (prompts, arrivals, destinations,
            // rooms behind doors, routes) are still appended at once: they hold for every save.
        }
    }
}
