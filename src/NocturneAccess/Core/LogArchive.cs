using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MelonLoader.Utils;

namespace NocturneAccess.Core
{
    /// <summary>
    /// Keeps a copy of each session's MelonLoader log.
    ///
    /// MelonLoader 0.6.1 overwrites MelonLoader/Latest.log on every launch and keeps no history
    /// (the Logs folder its documentation mentions is never created). When the game closes,
    /// this copies Latest.log to UserData/NocturneAccess/Logs/session_[start time].log and keeps
    /// the newest 20, so two test runs in a row never lose the first log.
    /// A crash skips the copy, but a crashed run is still in Latest.log until the next launch.
    /// </summary>
    internal static class LogArchive
    {
        private const int Keep = 20;

        internal static string Folder => Path.Combine(MelonEnvironment.UserDataDirectory, "NocturneAccess", "Logs");

        internal static void ArchiveCurrentSession()
        {
            try
            {
                string source = Path.Combine(MelonEnvironment.MelonLoaderDirectory, "Latest.log");
                if (!File.Exists(source))
                    return;

                Directory.CreateDirectory(Folder);
                DateTime started = Process.GetCurrentProcess().StartTime;
                string target = Path.Combine(Folder, $"session_{started:yyyy-MM-dd_HH-mm-ss}.log");
                Log.Info($"[LogArchive] Copying this session's log to {target}");

                // MelonLoader still has Latest.log open for writing, so share read/write access.
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var output = new FileStream(target, FileMode.Create, FileAccess.Write))
                {
                    input.CopyTo(output);
                }

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
    }
}
