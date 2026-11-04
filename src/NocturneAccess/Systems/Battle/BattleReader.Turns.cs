using System;
using Il2Cpp;
using Il2Cppnewbattle_H;
using Il2Cppnewdata_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Battle
{
    // BattleReader: whose turn, the side acting, press turns and half-turn hits.
    internal static partial class BattleReader
    {
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
            string name = ActingName(d, form);   // the acting party entry's own unit (0.0.108), else the slot's (0.0.88)
            if (name.Length == 0 && Units.TryGetValue(form, out Unit u))
                name = u.Name;
            if (name.Length == 0)
                return "";
            Log.Info($"{Tag} Turn: formindex {form} ({name}), press4_p {d.press4_p}, press4_ten {d.press4_ten}.");
            string side = _playerTurnUnsaid ? "Player turn. " : "";
            _playerTurnUnsaid = false;
            return $"{side}{name}'s turn";
        }

        /// <summary>
        /// The name of the unit choosing its action (0.0.108): activeunit indexes nbMainProcessData_t.party, whose entry has its
        /// own formindex; until a summon both run 0 to 3 together. After one (21 Oct 2026, 20:19:33) activeunit 3 was read as
        /// form 3 and said "Mizuchi's turn" (the demon just summoned) while Succubus acted (her skills followed: Dormina,
        /// Analyze, Zionga). The entry's unit comes from nbGetUnitWorkPointer; both readings are logged when they differ.
        /// </summary>
        /// <summary>
        /// The formindex of the unit acting (0.0.150): activeunit indexes nbMainProcessData_t.party on both sides, and each entry has
        /// its own formindex. 0.0.108 read it so for the party's turn line only; the enemy's action banner, the skill's HP cost
        /// and the half-turn line still took activeunit as a formindex. Francesco, 4 Nov 2026 (Pale Rider, 15:28:57): after a
        /// Call Evil, "Loa 1: Last Resort" while Loa 3 (just called, 414 HP) was the one that died by it.
        /// </summary>
        private static int ActiveForm(nbMainProcessData_t d)
        {
            int unit = d?.activeunit ?? -1;
            if (unit < 0)
                return -1;
            try
            {
                nbParty_t p = d.party != null && unit < d.party.Length ? d.party[unit] : null;
                if (p == null || p.formindex < 0)
                    return unit;
                if (p.formindex != unit && unit != _formLoggedFor)
                {
                    _formLoggedFor = unit;
                    Log.Info($"{Tag} Acting unit: activeunit {unit} is formindex {p.formindex} ({ActorName(p.formindex)}).");
                }
                return p.formindex;
            }
            catch (Exception)
            {
                return unit;
            }
        }

        private static int _formLoggedFor = -1;

        private static string ActingName(nbMainProcessData_t d, int form)
        {
            string bySlot = PartyName(form);
            try
            {
                nbParty_t p = d.party != null && form < d.party.Length ? d.party[form] : null;
                datUnitWork_t w = p != null ? nbMainProcess.nbGetUnitWorkPointer(d, p) : null;
                string byEntry = w != null && w.id != 0 ? NameOf(w.id) : "";
                if (byEntry.Length == 0)
                    return bySlot;
                if (byEntry != bySlot || p.formindex != form)
                    Log.Info($"{Tag} Acting unit: activeunit {form} is party entry formindex {p.formindex} \"{byEntry}\" (id {w.id}); slot {form} holds \"{bySlot}\": the entry is said.");
                return byEntry;
            }
            catch (Exception e)
            {
                Log.Info($"{Tag} Acting unit: party entry {form} not read ({e.Message}); slot name said.");
                return bySlot;
            }
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
            _sideAt = Clock.ElapsedMilliseconds;
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
            int actor = ActiveForm(d);
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
    }
}
