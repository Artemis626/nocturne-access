using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2Cppnewbattle_H;
using Il2Cppnewdata_H;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Battle
{
    /// <summary>
    /// Battle, version 1: start with the enemies, the banner (actions, targets, results), HP changes,
    /// and an on-demand status key. The command menu itself is a MenuReader spec ("Battle menu").
    ///
    /// Text sources (20:07 session, docs/class-index.md "Battle"):
    /// - Banner: Canvas_UI/bannounce(Clone)/stretch/TextTM, filled by nbHelpProcess.nbDispText(text1, text2,
    ///   type, max, col, type_skill): "Attack", "Deathtouch" (actions), "Foul　Will o' Wisp" (the target
    ///   under the cursor: text1 race, text2 name), "Dodged the attack!" (results, text from the message
    ///   engine). A call with text2 set is a target: spoken interrupting, since the cursor moves. Other
    ///   banners are queued. The same text is spoken again only after a new nbDispText call.
    /// - Battle UI up: Canvas_UI/bparty(Clone)/bparty_window01/bparty_text01TM (the party panel, first
    ///   member's name). Results: Canvas_UI/resultUI(Clone)/brparty/brchara01/TextTM.
    ///
    /// Data (Proxy; meanings from MatthiewNocturne, MIT, which reads and writes the same fields):
    /// - nbMainProcess.nbGetMainProcessData() -> nbMainProcessData_t: press4_p (full Press Turns) and
    ///   press4_ten (all Press Turn icons) as used by ModernPressTurns; enemypcnt, playerpcnt, turn,
    ///   activeunit, phase, exp, maka (logged, meanings to verify).
    /// - nbMainProcess.nbGetUnitWorkFromFormindex(formindex) -> datUnitWork_t: id, hp, maxhp, mp, maxmp.
    ///   Formindex 0 to 3 is the party, 4 and up the enemies (InfiniteEnemyMp: "formindex >= 4").
    /// - Names: party members as shown on the battle party panel (bparty_window0N); enemies by
    ///   datDevilName.Get(id) (DisplayBuffs).
    /// Every value read is logged on change as [Battle:Data], to check against the screen.
    /// </summary>
    internal static class BattleReader
    {
        private const string Tag = "[Battle]";
        private const string DataTag = "[Battle:Data]";
        private const string BannerPath = "Canvas_UI/bannounce(Clone)/stretch/TextTM";
        private const string PartyPanelPrefix = "Canvas_UI/bparty(Clone)/bparty_window0";
        private const string PartyPanelSuffix = "/bparty_text01TM";
        private const string ResultPath = "Canvas_UI/resultUI(Clone)/brparty/brchara01/TextTM";
        private const long StartDelayMs = 1000;
        private const long DataPollMs = 200;
        private const long VisibilityPollMs = 500;
        private const long BannerWaitMs = 1000;
        private const long EndAfterHiddenMs = 3000;
        private const int PartySlots = 4;
        private const int FirstEnemy = 4;
        private const int MaxEnemies = 8;

        private sealed class Unit
        {
            public int Form;
            public ushort Id;
            public int Hp, MaxHp, Mp, MaxMp;
            public string Name = "";
        }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Dictionary<int, Unit> Units = new Dictionary<int, Unit>();

        private static TMP_Text _partyPanel;
        private static readonly string[] PanelNames = new string[PartySlots];   // names on the party panel, by slot
        private static readonly TMP_Text[] PanelComponents = new TMP_Text[PartySlots];
        private static bool _active;
        private static bool _started;
        private static bool _seenVisible;   // the party panel is written while hidden during the encounter intro
        private static long _hiddenSince = -1;
        private static long _activeSince;
        private static long _lastDataPoll;
        private static long _lastVisibilityPoll;
        private static string _lastData = "";
        private static int _errors;

        // Banner
        private static bool _bannerRequested;
        private static bool _bannerIsTarget;
        private static string _lastBanner = "";
        private static string _pendingBanner;
        private static bool _pendingIsTarget;
        private static TMP_Text _bannerComponent;
        private static long _pendingSince;

        internal static bool Active => _active;

        internal static void Install()
        {
            if (!Config.BattleEnabled.Value)
            {
                Log.Info($"{Tag} Off in the config.");
                return;
            }

            TmpRouter.Register("BattleReader", Classify);
            Type s = typeof(string), i = typeof(int);
            Patcher.Postfix("Battle", typeof(nbHelpProcess), "nbDispText", new[] { s, s, i, i, typeof(uint), typeof(bool) },
                AccessTools.Method(typeof(BattleReader), nameof(AfterDispText)));
        }

        private static TmpRouter.Handler Classify(string path)
        {
            if (path.Length == PartyPanelPrefix.Length + 1 + PartyPanelSuffix.Length
                && path.StartsWith(PartyPanelPrefix, StringComparison.Ordinal) && path.EndsWith(PartyPanelSuffix, StringComparison.Ordinal))
            {
                int slot = path[PartyPanelPrefix.Length] - '1';
                if (slot >= 0 && slot < PartySlots)
                    return (component, raw) => OnPartyPanel(slot, component, raw);
            }

            switch (path)
            {
                case BannerPath: return OnBanner;
                case ResultPath: return OnResult;
                default: return null;
            }
        }

        // ---- Banner ----

        private static void AfterDispText(string __1)
        {
            _bannerRequested = true;
            _bannerIsTarget = __1 != null;
        }

        private static void OnBanner(TMP_Text component, string raw)
        {
            string text = TextUtil.Clean(raw);
            if (text.Length == 0)
                return;
            // Identical rewrites are not new banners; a new nbDispText call is.
            if (!_bannerRequested && text == _lastBanner)
                return;
            _bannerRequested = false;
            _lastBanner = text;
            _bannerComponent = component;
            _pendingBanner = text;
            _pendingIsTarget = _bannerIsTarget;
            _pendingSince = Clock.ElapsedMilliseconds;
        }

        private static void SpeakPendingBanner(long now)
        {
            if (_pendingBanner == null)
                return;
            if (!IsVisible(_bannerComponent))
            {
                if (now - _pendingSince > BannerWaitMs)
                    _pendingBanner = null;
                return;
            }

            string text = _pendingBanner;
            _pendingBanner = null;
            if (_pendingIsTarget)
            {
                string hp = TargetHp(text);
                SpeechManager.Say(hp.Length > 0 ? $"{text}, {hp}" : text, interrupt: true, force: true);
            }
            else
            {
                SpeechManager.Say(text, interrupt: false, force: true);
            }
        }

        /// <summary>
        /// HP of the unit named in a target banner ("Foul Will o' Wisp" ends with the name), when exactly one
        /// unit has that name; with duplicates the banner alone is spoken until the target index is known.
        /// </summary>
        private static string TargetHp(string banner)
        {
            List<Unit> matches = Units.Values.Where(u => u.Name.Length > 0 && banner.EndsWith(u.Name, StringComparison.Ordinal)).ToList();
            if (matches.Count != 1)
                return "";
            Unit u = matches[0];
            return $"HP {u.Hp} of {u.MaxHp}";
        }

        // ---- Battle start and end ----

        private static void OnPartyPanel(int slot, TMP_Text component, string raw)
        {
            PanelNames[slot] = TextUtil.Clean(raw);
            PanelComponents[slot] = component;
            if (slot != 0)
                return;
            _partyPanel = component;
            if (_active)
                return;
            _active = true;
            _started = false;
            _activeSince = Clock.ElapsedMilliseconds;
            _seenVisible = false;
            _hiddenSince = -1;
            Units.Clear();
            _lastData = "";
            Log.Info($"{Tag} Battle UI up.");
        }

        private static void OnResult(TMP_Text component, string raw)
        {
            if (!_active)
                return;
            LogData(force: true);
            Log.Info($"{Tag} Results screen.");
            SpeechManager.Say("Battle results.", interrupt: false, force: true);
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            if (!_active)
                return;
            long now = Clock.ElapsedMilliseconds;
            try
            {
                SpeakPendingBanner(now);

                if (!_started && now - _activeSince >= StartDelayMs)
                {
                    _started = true;
                    ReadUnits(speakChanges: false);
                    LogData(force: true);
                    AnnounceStart();
                }
                else if (_started && now - _lastDataPoll >= DataPollMs)
                {
                    _lastDataPoll = now;
                    ReadUnits(speakChanges: Config.SpeakHpChanges.Value);
                    LogData(force: false);
                }

                if (now - _lastVisibilityPoll >= VisibilityPollMs)
                {
                    _lastVisibilityPoll = now;
                    bool visible = IsVisible(_partyPanel);
                    if (visible)
                    {
                        _seenVisible = true;
                        _hiddenSince = -1;
                    }
                    else if (_seenVisible && _hiddenSince < 0)
                    {
                        _hiddenSince = now;
                    }
                    else if (_seenVisible && now - _hiddenSince > EndAfterHiddenMs)
                    {
                        _active = false;
                        _pendingBanner = null;
                        Array.Clear(PanelNames, 0, PanelNames.Length);
                        Log.Info($"{Tag} Battle UI gone.");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"{Tag} {ex.Message}");
            }
        }

        private static void AnnounceStart()
        {
            List<Unit> enemies = Units.Values.Where(u => u.Form >= FirstEnemy).OrderBy(u => u.Form).ToList();
            string names = string.Join(", ", enemies.Select(u => u.Name).Where(n => n.Length > 0));
            Log.Info($"{Tag} Start: {enemies.Count} enemies: {names}.");
            SpeechManager.Say(names.Length > 0 ? $"Battle. {names}." : "Battle.", interrupt: false, force: true);
        }

        // ---- Data ----

        private static void ReadUnits(bool speakChanges)
        {
            nbMainProcessData_t data = nbMainProcess.nbGetMainProcessData();
            if (data == null)
                return;

            var seen = new HashSet<int>();
            // Party: only slots that show a name on the battle party panel (formindex = panel slot, to verify in the log).
            for (int form = 0; form < PartySlots; form++)
            {
                if (!string.IsNullOrEmpty(PanelNames[form]))
                    ReadUnit(form, speakChanges, seen);
            }
            int enemies = Math.Min(Math.Max((int)data.enemypcnt, 0), MaxEnemies);
            for (int k = 0; k < enemies; k++)
                ReadUnit(FirstEnemy + k, speakChanges, seen);

            foreach (int gone in Units.Keys.Where(k => !seen.Contains(k)).ToList())
                Units.Remove(gone);
        }

        private static void ReadUnit(int form, bool speakChanges, HashSet<int> seen)
        {
            datUnitWork_t w = nbMainProcess.nbGetUnitWorkFromFormindex(form);
            if (w == null || w.maxhp == 0)
                return;
            seen.Add(form);

            if (!Units.TryGetValue(form, out Unit u) || u.Id != w.id)
            {
                u = new Unit { Form = form, Id = w.id, Hp = w.hp, MaxHp = w.maxhp, Mp = w.mp, MaxMp = w.maxmp, Name = form < PartySlots ? PanelNames[form] : NameOf(w.id) };
                Units[form] = u;
                return;
            }

            int oldHp = u.Hp;
            u.Hp = w.hp;
            u.MaxHp = w.maxhp;
            u.Mp = w.mp;
            u.MaxMp = w.maxmp;
            if (speakChanges && u.Hp != oldHp)
            {
                string who = DisplayName(u);
                SpeechManager.Say(u.Hp == 0 ? $"{who}, 0 HP." : $"{who}, HP {u.Hp} of {u.MaxHp}.", interrupt: false, force: true);
            }
        }

        private static string NameOf(ushort id)
        {
            try
            {
                return TextUtil.Clean(datDevilName.Get(id));
            }
            catch
            {
                return "";
            }
        }

        /// <summary>The unit's name, numbered when several units share it ("Will o' Wisp 2").</summary>
        private static string DisplayName(Unit u)
        {
            List<Unit> same = Units.Values.Where(x => x.Name == u.Name && (x.Form >= FirstEnemy) == (u.Form >= FirstEnemy))
                                          .OrderBy(x => x.Form).ToList();
            string name = u.Name.Length > 0 ? u.Name : (u.Form >= FirstEnemy ? "Enemy" : "Ally");
            return same.Count > 1 ? $"{name} {same.IndexOf(u) + 1}" : name;
        }

        /// <summary>Logs the raw battle state when it changes, to check the field meanings against the screen.</summary>
        private static void LogData(bool force)
        {
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            if (d == null)
                return;
            string units = string.Join("; ", Units.Values.OrderBy(u => u.Form)
                .Select(u => $"{u.Form} {u.Name} id {u.Id} HP {u.Hp}/{u.MaxHp} MP {u.Mp}/{u.MaxMp}"));
            string line = $"turn {d.turn}, phase {d.phase}, activeunit {d.activeunit}, press4_p {d.press4_p}, press4_ten {d.press4_ten}, "
                          + $"hansya {d.press4_hansya}, miss {d.press4_miss}, playerpcnt {d.playerpcnt}, enemypcnt {d.enemypcnt}, "
                          + $"TarSelType {d.TarSelType}, exp {d.exp}, maka {d.maka} | {units}";
            if (!force && line == _lastData)
                return;
            _lastData = line;
            Log.Info($"{DataTag} {line}");
        }

        // ---- Status key ----

        /// <summary>F2: Press Turns, enemies, then the party, all from game data.</summary>
        internal static void SpeakStatus()
        {
            if (!_active)
            {
                SpeechManager.Say("Not in battle.", interrupt: true, force: true);
                return;
            }

            ReadUnits(speakChanges: false);
            var parts = new List<string>();
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            if (d != null)
            {
                int full = d.press4_p;
                int blinking = Math.Max(d.press4_ten - d.press4_p, 0);
                parts.Add(blinking > 0 ? $"Press turns: {full} full, {blinking} blinking" : $"Press turns: {full}");
            }
            foreach (Unit u in Units.Values.Where(u => u.Form >= FirstEnemy).OrderBy(u => u.Form))
                parts.Add($"{DisplayName(u)}, HP {u.Hp} of {u.MaxHp}");
            foreach (Unit u in Units.Values.Where(u => u.Form < FirstEnemy).OrderBy(u => u.Form))
                parts.Add($"{DisplayName(u)}, HP {u.Hp} of {u.MaxHp}, MP {u.Mp} of {u.MaxMp}");
            LogData(force: true);
            SpeechManager.Say(string.Join(". ", parts) + ".", interrupt: true, force: true);
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
