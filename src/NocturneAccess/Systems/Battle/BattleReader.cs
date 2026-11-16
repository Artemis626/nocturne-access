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
    ///
    /// This file: install, start and end of a battle, Tick. The other parts: BattleReader.Banner (banner and targets),
    /// .Turns (sides, press turns, whose turn), .Units (unit data, names, data log), .Results (results, F2, F3).
    /// </summary>
    internal static partial class BattleReader
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
        private static int _battleField = int.MinValue;   // fldGb.fieldID when the battle began (217 in Shibuya, 202 on the world map)
        private static bool _leftBattleField;              // the field changed since: the battle is over
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
        // Halved, as elsewhere: now - long.MinValue overflows to a negative number, which read as a target call "just now"
        // in the first battle of a session (10:10 on 2 Oct: "Art, HP 48 of 48" before "Battle. Preta.").
        private static long _targetCallAt = long.MinValue / 2;
        private static long _tarSelAt = long.MinValue / 2;   // nbTarSelProcess1 last ran (0.0.119)
        private static string _targetRace = "";
        private static string _targetName = "";
        private static int _lastTargetForm = int.MinValue;
        private static bool _targetFromData;

        // Actions, sides and press turns
        private static bool _bannerIsAction, _pendingIsAction;
        private static int _actionActor = -1, _pendingActor = -1, _actionHpCost;
        private static int _halfTurnActor = -1;   // a half turn seen this poll, said after its HP lines (0.0.151)
        private static readonly System.Collections.Generic.Dictionary<int, long> LastHurtMs = new System.Collections.Generic.Dictionary<int, long>();   // form -> when its HP last dropped
        private const long HurtWindowMs = 1500;   // one action's damage lines span under a second (Mazionga, 21:47:55.376 to .980)
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
            // The target cursor's own process (0.0.119): it runs every frame while a target is picked, also where the game draws
            // no help banner (a dead ally under a Balm of Rising, 26 Oct 2026, 21:08:29 and 21:22:01: the pick was silent).
            Patcher.Postfix("Battle", typeof(nbTarSelProcess), nameof(nbTarSelProcess.nbTarSelProcess1), new[] { typeof(Il2Cppkernel_H.dds3ProcessID_t) },
                AccessTools.Method(typeof(BattleReader), nameof(AfterTarSel)));
            InstallWords();
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

            // The panel hides while an enemy talks (20:44 session): the same battle, not a new one. A long talk (recruiting
            // Kodama, 3 Oct 21:24, more than ResumeWindowMs) is still the same battle while the game's field has not changed
            // since it began: "Battle. Kodama." was announced again for the one enemy left.
            bool sameField = !_leftBattleField && _battleField != int.MinValue && CurrentField() == _battleField;
            if (!_resultsShown && ((_goneAt >= 0 && now - _goneAt < ResumeWindowMs) || sameField))
            {
                Log.Info($"{Tag} Battle UI back.");
                return;
            }

            _battleField = CurrentField();
            _leftBattleField = false;
            _started = false;
            _resultsShown = false;
            Units.Clear();
            LastHurtMs.Clear();
            _halfTurnActor = -1;
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

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            if (_battleField != int.MinValue && !_leftBattleField && CurrentField() != _battleField)
                _leftBattleField = true;
            if (!_active)
                return;
            long now = Clock.ElapsedMilliseconds;
            try
            {
                // Nothing can be targeted before the start is announced: a target seen while the battle is set up is dropped.
                if (_started)
                {
                    PollTarget(now);
                    PollStockPick();
                }
                else
                    _targetCallAt = long.MinValue / 2;
                if (!_started)
                {
                    _tarSelAt = long.MinValue / 2;
                    Words.Clear();
                }
                SpeakPendingBanner(now);
                SpeakWords(now);

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
                    if (_halfTurnActor >= 0)
                    {
                        int actor = _halfTurnActor;
                        _halfTurnActor = -1;
                        HalfTurn(actor);   // after this poll's HP lines, so every unit the action hurt is known (0.0.151)
                    }
                    LogData(force: false);
                }
                BattleAnalyze.Tick();

                if (now - _lastVisibilityPoll >= VisibilityPollMs)
                {
                    _lastVisibilityPoll = now;
                    bool visible = UiUtil.IsVisible(_partyPanel);
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

        private static int CurrentField()
        {
            try
            {
                return fldGlobal.fldGb?.fieldID ?? int.MinValue;
            }
            catch
            {
                return int.MinValue;
            }
        }

        private static void AnnounceStart()
        {
            // "Battle. 3 enemies: Phantom 1, Phantom 2, Phantom 3." with the numbers the damage lines use (0.0.125).
            string names = EnemyList();
            Log.Info($"{Tag} Start: {names}.");
            _joinedAt = -1;
            // No enemies: the battle screen coming back after a demon joined (or left) in a talk (3 Oct 16:45:13, 2 Oct 20:20:38),
            // the results follow; a bare "Battle." there was noise.
            if (names.Length > 0)
                SpeechManager.Say($"Battle. {names}.", interrupt: false, force: true);
        }
    }
}
