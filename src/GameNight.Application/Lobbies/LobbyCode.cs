namespace GameNight.Application.Lobbies;

/// <summary>
/// A short code people read out loud across a noisy room, so the alphabet deliberately
/// excludes I, O, 0 and 1. Always stored upper case so lookups are a plain ordinal match.
/// </summary>
public readonly record struct LobbyCode(string Value)
{
    public const int Length = 4;

    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public bool IsValid => !string.IsNullOrEmpty(Value) && Value.Length == Length;

    public static bool TryParse(string? raw, out LobbyCode code)
    {
        code = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var trimmed = raw.Trim().ToUpperInvariant();
        if (trimmed.Length != Length) return false;
        if (trimmed.Any(c => !Alphabet.Contains(c))) return false;

        code = new LobbyCode(trimmed);
        return true;
    }

    public override string ToString() => Value ?? string.Empty;
}
