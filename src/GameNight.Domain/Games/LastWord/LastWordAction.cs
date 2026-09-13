using System.Text.Json;

namespace GameNight.Domain.Games.LastWord;

public abstract record LastWordAction
{
    public sealed record Submit(string Word) : LastWordAction;

    public static LastWordAction? TryParse(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty("type", out var type)) return null;
        if (type.ValueKind != JsonValueKind.String || type.GetString() != "word") return null;

        if (!payload.TryGetProperty("word", out var word)) return null;
        if (word.ValueKind != JsonValueKind.String) return null;

        var text = word.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : new Submit(text.Trim());
    }
}
