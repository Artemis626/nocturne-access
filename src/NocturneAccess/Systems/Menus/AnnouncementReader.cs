using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Dialogue;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// Speaks one-off prompts and notices: each listed component's text is spoken once it has
    /// changed and is visible (prompts are often written while still hidden). Queued, so a prompt
    /// never cuts off the menu item or dialogue before it.
    /// When a component is hidden again, its text is forgotten, so the same prompt shown later
    /// ("Save completed.") is spoken again.
    ///
    /// Two options per path:
    /// - FollowsDialogue: waits, at most SpeechOrder.MaxHoldMs, while a dialogue page is due. The location
    ///   banner is written right after the elevator's "> Select a floor." is requested, 300 ms before that
    ///   question's lines (20:44 session), and was spoken first.
    /// - DropAfterQuestion: a short text (3 characters or fewer) that had to wait for a dialogue page is not spoken: the
    ///   elevator writes the current floor ("Ｂ1", "2Ｆ") as the location banner with "> Select a floor.", and spoken after the
    ///   question it sounded like a selectable option 6 s before the options open (Francesco, 21:31 session). The banner on
    ///   arriving at the new floor comes without a dialogue page and is spoken. Area names are longer and are spoken.
    /// - Reshow: spoken again every time the component reappears, even without a new write. The
    ///   interaction prompt ("Talk", "Open Door") is hidden when nothing is in reach and its text is only
    ///   written when the wording changes (20:44 session), so visibility is watched every 100 ms.
    ///
    /// Paths: probe logs of 30 Sep 2026, docs/class-index.md.
    /// </summary>
    internal static class AnnouncementReader
    {
        private const string Tag = "[Announce]";
        private const long SettleMs = 80;
        private const long WaitForVisibleMs = 10_000;
        private const long VisibilityPollMs = 250;
        private const long ReshowPollMs = 100;

        [Flags]
        private enum Options
        {
            None = 0,
            FollowsDialogue = 1,
            Reshow = 2,
            DropAfterQuestion = 4,
        }

        private static readonly Dictionary<string, Options> Paths = new Dictionary<string, Options>
        {
            // Save and load: "Checking data...", "Load File 1?", "Load completed.", "Save completed.",
            // "There is already a save file. Previous data will be overwritten. Will you overwrite the file?"
            ["Canvas_UI/saveUI(Clone)/saveannounce/saveannounce_tex/Text_announce01TM"] = Options.None,
            // "Select a save location.", "Select a file to load."
            ["Canvas_UI/saveUI(Clone)/savelist/savelist_data/Text_announceTM"] = Options.None,
            // "The input device has been disconnected." / "Please reconnect the device."
            ["Canvas_UI/DisconnectCaution/saveannounce_tex/Text_announce01TM"] = Options.None,
            ["Canvas_UI/DisconnectCaution/saveannounce_tex/Text_announce02TM"] = Options.None,
            // Questions of the title and config Yes/No prompts (MenuReader reads their options):
            // "> End the game?", the difficulty confirmation, "Returning without changing settings. Will you proceed?",
            // and the config notice "Settings have been changed." (16:49 session).
            ["Canvas_UI/titleUI(Clone)/titlemain/namechoice/talk_window/Text01TM"] = Options.None,
            ["Canvas_UI/titleUI(Clone)/titlemode/namechoice/talk_window/Text01TM"] = Options.None,
            ["Canvas_UI/configUI_steam(Clone)/talk_window/Text01TM"] = Options.None,
            // Name entry: "Enter a name.", "Enter a nickname." (20:44 session).
            ["Canvas_UI/nameUI_oubei(Clone)/nameinput/nameannounce/stretch/TextTM"] = Options.None,
            // Location name, written when arriving: "Underground Facility", "Terminal", "1Ｆ" (16:49 session).
            ["Canvas_UI/fieldUI/flocation/stretch/TextTM"] = Options.FollowsDialogue | Options.DropAfterQuestion,
            // Interaction prompt: "Talk", "Examine", "Open Door", "Enter", "Exit" (20:44 session).
            ["Canvas_UI/fieldUI/fannounce/guidetext/TextTM"] = Options.Reshow | Options.FollowsDialogue,
        };

        private sealed class Entry
        {
            public Options Options;
            public int Copy;   // which copy of the component owns the entry: the game re-creates some (every save screen)
            public TMP_Text Component;
            public string Text = "";
            public string Spoken = "";
            public long ChangedAt;
            public bool Pending;
            public bool WasVisible;
            public bool WaitedForDialogue;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static readonly Dictionary<string, Entry> EntryOfPath = new Dictionary<string, Entry>();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _lastPoll;
        private static long _lastReshowPoll;

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

        private const string LocationPath = "Canvas_UI/fieldUI/flocation/stretch/TextTM";
        private const string PromptPath = "Canvas_UI/fieldUI/fannounce/guidetext/TextTM";

        /// <summary>
        /// The location banner's last text (the game's own: the area in dungeons, "West Plaza"; the district on the world map,
        /// "Yoyogi Kamizonocho", rewritten as the marker crosses districts), or "" if it was never written. Where am I (0.0.44).
        /// </summary>
        internal static string LocationText
        {
            get
            {
                TMP_Text c = EntryOfPath.TryGetValue(LocationPath, out Entry e) ? e.Component : null;
                try
                {
                    return c == null ? "" : TextUtil.Clean(c.text);
                }
                catch
                {
                    return "";   // a destroyed copy
                }
            }
        }

        /// <summary>
        /// Whether the interaction prompt ("Talk", "Examine", "Open Door") is on screen; null while its component has never
        /// been written this session. AutoWalk's arrival at people and boxes (0.0.44): the game's hit index fires before the
        /// prompt (Shibuya, 3 Oct 15:17: the demon's hit at 232 units, "Talk" only at 172).
        /// </summary>
        internal static bool? InteractionPromptVisible
        {
            get
            {
                TMP_Text c = EntryOfPath.TryGetValue(PromptPath, out Entry e) ? e.Component : null;
                if (c == null)
                    return null;
                try
                {
                    return UiUtil.IsVisible(c) && TextUtil.Clean(c.text).Length > 0;
                }
                catch
                {
                    return null;
                }
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
            if (!Paths.TryGetValue(path, out Options options))
                return null;

            // One entry per path. A new copy of the component takes the path's entry over, starting as a new entry would,
            // and moves it to the end of the list, where a new entry would go; until 0.0.30 every copy added an entry
            // that stayed in the list all session.
            if (EntryOfPath.TryGetValue(path, out Entry entry))
            {
                entry.Copy++;
                entry.Component = null;
                entry.Text = "";
                entry.Spoken = "";
                entry.ChangedAt = 0;
                entry.Pending = false;
                entry.WasVisible = false;
                entry.WaitedForDialogue = false;
                Entries.Remove(entry);
            }
            else
            {
                entry = new Entry { Options = options };
                EntryOfPath[path] = entry;
            }
            Entries.Add(entry);
            int copy = entry.Copy;
            return (component, raw) =>
            {
                if (entry.Copy != copy)
                    return;   // an older copy, replaced by a newer one
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
            bool reshowPoll = now - _lastReshowPoll >= ReshowPollMs;
            if (reshowPoll)
                _lastReshowPoll = now;

            foreach (Entry e in Entries)
            {
                try
                {
                    if (reshowPoll && (e.Options & Options.Reshow) != 0)
                        CheckReshow(e, now);

                    if (e.Pending && now - e.ChangedAt >= SettleMs)
                    {
                        if (UiUtil.IsVisible(e.Component))
                        {
                            if ((e.Options & Options.FollowsDialogue) != 0 && DialogueReader.PageDue
                                && now - e.ChangedAt < SpeechOrder.MaxHoldMs)
                            {
                                e.WaitedForDialogue = true;
                                continue;   // let the dialogue page go first
                            }
                            bool waited = e.WaitedForDialogue;
                            e.WaitedForDialogue = false;
                            e.Pending = false;
                            e.Spoken = e.Text;
                            if (waited && (e.Options & Options.DropAfterQuestion) != 0 && e.Text.Trim().Length <= 3)
                            {
                                Log.Info($"{Tag} \"{e.Text}\" written with a dialogue page: the floor the elevator is on, not spoken.");
                                continue;
                            }
                            SpeechManager.Say(e.Text, interrupt: false, force: true);
                        }
                        else if (now - e.ChangedAt > WaitForVisibleMs)
                        {
                            e.Pending = false;
                        }
                    }
                    else if (poll && e.Spoken.Length > 0 && !UiUtil.IsVisible(e.Component))
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

        /// <summary>A Reshow component that has just become visible is spoken again with its current text.</summary>
        private static void CheckReshow(Entry e, long now)
        {
            bool visible = UiUtil.IsVisible(e.Component);
            if (visible && !e.WasVisible)
            {
                string text = TextUtil.Clean(e.Component.text);
                // Written and spoken just before this poll saw it appear (0.0.10: "Open Door" twice, 92 ms apart).
                if (text.Length > 0 && text != e.Spoken)
                {
                    e.Text = text;
                    e.ChangedAt = now;
                    e.Pending = true;
                }
            }
            else if (!visible && e.WasVisible)
            {
                e.Spoken = "";
                e.Text = "";
                e.Pending = false;
            }
            e.WasVisible = visible;
        }
    }
}
