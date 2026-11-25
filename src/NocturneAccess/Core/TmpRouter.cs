using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppTMPro;
using NocturneAccess.Capture;

namespace NocturneAccess.Core
{
    /// <summary>
    /// The single hook on TextMeshPro text writes, shared by every reader and the probe.
    ///
    /// Cost control (the game writes some texts every frame):
    /// - Blank writes return at once, before any call into the game. This drops the 116
    ///   UIManager/Text components that hold a single space and are rewritten every frame.
    /// - Each component is classified once, by its hierarchy path, and the result (a handler or
    ///   nothing) is cached by instance ID. Later writes cost one ID lookup.
    ///
    /// Readers register a classifier: given a component path, return a handler or null.
    /// </summary>
    internal static class TmpRouter
    {
        internal delegate void Handler(TMP_Text component, string raw);

        private static readonly List<(string Name, Func<string, Handler> Classify)> Classifiers = new List<(string, Func<string, Handler>)>();
        private static readonly Dictionary<int, Handler> Cache = new Dictionary<int, Handler>();
        private static int _errors;
        private const int MaxCached = 20000;

        internal static void Register(string name, Func<string, Handler> classifier) => Classifiers.Add((name, classifier));

        internal static void Install()
        {
            Type tmp = typeof(TMP_Text);
            var postfix = AccessTools.Method(typeof(TmpRouter), nameof(AfterSetText));
            Patcher.Postfix("Text", tmp, "set_text", new[] { typeof(string) }, postfix);
            Patcher.Postfix("Text", tmp, "SetText", new[] { typeof(string) }, postfix);
            Patcher.Postfix("Text", tmp, "SetText", new[] { typeof(string), typeof(bool) }, postfix);
        }

        private static void AfterSetText(TMP_Text __instance, string __0)
        {
            if (string.IsNullOrWhiteSpace(__0) || __instance == null)
                return;

            try
            {
                int id = __instance.GetInstanceID();
                if (!Cache.TryGetValue(id, out Handler handler))
                {
                    // The game makes new UI copies ("(Clone)") for every battle, save screen and menu, each with new IDs,
                    // so the cache would grow all session. Starting over only costs one classification per component.
                    if (Cache.Count >= MaxCached)
                    {
                        Log.Info($"[Text] Classification cache reached {MaxCached} components: cleared.");
                        Cache.Clear();
                    }
                    handler = Classify(__instance);
                    Cache[id] = handler;
                }

                handler?.Invoke(__instance, __0);

                if (TmpTextProbe.Enabled)
                    TmpTextProbe.Record(__instance, id, __0);
            }
            catch (Exception ex)
            {
                if (_errors++ < 5)
                    Log.Error($"[Text] postfix error: {ex.Message}");
            }
        }

        private static Handler Classify(TMP_Text component)
        {
            if (Classifiers.Count == 0)
                return null;

            string path = UiUtil.PathOf(component.transform);
            // The mod's own settings window speaks for itself (0.0.194): never a reader's text.
            if (path.StartsWith(Systems.Menus.ModSettings.RootName, StringComparison.Ordinal))
                return null;
            foreach ((string name, Func<string, Handler> classify) in Classifiers)
            {
                Handler handler = classify(path);
                if (handler != null)
                {
                    Log.Info($"[Text] {path} -> {name}");
                    return handler;
                }
            }
            return null;
        }
    }
}
