using System.Text.Json;
using GameNight.Application.Contracts;
using GameNight.Application.Games;
using GameNight.Application.Lobbies;
using GameNight.Application.Security;
using GameNight.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace GameNight.Application.Sessions;

/// <summary>What the transport is allowed to ask for. Everything else is inside a session.</summary>
public interface ISessionRuntime
{
    Task<(LobbyCode Code, JoinOutcome Outcome)> CreateLobbyAsync(string displayName, string connectionId);

    /// <summary>Local play: one device opens a room and sits down in every chair.</summary>
    Task<(LobbyCode Code, JoinManyOutcome Outcome)> CreateLocalLobbyAsync(
        IReadOnlyList<string> displayNames, string connectionId);

    Task<JoinOutcome> JoinAsync(LobbyCode code, string displayName, string connectionId);

    Task<JoinOutcome> ResumeAsync(LobbyCode code, PlayerId player, string? token, string connectionId);

    Task DisconnectedAsync(LobbyCode code, string connectionId);

    /// <summary>Re-send current state, for a connection that has just joined the group.</summary>
    Task ResyncAsync(LobbyCode code);

    Task SelectModeAsync(LobbyCode code, PlayerId player, string modeId);

    Task StartGameAsync(LobbyCode code, PlayerId player, string? modeId, int? setting);

    Task ActAsync(LobbyCode code, PlayerId player, JsonElement payload);
}

public sealed class SessionRuntime : ISessionRuntime
{
    private readonly ISessionStore _store;
    private readonly ILobbyCodeGenerator _codes;
    private readonly IGameModeRegistry _modes;
    private readonly ISessionBroadcaster _broadcast;
    private readonly IRejoinTokenService _tokens;
    private readonly ILoggerFactory _loggers;

    public SessionRuntime(
        ISessionStore store,
        ILobbyCodeGenerator codes,
        IGameModeRegistry modes,
        ISessionBroadcaster broadcast,
        IRejoinTokenService tokens,
        ILoggerFactory loggers)
    {
        _store = store;
        _codes = codes;
        _modes = modes;
        _broadcast = broadcast;
        _tokens = tokens;
        _loggers = loggers;
    }

    public async Task<(LobbyCode, JoinOutcome)> CreateLobbyAsync(string displayName, string connectionId)
    {
        var code = NextFreeCode();
        var session = NewSession(code);
        _store.Add(session);

        var outcome = await JoinCoreAsync(session, displayName, connectionId);
        return (code, outcome);
    }

    public async Task<(LobbyCode, JoinManyOutcome)> CreateLocalLobbyAsync(
        IReadOnlyList<string> displayNames, string connectionId)
    {
        var code = NextFreeCode();
        var session = NewSession(code);
        _store.Add(session);

        var reply = new TaskCompletionSource<JoinManyOutcome>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await session.PostAsync(new SessionCommand.JoinMany(displayNames, connectionId, reply));
        return (code, await reply.Task);
    }

    public async Task<JoinOutcome> JoinAsync(LobbyCode code, string displayName, string connectionId)
    {
        if (!_store.TryGet(code, out var session))
        {
            return JoinOutcome.Fail(RejectReasons.LobbyNotFound, "No lobby with that code.");
        }

        return await JoinCoreAsync(session, displayName, connectionId);
    }

    public async Task<JoinOutcome> ResumeAsync(
        LobbyCode code, PlayerId player, string? token, string connectionId)
    {
        if (!_store.TryGet(code, out var session))
        {
            return JoinOutcome.Fail(RejectReasons.LobbyNotFound, "That lobby has closed.");
        }

        var reply = new TaskCompletionSource<JoinOutcome>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await session.PostAsync(new SessionCommand.Rejoin(player, token, connectionId, reply));
        return await reply.Task;
    }

    public Task DisconnectedAsync(LobbyCode code, string connectionId) =>
        Post(code, new SessionCommand.Disconnected(connectionId));

    public Task ResyncAsync(LobbyCode code) => Post(code, new SessionCommand.Resync());

    public Task SelectModeAsync(LobbyCode code, PlayerId player, string modeId) =>
        Post(code, new SessionCommand.SelectMode(player, modeId));

    public Task StartGameAsync(LobbyCode code, PlayerId player, string? modeId, int? setting) =>
        Post(code, new SessionCommand.StartGame(player, modeId, setting));

    public Task ActAsync(LobbyCode code, PlayerId player, JsonElement payload) =>
        Post(code, new SessionCommand.Act(player, payload));

    private async Task<JoinOutcome> JoinCoreAsync(
        GameSession session, string displayName, string connectionId)
    {
        var reply = new TaskCompletionSource<JoinOutcome>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await session.PostAsync(new SessionCommand.Join(displayName, connectionId, reply));
        return await reply.Task;
    }

    private async Task Post(LobbyCode code, SessionCommand command)
    {
        if (_store.TryGet(code, out var session))
        {
            await session.PostAsync(command);
        }
    }

    private GameSession NewSession(LobbyCode code) =>
        new(code, _modes, _broadcast, _tokens, _loggers.CreateLogger<GameSession>());

    private LobbyCode NextFreeCode()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var candidate = _codes.Next();
            if (!_store.TryGet(candidate, out _)) return candidate;
        }

        // 32 collisions in a 32^4 space means something is very wrong, or we are
        // hosting a much bigger party than this design is for.
        throw new InvalidOperationException("Could not allocate a free lobby code.");
    }
}
