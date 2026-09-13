namespace GameNight.Domain.Abstractions;

public sealed record GameModeInfo(
    string Id,
    string Name,
    string Tagline,
    int MinPlayers,
    int MaxPlayers,
    GameSetting? Setting = null,
    /// <summary>
    /// True when a turn shows something the rest of the table must not see. On one shared
    /// device this is what puts a "pass to Ava" gate in front of each turn; games with
    /// nothing to hide skip it, because a gate on a ten-second turn is just friction.
    /// </summary>
    bool PrivateTurns = false);

/// <summary>
/// The one thing a host may choose before starting, as the game itself describes it.
///
/// The platform does not know what "rounds" means, or that Last Word has no rounds at all -
/// it renders whatever the game declares and hands the number back at Start. Games with
/// nothing to configure leave this null and no dropdown appears.
/// </summary>
public sealed record GameSetting(
    string Label,
    IReadOnlyList<GameSettingOption> Options,
    int Default,
    /// <summary>
    /// Spectrum's natural default is one round per player, which the game cannot know
    /// until the roster is settled. The client resolves it against the current lobby.
    /// </summary>
    bool DefaultToPlayerCount = false);

public sealed record GameSettingOption(int Value, string Label);
