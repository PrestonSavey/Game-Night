namespace GameNight.Application.Contracts;

/// <summary>The lobby as everyone sees it. Identical for every player, so it can go to the group.</summary>
public sealed record LobbyView(
    string Code,
    string Phase,
    string HostPlayerId,
    string? SelectedModeId,
    bool CanStart,
    string? CannotStartReason,
    IReadOnlyList<LobbyPlayerView> Players,
    IReadOnlyList<GameModeSummary> AvailableModes,
    IReadOnlyList<StandingView> LastStandings);

public sealed record LobbyPlayerView(
    string PlayerId,
    string DisplayName,
    bool IsHost,
    bool IsConnected);

public sealed record GameModeSummary(
    string Id,
    string Name,
    string Tagline,
    int MinPlayers,
    int MaxPlayers,
    /// <summary>Null when the game has nothing for the host to choose.</summary>
    GameSettingSummary? Setting,
    /// <summary>Whether a shared device needs a pass-the-device gate between turns.</summary>
    bool PrivateTurns);

public sealed record GameSettingSummary(
    string Label,
    IReadOnlyList<GameSettingOptionSummary> Options,
    int Default,
    bool DefaultToPlayerCount);

public sealed record GameSettingOptionSummary(int Value, string Label);

public sealed record StandingView(
    string PlayerId,
    string DisplayName,
    int Rank,
    string Label);
