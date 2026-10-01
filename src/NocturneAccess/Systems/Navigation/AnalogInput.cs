using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using NocturneAccess.Core;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// Telemetry of the game's analog stick channels, dds3PadManager.GetPadAnalog(padno, stick_lr, xy, cip_no): a byte
    /// centred on 128 (NocturneModernController, MIT, patches the same method). fldPlayerCalc_Nml reads
    /// GetPadAnalog(0, 0, 0/1, cip) for the left stick. 0.0.14 showed the keyboard never moves these channels: keyboard
    /// walking arrives as pad buttons (PadInput). Kept so a run with a gamepad shows its stick in the trace and dump.
    /// </summary>
    internal static class AnalogInput
    {
        private const int Neutral = 128;
        private const int Threshold = 32;

        private sealed class Channel
        {
            public long Calls;
            public byte Last;
            public byte Min = byte.MaxValue, Max = byte.MinValue;   // since the last trace line
        }

        private static readonly object Lock = new object();
        private static readonly Dictionary<(int, int, int, int), Channel> Channels = new Dictionary<(int, int, int, int), Channel>();

        internal static void Install()
        {
            Type i = typeof(int);
            Patcher.Postfix("Navigation analog", typeof(dds3PadManager), nameof(dds3PadManager.GetPadAnalog), new[] { i, i, i, i },
                AccessTools.Method(typeof(AnalogInput), nameof(AfterGetPadAnalog)));
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

        private static void AfterGetPadAnalog(int __0, int __1, int __2, int __3, byte __result)
        {
            try
            {
                lock (Lock)
                {
                    var key = (__0, __1, __2, __3);
                    if (!Channels.TryGetValue(key, out Channel c))
                    {
                        c = new Channel();
                        Channels[key] = c;
                    }
                    c.Calls++;
                    c.Last = __result;
                    if (__result < c.Min) c.Min = __result;
                    if (__result > c.Max) c.Max = __result;
                }
            }
            catch
            {
                // an exception here would reach the game
            }
        }
    }
}
