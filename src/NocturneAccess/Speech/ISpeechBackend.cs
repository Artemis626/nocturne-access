namespace NocturneAccess.Speech
{
    /// <summary>
    /// One way of getting text to the player. SpeechManager owns the active backend and
    /// calls it only from its own worker thread, never from the game's main thread.
    /// </summary>
    internal interface ISpeechBackend
    {
        /// <summary>Short name for logs and the status key, e.g. "NVDA".</summary>
        string Name { get; }

        /// <summary>True when a screen reader is running and can take speech right now.</summary>
        bool IsAvailable { get; }

        /// <summary>Speaks text. interrupt silences current speech first. Returns false if the reader rejected it.</summary>
        bool Speak(string text, bool interrupt);

        /// <summary>Silences current speech.</summary>
        void Stop();

        /// <summary>Shows text on the braille display, if the backend supports it.</summary>
        void Braille(string text) { }

        /// <summary>Releases the backend when the game closes.</summary>
        void Shutdown() { }
    }
}
