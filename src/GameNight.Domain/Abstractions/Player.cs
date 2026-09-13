namespace GameNight.Domain.Abstractions;

/// <param name="IsConnected">
/// False while the player's socket is gone but their seat is still held.
/// Each game mode decides for itself what a missing player means - in Spectrum the
/// round carries on without them, in Last Word their turn will time out.
/// </param>
public sealed record Player(PlayerId Id, string DisplayName)
{
    public bool IsConnected { get; init; } = true;
}
