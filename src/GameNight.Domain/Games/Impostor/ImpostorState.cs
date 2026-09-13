using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.Impostor;

public sealed record ImpostorClue(PlayerId Player, int Round, string Word);

/// <summary>
/// Server truth for a game of Impostor. This is the most secret-heavy state in the app:
/// one player's identity and one word must stay hidden from exactly the right people, and
/// which people that is changes when the game ends.
/// </summary>
public sealed record ImpostorState : GameState
{
    public required IReadOnlyList<Player> Players { get; init; }

    /// <summary>The whole game. Never leaves the server until the very end.</summary>
    public required PlayerId Impostor { get; init; }

    public required SecretWord Secret { get; init; }

    public required int TurnIndex { get; init; }

    /// <summary>Completed passes round the table. A vote needs at least the configured minimum.</summary>
    public required int CompletedRounds { get; init; }

    public required int RoundsBeforeVote { get; init; }

    public required IReadOnlyList<ImpostorClue> Clues { get; init; }

    /// <summary>Who voted for whom. Empty until somebody calls it.</summary>
    public required IReadOnlyDictionary<PlayerId, PlayerId> Votes { get; init; }

    public required ImpostorPhase Phase { get; init; }

    /// <summary>Who the table settled on, once the votes are counted.</summary>
    public PlayerId? Accused { get; init; }

    public string? ImpostorGuess { get; init; }

    public bool ImpostorWon { get; init; }

    /// <summary>Why the game ended, in words the client can show verbatim.</summary>
    public string? Outcome { get; init; }

    public Player Current => Players[TurnIndex];

    public bool CanCallVote => CompletedRounds >= RoundsBeforeVote;

    public override bool IsFinished => Phase == ImpostorPhase.Finished;
}
