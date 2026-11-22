using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// What a plain door leads to, from the field's door warp table (fldWap.gFldWarpBuff.door_buff, doormovetbl_s; decoded
    /// offline and verified against every door gone through, 17:11 session, 2 Oct 2026; docs/EXPLORED-DOORS-PLAN.md, "Door warp
    /// table: verified and analysed"). Read as data only: the table's loader runs in protected code and is never called.
    /// - "exit": the door leads to another field (union08 not 0 and not this field);
    /// - "room": every door of the area it leads to leads back to this area (a side room: Room B202, the Healing Room);
    /// - "passage": that area leads on to another area or field.
    /// Nothing for the elevator, Terminal and event warps (warptype 1, 2, 3), a door into its own area, or an area with no
    /// doors in the table. The entry is chosen as fldWap.fldDoor_Find does: in table order, union01 = area + 1 and union02 =
    /// the door's name, skipping an entry whose story flag is not set (EventBit.evtBitCheck, plain code).
    /// Config "DoorKinds" (default on) turns it off (Francesco: option 1, revertible).
    /// </summary>
    internal static class DoorTable
    {
        private sealed class Entry
        {
            public int AreaPlus1, Flag, WarpType, ToField, ToAreaPlus1, MoveType, Union03;
            public string Name = "", WarpPos = "";
            public string FirstPos = "";   // a door with a follow-up warp (0.0.139): where its own row puts the player first

            /// <summary>Where it leads from (field, area): union08 0 means this field, union09 0 this area (else area + 1).</summary>
            public void Dest(int field, int area, out int toField, out int toArea)
            {
                toField = ToField == 0 ? field : ToField;
                toArea = ToAreaPlus1 == 0 ? area : ToAreaPlus1 - 1;
            }
        }

        /// <summary>
        /// A door that moves the player to a point of its own area (0.0.52): warptype 0, union08 0 or this field, union09 0 or
        /// this area + 1, arriving at the resource named warp_pos ("01pos_02"). The Amala Network's hidden warps are movetype 2
        /// (no door model, fired by walking in: 4 Oct 2026, 15:56 to 15:58, area 3's 01d_03 sent the player to 01pos_02 four
        /// times); movetype 1 are doors with a model ("md_01d_02") and the "Open Door" prompt.
        /// </summary>
        internal static bool SameAreaWarp(int field, int area, string resName, out int moveType, out string warpPos)
            => SameAreaWarp(field, area, resName, out moveType, out warpPos, out _);

        /// <summary>As above; firstPos is the door's own landing when a follow-up warp then moves the player on (else "").</summary>
        internal static bool SameAreaWarp(int field, int area, string resName, out int moveType, out string warpPos, out string firstPos)
        {
            moveType = 0;
            warpPos = firstPos = "";
            try
            {
                Refresh(field);
                Entry e = Find(area, resName);
                if (e == null || e.WarpType != 0 || e.WarpPos.Length == 0)
                    return false;
                e.Dest(field, area, out int toField, out int toArea);
                if (toField != field || toArea != area)
                    return false;
                moveType = e.MoveType;
                warpPos = e.WarpPos;
                firstPos = e.FirstPos;
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav:Doors] Warp of {field}/{area}/{resName} could not be read: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// True when the entry the game would take now is gated by a story flag that is on (0.0.185): the Diet Building's
        /// 01eve_02 (field 33 area 0, row flag 1672, to area 1 at 01pos_02) plays the statue scene once and sets 1672; from then
        /// on its procedure only idles and the row makes it a plain door.
        /// </summary>
        internal static bool GatedRowOn(int field, int area, string resName)
        {
            try
            {
                Refresh(field);
                Entry e = Find(area, resName);
                return e != null && e.Flag != 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The movetype of the entry the game would take for this door now, or -1 (2: fired by walking in).</summary>
        internal static int MoveTypeOf(int field, int area, string resName)
        {
            try
            {
                Refresh(field);
                return Find(area, resName)?.MoveType ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// A ladder's way (movetype 5): +1 climb, -1 descend, 0 not known (0.0.75). The field script's shared ladder handler
        /// (hasigo_label, f020.bf) shows prompt 6 "Climb Ladder" when fldCommand_FLD_WARP_HASIGO_GETTBL(0) is 0, else prompt 7
        /// "Descend Ladder"; that value is the entry's union03: the Ginza underpass area 8 ladders with union03 0 land 800 above
        /// (01d_09, 01d_11, 01d_12), those with 1 back on the floor (01d_13, 01d_15, 01d_16); the Nihilo cube hall's 01d_04
        /// (union03 1) showed "Descend Ladder" (12 Oct 2026, 21:28:56), its far end in area 18 has 0.
        /// </summary>
        internal static int LadderWay(int field, int area, string resName)
        {
            try
            {
                Refresh(field);
                Entry e = Find(area, resName);
                if (e == null || e.MoveType != 5)
                    return 0;
                return e.Union03 == 0 ? 1 : e.Union03 == 1 ? -1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static IntPtr _tablePtr;
        private static int _field = int.MinValue;
        private static bool _loggedEmpty;

        /// <summary>"room", "passage", "exit", "to exit" (a room whose only other way out is one door off the field, 0.0.59, 0.0.126) or "" for door volume resName in (field, area).</summary>
        internal static string KindOf(int field, int area, string resName)
        {
            if (!(Config.DoorKinds?.Value ?? true))
                return "";
            try
            {
                Refresh(field);
                Entry e = Find(area, resName);
                if (e == null || e.WarpType != 0)
                    return "";
                e.Dest(field, area, out int toField, out int toArea);
                if (toField != field)
                    return "exit";
                if (toArea == area)
                    return "";
                bool any = false;
                int exits = 0;
                foreach (string name in Entries.Where(x => x.AreaPlus1 == toArea + 1 && x.Name.Length > 0).Select(x => x.Name).Distinct())
                {
                    Entry back = Find(toArea, name);
                    if (back == null)
                        continue;
                    any = true;
                    if (back.WarpType != 0 && back.WarpType != 1)
                        return "passage";   // a Terminal or event door leads on
                    back.Dest(field, toArea, out int f, out int a);
                    // A room whose only other way out leaves the field (stairs to the overworld): "to exit" (Francesco, 6 Oct 2026).
                    if (back.WarpType == 0 && f != field)
                    {
                        exits++;
                        continue;
                    }
                    if (back.WarpType == 1 || (a != area && a != toArea))
                        return "passage";
                }
                // Only a stairwell with one way out: Asakusa's streets (field 27 areas 7 and 18) open onto the overworld at two
                // ends and are rooms (Francesco, 28 Oct 2026: "rooms marked as doors to exit"; 0.0.126).
                return exits == 1 ? "to exit" : any || exits > 1 ? "room" : "";
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav:Doors] Door kind of {field}/{area}/{resName} could not be read: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// The field a plain door (warptype 0) leads to, or -1 when the table has no entry the game would take now. Any door,
        /// named or not: "Exit to overworld" (0.0.44) for a door into a field below 10 (FieldState.WorldMapField), as Shibuya's
        /// 01d_05 and the Medical Center's Annex Lobby exit 01d_04 lead to field 2.
        /// </summary>
        internal static int ToField(int field, int area, string resName) => Leads(field, area, resName, out int toField, out _) ? toField : -1;

        /// <summary>
        /// Where a plain door (warptype 0) leads now: its field and area (0.0.56, AreaRoutes: the door the player came in through
        /// counts as the way back only when it leads back to the area left). False without an entry the game would take.
        /// </summary>
        internal static bool Leads(int field, int area, string resName, out int toField, out int toArea)
        {
            toField = toArea = -1;
            try
            {
                Refresh(field);
                Entry e = Find(area, resName);
                if (e == null || e.WarpType != 0)
                    return false;
                e.Dest(field, area, out toField, out toArea);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav:Doors] Destination of {field}/{area}/{resName} could not be read: {ex.Message}");
                return false;
            }
        }

        /// <summary>The entry the game would take for this door now (fldWap.fldDoor_Find).</summary>
        /// <summary>
        /// True when the field's door table is loaded and holds no row at all (in any flag state) for this door volume (0.0.174):
        /// the game has nothing to do there. Fourth Kalpa B2's 01d_06 (field 44 area 27, mid-corridor, walked through with no
        /// prompt, 18 Nov 2026); the same in 40/0 01d_06, 42/1 01d_03, 43/0 01d_02, 43/4 01d_07, 43/7 01d_08, 44/0 01d_02 and
        /// Yoyogi Park 16/0 (F0NN.WAP read offline: no rows; none ever gone through in the explored files).
        /// </summary>
        internal static bool HasNoRows(int field, int area, string resName)
        {
            try
            {
                Refresh(field);
                if (Entries.Count == 0)
                    return false;   // no table: unknown, keep the door
                foreach (Entry e in Entries)
                {
                    if (e.AreaPlus1 == area + 1 && e.Name == resName)
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Entry Find(int area, string name)
        {
            foreach (Entry e in Entries)
            {
                if (e.AreaPlus1 != area + 1 || e.Name != name)
                    continue;
                if (e.Flag != 0 && !EventBit.evtBitCheck(e.Flag))
                    continue;
                return e;
            }
            return null;
        }

        /// <summary>Copies the table when the field or the table object changes (the game loads it per field).</summary>
        private static void Refresh(int field)
        {
            Il2CppReferenceArray<doormovetbl_s> table = fldWap.gFldWarpBuff?.door_buff;
            IntPtr ptr = table == null ? IntPtr.Zero : table.Pointer;
            if (ptr == _tablePtr && field == _field)
                return;
            _tablePtr = ptr;
            _field = field;
            Entries.Clear();
            if (table == null)
            {
                if (!_loggedEmpty)
                    Log.Info($"[Nav:Doors] No door table in field {field}.");
                _loggedEmpty = true;
                return;
            }
            int followUps = 0;
            for (int i = 0; i < table.Length; i++)
            {
                doormovetbl_s d = table[i];
                if (d == null)
                    continue;
                string name = Chars(d.union02);
                if (d.movetype == 0 && name.Length == 0)
                    continue;
                var e = new Entry
                {
                    AreaPlus1 = d.union01,
                    Name = name,
                    Flag = d.flag,
                    WarpType = d.warptype,
                    ToField = d.union08,
                    ToAreaPlus1 = d.union09,
                    MoveType = d.movetype,
                    Union03 = d.union03,
                    WarpPos = Chars(d.warp_pos),
                };
                // 0.0.139: a door whose after_flag is 1 and whose next row is a nameless movetype 10 entry puts the player on its
                // own landing, then that row warps them on: the Amala Temple's White Temple (field 34 areas 20 to 23; 23 doors, 8
                // more in field 37). Seen 2 Nov 2026, 15:29 to 15:38: area 20's 01d_06 landed on 01pos_05, then the player was in
                // area 21 (the next row: area+1 22, 01pos_02); 01d_24 landed on 01pos_23 (600 5100), then at 01pos_51 (1800 300).
                doormovetbl_s next = i + 1 < table.Length ? table[i + 1] : null;
                if (d.after_flag == 1 && next != null && next.movetype == 10 && Chars(next.union02).Length == 0 && Chars(next.warp_pos).Length > 0)
                {
                    e.FirstPos = e.WarpPos;
                    e.WarpType = next.warptype;
                    e.ToField = next.union08;
                    e.ToAreaPlus1 = next.union09;
                    e.WarpPos = Chars(next.warp_pos);
                    followUps++;
                }
                Entries.Add(e);
            }
            _loggedEmpty = false;
            Log.Info($"[Nav:Doors] Door table of field {field}: {Entries.Count} entries" + (followUps > 0 ? $", {followUps} doors with a follow-up warp." : "."));
        }

        /// <summary>A game char array up to its first NUL (also the navigation dump).</summary>
        internal static string Chars(Il2CppStructArray<char> a)
        {
            if (a == null)
                return "";
            var sb = new StringBuilder();
            for (int i = 0; i < a.Length && a[i] != '\0'; i++)
                sb.Append(a[i]);
            return sb.ToString();
        }
    }
}
