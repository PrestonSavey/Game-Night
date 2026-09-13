using GameNight.Application.Contracts;
using GameNight.Application.Lobbies;
using GameNight.Domain.Abstractions;

namespace GameNight.Application.Sessions;

/// <summary>
/// The runtime's view of the transport. Implemented by SignalR in the Api project, which
/// is the only reason this layer can be unit tested without a server.
/// </summary>
public interface ISessionBroadcaster
{
    /// <summary>Same payload for everybody, so it can go to the lobby's group.</summary>
    Task LobbyUpdated(LobbyCode code, LobbyView view);

    /// <summary>
    /// Per player, never to a group: every player's projection is different, and that
    /// difference is the whole hidden-information mechanism.
    ///
    /// The player id travels with the view because in local play one connection holds
    /// several seats and has to be able to tell the projections apart.
    /// </summary>
    Task ViewUpdated(string connectionId, PlayerId player, PlayerView view);

    Task Announce(LobbyCode code, string key, object? data);

    Task Rejected(string connectionId, string errorCode, string message);
}
