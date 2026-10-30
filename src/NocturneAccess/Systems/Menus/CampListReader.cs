using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Il2Cpp;
using Il2Cppcamp_H;
using Il2Cppfacility_H;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppnewdata_H;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Battle;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// The pause menu's item and skill lists: Item (Use, Discard, Gems, Key Items) and Skill (after the party pick).
    /// "What will you use? Medicine, have 3, 1 of 3. Fixed HP recovery for one ally."
    ///
    /// The highlight material cannot be used here: an entry that cannot be used now is drawn TMC02 and keeps that
    /// material under the cursor, so with the cursor on it no row is highlighted (12:03 session, 2 Oct: Art's Lunge and
    /// Analyze, Pixie's Zio). The cursor comes from the game instead (docs/class-index.md "Camp menu"):
    /// - cmpDrawItem.cmpDrawItemList and cmpDrawSkill.cmpDrawSkillList are called every frame while their list is shown,
    ///   with the list's cursor (cmpCursorInfo_t) and, for skills, the unit (pData, a datUnitWork_t);
    /// - the entry under the cursor is CursorPos.Index + Shift (cmpMisc.cmpGetCursorIndex, read offline): Shift is the
    ///   visible row (menuitem_top, menuitem_base02..08), Index the first entry shown (13:21 session: Index stayed 0
    ///   while Shift followed the cursor through 3 items);
    /// - items: cmpInit.CMP_GBWK.ItemIdx[entry] is the item id (ItemCnt entries), its count
    ///   dds3GlobalWork.DDS3_GBWK.item[id]; skills: unit.skill[entry], as cmpCalc.cmpGetSelectSkillID reads it.
    /// The name is the row's own text; the id's name from the game's tables must match it, or the count, cost and
    /// description are left out (logged). Descriptions are the game's help texts (datItemHelp_msg, datSkillHelp_msg),
    /// the same the list writes in menu_note.
    /// </summary>
    internal static class CampListReader
    {
        private const string Tag = "[CampList]";
        private const string RowPrefix = "Canvas_UI/campUIBase/campUI/menu_itemskill/menu_item_window/menuitem_";
        private const string RowSuffix = "/TextTM";
        private const string Unavailable = "<material=\"TMC02\">";
        private const string KeyItemsTitle = "KEY ITEMS";
        private const int Rows = 8;
        private const long DrawnWithinMs = 200;   // drawn every frame while shown
        private const long OpenDelayMs = 250;     // rows and title are written about 200 ms after the list starts drawing
        private const long SettleMs = 80;

        private enum Kind { None, Item, Skill }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly TMP_Text[] RowText = new TMP_Text[Rows];

        // From the draw postfixes.
        private static cmpCursorInfo_t _cursor;
        private static Il2CppSystem.Object _data;
        private static Kind _drawn;
        private static long _drawnAt = long.MinValue / 2;

        private static Kind _open;

        /// <summary>The unit whose skill list is open (its pData), or null: the caster during the skill's target pick.</summary>
        internal static datUnitWork_t SkillUser => _open == Kind.Skill ? _data?.TryCast<datUnitWork_t>() : null;

        private static long _openedAt;
        private static bool _announced;
        private static int _index = -1;
        private static int _pendingIndex = -1;
        private static long _pendingSince;
        private static int _errors;
        private static bool _mismatchLogged;


        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            TmpRouter.Register("CampListReader", Classify);
            Type[] args = { typeof(fclPanelPos_t), typeof(cmpCursorInfo_t), typeof(Il2CppSystem.Object), typeof(sbyte), typeof(int), typeof(uint) };
            Patcher.Postfix("Camp lists", typeof(cmpDrawItem), nameof(cmpDrawItem.cmpDrawItemList), args,
                AccessTools.Method(typeof(CampListReader), nameof(AfterItemList)));
            Patcher.Postfix("Camp lists", typeof(cmpDrawSkill), nameof(cmpDrawSkill.cmpDrawSkillList), args,
                AccessTools.Method(typeof(CampListReader), nameof(AfterSkillList)));
        }

        private static TmpRouter.Handler Classify(string path)
        {
            if (!path.StartsWith(RowPrefix, StringComparison.Ordinal) || !path.EndsWith(RowSuffix, StringComparison.Ordinal))
                return null;
            string slot = path.Substring(RowPrefix.Length, path.Length - RowPrefix.Length - RowSuffix.Length);
            int row = slot == "top" ? 0
                : slot.StartsWith("base", StringComparison.Ordinal) && int.TryParse(slot.Substring(4), out int n) ? n - 1
                : -1;
            if (row < 0 || row >= Rows)
                return null;
            return (component, raw) => RowText[row] = component;
        }

        private static void AfterItemList(cmpCursorInfo_t __1) => Drawn(Kind.Item, __1, null);

        private static void AfterSkillList(cmpCursorInfo_t __1, Il2CppSystem.Object __2) => Drawn(Kind.Skill, __1, __2);

        private static void Drawn(Kind kind, cmpCursorInfo_t cursor, Il2CppSystem.Object data)
        {
            _drawn = kind;
            _cursor = cursor;
            _data = data;
            _drawnAt = Clock.ElapsedMilliseconds;
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            long now = Clock.ElapsedMilliseconds;
            try
            {
                if (now - _drawnAt > DrawnWithinMs || _cursor == null)
                {
                    if (_open != Kind.None)
                        Log.Info($"{Tag} {_open} list closed.");
                    _open = Kind.None;
                    return;
                }
                if (_open != _drawn)
                {
                    _open = _drawn;
                    _openedAt = now;
                    _announced = false;
                    _index = _pendingIndex = -1;
                    LogOpening();
                }

                cmpCursorPos_t pos = _cursor.CursorPos;
                if (pos == null)
                    return;
                int index = pos.Index + pos.Shift;
                if (index != _pendingIndex)
                {
                    _pendingIndex = index;
                    _pendingSince = now;
                }
                if (now - _openedAt < OpenDelayMs || now - _pendingSince < SettleMs || (_announced && index == _index))
                    return;

                TMP_Text row = pos.Shift < Rows ? RowText[pos.Shift] : null;
                if (!UiUtil.IsVisible(row))
                    return;
                bool opening = !_announced;
                _announced = true;
                _index = index;
                Speak(opening, index, row.text ?? "");
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"{Tag} {ex.Message}");
            }
        }

        private static void Speak(bool opening, int index, string raw)
        {
            string name = TextUtil.Clean(raw);
            string title = MenuReader.TitleOf("Pause menu");
            if (name.Length == 0)
            {
                // An empty list (GEMS with none, 13:22 session: ItemCnt 0) has only blank rows: say so once.
                if (opening && _open == Kind.Item && cmpInit.CMP_GBWK?.ItemCnt == 0)
                    Say((title.Length > 0 ? Sentence(title) + " " : "") + "Empty.", interrupt: false);
                return;
            }
            string detail = "", description = "";
            int total = _open == Kind.Item ? ItemFacts(index, name, title, ref detail, ref description)
                                           : SkillFacts(index, name, ref detail, ref description);

            string entry = name;
            if (detail.Length > 0)
                entry += ", " + detail;
            if (raw.Contains(Unavailable))
                entry += ", unavailable";
            if (total > 0)
                entry += $", {index + 1} of {total}";

            string text = entry + ".";
            if (opening && title.Length > 0)
                text = Sentence(title) + " " + text;
            if (description.Length > 0)
                text += " " + description;

            Say(text, interrupt: !opening);
        }

        private static void Say(string text, bool interrupt)
        {
            RepeatBuffer.Record(text);
            SpeechManager.Say(text, interrupt: interrupt, force: true);
        }

        /// <summary>The entry count; detail "have 3" (key items only above one, 0.0.135) and the description, when the id matches the row.</summary>
        private static int ItemFacts(int index, string name, string title, ref string detail, ref string description)
        {
            cmpGlobalWork_t g = cmpInit.CMP_GBWK;
            Il2CppStructArray<ushort> ids = g?.ItemIdx;
            if (g == null || ids == null || index < 0 || index >= ids.Length)
                return 0;
            int id = ids[index];
            string dataName = Safe(() => datItemName.Get(id));
            Log.Info($"{Tag} Item entry {index}: id {id} \"{dataName}\", row \"{name}\".");
            if (!Matches(dataName, name))
                return g.ItemCnt;

            Il2CppStructArray<byte> counts = dds3GlobalWork.DDS3_GBWK?.item;
            // Key items draw no count; most are single, but Deathstones stack (Francesco, 30 Oct 2026, 14:09 UI dump: "Deathstone"
            // alone): a key item says its count only above one (0.0.135).
            if (counts != null && id < counts.Length && (title != KeyItemsTitle || counts[id] > 1))
                detail = $"have {counts[id]}";
            description = Safe(() => datItemHelp_msg.Get(id));
            return g.ItemCnt;
        }

        /// <summary>The entry count; detail the cost ("3 MP", as the battle menu says it) and the description.</summary>
        private static int SkillFacts(int index, string name, ref string detail, ref string description)
        {
            datUnitWork_t unit = _data?.TryCast<datUnitWork_t>();
            Il2CppStructArray<int> skills = unit?.skill;
            if (skills == null)
                return 0;
            int total = Math.Min(Math.Max(unit.skillcnt, 0), skills.Length);
            if (index < 0 || index >= total)
                return total;
            int id = skills[index];
            string dataName = Safe(() => datSkillName.Get(id, unit.id));
            Log.Info($"{Tag} Skill entry {index}: id {id} \"{dataName}\", row \"{name}\", unit {unit.id}.");
            if (!Matches(dataName, name))
                return total;

            if (BattleSkills.Cost(id, unit, out string cost))
                detail = cost;
            description = Safe(() => datSkillHelp_msg.Get(id));
            return total;
        }

        private static bool Matches(string dataName, string rowName)
        {
            if (dataName == rowName)
                return true;
            if (!_mismatchLogged)
            {
                _mismatchLogged = true;
                Log.Warn($"{Tag} The game's table names \"{dataName}\" where the row shows \"{rowName}\": count, cost and description left out.");
            }
            return false;
        }

        /// <summary>The raw cursor and list data at each opening, to check the mapping against the screen.</summary>
        private static void LogOpening()
        {
            cmpCursorPos_t pos = _cursor?.CursorPos;
            string cursor = pos == null ? "no cursor" : $"Index {pos.Index} Shift {pos.Shift} ListNums {pos.ListNums} ShiftMax {pos.ShiftMax}";
            string list = "";
            if (_drawn == Kind.Item)
            {
                cmpGlobalWork_t g = cmpInit.CMP_GBWK;
                Il2CppStructArray<ushort> ids = g?.ItemIdx;
                var shown = new List<string>();
                for (int k = 0; ids != null && k < Math.Min((int)g.ItemCnt, ids.Length); k++)
                    shown.Add(ids[k].ToString());
                list = g == null ? "no CMP_GBWK" : $"ItemCnt {g.ItemCnt} ItemType {g.ItemType} ItemListFlag {g.ItemListFlag} ItemIdx [{string.Join(" ", shown)}]";
            }
            else
            {
                datUnitWork_t unit = _data?.TryCast<datUnitWork_t>();
                list = unit == null ? "pData is not a datUnitWork_t" : $"unit {unit.id} skillcnt {unit.skillcnt}";
            }
            Log.Info($"{Tag} {_drawn} list opened: {cursor}; {list}; title \"{MenuReader.TitleOf("Pause menu")}\".");
        }

        private static string Safe(Func<string> read)
        {
            try
            {
                return TextUtil.Clean(read() ?? "");
            }
            catch
            {
                return "";
            }
        }

        /// <summary>The title as a sentence: "What will you use?" stays, "KEY ITEMS" gets a full stop.</summary>
        private static string Sentence(string text)
        {
            char last = text[text.Length - 1];
            return last == '.' || last == '?' || last == '!' ? text : text + ".";
        }
    }
}
