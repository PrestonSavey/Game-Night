using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.Bluff;

public sealed record BluffView : PlayerView
{
    public override string ModeId => BluffGameMode.ModeId;

    public override bool YourTurn => IsYourTurn;

    public override bool OpenToAll => Phase is "Reveal" or "Finished";

    public required string Phase { get; init; }

    public required bool IsYourTurn { get; init; }

    public required int QuestionNumber { get; init; }

    public required int TotalQuestions { get; init; }

    public required string Prompt { get; init; }

    /// <summary>Your own lie, so the client can show it back. Never anyone else's.</summary>
    public string? YourLie { get; init; }

    public required int Submitted { get; init; }

    public required int PlayerCount { get; init; }

    public required IReadOnlyList<BluffOptionView> Options { get; init; }

    public string? YourVote { get; init; }

    public required int VotesCast { get; init; }

    /// <summary>Null until the reveal. This is the field the whole game protects.</summary>
    public string? Answer { get; init; }

    public required IReadOnlyList<BluffScoreView> Scores { get; init; }

    /// <summary>Only ever set for the player whose submission bounced.</summary>
    public string? Rejection { get; init; }

    /// <summary>Reveal only: who accidentally typed the real answer.</summary>
    public required IReadOnlyList<string> KnewItNames { get; init; }
}

/// <param name="IsYours">
/// You wrote this one. Told to you and to nobody else - it is what stops you voting for
/// your own lie without revealing to the table which one is yours.
/// </param>
/// <param name="IsTruth">Null until the reveal, for everyone.</param>
public sealed record BluffOptionView(
    string Key,
    string Text,
    bool IsYours,
    bool? IsTruth,
    IReadOnlyList<string> AuthorNames,
    IReadOnlyList<string> VoterNames);

public sealed record BluffScoreView(string PlayerId, string DisplayName, int Score);
