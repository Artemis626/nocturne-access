using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// Speaks one-off prompts and notices: each listed component's text is spoken once it has
    /// changed and is visible (prompts are often written while still hidden). Queued, so a prompt
    /// never cuts off the menu item or dialogue before it.
    /// When a component is hidden again, its text is forgotten, so the same prompt shown later
    /// ("Save completed.") is spoken again.
    ///
    /// Paths: probe logs of 30 Sep 2026, docs/class-index.md.
    /// </summary>
    internal static class AnnouncementReader
    {
        private const string Tag = "[Announce]";
        private const long SettleMs = 80;
        private const long WaitForVisibleMs = 10_000;
        private const long VisibilityPollMs = 250;

        private static readonly HashSet<string> Paths = new HashSet<string>
        {
            // Save and load: "Checking data...", "Load File 1?", "Load completed.", "Save completed.",
            // "There is already a save file. Previous data will be overwritten. Will you overwrite the file?"
            "Canvas_UI/saveUI(Clone)/saveannounce/saveannounce_tex/Text_announce01TM",
            // "Select a save location.", "Select a file to load."
            "Canvas_UI/saveUI(Clone)/savelist/savelist_data/Text_announceTM",
            // "The input device has been disconnected." / "Please reconnect the device."
            "Canvas_UI/DisconnectCaution/saveannounce_tex/Text_announce01TM",
            "Canvas_UI/DisconnectCaution/saveannounce_tex/Text_announce02TM",
            // Questions of the title and config Yes/No prompts (MenuReader reads their options):
            // "> End the game?", the difficulty confirmation, "Returning without changing settings. Will you proceed?",
            // and the config notice "Settings have been changed." (16:49 session).
            "Canvas_UI/titleUI(Clone)/titlemain/namechoice/talk_window/Text01TM",
            "Canvas_UI/titleUI(Clone)/titlemode/namechoice/talk_window/Text01TM",
            "Canvas_UI/configUI_steam(Clone)/talk_window/Text01TM",
            // Location name, written when arriving: "Underground Facility", "Terminal" (16:49 session).
            "Canvas_UI/fieldUI/flocation/stretch/TextTM",
        };

        private sealed class Entry
        {
            public TMP_Text Component;
            public string Text = "";
            public string Spoken = "";
            public long ChangedAt;
            public bool Pending;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _lastPoll;

        /// <summary>
        /// A prompt has new text that is not spoken yet (Systems/SpeechOrder.cs). Only recent changes
        /// count: a prompt written while hidden can stay pending for WaitForVisibleMs.
        /// </summary>
        internal static bool AnyDue
        {
            get
            {
                long now = Clock.ElapsedMilliseconds;
                foreach (Entry e in Entries)
                {
                    if (e.Pending && now - e.ChangedAt < SpeechOrder.MaxHoldMs)
                        return true;
                }
                return false;
            }
        }

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            TmpRouter.Register("AnnouncementReader", Classify);
        }

        private static TmpRouter.Handler Classify(string path)
        {
            if (!Paths.Contains(path))
                return null;

            var entry = new Entry();
            Entries.Add(entry);
            return (component, raw) =>
            {
                string text = TextUtil.Clean(raw);
                entry.Component = component;
                if (text == entry.Text)
                    return;
                entry.Text = text;
                entry.ChangedAt = Clock.ElapsedMilliseconds;
                entry.Pending = text.Length > 0 && text != entry.Spoken;
            };
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            long now = Clock.ElapsedMilliseconds;
            bool poll = now - _lastPoll >= VisibilityPollMs;
            if (poll)
                _lastPoll = now;

            foreach (Entry e in Entries)
            {
                try
                {
                    if (e.Pending && now - e.ChangedAt >= SettleMs)
                    {
                        if (IsVisible(e.Component))
                        {
                            e.Pending = false;
                            e.Spoken = e.Text;
                            SpeechManager.Say(e.Text, interrupt: false, force: true);
                        }
                        else if (now - e.ChangedAt > WaitForVisibleMs)
                        {
                            e.Pending = false;
                        }
                    }
                    else if (poll && e.Spoken.Length > 0 && !IsVisible(e.Component))
                    {
                        // Hidden again: forget it, so the same prompt written later counts as new.
                        e.Spoken = "";
                        e.Text = "";
                    }
                }
                catch (Exception ex)
                {
                    e.Pending = false;
                    Log.Error($"{Tag} {ex.Message}");
                }
            }
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
