using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Battle;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// Speaks the highlighted item of menus that mark their cursor with a font material.
    ///
    /// Source (docs/class-index.md, "Menu highlight is a font material"): when the cursor moves,
    /// the game rewrites the old item with its normal material and the new item with the highlight
    /// material. So a write that carries the highlight material means "this item now has the cursor".
    ///
    /// Each menu is a MenuSpec: item path pattern (with a slot), highlight material, optional
    /// screen-title component, optional description lines.
    /// - Announced 80 ms after the last related write, so the title and description written in the
    ///   same frame are included: "Title. Item, 2 of 6. Description".
    /// - Menus are often built while hidden, so an announcement waits until the item is visible.
    /// - The first announcement after a menu opens is queued, and waits while a dialogue page or prompt
    ///   is still due, so the terminal's question comes first (Systems/SpeechOrder.cs); later cursor
    ///   moves interrupt.
    /// - When the highlighted item disappears, the menu counts as closed, so reopening it announces again.
    /// </summary>
    internal static class MenuReader
    {
        private const string Tag = "[Menu]";
        private const long SettleMs = 80;
        private const long WaitForVisibleMs = 60_000;
        private const long VisibilityPollMs = 250;

        private sealed class MenuSpec
        {
            public string Name;
            public Regex Item;          // group "slot": "top", "baseNN" or "NN"
            public string Highlight;    // material tag marking the highlighted item
            public string TitlePath;    // exact path, or null
            public Regex Description;   // group "line" (a one-line description may omit it), or null
            public bool IsChoice;       // a Yes/No: the opening says "Choice, N options."
            public bool CloseWhenUnhighlighted;   // no item carries the highlight any more: the menu is closed
            public bool Reshow;                   // closed while an item keeps the highlight: announce it when shown again
            public string YieldTo;                // stay silent while this menu has a visible highlighted item
            public bool ReannounceOnTitle;        // a new screen title re-announces the current item
            public Func<string> OpeningPrefix;    // said before the item when the menu opens, if not empty
            public Func<string, string> ItemDetail;   // said after the item's text (a skill's cost), if not empty
            public bool NoPosition;               // one slot shows the current choice: no "1 of 1"
            public bool LinesAreSentences;        // description lines are separate statements, not one wrapped sentence
            public Func<TMP_Text, string, string> RowDetail;   // game data for the highlighted row (a price), said after the item
            public Func<int, (int, int)?> GamePosition;        // the entry and count from the game's cursor for this slot, or null to count rows
        }

        private sealed class Slot
        {
            public TMP_Text Component;
            public string Text;
            public bool Highlighted;    // the last write carried the highlight material
        }

        private sealed class MenuState
        {
            public MenuSpec Spec;
            public readonly Dictionary<int, Slot> Slots = new Dictionary<int, Slot>();
            public readonly string[] Description = new string[8];
            public readonly TMP_Text[] DescriptionComponents = new TMP_Text[8];
            public int Current = -1;
            public string CurrentText;
            public TMP_Text CurrentComponent;
            public int PendingSlot = -1;
            public string PendingText;
            public long PendingSince;
            public long LastChange;
            public long HoldSince = -1;
            public string Title = "";
            public string LastTitleSpoken = "";
            public bool Yielded;        // stayed silent for its YieldTo menu since the last announcement
            public int LastSlot = -1;   // the item last announced or taken, kept across Close
            public string LastText;
        }

        // Paths and materials: probe logs of 30 Sep 2026, see docs/class-index.md.
        private static readonly MenuSpec[] Specs =
        {
            new MenuSpec
            {
                Name = "Pause menu",
                Item = new Regex(@"^Canvas_UI/campUIBase/campUI/menu/menu_window/menu_(?<slot>top|base\d\d)/TextTM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC01\">",
                TitlePath = "Canvas_UI/campUIBase/campUI/title_window/title_window_oubei/TextTM",
                // The party pick keeps "Stats" highlighted here; back in the pause menu the title changes
                // from "STATS" to "COMMAND MENU" with no item write (22:05 session, 22:07:28).
                YieldTo = "Party pick",
                ReannounceOnTitle = true,
            },
            new MenuSpec
            {
                // Party member pick (Stats "STATS", Skill "Who will use a skill?"): the member's name turns
                // TMC01 and back to TMC00 on cancel, while the name stays visible (22:05 session, 22:07:10).
                // Stock names (stock_statusNN, empty while no demons) count after the party (slot 10 + NN).
                Name = "Party pick",
                Item = new Regex(@"^Canvas_UI/campUIBase/campUI/party/party_status/(?:party_status(?<slot>\d\d)|stock_status(?<stock>\d\d))/Text_nameTM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC01\">",
                TitlePath = "Canvas_UI/campUIBase/campUI/title_window/title_window_oubei/TextTM",
                CloseWhenUnhighlighted = true,
                Reshow = true,
            },
            new MenuSpec
            {
                // The game's "institution" menus share one layout under their screen, highlighted TMC01 (0.0.49): the Terminal
                // (terminalUI), the Fountain of Life (recovUI: Heal, Revive, Remove Curse, Talk, Exit), the Junk Shop (shopUI:
                // Buy, Sell, Talk, Exit), the Cathedral of Shadows (combUI_oubei: Fuse, Talk, Exit; its Talk topics Fusion
                // Basics, Element and Mitama, ...). F6 dumps of 3 Oct 2026, 16:22 and 21:14 to 21:21.
                Name = "Institution menu",
                Item = new Regex(@"^Canvas_UI/[^/]+/institutionUI/institution_menu/insmenu_window/insmenu_(?<slot>top|base\d\d)/TextTM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC01\">",
            },
            new MenuSpec
            {
                // The Terminal's Amala Link destinations (13:42 session, 5 Oct 2026, F6 dump): "Amala Link" title, one place per
                // row (TMC01 highlighted; unused rows hold "(null)"), "> Select your destination." is a dialogue page. 0.0.54.
                Name = "Amala Link",
                Item = new Regex(@"^Canvas_UI/terminalUI/menu_tmnl/tmnlmenu_window/tmnlmenu_(?<slot>top|base\d\d)/tmnl_text/TextTM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC01\">",
                TitlePath = "Canvas_UI/terminalUI/tmnltitle/Text_tmnltitleTM",
                CloseWhenUnhighlighted = true,
            },
            new MenuSpec
            {
                // Junk Shop lists (21:21 session): "BUY" / "SELL" title, one item name per row (TMC01 highlighted), the item's help
                // in shophelp/Text01TM..Text03TM (a wrapped sentence). Prices, counts and the position in a list longer than
                // its 9 rows come from the game (FacilityReader, 0.0.50).
                Name = "Shop list",
                Item = new Regex(@"^Canvas_UI/shopUI/shoplist/shop_row/shop_row(?<slot>\d\d)/Text_nameTM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC01\">",
                TitlePath = "Canvas_UI/shopUI/shoptitle/Text_shoptitleTM",
                Description = new Regex(@"^Canvas_UI/shopUI/shophelp/Text(?<line>\d\d)TM$", RegexOptions.Compiled),
                CloseWhenUnhighlighted = true,
                RowDetail = FacilityReader.ShopDetail,
                GamePosition = FacilityReader.ShopPosition,
            },
            // The Cathedral of Shadows' demon tables are read by FusionReader (0.0.50), from the game's cursor.
            new MenuSpec
            {
                // Its party list ("HEAL" title; "Whom shall I heal?" is a dialogue page): one name per row, highlighted TMC01; a
                // healed member leaves the list (row01 became "Pixie" after Art was healed). HP and the fee come from the game
                // (FacilityReader, 0.0.50).
                Name = "Fountain list",
                Item = new Regex(@"^Canvas_UI/recovUI/rcvlist/rcv_row/rcv_row(?<slot>\d\d)/Text_nameTM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC01\">",
                TitlePath = "Canvas_UI/recovUI/rcvtitle/Text_rcvtitleTM",
                CloseWhenUnhighlighted = true,
                RowDetail = FacilityReader.FountainDetail,
                GamePosition = FacilityReader.FountainPosition,
            },
            new MenuSpec
            {
                Name = "Title menu",
                Item = new Regex(@"^Canvas_UI/titleUI\(Clone\)/titlemain/titlemainmenu/stretch/titlemain_item(?<slot>\d\d)/slide/Text_TM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC00\">",
                Description = new Regex(@"^Canvas_UI/titleUI\(Clone\)/titlemain/titlemain_note/Text_mainnote(?<line>\d\d)TM$", RegexOptions.Compiled),
            },
            new MenuSpec
            {
                // Battle commands (20:07 session): "Attack", "Pass", highlighted TMC01; help line under the menu.
                Name = "Battle menu",
                Item = new Regex(@"^Canvas_UI/bmenuset\(Clone\)/bmenu/[^/]+/bmenu_command/bmenu_command(?<slot>\d\d)/bmenu_text01TM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC01\">",
                Description = new Regex(@"^Canvas_UI/bmenuset\(Clone\)/bhelp_window/text_mask/TextTM$", RegexOptions.Compiled),
                ItemDetail = BattleSkills.CostOfActing,          // "Lunge, 4 HP": the cost is drawn as images (2 Oct 2026)
                OpeningPrefix = BattleReader.TurnAnnouncement,   // "Pixie's turn" (22:14 session: the acting unit was never said)
            },
            new MenuSpec
            {
                // Magatama (13:51 session, 2 Oct 2026): one name slot shows the magatama under the cursor (TMC19), with its
                // notes ("Imparts Physical skills", "Normal resistance"); the stat bonuses are images.
                Name = "Magatama",
                Item = new Regex(@"^Canvas_UI/magUI\(Clone\)/(?<slot>magname)_base/TextTM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC19\">",
                TitlePath = "Canvas_UI/magUI(Clone)/magtitle_window/TextTM",
                Description = new Regex(@"^Canvas_UI/magUI\(Clone\)/magnote/Text(?<line>\d\d)TM$", RegexOptions.Compiled),
                NoPosition = true,
                LinesAreSentences = true,
            },
            // Yes/No prompts outside dialogue (16:49 session): highlighted option TMC08, other TMC22.
            // Their questions are prompts (AnnouncementReader), so the options queue behind them.
            new MenuSpec
            {
                Name = "Title prompt",   // EXIT "> End the game?"; the difficulty confirmation
                Item = new Regex(@"^Canvas_UI/titleUI\(Clone\)/(?:titlemain|titlemode)/talk01_choice/choice_answer(?<slot>\d\d)/answer01TM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC08\">",
                IsChoice = true,
            },
            new MenuSpec
            {
                Name = "Name prompt",   // name entry "> Is this okay?"; the question comes from NameEntryReader
                Item = new Regex(@"^Canvas_UI/nameUI_oubei\(Clone\)/talk01_choice/choice_answer(?<slot>\d\d)/answer01TM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC08\">",
                IsChoice = true,
            },
            new MenuSpec
            {
                Name = "Config prompt",   // "Returning without changing settings. Will you proceed?"
                Item = new Regex(@"^Canvas_UI/configUI_steam\(Clone\)/ChoiceKernel/talk01_choice/choice_answer(?<slot>\d\d)/answer01TM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC08\">",
                IsChoice = true,
            },
        };

        private static readonly List<MenuState> States = Specs.Select(s => new MenuState { Spec = s }).ToList();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _lastPoll;

        /// <summary>The last menu announcement and when it was made, for the repeat key.</summary>
        internal static string LastUtterance { get; private set; } = "";
        internal static long LastUtteranceAt { get; private set; } = long.MinValue;   // Environment.TickCount64
        internal static long Now => Clock.ElapsedMilliseconds;

        /// <summary>True while the named menu is shown or about to be announced.</summary>
        internal static bool IsActive(string name)
            => States.Any(s => s.Spec.Name == name && (s.Current >= 0 || s.PendingSlot >= 0));

        /// <summary>The screen title last written for the named menu ("What will you use?" for the pause menu's lists), or "".</summary>
        internal static string TitleOf(string name)
            => States.FirstOrDefault(s => s.Spec.Name == name)?.Title ?? "";

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
            {
                Log.Info($"{Tag} Off in the config.");
                return;
            }
            TmpRouter.Register("MenuReader", Classify);
        }

        private static TmpRouter.Handler Classify(string path)
        {
            // One title component can serve several menus (pause menu and party pick).
            List<MenuState> titled = States.Where(s => s.Spec.TitlePath == path).ToList();
            if (titled.Count > 0)
                return (component, raw) => titled.ForEach(s => OnTitle(s, raw));

            foreach (MenuState state in States)
            {
                MenuSpec spec = state.Spec;

                Match item = spec.Item.Match(path);
                if (item.Success)
                {
                    int slot = item.Groups["stock"].Success
                        ? 10 + SlotNumber(item.Groups["stock"].Value)
                        : SlotNumber(item.Groups["slot"].Value);
                    return (component, raw) => OnItem(state, slot, component, raw);
                }

                if (spec.Description != null)
                {
                    Match desc = spec.Description.Match(path);
                    if (desc.Success)
                    {
                        int line = desc.Groups["line"].Success ? int.Parse(desc.Groups["line"].Value) - 1 : 0;
                        if (line >= 0 && line < state.Description.Length)
                            return (component, raw) => OnDescription(state, line, component, raw);
                    }
                }
            }
            return null;
        }

        /// <summary>"top" is the first slot; "base02" and "02" are slot 2.</summary>
        private static int SlotNumber(string slot)
        {
            if (slot == "top")
                return 1;
            string digits = new string(slot.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int n) ? n : 99;
        }

        private static void OnItem(MenuState state, int slot, TMP_Text component, string raw)
        {
            string text = TextUtil.Clean(raw);
            if (text == "(null)")
                text = "";   // the game's placeholder in unused rows (Amala Link list): neither an item nor counted
            if (!state.Slots.TryGetValue(slot, out Slot s))
                state.Slots[slot] = s = new Slot();
            s.Component = component;
            s.Text = text;
            s.Highlighted = raw.Contains(state.Spec.Highlight) && text.Length > 0;

            if (!s.Highlighted)
                return;
            // Menus rewrite the highlighted item every frame with identical text (0.0.5 test, 30 Sep 2026):
            // only a real change may start or restart the settle timer, or it never runs out.
            if (state.PendingSlot < 0 && slot == state.Current && text == state.CurrentText)
                return;
            if (state.PendingSlot == slot && text == state.PendingText)
                return;
            state.PendingText = text;

            long now = Now;
            if (state.PendingSlot < 0)
                state.PendingSince = now;
            state.PendingSlot = slot;
            state.LastChange = now;
        }

        private static void OnTitle(MenuState state, string raw)
        {
            string title = TextUtil.Clean(raw);
            if (title == state.Title)
                return;
            state.Title = title;
            if (state.PendingSlot >= 0)
            {
                state.LastChange = Now;
            }
            else if (state.Spec.ReannounceOnTitle && state.Yielded && state.Current >= 0
                     && state.Slots.TryGetValue(state.Current, out Slot current) && current.Highlighted)
            {
                // Back on this menu from a screen that shared its title: say where the cursor is.
                state.PendingSlot = state.Current;
                state.PendingText = current.Text;
                state.PendingSince = state.LastChange = Now;
            }
        }

        private static void OnDescription(MenuState state, int line, TMP_Text component, string raw)
        {
            state.DescriptionComponents[line] = component;
            string text = TextUtil.Clean(raw);
            if (text == state.Description[line])
                return;
            state.Description[line] = text;
            if (state.PendingSlot >= 0)
                state.LastChange = Now;
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            long now = Now;
            bool poll = now - _lastPoll >= VisibilityPollMs;
            if (poll)
                _lastPoll = now;

            foreach (MenuState state in States)
            {
                try
                {
                    if (state.PendingSlot >= 0 && now - state.LastChange >= SettleMs)
                        TryAnnounce(state, now);
                    else if (poll && state.Current >= 0 && state.PendingSlot < 0
                             && (!UiUtil.IsVisible(state.CurrentComponent)
                                 || (state.Spec.CloseWhenUnhighlighted && !state.Slots.Values.Any(s => s.Highlighted))))
                        Close(state);
                    else if (poll && state.Spec.Reshow && state.Current < 0 && state.PendingSlot < 0)
                        Reshow(state, now);
                }
                catch (Exception ex)
                {
                    Log.Error($"{Tag} {state.Spec.Name}: {ex.Message}");
                    state.PendingSlot = -1;
                }
            }
        }

        private static void TryAnnounce(MenuState state, long now)
        {
            if (!state.Slots.TryGetValue(state.PendingSlot, out Slot slot))
            {
                state.PendingSlot = -1;
                return;
            }

            // Built while hidden: wait until it is shown.
            if (!UiUtil.IsVisible(slot.Component))
            {
                if (now - state.PendingSince > WaitForVisibleMs)
                    state.PendingSlot = -1;
                return;
            }

            bool opening = state.Current < 0;

            MenuState yieldTo = state.Spec.YieldTo == null ? null : States.FirstOrDefault(s => s.Spec.Name == state.Spec.YieldTo);
            if (yieldTo != null && HasVisibleHighlight(yieldTo) && state.PendingSlot == state.LastSlot && slot.Text == state.LastText)
            {
                // The other menu has the cursor: take this one's position silently. When this menu reopens
                // (back from a screen), the other one is where the cursor is, so it announces again.
                state.Current = state.PendingSlot;
                state.CurrentText = slot.Text;
                state.CurrentComponent = slot.Component;
                state.PendingSlot = -1;
                state.Yielded = true;
                if (opening && yieldTo.Current >= 0)
                    Close(yieldTo);
                Log.Info($"{Tag} {state.Spec.Name} gives way to the {yieldTo.Spec.Name}.");
                return;
            }

            if (opening && SpeechOrder.Hold(state.Spec.Name, ref state.HoldSince, now))
                return;
            state.HoldSince = -1;
            state.Yielded = false;

            state.Current = state.LastSlot = state.PendingSlot;
            state.CurrentText = state.LastText = slot.Text;
            state.CurrentComponent = slot.Component;
            state.PendingSlot = -1;
            // The menus that give way to this one re-announce when their title comes back.
            foreach (MenuState other in States.Where(s => s.Spec.YieldTo == state.Spec.Name))
                other.Yielded = true;

            List<int> visible = state.Slots
                .Where(kv => kv.Value.Text.Length > 0 && UiUtil.IsVisible(kv.Value.Component))
                .Select(kv => kv.Key)
                .OrderBy(k => k)
                .ToList();
            int position = visible.IndexOf(state.Current) + 1;
            int count = visible.Count;
            // A list longer than its rows (the shop's SELL list) counts from the game's cursor, when it is on this row.
            (int, int)? game = state.Spec.GamePosition?.Invoke(state.Current);
            if (game.HasValue)
                (position, count) = game.Value;
            string detail = state.Spec.ItemDetail?.Invoke(slot.Text) ?? "";
            string rowDetail = state.Spec.RowDetail?.Invoke(slot.Component, slot.Text) ?? "";
            if (rowDetail.Length > 0)
                detail = detail.Length > 0 ? $"{detail}, {rowDetail}" : rowDetail;
            string named = detail.Length > 0 ? $"{slot.Text}, {detail}" : slot.Text;
            string item = position > 0 && !state.Spec.NoPosition ? $"{named}, {position} of {count}" : named;
            if (opening && state.Spec.IsChoice)
                item = $"Choice, {count} options. {item}";

            var parts = new List<string>();
            if (opening && state.Spec.OpeningPrefix != null)
            {
                string prefix = state.Spec.OpeningPrefix();
                if (!string.IsNullOrEmpty(prefix))
                    parts.Add(prefix);
            }
            if (state.Title.Length > 0 && state.Title != state.LastTitleSpoken)
            {
                parts.Add(state.Title);
                state.LastTitleSpoken = state.Title;
            }
            parts.Add(item);

            // Each line as it is now: a line emptied by the game is a blank write, which never reaches the readers, so the
            // stored text would keep the previous item's words (21:46 session: "Support-type Magatama. for one ally.").
            for (int i = 0; i < state.Description.Length; i++)
            {
                TMP_Text line = state.DescriptionComponents[i];
                if (line != null)
                    state.Description[i] = UiUtil.IsVisible(line) ? TextUtil.Clean(line.text) : "";
            }
            string description = string.Join(state.Spec.LinesAreSentences ? ". " : " ", state.Description.Where(d => !string.IsNullOrEmpty(d)));
            if (state.Spec.LinesAreSentences && description.Length > 0)
                description += ".";
            if (description.Length > 0)
                parts.Add(description);

            string utterance = string.Join(". ", parts);
            LastUtterance = utterance;
            LastUtteranceAt = Environment.TickCount64;
            if (opening)
                Log.Info($"{Tag} {state.Spec.Name} opened.");
            SpeechManager.Say(utterance, interrupt: !opening, force: true);
        }

        private static bool HasVisibleHighlight(MenuState state)
            => state.Slots.Values.Any(s => s.Highlighted && UiUtil.IsVisible(s.Component));

        /// <summary>Closed while an item kept the highlight (a screen covered it): announce it when it is shown again.</summary>
        private static void Reshow(MenuState state, long now)
        {
            foreach (KeyValuePair<int, Slot> kv in state.Slots)
            {
                if (!kv.Value.Highlighted || !UiUtil.IsVisible(kv.Value.Component))
                    continue;
                state.PendingSlot = kv.Key;
                state.PendingText = kv.Value.Text;
                state.PendingSince = state.LastChange = now;
                Log.Info($"{Tag} {state.Spec.Name} shown again.");
                return;
            }
        }

        private static void Close(MenuState state)
        {
            state.Current = -1;
            state.CurrentText = null;
            state.CurrentComponent = null;
            state.LastTitleSpoken = "";
            Log.Info($"{Tag} {state.Spec.Name} closed.");
        }
    }
}
