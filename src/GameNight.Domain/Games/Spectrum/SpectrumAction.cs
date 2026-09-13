using System.Text.Json;

namespace GameNight.Domain.Games.Spectrum;

/// <summary>
/// The moves a player can make. Parsed from the loose JSON payload the client sends, so
/// that a malformed or hostile message becomes null here rather than an exception in Step.
/// </summary>
public abstract record SpectrumAction
{
    public sealed record GiveClue(string Clue) : SpectrumAction;

    public sealed record SubmitGuess(int Position) : SpectrumAction;

    public sealed record Continue : SpectrumAction;

    public static SpectrumAction? TryParse(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty("type", out var typeProperty)) return null;
        if (typeProperty.ValueKind != JsonValueKind.String) return null;

        switch (typeProperty.GetString())
        {
            case "clue":
                if (payload.TryGetProperty("clue", out var clue) &&
                    clue.ValueKind == JsonValueKind.String)
                {
                    var text = clue.GetString();
                    return string.IsNullOrWhiteSpace(text) ? null : new GiveClue(text.Trim());
                }
                return null;

            case "guess":
                if (payload.TryGetProperty("position", out var position) &&
                    position.ValueKind == JsonValueKind.Number &&
                    position.TryGetInt32(out var value))
                {
                    return new SubmitGuess(value);
                }
                return null;

            case "continue":
                return new Continue();

            default:
                return null;
        }
    }
}
