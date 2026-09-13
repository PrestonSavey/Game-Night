using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.Spectrum;

/// <summary>What one player is allowed to see. This is what gets serialized.</summary>
public sealed record SpectrumView : PlayerView
{
    public override string ModeId => SpectrumGameMode.ModeId;

    /// <summary>Your clue if you are giving one, otherwise your guess if you owe one.</summary>
    public override bool YourTurn =>
        Phase == "AwaitingClue" ? YouAreClueGiver
        : Phase == "Guessing" ? !YouAreClueGiver && YourGuess is null
        : false;

    public override bool OpenToAll => Phase is "Reveal" or "Finished";

    public required string Phase { get; init; }

    public required int RoundNumber { get; init; }

    public required int TotalRounds { get; init; }

    public required string LeftLabel { get; init; }

    public required string RightLabel { get; init; }

    public required string ClueGiverName { get; init; }

    public required bool YouAreClueGiver { get; init; }

    /// <summary>Null unless you are the clue giver, or the round has been revealed.</summary>
    public int? Target { get; init; }

    public string? Clue { get; init; }

    public int? YourGuess { get; init; }

    public required IReadOnlyList<SpectrumGuessView> Guesses { get; init; }

    public required IReadOnlyList<SpectrumScoreView> Scores { get; init; }
}

/// <param name="Position">Null until Reveal - you can see THAT someone answered, not what.</param>
public sealed record SpectrumGuessView(
    string PlayerId,
    string DisplayName,
    int? Position,
    bool HasAnswered);

public sealed record SpectrumScoreView(string PlayerId, string DisplayName, int Score);
