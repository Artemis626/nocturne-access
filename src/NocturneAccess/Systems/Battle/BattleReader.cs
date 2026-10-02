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
    /// - Target under the cursor: nbTarSelProcess.GetTarSelProcessData().nowform (formindex) while target help
    ///   is being shown, spoken with the unit's number and HP; identical enemies have identical banners.
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
        private const long ResumeWindowMs = 20_000;   // the party panel hides for about 5 s while an enemy talks
        private const long TargetWindowMs = 300;      // nbDispText is called continuously while targeting
        private const int PartySlots = 4;
        private const int FirstEnemy = 4;
        private const int MaxEnemies = 8;

        private sealed class Unit
        {
            public int Form;
            public ushort Id;
            public int Hp, MaxHp, Mp, MaxMp;
            public string Name = "";
            public int Number;   // among units of the same name and side, fixed for the battle
        }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Dictionary<int, Unit> Units = new Dictionary<int, Unit>();
        private static readonly Dictionary<string, int> NameCounts = new Dictionary<string, int>();   // side+name -> units seen this battle

        private static TMP_Text _partyPanel;
        private static readonly string[] PanelNames = new string[PartySlots];   // names on the party panel, by slot
        private static readonly TMP_Text[] PanelComponents = new TMP_Text[PartySlots];
        private static bool _active;
        private static bool _started;
        private static bool _seenVisible;   // the party panel is written while hidden during the encounter intro
        private static long _hiddenSince = -1;
        private static long _goneAt = -1;
        private static bool _resultsShown;
        private static long _activeSince;
        private static long _lastDataPoll;
        private static long _lastVisibilityPoll;
        private static string _lastData = "";
        private static int _errors;
        private static int _battleNo;
        private static string _lastTurnKey = "";
        private static int _enemySlots;   // enemy formindex slots in use this battle: survivors keep their slot when others fall

        // Banner
        private static bool _bannerRequested;
        private static bool _bannerIsTarget;
        private static string _lastBanner = "";
        private static string _pendingBanner;
        private static bool _pendingIsTarget;
        private static TMP_Text _bannerComponent;
        private static long _pendingSince;

        // Targeting
        private static long _targetCallAt = long.MinValue;
        private static string _targetRace = "";
        private static string _targetName = "";
        private static int _lastTargetForm = int.MinValue;
        private static bool _targetFromData;

        // Actions, sides and press turns
        private static bool _bannerIsAction, _pendingIsAction;
        private static int _actionActor = -1, _pendingActor = -1, _actionHpCost;
        private static bool _sideKnown, _sideEnemy, _playerTurnUnsaid;
        private static int _pressActor = -99, _pressTurn = -1, _pressP, _pressTen;

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

        private static void AfterDispText(string __0, string __1, int __2)
        {
            _bannerRequested = true;
            _bannerIsTarget = __1 != null;
            // An action name: ("Feral Claw", null, 0, ...) or ("Attack", null, 0, ...); results are (null, null, 1, ...).
            _bannerIsAction = __1 == null && __0 != null && __2 == 0;
            if (_bannerIsAction)
                NoteAction();
            if (__1 != null)
            {
                // Target help: text1 is the race, text2 the name. Called continuously while the cursor is shown.
                _targetCallAt = Clock.ElapsedMilliseconds;
                _targetRace = TextUtil.Clean(__0);
                _targetName = TextUtil.Clean(__1);
            }
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
            _pendingIsAction = _bannerIsAction;
            _pendingActor = _actionActor;
            _pendingSince = Clock.ElapsedMilliseconds;
        }

        /// <summary>
        /// An action begins: remember who acts (activeunit) and the HP its skill costs, so the HP drop is called a cost,
        /// not damage. The skill itself is read from the actor's unit work when it lands (nowcommand 1 = skill, nowindex =
        /// skill id, nowtform = target: Analyze gave 1, 71, 4 in the 09:16 dump; nowcommand 0 = attack).
        /// </summary>
        private static void NoteAction()
        {
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            _actionActor = d?.activeunit ?? -1;
            _actionHpCost = 0;
            if (_actionActor < 0)
                return;
            datUnitWork_t w = nbMainProcess.nbGetUnitWorkFromFormindex(_actionActor);
            if (w != null && w.nowcommand == 1)
                _actionHpCost = BattleSkills.HpCost(w.nowindex, w);
        }

        private static string ActorName(int form)
            => Units.TryGetValue(form, out Unit u) ? DisplayName(u) : "";

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
                if (_targetFromData)
                    return;   // PollTarget already said this target, with its number and HP
                string hp = TargetHp(text);
                SpeechManager.Say(hp.Length > 0 ? $"{text}, {hp}" : text, interrupt: true, force: true);
            }
            else if (_pendingIsAction)
            {
                // Enemies act without a menu: say who ("Preta 2: Feral Claw"). The party's own choice needs no name.
                string actor = ActorName(_pendingActor);
                SpeechManager.Say(_pendingActor >= FirstEnemy && actor.Length > 0 ? $"{actor}: {text}" : text, interrupt: false, force: true);
                BattleLog.Add(actor.Length > 0 ? $"{actor}: {text}" : text);
            }
            else
            {
                SpeechManager.Say(text, interrupt: false, force: true);
                BattleLog.Add(text);
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

            long now = Clock.ElapsedMilliseconds;
            _active = true;
            _activeSince = now;
            _seenVisible = false;
            _hiddenSince = -1;

            // The panel hides while an enemy talks (20:44 session): the same battle, not a new one.
            if (_goneAt >= 0 && !_resultsShown && now - _goneAt < ResumeWindowMs)
            {
                Log.Info($"{Tag} Battle UI back.");
                return;
            }

            _started = false;
            _resultsShown = false;
            Units.Clear();
            _battleNo++;
            _lastTurnKey = "";
            _enemySlots = 0;
            _sideKnown = false;
            _playerTurnUnsaid = false;
            _pressActor = -99;
            _actionActor = _pendingActor = -1;
            BattleLog.NewBattle();
            NameCounts.Clear();
            _lastData = "";
            Log.Info($"{Tag} Battle UI up.");
        }

        private static void OnResult(TMP_Text component, string raw)
        {
            // The results text is rewritten every frame (20:44 session): once per battle.
            if (!_active || _resultsShown)
                return;
            _resultsShown = true;
            LogData(force: true);
            string text = "Battle results." + Spoils();
            Log.Info($"{Tag} Results screen: {text}");
            SpeechManager.Say(text, interrupt: false, force: true);
            BattleLog.Add(text);
        }

        /// <summary>
        /// " 9 EXP. 27 macca. Medicine." from the main process data at the results screen: exp and maka matched the screen
        /// (EXP 9, macca 27, 09:22 recording); items from item[] / itemcnt[] named by datItemName.Get (the results screen
        /// asked for item 2, "Medicine"), the count added when more than one. The raw arrays are logged to check.
        /// </summary>
        private static string Spoils()
        {
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            if (d == null)
                return "";
            var parts = new List<string>();
            if (d.exp > 0)
                parts.Add($"{d.exp} EXP.");
            if (d.maka > 0)
                parts.Add($"{d.maka} macca.");
            var items = d.item;
            var counts = d.itemcnt;
            if (items != null)
            {
                Log.Info($"{Tag} Results items: item [{string.Join(" ", items)}] itemcnt [{(counts == null ? "null" : string.Join(" ", counts))}].");
                for (int k = 0; k < items.Length; k++)
                {
                    if (items[k] == 0)
                        continue;
                    string name;
                    try
                    {
                        name = TextUtil.Clean(datItemName.Get(items[k]) ?? "");
                    }
                    catch
                    {
                        continue;
                    }
                    int count = counts != null && k < counts.Length ? counts[k] : 1;
                    if (name.Length > 0)
                        parts.Add(count > 1 ? $"{name}, {count}." : $"{name}.");
                }
            }
            return parts.Count == 0 ? "" : " " + string.Join(" ", parts);
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            if (!_active)
                return;
            long now = Clock.ElapsedMilliseconds;
            try
            {
                PollTarget(now);
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
                    nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
                    if (d != null)
                    {
                        ObserveSide(d);
                        ObservePress(d);
                    }
                    ReadUnits(speakChanges: Config.SpeakHpChanges.Value);
                    LogData(force: false);
                }
                BattleAnalyze.Tick();

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
                        _goneAt = now;
                        if (_resultsShown)
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

        /// <summary>
        /// While the target cursor is shown (nbDispText with a name is being called), reads the target from
        /// nbTarSelProcess.GetTarSelProcessData().nowform and speaks it when it changes: "Foul Will o' Wisp 2,
        /// HP 10 of 10". Identical enemies give identical banners, so the banner alone cannot tell them apart.
        /// Group targets (ctype not 0) are left to the banner.
        /// </summary>
        private static void PollTarget(long now)
        {
            if (now - _targetCallAt > TargetWindowMs)
            {
                _lastTargetForm = int.MinValue;
                _targetFromData = false;
                return;
            }

            nbTarSelProcessData_t t = nbTarSelProcess.GetTarSelProcessData();
            if (t == null || t.ctype != 0)
            {
                _targetFromData = false;
                return;
            }
            int form = t.nowform;
            if (form == _lastTargetForm)
                return;
            _lastTargetForm = form;

            if (!Units.TryGetValue(form, out Unit u))
                ReadUnits(speakChanges: false);
            if (!Units.TryGetValue(form, out u))
            {
                Log.Info($"{Tag} Target formindex {form}: no unit known, using the banner.");
                _targetFromData = false;
                return;
            }

            _targetFromData = true;
            string race = _targetName == u.Name && _targetRace.Length > 0 ? _targetRace + " " : "";
            Log.Info($"{Tag} Target formindex {form} ({u.Name}).");
            SpeechManager.Say($"{race}{DisplayName(u)}, HP {u.Hp} of {u.MaxHp}", interrupt: true, force: true);
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
            // Enemies keep their formindex when others fall while enemypcnt counts the living (22:14 session: with Pretas 1
            // and 2 dead, enemypcnt 1 and Preta 3 at formindex 6 was no longer read, so its target and damage had no HP).
            // Read every slot up to the highest one used this battle; a fallen enemy's slot returns null.
            int living = Math.Min(Math.Max((int)data.enemypcnt, 0), MaxEnemies);
            int found = 0;
            for (int k = 0; k < MaxEnemies; k++)
            {
                if (k >= _enemySlots && found >= living)
                    break;   // further slots only while fewer living enemies were found than enemypcnt says (a newcomer)
                if (ReadUnit(FirstEnemy + k, speakChanges, seen))
                {
                    found++;
                    _enemySlots = Math.Max(_enemySlots, k + 1);
                }
            }

            foreach (int gone in Units.Keys.Where(k => !seen.Contains(k)).ToList())
                Units.Remove(gone);
        }

        /// <summary>Reads one formindex; false when the slot is empty or fallen (null).</summary>
        private static bool ReadUnit(int form, bool speakChanges, HashSet<int> seen)
        {
            datUnitWork_t w = nbMainProcess.nbGetUnitWorkFromFormindex(form);
            if (w == null || w.maxhp == 0)
                return false;
            seen.Add(form);

            if (!Units.TryGetValue(form, out Unit u) || u.Id != w.id)
            {
                u = new Unit { Form = form, Id = w.id, Hp = w.hp, MaxHp = w.maxhp, Mp = w.mp, MaxMp = w.maxmp, Name = form < PartySlots ? PanelNames[form] : NameOf(w.id) };
                Number(u);
                Units[form] = u;
                return true;
            }

            int oldHp = u.Hp;
            u.Hp = w.hp;
            u.MaxHp = w.maxhp;
            u.Mp = w.mp;
            u.MaxMp = w.maxmp;
            if (speakChanges && u.Hp != oldHp)
            {
                // The amount, as the game's damage numbers show it ("7" over Pixie, 09:22 recording); an HP drop equal to
                // the acting unit's own skill cost is the cost (Lunge: 4 HP), said once.
                string who = DisplayName(u);
                string what;
                if (u.Hp < oldHp && form == _actionActor && _actionHpCost > 0 && oldHp - u.Hp == _actionHpCost)
                {
                    what = $"{_actionHpCost} HP cost";
                    _actionHpCost = 0;
                }
                else
                {
                    what = u.Hp < oldHp ? $"{oldHp - u.Hp} damage" : $"{u.Hp - oldHp} healed";
                }
                string text = u.Hp == 0 ? $"{who}: {what}, 0 HP." : $"{who}: {what}, HP {u.Hp} of {u.MaxHp}.";
                SpeechManager.Say(text, interrupt: false, force: true);
                BattleLog.Add(text);
            }
            return true;
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

        /// <summary>The unit's name, numbered when several units of this battle share it ("Will o' Wisp 2").</summary>
        private static string DisplayName(Unit u)
        {
            string name = u.Name.Length > 0 ? u.Name : (u.Form >= FirstEnemy ? "Enemy" : "Ally");
            // Numbered when the battle had several of this name; the number stays when one falls.
            return NameCounts.TryGetValue(NameKey(u), out int count) && count > 1 ? $"{name} {u.Number}" : name;
        }

        private static string NameKey(Unit u) => (u.Form >= FirstEnemy ? "e:" : "p:") + u.Name;

        private static void Number(Unit u)
        {
            string key = NameKey(u);
            NameCounts.TryGetValue(key, out int count);
            NameCounts[key] = ++count;
            u.Number = count;
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

        // ---- Whose turn ----

        /// <summary>
        /// "Pixie's turn", for the battle command menu's opening line (MenuReader OpeningPrefix), read from
        /// nbMainProcessData_t.activeunit at that moment: the formindex of the unit choosing its action (0 to 3 the
        /// party; it changed 50 ms before Pixie's menu opened, 22:14 session). Said once per unit and press-turn state,
        /// so backing out of a submenu does not repeat it, while a unit acting again after an extra press turn does.
        /// </summary>
        internal static string TurnAnnouncement()
        {
            if (!_active)
                return "";
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            if (d == null || d.activeunit < 0 || d.activeunit >= PartySlots)
                return "";
            ObserveSide(d);   // the menu opens 50 ms after activeunit changes, before the 200 ms poll sees it
            int form = d.activeunit;
            string key = $"{_battleNo}/{d.turn}/{form}/{d.press4_p}/{d.press4_ten}";
            if (key == _lastTurnKey)
                return "";
            _lastTurnKey = key;
            string name = !string.IsNullOrEmpty(PanelNames[form]) ? PanelNames[form]
                : Units.TryGetValue(form, out Unit u) ? u.Name : "";
            if (name.Length == 0)
                return "";
            Log.Info($"{Tag} Turn: formindex {form} ({name}), press4_p {d.press4_p}, press4_ten {d.press4_ten}.");
            string side = _playerTurnUnsaid ? "Player turn. " : "";
            _playerTurnUnsaid = false;
            return $"{side}{name}'s turn";
        }

        /// <summary>
        /// The side acting changed (activeunit 0 to 3 the party, 4 and up the enemies): the game shows "PLAYER TURN" or
        /// "ENEMY TURN" as images (09:22 recording). "Enemy turn." is spoken at once; "Player turn." opens the next party
        /// command menu ("Player turn. Art's turn. Attack, 1 of 4."). Each side's turn starts a new part of the battle log.
        /// </summary>
        private static void ObserveSide(nbMainProcessData_t d)
        {
            if (d.activeunit < 0)
                return;
            bool enemy = d.activeunit >= FirstEnemy;
            if (_sideKnown && enemy == _sideEnemy)
                return;
            _sideKnown = true;
            _sideEnemy = enemy;
            BattleLog.NewSide(enemy ? "Enemy turn" : "Player turn");
            Log.Info($"{Tag} Side: {(enemy ? "enemies" : "party")} (turn {d.turn}, activeunit {d.activeunit}).");
            if (enemy)
            {
                _playerTurnUnsaid = false;
                SpeechManager.Say("Enemy turn.", interrupt: false, force: true);
            }
            else
            {
                _playerTurnUnsaid = true;
            }
        }

        /// <summary>
        /// A hit that costs only half a press turn: while the same unit acts, a full icon becomes a half one (press4_p one
        /// less, press4_ten the same). Seen for Pixie's Zio on the Preta's Elec weakness (1/1 to 0 full of 1, 09:17) and
        /// for Art's critical hit on a Kodama (2/2 to 1 full of 2, 22:20 on 1 Oct). The cause is told from the actor's
        /// action and the target's affinity: weak to the action's element = "Weakness"; a physical or skill hit
        /// otherwise = "Critical"; anything else (Pass costs half a turn too) = no claim. Both sides play by these rules,
        /// so enemies' hits on the party are announced as well.
        /// </summary>
        private static void ObservePress(nbMainProcessData_t d)
        {
            int actor = d.activeunit;
            if (actor >= 0 && actor == _pressActor && d.turn == _pressTurn && _pressP > 0
                && d.press4_ten == _pressTen && d.press4_p == _pressP - 1)
                HalfTurn(actor);
            _pressActor = actor;
            _pressTurn = d.turn;
            _pressP = d.press4_p;
            _pressTen = d.press4_ten;
        }

        private static void HalfTurn(int actor)
        {
            datUnitWork_t w = nbMainProcess.nbGetUnitWorkFromFormindex(actor);
            if (w == null)
                return;
            int element = w.nowcommand == 0 ? BattleSkills.Physical : w.nowcommand == 1 ? BattleSkills.Element(w.nowindex) : -1;
            int target = w.nowtform;
            string text;
            if (element < 0)
            {
                text = "Half a turn used.";
                Log.Info($"{Tag} Half turn: actor {actor}, nowcommand {w.nowcommand}, nowindex {w.nowindex}: no element.");
            }
            else
            {
                bool weak = BattleSkills.IsWeak(target, element, out uint raw);
                string who = ActorName(target);
                text = weak ? (who.Length > 0 ? $"Weakness hit on {who}, only half a turn used." : "Weakness hit, only half a turn used.")
                            : "Critical hit, only half a turn used.";
                Log.Info($"{Tag} Half turn: actor {actor}, nowcommand {w.nowcommand}, nowindex {w.nowindex}, element {element}, target {target}, aisyo 0x{raw:X}: {(weak ? "weak" : "not weak")}.");
            }
            SpeechManager.Say(text, interrupt: false, force: true);
            BattleLog.Add(text);
        }

        /// <summary>"2 press turns left, 1 full and 1 half." (press4_ten all icons, press4_p the full ones; a blinking icon is half a turn).</summary>
        private static string PressTurns(nbMainProcessData_t d)
        {
            int total = Math.Max((int)d.press4_ten, 0);
            int full = Math.Min(Math.Max((int)d.press4_p, 0), total);
            int half = total - full;
            string left = total == 1 ? "1 press turn left" : $"{total} press turns left";
            if (half == 0)
                return left + ".";
            return $"{left}: {full} full and {half} half.";
        }

        // ---- Status key ----

        // Short sentences, each ending in a full stop, so the screen reader pauses between them (Francesco, 22:14 session:
        // "Press turns: 1 full, 1 blinking" ran together). Split in two keys (Francesco, 2 Oct 2026: one key gave too much
        // at once): F2 our side, F3 the enemies.

        /// <summary>F2: whose turn, the press turns left, then each party member's HP and MP.</summary>
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
                string side = d.activeunit >= FirstEnemy ? "Enemy turn. " : "";
                if (d.activeunit >= 0 && d.activeunit < PartySlots && !string.IsNullOrEmpty(PanelNames[d.activeunit]))
                    side = $"{PanelNames[d.activeunit]}'s turn. ";
                parts.Add(side + Capital(PressTurns(d)));
            }
            foreach (Unit u in Units.Values.Where(u => u.Form < FirstEnemy).OrderBy(u => u.Form))
                parts.Add($"{DisplayName(u)}: HP {u.Hp} of {u.MaxHp}, MP {u.Mp} of {u.MaxMp}.");
            LogData(force: true);
            SpeechManager.Say(string.Join(" ", parts), interrupt: true, force: true);
        }

        /// <summary>F3 in battle: each living enemy with HP, and its affinities once Analyze has shown them.</summary>
        internal static void SpeakEnemies()
        {
            if (!_active)
                return;
            ReadUnits(speakChanges: false);
            List<Unit> enemies = Units.Values.Where(u => u.Form >= FirstEnemy && u.Hp > 0).OrderBy(u => u.Form).ToList();
            var parts = new List<string> { enemies.Count == 1 ? "1 enemy." : $"{enemies.Count} enemies." };
            foreach (Unit u in enemies)
            {
                string affinities = BattleAnalyze.AffinitiesOf(u.Name);
                parts.Add($"{DisplayName(u)}: HP {u.Hp} of {u.MaxHp}." + (affinities.Length > 0 ? " " + affinities : ""));
            }
            SpeechManager.Say(string.Join(" ", parts), interrupt: true, force: true);
        }

        /// <summary>For the Analyze screen, whose level, HP and MP are images: "Level 4. HP 30 of 30. MP 24 of 24." for the
        /// enemy of that name (the one targeted when several share it), or "".</summary>
        internal static string UnitFigures(string name)
        {
            if (!_active)
                return "";
            List<Unit> matches = Units.Values.Where(u => u.Form >= FirstEnemy && u.Name == name).ToList();
            Unit unit = matches.Count == 1 ? matches[0] : null;
            if (unit == null && matches.Count > 1)
            {
                nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
                datUnitWork_t actor = d == null || d.activeunit < 0 ? null : nbMainProcess.nbGetUnitWorkFromFormindex(d.activeunit);
                if (actor != null)
                    unit = matches.FirstOrDefault(u => u.Form == actor.nowtform);
            }
            if (unit == null)
                return "";
            datUnitWork_t w = nbMainProcess.nbGetUnitWorkFromFormindex(unit.Form);
            string level = w != null ? $"Level {w.level}. " : "";
            return $"{level}HP {unit.Hp} of {unit.MaxHp}. MP {unit.Mp} of {unit.MaxMp}.";
        }

        private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

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
