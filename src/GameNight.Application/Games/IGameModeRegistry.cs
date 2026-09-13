using GameNight.Application.Contracts;
using GameNight.Domain.Abstractions;

namespace GameNight.Application.Games;

public interface IGameModeRegistry
{
    IReadOnlyList<GameModeSummary> All { get; }

    bool TryGet(string? modeId, out IGameMode mode);
}

public sealed class GameModeRegistry : IGameModeRegistry
{
    private readonly Dictionary<string, IGameMode> _modes;

    public GameModeRegistry(IEnumerable<IGameMode> modes)
    {
        _modes = modes.ToDictionary(m => m.Info.Id, StringComparer.OrdinalIgnoreCase);

        All = _modes.Values
            .Select(m => new GameModeSummary(
                m.Info.Id,
                m.Info.Name,
                m.Info.Tagline,
                m.Info.MinPlayers,
                m.Info.MaxPlayers,
                Describe(m.Info.Setting),
                m.Info.PrivateTurns))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<GameModeSummary> All { get; }

    public bool TryGet(string? modeId, out IGameMode mode)
    {
        if (modeId is not null && _modes.TryGetValue(modeId, out var found))
        {
            mode = found;
            return true;
        }

        mode = null!;
        return false;
    }

    /// <summary>
    /// The lobby renders whatever the game declares. It never learns what "rounds" or
    /// "seconds per turn" actually mean, which is why adding a game needs no UI changes.
    /// </summary>
    private static GameSettingSummary? Describe(GameSetting? setting) =>
        setting is null
            ? null
            : new GameSettingSummary(
                setting.Label,
                setting.Options.Select(o => new GameSettingOptionSummary(o.Value, o.Label)).ToList(),
                setting.Default,
                setting.DefaultToPlayerCount);
}
