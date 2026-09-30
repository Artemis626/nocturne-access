using System;
using System.Collections.Generic;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using UnityEngine;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Debug key (default F6): writes every TextMeshPro component currently visible on screen
    /// to the log, sorted by hierarchy path, with its current text. A snapshot of "what the
    /// screen says right now", complementing the probe, which only sees changes.
    ///
    /// Log lines: [UiDump] Canvas/.../name_textTM [TextMeshProUGUI] = "text"
    /// </summary>
    internal static class UiTreeDumper
    {
        private const string Tag = "[UiDump]";

        internal static void Dump()
        {
            var lines = new List<string>();
            int notShown = 0;

            foreach (TMP_Text component in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (component == null)
                    continue;

                // Also excludes prefab assets, which are never active in a scene hierarchy.
                if (!component.isActiveAndEnabled)
                {
                    notShown++;
                    continue;
                }

                string kind = component.GetIl2CppType().Name;
                lines.Add($"{UiUtil.PathOf(component.transform)} [{kind}] = \"{UiUtil.Escape(component.text)}\"");
            }

            lines.Sort(StringComparer.Ordinal);

            Log.Info($"{Tag} ---- begin: {lines.Count} visible text components ----");
            foreach (string line in lines)
                Log.Info($"{Tag} {line}");
            Log.Info($"{Tag} ---- end ({notShown} inactive, disabled or not in a scene) ----");

            SpeechManager.Say($"UI dumped, {lines.Count} texts.", interrupt: true, force: true);
        }
    }
}
