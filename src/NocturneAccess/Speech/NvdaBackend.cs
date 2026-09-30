using System;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader.Utils;
using NocturneAccess.Core;

namespace NocturneAccess.Speech
{
    /// <summary>
    /// Talks to NVDA directly through its controller client DLL, without Tolk.
    ///
    /// The DLL sits beside smt3hd.exe. The NVDA 2024.1+ controller package names the 64-bit
    /// build nvdaControllerClient.dll (in its x64 folder); older packages called it
    /// nvdaControllerClient64.dll. Both names are tried, first in the game folder, then in UserLibs.
    ///
    /// Every controller function returns a Win32 error code: 0 means success.
    /// </summary>
    internal sealed class NvdaBackend : ISpeechBackend
    {
        private static readonly string[] DllNames = { "nvdaControllerClient.dll", "nvdaControllerClient64.dll" };

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int NoArgFn();

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int TextFn([MarshalAs(UnmanagedType.LPWStr)] string text);

        private readonly NoArgFn _testIfRunning;
        private readonly TextFn _speakText;
        private readonly NoArgFn _cancelSpeech;
        private readonly TextFn _brailleMessage;

        public string Name => "NVDA";

        public bool IsAvailable => _testIfRunning() == 0;

        internal string DllPath { get; }

        private NvdaBackend(string path, IntPtr library)
        {
            DllPath = path;
            _testIfRunning = GetExport<NoArgFn>(library, "nvdaController_testIfRunning");
            _speakText = GetExport<TextFn>(library, "nvdaController_speakText");
            _cancelSpeech = GetExport<NoArgFn>(library, "nvdaController_cancelSpeech");
            _brailleMessage = GetExport<TextFn>(library, "nvdaController_brailleMessage");
        }

        /// <summary>Loads the controller DLL. Returns null if it is missing or unusable.</summary>
        internal static NvdaBackend TryCreate()
        {
            foreach (string dir in new[] { MelonEnvironment.GameRootDirectory, MelonEnvironment.UserLibsDirectory })
            {
                foreach (string name in DllNames)
                {
                    string path = Path.Combine(dir, name);
                    if (!File.Exists(path))
                        continue;

                    if (!NativeLibrary.TryLoad(path, out IntPtr library))
                    {
                        Log.Warn($"Could not load {path}. It must be the 64-bit build of the NVDA controller client.");
                        continue;
                    }

                    try
                    {
                        return new NvdaBackend(path, library);
                    }
                    catch (EntryPointNotFoundException ex)
                    {
                        Log.Warn($"{path} is missing an expected function: {ex.Message}");
                    }
                }
            }

            Log.Info("NVDA controller client not found (looked for nvdaControllerClient.dll and nvdaControllerClient64.dll beside smt3hd.exe).");
            return null;
        }

        public bool Speak(string text, bool interrupt)
        {
            if (interrupt)
                _cancelSpeech();
            return _speakText(text) == 0;
        }

        public void Stop() => _cancelSpeech();

        public void Braille(string text) => _brailleMessage(text);

        private static T GetExport<T>(IntPtr library, string name) where T : Delegate
            => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
    }
}
