using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Il2Cpp;
using Il2Cppbasic_H;
using Il2Cppfield_H;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NocturneAccess.Core;
using NocturneAccess.Systems.Navigation;
using static NocturneAccess.Systems.Navigation.FieldData;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Shift+F6 section "Event scripts" (0.0.70, docs/EVENT-PROMPTS-PLAN.md step 1; diagnostic only, nothing spoken): what
    /// each event volume's FlowScript procedure does, read from the game's own script before any visit.
    /// - The scene's script file: dds3WorldBasic.dds3GetActiveScene().workdata as dds3SceneBasic_t, its pScrFileData (the
    ///   object ScrScriptProcess.scrSearchProcedureName searches). Parsed with the game's own layouts (scr_H): scrHeader_t
    ///   (32 bytes), scrTypeTable_t (Type, ItemSize, ItemCount, ItemAddress; SCR_DATATYPE order), scrProcTable_t (32 bytes:
    ///   24-byte name, CodeAddress), scrCode_t (4 bytes: s0 opcode, s1 operand).
    /// - Opcode names from the game's dispatch table ScrTraceCode.codefunc[s0].func (scrTraceCode); command names from
    ///   ScrCommandTable.scrCommandTable[s1] (CodeFunc_Comm; 16-byte value entries: the comm delegate, then paranum), read
    ///   as raw memory because the proxy wraps that value array as references. Names through each delegate's method.
    /// - A volume's procedure: fldEveHit_ChkEveHit asks fldInf.fldInf_FindTypeEveNpc (events, people; fldWap.fldDoor_Find2
    ///   for doors) before fldEveHit_ScriptStart. Both set the game's current-event statics (fldInf.gFldInfIdx,
    ///   fldWap.gFldWarpIdx), so they are never called here: InfLookup repeats the first one's table search read-only, and
    ///   a volume that is no information event is looked up as a procedure of its own name (unconfirmed).
    /// - A prompt: fldCommand_ACT_WIN_DSP / 2 / 3 take the script integer before them (scrGetIntPara(0)) as the number of
    ///   fldPanel.inputpanel_mes0, drawn through Localize.GetLocalizeText (fldInputPanelProcess2).
    /// Unverified, checked by this dump: that PUSHI takes its value from the next 4-byte word (its code is not readable in
    /// the file), whether CodeAddress counts instructions, and that pScrFileData is the raw file (byte[]).
    /// </summary>
    internal static class EventScripts
    {
        private const int MaxListed = 400;   // instructions written per procedure
        private const int MaxDepth = 3;      // CALLs followed

        private sealed class Proc
        {
            public string Name;
            public int Start, End;   // instruction indices, End exclusive
        }

        private sealed class Script
        {
            public byte[] Data;
            public int CodeAt, CodeCount;   // file offset and count of 4-byte instructions
            public List<Proc> ProcsByIndex = new List<Proc>();   // file order: CALL operands index this
            public List<Proc> Procs = new List<Proc>();
            public short Op(int i) => BitConverter.ToInt16(Data, CodeAt + i * 4);
            public short Arg(int i) => BitConverter.ToInt16(Data, CodeAt + i * 4 + 2);
            public int Word(int i) => BitConverter.ToInt32(Data, CodeAt + i * 4);
            public float Float(int i) => BitConverter.ToSingle(Data, CodeAt + i * 4);
        }

        internal static void Dump(Action<string> both, Action<string> fileOnly, fldGlobalWork_t gb)
        {
            string[] ops = OpcodeNames(both);
            List<(string Name, int Params)> comms = CommandNames(both);
            List<string> panel = PanelTexts(both, fileOnly);
            Script scr = LoadScript(both, fileOnly);
            if (scr == null)
                return;

            // The area's event and people volumes (doors are left for later) and what each runs.
            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            int n = ranges == null ? 0 : Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            var listed = new HashSet<string>();
            int withPrompt = 0, without = 0, inf = 0, missing = 0;
            Il2CppStringArray labels = fldInf.scriptlabel;
            fileOnly($"  fldInf.scriptlabel: {(labels == null ? "null" : string.Join(" ", Enumerable.Range(0, labels.Length).Select(k => $"{k}={Q(labels[k])}")))}");
            for (int i = 0; i < n; i++)
            {
                fldResHead_t h = ranges[i];
                string name = h?.resourceName ?? "";
                if (h == null || h.resourceType != 10 || name.Length == 0 || DoorVolume.IsMatch(name))
                    continue;
                string learned = ExploredDoors.PromptOf(gb.fieldID, gb.areaID, name);
                string learnedText = learned.Length > 0 ? $"; learned prompt {Q(learned)}" : "";
                string infText = InfLookup(name, gb.areaID, out string proc);
                if (infText != null)
                    inf++;
                // Not an information event: a procedure named like the volume (to be confirmed by these dumps).
                Proc p = scr.Procs.FirstOrDefault(q => q.Name == (proc ?? name));
                if (p == null)
                {
                    missing++;
                    var like = scr.Procs.Where(q => q.Name.IndexOf(name, StringComparison.Ordinal) >= 0).Select(q => q.Name).ToList();
                    both($"  gEvnRng {i} {Q(name)}: {infText ?? "not an information event"}; procedure {Q(proc ?? name)} not in the script" +
                         (like.Count > 0 ? $" (named like it: {string.Join(" ", like)})" : "") + learnedText);
                    continue;
                }
                var prompts = new List<string>();
                var calls = new List<string>();
                Walk(scr, p, ops, comms, 0, new HashSet<string>(), (cmd, args) =>
                {
                    calls.Add(args.Count == 0 ? cmd : $"{cmd}({string.Join(", ", args)})");
                    if (cmd.StartsWith("fldCommand_ACT_WIN_DSP", StringComparison.Ordinal))
                        prompts.Add(args.Count == 0 ? $"{cmd}(?)" : $"{cmd}({args[0]}) {Q(PanelText(panel, args[0]))}");
                });
                if (prompts.Count > 0) withPrompt++; else without++;
                both($"  gEvnRng {i} {Q(name)}: {infText ?? "not an information event"}; runs {Q(p.Name)} [{p.Start}..{p.End}): " +
                     (prompts.Count > 0 ? "prompt " + string.Join(", ", prompts.Distinct()) : "no prompt") + learnedText);
                fileOnly($"    commands: {string.Join("; ", calls)}");
                listed.Add(p.Name);
            }
            both($"event volumes: {withPrompt} with a prompt, {without} without, {missing} with no procedure found; {inf} information events");
            DumpInfTable(fileOnly);

            // The listed procedures in full (file only), then every procedure name of the script.
            foreach (Proc p in scr.Procs.Where(q => listed.Contains(q.Name)))
            {
                fileOnly($"  procedure {Q(p.Name)} [{p.Start}..{p.End}):");
                int shown = 0;
                for (int c = p.Start; c < p.End && shown < MaxListed; shown++)
                    c = Disassemble(scr, c, ops, comms, fileOnly);
            }
            fileOnly($"  all procedures ({scr.Procs.Count}): {string.Join(" ", scr.Procs.Select(q => $"{q.Name}@{q.Start}"))}");
        }

        /// <summary>
        /// fldInf.fldInf_FindTypeEveNpc read without calling it (it sets fldInf.gFldInfIdx, the game's current information
        /// event): gFldInf.pack[0..7] whose Idx is not 0, each EveHit[0..4] with area == areaID + 1 and EveName == the volume;
        /// then gFldInf.set[0..39] whose st.Type is 0 or 1, st.Area == areaID + 1 and st.EveHit == the volume. A match runs
        /// fldInf.scriptlabel[4]. Returns a description, or null when the volume is not an information event.
        /// </summary>
        private static string InfLookup(string volume, int areaId, out string proc)
        {
            proc = null;
            FLD_INF_ALL_t all = fldInf.gFldInf;
            if (all == null)
                return null;
            Il2CppStringArray labels = fldInf.scriptlabel;
            string label = labels != null && labels.Length > 4 ? labels[4] : null;
            var packs = all.pack;
            for (int i = 0; packs != null && i < 8 && i < packs.Length; i++)
            {
                FLD_INF_PACK_t pk = packs[i];
                if (pk == null || pk.Idx == 0 || pk.EveHit == null)
                    continue;
                for (int j = 0; j < 5 && j < pk.EveHit.Length; j++)
                {
                    FLD_INF_PACK2_t e = pk.EveHit[j];
                    if (e != null && e.area == areaId + 1 && e.EveName == volume)
                    {
                        proc = label;
                        return $"information event, pack {i} Idx {pk.Idx}";
                    }
                }
            }
            var sets = all.set;
            for (int k = 0; sets != null && k < 40 && k < sets.Length; k++)
            {
                FLD_INF_START_t st = sets[k]?.st;
                if (st == null || (st.Type != 0 && st.Type != 1) || st.Area != areaId + 1 || st.EveHit != volume)
                    continue;
                proc = label;
                return $"information event, set {k} (Type {st.Type} Action {(int)st.Action}{Messages(sets[k])})";
            }
            return null;
        }

        private static string Messages(FLD_INF_SET_t set)
        {
            var ms = set?.ms;
            if (ms == null || ms.Length == 0)
                return "";
            return "; messages " + string.Join(" ", Enumerable.Range(0, ms.Length).Select(m => ms[m] == null ? "-" : $"{ms[m].Type}/{ms[m].Msg}"));
        }

        /// <summary>The field's information event table (file only): packs and the sets with a name.</summary>
        private static void DumpInfTable(Action<string> fileOnly)
        {
            FLD_INF_ALL_t all = fldInf.gFldInf;
            if (all == null)
            {
                fileOnly("  fldInf.gFldInf: null");
                return;
            }
            var packs = all.pack;
            for (int i = 0; packs != null && i < packs.Length; i++)
            {
                FLD_INF_PACK_t pk = packs[i];
                if (pk?.EveHit == null)
                    continue;
                fileOnly($"  inf pack {i} Idx {pk.Idx}: {string.Join(" ", Enumerable.Range(0, pk.EveHit.Length).Select(j => pk.EveHit[j] == null ? "-" : $"{(int)pk.EveHit[j].area}:{Q(pk.EveHit[j].EveName)}"))}");
            }
            var sets = all.set;
            for (int k = 0; sets != null && k < sets.Length; k++)
            {
                FLD_INF_START_t st = sets[k]?.st;
                if (st == null || string.IsNullOrEmpty(st.EveHit))
                    continue;
                fileOnly($"  inf set {k}: Type {st.Type} Area {(int)st.Area} Action {(int)st.Action} EveHit {Q(st.EveHit)}{Messages(sets[k])}");
            }
        }

        /// <summary>
        /// Follows a procedure's code in order, reporting each COMM with the literal integers pushed before it (as many as its
        /// paranum), and CALLs into other procedures (CALL's operand is a procedure index) up to MaxDepth.
        /// </summary>
        private static void Walk(Script scr, Proc p, string[] ops, List<(string Name, int Params)> comms, int depth,
                                 HashSet<string> seen, Action<string, List<int>> onComm)
        {
            if (!seen.Add(p.Name))
                return;
            var pushed = new List<int>();
            for (int c = p.Start; c < p.End; c++)
            {
                string op = OpName(ops, scr.Op(c));
                switch (op)
                {
                    case "CodeFunc_PushI":
                        if (c + 1 < scr.CodeCount)
                            pushed.Add(scr.Word(c + 1));
                        c++;
                        break;
                    case "CodeFunc_PushF":
                        c++;
                        break;
                    case "CodeFunc_PushIS":
                        pushed.Add(scr.Arg(c));
                        break;
                    case "CodeFunc_Comm":
                    {
                        int k = (ushort)scr.Arg(c);
                        string name = k < comms.Count ? comms[k].Name : $"comm{k}";
                        int want = k < comms.Count ? comms[k].Params : 0;
                        var args = want > 0 && pushed.Count >= want ? pushed.Skip(pushed.Count - want).ToList() : new List<int>();
                        onComm(name, args);
                        pushed.Clear();
                        break;
                    }
                    case "CodeFunc_Call":
                    {
                        int k = (ushort)scr.Arg(c);
                        if (depth < MaxDepth && k < scr.ProcsByIndex.Count)
                            Walk(scr, scr.ProcsByIndex[k], ops, comms, depth + 1, seen, onComm);
                        pushed.Clear();
                        break;
                    }
                    default:
                        if (!op.StartsWith("CodeFunc_Push", StringComparison.Ordinal))
                            pushed.Clear();
                        break;
                }
            }
        }

        /// <summary>Writes one instruction (two for PUSHI / PUSHF) and returns the next index.</summary>
        private static int Disassemble(Script scr, int c, string[] ops, List<(string Name, int Params)> comms, Action<string> fileOnly)
        {
            short o = scr.Op(c), a = scr.Arg(c);
            string op = OpName(ops, o);
            string text = op;
            int next = c + 1;
            if (op == "CodeFunc_PushI" && c + 1 < scr.CodeCount)
            {
                text += $" {scr.Word(c + 1)} (next word 0x{scr.Word(c + 1):X8})";
                next = c + 2;
            }
            else if (op == "CodeFunc_PushF" && c + 1 < scr.CodeCount)
            {
                text += $" {scr.Float(c + 1)}";
                next = c + 2;
            }
            else if (op == "CodeFunc_Comm")
                text += $" {a} {((ushort)a < comms.Count ? comms[(ushort)a].Name : "?")}";
            else
                text += $" {a}";
            fileOnly($"    {c,5}: {o,3} {text}");
            return next;
        }

        private static string OpName(string[] ops, short o) => o >= 0 && o < ops.Length ? ops[o] : $"op{o}";

        private static string MethodName(IntPtr delegatePtr)
        {
            if (delegatePtr == IntPtr.Zero)
                return "";
            IntPtr method = new Il2CppSystem.Delegate(delegatePtr).method;
            if (method == IntPtr.Zero)
                return "";
            return Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(method)) ?? "";
        }

        /// <summary>The script machine's opcodes in dispatch order: ScrTraceCode.codefunc[i].func's method names.</summary>
        private static string[] OpcodeNames(Action<string> both)
        {
            Il2CppReferenceArray<Scr_AnonymousClass> table = ScrTraceCode.codefunc;
            int n = table == null ? 0 : table.Length;
            var names = new string[n];
            for (int i = 0; i < n; i++)
            {
                Scr_AnonymousClass e = table[i];
                names[i] = e?.func == null ? "" : MethodName(e.func.Pointer);
            }
            both($"opcodes ({n}): {string.Join(" ", names.Select((s, i) => $"{i}={s.Replace("CodeFunc_", "")}"))}");
            return names;
        }

        /// <summary>
        /// The script commands in COMM order. ScrCommandTable.scrCommandTable holds 16-byte values (the comm delegate, then
        /// paranum; CodeFunc_Comm indexes it in 16-byte steps), read from the array's memory: length at 0x18, data at 0x20.
        /// </summary>
        private static List<(string Name, int Params)> CommandNames(Action<string> both)
        {
            var list = new List<(string, int)>();
            var table = ScrCommandTable.scrCommandTable;
            if (table == null)
            {
                both("command table: none");
                return list;
            }
            IntPtr a = table.Pointer;
            long n = Marshal.ReadInt64(a, 0x18);
            for (int k = 0; k < n && k < 4096; k++)
            {
                IntPtr entry = a + 0x20 + k * 16;
                list.Add((MethodName(Marshal.ReadIntPtr(entry)), Marshal.ReadInt32(entry, 8)));
            }
            int act = list.FindIndex(e => e.Item1 == "fldCommand_ACT_WIN_DSP");
            both($"commands: {list.Count}; fldCommand_ACT_WIN_DSP at {act} ({(act >= 0 ? list[act].Item2 : 0)} parameters), " +
                 $"_ACT_WIN_DSP2 at {list.FindIndex(e => e.Item1 == "fldCommand_ACT_WIN_DSP2")}, _ACT_WIN_DSP3 at {list.FindIndex(e => e.Item1 == "fldCommand_ACT_WIN_DSP3")}");
            return list;
        }

        /// <summary>The prompt texts: fldPanel.inputpanel_mes0[i] and Localize.GetLocalizeText of it.</summary>
        private static List<string> PanelTexts(Action<string> both, Action<string> fileOnly)
        {
            var texts = new List<string>();
            Il2CppStringArray ids = fldPanel.inputpanel_mes0;
            int n = ids == null ? 0 : ids.Length;
            var line = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                string id = ids[i] ?? "";
                string text = "";
                try { text = id.Length == 0 ? "" : Localize.GetLocalizeText(id) ?? ""; }
                catch (Exception ex) { text = "(" + ex.Message + ")"; }
                text = TextUtil.Clean(text).Trim();
                texts.Add(text);
                line.Append($" {i}={Q(text)}");
                fileOnly($"  inputpanel_mes0[{i}] {Q(id)} -> {Q(text)}");
            }
            both($"prompt texts ({n}):{line}");
            return texts;
        }

        private static string PanelText(List<string> panel, int no) => no >= 0 && no < panel.Count ? panel[no] : "?";

        private static Script LoadScript(Action<string> both, Action<string> fileOnly)
        {
            dds3Basic_t scene = dds3WorldBasic.dds3GetActiveScene();
            dds3SceneBasic_t work = scene?.workdata?.TryCast<dds3SceneBasic_t>();
            Il2CppSystem.Object data = work?.pScrFileData;
            if (data == null)
            {
                both($"scene script: none (scene {(scene == null ? "null" : Q(scene.name))}, scene data {(work == null ? "null" : "set")})");
                return null;
            }
            string type = data.GetIl2CppType()?.FullName ?? "?";
            if (type != "System.Byte[]")
            {
                both($"scene script: pScrFileData is {type}, not a byte array; not parsed");
                return null;
            }
            byte[] bytes = new Il2CppStructArray<byte>(data.Pointer);
            var scr = new Script { Data = bytes };
            string magic = bytes.Length >= 12 ? Encoding.ASCII.GetString(bytes, 8, 4) : "";
            int tables = bytes.Length >= 24 ? BitConverter.ToInt32(bytes, 20) : 0;
            both($"scene script: {Q(scene.name)}, {bytes.Length} bytes, file type {bytes[0]}, magic {Q(magic)}, size field {BitConverter.ToInt32(bytes, 4)}, {tables} tables");
            int procAt = -1, procCount = 0, procSize = 0;
            for (int t = 0; t < tables && 32 + t * 16 + 16 <= bytes.Length; t++)
            {
                int at = 32 + t * 16;
                int kind = BitConverter.ToInt32(bytes, at), size = BitConverter.ToInt32(bytes, at + 4);
                int count = BitConverter.ToInt32(bytes, at + 8), address = BitConverter.ToInt32(bytes, at + 12);
                fileOnly($"  table {t}: type {kind} item size {size} count {count} at {address}");
                if (kind == 0) { procAt = address; procCount = count; procSize = size; }
                if (kind == 2) { scr.CodeAt = address; scr.CodeCount = count; }
            }
            if (procAt < 0 || scr.CodeCount == 0 || procSize < 28)
            {
                both("scene script: no procedure table or code; not parsed");
                return null;
            }
            for (int i = 0; i < procCount && procAt + (i + 1) * procSize <= bytes.Length; i++)
            {
                int at = procAt + i * procSize;
                int len = Array.IndexOf(bytes, (byte)0, at, 24) - at;
                string name = Encoding.ASCII.GetString(bytes, at, len < 0 ? 24 : len);
                scr.ProcsByIndex.Add(new Proc { Name = name, Start = BitConverter.ToInt32(bytes, at + 24) });
            }
            // A procedure runs to the next one's start (in address order); the last to the end of the code.
            var ordered = scr.ProcsByIndex.OrderBy(q => q.Start).ToList();
            for (int i = 0; i < ordered.Count; i++)
                ordered[i].End = i + 1 < ordered.Count ? ordered[i + 1].Start : scr.CodeCount;
            scr.Procs = ordered;
            int maxStart = ordered.Count == 0 ? 0 : ordered[ordered.Count - 1].Start;
            both($"procedures: {procCount}; code: {scr.CodeCount} instructions; highest start {maxStart} ({(maxStart < scr.CodeCount ? "within the code: starts count instructions" : "beyond the code: starts are not instruction counts")})");
            return scr;
        }
    }
}
