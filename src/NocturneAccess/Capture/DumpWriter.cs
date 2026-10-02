using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader.Utils;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// The file and log summary shared by the debug dumps (Shift+F6 NavDump, Shift+F2 BattleDump): lines go to the file
    /// only or to the file and the log summary; each section is guarded on its own, so one failing read does not lose the
    /// rest; Save writes UserData/NocturneAccess/[folder]/[prefix]_[time].txt, logs the summary under the dump's tag and
    /// says "[subject] data saved." or "[subject] dump failed.".
    /// </summary>
    internal sealed class DumpWriter
    {
        private readonly StringBuilder _file = new StringBuilder();
        private readonly List<string> _summary = new List<string>();
        private readonly string _tag, _folder, _prefix, _subject;
        private readonly bool _logEveryLine;

        /// <param name="logEveryLine">The log gets every line of the file, not only the summary lines.</param>
        internal DumpWriter(string tag, string folder, string prefix, string subject, bool logEveryLine = false)
        {
            _tag = tag;
            _folder = Path.Combine(MelonEnvironment.UserDataDirectory, "NocturneAccess", folder);
            _prefix = prefix;
            _subject = subject;
            _logEveryLine = logEveryLine;
        }

        internal void FileOnly(string line) => _file.AppendLine(line);

        internal void Both(string line)
        {
            _file.AppendLine(line);
            _summary.Add(line);
        }

        internal void Section(string name, Action body)
        {
            Both($"== {name}");
            try
            {
                body();
            }
            catch (Exception ex)
            {
                Both($"{name} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        internal void Save()
        {
            string path = "";
            try
            {
                Directory.CreateDirectory(_folder);
                path = Path.Combine(_folder, $"{_prefix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                File.WriteAllText(path, _file.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Error($"{_tag} Could not write the dump file: {ex.Message}");
                path = "";
            }

            if (_logEveryLine)
            {
                foreach (string line in _file.ToString().Split('\n'))
                    if (line.Trim().Length > 0)
                        Log.Info($"{_tag} " + line.TrimEnd('\r'));
            }
            else
            {
                foreach (string line in _summary)
                    Log.Info($"{_tag} " + line);
            }
            Log.Info($"{_tag} Full dump: {(path.Length == 0 ? "not written" : path)}");
            SpeechManager.Say(path.Length == 0 ? $"{_subject} dump failed." : $"{_subject} data saved.", interrupt: true, force: true);
        }
    }
}
