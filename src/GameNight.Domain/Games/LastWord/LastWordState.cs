using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.LastWord;

/// <summary>
/// Server truth for a game of Last Word.
///
/// Almost nothing here is secret, which is the point of building it second: it shares the
/// lobby, the turn order, the timers and the standings with Spectrum while agreeing with
/// it on almost nothing else - round-robin instead of simultaneous, elimination instead of
/// points, a hard clock instead of a soft one.
/// </summary>
public sealed record LastWordState : GameState
{
    /// <summary>Everyone who started, kept for names and final standings.</summary>
    public required IReadOnlyList<Player> Players { get; init; }

    /// <summary>Still in, in turn order.</summary>
    public required IReadOnlyList<PlayerId> Alive { get; init; }

    public required int TurnIndex { get; init; }

    public required LetterPair Pair { get; init; }

    /// <summary>Words already spent on this pair. Cleared when the letters change.</summary>
    public required IReadOnlyList<string> Used { get; init; }

    /// <summary>In the order they went out, so the last one out finishes second.</summary>
    public required IReadOnlyList<PlayerId> KnockedOut { get; init; }

    public required int TurnSeconds { get; init; }

    /// <summary>
    /// Increments on every change of turn, and names the timer for that turn. A timer that
    /// fires late, after its turn has already ended, carries the old token and is ignored -
    /// without this, one slow message could eliminate the wrong player.
    /// </summary>
    public required int TurnToken { get; init; }

    public required LastWordPhase Phase { get; init; }

    /// <summary>Pairs for the rounds still to come, pre-drawn because Step cannot draw.</summary>
    public required IReadOnlyList<LetterPair> UpcomingPairs { get; init; }

    /// <summary>Why the last submission bounced, and whose it was. Shown only to them.</summary>
    public string? Rejection { get; init; }

    public PlayerId? RejectionFor { get; init; }

    public string? JustAccepted { get; init; }

    public PlayerId CurrentPlayer => Alive[TurnIndex];

    public override bool IsFinished => Phase == LastWordPhase.Finished;
}
