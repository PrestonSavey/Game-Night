using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.Bluff;

/// <param name="Authors">
/// Empty for the true answer. More than one when two players happened to invent the same
/// lie, in which case they share the credit for anyone who falls for it.
/// </param>
public sealed record BluffAnswer(
    string Key,
    string Text,
    IReadOnlyList<PlayerId> Authors,
    bool IsTruth);

public sealed record BluffState : GameState
{
    public required IReadOnlyList<Player> Players { get; init; }

    public required int QuestionNumber { get; init; }

    public required int TotalQuestions { get; init; }

    public required TriviaQuestion Question { get; init; }

    public required IReadOnlyList<TriviaQuestion> Upcoming { get; init; }

    /// <summary>What each player invented this round. Nobody sees anyone else's.</summary>
    public required IReadOnlyDictionary<PlayerId, string> Lies { get; init; }

    /// <summary>Players who typed the real answer while trying to make one up.</summary>
    public required IReadOnlyList<PlayerId> KnewIt { get; init; }

    /// <summary>Built when writing closes: the truth and every lie, shuffled together.</summary>
    public required IReadOnlyList<BluffAnswer> Options { get; init; }

    /// <summary>Player to the option key they picked.</summary>
    public required IReadOnlyDictionary<PlayerId, string> Votes { get; init; }

    public required IReadOnlyDictionary<PlayerId, int> Scores { get; init; }

    public required BluffPhase Phase { get; init; }

    /// <summary>
    /// Why each player's last submission bounced. A dictionary rather than one slot
    /// because everybody writes at once - one shared message would flicker away the
    /// moment somebody else typed something.
    /// </summary>
    public required IReadOnlyDictionary<PlayerId, string> Rejections { get; init; }

    public override bool IsFinished => Phase == BluffPhase.Finished;
}
