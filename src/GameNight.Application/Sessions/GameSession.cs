using System.Threading.Channels;
using GameNight.Application.Contracts;
using GameNight.Application.Games;
using GameNight.Application.Lobbies;
using GameNight.Application.Security;
using GameNight.Application.Timers;
using GameNight.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace GameNight.Application.Sessions;

/// <summary>
/// One lobby, and whatever game it is currently playing.
///
/// Every command - a join, a guess, a timer, a dropped socket - is written to a channel
/// and applied by a single consumer loop. That is the entire concurrency story: no locks,
/// no reentrancy, and IGameMode.Step is guaranteed to be called from one thread at a time.
/// </summary>
public sealed class GameSession : IDisposable
{
    /// <summary>How long a seat is held for a phone that locked or lost signal.</summary>
    public static readonly TimeSpan DisconnectGrace = TimeSpan.FromMinutes(2);

    public const int MaxPlayers = 16;

    private const string ReleaseTimerPrefix = "session:release:";

    private readonly Channel<SessionCommand> _commands =
        Channel.CreateUnbounded<SessionCommand>(new UnboundedChannelOptions { SingleReader = true });

    private readonly List<Player> _players = new();
    private readonly Dictionary<PlayerId, string> _connections = new();

    private readonly IGameModeRegistry _modes;
    private readonly ISessionBroadcaster _broadcast;
    private readonly IRejoinTokenService _tokens;
    private readonly ILogger _logger;
    private readonly SessionTimers _timers;

    private PlayerId _hostId;
    private string? _selectedModeId;
    private IGameMode? _mode;
    private GameState? _state;
    private LobbyPhase _phase = LobbyPhase.Waiting;
    private IReadOnlyList<StandingView> _lastStandings = Array.Empty<StandingView>();

    public GameSession(
        LobbyCode code,
        IGameModeRegistry modes,
        ISessionBroadcaster broadcast,
        IRejoinTokenService tokens,
        ILogger logger,
        TimeProvider? time = null)
    {
        Code = code;
        _modes = modes;
        _broadcast = broadcast;
        _tokens = tokens;
        _logger = logger;
        _timers = new SessionTimers(id => PostAsync(new SessionCommand.TimerElapsed(id)), time);

        _ = Task.Run(RunAsync);
    }

    public LobbyCode Code { get; }

    public bool IsEmpty => _players.Count == 0;

    public ValueTask PostAsync(SessionCommand command) => _commands.Writer.WriteAsync(command);

    // -------------------------------------------------------------------------------
    // The loop. One at a time, in order, forever.
    // -------------------------------------------------------------------------------

    private async Task RunAsync()
    {
        await foreach (var command in _commands.Reader.ReadAllAsync())
        {
            try
            {
                await HandleAsync(command);
                await BroadcastAsync();
            }
            catch (Exception ex)
            {
                // One bad command must never take the lobby down with it.
                _logger.LogError(ex, "Lobby {Code} failed handling {Command}",
                    Code, command.GetType().Name);

                // ...but swallowing it silently makes the app look dead to whoever moved,
                // which is worse than an error. Tell them something went wrong.
                if (command is SessionCommand.Act failed)
                {
                    try
                    {
                        await RejectAsync(failed.Player, "server_error",
                            "That move could not be processed. The problem is on our side.");
                    }
                    catch (Exception nested)
                    {
                        _logger.LogError(nested, "Lobby {Code} could not report a failure", Code);
                    }
                }
            }
        }
    }

    private Task HandleAsync(SessionCommand command) => command switch
    {
        SessionCommand.Join join => JoinAsync(join),
        SessionCommand.JoinMany many => JoinManyAsync(many),
        SessionCommand.Rejoin rejoin => RejoinAsync(rejoin),
        SessionCommand.Disconnected dropped => DisconnectedAsync(dropped),
        SessionCommand.ReleaseSeat release => ReleaseSeatAsync(release.Player),
        SessionCommand.SelectMode select => SelectModeAsync(select),
        SessionCommand.StartGame start => StartGameAsync(start),
        SessionCommand.Act act => AdvanceAsync(new GameEvent.Action(act.Player, act.Payload)),
        SessionCommand.TimerElapsed timer => TimerElapsedAsync(timer),
        // Handled entirely by the broadcast that follows every command.
        SessionCommand.Resync => Task.CompletedTask,
        _ => Task.CompletedTask,
    };

