using System.Text.Json;

namespace GameNight.Domain.Games.Bluff;

public abstract record BluffAction
{
    /// <summary>A lie, offered as if it were the answer.</summary>
    public sealed record Submit(string Text) : BluffAction;

    public sealed record Vote(string OptionKey) : BluffAction;

    public sealed record Continue : BluffAction;

    public static BluffAction? TryParse(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        if (!payload.TryGetProperty("type", out var type)) return null;
        if (type.ValueKind != JsonValueKind.String) return null;

        switch (type.GetString())
        {
            case "lie":
                return Text(payload, "text") is { } lie ? new Submit(lie) : null;

            case "vote":
                return Text(payload, "option") is { } option ? new Vote(option) : null;

            case "continue":
                return new Continue();

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
