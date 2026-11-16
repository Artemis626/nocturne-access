using System;
using System.Diagnostics;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Descriptive cues for the Labyrinth of Amala's Fourth Kalpa (field 44; 0.0.168, Francesco, 16 Nov 2026: no way on there, a
    /// few spoken descriptions instead). All read offline from the field's bundle (`research/gamedata/f044`) and his logs:
    ///
    /// - Cursed floors: the damage reaction (fldPlayer.fldPlayerAct 52) came only in areas 2 and 4 (11 Nov 21:29, 13 Nov 21:42
    ///   to 21:50, 44 steps); their floors carry no damage attribute (dump 13 Nov 21:46:36: Attr 0x0, 0x800, 0x1000 only), so the
    ///   curse is the area's. Once the Wicked King falls (flag 276) the doors lead to areas 3 and 5 instead, the same rooms
    ///   uncursed ("The curse has been lifted.", F044INF_L0078), so the area rule follows the game by itself.
    /// - The Twelve Meters of Eternity: the corridor in B1 (area 10), procedure 011_01eve_01, reads the moon
    ///   (evtCommand_GET_MOON, script command 0x25) and calls door row 64 at phase 0 (area 23, 01pos_04: the Wicked King's side),
    ///   61 at odd phases (area 11, Hell's Vault), 63 at phases 2 and 6 (area 20, Hell's Maze), else 62 (area 14, the beams).
    ///   Areas 11 to 26 have no auto-map record: the game hides the map there from everyone. Francesco chose areas 11 to 26 for
    ///   the challenge line, on arrival and on Shift+End (WayOn.Speak).
    /// - The secret: arriving in area 23 straight from area 10 is the new-moon walk through the corridor. A sighted player sees
    ///   the different place; a Soul in area 3 hints at it (F044INF_L0136), nothing else does.
    /// Battles (fields 200 to 299) are not area changes here. Each line waits until the player has control and is held against
    /// interrupts, as the plane shift line is.
    /// </summary>
    internal static class FourthKalpaCues
    {
        private const int Field = 44;
        private const int CorridorArea = 10, SecretArea = 23;
        private const int ChallengeFirst = 11, ChallengeLast = 26;
        private const int DamageFloorAct = 52;
        private const long PendingMs = 15000;
        private const int HoldMs = 4000;

        internal const string CursedWords = "Cursed floors ahead: damage on every step to the whole party.";
        internal const string ClearWords = "This area no longer features cursed floors.";
        internal const string SecretWords = "Secret area reached via warp.";
        internal const string ChallengeWords = "Twelve Meters of Eternity: a navigation challenge to solve on your own. No map is available, even for sighted players.";

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static int _field = int.MinValue, _area = int.MinValue;
        private static string _pending;
        private static bool _secret;
        private static long _pendingMs;
        private static int _damageLoggedArea = int.MinValue;

        // 0.0.169: area 24 too (Hell's Hall on the Wicked King's side; 16 damage steps in the 16 Nov 20:56 session); the twins
        // after flag 276 are 3, 5 and 25 (door rows 212 to 215, 222).
        private static bool Cursed(int field, int area) => field == Field && (area == 2 || area == 4 || area == 24);

        private const int KingFlag = 276;   // the door rows' gate to the uncursed twins: set with the Wicked King's defeat
        private const string LiftedId = "<F044INF_L0078>";   // "The curse has been lifted." (the area 8 Soul's line)
        internal const string ExitWords = "Twelve Meters of Eternity behind you: back in B1, beside the ladder down to B2.";
        private static int _kingFlag = -1;   // unknown until read in the field
        private static bool _exit;

        /// <summary>Areas 11 to 26 of the Fourth Kalpa, beyond the Twelve Meters of Eternity.</summary>
        internal static bool InChallenge(int field, int area) => field == Field && area >= ChallengeFirst && area <= ChallengeLast;

        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return;
            int field = gb.fieldID, area = gb.areaID;
            if (field < 0 || field >= 200)
                return;   // not a place (0.0.169): battles 2xx, story scenes 7xx (749: the Wicked King), facilities 8xx, the falling minigame 999
            long now = Clock.ElapsedMilliseconds;
            if (field == Field && FieldState.IsExploring)
                WatchKing(now);

            if (field != _field || area != _area)
            {
                OnChange(_field, _area, field, area, now);
                _field = field;
                _area = area;
            }

            if (_pending != null)
            {
                if (now - _pendingMs > PendingMs)
                {
                    Log.Info($"[Nav:Kalpa4] The player did not take control in time: \"{_pending}\" not said.");
                    _pending = null;
                    _secret = false;
                }
                else if (FieldState.IsExploring)
                {
                    if (_secret && Config.SecretSound.Value)
                        Earcons.Secret();
                    else if (_exit && Config.SecretSound.Value)
                        Earcons.SectionExit();
                    _exit = false;
                    Log.Info($"[Nav:Kalpa4] Said in area {area}, {now - _pendingMs} ms after the change.");
                    SpeechManager.SayHeld(_pending, HoldMs);
                    _pending = null;
                    _secret = false;
                }
            }

            if (field == Field && !Cursed(field, area) && area != _damageLoggedArea && FieldState.IsExploring
                && fldPlayer.fldPlayerAct == DamageFloorAct)
            {
                _damageLoggedArea = area;
                Log.Info($"[Nav:Kalpa4] A damage-floor reaction in area {area}, not listed as cursed: add it if it repeats.");
            }
        }

        private static void OnChange(int oldField, int oldArea, int field, int area, long now)
        {
            string line = null;
            bool secret = false;
            bool wasCursed = Cursed(oldField, oldArea), isCursed = Cursed(field, area);
            if (isCursed && !wasCursed)
                line = CursedWords;
            else if (wasCursed && !isCursed)
                line = ClearWords;

            if (InChallenge(field, area) && !InChallenge(oldField, oldArea))
            {
                if (oldField == Field && oldArea == CorridorArea && area == SecretArea)
                {
                    secret = true;
                    line = Join(line, SecretWords);
                }
                line = Join(line, ChallengeWords);
            }

            bool exit = false;
            if (field == Field && area == CorridorArea && InChallenge(oldField, oldArea))
            {
                // Every section's end warp lands in area 10 at 01pos_06, by the ladder down to B2 (area 11 01d_04, area 14
                // 01d_02, area 21 01d_16, area 23 01d_04; 16 Nov 22:16:54: Hell's Hall to B1, then the ladder 5 s later).
                exit = true;
                line = Join(line, ExitWords);
            }

            if (line == null)
                return;
            _exit = exit;
            Log.Info($"[Nav:Kalpa4] Field {oldField} area {oldArea} to field {field} area {area}: \"{line}\" once the player has control.");
            _pending = line;
            _secret = secret;
            _pendingMs = now;
        }

        /// <summary>
        /// The Wicked King's defeat (0.0.169): flag 276 going on while in the field says the game's own "The curse has been lifted."
        /// once the player has control. Read without a word the first time (a save loaded after the fight).
        /// </summary>
        private static void WatchKing(long now)
        {
            int on;
            try
            {
                on = EventBit.evtBitCheck(KingFlag) ? 1 : 0;
            }
            catch
            {
                return;
            }
            if (_kingFlag == 0 && on == 1)
            {
                string text = "";
                try
                {
                    text = TextUtil.Clean(Localize.GetLocalizeText(LiftedId) ?? "").TrimStart('>', ' ').Trim();
                }
                catch (Exception ex)
                {
                    Log.Warn($"[Nav:Kalpa4] {LiftedId}: {ex.Message}");
                }
                Log.Info($"[Nav:Kalpa4] Flag {KingFlag} on: the curse lifted; {LiftedId} reads \"{text}\".");
                if (text.Length > 0 && !text.StartsWith("<"))
                {
                    _pending = Join(_pending, text);
                    _pendingMs = now;
                }
            }
            _kingFlag = on;
        }

        private static string Join(string a, string b) => a == null ? b : a + " " + b;
    }
}
