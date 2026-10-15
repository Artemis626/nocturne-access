using System;
using System.IO;
using System.Runtime.InteropServices;
using NocturneAccess.Core;

namespace NocturneAccess.Speech
{
    /// <summary>
    /// Short sounds the mod makes itself (0.0.83; Francesco, 15 Oct 2026: a thud on walking into a wall, which this game, unlike
    /// Strange Journey, does not have). Synthesised in memory as a WAV and played through Windows (winmm PlaySound), so no file
    /// ships with the mod and the game's own audio is not touched.
    /// </summary>
    internal static class Earcons
    {
        private const uint SND_ASYNC = 0x0001, SND_NODEFAULT = 0x0002, SND_MEMORY = 0x0004;

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern bool PlaySound(IntPtr pszSound, IntPtr hmod, uint fdwSound);

        private static GCHandle _thud;   // pinned: an asynchronous sound plays from this memory after PlaySound returns
        private static bool _failed;

        /// <summary>A low, short thud: a falling 120 to 55 Hz tone with a soft noise attack, 120 ms.</summary>
        internal static void Thud()
        {
            if (_failed)
                return;
            try
            {
                if (!_thud.IsAllocated)
                    _thud = GCHandle.Alloc(MakeThud(), GCHandleType.Pinned);
                PlaySound(_thud.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
            }
            catch (Exception ex)
            {
                _failed = true;
                Log.Warn($"[Sound] The thud could not be played, sounds off: {ex.Message}");
            }
        }

        private static byte[] MakeThud()
        {
            const int rate = 22050;
            int n = rate * 120 / 1000;
            var samples = new short[n];
            var random = new Random(7);
            double phase = 0, noise = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                double freq = 55 + 65 * Math.Exp(-t * 30);          // a quick fall in pitch
                phase += 2 * Math.PI * freq / rate;
                noise = noise * 0.85 + (random.NextDouble() * 2 - 1) * 0.15;   // low-passed noise for the impact
                double attack = Math.Min(1.0, t / 0.003);
                double body = Math.Exp(-t * 28) * Math.Sin(phase);
                double hit = Math.Exp(-t * 120) * noise * 1.5;
                samples[i] = (short)(Math.Max(-1, Math.Min(1, 0.7 * attack * (body + hit))) * short.MaxValue);
            }
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            w.Write(36 + n * 2);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            w.Write(16);
            w.Write((short)1);          // PCM
            w.Write((short)1);          // mono
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            w.Write(n * 2);
            foreach (short s in samples)
                w.Write(s);
            w.Flush();
            return ms.ToArray();
        }
    }
}
