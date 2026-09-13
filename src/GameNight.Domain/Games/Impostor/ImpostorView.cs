using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.Impostor;

public sealed record ImpostorView : PlayerView
{
    public override string ModeId => ImpostorGameMode.ModeId;

    public override bool YourTurn => IsYourTurn;

    /// <summary>Only once it is over. Until then somebody always has something to hide.</summary>
    public override bool OpenToAll => Phase == "Finished";

    public required string Phase { get; init; }

    public required bool IsYourTurn { get; init; }

    /// <summary>Everyone sees this, impostor included - it is their only foothold.</summary>
    public required string Category { get; init; }

    /// <summary>
    /// The word, or null if you are the impostor. This one nullable field is the entire
    /// game: everything else players do is built on who can see it and who cannot.
    /// </summary>
    public string? SecretWord { get; init; }

    public required bool YouAreTheImpostor { get; init; }

    public required int Round { get; init; }

    public required string CurrentPlayerName { get; init; }

    public required bool CanCallVote { get; init; }

    public required IReadOnlyList<ImpostorClueView> Clues { get; init; }

    public required IReadOnlyList<ImpostorPlayerView> Players { get; init; }

    /// <summary>Your own vote, so the client can show it back. Never anyone else's.</summary>
    public string? YourVote { get; init; }

    public required int VotesCast { get; init; }

    // --- only populated once the game is over ---

    public string? AccusedName { get; init; }

    public string? ImpostorName { get; init; }

    public string? ImpostorGuess { get; init; }

    public bool ImpostorWon { get; init; }

    public string? Outcome { get; init; }
}

public sealed record ImpostorClueView(string DisplayName, int Round, string Word);

public sealed record ImpostorPlayerView(
    string PlayerId,
    string DisplayName,
    /// <summary>You, in this projection. Saves the client guessing which row is its own.</summary>
    bool IsYou,
    bool IsCurrent,
    bool HasVoted,
    /// <summary>Revealed to everyone only at the end.</summary>
    bool WasTheImpostor);
