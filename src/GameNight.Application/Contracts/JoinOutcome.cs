using GameNight.Domain.Abstractions;

namespace GameNight.Application.Contracts;

/// <param name="RejoinToken">
/// Stored in the browser alongside the player id. Presenting both is what lets a phone
/// that locked mid-round get its seat back instead of arriving as a stranger.
/// </param>
public sealed record JoinOutcome(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    PlayerId Player,
    string? RejoinToken)
{
    public static JoinOutcome Ok(PlayerId player, string token) =>
        new(true, null, null, player, token);

    public static JoinOutcome Fail(string code, string message) =>
        new(false, code, message, default, null);
}

/// <summary>One person's place in a lobby, as handed back to whoever claimed it.</summary>
public sealed record Seat(string PlayerId, string DisplayName, string RejoinToken);

/// <summary>
/// The result of claiming several seats at once, which is what local play is: one device
/// holding every player. The server treats them as ordinary players who happen to share a
/// connection, so nothing else in the runtime has to know local play exists.
/// </summary>
public sealed record JoinManyOutcome(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<Seat> Seats)
{
    public static JoinManyOutcome Ok(IReadOnlyList<Seat> seats) => new(true, null, null, seats);

    public static JoinManyOutcome Fail(string code, string message) =>
        new(false, code, message, Array.Empty<Seat>());
}

public static class RejectReasons
{
    public const string LobbyNotFound = "lobby_not_found";
    public const string LobbyFull = "lobby_full";
    public const string GameInProgress = "game_in_progress";
    public const string NameTaken = "name_taken";
    public const string NameInvalid = "name_invalid";
    public const string NotEnoughPlayers = "not_enough_players";
    public const string TooManyPlayers = "too_many_players";
    public const string NotTheHost = "not_the_host";
    public const string NoModeSelected = "no_mode_selected";
    public const string UnknownMode = "unknown_mode";
    public const string BadRejoinToken = "bad_rejoin_token";
    public const string SeatGone = "seat_gone";
}
