using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using HarmonyLib;
using Il2Cpp;
using Il2Cppcamp_H;
using Il2Cppfacility_H;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// Rag's Jewelry, Ginza (0.0.53): the trade lists "ITEM TRADE", "ELEMENT TRADE", "MITAMA TRADE".
    /// "ITEM TRADE. Bead Chain, Pearl 1, have 3, 1 of 14. Revives one ally to full HP."
    ///
    /// Sources (docs/class-index.md "Rag's Jewelry"):
    /// - F6 dumps of 4 Oct 2026, 21:52 and 21:53: ragUI/ragtitle/Text_ragtitleTM (the title), ragUI/ragitemlist/ragitem_row/
    ///   ragitem_rowNN/Text_nameTM (the entries, TMC01 under the cursor, TMC02 when the jewels are not enough, and then TMC02
    ///   under the cursor too, so the highlight cannot follow the cursor), ragUI/ragjewel/ragjewel_rowNN/Text_nameTM (the
    ///   jewels the entry under the cursor costs, TMC18), ragUI/raghelp/Text01TM..Text03TM (its help).
    /// - fclRagDraw.ragDrawItemWindow and ragDrawDevilWindow(lstListWindow_t pListWindow, byte Type, sbyte[] pIdxList, int top,
    ///   sbyte Mode) draw the item and the Element / Mitama lists every frame with their list window: its CursorInfo.CursorPos
    ///   gives the entry (Index + Shift), the row (Shift) and the count (ListNums), as in the shop.
    /// - The jewel counts are sprite digits: under each jewel row the counters num_jewel (the cost) and num_possess (owned),
    ///   names from the game's string literals ("ragjewel_row{0:D2}", "_jewel", "num_jewel", "_possess", "num_possess").
    /// </summary>
    internal static class RagReader
    {
        private const string Tag = "[Rag]";
        private const string RootName = "ragUI";
        private const string Unavailable = "<material=\"TMC02\">";
        private const long DrawnWithinMs = 200;
        private const long OpenDelayMs = 150;
        private const long SettleMs = 80;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static lstListWindow_t _window;
        private static long _drawnAt = long.MinValue / 2;
        private static Transform _root;
        private static bool _open;
        private static long _openedAt;
        private static string _title = "";
        private static int _key = -1, _pendingKey = -1;
        private static long _pendingSince;
        private static long _holdSince = -1;
        private static bool _rowsLogged;
        private static int _errors;

        internal static string LastUtterance { get; private set; } = "";
        internal static long LastUtteranceAt { get; private set; } = long.MinValue;   // Environment.TickCount64

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            TmpRouter.Register("RagReader", Classify);
            var args = new[] { typeof(lstListWindow_t), typeof(byte), typeof(Il2CppStructArray<sbyte>), typeof(int), typeof(sbyte) };
            Patcher.Postfix("Rag's Jewelry", typeof(fclRagDraw), nameof(fclRagDraw.ragDrawItemWindow), args,
                AccessTools.Method(typeof(RagReader), nameof(AfterWindow)));
            Patcher.Postfix("Rag's Jewelry", typeof(fclRagDraw), nameof(fclRagDraw.ragDrawDevilWindow), args,
                AccessTools.Method(typeof(RagReader), nameof(AfterWindow)));
        }

        /// <summary>Finds the screen's root from its list, jewel, help and title texts; its institution menu stays the MenuReader's.</summary>
        private static TmpRouter.Handler Classify(string path)
        {
            const string prefix = "Canvas_UI/" + RootName + "/";
            if (!path.StartsWith(prefix + "ragitemlist/", StringComparison.Ordinal) && !path.StartsWith(prefix + "ragjewel/", StringComparison.Ordinal)
                && !path.StartsWith(prefix + "raghelp/", StringComparison.Ordinal) && !path.StartsWith(prefix + "ragtitle/", StringComparison.Ordinal))
                return null;
            return (component, raw) =>
            {
                if (_root != null)
                    return;
                for (Transform t = component.transform; t != null; t = t.parent)
                {
                    if (t.name == RootName)
                    {
                        _root = t;
                        return;
                    }
                }
            };
        }

        private static void AfterWindow(lstListWindow_t __0)
        {
            _window = __0;
            _drawnAt = Clock.ElapsedMilliseconds;
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            long now = Clock.ElapsedMilliseconds;
            try
            {
                if (now - _drawnAt > DrawnWithinMs || _window == null || _root == null)
                {
                    if (_open)
                        Log.Info($"{Tag} List closed.");
                    _open = false;
                    _key = _pendingKey = -1;
                    return;
                }
                cmpCursorPos_t pos = _window.CursorInfo?.CursorPos;
                if (pos == null)
                    return;
                string title = Text(_root.Find("ragtitle/Text_ragtitleTM"));
                int entry = pos.Index + pos.Shift;
                int key = entry;
                if (!_open || title != _title)
                {
                    if (!_open)
                        Log.Info($"{Tag} List opened \"{title}\": Index {pos.Index} Shift {pos.Shift} ListNums {pos.ListNums} ShiftMax {pos.ShiftMax}.");
                    _open = true;
                    _openedAt = now;
                    _key = -1;
                    _rowsLogged = false;
                }
                if (key != _pendingKey)
                {
                    _pendingKey = key;
                    _pendingSince = now;
                }
                if (now - _openedAt < OpenDelayMs || now - _pendingSince < SettleMs || key == _key)
                    return;
                bool opening = _key < 0;
                if (opening && SpeechOrder.Hold("Rag list", ref _holdSince, now))
                    return;
                string text = Describe(pos.Shift + 1, entry, pos.ListNums);
                if (text.Length == 0)
                    return;
                _holdSince = -1;
                _key = key;
                if (opening && title.Length > 0)
                    text = title + ". " + text;
                _title = title;
                Say(text, interrupt: !opening);
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"{Tag} {ex.Message}");
            }
        }

        /// <summary>"Bead Chain, Pearl 1, have 3, 1 of 14. Revives one ally to full HP." or "" while the row is blank.</summary>
        private static string Describe(int row, int entry, int count)
        {
            Transform rowObj = _root.Find($"ragitemlist/ragitem_row/ragitem_row{row:D2}");
            Transform nameObj = rowObj == null ? null : rowObj.Find("Text_nameTM");
            TMP_Text nameText = nameObj == null ? null : nameObj.GetComponent<TMP_Text>();
            if (!UiUtil.IsVisible(nameText))
                return "";
            string name = TextUtil.Clean(nameText.text);
            if (name.Length == 0)
                return "";
            var parts = new List<string> { name };
            if ((nameText.text ?? "").Contains(Unavailable))
                parts.Add("unavailable");
            parts.AddRange(Jewels());
            if (count > 0 && entry >= 0 && entry < count)
                parts.Add($"{entry + 1} of {count}");
            string text = string.Join(", ", parts) + ".";
            var help = new List<string>();
            for (int k = 1; k <= 3; k++)
            {
                string line = Text(_root.Find($"raghelp/Text{k:D2}TM"));
                if (line.Length > 0)
                    help.Add(line);
            }
            if (help.Count > 0)
                text += " " + string.Join(" ", help);
            return text;
        }

        /// <summary>The jewels the entry under the cursor costs: "Pearl 1, have 3" per jewel row shown.</summary>
        private static List<string> Jewels()
        {
            var list = new List<string>();
            var logged = new List<string>();
            for (int k = 1; k <= 8; k++)
            {
                Transform jewel = _root.Find($"ragjewel/ragjewel_row{k:D2}");
                if (jewel == null)
                    break;
                string name = Text(jewel.Find("Text_nameTM"));
                if (name.Length == 0)
                    continue;
                int? cost = null, have = null;
                foreach (CounterCtr c in jewel.GetComponentsInChildren<CounterCtr>(false))
                {
                    string path = UiUtil.PathOf(c.transform);
                    logged.Add($"{path.Substring(path.IndexOf("ragjewel_row", StringComparison.Ordinal))}={c.value}");
                    if (path.Contains("possess") && !have.HasValue)
                        have = c.value;
                    else
                        cost ??= c.value;   // num_jewel
                }
                string part = name;
                if (cost.HasValue)
                    part += " " + cost.Value.ToString(CultureInfo.InvariantCulture);
                if (have.HasValue)
                    part += ", have " + have.Value.ToString(CultureInfo.InvariantCulture);
                list.Add(part);
            }
            if (!_rowsLogged && logged.Count > 0)
            {
                _rowsLogged = true;
                Log.Info($"{Tag} Jewel counters: {string.Join("; ", logged)}.");
            }
            return list;
        }

        private static string Text(Transform t)
        {
            if (t == null || !t.gameObject.activeInHierarchy)
                return "";
            TMP_Text text = t.GetComponent<TMP_Text>();
            return text == null ? "" : TextUtil.Clean(text.text);
        }

        private static void Say(string text, bool interrupt)
        {
            LastUtterance = text;
            LastUtteranceAt = Environment.TickCount64;
            SpeechManager.Say(text, interrupt: interrupt, force: true);
        }
    }
}
