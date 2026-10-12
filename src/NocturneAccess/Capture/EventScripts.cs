using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2Cppfield_H;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NocturneAccess.Systems.Navigation;
using static NocturneAccess.Systems.Navigation.FieldData;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Shift+F6 section "Event scripts" (0.0.70; on FieldScript from 0.0.71, docs/EVENT-PROMPTS-PLAN.md): the script machine's
    /// named commands, the prompt texts, the scene's script, the verdict of every event volume's dry run (what Page Up says)
    /// and, in the file, the instructions of each volume's procedure and the whole command table.
    /// </summary>
    internal static class EventScripts
    {
        private const int MaxListed = 400;   // instructions written per procedure

        internal static void Dump(Action<string> both, Action<string> fileOnly, fldGlobalWork_t gb)
        {
            bool loaded = FieldScript.Ensure();
            both($"script: {FieldScript.LoadNote}");
            both($"opcodes ({FieldScript.OpNames.Length}): {string.Join(" ", FieldScript.OpNames.Select((s, i) => $"{i}={s.Replace("CodeFunc_", "")}"))}");
            var named = new List<string>();
            for (int k = 0; k < FieldScript.Commands.Count; k++)
            {
                string n = FieldScript.Commands[k].Name;
                if (n.Contains("BIT_") || n.Contains("ACT_WIN") || n.Contains("SYNC") || n.StartsWith("itfMesMng", StringComparison.Ordinal) || n.Contains("DIR_HIT") || n.Contains("PAD_DISABLE"))
                    named.Add($"0x{k:X}={n}/{FieldScript.Commands[k].Params}");
            }
            both($"commands: {FieldScript.Commands.Count}; {string.Join(" ", named)}");
            var prompts = new List<string>();
            for (int i = 0; i < 16; i++)
            {
                string t = FieldScript.PanelText(i);
                if (t.Length > 0)
                    prompts.Add($"{i}={Q(t)}");
            }
            both($"prompt texts: {string.Join(" ", prompts)}");
            if (!loaded)
                return;

            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            int count = ranges == null ? 0 : Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            var listed = new List<string>();
            for (int i = 0; i < count; i++)
            {
                fldResHead_t h = ranges[i];
                string name = h?.resourceName ?? "";
                if (h == null || h.resourceType != 10 || !FieldData.EventVolume.IsMatch(name) && !name.Contains("npc_"))
                    continue;
                bool on = FieldScript.VolumeOn(gb.fieldID, gb.areaID, name);
                ScriptVerdict v = FieldScript.Classify(gb.areaID, name);
                string learned = ExploredDoors.PromptOf(gb.fieldID, gb.areaID, name);
                both($"  gEvnRng {i} {Q(name)} -> {Q(v.Procedure)}: {(on ? "" : "OFF in the on/off table; ")}{v}" +
                     (learned.Length > 0 ? $"; learned prompt {Q(learned)}" : ""));
                listed.Add(v.Procedure);
            }
            // Door volumes: the movetype the game would take now (4: jump down).
            var doors = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string name = ranges[i]?.resourceName ?? "";
                if (FieldData.DoorVolume.IsMatch(name))
                    doors.Add($"{name}:{DoorTable.MoveTypeOf(gb.fieldID, gb.areaID, name)}");
            }
            both($"door volumes, movetype now: {string.Join(" ", doors)}");

            foreach (string name in listed.Distinct())
            {
                FieldScript.Proc p = FieldScript.ProcsByIndex.FirstOrDefault(q => q.Name == name);
                if (p == null)
                    continue;
                fileOnly($"  procedure {Q(p.Name)} [{p.Start}..{p.End}):");
                int shown = 0;
                for (int c = p.Start; c < p.End && shown < MaxListed; shown++)
                    c = Disassemble(c, fileOnly);
            }
            fileOnly($"  all procedures ({FieldScript.ProcsByIndex.Count}): {string.Join(" ", FieldScript.ProcsByIndex.Select(q => $"{q.Name}@{q.Start}"))}");
            fileOnly($"  command table: {string.Join(" ", FieldScript.Commands.Select((c, k) => $"0x{k:X}={c.Name}/{c.Params}"))}");
        }

        /// <summary>Writes one instruction (two for PushI / PushF) and returns the next index.</summary>
        private static int Disassemble(int c, Action<string> fileOnly)
        {
            short o = FieldScript.OpAt(c), a = FieldScript.ArgAt(c);
            FieldScript.Op op = FieldScript.OpOf(o);
            string text = $"{op} {a}";
            int next = c + 1;
            if ((op == FieldScript.Op.PushI || op == FieldScript.Op.PushF) && c + 1 < FieldScript.CodeCount)
            {
                text = $"{op} {(op == FieldScript.Op.PushI ? FieldScript.WordAt(c + 1).ToString() : BitConverter.ToSingle(BitConverter.GetBytes(FieldScript.WordAt(c + 1)), 0).ToString())}";
                next = c + 2;
            }
            else if (op == FieldScript.Op.Comm)
                text = $"Comm 0x{(ushort)a:X} {FieldScript.CommandName((ushort)a)}";
            else if (op == FieldScript.Op.IF || op == FieldScript.Op.Goto)
                text = $"{op} -> {FieldScript.Label((ushort)a)}";
            else if (op == FieldScript.Op.Call || op == FieldScript.Op.Jump)
                text = $"{op} {((ushort)a < FieldScript.ProcsByIndex.Count ? FieldScript.ProcsByIndex[(ushort)a].Name : "?")}";
            fileOnly($"    {c,5}: {text}");
            return next;
        }
    }
}
