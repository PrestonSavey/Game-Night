namespace GameNight.Api.Hubs;

/// <summary>
/// Wire shape for a join attempt. PlayerId is a string here rather than the domain's
/// struct, so the client stores exactly what it sends back on resume.
/// </summary>
public sealed record JoinResponse(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    string? LobbyCode,
    string? PlayerId,
    string? RejoinToken);

public sealed record SeatResponse(string PlayerId, string DisplayName, string RejoinToken);

/// <summary>Local play, where one device holds every seat.</summary>
public sealed record LocalJoinResponse(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    string? LobbyCode,
    IReadOnlyList<SeatResponse> Seats);
