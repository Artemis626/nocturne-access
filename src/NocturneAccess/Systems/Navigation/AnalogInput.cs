using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Watches and, on request, drives the game's analog stick channels.
    ///
    /// 0.0.13 showed that walking does not come from padcheck(CMN_UpFront): 15 injected DOWN answers moved
    /// nothing. dds3PadManager.GetPadAnalog(padno, stick_lr, xy, cip_no) returns a byte centred on 128
    /// (NocturneModernController, MIT: its camera reads GetPadAnalog(0, 1, x/y, 1), and its telemetry and
    /// right-stick patches are Harmony postfixes on this method). The left stick is the likely walking input.
    ///
    /// Rather than guess the channel and its polarity, this learns them: while the player walks forward with
    /// their own key (gKeyInputDir 0, fldPlayerAct 1), every channel that answered away from 128 within the
    /// last 100 ms is stored as the forward profile. The step test replays that profile. Channels are logged
    /// in the trace either way, so a run also shows whether the keyboard goes through this method at all.
    /// Injection happens only while exploring.
    /// </summary>
    internal static class AnalogInput
    {
        private const int Neutral = 128;
        private const int Threshold = 32;
        private const long RecentMs = 100;

        private sealed class Channel
        {
            public long Calls;
            public byte Last;
            public byte Min = byte.MaxValue, Max = byte.MinValue;   // since the last trace line
            public long LastActiveMs = long.MinValue / 2;
            public byte LastActive;
        }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly object Lock = new object();
        private static readonly Dictionary<(int, int, int, int), Channel> Channels = new Dictionary<(int, int, int, int), Channel>();
        private static Dictionary<(int, int, int, int), byte> _forward = new Dictionary<(int, int, int, int), byte>();
        private static Dictionary<(int, int, int, int), byte> _inject;

        internal static bool HasForwardProfile => _forward.Count > 0;

        internal static string ForwardProfileText => Describe(_forward);

        internal static long Injected { get; private set; }

        internal static void Install()
        {
            Type i = typeof(int);
            Patcher.Postfix("Navigation analog", typeof(dds3PadManager), nameof(dds3PadManager.GetPadAnalog), new[] { i, i, i, i },
                AccessTools.Method(typeof(AnalogInput), nameof(AfterGetPadAnalog)));
        }

        /// <summary>Starts replaying the learned forward profile. Returns false when there is none yet.</summary>
        internal static bool StartForward()
        {
            if (_forward.Count == 0)
                return false;
            _inject = new Dictionary<(int, int, int, int), byte>(_forward);
            return true;
        }

        internal static void Stop() => _inject = null;

        /// <summary>Called once per frame: learns the forward profile while the player walks forward by hand.</summary>
        internal static void Tick()
        {
            if (_inject != null || !FieldState.IsExploring || fldPlayer.gKeyInputDir != 0 || fldPlayer.fldPlayerAct != 1)
                return;

            long now = Clock.ElapsedMilliseconds;
            Dictionary<(int, int, int, int), byte> active;
            lock (Lock)
            {
                active = Channels.Where(kv => now - kv.Value.LastActiveMs <= RecentMs)
                                 .ToDictionary(kv => kv.Key, kv => kv.Value.LastActive);
            }
            if (active.Count == 0)
                return;

            bool changed = active.Count != _forward.Count || active.Any(kv => !_forward.TryGetValue(kv.Key, out byte v) || v != kv.Value);
            if (!changed)
                return;
            _forward = active;
            Log.Info($"[Nav:Analog] Forward profile learned while walking forward: {Describe(active)}");
        }

        /// <summary>Channels that left neutral since the last call, as "pad,stick,xy,cip=min..max", then reset.</summary>
        internal static string TakeActivity()
        {
            var parts = new List<string>();
            lock (Lock)
            {
                foreach (KeyValuePair<(int, int, int, int), Channel> kv in Channels)
                {
                    Channel c = kv.Value;
                    if (c.Min != byte.MaxValue && (Math.Abs(c.Min - Neutral) > Threshold || Math.Abs(c.Max - Neutral) > Threshold))
                        parts.Add($"{kv.Key.Item1},{kv.Key.Item2},{kv.Key.Item3},{kv.Key.Item4}={c.Min}..{c.Max}");
                    c.Min = byte.MaxValue;
                    c.Max = byte.MinValue;
                }
            }
            return string.Join(" ", parts);
        }

        /// <summary>Every channel the game asked about, with call counts and last value (for the dump).</summary>
        internal static IEnumerable<string> ChannelLines()
        {
            lock (Lock)
            {
                return Channels.OrderBy(kv => kv.Key)
                    .Select(kv => $"GetPadAnalog({kv.Key.Item1}, {kv.Key.Item2}, {kv.Key.Item3}, {kv.Key.Item4}): {kv.Value.Calls} calls, last {kv.Value.Last}")
                    .ToList();
            }
        }

        private static void AfterGetPadAnalog(int __0, int __1, int __2, int __3, ref byte __result)
        {
            try
            {
                var key = (__0, __1, __2, __3);
                Dictionary<(int, int, int, int), byte> inject = _inject;
                if (inject != null && inject.TryGetValue(key, out byte forced) && FieldState.IsExploring)
                {
                    __result = forced;
                    Injected++;
                    return;   // injected values are never learned
                }

                lock (Lock)
                {
                    if (!Channels.TryGetValue(key, out Channel c))
                    {
                        c = new Channel();
                        Channels[key] = c;
                    }
                    c.Calls++;
                    c.Last = __result;
                    if (__result < c.Min) c.Min = __result;
                    if (__result > c.Max) c.Max = __result;
                    if (Math.Abs(__result - Neutral) > Threshold)
                    {
                        c.LastActiveMs = Clock.ElapsedMilliseconds;
                        c.LastActive = __result;
                    }
                }
            }
            catch
            {
                // an exception here would reach the game
            }
        }

        private static string Describe(Dictionary<(int, int, int, int), byte> d) =>
            d.Count == 0 ? "none" : string.Join(" ", d.Select(kv => $"{kv.Key.Item1},{kv.Key.Item2},{kv.Key.Item3},{kv.Key.Item4}={kv.Value}"));
    }
}
