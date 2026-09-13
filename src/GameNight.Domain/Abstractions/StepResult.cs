namespace GameNight.Domain.Abstractions;

public sealed record StepResult(GameState State, IReadOnlyList<Effect> Effects)
{
    public static StepResult Unchanged(GameState state) => new(state, Array.Empty<Effect>());

    public static StepResult From(GameState state, params Effect[] effects) => new(state, effects);
}
