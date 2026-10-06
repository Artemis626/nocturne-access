using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// Speaks the highlighted slot of the save/load file list: "File 1. AAAA, Level 1, Shinjuku Medical
    /// Center, NORMAL" or "File 2. No File".
    /// The title screen's Continue screen (continueUI(Clone), one slot, the suspend data, no file number) is the
    /// same saveUI: slMain.SetupUI loads one of two prefabs by slMain.gslMode (5 = Continue) and stores either's
    /// saveUI in slMain._saveUI (read offline in slMain.SetupUI, RVA 0x2312BA0, 2 Oct 2026). Before 0.0.40 its slot
    /// was silent (21:03 session); now it reads like a load slot without the file number: "Art, Level 8, Yoyogi
    /// Park-West Plaza, NORMAL" (the level when the screen shows it).
    ///
    /// Sources (docs/class-index.md, "Save and load" and the highlight list):
    /// - Trigger: the highlighted slot's texts are rewritten with material TMC01 (16:18 session). Every
    ///   empty slot has the same path (savefile(Clone)/Text_newTM), so the path cannot tell slots apart.
    /// - Identity: the text's nearest parent that is one of slMain._saveUI._fileList (one saveFileUI per
    ///   slot, on the slot's root object).
    /// - Numbers are sprite digits, so they come from the slot's CounterCtr.value: the file number from
    ///   _fileNum or _noDataNum (whichever is shown), the level from _playerLV.
    /// - Texts are the slot's own components, read as the game wrote them.
    ///
    /// Like MenuReader: announced 80 ms after the last change, once visible; identical rewrites never
    /// count as a change; the first slot after opening is queued behind the prompt ("Select a file to
    /// load.", Systems/SpeechOrder.cs), later moves interrupt. Each announcement logs the raw numbers
    /// (file number, level, play time, slMain.gslSelect) so the mapping can be checked against the screen.
    /// </summary>
    internal static class SaveListReader
    {
        private const string Tag = "[SaveList]";
        private const string Highlight = "<material=\"TMC01\">";
        private const long SettleMs = 80;
        private const long WaitForVisibleMs = 10_000;
        private const long VisibilityPollMs = 250;
        private const int MaxOwnerDepth = 4;

        private static readonly Regex SlotText = new Regex(
            @"^Canvas_UI/(?:saveUI|continueUI)\(Clone\)/savelist/savelist_data/savefile_mask/savefile/savefile(?:\(Clone\))?/(?:savefile_data/)?Text_(?:name|location|difficulty|new)TM$",
            RegexOptions.Compiled);

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Dictionary<int, int> Owners = new Dictionary<int, int>();          // component ID -> slot index
        private static readonly Dictionary<int, string> Highlighted = new Dictionary<int, string>(); // component ID -> last highlighted text
        private const int MaxRemembered = 2000;

        private static int _current = -1;
        private static TMP_Text _currentComponent;
        private static int _pending = -1;
        private static TMP_Text _pendingComponent;
        private static long _pendingSince;
        private static long _lastChange;
        private static long _holdSince = -1;
        private static long _lastPoll;
        private static int _lookupFailures;

        /// <summary>The last slot announcement, for the repeat key.</summary>
        internal static string LastUtterance { get; private set; } = "";
        internal static long LastUtteranceAt { get; private set; } = long.MinValue;   // Environment.TickCount64

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            TmpRouter.Register("SaveListReader", path => SlotText.IsMatch(path) ? OnSlotText : null);
        }

        private static void OnSlotText(TMP_Text component, string raw)
        {
            if (!raw.Contains(Highlight))
                return;
            string text = TextUtil.Clean(raw);
            if (text.Length == 0)
                return;

            int id = component.GetInstanceID();
            // Every save screen is a new copy with new IDs: start over now and then rather than keep them all session (0.0.56).
            if (Highlighted.Count > MaxRemembered || Owners.Count > MaxRemembered)
            {
                Highlighted.Clear();
                Owners.Clear();
            }
            bool changed = !Highlighted.TryGetValue(id, out string last) || last != text;
            Highlighted[id] = text;

            int slot = SlotOf(component, id);
            if (slot < 0)
                return;
            // Only a new slot or new text counts: identical rewrites must never restart the settle timer.
            if (!changed && (slot == _pending || (_pending < 0 && slot == _current)))
                return;

            long now = Clock.ElapsedMilliseconds;
            if (_pending < 0)
                _pendingSince = now;
            _pending = slot;
            _pendingComponent = component;
            _lastChange = now;
        }

        /// <summary>Index in slMain._saveUI._fileList of the slot owning this text, or -1.</summary>
        private static int SlotOf(TMP_Text component, int id)
        {
            if (Owners.TryGetValue(id, out int cached))
                return cached;

            Il2CppReferenceArray<saveFileUI> files = slMain._saveUI?._fileList;
            if (files != null)
            {
                // The nearest parent that is a slot's own object (saveFileUI sits on the slot root).
                Transform t = component.transform;
                for (int depth = 0; t != null && depth < MaxOwnerDepth; depth++, t = t.parent)
                {
                    IntPtr candidate = t.Pointer;
                    for (int i = 0; i < files.Length; i++)
                    {
                        saveFileUI f = files[i];
                        if (f != null && f.transform.Pointer == candidate)
                        {
                            Owners[id] = i;
                            return i;
                        }
                    }
                }
            }

            if (_lookupFailures++ < 5)
                Log.Warn($"{Tag} No slot owns {UiUtil.PathOf(component.transform)} ({(files == null ? "no file list" : files.Length + " slots")}).");
            return -1;
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            long now = Clock.ElapsedMilliseconds;
            try
            {
                if (_pending >= 0 && now - _lastChange >= SettleMs)
                {
                    TryAnnounce(now);
                }
                else if (now - _lastPoll >= VisibilityPollMs)
                {
                    _lastPoll = now;
                    if (_current >= 0 && _pending < 0 && !UiUtil.IsVisible(_currentComponent))
                    {
                        _current = -1;
                        _currentComponent = null;
                        Log.Info($"{Tag} Closed.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"{Tag} {ex.Message}");
                _pending = -1;
            }
        }

        private static void TryAnnounce(long now)
        {
            // The list is built while hidden: wait until it is shown.
            if (!UiUtil.IsVisible(_pendingComponent))
            {
                if (now - _pendingSince > WaitForVisibleMs)
                    _pending = -1;
                return;
            }

            bool opening = _current < 0;
            if (opening && SpeechOrder.Hold("Save list", ref _holdSince, now))
                return;
            _holdSince = -1;

            Il2CppReferenceArray<saveFileUI> files = slMain._saveUI?._fileList;
            if (files == null || _pending >= files.Length)
            {
                _pending = -1;
                return;
            }

            string utterance = Describe(files[_pending], _pending, _pendingComponent);
            _current = _pending;
            _currentComponent = _pendingComponent;
            _pending = -1;
            if (utterance.Length == 0)
                return;

            LastUtterance = utterance;
            LastUtteranceAt = Environment.TickCount64;
            if (opening)
                Log.Info($"{Tag} Opened.");
            SpeechManager.Say(utterance, interrupt: !opening, force: true);
        }

        /// <param name="trigger">The highlighted text that started this announcement; spoken if nothing else is found.</param>
        private static string Describe(saveFileUI file, int index, TMP_Text trigger)
        {
            int? fileNum = Shown(file._fileNum);
            int? noDataNum = Shown(file._noDataNum);
            int? level = Shown(file._playerLV);
            int? number = fileNum ?? noDataNum;

            Log.Info($"{Tag} Slot {index}: fileNum {Raw(file._fileNum)}, noDataNum {Raw(file._noDataNum)}, level {Raw(file._playerLV)}, "
                     + $"play time [{PlayTime(file)}], gslSelect {slMain.gslSelect}, gslSelTop {slMain.gslSelTop}.");

            var parts = new List<string>();
            string empty = ShownText(file._noDataText);
            if (empty.Length > 0)
            {
                parts.Add(empty);
            }
            else
            {
                AddIf(parts, ShownText(file._playerName));
                if (level.HasValue)
                    parts.Add($"Level {level.Value}");
                AddIf(parts, ShownText(file._terminalName));
                AddIf(parts, ShownText(file._difficulty));
            }
            if (parts.Count == 0)
            {
                Log.Warn($"{Tag} Slot {index}: no known text shown, using {UiUtil.PathOf(trigger.transform)}.");
                AddIf(parts, ShownText(trigger));
            }

            string details = string.Join(", ", parts);
            return number.HasValue ? $"File {number.Value}. {details}" : details;
        }

        private static void AddIf(List<string> parts, string text)
        {
            if (text.Length > 0)
                parts.Add(text);
        }

        /// <summary>A number the screen is showing: the CounterCtr's value when its object is visible.</summary>
        private static int? Shown(CounterCtr counter) => UiUtil.IsVisible(counter) ? counter.value : (int?)null;

        private static string ShownText(TMP_Text component) => UiUtil.IsVisible(component) ? TextUtil.Clean(component.text) : "";

        private static string Raw(CounterCtr counter)
        {
            if (counter == null)
                return "none";
            return $"{counter.value}{(UiUtil.IsVisible(counter) ? "" : " hidden")}{(counter.blank ? " blank" : "")}";
        }

        private static string PlayTime(saveFileUI file)
        {
            Il2CppReferenceArray<CounterCtr> parts = file._playTime;
            if (parts == null)
                return "none";
            var values = new List<string>();
            for (int i = 0; i < parts.Length; i++)
                values.Add(Raw(parts[i]));
            return string.Join(", ", values);
        }
    }
}
