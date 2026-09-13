using System.Collections.Concurrent;
using GameNight.Application.Lobbies;

namespace GameNight.Application.Sessions;

/// <summary>
/// Deliberately in-process. Sessions are small, short lived and chatty, and a party has
/// tens of players rather than tens of thousands. This interface is the seam where a
/// Redis-backed store and a SignalR backplane would go if that ever changed.
/// </summary>
public interface ISessionStore
{
    bool TryGet(LobbyCode code, out GameSession session);

    void Add(GameSession session);

    void Remove(LobbyCode code);

    IReadOnlyCollection<GameSession> All();
}

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<LobbyCode, GameSession> _sessions = new();

    public bool TryGet(LobbyCode code, out GameSession session) =>
        _sessions.TryGetValue(code, out session!);

    public void Add(GameSession session) => _sessions[session.Code] = session;

    public void Remove(LobbyCode code)
    {
        if (_sessions.TryRemove(code, out var session))
        {
            session.Dispose();
        }
    }

    public IReadOnlyCollection<GameSession> All() => _sessions.Values.ToList();
}
