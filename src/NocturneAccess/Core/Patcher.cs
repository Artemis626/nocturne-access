using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace NocturneAccess.Core
{
    /// <summary>
    /// Tolerant, logged Harmony patching (Suikoden text-hooking handoff §2.1 and §2.4).
    ///
    /// - MelonLoader's automatic PatchAll is off ([assembly: HarmonyDontPatchAll] in ModMain):
    ///   one broken patch class would otherwise stop every patch from loading.
    /// - Every hook is applied by hand, one method at a time, and resolved strictly:
    ///   declared on that exact type, with exact parameter types.
    /// - Every attempt is logged as patched, NOT DECLARED or FAILED, so "not found"
    ///   and "never tried" never look the same in the log. A summary line closes the pass.
    /// - A given prefix or postfix is applied to a method once; different features may each add their own.
    /// </summary>
    internal static class Patcher
    {
        private const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private static readonly HashSet<(MethodBase, MethodInfo)> Done = new HashSet<(MethodBase, MethodInfo)>();
        private static HarmonyLib.Harmony _harmony;
        private static int _patched;
        private static int _missing;
        private static int _failed;

        internal static void Init(HarmonyLib.Harmony harmony) => _harmony = harmony;

        /// <summary>Adds a postfix to type.method(args). Returns false, with a log line, if it could not.</summary>
        internal static bool Postfix(string feature, Type type, string method, Type[] args, MethodInfo postfix)
            => Apply(feature, type, method, args, postfix, isPrefix: false);

        /// <summary>Adds a prefix to type.method(args). The prefix must return void: it observes, never skips the original.</summary>
        internal static bool Prefix(string feature, Type type, string method, Type[] args, MethodInfo prefix)
            => Apply(feature, type, method, args, prefix, isPrefix: true);

        private static bool Apply(string feature, Type type, string method, Type[] args, MethodInfo patch, bool isPrefix)
        {
            string kind = isPrefix ? "prefix" : "postfix";
            string signature = $"{type.Name}.{method}({string.Join(", ", args.Select(a => a.Name))})";

            MethodInfo target;
            try
            {
                target = type.GetMethod(method, Declared, null, args, null);
            }
            catch (AmbiguousMatchException)
            {
                target = null;
            }

            if (target == null || target.IsAbstract)
            {
                _missing++;
                Log.Warn($"[Patch] {feature}: {signature} NOT DECLARED");
                return false;
            }

            if (!Done.Add((target, patch)))
                return true;

            try
            {
                var harmonyMethod = new HarmonyMethod(patch);
                if (isPrefix)
                    _harmony.Patch(target, prefix: harmonyMethod);
                else
                    _harmony.Patch(target, postfix: harmonyMethod);
                _patched++;
                Log.Info($"[Patch] {feature}: {signature} {kind} patched");
                return true;
            }
            catch (Exception ex)
            {
                Done.Remove((target, patch));
                _failed++;
                Log.Error($"[Patch] {feature}: {signature} {kind} FAILED: {ex.Message}");
                return false;
            }
        }

        internal static void LogSummary()
        {
            Log.Info($"[Patch] Summary: {_patched} patched, {_missing} not declared, {_failed} failed.");
        }
    }
}
