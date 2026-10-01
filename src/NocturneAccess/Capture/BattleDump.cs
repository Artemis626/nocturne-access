using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Il2Cpp;
using Il2Cppnewbattle_H;
using Il2Cppnewdata_H;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader.Utils;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Battle;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Shift+F2: everything the battle data offers at this moment, to scope what the battle reader still misses
    /// (Francesco, 22:14 session: turns, press turns, HP). Written to UserData/NocturneAccess/BattleDumps, with a
    /// summary in the log as [Battle:Dump]. Fields from the proxy decompile (docs/class-index.md "Battle"); meanings
    /// beyond those recorded there are to be read off the dumps.
    /// </summary>
    internal static class BattleDump
    {
        private const int Slots = 12;   // formindex 0 to 3 the party, 4 and up the enemies

        internal static string Folder => Path.Combine(MelonEnvironment.UserDataDirectory, "NocturneAccess", "BattleDumps");

        private static StringBuilder _file;

        internal static void Dump()
        {
            if (!BattleReader.Active)
            {
                SpeechManager.Say("Not in battle.", interrupt: true, force: true);
                return;
            }
            _file = new StringBuilder();
            Line($"Nocturne Access {ModMain.ModVersion} battle dump, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Section("Main process", DumpMain);
            Section("Units by formindex", DumpUnits);
            Section("Party records", DumpParty);
            Section("Formation", DumpFormation);
            Section("Target selection", DumpTarget);

            string path = "";
            try
            {
                Directory.CreateDirectory(Folder);
                path = Path.Combine(Folder, $"battledump_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                File.WriteAllText(path, _file.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Error($"[Battle:Dump] Could not write the dump file: {ex.Message}");
                path = "";
            }
            foreach (string line in _file.ToString().Split('\n'))
                if (line.Trim().Length > 0)
                    Log.Info("[Battle:Dump] " + line.TrimEnd('\r'));
            Log.Info($"[Battle:Dump] Full dump: {(path.Length == 0 ? "not written" : path)}");
            SpeechManager.Say(path.Length == 0 ? "Battle dump failed." : "Battle data saved.", interrupt: true, force: true);
            _file = null;
        }

        private static void Line(string text) => _file.AppendLine(text);

        private static void Section(string name, Action body)
        {
            Line($"== {name}");
            try
            {
                body();
            }
            catch (Exception ex)
            {
                Line($"{name} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string Join<T>(IEnumerable<T> values) => values == null ? "null" : string.Join(" ", values);

        private static void DumpMain()
        {
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            if (d == null)
            {
                Line("no main process data");
                return;
            }
            Line($"turn {d.turn} phase {d.phase} nextphase {d.nextphase} phasetime {d.phasetime} battletime {d.battletime} stat 0x{d.stat:X} flag 0x{d.flag:X} endtype {d.endtype}");
            Line($"activeunit {d.activeunit} playerpcnt {d.playerpcnt} enemypcnt {d.enemypcnt} orderindex {d.orderindex} order {Join(d.order)}");
            Line($"press3 {d.press3} press4_p {d.press4_p} press4_ten {d.press4_ten} press4_hansya {d.press4_hansya} press4_miss {d.press4_miss}");
            Line($"prevcommand {d.prevcommand} prevcommind {d.prevcommind} previndex {d.previndex} prevflag {d.prevflag} TarSelType {d.TarSelType}");
            Line($"encno {d.encno} encpackno {d.encpackno} formationtype {d.formationtype} esccnt {d.esccnt} eventno {d.eventno} nakamayobi {d.nakamayobi}");
            Line($"negopformindex {d.negopformindex} negoeformindex {d.negoeformindex} negoresult {d.negoresult} exp {d.exp} maka {d.maka}");
            Line($"actionstackp {d.actionstackp} NowActionProcess {d.NowActionProcess}");
        }

        private static void DumpUnits()
        {
            for (int form = 0; form < Slots; form++)
            {
                datUnitWork_t w = nbMainProcess.nbGetUnitWorkFromFormindex(form);
                if (w == null)
                {
                    Line($"{form}: null");
                    continue;
                }
                string name = form < 4 ? "" : SafeName(() => datDevilName.Get(w.id));
                Line($"{form}: id {w.id} {name} flag 0x{w.flag:X} level {w.level} HP {w.hp}/{w.maxhp} MP {w.mp}/{w.maxmp} badstatus 0x{w.badstatus:X} exp {w.exp}");
                Line($"   param {Join(w.param)} skillparam {Join(w.skillparam)}");
                Line($"   nowcommand {w.nowcommand} nowindex {w.nowindex} nowtarea {w.nowtarea} nowtform {w.nowtform} prevcommand {w.prevcommand} previndex {w.previndex} prevtarea {w.prevtarea} prevtform {w.prevtform}");
                Il2CppStructArray<int> skills = w.skill;
                int count = skills == null ? 0 : Math.Min(Math.Max(w.skillcnt, 0), skills.Length);
                var names = new List<string>();
                for (int k = 0; k < count; k++)
                {
                    int id = skills[k];
                    names.Add($"{id} {SafeName(() => datSkillName.Get(id, w.id))}");
                }
                Line($"   skills ({w.skillcnt}): {string.Join(", ", names)}");
                nbParty_t p = SafeParty(form);
                if (p != null)
                    Line($"   party flag {p.flag} formindex {p.formindex} statindex {p.statindex} partyindex {p.partyindex} count {Join(p.count)} aisyo {Join(p.aisyo?.Select(a => "0x" + a.ToString("X")))} hojoskill {Join(p.hojoskill)}");
            }
        }

        private static void DumpParty()
        {
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            Il2CppReferenceArray<nbParty_t> party = d?.party;
            if (party == null)
            {
                Line("no party array");
                return;
            }
            for (int k = 0; k < party.Length; k++)
            {
                nbParty_t p = party[k];
                if (p != null)
                    Line($"{k}: flag {p.flag} formindex {p.formindex} statindex {p.statindex} partyindex {p.partyindex} count {Join(p.count)}");
            }
        }

        private static void DumpFormation()
        {
            nbMainProcessData_t d = nbMainProcess.nbGetMainProcessData();
            Il2CppReferenceArray<nbFormation_t> form = d?.form;
            if (form == null)
            {
                Line("no formation array");
                return;
            }
            for (int k = 0; k < form.Length; k++)
            {
                nbFormation_t f = form[k];
                if (f != null)
                    Line($"{k}: type {f.type} stat 0x{f.stat:X} flag 0x{f.flag:X} formindex {f.formindex} partyindex {f.partyindex} model {(f.modelobj == null ? "none" : f.modelobj.name)}");
            }
        }

        private static void DumpTarget()
        {
            nbTarSelProcessData_t t = nbTarSelProcess.GetTarSelProcessData();
            if (t == null)
            {
                Line("no target selection");
                return;
            }
            Line($"stat {t.stat} ctype {t.ctype} nowform {t.nowform} nowno {t.nowno} friendcnt {t.friendcnt} enemycnt {t.enemycnt} friendno {Join(t.friendno)} enemyno {Join(t.enemyno)} partyindex {t.partyindex}");
        }

        private static nbParty_t SafeParty(int form)
        {
            try
            {
                return nbMainProcess.nbGetPartyFromFormindex(form);
            }
            catch
            {
                return null;
            }
        }

        private static string SafeName(Func<string> read)
        {
            try
            {
                return TextUtil.Clean(read() ?? "");
            }
            catch
            {
                return "?";
            }
        }
    }
}
