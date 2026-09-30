using System;
using System.Diagnostics;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// Says "Title screen. Press any button." when the title screen waits for a key.
    ///
    /// The prompt on that screen is an image, not text, so this is the mod's own cue, not game text.
    /// Signal (16:49 session, docs/class-index.md "Title screen and config"): the title's version text
    /// (titleUI(Clone)/title/Version, "Ver.1.0.4") is written each time the title screen appears, about
    /// 8 s before the player opens the menu; it is also written when coming back from Config, but then
    /// the title menu is already open. So: the version text is shown, and half a second later the title
    /// menu is still not open. Once per appearance: the cue re-arms when the version text is hidden.
    /// </summary>
    internal static class TitleCue
    {
        private const string Tag = "[TitleCue]";
        private const string VersionPath = "Canvas_UI/titleUI(Clone)/title/Version";
        private const long DelayMs = 500;
        private const long VisibilityPollMs = 250;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static TMP_Text _version;
        private static bool _pending;
        private static bool _cued;
        private static long _writtenAt;
        private static long _lastPoll;

        internal static void Install()
        {
            if (!Config.MenusEnabled.Value)
                return;
            TmpRouter.Register("TitleCue", path => path == VersionPath ? OnVersion : null);
        }

        private static void OnVersion(TMP_Text component, string raw)
        {
            _version = component;
            if (_cued || _pending)
                return;
            _pending = true;
            _writtenAt = Clock.ElapsedMilliseconds;
        }

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            if (_version == null)
                return;
            long now = Clock.ElapsedMilliseconds;
            try
            {
                if (_pending && now - _writtenAt >= DelayMs)
                {
                    _pending = false;
                    if (IsVisible(_version) && !MenuReader.IsActive("Title menu"))
                    {
                        _cued = true;
                        Log.Info($"{Tag} Title screen waits for a key.");
                        SpeechManager.Say("Title screen. Press any button.", interrupt: false, force: true);
                    }
                }
                else if (_cued && now - _lastPoll >= VisibilityPollMs)
                {
                    _lastPoll = now;
                    if (!IsVisible(_version))
                        _cued = false;
                }
            }
            catch (Exception ex)
            {
                _pending = false;
                Log.Error($"{Tag} {ex.Message}");
            }
        }

        private static bool IsVisible(TMP_Text component)
        {
            try
            {
                return component != null && component.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }
    }
}
