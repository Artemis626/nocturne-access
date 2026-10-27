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

        private static void PollTarget(long now)
        {
            // The help banner, or the cursor's process on the party's turn when the game draws no banner (0.0.119: a dead ally).
            long seen = Math.Max(_targetCallAt, _sideKnown && !_sideEnemy ? _tarSelAt : long.MinValue / 2);
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

            _targetFromData = true;
            string race = _targetName == u.Name && _targetRace.Length > 0 ? _targetRace + " " : "";
            Log.Info($"{Tag} Target formindex {form} ({u.Name}){(now - _targetCallAt > TargetWindowMs ? ", no help banner: from the cursor's process" : "")}.");
            SpeechManager.Say($"{race}{DisplayName(u)}, HP {u.Hp} of {u.MaxHp}", interrupt: true, force: true);
        }
    }
}
