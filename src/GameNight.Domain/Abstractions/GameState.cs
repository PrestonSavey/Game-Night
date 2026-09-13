namespace GameNight.Domain.Abstractions;

/// <summary>
/// The server's complete, authoritative truth about one game in progress.
///
/// This type NEVER crosses the wire. Clients only ever receive the redacted
/// <see cref="PlayerView"/> produced by <see cref="IGameMode.Project"/>. If you ever
/// find yourself serializing a GameState, you have just handed every player the answers.
/// </summary>
public abstract record GameState
{
    public abstract bool IsFinished { get; }
}
