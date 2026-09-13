namespace GameNight.Domain.Abstractions;

/// <param name="Setting">
/// Whatever the host chose from <see cref="GameSetting"/>, or null for the game's default.
/// One number because one dropdown; when a game wants two knobs this becomes a dictionary.
/// </param>
public sealed record GameStartContext(
    IReadOnlyList<Player> Players,
    IRandomSource Random,
    int? Setting = null);
