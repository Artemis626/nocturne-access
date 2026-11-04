using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2Cppnewbattle_H;
using Il2Cppnewdata_H;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Battle
{
    // BattleReader: the banner (actions, results, targets) and the target under the cursor.
    internal static partial class BattleReader
    {
        // ---- Banner ----

        private static void AfterDispText(string __0, string __1, int __2)
        {
            if (__1 != null && Clock.ElapsedMilliseconds - _actionAt < ActionTailMs)
                return;   // the target's help drawn on past the confirm: not a new banner (0.0.133)
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
            _actionActor = ActiveForm(d);
            _actionHpCost = 0;
            // The pick is over. 0.0.128 forgot the target here, but the game draws the target's help a few frames past the confirm,
            // so the target was said again over the action (Francesco, 29 Oct 2026: "Corpus Sakahagi, HP 1486 of 1600" at 21:26:30.722,
            // again at .873 with "Attack", then the damage). Help drawn just after an action is now ignored (AfterDispText), so the
            // banner counts as gone and the next unit's pick of the same enemy is still said (0.0.133).
            _actionAt = Clock.ElapsedMilliseconds;
            if (_actionActor < 0)
                return;
            if (_actionActor < FirstEnemy)
                Menus.MenuReader.PartyActed();
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
            if (_pendingIsTarget && !_started)
            {
                _pendingBanner = null;   // a target before the start is announced, as in Tick
                return;
            }
            if (!UiUtil.IsVisible(_bannerComponent))
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

        /// <summary>
        /// While the target cursor is shown (nbDispText with a name is being called), reads the target from
        /// nbTarSelProcess.GetTarSelProcessData().nowform and speaks it when it changes: "Foul Will o' Wisp 2,
        /// HP 10 of 10". Identical enemies give identical banners, so the banner alone cannot tell them apart.
        /// Group targets (ctype not 0) are left to the banner.
        /// </summary>
        private static void AfterTarSel() => _tarSelAt = Clock.ElapsedMilliseconds;

        // The cursor's process also runs for an instant as the party's side begins, with no pick on screen: "Art, HP 415 of 429"
        // before every first turn line (27 Oct 2026, 16:00 session: five times, 9 ms after the side changed). Calls that close to
        // the side change are not a pick (0.0.125).
        private const long SideStartTarSelMs = 1000;
        private static long _sideAt = long.MinValue / 2;
        private static bool _lastTargetByBanner;   // the last target said came with the game's help banner
        private static long _actionAt = long.MinValue / 2;   // the last action banner (0.0.133)
        private const long ActionTailMs = 500;              // help drawn this soon after an action belongs to the pick just made

        private static void PollTarget(long now)
        {
            // The help banner, or the cursor's process on the party's turn when the game draws no banner (0.0.119: a dead ally).
            bool pick = _sideKnown && !_sideEnemy && Math.Abs(_tarSelAt - _sideAt) >= SideStartTarSelMs;
            long seen = Math.Max(_targetCallAt, pick ? _tarSelAt : long.MinValue / 2);
            // The cursor's process runs on through the party's side, so its window alone never closes between two units' picks: a
            // target said with its banner is forgotten once the banner goes, or the next unit's pick of the same enemy was silent,
            // its banner held back as "already said" (Francesco, 28 Oct 2026, 21:04:49: Setanta after Okuninushi, both on Troll 1;
            // 0.0.128).
            if (_lastTargetByBanner && now - _targetCallAt > TargetWindowMs)
            {
                _lastTargetForm = int.MinValue;
                _targetFromData = false;
                _lastTargetByBanner = false;
            }
            if (now - seen > TargetWindowMs)
            {
                _lastTargetForm = int.MinValue;
                _targetFromData = false;
                return;
            }

            // The game's target data is gone while a battle closes (0.0.123: since 0.0.119 the cursor's window outlived it, and the
            // call threw a NullReferenceException that skipped the rest of the battle update, five times a session).
            nbTarSelProcessData_t t;
            int form;
            try
            {
                t = nbTarSelProcess.GetTarSelProcessData();
                // ctype not 0: every target at once. The game shows no pick for it: its data turns on (stat 1) for about 100 ms
                // before the action (logged 0.0.149, 3 and 4 Nov 2026: Attack All, Mazan); the skill help already says "all foes".
                if (t == null || t.ctype != 0)
                {
                    _targetFromData = false;
                    return;
                }
                form = t.nowform;
            }
            catch (Exception)
            {
                _targetFromData = false;
                _tarSelAt = long.MinValue / 2;
                return;
            }
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

            // Without a help banner only a dead ally is a pick (the game draws no banner for it, 0.0.119). The cursor's process also
            // runs as each party turn opens and through auto battle, on living units: "Art, HP 435 of 435" and, after a summon,
            // "Succubus, HP 306 of 306" cut the turn line and the enemy list (Francesco, 28 Oct 2026: 18 times in one session; 0.0.126).
            // A dead enemy is no pick either: the process runs on it as the last enemy falls ("Naga 2, HP 0 of 264" before the
            // results, 28 Oct 2026, 16:28:45 and 16:00:43; 0.0.127).
            if (now - _targetCallAt > TargetWindowMs && (u.Hp > 0 || form >= FirstEnemy))
            {
                _targetFromData = false;
                _lastTargetForm = int.MinValue;   // not said, so not remembered: the next pick of this unit is new (0.0.128)
                return;
            }
            _targetFromData = true;
            _lastTargetByBanner = now - _targetCallAt <= TargetWindowMs;
            string race = _targetName == u.Name && _targetRace.Length > 0 ? _targetRace + " " : "";
            Log.Info($"{Tag} Target formindex {form} ({u.Name}){(now - _targetCallAt > TargetWindowMs ? ", no help banner: from the cursor's process" : "")}.");
            SpeechManager.Say($"{race}{DisplayName(u)}, HP {u.Hp} of {u.MaxHp}{StatusOf(u)}", interrupt: true, force: true);
        }
    }
}
