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
    /// Numbers the Junk Shop and the Fountain of Life draw as sprites, from what the game draws (0.0.50).
    ///
    /// Junk Shop (docs/class-index.md "Junk Shop"; read offline):
    /// - fclShopDraw.shpDrawItem(path, col, item, price) draws each row of the BUY and SELL lists (shpDrawBuyItem and
    ///   shpDrawSellItem call it with the price from fclShopCalc.shpCalcItemPrice): the row key, item id and price are
    ///   kept, and the highlighted row says "50 Macca, have 3" (owned count dds3GlobalWork.DDS3_GBWK.item[id], as the
    ///   pause menu's item list) when the id's name matches the row;
    /// - the entry and count come from the list's cursor, fclShopInit.GBWK.ListWindow.CursorInfo.CursorPos (Index + Shift,
    ///   ListNums), so a list longer than its 9 rows counts right ("Mazio Rock, 11 of 11");
    /// - quantity: fclShopDraw.shpDrawNumsControl(csr, val, mode) draws the number chosen ("How many do you want, ho?",
    ///   CounterCtr.SetInstitutionColor(val)), fclShopDraw.shpDrawPayMoney(col, val) the sum (fclUI.DrawPayMoney).
    /// Fountain of Life:
    /// - fclRecoverDraw.rcvDrawPayMoney(col, val) draws the fee of the member under the cursor; each row shows HP as
    ///   counters under rcvhpbar (rcvhpnum_current, rcvhpnum_full; names from the game's string literals);
    /// - the entry and count: fclRecoverInit.GBWK.StockWindow.CursorInfo.CursorPos.
    /// </summary>
    internal static class FacilityReader
    {
        private const string Tag = "[Facility]";
        private const long DrawnWithinMs = 300;
        private const long QuantitySettleMs = 60;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private struct ShopRow
        {
            public ushort Item;
            public int Price;
        }

        private static readonly Dictionary<string, ShopRow> ShopRows = new Dictionary<string, ShopRow>();
        private static readonly HashSet<string> PathsLogged = new HashSet<string>();
        private static int _shopPay = -1;
        private static long _shopPayAt = long.MinValue / 2;
        private static int _quantity = -1;
        private static long _quantityAt = long.MinValue / 2;
        private static int _quantitySpoken = -1;
        private static long _quantityChangedAt;
        private static bool _quantityOpen;
        private static long _holdSince = -1;
        private static int _fee = -1;
        private static long _feeAt = long.MinValue / 2;
        private static int _errors;

        internal static string LastUtterance { get; private set; } = "";
        internal static long LastUtteranceAt { get; private set; } = long.MinValue;   // Environment.TickCount64

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            Patcher.Postfix("Shop prices", typeof(fclShopDraw), nameof(fclShopDraw.shpDrawItem),
                new[] { typeof(string), typeof(uint), typeof(ushort), typeof(int) },
                AccessTools.Method(typeof(FacilityReader), nameof(AfterShopItem)));
            Patcher.Postfix("Shop prices", typeof(fclShopDraw), nameof(fclShopDraw.shpDrawNumsControl),
                new[] { typeof(cmpCursorInfo_t), typeof(int), typeof(sbyte) },
                AccessTools.Method(typeof(FacilityReader), nameof(AfterShopQuantity)));
            Patcher.Postfix("Shop prices", typeof(fclShopDraw), nameof(fclShopDraw.shpDrawPayMoney),
                new[] { typeof(uint), typeof(int) },
                AccessTools.Method(typeof(FacilityReader), nameof(AfterShopPay)));
            Patcher.Postfix("Fountain fees", typeof(fclRecoverDraw), nameof(fclRecoverDraw.rcvDrawPayMoney),
                new[] { typeof(uint), typeof(int) },
                AccessTools.Method(typeof(FacilityReader), nameof(AfterFee)));
        }

        private static void AfterShopItem(string __0, ushort __2, int __3)
        {
            if (__0 == null)
                return;
            ShopRows[__0] = new ShopRow { Item = __2, Price = __3 };
            if (PathsLogged.Count < 24 && PathsLogged.Add(__0))
                Log.Info($"{Tag} Shop row \"{__0}\": item {__2}, price {__3}.");
        }

        private static void AfterShopQuantity(int __1, sbyte __2)
        {
            long now = Clock.ElapsedMilliseconds;
            if (__1 != _quantity)
            {
                Log.Info($"{Tag} Quantity {__1} (mode {__2}), pay {_shopPay}.");
                _quantity = __1;
                _quantityChangedAt = now;
            }
            _quantityAt = now;
        }

        private static void AfterShopPay(int __1)
        {
            _shopPay = __1;
            _shopPayAt = Clock.ElapsedMilliseconds;
        }

        private static void AfterFee(int __1)
        {
            if (__1 != _fee)
                Log.Info($"{Tag} Fountain fee {__1}.");
            _fee = __1;
            _feeAt = Clock.ElapsedMilliseconds;
        }

        /// <summary>"50 Macca, have 3" for a shop row, or "" when its price or item is not known.</summary>
        internal static string ShopDetail(TMP_Text component, string name)
        {
            try
            {
                string row = component?.transform.parent?.name;
                if (string.IsNullOrEmpty(row))
                    return "";
                foreach (KeyValuePair<string, ShopRow> kv in ShopRows)
                {
                    if (kv.Key != row && !kv.Key.EndsWith("/" + row, StringComparison.Ordinal))
                        continue;
                    string dataName = TextUtil.Clean(datItemName.Get(kv.Value.Item) ?? "");
                    if (dataName != name)
                    {
                        Log.Info($"{Tag} Shop row {row}: item {kv.Value.Item} is \"{dataName}\", the row shows \"{name}\".");
                        return "";
                    }
                    string detail = $"{kv.Value.Price.ToString(CultureInfo.InvariantCulture)} Macca";
                    Il2CppStructArray<byte> counts = dds3GlobalWork.DDS3_GBWK?.item;
                    if (counts != null && kv.Value.Item < counts.Length)
                        detail += $", have {counts[kv.Value.Item]}";
                    return detail;
                }
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"{Tag} Shop row: {ex.Message}");
            }
            return "";
        }

        /// <summary>The shop list's entry (1-based) and count when its cursor is on this row (slot 1 = first row), else null.</summary>
        internal static (int, int)? ShopPosition(int slot)
            => Position(fclShopInit.GBWK?.ListWindow?.CursorInfo?.CursorPos, slot);

        /// <summary>The Fountain list's entry (1-based) and count when its cursor is on this row, else null.</summary>
        internal static (int, int)? FountainPosition(int slot)
            => Position(fclRecoverInit.GBWK?.StockWindow?.CursorInfo?.CursorPos, slot);

        private static (int, int)? Position(cmpCursorPos_t pos, int slot)
        {
            try
            {
                if (pos == null || pos.Shift + 1 != slot || pos.ListNums <= 0)
                    return null;
                int entry = pos.Index + pos.Shift;
                return entry >= 0 && entry < pos.ListNums ? (entry + 1, (int)pos.ListNums) : ((int, int)?)null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>"HP 50 of 84, 120 Macca" for a Fountain row: the row's HP counters and the fee drawn for the cursor.</summary>
        internal static string FountainDetail(TMP_Text component, string name)
        {
            var parts = new List<string>();
            try
            {
                Transform row = component?.transform.parent;
                int? hp = null, max = null;
                var names = new List<string>();
                if (row != null)
                {
                    foreach (CounterCtr c in row.GetComponentsInChildren<CounterCtr>(false))
                    {
                        string n = UiUtil.PathOf(c.transform).Replace(UiUtil.PathOf(row) + "/", "");
                        names.Add($"{n}={c.value}");
                        if (n.Contains("hpnum_current") && !hp.HasValue)
                            hp = c.value;
                        else if (n.Contains("hpnum_full") && !max.HasValue)
                            max = c.value;
                    }
                }
                if (PathsLogged.Add("fountain:" + (row != null ? row.name : "")))
                    Log.Info($"{Tag} Fountain row {(row != null ? row.name : "?")}: {string.Join("; ", names)}.");
                if (hp.HasValue && max.HasValue)
                    parts.Add($"HP {hp.Value} of {max.Value}");
                if (Clock.ElapsedMilliseconds - _feeAt <= DrawnWithinMs && _fee >= 0)
                    parts.Add($"{_fee.ToString(CultureInfo.InvariantCulture)} Macca");
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"{Tag} Fountain row: {ex.Message}");
            }
            return string.Join(", ", parts);
        }

        /// <summary>Called every frame: the quantity chosen in the shop ("3, 150 Macca").</summary>
        internal static void Tick()
        {
            long now = Clock.ElapsedMilliseconds;
            try
            {
                if (now - _quantityAt > DrawnWithinMs)
                {
                    if (_quantityOpen)
                        Log.Info($"{Tag} Quantity closed.");
                    _quantityOpen = false;
                    _quantitySpoken = -1;
                    return;
                }
                if (now - _quantityChangedAt < QuantitySettleMs || _quantity == _quantitySpoken)
                    return;
                bool opening = !_quantityOpen;
                // "How many do you want, ho?" is a dialogue page: it goes first.
                if (opening && SpeechOrder.Hold("Shop quantity", ref _holdSince, now))
                    return;
                _holdSince = -1;
                _quantityOpen = true;
                _quantitySpoken = _quantity;
                string text = _quantity.ToString(CultureInfo.InvariantCulture);
                if (now - _shopPayAt <= DrawnWithinMs && _shopPay >= 0)
                    text += $", {_shopPay.ToString(CultureInfo.InvariantCulture)} Macca";
                LastUtterance = text;
                LastUtteranceAt = Environment.TickCount64;
                SpeechManager.Say(text, interrupt: !opening, force: true);
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"{Tag} {ex.Message}");
            }
        }
    }
}
