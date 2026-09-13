namespace GameNight.Domain.Games.Spectrum;

public enum SpectrumPhase
{
    /// <summary>The clue giver can see the target and is thinking of a clue.</summary>
    AwaitingClue,

    /// <summary>Everyone else places the dial. Guesses stay hidden from each other.</summary>
    Guessing,

    /// <summary>The target is shown to everyone alongside all the guesses.</summary>
    Reveal,

    Finished,
}
