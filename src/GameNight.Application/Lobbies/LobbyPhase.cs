namespace GameNight.Application.Lobbies;

public enum LobbyPhase
{
    /// <summary>Waiting in the lobby: people joining, host picking a game.</summary>
    Waiting,

    InGame,

    /// <summary>Game over, standings on screen, everyone back in the lobby after this.</summary>
    Results,
}
