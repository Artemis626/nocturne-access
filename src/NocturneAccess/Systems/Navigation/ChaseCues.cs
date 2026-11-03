using System;
using System.Diagnostics;
using HarmonyLib;
using Il2Cpp;
using Il2Cppfield_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using static NocturneAccess.Systems.Navigation.Angles;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// The Third Kalpa's chase (0.0.143; Francesco, 2 Nov 2026: cues first, and an off switch he may choose). Raidou (Dante in the
    /// other edition) is the game's own chase AI, fldTest.fldRun_Dante; read offline and from the 21:49 probe run: while story flag
    /// 2803 is on, fldTest.fldRunEncountChk catches the player within 250 (x-z) and starts a battle; he runs about 750 units a
    /// second, so the cues start far out.
    ///
    /// - A pulse (Earcons.ChasePulse, low and soft) while he is within PulseFrom and coming nearer, or within NearAlways whatever
    ///   he does; it repeats faster as he closes in (once a second far out, about seven a second at the catch radius).
    /// - "Raidou moving." when he sets off after standing still (area 4's wait by the door ends, then he hunts).
    /// - (0.0.143 to 0.0.147: Insert said where he was; removed in 0.0.148, Francesco: with him stopped the key is free.)
    /// - ChaseOff (config, on by default since 0.0.144; Francesco, 2 Nov 2026, after testing the cues: he is too fast to react to
    ///   by ear, an accessibility change stated in the README). Read offline: the AI, his collision, model and sounds run in
    ///   fldTest.fldRunView only while story flags 2803 and 2804 are both on; 2804 is the script's FLD_DANTE_AI_START / STOP flag,
    ///   and fldRunView is the only reader of it. The mod answers evtBitCheck(2804) with false (as FirstPersonView answers 2800:
    ///   nothing is written, nothing saved), so he stands still; and a postfix on fldRunEncountChk (2803 and within 250, the
    ///   catch) turns a catch into nothing, should the player pass by him. Flag 2803 itself stays the game's (the doors read it,
    ///   fldWap.fldDoor_Step). The stages, doors and story flags are the game's; the boss fight is a separate door and stays.
    /// </summary>
    internal static class ChaseCues
    {
        private const int ChaseFlag = 2803;
        private const float CatchRadius = 250f;     // fldRunEncountChk (constant 0x1828C7648)
        private const float PulseFrom = 3000f;      // coming nearer within this: the pulse
        private const float NearAlways = 1200f;     // within this: the pulse whatever he does
        private const long SlowestMs = 1000, FastestMs = 140;
        private const long StillMs = 2000;          // standing this long, then moving: "Raidou moving."
        // Raidou in the Chronicle edition (free DLC), Dante in Maniax (0.0.145; Francesco, 2 Nov 2026: the mod must hold in every
        // edition). The game's own test, read offline in AssetBundleCtrl.CheckRaidouDLC: the Chronicle content installed
        // (dds3KernelMain.dds3CheckDLC9) and story flag 3718 on. The Maniax field data (f043m) has the same doors, area files and
        // chase commands; only the partner's pictures differ in its script.
        internal static bool RaidouEdition
        {
            get
            {
                try
                {
                    return dds3KernelMain.dds3CheckDLC9() && EventBit.evtBitCheck(RaidouFlag);
                }
                catch
                {
                    return true;
                }
            }
        }

        internal const int RaidouFlag = 3718;
        private static string Name => RaidouEdition ? "Raidou" : "Dante";

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static bool _on;
        private static long _lastPulse, _stillSince = -1, _lastSample;
        private static float _lastLen = -1, _trend;   // _trend: distance change per second, smoothed (negative: coming nearer)
        private static bool _warnedMove;

        private const int AiFlag = 2804;            // FLD_DANTE_AI_START / STOP; read only by fldTest.fldRunView

        private static bool Off => Config.ChaseOff?.Value ?? true;
        private static bool _heldLogged;

        internal static void Install()
        {
            Patcher.Postfix("Chase", typeof(fldTest), nameof(fldTest.fldRunEncountChk), Type.EmptyTypes,
                AccessTools.Method(typeof(ChaseCues), nameof(AfterEncountChk)));
            Patcher.Postfix("Chase", typeof(EventBit), nameof(EventBit.evtBitCheck), new[] { typeof(int) },
                AccessTools.Method(typeof(ChaseCues), nameof(AfterBitCheck)));
            Log.Info($"[Chase] The Third Kalpa chase is {(Off ? "off (ChaseOff): the pursuer stands still and never catches" : "the game's (ChaseOff false)")}.");
        }

        private static void AfterEncountChk(ref int __result)
        {
            if (__result == 0 || !Off)
                return;
            __result = 0;
            Log.Info($"[Chase] {Name} reached the player: no battle (ChaseOff).");
        }

        private static void AfterBitCheck(int __0, ref bool __result)
        {
            if (__0 != AiFlag || !__result || !Off)
                return;
            __result = false;
            if (_heldLogged)
                return;
            _heldLogged = true;
            Log.Info($"[Chase] The game asked for flag {AiFlag} (the pursuer's AI): answered off (ChaseOff); he stands still.");
        }

        private static bool InChase(fldGlobalWork_t gb, out fldTest.FLDRUN_DANTE_t d)
        {
            d = null;
            try
            {
                if (gb == null || gb.fieldID >= 100 || !EventBit.evtBitCheck(ChaseFlag))
                    return false;
                d = fldTest.fldRun_Dante;
                return d != null;
            }
            catch
            {
                return false;
            }
        }

        // The "disabled" line (0.0.146; Francesco, 3 Nov 2026): once after each load or title screen, on first reaching the chase
        // with ChaseOff on (ExploredDoors.ClearAll calls ForgetHeard, as for the way-on tone).
        // 0.0.149 (Francesco, 3 Nov 2026 13:57 session: the line was cut by the area's arrival page, "It seems as if someone is
        // watching you...", 0.3 s later while the fade still covered the screen): said once the player has been exploring for
        // SettleMs with no dialogue page due or spoken meanwhile, and held against interrupts while it is read.
        private const long SettleMs = 2000;
        private const int TellHoldMs = 7000;
        private static bool _toldOff;
        private static long _settledSince = -1;

        internal static void ForgetHeard()
        {
            _toldOff = false;
            _settledSince = -1;
        }

        private static void TellOff(fldGlobalWork_t gb)
        {
            if (_toldOff || !Off)
                return;
            long now = Clock.ElapsedMilliseconds;
            if (!FieldState.IsExploring || !InChase(gb, out _) || Dialogue.DialogueReader.PageDue
                || Dialogue.DialogueReader.SpokeWithin(SettleMs))
            {
                _settledSince = -1;
                return;
            }
            if (_settledSince < 0)
                _settledSince = now;
            if (now - _settledSince < SettleMs)
                return;
            _toldOff = true;
            Log.Info("[Chase] Said: the chase is disabled (ChaseOff).");
            SpeechManager.SayHeld("Chase disabled: set ChaseOff to false in UserData, NocturneAccess.cfg, with the game closed, to enable it.", TellHoldMs);
        }

        internal static void Tick()
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            TellOff(gb);
            fldTest.FLDRUN_DANTE_t d = null;
            bool on = !Off && InChase(gb, out d) && FieldState.IsExploring;
            if (on != _on)
            {
                _on = on;
                _lastLen = -1;
                _trend = 0;
                _stillSince = -1;
                _warnedMove = false;
            }
            if (!on)
                return;
            long now = Clock.ElapsedMilliseconds;
            float len = Distance(gb, d);

            // How fast he is closing in, from the distance over time (smoothed; his speed alone says nothing of his heading).
            if (_lastLen >= 0 && now - _lastSample >= 50)
            {
                float perSecond = (len - _lastLen) * 1000f / (now - _lastSample);
                _trend = _trend * 0.6f + perSecond * 0.4f;
                _lastLen = len;
                _lastSample = now;
            }
            else if (_lastLen < 0)
            {
                _lastLen = len;
                _lastSample = now;
            }

            // Standing still, then off: say so once (area 4's countdown; a fresh start of a stage).
            bool moving = d.speed > 0.5f;
            if (!moving)
            {
                if (_stillSince < 0)
                    _stillSince = now;
                _warnedMove = false;
            }
            else
            {
                if (_stillSince >= 0 && now - _stillSince >= StillMs && !_warnedMove)
                {
                    _warnedMove = true;
                    Log.Info($"[Chase] {Name} moving after {(now - _stillSince) / 1000.0:0.#} s still, {len:0} away.");
                    SpeechManager.Say($"{Name} moving.", interrupt: true);
                }
                _stillSince = -1;
            }

            if (!(Config.ChaseSound?.Value ?? true))
                return;
            bool coming = _trend < -100f;
            if (len > PulseFrom || (len > NearAlways && !coming))
                return;
            float k = Math.Max(0f, Math.Min(1f, (len - CatchRadius) / (PulseFrom - CatchRadius)));
            long interval = FastestMs + (long)((SlowestMs - FastestMs) * k);
            if (now - _lastPulse < interval)
                return;
            _lastPulse = now;
            Earcons.ChasePulse();
        }

        private static float Distance(fldGlobalWork_t gb, fldTest.FLDRUN_DANTE_t d)
        {
            float dx = gb.playerX - d.x, dz = gb.playerZ - d.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

    }
}
