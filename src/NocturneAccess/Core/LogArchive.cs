using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MelonLoader.Utils;

namespace NocturneAccess.Core
{
    /// <summary>
    /// Keeps a copy of each session's MelonLoader log.
    ///
    /// MelonLoader 0.6.1 overwrites MelonLoader/Latest.log on every launch and keeps no history
    /// (the Logs folder its documentation mentions is never created). This copies Latest.log to
    /// UserData/NocturneAccess/Logs/session_[start time].log:
    /// - every 30 seconds while the game runs, on a background thread, so a session that ends
    ///   without a clean quit (crash, killed process, closed console window) is still kept;
    /// - once more when the game closes normally, which also prunes the folder to the newest 20.
    /// </summary>
    internal static class LogArchive
    {
        private const int Keep = 20;
        private const long SnapshotIntervalMs = 30_000;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _lastSnapshotMs;
        private static int _busy;
        private static bool _failureReported;

        internal static string Folder => Path.Combine(MelonEnvironment.UserDataDirectory, "NocturneAccess", "Logs");

        // The archive file of this session, worked out once: Process.GetCurrentProcess() returns a new handle each call,
        // and it was called (and never disposed) on every 30-second snapshot.
        private static string _target;

        private static string Target
        {
            get
            {
                if (_target == null)
                {
                    using (Process self = Process.GetCurrentProcess())
                        _target = Path.Combine(Folder, $"session_{self.StartTime:yyyy-MM-dd_HH-mm-ss}.log");
                }
                return _target;
            }
        }

        /// <summary>Called every frame; starts a background snapshot every 30 seconds.</summary>
        internal static void Tick()
        {
            long now = Clock.ElapsedMilliseconds;
            if (now - _lastSnapshotMs < SnapshotIntervalMs)
                return;
            _lastSnapshotMs = now;

            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                return;

            Task.Run(() =>
            {
                try
                {
                    Copy();
                }
                catch (Exception ex)
                {
                    if (!_failureReported)
                    {
                        _failureReported = true;
                        Log.Warn($"[LogArchive] Snapshot failed: {ex.Message}");
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _busy, 0);
                }
            });
        }

        /// <summary>Final copy when the game closes normally.</summary>
        internal static void ArchiveCurrentSession()
        {
            try
            {
                Log.Info($"[LogArchive] Copying this session's log to {Target}");
                Copy();

                foreach (FileInfo old in new DirectoryInfo(Folder).GetFiles("session_*.log")
                             .OrderByDescending(f => f.Name)
                             .Skip(Keep))
                {
                    old.Delete();
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[LogArchive] Could not archive the log: {ex.Message}");
            }
        }

        private static void Copy()
        {
            string source = Path.Combine(MelonEnvironment.MelonLoaderDirectory, "Latest.log");
            if (!File.Exists(source))
                return;

            Directory.CreateDirectory(Folder);

            // MelonLoader still has Latest.log open for writing, so share read/write access.
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var output = new FileStream(Target, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                input.CopyTo(output);
            }
        }
    }
}
