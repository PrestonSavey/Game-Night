using System.Text.Json;

namespace GameNight.Domain.Games.Impostor;

public abstract record ImpostorAction
{
    public sealed record GiveClue(string Clue) : ImpostorAction;

    public sealed record CallVote : ImpostorAction;

    public sealed record Vote(string TargetPlayerId) : ImpostorAction;

    /// <summary>The caught impostor's one shot at naming the word.</summary>
    public sealed record Guess(string Word) : ImpostorAction;

    public static ImpostorAction? TryParse(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty("type", out var type)) return null;
        if (type.ValueKind != JsonValueKind.String) return null;

        switch (type.GetString())
        {
            case "clue":
                return Text(payload, "clue") is { } clue ? new GiveClue(clue) : null;

            case "callVote":
                return new CallVote();

            case "vote":
                return Text(payload, "target") is { } target ? new Vote(target) : null;

            case "guess":
                return Text(payload, "word") is { } word ? new Guess(word) : null;

            default:
                return null;
        }
    }

    private static string? Text(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String) return null;

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
