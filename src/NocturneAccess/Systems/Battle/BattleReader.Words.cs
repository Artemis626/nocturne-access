using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2Cppnewbattle_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Battle
{
    /// <summary>
    /// The battle's word popups on a target (0.0.170; Francesco, 16 Nov 2026: a blocked attack on an ally was not said). The HP
    /// lines speak only HP changes, so a target that takes nothing stayed silent: Beelzebub's Mazandyne at 21:18:23 left Chimera
    /// untouched, Mazionga at 21:23:50 Decarabia, and only the game's miss counter (press4_miss 0 to 1) moved.
    ///
    /// Every word popup is made by nbMakePacket.nbAddMojiEffKoukaPacket(startframe, frame, uniqueid, ref form, type), read
    /// offline (GameAssembly.dll, RVA 0x273F180, and its callers): type 3 from nbActionProcess.SetMissPacket and from
    /// SetAllPacket (a miss), type 4 from SetAllPacket where the result is 0x10000 (the null affinity, as nbParty_t.aisyo holds
    /// it: "Null" on the Analyze screen), types 1 and 2 from nbMakeDamagePacket by the damage type (the weakness and critical
    /// hits, said by the half-turn line). Only 3 and 4 are said, as "Chimera: Null." / "Decarabia: Missed.", when the popup
    /// shows: startframe frames (30 a second) after the packet is made, at most MaxDelayMs.
    /// </summary>
    internal static partial class BattleReader
    {
        private const int MojiMiss = 3, MojiNull = 4;
        private const long FrameMs = 33, MaxDelayMs = 3000;

        private struct Word
        {
            public long DueMs;
            public int Form;
            public int Type;
        }

        private static readonly List<Word> Words = new List<Word>();

        private static void InstallWords()
        {
            Type i = typeof(int);
            Patcher.Postfix("Battle", typeof(nbMakePacket), nameof(nbMakePacket.nbAddMojiEffKoukaPacket),
                new[] { i, i, i, typeof(nbFormation_t).MakeByRefType(), i },
                AccessTools.Method(typeof(BattleReader), nameof(AfterMojiPacket)));
        }

        private static void AfterMojiPacket(int __0, ref nbFormation_t __3, int __4)
        {
            try
            {
                if (!_active || (__4 != MojiMiss && __4 != MojiNull) || __3 == null)
                    return;
                int form = __3.formindex;
                long delay = Math.Min(Math.Max(__0, 0) * FrameMs, MaxDelayMs);
                Words.Add(new Word { DueMs = Clock.ElapsedMilliseconds + delay, Form = form, Type = __4 });
                Log.Info($"{Tag} Word popup type {__4} on form {form} ({ActorName(form)}), in {delay} ms.");
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} Word popup: {ex.Message}");
            }
        }

        private static void SpeakWords(long now)
        {
            for (int n = 0; n < Words.Count; n++)
            {
                Word w = Words[n];
                if (now < w.DueMs)
                    continue;
                Words.RemoveAt(n--);
                string who = ActorName(w.Form);
                if (who.Length == 0)
                    continue;
                string text = $"{who}: {(w.Type == MojiNull ? "Null" : "Missed")}.";
                SpeechManager.Say(text, interrupt: false, force: true);
                BattleLog.Add(text);
            }
        }
    }
}
