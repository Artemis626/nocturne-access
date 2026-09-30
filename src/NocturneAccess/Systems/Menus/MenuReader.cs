using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

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
        }

        private sealed class Slot
        {
            public TMP_Text Component;
            public string Text;
        }

        private sealed class MenuState
        {
            public MenuSpec Spec;
            public readonly Dictionary<int, Slot> Slots = new Dictionary<int, Slot>();
            public readonly string[] Description = new string[8];
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
            },
            new MenuSpec
            {
                Name = "Terminal menu",
                Item = new Regex(@"^Canvas_UI/terminalUI/institutionUI/institution_menu/insmenu_window/insmenu_(?<slot>top|base\d\d)/TextTM$", RegexOptions.Compiled),
                Highlight = "<material=\"TMC01\">",
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
            foreach (MenuState state in States)
            {
                MenuSpec spec = state.Spec;

                Match item = spec.Item.Match(path);
                if (item.Success)
                {
                    int slot = SlotNumber(item.Groups["slot"].Value);
                    return (component, raw) => OnItem(state, slot, component, raw);
                }

                if (spec.TitlePath != null && path == spec.TitlePath)
                    return (component, raw) => OnTitle(state, raw);

                if (spec.Description != null)
                {
                    Match desc = spec.Description.Match(path);
                    if (desc.Success)
                    {
                        int line = desc.Groups["line"].Success ? int.Parse(desc.Groups["line"].Value) - 1 : 0;
                        if (line >= 0 && line < state.Description.Length)
                            return (component, raw) => OnDescription(state, line, raw);
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
            if (!state.Slots.TryGetValue(slot, out Slot s))
                state.Slots[slot] = s = new Slot();
            s.Component = component;
            s.Text = text;

            if (!raw.Contains(state.Spec.Highlight) || text.Length == 0)
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
                state.LastChange = Now;
        }

        private static void OnDescription(MenuState state, int line, string raw)
        {
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
                    else if (poll && state.Current >= 0 && state.PendingSlot < 0 && !IsVisible(state.CurrentComponent))
                        Close(state);
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
            if (!IsVisible(slot.Component))
            {
                if (now - state.PendingSince > WaitForVisibleMs)
                    state.PendingSlot = -1;
                return;
            }

            bool opening = state.Current < 0;
            if (opening && SpeechOrder.Hold(state.Spec.Name, ref state.HoldSince, now))
                return;
            state.HoldSince = -1;

            state.Current = state.PendingSlot;
            state.CurrentText = slot.Text;
            state.CurrentComponent = slot.Component;
            state.PendingSlot = -1;

            List<int> visible = state.Slots
                .Where(kv => kv.Value.Text.Length > 0 && IsVisible(kv.Value.Component))
                .Select(kv => kv.Key)
                .OrderBy(k => k)
                .ToList();
            int position = visible.IndexOf(state.Current) + 1;
            string item = position > 0 ? $"{slot.Text}, {position} of {visible.Count}" : slot.Text;
            if (opening && state.Spec.IsChoice)
                item = $"Choice, {visible.Count} options. {item}";

            var parts = new List<string>();
            if (state.Title.Length > 0 && state.Title != state.LastTitleSpoken)
            {
                parts.Add(state.Title);
                state.LastTitleSpoken = state.Title;
            }
            parts.Add(item);

            string description = string.Join(" ", state.Description.Where(d => !string.IsNullOrEmpty(d)));
            if (description.Length > 0)
                parts.Add(description);

            string utterance = string.Join(". ", parts);
            LastUtterance = utterance;
            LastUtteranceAt = Environment.TickCount64;
            if (opening)
                Log.Info($"{Tag} {state.Spec.Name} opened.");
            SpeechManager.Say(utterance, interrupt: !opening, force: true);
        }

        private static void Close(MenuState state)
        {
            state.Current = -1;
            state.CurrentText = null;
            state.CurrentComponent = null;
            state.LastTitleSpoken = "";
            Log.Info($"{Tag} {state.Spec.Name} closed.");
        }

        private static bool IsVisible(TMP_Text component)
        {
            try
            {
                return component != null && component.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }
    }
}
