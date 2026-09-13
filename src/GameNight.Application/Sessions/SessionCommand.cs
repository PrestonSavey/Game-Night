using System.Text.Json;
using GameNight.Application.Contracts;
using GameNight.Domain.Abstractions;

namespace GameNight.Application.Sessions;

/// <summary>
/// Everything the outside world can ask of a session. All of it goes through one channel
/// and is applied by one loop, which is why nothing in GameSession needs a lock.
/// </summary>
public abstract record SessionCommand
{
    public sealed record Join(
        string DisplayName,
        string ConnectionId,
        TaskCompletionSource<JoinOutcome> Reply) : SessionCommand;

    /// <summary>Local play: one device claims every seat in one go.</summary>
    public sealed record JoinMany(
        IReadOnlyList<string> DisplayNames,
        string ConnectionId,
        TaskCompletionSource<JoinManyOutcome> Reply) : SessionCommand;

    public sealed record Rejoin(
        PlayerId Player,
        string? Token,
        string ConnectionId,
        TaskCompletionSource<JoinOutcome> Reply) : SessionCommand;

    public sealed record Disconnected(string ConnectionId) : SessionCommand;

    public sealed record SelectMode(PlayerId Player, string ModeId) : SessionCommand;

    /// <param name="ModeId">
    /// Picking a game and starting it are one gesture in the UI - you tap the tile you
    /// want - so they are one command here rather than two that could interleave.
    /// </param>
    /// <param name="Setting">Whatever the host chose from the game's own dropdown.</param>
    public sealed record StartGame(PlayerId Player, string? ModeId, int? Setting)
        : SessionCommand;

    public sealed record Act(PlayerId Player, JsonElement Payload) : SessionCommand;

    public sealed record TimerElapsed(string TimerId) : SessionCommand;

    /// <summary>
    /// Do nothing, then broadcast. The loop broadcasts after every command, so this is how
    /// a connection that has just been added to the lobby's group asks for the state it
    /// missed - the first LobbyUpdated after a join goes out before the joiner is in the
    /// group, and without this the client waits forever for a message already sent.
    /// </summary>
    public sealed record Resync : SessionCommand;

    public sealed record ReleaseSeat(PlayerId Player) : SessionCommand;
}
