using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.LastWord;

public sealed record LastWordView : PlayerView
{
    public override string ModeId => LastWordGameMode.ModeId;

    public override bool YourTurn => IsYourTurn;

    /// <summary>Nothing here is worth gating a ten-second turn over.</summary>
    public override bool OpenToAll => Phase == "Finished";

    public required string Phase { get; init; }

    /// <summary>Backing value for <see cref="YourTurn"/>.</summary>
    public required bool IsYourTurn { get; init; }

    public required char StartLetter { get; init; }

    public required char EndLetter { get; init; }

    public required string CurrentPlayerName { get; init; }

    public required bool YouAreOut { get; init; }

    public required int TurnSeconds { get; init; }

    /// <summary>
    /// The client restarts its countdown whenever this changes. Deliberately not an
    /// absolute deadline: Step is pure and has no clock to read one from. The server still
    /// owns the actual elimination, so the worst case is a countdown a round-trip out of
    /// step with a timer that decides the outcome anyway.
    /// </summary>
    public required int TurnToken { get; init; }

    public required IReadOnlyList<string> Used { get; init; }

    public string? JustAccepted { get; init; }

    /// <summary>Only ever populated for the player whose word was rejected.</summary>
    public string? Rejection { get; init; }

    public required IReadOnlyList<LastWordPlayerView> Players { get; init; }
}

public sealed record LastWordPlayerView(
    string PlayerId,
    string DisplayName,
    bool IsAlive,
    bool IsCurrent);
