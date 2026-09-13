namespace GameNight.Domain.Abstractions;

/// <summary>
/// A player's stable identity for the lifetime of a lobby.
/// Deliberately NOT the SignalR connection id: connections die every time a phone
/// locks or a browser backgrounds itself, and the player is still in the game.
/// </summary>
public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.NewGuid());

    public static PlayerId Parse(string value) => new(Guid.Parse(value));

    public override string ToString() => Value.ToString("N");
}
