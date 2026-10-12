using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Il2Cpp;
using Il2Cppbasic_H;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    internal enum ScriptOutcome { Missing, Unknown, Inactive, Story, Action }

    /// <summary>What an event volume's script does now (FieldScript.Classify).</summary>
    internal sealed class ScriptVerdict
    {
        public ScriptOutcome Outcome;
        public string Procedure = "";
        public int Prompt = -1;            // the prompt number (fldPanel.inputpanel_mes0 index) an action waits on
        public string PromptText = "";     // its words: "Examine", "Talk", "Ride"
        public string Detail = "";         // the first narration line after confirming ("There's a switch"), or ""
        public string Reason = "";         // for the log: the command or end that decided it
        internal int ResumeAt = -1;          // an action: the instruction after its prompt command
        internal List<int?> ResumeStack;     // and the stack there

        public override string ToString() => Outcome == ScriptOutcome.Action
            ? $"action {PromptText}{(Detail.Length > 0 ? ": " + Detail : "")} ({Reason})"
            : $"{Outcome.ToString().ToLowerInvariant()} ({Reason})";
    }

    /// <summary>
    /// The field's own event script, read as data (0.0.71, docs/EVENT-PROMPTS-PLAN.md; offline survey of all 35 dungeon
    /// fields with tools/unity/simulate.pl: 539 actions, 514 story events, 134 idle with every flag off, none unknown).
    /// - The script: dds3WorldBasic.dds3GetActiveScene().workdata as dds3SceneBasic_t, its pScrFileData, the FlowScript file
    ///   the game starts procedures from (fldEveHit_ScriptStart, dds3SceneBasicStartScript). Layout of scr_H: scrHeader_t
    ///   (32 bytes; TypeTableCount at 16), scrTypeTable_t (Type, ItemSize, ItemCount, ItemAddress; SCR_DATATYPE: procedures,
    ///   labels, code, messages, strings), scrProcTable_t / scrLabelTable_t (24-byte name, CodeAddress), scrCode_t (s0
    ///   opcode, s1 operand; PushI and PushF take the next word as their value).
    /// - Opcode names from ScrTraceCode.codefunc[s0].func (scrTraceCode dispatches on it); command names and parameter counts
    ///   from ScrCommandTable.scrCommandTable[s1] (CodeFunc_Comm), each delegate's method named by il2cpp_method_get_name.
    /// - An event volume runs the procedure "{area + 1:000}_{volume}" ("020_01eve_06": Assembly of Nihilo B12's switch;
    ///   every NNeve / NNnpc volume of the 35 fields has one). People and doors run shared handlers.
    /// - The dry run follows the control flow from the procedure's start, evaluating only scrCommand_BIT_CHK (the game's
    ///   EventBit.evtBitCheck, read) and applying BIT_ON / BIT_OFF to its own copy; a command whose result is read at once
    ///   (PushReg next) is a query whose value is not known, and a branch on it is followed both ways (the action, story,
    ///   idle order decides). Outcomes: a prompt command (fldCommand_ACT_WIN_DSP, 2, 3: the prompt number is the value
    ///   pushed last; the game shows Localize.GetLocalizeText(fldPanel.inputpanel_mes0[n])) is an action; scrCommand_SYNC (a
    ///   polling loop's frame wait) or END first is idle; any other command first is a story event (the game takes over).
    /// - An action's detail: from the prompt on, as if confirmed, the first itfMesMngMessage(dialog) of the message block
    ///   (BMD "MSG1": dialogs with a 24-byte name and lines holding localization keys "&lt;F020_L0002&gt;"); its first line,
    ///   localized, when it is narration (the game's lines that start with "&gt;": "&gt; There's a switch.").
    /// - The game's on/off table fldEveHit.gFldEveHitOnOffTbl (fldEveHit_ChkOn): a volume listed for this field and area + 1
    ///   is active only while its flag is set.
    /// Nothing here calls the game's own lookups (fldInf_FindTypeEveNpc and fldDoor_Find2 write its current-event statics).
    /// </summary>
    internal static class FieldScript
    {
        private const string Tag = "[Nav:Script]";
        private const int StepBudget = 20000;
        private const int MaxBranchDepth = 12;
        private const int DetailSteps = 400;

        internal enum Op
        {
            PushI, PushIS, PushF, PushIX, PushFX, PushLIX, PushLFX, PushSTR, PushReg, PopIX, PopFX, PopLIX, PopLFX,
            Proc, Comm, End, Jump, Call, Run, Goto, Add, Sub, Mul, Div, Minus, Not, Or, And, EQ, NEQ, S, L, SE, LE, IF, Unknown
        }

        // The order the game's own scripts decode with (every procedure of the 35 dungeon field scripts starts with Proc,
        // tools/unity/flowscript.pl): used only when the dispatch table cannot be named.
        private static readonly Op[] FileOrder =
        {
            Op.PushI, Op.PushF, Op.PushIX, Op.PushFX, Op.PushReg, Op.PopIX, Op.PopFX, Op.Proc, Op.Comm, Op.End, Op.Jump, Op.Call,
            Op.Run, Op.Goto, Op.Add, Op.Sub, Op.Mul, Op.Div, Op.Minus, Op.Not, Op.Or, Op.And, Op.EQ, Op.NEQ, Op.S, Op.L,
            Op.SE, Op.LE, Op.IF, Op.PushIS, Op.PushLIX, Op.PushLFX, Op.PopLIX, Op.PopLFX, Op.PushSTR,
        };

        internal sealed class Proc { public string Name = ""; public int Start, End; }

        // The loaded script.
        private static IntPtr _dataPtr;
        private static byte[] _data;
        private static int _codeAt, _codeCount;
        internal static readonly List<Proc> ProcsByIndex = new List<Proc>();
        private static readonly Dictionary<string, Proc> ProcByName = new Dictionary<string, Proc>();
        private static readonly List<int> LabelAddress = new List<int>();
        private static readonly List<string> DialogFirstKey = new List<string>();
        internal static string LoadNote { get; private set; } = "not loaded";

        // The machine's tables (static in the game).
        internal static Op[] Ops = Array.Empty<Op>();
        internal static string[] OpNames = Array.Empty<string>();
        internal static readonly List<(string Name, int Params)> Commands = new List<(string, int)>();
        private static int _bitChk = -1, _bitOn = -1, _bitOff = -1, _sync = -1, _message = -1;
        private static readonly HashSet<int> PromptCommands = new HashSet<int>();
        private static bool _tablesRead;

        internal static byte[] Data => _data;
        internal static int CodeCount => _codeCount;
        internal static short OpAt(int i) => BitConverter.ToInt16(_data, _codeAt + i * 4);
        internal static short ArgAt(int i) => BitConverter.ToInt16(_data, _codeAt + i * 4 + 2);
        internal static int WordAt(int i) => BitConverter.ToInt32(_data, _codeAt + i * 4);
        internal static Op OpOf(short o) => o >= 0 && o < Ops.Length ? Ops[o] : Op.Unknown;
        internal static int Label(int i) => i >= 0 && i < LabelAddress.Count ? LabelAddress[i] : -1;
        internal static string CommandName(int k) => k >= 0 && k < Commands.Count ? Commands[k].Name : $"command {k}";
        internal static bool IsPrompt(int k) => PromptCommands.Contains(k);
        internal static int MessageCommand => _message;

        /// <summary>Loads the scene's script and the machine's tables when they changed; false when there is none.</summary>
        internal static bool Ensure()
        {
            try
            {
                ReadTables();
                dds3Basic_t scene = dds3WorldBasic.dds3GetActiveScene();
                dds3SceneBasic_t work = scene?.workdata?.TryCast<dds3SceneBasic_t>();
                Il2CppSystem.Object obj = work?.pScrFileData;
                if (obj == null)
                {
                    LoadNote = "the scene has no script";
                    return false;
                }
                if (obj.Pointer == _dataPtr && _data != null)
                    return true;
                string type = obj.GetIl2CppType()?.FullName ?? "?";
                if (type != "System.Byte[]")
                {
                    LoadNote = $"the scene's script is {type}, not a byte array";
                    return false;
                }
                _dataPtr = obj.Pointer;
                _data = new Il2CppStructArray<byte>(obj.Pointer);
                Parse();
                Cache.Clear();
                Log.Info($"{Tag} Script of {scene.name}: {LoadNote}.");
                return _codeCount > 0;
            }
            catch (Exception ex)
            {
                LoadNote = "read failed: " + ex.Message;
                Log.Warn($"{Tag} {LoadNote}");
                _data = null;
                _dataPtr = IntPtr.Zero;
                return false;
            }
        }

        private static void Parse()
        {
            ProcsByIndex.Clear();
            ProcByName.Clear();
            LabelAddress.Clear();
            DialogFirstKey.Clear();
            _codeAt = _codeCount = 0;
            byte[] d = _data;
            if (d.Length < 32 || Encoding.ASCII.GetString(d, 8, 4) != "FLW0")
            {
                LoadNote = $"{d.Length} bytes, not a FlowScript file";
                return;
            }
            int tables = BitConverter.ToInt32(d, 16);
            int procAt = 0, procCount = 0, procSize = 0, labelAt = 0, labelCount = 0, labelSize = 0, msgAt = -1, msgSize = 0;
            for (int t = 0; t < tables && 32 + t * 16 + 16 <= d.Length; t++)
            {
                int at = 32 + t * 16;
                int kind = BitConverter.ToInt32(d, at), size = BitConverter.ToInt32(d, at + 4);
                int count = BitConverter.ToInt32(d, at + 8), address = BitConverter.ToInt32(d, at + 12);
                switch (kind)
                {
                    case 0: procAt = address; procCount = count; procSize = size; break;
                    case 1: labelAt = address; labelCount = count; labelSize = size; break;
                    case 2: _codeAt = address; _codeCount = count; break;
                    case 3: msgAt = address; msgSize = count * size; break;
                }
            }
            for (int i = 0; i < procCount && procSize >= 28 && procAt + (i + 1) * procSize <= d.Length; i++)
            {
                int at = procAt + i * procSize;
                var p = new Proc { Name = Name24(d, at), Start = BitConverter.ToInt32(d, at + 24) };
                ProcsByIndex.Add(p);
                ProcByName[p.Name] = p;
            }
            var ordered = new List<Proc>(ProcsByIndex);
            ordered.Sort((a, b) => a.Start.CompareTo(b.Start));
            for (int i = 0; i < ordered.Count; i++)
                ordered[i].End = i + 1 < ordered.Count ? ordered[i + 1].Start : _codeCount;
            for (int i = 0; i < labelCount && labelSize >= 28 && labelAt + (i + 1) * labelSize <= d.Length; i++)
                LabelAddress.Add(BitConverter.ToInt32(d, labelAt + i * labelSize + 24));
            if (msgAt >= 0)
                ParseDialogs(d, msgAt, msgSize);
            LoadNote = $"{d.Length} bytes, {ProcsByIndex.Count} procedures, {LabelAddress.Count} labels, {_codeCount} instructions, {DialogFirstKey.Count} dialogs";
        }

        /// <summary>The message block (BMD "MSG1"): per dialog the first localization key of its text ("F020_L0002").</summary>
        private static void ParseDialogs(byte[] d, int at, int size)
        {
            if (at + 0x20 > d.Length || Encoding.ASCII.GetString(d, at + 8, 4) != "MSG1")
                return;
            int count = BitConverter.ToInt32(d, at + 0x18);
            for (int i = 0; i < count && at + 0x20 + 8 * i + 8 <= d.Length; i++)
            {
                int off = BitConverter.ToInt32(d, at + 0x20 + 8 * i + 4);
                int p = at + 0x20 + off;
                int end = Math.Min(d.Length, at + size);
                DialogFirstKey.Add(p + 24 < end ? FirstKey(d, p + 24, end) : "");
            }
        }

        /// <summary>The first "&lt;KEY&gt;" (letters, digits, underscores) from position p, without the brackets.</summary>
        private static string FirstKey(byte[] d, int p, int end)
        {
            for (int i = p; i < end && i < p + 4096; i++)
            {
                if (d[i] != (byte)'<')
                    continue;
                int j = i + 1;
                while (j < end && (char.IsLetterOrDigit((char)d[j]) || d[j] == (byte)'_'))
                    j++;
                if (j > i + 1 && j < end && d[j] == (byte)'>')
                    return Encoding.ASCII.GetString(d, i + 1, j - i - 1);
            }
            return "";
        }

        private static string Name24(byte[] d, int at)
        {
            int n = 0;
            while (n < 24 && d[at + n] != 0)
                n++;
            return Encoding.ASCII.GetString(d, at, n);
        }

        private static string MethodName(IntPtr delegatePtr)
        {
            if (delegatePtr == IntPtr.Zero)
                return "";
            IntPtr method = new Il2CppSystem.Delegate(delegatePtr).method;
            return method == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(IL2CPP.il2cpp_method_get_name(method)) ?? "";
        }

        /// <summary>The opcode and command tables, named from their delegates (once: both are static in the game).</summary>
        private static void ReadTables()
        {
            if (_tablesRead)
                return;
            Il2CppReferenceArray<Scr_AnonymousClass> codefunc = ScrTraceCode.codefunc;
            var table = ScrCommandTable.scrCommandTable;
            if (codefunc == null || table == null)
                return;
            _tablesRead = true;
            OpNames = new string[codefunc.Length];
            Ops = new Op[codefunc.Length];
            bool named = true;
            for (int i = 0; i < codefunc.Length; i++)
            {
                Scr_AnonymousClass e = codefunc[i];
                OpNames[i] = e?.func == null ? "" : MethodName(e.func.Pointer);
                Ops[i] = Enum.TryParse(OpNames[i].Replace("CodeFunc_", ""), out Op op) ? op : Op.Unknown;
                named &= Ops[i] != Op.Unknown;
            }
            if (!named)
            {
                Log.Warn($"{Tag} Opcode names not all known ({string.Join(" ", OpNames)}): the file order is used.");
                Ops = (Op[])FileOrder.Clone();
            }
            // 16-byte value entries (the comm delegate, then paranum), read from the array's memory: the proxy wraps them as references.
            IntPtr a = table.Pointer;
            long n = Marshal.ReadInt64(a, 0x18);
            Commands.Clear();
            for (int k = 0; k < n && k < 4096; k++)
            {
                IntPtr entry = a + 0x20 + k * 16;
                Commands.Add((MethodName(Marshal.ReadIntPtr(entry)), Marshal.ReadInt32(entry, 8)));
            }
            _bitChk = Commands.FindIndex(c => c.Name == "scrCommand_BIT_CHK");
            _bitOn = Commands.FindIndex(c => c.Name == "scrCommand_BIT_ON");
            _bitOff = Commands.FindIndex(c => c.Name == "scrCommand_BIT_OFF");
            _sync = Commands.FindIndex(c => c.Name == "scrCommand_SYNC");
            _message = Commands.FindIndex(c => c.Name == "itfMesMngMessage");
            // The prompt commands: fldCommand_ACT_WIN_DSP, _DSP2, _DSP3 (and any other ACT_WIN command the table holds).
            PromptCommands.Clear();
            for (int k = 0; k < Commands.Count; k++)
            {
                if (Commands[k].Name.StartsWith("fldCommand_ACT_WIN", StringComparison.Ordinal))
                    PromptCommands.Add(k);
            }
            Log.Info($"{Tag} Script machine: {Ops.Length} opcodes, {Commands.Count} commands; BIT_CHK {Hex(_bitChk)}, BIT_ON {Hex(_bitOn)}, BIT_OFF {Hex(_bitOff)}, " +
                     $"SYNC {Hex(_sync)}, message {Hex(_message)}, prompts {string.Join(" ", System.Linq.Enumerable.Select(PromptCommands, Hex))}.");
        }

        private static string Hex(int k) => k < 0 ? "none" : "0x" + k.ToString("X");

        /// <summary>The game's prompt words for prompt number n (Localize of fldPanel.inputpanel_mes0[n]), or "".</summary>
        internal static string PanelText(int n)
        {
            try
            {
                Il2CppStringArray ids = fldPanel.inputpanel_mes0;
                if (ids == null || n < 0 || n >= ids.Length || string.IsNullOrEmpty(ids[n]))
                    return "";
                return TextUtil.Clean(Localize.GetLocalizeText(ids[n]) ?? "").Trim();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>The first line of dialog i, localized, when it is the game's narration ("&gt; It's a statue."): "It's a statue".</summary>
        internal static string Narration(int dialog)
        {
            if (dialog < 0 || dialog >= DialogFirstKey.Count || DialogFirstKey[dialog].Length == 0)
                return "";
            string text;
            try { text = TextUtil.Clean(Localize.GetLocalizeText("<" + DialogFirstKey[dialog] + ">") ?? "").Trim(); }
            catch { return ""; }
            if (!text.StartsWith(">", StringComparison.Ordinal))
                return "";
            return text.TrimStart('>', ' ').Trim().TrimEnd('.');
        }

        internal static string DialogKey(int dialog) => dialog >= 0 && dialog < DialogFirstKey.Count ? DialogFirstKey[dialog] : "";

        /// <summary>Whether the game's on/off table lets this volume fire now (fldEveHit_ChkOn): true when it is not listed.</summary>
        internal static bool VolumeOn(int field, int area, string volume)
        {
            try
            {
                Il2CppReferenceArray<typeFLD_EVEHIT_TBL> tbl = fldEveHit.gFldEveHitOnOffTbl;
                for (int i = 0; tbl != null && i < tbl.Length && i < 32; i++)
                {
                    typeFLD_EVEHIT_TBL e = tbl[i];
                    if (e != null && e.field == field && e.area == area + 1 && e.hitname == volume)
                        return EventBit.evtBitCheck(e.flag);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} On/off table could not be read: {ex.Message}");
            }
            return true;
        }

        private static readonly Dictionary<string, (long At, ScriptVerdict V)> Cache = new Dictionary<string, (long, ScriptVerdict)>();
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        private const long CacheMs = 500;

        /// <summary>What event volume `volume` of area `area` does now; Missing / Unknown when the script or the tables cannot tell.</summary>
        internal static ScriptVerdict Classify(int area, string volume)
        {
            string proc = $"{area + 1:000}_{volume}";
            long now = Clock.ElapsedMilliseconds;
            if (Cache.TryGetValue(proc, out var hit) && now - hit.At < CacheMs)
                return hit.V;
            ScriptVerdict v;
            if (!Ensure())
                v = new ScriptVerdict { Outcome = ScriptOutcome.Missing, Procedure = proc, Reason = LoadNote };
            else if (_bitChk < 0 || PromptCommands.Count == 0)
                v = new ScriptVerdict { Outcome = ScriptOutcome.Unknown, Procedure = proc, Reason = "the flag test or the prompt commands are not named" };
            else if (!ProcByName.TryGetValue(proc, out Proc p))
                v = new ScriptVerdict { Outcome = ScriptOutcome.Missing, Procedure = proc, Reason = "no such procedure" };
            else
            {
                var run = new DryRun();
                v = run.Explore(p.Start, new List<int?>(), null, new List<int>(), 0);
                v.Procedure = proc;
                if (v.Outcome == ScriptOutcome.Action)
                {
                    v.PromptText = PanelText(v.Prompt);
                    if (v.PromptText.Length == 0)
                        v.Outcome = ScriptOutcome.Unknown;   // a prompt the game has no words for: keep the old "Event"
                    else
                        v.Detail = run.DetailAfter(v.ResumeAt, v.ResumeStack);
                }
            }
            Cache[proc] = (now, v);
            return v;
        }

        /// <summary>One classification: the step budget, the branches already split, and its own copy of the flags it set.</summary>
        private sealed class DryRun
        {
            private int _budget = StepBudget;
            private readonly HashSet<int> _split = new HashSet<int>();
            private readonly Dictionary<int, bool> _set = new Dictionary<int, bool>();

            private bool Flag(int f) => _set.TryGetValue(f, out bool v) ? v : EventBit.evtBitCheck(f);

            private static int Rank(ScriptOutcome o) => o == ScriptOutcome.Action ? 3 : o == ScriptOutcome.Story ? 2 : o == ScriptOutcome.Inactive ? 1 : 0;

            internal ScriptVerdict Explore(int pc, List<int?> stack0, int? reg, List<int> calls0, int depth)
            {
                var st = new List<int?>(stack0);
                var calls = new List<int>(calls0);
                var gotos = new Dictionary<int, int>();
                while (_budget-- > 0)
                {
                    if (pc < 0 || pc >= _codeCount)
                        return Verdict(ScriptOutcome.Unknown, $"ran off the code at {pc}");
                    short o = OpAt(pc);
                    int u = (ushort)ArgAt(pc);
                    switch (OpOf(o))
                    {
                        case Op.Proc: pc++; break;
                        case Op.PushIS: st.Add(ArgAt(pc)); pc++; break;
                        case Op.PushI: st.Add(pc + 1 < _codeCount ? WordAt(pc + 1) : (int?)null); pc += 2; break;
                        case Op.PushF: st.Add(null); pc += 2; break;
                        case Op.PushReg: st.Add(reg); pc++; break;
                        case Op.PushSTR: case Op.PushIX: case Op.PushFX: case Op.PushLIX: case Op.PushLFX: st.Add(null); pc++; break;
                        case Op.PopIX: case Op.PopFX: case Op.PopLIX: case Op.PopLFX: Pop(st); pc++; break;
                        case Op.Minus: { int? a = Pop(st); st.Add(a.HasValue ? -a.Value : (int?)null); pc++; break; }
                        case Op.Not: { int? a = Pop(st); st.Add(a.HasValue ? (a.Value == 0 ? 1 : 0) : (int?)null); pc++; break; }
                        case Op.EQ: case Op.NEQ: case Op.Or: case Op.And:
                        {
                            int? b = Pop(st), a = Pop(st);
                            Op op = OpOf(o);
                            st.Add(!a.HasValue || !b.HasValue ? (int?)null
                                : op == Op.EQ ? (a == b ? 1 : 0) : op == Op.NEQ ? (a != b ? 1 : 0)
                                : op == Op.Or ? (a != 0 || b != 0 ? 1 : 0) : (a != 0 && b != 0 ? 1 : 0));
                            pc++;
                            break;
                        }
                        case Op.Add: case Op.Sub: case Op.Mul: case Op.Div: case Op.S: case Op.L: case Op.SE: case Op.LE:
                            Pop(st); Pop(st); st.Add(null); pc++;   // operand order unverified: not evaluated
                            break;
                        case Op.IF:
                        {
                            int? v = Pop(st);
                            int target = Label(u);
                            if (target < 0)
                                return Verdict(ScriptOutcome.Unknown, $"label {u} missing at {pc}");
                            if (v.HasValue)
                            {
                                pc = v.Value != 0 ? pc + 1 : target;   // IF jumps when the value is 0
                                break;
                            }
                            if (depth > MaxBranchDepth || !_split.Add(pc))
                                return Verdict(ScriptOutcome.Unknown, $"branch limit at {pc}");
                            ScriptVerdict yes = Explore(pc + 1, st, reg, calls, depth + 1);
                            ScriptVerdict no = Explore(target, st, reg, calls, depth + 1);
                            return Rank(yes.Outcome) >= Rank(no.Outcome) ? yes : no;
                        }
                        case Op.Goto:
                        {
                            int target = Label(u);
                            if (target < 0)
                                return Verdict(ScriptOutcome.Unknown, $"label {u} missing at {pc}");
                            gotos.TryGetValue(target, out int times);
                            if (times > 50)
                                return Verdict(ScriptOutcome.Inactive, $"a loop at {pc} with nothing to do");
                            gotos[target] = times + 1;
                            pc = target;
                            break;
                        }
                        case Op.Jump:
                            if (u >= ProcsByIndex.Count)
                                return Verdict(ScriptOutcome.Unknown, $"procedure {u} missing at {pc}");
                            pc = ProcsByIndex[u].Start;
                            break;
                        case Op.Call:
                            if (u >= ProcsByIndex.Count)
                                return Verdict(ScriptOutcome.Unknown, $"procedure {u} missing at {pc}");
                            calls.Add(pc + 1);
                            pc = ProcsByIndex[u].Start;
                            break;
                        case Op.End:
                            if (calls.Count == 0)
                                return Verdict(ScriptOutcome.Inactive, $"ends at {pc}");
                            pc = calls[calls.Count - 1];
                            calls.RemoveAt(calls.Count - 1);
                            break;
                        case Op.Comm:
                        {
                            int args = u < Commands.Count ? Commands[u].Params : 0;
                            if (u == _bitChk)
                            {
                                int? f = Pop(st);
                                reg = f.HasValue ? (Flag(f.Value) ? 1 : 0) : (int?)null;
                                pc++;
                                break;
                            }
                            if (u == _bitOn || u == _bitOff)
                            {
                                int? f = Pop(st);
                                if (f.HasValue)
                                    _set[f.Value] = u == _bitOn;
                                pc++;
                                break;
                            }
                            if (PromptCommands.Contains(u))
                            {
                                int? n = st.Count > 0 ? st[st.Count - 1] : null;
                                for (int i = 0; i < Math.Max(1, args); i++)
                                    Pop(st);
                                if (!n.HasValue)
                                    return Verdict(ScriptOutcome.Unknown, $"{CommandName(u)} with an unknown number at {pc}");
                                var a = Verdict(ScriptOutcome.Action, $"{CommandName(u)}({n.Value}) at {pc}");
                                a.Prompt = n.Value;
                                a.ResumeAt = pc + 1;
                                a.ResumeStack = new List<int?>(st);
                                return a;
                            }
                            if (u == _sync)
                                return Verdict(ScriptOutcome.Inactive, $"waits at {pc} with nothing to do (a polling loop)");
                            if (pc + 1 < _codeCount && OpOf(OpAt(pc + 1)) == Op.PushReg)
                            {
                                // A query whose result is read at once (the facing check before a prompt, an item check).
                                for (int i = 0; i < args; i++)
                                    Pop(st);
                                reg = null;
                                pc++;
                                break;
                            }
                            return Verdict(ScriptOutcome.Story, $"{CommandName(u)} at {pc}");
                        }
                        default:
                            return Verdict(ScriptOutcome.Unknown, $"opcode {o} at {pc}");
                    }
                }
                return Verdict(ScriptOutcome.Unknown, "step budget used up");
            }

            /// <summary>From just after the prompt, as if confirmed (its result 1): the first message's narration, or "".</summary>
            internal string DetailAfter(int pc, List<int?> stack0)
            {
                if (_message < 0)
                    return "";
                var st = new List<int?>(stack0);
                int? reg = 1;
                for (int steps = 0; steps < DetailSteps && pc >= 0 && pc < _codeCount; steps++)
                {
                    short o = OpAt(pc);
                    int u = (ushort)ArgAt(pc);
                    switch (OpOf(o))
                    {
                        case Op.PushIS: st.Add(ArgAt(pc)); pc++; break;
                        case Op.PushI: st.Add(pc + 1 < _codeCount ? WordAt(pc + 1) : (int?)null); pc += 2; break;
                        case Op.PushF: st.Add(null); pc += 2; break;
                        case Op.PushReg: st.Add(reg); pc++; break;
                        case Op.EQ: case Op.NEQ:
                        {
                            int? b = Pop(st), a = Pop(st);
                            st.Add(!a.HasValue || !b.HasValue ? (int?)null : (a == b) == (OpOf(o) == Op.EQ) ? 1 : 0);
                            pc++;
                            break;
                        }
                        case Op.IF:
                        {
                            int? v = Pop(st);
                            int target = Label(u);
                            pc = v.HasValue && v.Value == 0 && target >= 0 ? target : pc + 1;   // unknown: as if true
                            break;
                        }
                        case Op.Goto:
                            pc = Label(u);
                            break;
                        case Op.End: case Op.Jump: case Op.Call: case Op.Run: case Op.Unknown:
                            return "";
                        case Op.Comm:
                        {
                            if (u == _message)
                            {
                                int? dialog = st.Count > 0 ? st[st.Count - 1] : null;
                                return dialog.HasValue ? Narration(dialog.Value) : "";
                            }
                            int args = u < Commands.Count ? Commands[u].Params : 0;
                            if (u == _bitChk)
                            {
                                int? f = Pop(st);
                                reg = f.HasValue ? (Flag(f.Value) ? 1 : 0) : (int?)null;
                            }
                            else
                            {
                                for (int i = 0; i < args; i++)
                                    Pop(st);
                                reg = null;
                            }
                            pc++;
                            break;
                        }
                        default:
                        {
                            Op op = OpOf(o);
                            if (op >= Op.PushIX && op <= Op.PushLFX || op == Op.PushSTR)
                                st.Add(null);
                            else if (op >= Op.PopIX && op <= Op.PopLFX)
                                Pop(st);
                            else if (op == Op.Minus || op == Op.Not)
                            {
                                Pop(st); st.Add(null);
                            }
                            else if (op != Op.Proc)
                            {
                                Pop(st); Pop(st); st.Add(null);
                            }
                            pc++;
                            break;
                        }
                    }
                }
                return "";
            }

            private static ScriptVerdict Verdict(ScriptOutcome o, string reason) => new ScriptVerdict { Outcome = o, Reason = reason };

            private static int? Pop(List<int?> st)
            {
                if (st.Count == 0)
                    return null;
                int? v = st[st.Count - 1];
                st.RemoveAt(st.Count - 1);
                return v;
            }
        }
    }
}
