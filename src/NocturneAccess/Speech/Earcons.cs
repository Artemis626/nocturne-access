using System;
using System.Collections.Generic;
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

        private static GCHandle _thud, _wayOn;   // pinned: an asynchronous sound plays from this memory after PlaySound returns
        private static bool _failed;

        /// <summary>A low, short thud: a falling 120 to 55 Hz tone with a soft noise attack, 120 ms.</summary>
        internal static void Thud() => Play(ref _thud, MakeThud, "thud");

        /// <summary>
        /// The way-on cue (0.0.122; Francesco, 27 Oct 2026: a tone of its own when an area has a Shift+End way on, unlike the thud
        /// and not high): two soft, rounded notes rising a fourth, D4 then G4 (294, 392 Hz), with a little of the octave for warmth,
        /// 450 ms in all.
        /// </summary>
        internal static void WayOn() => Play(ref _wayOn, MakeWayOn, "way-on tone");

        /// <summary>
        /// The chase pulse (0.0.143; the Third Kalpa's Raidou, repeated faster as he closes in): one soft, low knock, G2 to D3
        /// (98 and 147 Hz, a little D4 on top), 90 ms, quick decay; lower than the way-on tone and unlike the thud's falling pitch.
        /// </summary>
        internal static void ChasePulse() => Play(ref _pulse, MakePulse, "chase pulse");

        private static GCHandle _pulse;

        /// <summary>
        /// The door cue (0.0.167; Francesco, 13 Nov 2026: speech can cut the game's "Open Door" prompt, a sound cannot): two soft
        /// taps on C4 (262 Hz, a little of the octave), 60 ms each, 70 ms apart, like a knock; one pitch, unlike the way-on tone's
        /// rising fourth and higher than the chase pulse.
        /// </summary>
        internal static void Door() => Play(ref _door, MakeDoor, "door tone");

        private static GCHandle _door;

        /// <summary>
        /// The secret-area cue (0.0.168; Francesco, 16 Nov 2026: arriving where only sight tells a special place was reached): three
        /// soft notes rising G3, C4, E4 (196, 262, 330 Hz), 160, 160 and 320 ms, the way-on tone's timbre; a chord unlike every other
        /// cue, and not high.
        /// </summary>
        internal static void Secret() => Play(ref _secret, MakeSecret, "secret tone");

        private static GCHandle _secret;

        /// <summary>
        /// A challenge section left (0.0.169; the Fourth Kalpa's end warps back to B1): C4, G3, C4 (262, 196, 262 Hz), 140, 140 and
        /// 300 ms, soft; a return home, unlike the secret tone's rise.
        /// </summary>
        internal static void SectionExit() => Play(ref _sectionExit, MakeSectionExit, "section exit tone");

        private static GCHandle _sectionExit;

        private static byte[] MakeSectionExit()
        {
            const int rate = 22050;
            var all = new List<short>();
            foreach (var (hz, ms) in new[] { (261.63, 140), (196.0, 140), (261.63, 300) })
            {
                int n = rate * ms / 1000;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / rate;
                    double attack = Math.Min(1.0, t / 0.012);
                    double decay = Math.Exp(-t * 5.0);
                    double release = Math.Min(1.0, (n - i) / (rate * 0.02));
                    double wave = Math.Sin(2 * Math.PI * hz * t) + 0.25 * Math.Sin(2 * Math.PI * 2 * hz * t);
                    all.Add((short)(0.45 / 1.25 * attack * decay * release * wave * short.MaxValue));
                }
            }
            return Wav(all.ToArray(), rate);
        }

        private static byte[] MakeSecret()
        {
            const int rate = 22050;
            var all = new List<short>();
            foreach (var (hz, ms) in new[] { (196.0, 160), (261.63, 160), (329.63, 320) })
            {
                int n = rate * ms / 1000;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / rate;
                    double attack = Math.Min(1.0, t / 0.012);
                    double decay = Math.Exp(-t * 5.0);
                    double release = Math.Min(1.0, (n - i) / (rate * 0.02));
                    double wave = Math.Sin(2 * Math.PI * hz * t) + 0.25 * Math.Sin(2 * Math.PI * 2 * hz * t);
                    all.Add((short)(0.45 / 1.25 * attack * decay * release * wave * short.MaxValue));
                }
            }
            return Wav(all.ToArray(), rate);
        }

        private static byte[] MakeDoor()
        {
            const int rate = 22050;
            var all = new List<short>();
            for (int tap = 0; tap < 2; tap++)
            {
                if (tap == 1)
                    all.AddRange(new short[rate * 70 / 1000]);
                int n = rate * 60 / 1000;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / rate;
                    double attack = Math.Min(1.0, t / 0.004);
                    double decay = Math.Exp(-t * 40.0);
                    double release = Math.Min(1.0, (n - i) / (rate * 0.008));
                    double wave = Math.Sin(2 * Math.PI * 261.63 * t) + 0.3 * Math.Sin(2 * Math.PI * 523.25 * t);
                    all.Add((short)(0.5 / 1.3 * attack * decay * release * wave * short.MaxValue));
                }
            }
            return Wav(all.ToArray(), rate);
        }

        /// <summary>
        /// The Red Temple's alarm (0.0.158; Francesco, 6 Nov 2026: a simple cue when the shadows' alarm goes on, and another when it
        /// goes off): on, two soft low notes falling a fourth, A3 then E3 (220, 165 Hz); off, the same rising, E3 then A3. Lower
        /// than the way-on tone (D4 to G4) and three notes' worth shorter.
        /// </summary>
        internal static void AlarmOn() => Play(ref _alarmOn, () => TwoNotes(220.0, 165.0), "alarm-on tone");

        internal static void AlarmOff() => Play(ref _alarmOff, () => TwoNotes(165.0, 220.0), "alarm-off tone");

        private static GCHandle _alarmOn, _alarmOff;

        /// <summary>
        /// A damage floor ahead or underfoot (0.0.201; Francesco, 26 Nov 2026: the Kagutsuchi Tower's cursed tiles, as the
        /// Labyrinth's): one soft, uneasy note, D3 and E-flat 3 together (147 and 156 Hz, beating, a little of each octave on top),
        /// 280 ms; low like the alarm, but a single wavering note unlike every other cue.
        /// </summary>
        internal static void DamageFloor() => Play(ref _damage, MakeDamage, "damage floor tone");

        private static GCHandle _damage;

        private static byte[] MakeDamage()
        {
            const int rate = 22050;
            int n = rate * 280 / 1000;
            var samples = new short[n];
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                double attack = Math.Min(1.0, t / 0.015);
                double decay = Math.Exp(-t * 4.0);
                double release = Math.Min(1.0, (n - i) / (rate * 0.03));
                double wave = 0.4 * Math.Sin(2 * Math.PI * 146.83 * t) + 0.4 * Math.Sin(2 * Math.PI * 155.56 * t)
                              + 0.1 * Math.Sin(2 * Math.PI * 293.66 * t) + 0.1 * Math.Sin(2 * Math.PI * 311.13 * t);
                samples[i] = (short)(0.55 * attack * decay * release * wave * short.MaxValue);
            }
            return Wav(samples, rate);
        }

        private static byte[] TwoNotes(double first, double second)
        {
            const int rate = 22050;
            var all = new List<short>();
            foreach (var (hz, ms) in new[] { (first, 170), (second, 260) })
            {
                int n = rate * ms / 1000;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / rate;
                    double attack = Math.Min(1.0, t / 0.012);
                    double decay = Math.Exp(-t * 5.0);
                    double release = Math.Min(1.0, (n - i) / (rate * 0.02));
                    double wave = Math.Sin(2 * Math.PI * hz * t) + 0.3 * Math.Sin(2 * Math.PI * 2 * hz * t);   // the octave: audible on small speakers
                    all.Add((short)(0.5 / 1.3 * attack * decay * release * wave * short.MaxValue));
                }
            }
            return Wav(all.ToArray(), rate);
        }

        private static byte[] MakePulse()
        {
            const int rate = 22050;
            int n = rate * 90 / 1000;
            var samples = new short[n];
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                double attack = Math.Min(1.0, t / 0.004);
                double decay = Math.Exp(-t * 35);
                double release = Math.Min(1.0, (n - i) / (rate * 0.01));
                double wave = 0.5 * Math.Sin(2 * Math.PI * 98 * t) + 0.35 * Math.Sin(2 * Math.PI * 146.83 * t) + 0.15 * Math.Sin(2 * Math.PI * 293.66 * t);   // D4 on top: audible on small speakers
                samples[i] = (short)(0.6 * attack * decay * release * wave * short.MaxValue);
            }
            return Wav(samples, rate);
        }

        private static void Play(ref GCHandle handle, Func<byte[]> make, string what)
        {
            if (_failed)
                return;
            try
            {
                if (!handle.IsAllocated)
                    handle = GCHandle.Alloc(make(), GCHandleType.Pinned);
                PlaySound(handle.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
            }
            catch (Exception ex)
            {
                _failed = true;
                Log.Warn($"[Sound] The {what} could not be played, sounds off: {ex.Message}");
            }
        }

        private static byte[] MakeWayOn()
        {
            const int rate = 22050;
            var notes = new[] { (Hz: 293.66, Ms: 180), (Hz: 392.0, Ms: 270) };
            var all = new List<short>();
            foreach (var (hz, ms) in notes)
            {
                int n = rate * ms / 1000;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / rate;
                    double attack = Math.Min(1.0, t / 0.012);                 // no click
                    double decay = Math.Exp(-t * 6.0);                         // a soft, bell-like fall
                    double release = Math.Min(1.0, (n - i) / (rate * 0.02));   // and no click at the end
                    double wave = Math.Sin(2 * Math.PI * hz * t) + 0.25 * Math.Sin(2 * Math.PI * 2 * hz * t);
                    all.Add((short)(0.45 / 1.25 * attack * decay * release * wave * short.MaxValue));
                }
            }
            return Wav(all.ToArray(), rate);
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
            return Wav(samples, rate);
        }

        /// <summary>16-bit mono PCM samples as a WAV file in memory.</summary>
        private static byte[] Wav(short[] samples, int rate)
        {
            int n = samples.Length;
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
