using System.Collections.Generic;
using System.Linq;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Battle
{
    /// <summary>
    /// What happened in battle, kept per side's turn ("Player turn", "Enemy turn"): actions, results, damage, weakness
    /// and critical hits, as the battle reader speaks them (Francesco, 2 Oct 2026: "a lot can happen within even just one
    /// turn"). F5 speaks the previous side's turn and the current one so far, so after the enemies act it says what they
    /// did. Kept until the next battle starts, so it can be heard after the results too.
    /// </summary>
    internal static class BattleLog
    {
        private const int KeepRounds = 8;

        private sealed class Round
        {
            public string Header = "";
            public readonly List<string> Lines = new List<string>();
        }

        private static readonly List<Round> Rounds = new List<Round>();

        internal static void NewBattle() => Rounds.Clear();

        /// <summary>A side's turn begins: "Player turn" or "Enemy turn".</summary>
        internal static void NewSide(string header)
        {
            Rounds.Add(new Round { Header = header });
            if (Rounds.Count > KeepRounds)
                Rounds.RemoveAt(0);
        }

        /// <summary>One event, a sentence ending in a full stop.</summary>
        internal static void Add(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;
            if (Rounds.Count == 0)
                NewSide("Battle start");
            string text = line.Trim();
            if (!text.EndsWith(".") && !text.EndsWith("!") && !text.EndsWith("?"))
                text += ".";
            Rounds[Rounds.Count - 1].Lines.Add(text);
        }

        /// <summary>F5.</summary>
        internal static void Speak()
        {
            List<Round> recent = Rounds.Where(r => r.Lines.Count > 0).ToList();
            if (recent.Count == 0)
            {
                SpeechManager.Say("No battle events yet.", interrupt: true, force: true);
                return;
            }
            // The current side's turn, and the one before it.
            List<Round> said = recent.Skip(System.Math.Max(0, recent.Count - 2)).ToList();
            string text = string.Join(" ", said.Select(r => $"{r.Header}: {string.Join(" ", r.Lines)}"));
            Log.Info("[Battle] Log: " + text);
            SpeechManager.Say(text, interrupt: true, force: true);
        }
    }
}
