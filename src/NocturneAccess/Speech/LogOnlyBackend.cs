namespace NocturneAccess.Speech
{
    /// <summary>
    /// Fallback when no screen reader can be reached. Says nothing; SpeechManager still
    /// writes every line to Latest.log with the [SPEECH] prefix, so testing never breaks.
    /// </summary>
    internal sealed class LogOnlyBackend : ISpeechBackend
    {
        public string Name => "log only";

        public bool IsAvailable => true;

        public bool Speak(string text, bool interrupt) => true;

        public void Stop() { }
    }
}