    // -------------------------------------------------------------------------------
    // Lobby
    // -------------------------------------------------------------------------------

    private Task JoinAsync(SessionCommand.Join join)
    {
        var name = (join.DisplayName ?? string.Empty).Trim();

        if (name.Length is < 1 or > 16)
        {
            return Reply(join.Reply, RejectReasons.NameInvalid, "Names are 1 to 16 characters.");
        }

        if (_phase == LobbyPhase.InGame)
        {
            return Reply(join.Reply, RejectReasons.GameInProgress,
                "That game has already started. You can join when the round ends.");
        }

        if (_players.Count >= MaxPlayers)
        {
            return Reply(join.Reply, RejectReasons.LobbyFull, $"This lobby is full ({MaxPlayers} players).");
        }

        if (_players.Any(p => string.Equals(p.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Reply(join.Reply, RejectReasons.NameTaken, $"Someone here is already called {name}.");
        }

        var player = new Player(PlayerId.New(), name);
        _players.Add(player);
        _connections[player.Id] = join.ConnectionId;

        // First one through the door runs the lobby.
        if (_hostId == default)
        {
            _hostId = player.Id;
        }

        join.Reply.TrySetResult(JoinOutcome.Ok(player.Id, _tokens.Issue(Code, player.Id)));
        return Task.CompletedTask;
    }

    private Task JoinManyAsync(SessionCommand.JoinMany join)
    {
        var names = join.DisplayNames.Select(n => (n ?? string.Empty).Trim()).ToList();

        if (names.Count < 2)
        {
            return ReplyMany(join.Reply, RejectReasons.NotEnoughPlayers,
                "Local play needs at least two names.");
        }

        if (names.Any(n => n.Length is < 1 or > 16))
        {
            return ReplyMany(join.Reply, RejectReasons.NameInvalid, "Names are 1 to 16 characters.");
        }

        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
        {
            return ReplyMany(join.Reply, RejectReasons.NameTaken, "Every player needs a different name.");
        }

        if (_phase == LobbyPhase.InGame)
        {
            return ReplyMany(join.Reply, RejectReasons.GameInProgress, "That game has already started.");
        }

        if (_players.Count + names.Count > MaxPlayers)
        {
            return ReplyMany(join.Reply, RejectReasons.LobbyFull, $"At most {MaxPlayers} players.");
        }

        if (names.Any(n => _players.Any(p =>
                string.Equals(p.DisplayName, n, StringComparison.OrdinalIgnoreCase))))
        {
            return ReplyMany(join.Reply, RejectReasons.NameTaken, "Someone here already has that name.");
        }

        var seats = new List<Seat>();
        foreach (var name in names)
        {
            // Ordinary players who happen to share a connection. Nothing below this line
            // in the runtime knows or cares that local play exists.
            var player = new Player(PlayerId.New(), name);
            _players.Add(player);
            _connections[player.Id] = join.ConnectionId;
            seats.Add(new Seat(player.Id.ToString(), name, _tokens.Issue(Code, player.Id)));
        }

        if (_hostId == default)
        {
            _hostId = _players[0].Id;
        }

        join.Reply.TrySetResult(JoinManyOutcome.Ok(seats));
        return Task.CompletedTask;
    }

    private async Task RejoinAsync(SessionCommand.Rejoin rejoin)
    {
        var index = _players.FindIndex(p => p.Id == rejoin.Player);
        if (index < 0)
        {
            await Reply(rejoin.Reply, RejectReasons.SeatGone,
                "Your seat was given up. Join again with the lobby code.");
            return;
        }

        if (!_tokens.IsValid(Code, rejoin.Player, rejoin.Token))
        {
            await Reply(rejoin.Reply, RejectReasons.BadRejoinToken, "That seat is not yours.");
            return;
        }

        _timers.Cancel(ReleaseTimerPrefix + rejoin.Player);
        _players[index] = _players[index] with { IsConnected = true };
        _connections[rejoin.Player] = rejoin.ConnectionId;

        rejoin.Reply.TrySetResult(JoinOutcome.Ok(rejoin.Player, _tokens.Issue(Code, rejoin.Player)));

        await AdvanceAsync(new GameEvent.PlayerRejoined(rejoin.Player));
    }

    private Task DisconnectedAsync(SessionCommand.Disconnected dropped)
    {
        // In local play one connection holds every seat, so a drop is never just one player.
        var leaving = _connections
            .Where(kv => kv.Value == dropped.ConnectionId)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var player in leaving)
        {
            _connections.Remove(player);

            var index = _players.FindIndex(p => p.Id == player);
            if (index >= 0)
            {
                _players[index] = _players[index] with { IsConnected = false };
            }

            // Hold the seat. Phones lock constantly and the game should not care.
            _timers.Schedule(ReleaseTimerPrefix + player, DisconnectGrace);
        }

        return Task.CompletedTask;
    }

    private async Task ReleaseSeatAsync(PlayerId player)
    {
        // They came back inside the grace period - nothing to do.
        if (_connections.ContainsKey(player)) return;

        var index = _players.FindIndex(p => p.Id == player);
        if (index < 0) return;

        _players.RemoveAt(index);

        if (_hostId == player && _players.Count > 0)
        {
            _hostId = _players[0].Id;
        }

        await AdvanceAsync(new GameEvent.PlayerLeft(player));
    }

    private async Task SelectModeAsync(SessionCommand.SelectMode select)
    {
        if (select.Player != _hostId)
        {
            await RejectAsync(select.Player, RejectReasons.NotTheHost, "Only the host picks the game.");
            return;
        }

        if (!_modes.TryGet(select.ModeId, out _))
        {
            await RejectAsync(select.Player, RejectReasons.UnknownMode, "No such game.");
            return;
        }

        _selectedModeId = select.ModeId;
    }

    private async Task StartGameAsync(SessionCommand.StartGame start)
    {
        if (start.Player != _hostId)
        {
            await RejectAsync(start.Player, RejectReasons.NotTheHost, "Only the host can start.");
            return;
        }

        if (_phase == LobbyPhase.InGame) return;

        if (start.ModeId is not null)
        {
            if (!_modes.TryGet(start.ModeId, out _))
            {
                await RejectAsync(start.Player, RejectReasons.UnknownMode, "No such game.");
                return;
            }

            _selectedModeId = start.ModeId;
        }

        if (!_modes.TryGet(_selectedModeId, out var mode))
        {
            await RejectAsync(start.Player, RejectReasons.NoModeSelected, "Pick a game first.");
            return;
        }

        if (_players.Count < mode.Info.MinPlayers)
        {
            await RejectAsync(start.Player, RejectReasons.NotEnoughPlayers,
                $"{mode.Info.Name} needs at least {mode.Info.MinPlayers} players.");
            return;
        }

        if (_players.Count > mode.Info.MaxPlayers)
        {
            await RejectAsync(start.Player, RejectReasons.TooManyPlayers,
                $"{mode.Info.Name} takes at most {mode.Info.MaxPlayers} players.");
            return;
        }

        var seed = Random.Shared.Next();

        // Logging the seed and the setting means any game anyone complains about can be
        // replayed exactly.
        _logger.LogInformation(
            "Lobby {Code} starting {Mode} with {Players} players, setting {Setting}, seed {Seed}",
            Code, mode.Info.Id, _players.Count, start.Setting, seed);

        var opening = mode.Start(
            new GameStartContext(_players.ToList(), new SeededRandomSource(seed), start.Setting));

        _mode = mode;
        _state = opening.State;
        _phase = LobbyPhase.InGame;
        _lastStandings = Array.Empty<StandingView>();

        // Some games are already on the clock before anyone has moved.
        foreach (var effect in opening.Effects)
        {
            await ApplyEffectAsync(effect);
        }
    }

    // -------------------------------------------------------------------------------
    // Game
    // -------------------------------------------------------------------------------

    private async Task AdvanceAsync(GameEvent gameEvent)
    {
        if (_phase != LobbyPhase.InGame || _mode is null || _state is null) return;

        var result = _mode.Step(_state, gameEvent);
        _state = result.State;

        foreach (var effect in result.Effects)
        {
            await ApplyEffectAsync(effect);
        }
    }

    private async Task ApplyEffectAsync(Effect effect)
    {
        switch (effect)
        {
            case Effect.ScheduleTimer schedule:
                _timers.Schedule(schedule.TimerId, schedule.Delay);
                break;

            case Effect.CancelTimer cancel:
                _timers.Cancel(cancel.TimerId);
                break;

            case Effect.Announce announce:
                await _broadcast.Announce(Code, announce.Key, announce.Data);
                break;

            case Effect.Finished finished:
                Finish(finished.Standings);
                break;
        }
    }

    private Task TimerElapsedAsync(SessionCommand.TimerElapsed timer)
    {
        if (timer.TimerId.StartsWith(ReleaseTimerPrefix, StringComparison.Ordinal))
        {
            var raw = timer.TimerId[ReleaseTimerPrefix.Length..];
            return Guid.TryParse(raw, out var id)
                ? ReleaseSeatAsync(new PlayerId(id))
                : Task.CompletedTask;
        }

        return AdvanceAsync(new GameEvent.TimerFired(timer.TimerId));
    }

    private void Finish(IReadOnlyList<Standing> standings)
    {
        _timers.CancelAll();

        _lastStandings = standings
            .Select(s => new StandingView(s.Player.ToString(), NameOf(s.Player), s.Rank, s.Label))
            .OrderBy(s => s.Rank)
            .ToList();

        _phase = LobbyPhase.Results;
        _mode = null;
        _state = null;
    }

    // -------------------------------------------------------------------------------
    // Talking to clients
    // -------------------------------------------------------------------------------

    private async Task BroadcastAsync()
    {
        await _broadcast.LobbyUpdated(Code, BuildLobbyView());

        if (_phase != LobbyPhase.InGame || _mode is null || _state is null) return;

        // One message per player, because one projection per player.
        foreach (var player in _players)
        {
            if (!_connections.TryGetValue(player.Id, out var connection)) continue;

            await _broadcast.ViewUpdated(connection, player.Id, _mode.Project(_state, player.Id));
        }
    }

    private LobbyView BuildLobbyView()
    {
        var (canStart, reason) = CanStart();

        return new LobbyView(
            Code: Code.Value,
            Phase: _phase.ToString(),
            HostPlayerId: _hostId.ToString(),
            SelectedModeId: _selectedModeId,
            CanStart: canStart,
            CannotStartReason: reason,
            Players: _players
                .Select(p => new LobbyPlayerView(
                    p.Id.ToString(), p.DisplayName, p.Id == _hostId, p.IsConnected))
                .ToList(),
            AvailableModes: _modes.All,
            LastStandings: _lastStandings);
    }

    private (bool CanStart, string? Reason) CanStart()
    {
        if (_phase == LobbyPhase.InGame) return (false, "A game is already running.");
        if (!_modes.TryGet(_selectedModeId, out var mode)) return (false, "Pick a game.");

        var connected = _players.Count(p => p.IsConnected);
        if (connected < mode.Info.MinPlayers)
        {
            var needed = mode.Info.MinPlayers - connected;
            return (false, $"Waiting for {needed} more player{(needed == 1 ? "" : "s")}.");
        }

        if (connected > mode.Info.MaxPlayers)
        {
            return (false, $"{mode.Info.Name} takes at most {mode.Info.MaxPlayers} players.");
        }

        return (true, null);
    }

    private string NameOf(PlayerId id) =>
        _players.FirstOrDefault(p => p.Id == id)?.DisplayName ?? "Player";

    private Task ReplyMany(
        TaskCompletionSource<JoinManyOutcome> reply, string code, string message)
    {
        reply.TrySetResult(JoinManyOutcome.Fail(code, message));
        return Task.CompletedTask;
    }

    private Task Reply(TaskCompletionSource<JoinOutcome> reply, string code, string message)
    {
        reply.TrySetResult(JoinOutcome.Fail(code, message));
        return Task.CompletedTask;
    }

    private Task RejectAsync(PlayerId player, string code, string message) =>
        _connections.TryGetValue(player, out var connection)
            ? _broadcast.Rejected(connection, code, message)
            : Task.CompletedTask;

    public void Dispose()
    {
        _commands.Writer.TryComplete();
        _timers.Dispose();
    }
}
