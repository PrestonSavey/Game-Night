namespace GameNight.Domain.Abstractions;

/// <summary>
/// The slice of a game that one specific player is allowed to see, at this moment.
/// This is the only game-shaped thing that is ever serialized to a client.
/// </summary>
public abstract record PlayerView
{
    /// <summary>Tells the client which game's component tree to render.</summary>
    public abstract string ModeId { get; }

    /// <summary>
    /// Whether the game is waiting on this player specifically.
    ///
    /// It lives on the base view because the platform genuinely needs it: on one shared
    /// device something has to decide whose hands the phone belongs in, and that question
    /// has the same answer shape in every game. Before this existed the local-play screen
    /// had a switch on the game id, which is exactly the sort of thing this whole design
    /// is meant to avoid.
    /// </summary>
    public abstract bool YourTurn { get; }

    /// <summary>
    /// True when there is nothing left to hide and the table can look at one screen
    /// together - a reveal, a result. The pass-the-device gate stands down.
    /// </summary>
    public abstract bool OpenToAll { get; }
}
