using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using NocturneAccess.Core;
using UnityEngine;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Discovery probe (Phase 1): logs calls to candidate methods of the game's message engine
    /// (itfMesManager, ported from the PS2 code) and to the name/help lookups other Nocturne mods use.
    /// The goal is to learn which of them actually fire for dialogue, choices and menus, and with
    /// which arguments, before any real handler is written (Suikoden handoff §8, step 3).
    ///
    /// Every name below comes from the proxy assembly or a reference repo; see docs/class-index.md.
    /// Methods taking a by-ref struct are deliberately left out (Suikoden handoff §2.6).
    ///
    /// Log line: [Probe:Mes] itfMesManager.SetMessage(GameObject:Canvas/.../x, 0, "text") -> "result"
    /// - The same method with the same arguments is logged at most once every 3 seconds.
    /// - A method logging more than 40 lines in a second is muted, with one log line.
    /// </summary>
    internal static class MesProbe
    {
        private const string Tag = "[Probe:Mes]";
        private const long RepeatWindowMs = 3000;
        private const int NoisyLinesPerSecond = 40;

        private sealed class MethodState
        {
            public readonly Dictionary<string, long> LastLogged = new Dictionary<string, long>();
            public long WindowStartMs;
            public int WindowLines;
            public bool Muted;
        }

        private static readonly Dictionary<MethodBase, MethodState> States = new Dictionary<MethodBase, MethodState>();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static int _errors;

        internal static bool Enabled { get; set; }

        internal static void Install()
        {
            MethodInfo call = AccessTools.Method(typeof(MesProbe), nameof(AfterCall));
            MethodInfo text = AccessTools.Method(typeof(MesProbe), nameof(AfterStringCall));

            Type i = typeof(int), s = typeof(string), b = typeof(bool), u = typeof(uint);
            Type go = typeof(GameObject), inst = typeof(instanceMes_tag), sel = typeof(select_tag);
            Type mes = typeof(itfMesManager);

            // Message window: text, speaker, open/close.
            Add(mes, "SetMessage", call, go, i, s);
            Add(mes, "itfMesMngRequestMessage", call, i, i, i, i);
            Add(mes, "itfMesMngGetStrImmediate", text, i, i, i);
            Add(mes, "OpenMessageWindow", call, inst);
            Add(mes, "OpenCheckSpeakerWindow", call, inst);
            Add(mes, "WindowDispOn", call, i);
            Add(mes, "WindowDispOn", call, inst);
            Add(mes, "WindowDispOff", call, b);
            Add(mes, "ForceWindowDispOff", call);
            Add(mes, "ClearMessage", call);
            Add(mes, "ClearSpeaker", call);
            Add(mes, "SwitchSpeaker", call, b, b);
            Add(mes, "GetSpeakerName", call, inst);
            Add(mes, "SetVar", call, inst, i, s, i);
            Add(mes, "itfMesMngSetVarImmediate", call, i, i, s);

            // Choice (select) window: open/close and cursor.
            Add(mes, "SelectWindowDispOn", call, inst);
            Add(mes, "SelectWindowDispOff", call, inst);
            Add(mes, "itfMesMngRequestSelect", call, i, i);
            Add(mes, "itfMesMngSetSelect", call, i, i);
            Add(mes, "ChangeSelection", call, i, i);
            Add(mes, "SelectItemMove", call, sel, i);

            // Name lookups on the message engine.
            Add(mes, "DAT_STR_REF_DVNAME", text, i);
            Add(mes, "DAT_STR_REF_RCNAME", text, i);
            Add(mes, "DAT_STR_REF_SKNAME", text, i);
            Add(mes, "DAT_STR_REF_ITNAME", text, i);
            Add(mes, "DAT_STR_REF_DHNAME", text, i);

            // Name and help lookups patched by NocturneAddNewSkills / MatthiewNocturne.
            Add(typeof(datSkillName), "Get", text, i, i);
            Add(typeof(datItemName), "Get", text, i);
            Add(typeof(datSkillHelp_msg), "Get", text, i);
            Add(typeof(datItemHelp_msg), "Get", text, i);
            Add(typeof(nbHelpProcess), "nbDispText", call, s, s, i, i, u, b);
        }

        private static void Add(Type type, string method, MethodInfo postfix, params Type[] args)
            => Patcher.Postfix("Probe:Mes", type, method, args, postfix);

        private static void AfterCall(MethodBase __originalMethod, object[] __args)
        {
            if (!Enabled)
                return;
            try
            {
                Record(__originalMethod, __args, null, false);
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        }

        private static void AfterStringCall(MethodBase __originalMethod, object[] __args, string __result)
        {
            if (!Enabled)
                return;
            try
            {
                Record(__originalMethod, __args, __result, true);
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        }

        private static void Record(MethodBase method, object[] args, string result, bool hasResult)
        {
            if (!States.TryGetValue(method, out MethodState state))
            {
                state = new MethodState();
                States[method] = state;
            }
            if (state.Muted)
                return;

            string call = $"{method.DeclaringType?.Name}.{method.Name}({string.Join(", ", (args ?? Array.Empty<object>()).Select(Format))})";
            if (hasResult)
                call += " -> " + Format(result);

            long now = Clock.ElapsedMilliseconds;
            if (state.LastLogged.TryGetValue(call, out long last) && now - last < RepeatWindowMs)
                return;
            state.LastLogged[call] = now;

            if (now - state.WindowStartMs > 1000)
            {
                state.WindowStartMs = now;
                state.WindowLines = 0;
            }
            if (++state.WindowLines > NoisyLinesPerSecond)
            {
                state.Muted = true;
                Log.Info($"{Tag} muted {method.DeclaringType?.Name}.{method.Name}: more than {NoisyLinesPerSecond} lines per second.");
                return;
            }

            Log.Info($"{Tag} {call}");
        }

        private static string Format(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string s:
                    return "\"" + UiUtil.Escape(s) + "\"";
                case GameObject g:
                    return "GameObject:" + UiUtil.PathOf(g.transform);
                case Il2CppObjectBase o:
                    return o.GetType().Name;
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static void ReportError(Exception ex)
        {
            if (_errors++ < 5)
                Log.Error($"{Tag} postfix error: {ex.Message}");
        }
    }
}
