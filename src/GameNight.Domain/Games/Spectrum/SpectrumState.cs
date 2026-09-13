using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.Spectrum;

/// <summary>
/// Server truth for a game of Spectrum. Contains <see cref="Target"/>, which is exactly
/// the thing most players must not see - hence this never leaving the server.
/// </summary>
public sealed record SpectrumState : GameState
{
    public required IReadOnlyList<Player> Players { get; init; }

    public required int RoundNumber { get; init; }

    public required int TotalRounds { get; init; }

    /// <summary>Index into <see cref="Players"/>. Rotates by one each round.</summary>
    public required int ClueGiverIndex { get; init; }

    public required SpectrumCard Card { get; init; }

    /// <summary>The hidden answer, 1-100. Visible to the clue giver only, until Reveal.</summary>
    public required int Target { get; init; }

    public string? Clue { get; init; }

    public required IReadOnlyDictionary<PlayerId, int> Guesses { get; init; }

    public required IReadOnlyDictionary<PlayerId, int> Scores { get; init; }

    public required SpectrumPhase Phase { get; init; }

    /// <summary>Remaining shuffled cards, so the next round is deterministic from the seed.</summary>
    public required IReadOnlyList<SpectrumCard> Deck { get; init; }

    /// <summary>
    /// Targets for the rounds still to come, drawn in Start.
    ///
    /// They have to be pre-drawn because Step is pure: it is handed a state and an event
    /// and has no random source to reach for. Carrying them here is what makes one seed
    /// reproduce an entire game rather than only its first round.
    /// </summary>
    public required IReadOnlyList<int> UpcomingTargets { get; init; }

    public Player ClueGiver => Players[ClueGiverIndex];

    public IEnumerable<Player> Guessers => Players.Where(p => p.Id != ClueGiver.Id);

    public override bool IsFinished => Phase == SpectrumPhase.Finished;
}
