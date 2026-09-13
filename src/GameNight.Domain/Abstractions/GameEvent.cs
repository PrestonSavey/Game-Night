using System.Text.Json;

namespace GameNight.Domain.Abstractions;

/// <summary>
/// Everything that can move a game forward.
///
/// Note that time arrives as <see cref="TimerFired"/> rather than a game reading a clock.
/// That single decision is what keeps <see cref="IGameMode.Step"/> a pure function, and
/// it is why you can test a ten-second elimination synchronously in under a millisecond.
/// </summary>
public abstract record GameEvent
{
    /// <summary>A player did something. Payload shape is defined by the game mode.</summary>
    public sealed record Action(PlayerId Player, JsonElement Payload) : GameEvent;

    /// <summary>A timer the game previously asked for has elapsed.</summary>
    public sealed record TimerFired(string TimerId) : GameEvent;

    /// <summary>A player's connection dropped and the grace period expired.</summary>
    public sealed record PlayerLeft(PlayerId Player) : GameEvent;

    /// <summary>A player came back before their seat was released.</summary>
    public sealed record PlayerRejoined(PlayerId Player) : GameEvent;
}
