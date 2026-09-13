using System.Security.Cryptography;
using System.Text;
using GameNight.Application.Lobbies;
using GameNight.Domain.Abstractions;

namespace GameNight.Application.Security;

public interface IRejoinTokenService
{
    string Issue(LobbyCode code, PlayerId player);

    bool IsValid(LobbyCode code, PlayerId player, string? token);
}

/// <summary>
/// Stops someone typing a stranger's player id into local storage and stealing their seat.
/// Deliberately not a JWT - there is no identity here worth that much ceremony, just a
/// signature over "this player belongs to this lobby".
/// </summary>
public sealed class RejoinTokenService : IRejoinTokenService
{
    private readonly byte[] _key;

    public RejoinTokenService(byte[]? key = null) =>
        _key = key ?? RandomNumberGenerator.GetBytes(32);

    public string Issue(LobbyCode code, PlayerId player)
    {
        var payload = Encoding.UTF8.GetBytes($"{code.Value}:{player}");
        var signature = HMACSHA256.HashData(_key, payload);
        return Convert.ToBase64String(signature);
    }

    public bool IsValid(LobbyCode code, PlayerId player, string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;

        var expected = Issue(code, player);

        // Fixed-time compare - cheap here, and the habit is worth keeping.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(token));
    }
}
