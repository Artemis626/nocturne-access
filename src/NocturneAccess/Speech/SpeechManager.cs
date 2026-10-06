using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NocturneAccess.Core;

namespace NocturneAccess.Speech
{
    /// <summary>
    /// The single entry point for everything the mod says.
    ///
    /// - Every accepted line is mirrored to Latest.log with a [SPEECH] prefix, whatever the backend.
    /// - Dedupe: an identical line repeated within DedupeWindowMs is dropped, unless force is set.
    ///   Hotkeys and cursor movement onto identically named items must pass force.
    /// - Queue (default): the line is spoken after anything already waiting.
    ///   Interrupt: queued lines are dropped and current speech is silenced first.
    /// - All screen reader calls, including choosing the backend, run on one background thread.
    ///   A slow or frozen screen reader can then never stall the game's frame loop.
    /// </summary>
    internal static class SpeechManager
    {
        private static readonly object Gate = new object();
        private static readonly Queue<string> Pending = new Queue<string>();
        private const int MaxPending = 64;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private static ISpeechBackend _backend;
        private static Thread _worker;
        private static bool _running;
        private static bool _cancelRequested;
        private static bool _rejectionReported;

        private static string _lastText;
        private static long _lastTimeMs;

        private static volatile string _backendName = "starting";

        /// <summary>Name of the active backend, for the status key and logs.</summary>
        internal static string BackendName => _backendName;

        /// <summary>The last line accepted for speech. Basis for a future repeat key.</summary>
        internal static string LastSpoken { get; private set; } = string.Empty;

        internal static void Init()
        {
            lock (Gate)
            {
                if (_running)
                    return;
                _running = true;
            }

            _worker = new Thread(WorkerMain)
            {
                IsBackground = true,
                Name = "NocturneAccess.Speech",
            };
            // Tolk and some readers use COM, which expects a single-threaded apartment.
            _worker.SetApartmentState(ApartmentState.STA);
            _worker.Start();
        }

        /// <summary>
        /// Queues text for speech. Safe to call from any thread; normally called from the game thread.
        /// </summary>
        /// <param name="interrupt">Drop queued lines and silence current speech first.</param>
        /// <param name="force">Skip the dedupe check (on-demand keys, repeated menu items).</param>
        internal static void Say(string text, bool interrupt = false, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            text = text.Trim();

            lock (Gate)
            {
                long now = Clock.ElapsedMilliseconds;
                if (!force && text == _lastText && now - _lastTimeMs < Config.DedupeWindowMs.Value)
                {
                    Log.Debug("[SPEECH-DEDUPED] " + text);
                    return;
                }

                _lastText = text;
                _lastTimeMs = now;
                LastSpoken = text;
                Log.Speech(text);

                if (interrupt)
                {
                    Pending.Clear();
                    _cancelRequested = true;
                }
                // A screen reader that stops taking lines must not make the queue grow without end: the oldest go first.
                while (Pending.Count >= MaxPending)
                    Pending.Dequeue();
                Pending.Enqueue(text);
                Monitor.Pulse(Gate);
            }
        }

        /// <summary>Drops queued lines and silences current speech.</summary>
        internal static void Stop()
        {
            lock (Gate)
            {
                Pending.Clear();
                _cancelRequested = true;
                Monitor.Pulse(Gate);
            }
        }

        internal static void Shutdown()
        {
            lock (Gate)
            {
                _running = false;
                Monitor.Pulse(Gate);
            }
            _worker?.Join(1000);
        }

        private static void WorkerMain()
        {
            try
            {
                _backend = SelectBackend(Config.SpeechBackend.Value);
            }
            catch (Exception ex)
            {
                Log.Error("Speech backend selection failed, using log only: " + ex);
                _backend = new LogOnlyBackend();
            }
            _backendName = _backend.Name;

            while (true)
            {
                string text;
                bool cancel;
                lock (Gate)
                {
                    while (_running && !_cancelRequested && Pending.Count == 0)
                        Monitor.Wait(Gate);
                    if (!_running)
                        break;

                    cancel = _cancelRequested;
                    _cancelRequested = false;
                    text = Pending.Count > 0 ? Pending.Dequeue() : null;
                }

                try
                {
                    if (text == null)
                    {
                        _backend.Stop();
                        continue;
                    }

                    bool accepted = _backend.Speak(text, cancel);
                    if (Config.BrailleOutput.Value)
                        _backend.Braille(text);
                    ReportAcceptance(accepted);
                }
                catch (Exception ex)
                {
                    Log.Error($"{_backend.Name} speech call failed: {ex.Message}");
                }
            }

            try
            {
                _backend.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Warn($"{_backend.Name} shutdown failed: {ex.Message}");
            }
        }

        /// <summary>Warns once when the reader starts rejecting speech, and once when it recovers.</summary>
        private static void ReportAcceptance(bool accepted)
        {
            if (!accepted && !_rejectionReported)
            {
                _rejectionReported = true;
                Log.Warn($"{_backend.Name} did not accept speech. Is the screen reader running? Lines are still logged.");
            }
            else if (accepted && _rejectionReported)
            {
                _rejectionReported = false;
                Log.Info($"{_backend.Name} is accepting speech again.");
            }
        }

        /// <summary>
        /// auto: NVDA if it is running; else Tolk if it finds a reader; else NVDA anyway if its DLL
        /// loaded (so starting NVDA after the game still works); else log only.
        /// </summary>
        private static ISpeechBackend SelectBackend(string choice)
        {
            string mode = (choice ?? "auto").Trim().ToLowerInvariant();
            ISpeechBackend chosen;

            switch (mode)
            {
                case "log":
                    chosen = new LogOnlyBackend();
                    break;

                case "nvda":
                    chosen = (ISpeechBackend)NvdaBackend.TryCreate() ?? new LogOnlyBackend();
                    break;

                case "tolk":
                    chosen = (ISpeechBackend)TolkBackend.TryCreate() ?? new LogOnlyBackend();
                    break;

                default:
                    if (mode != "auto")
                        Log.Warn($"Unknown speech backend '{choice}', using auto.");
                    chosen = SelectAuto();
                    break;
            }

            string running = chosen is LogOnlyBackend ? "" : chosen.IsAvailable ? ", screen reader running" : ", screen reader NOT running yet";
            string dll = chosen is NvdaBackend nvda ? $" via {nvda.DllPath}" : "";
            Log.Info($"Speech backend: {chosen.Name}{dll}{running}.");
            return chosen;
        }

        private static ISpeechBackend SelectAuto()
        {
            NvdaBackend nvda = NvdaBackend.TryCreate();
            if (nvda != null && nvda.IsAvailable)
                return nvda;

            TolkBackend tolk = TolkBackend.TryCreate();
            if (tolk != null)
            {
                if (tolk.IsAvailable)
                    return tolk;
                tolk.Shutdown();
            }

            if (nvda != null)
                return nvda;

            return new LogOnlyBackend();
        }
    }
}
