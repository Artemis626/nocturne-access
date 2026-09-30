using System;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader.Utils;
using NocturneAccess.Core;

namespace NocturneAccess.Speech
{
    /// <summary>
    /// Optional backend through Tolk (https://github.com/dkager/tolk), for JAWS and other readers.
    /// Only used when Tolk.dll is beside smt3hd.exe. Not shipped by default and not yet tested in game.
    ///
    /// Note: Tolk looks for NVDA as nvdaControllerClient64.dll, so with the newer DLL name
    /// NVDA users should stay on the direct NVDA backend.
    /// Function names and signatures follow Tolk.h; C++ bool is one byte, hence the I1 marshalling.
    /// </summary>
    internal sealed class TolkBackend : ISpeechBackend
    {
        private const string DllName = "Tolk.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VoidFn();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool BoolFn();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr StringFn();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool SpeakFn([MarshalAs(UnmanagedType.LPWStr)] string text, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool BrailleFn([MarshalAs(UnmanagedType.LPWStr)] string text);

        private readonly VoidFn _unload;
        private readonly StringFn _detectScreenReader;
        private readonly BoolFn _hasSpeech;
        private readonly SpeakFn _speak;
        private readonly BrailleFn _braille;
        private readonly BoolFn _silence;

        public string Name { get; }

        public bool IsAvailable => DetectedReader != null && _hasSpeech();

        private string DetectedReader
        {
            get
            {
                IntPtr p = _detectScreenReader();
                return p == IntPtr.Zero ? null : Marshal.PtrToStringUni(p);
            }
        }

        private TolkBackend(IntPtr library)
        {
            VoidFn load = GetExport<VoidFn>(library, "Tolk_Load");
            _unload = GetExport<VoidFn>(library, "Tolk_Unload");
            _detectScreenReader = GetExport<StringFn>(library, "Tolk_DetectScreenReader");
            _hasSpeech = GetExport<BoolFn>(library, "Tolk_HasSpeech");
            _speak = GetExport<SpeakFn>(library, "Tolk_Speak");
            _braille = GetExport<BrailleFn>(library, "Tolk_Braille");
            _silence = GetExport<BoolFn>(library, "Tolk_Silence");
            load();

            string reader = DetectedReader;
            Name = reader == null ? "Tolk" : "Tolk (" + reader + ")";
        }

        /// <summary>Loads and initialises Tolk. Returns null if Tolk.dll is missing or unusable.</summary>
        internal static TolkBackend TryCreate()
        {
            string path = Path.Combine(MelonEnvironment.GameRootDirectory, DllName);
            if (!File.Exists(path))
                return null;

            if (!NativeLibrary.TryLoad(path, out IntPtr library))
            {
                Log.Warn($"Could not load {path}. It must be the 64-bit build of Tolk.");
                return null;
            }

            try
            {
                return new TolkBackend(library);
            }
            catch (EntryPointNotFoundException ex)
            {
                Log.Warn($"{path} is missing an expected function: {ex.Message}");
                return null;
            }
        }

        public bool Speak(string text, bool interrupt) => _speak(text, interrupt);

        public void Stop() => _silence();

        public void Braille(string text) => _braille(text);

        public void Shutdown() => _unload();

        private static T GetExport<T>(IntPtr library, string name) where T : Delegate
            => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
    }
}
