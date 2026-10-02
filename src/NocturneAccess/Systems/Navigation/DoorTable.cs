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
            public int AreaPlus1, Flag, WarpType, ToField, ToAreaPlus1;
            public string Name = "";
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static IntPtr _tablePtr;
        private static int _field = int.MinValue;
        private static bool _loggedEmpty;

        /// <summary>"room", "passage", "exit" or "" for door volume resName in (field, area).</summary>
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
                int toField = e.ToField == 0 ? field : e.ToField;
                if (toField != field)
                    return "exit";
                int toArea = e.ToAreaPlus1 == 0 ? area : e.ToAreaPlus1 - 1;
                if (toArea == area)
                    return "";
                bool any = false;
                foreach (string name in Entries.Where(x => x.AreaPlus1 == toArea + 1 && x.Name.Length > 0).Select(x => x.Name).Distinct())
                {
                    Entry back = Find(toArea, name);
                    if (back == null)
                        continue;
                    any = true;
                    if (back.WarpType != 0 && back.WarpType != 1)
                        return "passage";   // a Terminal or event door leads on
                    int f = back.ToField == 0 ? field : back.ToField;
                    int a = back.ToAreaPlus1 == 0 ? toArea : back.ToAreaPlus1 - 1;
                    if (back.WarpType == 1 || f != field || (a != area && a != toArea))
                        return "passage";
                }
                return any ? "room" : "";
            }
            catch (Exception ex)
            {
                Log.Warn($"[Nav:Doors] Door kind of {field}/{area}/{resName} could not be read: {ex.Message}");
                return "";
            }
        }

        /// <summary>The entry the game would take for this door now (fldWap.fldDoor_Find).</summary>
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
            for (int i = 0; i < table.Length; i++)
            {
                doormovetbl_s d = table[i];
                if (d == null)
                    continue;
                string name = Chars(d.union02);
                if (d.movetype == 0 && name.Length == 0)
                    continue;
                Entries.Add(new Entry
                {
                    AreaPlus1 = d.union01,
                    Name = name,
                    Flag = d.flag,
                    WarpType = d.warptype,
                    ToField = d.union08,
                    ToAreaPlus1 = d.union09,
                });
            }
            _loggedEmpty = false;
            Log.Info($"[Nav:Doors] Door table of field {field}: {Entries.Count} entries.");
        }

        private static string Chars(Il2CppStructArray<char> a)
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
