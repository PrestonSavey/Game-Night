using GameNight.Application.Contracts;
using GameNight.Application.Lobbies;
using GameNight.Application.Sessions;
using GameNight.Domain.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace GameNight.Api.Hubs;

public sealed class SignalRBroadcaster : ISessionBroadcaster
{
    private readonly IHubContext<GameHub> _hub;

    public SignalRBroadcaster(IHubContext<GameHub> hub) => _hub = hub;

    public Task LobbyUpdated(LobbyCode code, LobbyView view) =>
        _hub.Clients.Group(GameHub.GroupFor(code)).SendAsync("LobbyUpdated", view);

    /// <summary>
    /// Per connection, deliberately. This is the one message that cannot go to a group,
    /// because every player's copy has different things redacted out of it. The player id
    /// rides along so a local-play device can tell its own seats apart.
    /// </summary>
    public Task ViewUpdated(string connectionId, PlayerId player, PlayerView view) =>
        _hub.Clients.Client(connectionId).SendAsync("ViewUpdated", player.ToString(), view);

    public Task Announce(LobbyCode code, string key, object? data) =>
        _hub.Clients.Group(GameHub.GroupFor(code)).SendAsync("Announcement", key, data);

    public Task Rejected(string connectionId, string errorCode, string message) =>
        _hub.Clients.Client(connectionId).SendAsync("Rejected", errorCode, message);
}
