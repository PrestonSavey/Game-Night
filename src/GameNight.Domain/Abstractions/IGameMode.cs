namespace GameNight.Domain.Abstractions;

/// <summary>
/// One game. Implementations are pure: no clock, no I/O, no sockets, no database.
/// Everything the platform needs from a game is these four members, which is what lets
/// the lobby, reconnection, timer and scoreboard machinery be written exactly once.
/// </summary>
public interface IGameMode
{
    GameModeInfo Info { get; }

    /// <summary>
    /// Deterministic: the same players and the same seed produce the same game.
    ///
    /// Returns effects as well as state because some games are already on the clock the
    /// moment they begin - Last Word's first player has ten seconds before anyone has
    /// done anything at all, and a Start that could only return state could never say so.
    /// </summary>
    StepResult Start(GameStartContext context);

    /// <summary>
    /// The only way state ever changes. MUST be pure - same inputs, same outputs, always.
    /// Invalid moves return the state unchanged rather than throwing; a player on a laggy
    /// phone double-tapping Submit is expected traffic, not an exception.
    /// </summary>
    StepResult Step(GameState state, GameEvent evt);

    /// <summary>
    /// Redact <paramref name="state"/> down to what this one player may see.
    /// This is the single place hidden information is enforced.
    /// </summary>
    PlayerView Project(GameState state, PlayerId viewer);
}
