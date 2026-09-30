using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// The Stats screen and the level-up allocation (both statusUI).
    ///
    /// Text (20:07 and 20:44 sessions): name, race and the stat labels "Str".."Luc" are text; every number is
    /// a sprite display (CounterCtr) and the allocation cursor is an object: nothing is written while the
    /// player allocates points. So (docs/class-index.md "Battle", statusUI):
    /// - numbers: CounterCtr.value of the counter under statusUI.ObjLV, ObjNowHp, ObjMaxHp, ObjNowMp, ObjMaxMp,
    ///   ObjStatus[i] (one per stat) and ObjPoint (points left), the same source as the save list's numbers;
    /// - level-up mode: statusUI.lvUpBase is shown; the stat under the cursor is the label TextStatus[i] whose
    ///   row is nearest statusUI.stsCursor (vertical distance).
    /// Opening speaks a summary: "Artemis Soul, Fiend. Level 2. HP 34 of 34. MP 14 of 14. Str 3, Mag 2, ...".
    /// In level-up mode each cursor move says "Vit 4", a value change says the new value, and a change of
    /// the points left says "2 points left". The raw readings are logged once per opening, to check the
    /// mapping against the screen.
    /// </summary>
    internal static class StatusReader
    {
        private const string Tag = "[Status]";
        private const string AnchorPath = "Canvas_UI/campUIBase/statusUI(Clone)/sinfo_basic/sstatusname/Text_nameTM";
        private const long PollMs = 100;
        private const long StableMs = 150;      // name and race unchanged this long before the summary
        private const long MaxWaitMs = 600;     // speak anyway (a demon may show no race)
        private const int MaxParentDepth = 8;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static TMP_Text _anchor;
        private static statusUI _ui;
        private static long _lastPoll;
        private static bool _open;
        private static bool _levelUp;
        private static int _cursor = -1;
        private static int _cursorValue = int.MinValue;
        private static int _points = int.MinValue;
        private static int _errors;
        private static long _holdSince = -1;
        private static string _shown = "";      // "name|race" last spoken
        private static string _seen = "";       // "name|race" last read
        private static long _seenSince;
        private static long _waitSince = -1;

        internal static string LastUtterance { get; private set; } = "";
        internal static long LastUtteranceAt { get; private set; } = long.MinValue;   // Environment.TickCount64

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            TmpRouter.Register("StatusReader", path => path == AnchorPath ? OnAnchor : null);
        }

        private static void OnAnchor(TMP_Text component, string raw)
        {
            if (_anchor != null && _anchor.GetInstanceID() == component.GetInstanceID() && _ui != null)
                return;
            _anchor = component;
            _ui = FindStatusUI(component.transform);
            Log.Info($"{Tag} statusUI {(_ui != null ? "found" : "NOT found")} above the name text.");
        }

        private static statusUI FindStatusUI(Transform t)
        {
            for (int depth = 0; t != null && depth < MaxParentDepth; depth++, t = t.parent)
            {
                statusUI ui = t.GetComponent<statusUI>();
                if (ui != null)
                    return ui;
            }
            return null;
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            if (_ui == null)
                return;
            long now = Clock.ElapsedMilliseconds;
            if (now - _lastPoll < PollMs)
                return;
            _lastPoll = now;

            try
            {
                bool visible = IsVisible(_anchor);
                if (!visible)
                {
                    if (_open)
                        Log.Info($"{Tag} Closed.");
                    _open = false;
                    _levelUp = false;
                    _shown = _seen = "";
                    _waitSince = -1;
                    return;
                }

                bool levelUp = IsVisible(_ui.lvUpBase);

                // The race is written after the name (22:05 session: "Fiend" missing from the summary), and
                // switching character (L/R) rewrites both: wait until they have been stable a moment.
                string seen = Text(_ui.nameText) + "|" + Race();
                if (seen != _seen)
                {
                    _seen = seen;
                    _seenSince = now;
                }
                bool newCharacter = _open && seen != _shown && !_levelUp;
                if (!_open || newCharacter)
                {
                    if (_waitSince < 0)
                        _waitSince = now;
                    bool raceShown = !seen.EndsWith("|", StringComparison.Ordinal);
                    bool stable = now - _seenSince >= StableMs && raceShown;
                    if (!stable && now - _waitSince < MaxWaitMs)
                        return;
                    // At a level-up the screen opens just before "> You leveled up!": that page goes first.
                    if (!_open && SpeechOrder.Hold("Stats", ref _holdSince, now))
                        return;
                    _holdSince = -1;
                    _waitSince = -1;
                    _shown = seen;
                    if (!_open)
                    {
                        _open = true;
                        _levelUp = levelUp;
                        _cursor = CursorRow();   // the starting row is in the summary; only moves are spoken
                        _cursorValue = _cursor >= 0 ? StatValue(_cursor) ?? int.MinValue : int.MinValue;
                        _points = Number(_ui.ObjPoint) ?? int.MinValue;
                    }
                    LogRaw();
                    Say(Summary(), interrupt: newCharacter);
                    return;
                }
                else if (levelUp != _levelUp)
                {
                    _levelUp = levelUp;
                    _cursor = CursorRow();
                    _cursorValue = _cursor >= 0 ? StatValue(_cursor) ?? int.MinValue : int.MinValue;
                    Log.Info($"{Tag} Level-up mode {(levelUp ? "on" : "off")}.");
                }

                if (_levelUp)
                    PollLevelUp();
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"{Tag} {ex.Message}");
            }
        }

        private static void PollLevelUp()
        {
            int row = CursorRow();
            if (row < 0)
                return;
            int value = StatValue(row) ?? int.MinValue;
            int points = Number(_ui.ObjPoint) ?? int.MinValue;

            var parts = new List<string>();
            if (row != _cursor)
            {
                _cursor = row;
                _cursorValue = value;
                parts.Add(StatText(row, value));
            }
            else if (value != _cursorValue)
            {
                _cursorValue = value;
                parts.Add(StatText(row, value));
            }
            if (points != _points && points != int.MinValue)
            {
                _points = points;
                parts.Add(points == 1 ? "1 point left" : $"{points} points left");
            }
            if (parts.Count > 0)
                Say(string.Join(". ", parts), interrupt: true);
        }

        private static string StatText(int row, int value)
        {
            string label = Label(row);
            return value == int.MinValue ? label : $"{label} {value}";
        }

        /// <summary>The stat row nearest the cursor object, by vertical distance to the labels.</summary>
        private static int CursorRow()
        {
            GameObject cursor = _ui.stsCursor;
            Il2CppReferenceArray<TextMeshProUGUI> labels = _ui.TextStatus;
            if (cursor == null || labels == null || !cursor.activeInHierarchy)
                return -1;
            float y = cursor.transform.position.y;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < labels.Length; i++)
            {
                if (!IsVisible(labels[i]))
                    continue;
                float d = Math.Abs(labels[i].transform.position.y - y);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>"Artemis Soul, Fiend. Level 2. HP 34 of 34. MP 14 of 14. Str 3, Mag 2, Vit 3, Agi 3, Luc 3. 1 point left"</summary>
        private static string Summary()
        {
            var parts = new List<string>();
            string name = Text(_ui.nameText), race = Race();
            parts.Add(race.Length > 0 ? $"{name}, {race}" : name);
            int? level = Number(_ui.ObjLV);
            if (level.HasValue)
                parts.Add($"Level {level.Value}");
            AddPair(parts, "HP", Number(_ui.ObjNowHp), Number(_ui.ObjMaxHp));
            AddPair(parts, "MP", Number(_ui.ObjNowMp), Number(_ui.ObjMaxMp));

            var stats = new List<string>();
            Il2CppReferenceArray<TextMeshProUGUI> labels = _ui.TextStatus;
            for (int i = 0; labels != null && i < labels.Length; i++)
            {
                if (IsVisible(labels[i]))
                    stats.Add(StatText(i, StatValue(i) ?? int.MinValue));
            }
            if (stats.Count > 0)
                parts.Add(string.Join(", ", stats));
            if (_levelUp && _points != int.MinValue)
                parts.Add(_points == 1 ? "1 point left" : $"{_points} points left");
            return string.Join(". ", parts) + ".";
        }

        /// <summary>
        /// The race: raceText_e is the Western version's text (Text_raceTM_oubei, "Fiend"); raceText, read in
        /// the 22:05 session, gave nothing. Both fields: proxy statusUI.
        /// </summary>
        private static string Race()
        {
            string race = Text(_ui.raceText_e);
            return race.Length > 0 ? race : Text(_ui.raceText);
        }

        private static void AddPair(List<string> parts, string label, int? now, int? max)
        {
            if (now.HasValue && max.HasValue)
                parts.Add($"{label} {now.Value} of {max.Value}");
            else if (now.HasValue)
                parts.Add($"{label} {now.Value}");
        }

        private static string Label(int row)
        {
            Il2CppReferenceArray<TextMeshProUGUI> labels = _ui.TextStatus;
            return labels != null && row < labels.Length ? Text(labels[row]) : "";
        }

        private static int? StatValue(int row)
        {
            Il2CppReferenceArray<GameObject> objects = _ui.ObjStatus;
            return objects != null && row < objects.Length ? Number(objects[row]) : null;
        }

        /// <summary>The value of the first visible CounterCtr on or under the object.</summary>
        private static int? Number(GameObject obj)
        {
            if (obj == null || !obj.activeInHierarchy)
                return null;
            CounterCtr c = obj.GetComponentInChildren<CounterCtr>();
            return c != null ? c.value : (int?)null;
        }

        /// <summary>Logs every counter under each object once per opening, to check the mapping against the screen.</summary>
        private static void LogRaw()
        {
            var parts = new List<string>
            {
                "LV " + Counters(_ui.ObjLV),
                "HP " + Counters(_ui.ObjNowHp) + "/" + Counters(_ui.ObjMaxHp),
                "MP " + Counters(_ui.ObjNowMp) + "/" + Counters(_ui.ObjMaxMp),
                "points " + Counters(_ui.ObjPoint),
                "bonus " + Counters(_ui.ObjBonus),
                $"levelup {IsVisible(_ui.lvUpBase)}",
            };
            Il2CppReferenceArray<GameObject> objects = _ui.ObjStatus;
            for (int i = 0; objects != null && i < objects.Length; i++)
                parts.Add($"{Label(i)} {Counters(objects[i])}");
            Log.Info($"{Tag} Opened: {string.Join("; ", parts)}; cursor row {CursorRow()}.");
        }

        private static string Counters(GameObject obj)
        {
            if (obj == null)
                return "none";
            var values = new List<string>();
            foreach (CounterCtr c in obj.GetComponentsInChildren<CounterCtr>(true))
                values.Add(c.value.ToString(CultureInfo.InvariantCulture) + (c.gameObject.activeInHierarchy ? "" : " hidden"));
            return values.Count > 0 ? "[" + string.Join(", ", values) + "]" : "[no counter]";
        }

        private static string Text(TMP_Text component) => IsVisible(component) ? TextUtil.Clean(component.text) : "";

        private static void Say(string text, bool interrupt)
        {
            if (text.Length == 0)
                return;
            LastUtterance = text;
            LastUtteranceAt = Environment.TickCount64;
            SpeechManager.Say(text, interrupt: interrupt, force: true);
        }

        private static bool IsVisible(Component component)
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

        private static bool IsVisible(GameObject obj)
        {
            try
            {
                return obj != null && obj.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }
    }
}
