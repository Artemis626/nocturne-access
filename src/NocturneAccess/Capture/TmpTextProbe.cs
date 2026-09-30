using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppTMPro;
using NocturneAccess.Core;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Discovery probe (Phase 1): logs every string the game writes into a TextMeshPro component,
    /// with the component's hierarchy path and real type. This is the game's own text, as handed
    /// to the UI; nothing is reconstructed.
    ///
    /// Log line: [Probe:TMP] Canvas/.../name_textTM [TextMeshProUGUI] (hidden) (12 writes) = "text"
    ///
    /// - Settle: a component's text is logged once it has not changed for 200 ms, so a typewriter
    ///   effect that grows a line gives one line with a write count, not one line per letter.
    /// - "(hidden)": the object was inactive in the hierarchy when written (prefab placeholders,
    ///   alternative layouts). Useful to learn which components never show.
    /// - A component changing more than 30 times a second to unrelated text is muted, with one log line.
    /// - Fed by Core/TmpRouter (blank writes never arrive). Texts are compared and logged with the
    ///   typewriter's &lt;alpha=#00&gt; tags removed; other tags (fonts, colours) are kept.
    /// </summary>
    internal static class TmpTextProbe
    {
        private const string Tag = "[Probe:TMP]";
        private const long SettleMs = 200;
        private const int NoisyChangesPerSecond = 30;

        private sealed class Entry
        {
            public string LoggedText;
            public string PendingText;
            public string Path;
            public string Kind;
            public bool Active;
            public int Writes;
            public long LastChangeMs;
            public long WindowStartMs;
            public int WindowChanges;
            public bool Queued;
            public bool Muted;
        }

        private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();
        private static readonly List<Entry> Queue = new List<Entry>();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        internal static bool Enabled { get; set; }

        /// <summary>Called by TmpRouter for every non-blank write while the probe is enabled.</summary>
        internal static void Record(TMP_Text component, int id, string raw)
        {
            // Compare without the typewriter's alpha tags, so revealing a line is not "new text".
            string text = TextUtil.RemoveTypewriter(raw);
            if (!Entries.TryGetValue(id, out Entry e))
            {
                e = new Entry();
                Entries[id] = e;
            }
            if (e.Muted)
                return;

            string current = e.PendingText ?? e.LoggedText;
            if (text == current)
                return;

            long now = Clock.ElapsedMilliseconds;
            bool growth = e.PendingText != null
                          && text.Length > e.PendingText.Length
                          && text.StartsWith(e.PendingText, StringComparison.Ordinal);

            if (!growth)
            {
                if (e.PendingText != null)
                    Flush(e);

                if (now - e.WindowStartMs > 1000)
                {
                    e.WindowStartMs = now;
                    e.WindowChanges = 0;
                }
                if (++e.WindowChanges > NoisyChangesPerSecond)
                {
                    e.Muted = true;
                    Log.Info($"{Tag} muted {e.Path}: more than {NoisyChangesPerSecond} changes per second.");
                    return;
                }

                e.Path = UiUtil.PathOf(component.transform);
                e.Kind = component.GetIl2CppType().Name;
                e.Writes = 0;
            }

            e.PendingText = text;
            e.Writes++;
            e.LastChangeMs = now;
            e.Active = component.gameObject.activeInHierarchy;

            if (!e.Queued)
            {
                e.Queued = true;
                Queue.Add(e);
            }
        }

        /// <summary>Called every frame: logs texts that have settled.</summary>
        internal static void Tick()
        {
            if (Queue.Count == 0)
                return;

            long now = Clock.ElapsedMilliseconds;
            int kept = 0;
            for (int i = 0; i < Queue.Count; i++)
            {
                Entry e = Queue[i];
                if (e.PendingText != null && now - e.LastChangeMs < SettleMs)
                {
                    Queue[kept++] = e;
                    continue;
                }
                if (e.PendingText != null)
                    Flush(e);
                e.Queued = false;
            }
            Queue.RemoveRange(kept, Queue.Count - kept);
        }

        internal static void FlushAll()
        {
            foreach (Entry e in Queue)
            {
                if (e.PendingText != null)
                    Flush(e);
                e.Queued = false;
            }
            Queue.Clear();
        }

        private static void Flush(Entry e)
        {
            string hidden = e.Active ? "" : " (hidden)";
            string writes = e.Writes > 1 ? $" ({e.Writes} writes)" : "";
            Log.Info($"{Tag} {e.Path} [{e.Kind}]{hidden}{writes} = \"{UiUtil.Escape(e.PendingText)}\"");
            e.LoggedText = e.PendingText;
            e.PendingText = null;
        }
    }
}
