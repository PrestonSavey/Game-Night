namespace GameNight.Domain.Abstractions;

/// <summary>
/// Something the runtime must do on the game's behalf.
///
/// A game never performs a side effect itself. It describes what it wants and the
/// runtime carries it out. That is what lets a test assert "this move should have
/// scheduled a 10 second timer" without a scheduler existing at all.
/// </summary>
public abstract record Effect
{
    /// <summary>Wake the game with a matching TimerFired after <paramref name="Delay"/>.</summary>
    public sealed record ScheduleTimer(string TimerId, TimeSpan Delay) : Effect;

    public sealed record CancelTimer(string TimerId) : Effect;

    /// <summary>An ephemeral message for the clients - a toast, a sound cue. Not state.</summary>
    public sealed record Announce(string Key, object? Data = null) : Effect;

    /// <summary>The game is over. The runtime returns everyone to the lobby.</summary>
    public sealed record Finished(IReadOnlyList<Standing> Standings) : Effect;
}
