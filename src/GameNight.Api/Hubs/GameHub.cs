using System.Text.Json;
using GameNight.Application.Contracts;
using GameNight.Application.Lobbies;
using GameNight.Application.Sessions;
using GameNight.Domain.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace GameNight.Api.Hubs;

/// <summary>
/// The transport, and nothing more. Every method here validates its arguments, works out
/// who is calling, and hands a command to the runtime. No game logic lives in this file
/// and none ever should.
///
/// A connection normally holds one seat. In local play it holds all of them, which is why
/// the caller is a list rather than a single player.
/// </summary>
public sealed class GameHub : Hub
{
    private const string CodeKey = "lobbyCode";
    private const string SeatsKey = "seats";

    private readonly ISessionRuntime _runtime;

    public GameHub(ISessionRuntime runtime) => _runtime = runtime;

    public async Task<JoinResponse> CreateLobby(string displayName)
    {
        var (code, outcome) = await _runtime.CreateLobbyAsync(displayName, Context.ConnectionId);
        return await CompleteJoin(code, outcome);
    }

    public async Task<LocalJoinResponse> CreateLocalLobby(string[] displayNames)
    {
        var names = displayNames ?? Array.Empty<string>();

        var (code, outcome) = await _runtime.CreateLocalLobbyAsync(names, Context.ConnectionId);

        if (!outcome.Success)
        {
            return new LocalJoinResponse(
                false, outcome.ErrorCode, outcome.ErrorMessage, null, Array.Empty<SeatResponse>());
        }

        var seats = outcome.Seats
            .Select(s => new SeatResponse(s.PlayerId, s.DisplayName, s.RejoinToken))
            .ToList();

        Context.Items[CodeKey] = code;
        Context.Items[SeatsKey] = seats.Select(s => PlayerId.Parse(s.PlayerId)).ToList();

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(code));

        // Only now can this connection hear the group, so ask for the state it missed.
        await _runtime.ResyncAsync(code);

        return new LocalJoinResponse(true, null, null, code.Value, seats);
    }

    public async Task<JoinResponse> JoinLobby(string lobbyCode, string displayName)
    {
        if (!LobbyCode.TryParse(lobbyCode, out var code))
        {
            return Failure(RejectReasons.LobbyNotFound, "That is not a valid lobby code.");
        }

        var outcome = await _runtime.JoinAsync(code, displayName, Context.ConnectionId);
        return await CompleteJoin(code, outcome);
    }

    /// <summary>
    /// Called on every reconnect. The client holds playerId and token in local storage, so
    /// a locked phone comes back to its own seat rather than arriving as a new player.
    /// </summary>
    public async Task<JoinResponse> Resume(string lobbyCode, string playerId, string token)
    {
        if (!LobbyCode.TryParse(lobbyCode, out var code))
        {
            return Failure(RejectReasons.LobbyNotFound, "That is not a valid lobby code.");
        }

        if (!Guid.TryParse(playerId, out var raw))
        {
            return Failure(RejectReasons.BadRejoinToken, "That seat is not yours.");
        }

        var outcome = await _runtime.ResumeAsync(code, new PlayerId(raw), token, Context.ConnectionId);
        return await CompleteJoin(code, outcome);
    }

    public Task SelectGame(string modeId) =>
        WithHost((code, player) => _runtime.SelectModeAsync(code, player, modeId));

    /// <summary>Picking a tile and starting are one gesture, so they are one call.</summary>
    public Task StartGame(string? modeId, int? setting) =>
        WithHost((code, player) => _runtime.StartGameAsync(code, player, modeId, setting));

    public Task SubmitAction(JsonElement payload) =>
        WithHost((code, player) => _runtime.ActAsync(code, player, payload));

    /// <summary>
    /// Local play: this device holds several seats, so it has to say which one is moving.
    /// The seat list comes from the connection, never from the argument, so a client cannot
    /// act as somebody it does not hold.
    /// </summary>
    public Task SubmitActionAs(string playerId, JsonElement payload)
    {
        if (!TryGetSeats(out var code, out var seats)) return Task.CompletedTask;
        if (!Guid.TryParse(playerId, out var raw)) return Task.CompletedTask;

        var player = new PlayerId(raw);
        if (!seats.Contains(player)) return Task.CompletedTask;

        return _runtime.ActAsync(code, player, payload);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetSeats(out var code, out _))
        {
            await _runtime.DisconnectedAsync(code, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ---------------------------------------------------------------------------------

    private async Task<JoinResponse> CompleteJoin(LobbyCode code, JoinOutcome outcome)
    {
        if (!outcome.Success)
        {
            return Failure(outcome.ErrorCode!, outcome.ErrorMessage!);
        }

        Context.Items[CodeKey] = code;

        // Append rather than replace: a local-play device resuming after a refresh calls
        // this once per seat it holds, and each call must add to the set, not reset it.
        var seats = Context.Items.TryGetValue(SeatsKey, out var raw) && raw is List<PlayerId> held
            ? held
            : new List<PlayerId>();

        if (!seats.Contains(outcome.Player)) seats.Add(outcome.Player);
        Context.Items[SeatsKey] = seats;

        // The group carries lobby-wide messages. Per-player views are sent by connection.
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(code));

        // Only now can this connection hear the group, so ask for the state it missed.
        await _runtime.ResyncAsync(code);

        return new JoinResponse(
            true, null, null, code.Value, outcome.Player.ToString(), outcome.RejoinToken);
    }

    /// <summary>Lobby-level actions come from the first seat this connection claimed.</summary>
    private Task WithHost(Func<LobbyCode, PlayerId, Task> action) =>
        TryGetSeats(out var code, out var seats) && seats.Count > 0
            ? action(code, seats[0])
            : Task.CompletedTask;

    private bool TryGetSeats(out LobbyCode code, out List<PlayerId> seats)
    {
        code = default;
        seats = new List<PlayerId>();

        if (Context.Items.TryGetValue(CodeKey, out var rawCode) && rawCode is LobbyCode c &&
            Context.Items.TryGetValue(SeatsKey, out var rawSeats) && rawSeats is List<PlayerId> s)
        {
            code = c;
            seats = s;
            return true;
        }

        return false;
    }

    private static JoinResponse Failure(string code, string message) =>
        new(false, code, message, null, null, null);

    public static string GroupFor(LobbyCode code) => $"lobby:{code.Value}";
}
