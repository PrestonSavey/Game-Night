namespace GameNight.Domain.Abstractions;

/// <summary>
/// Every random choice a game makes goes through here.
/// Games must never touch System.Random directly - if they do, the same seed stops
/// producing the same game and your tests turn flaky for reasons you cannot see.
/// </summary>
public interface IRandomSource
{
    /// <summary>Uniform value in [minInclusive, maxExclusive).</summary>
    int Next(int minInclusive, int maxExclusive);
}

public sealed class SeededRandomSource : IRandomSource
{
    private readonly Random _random;

    public SeededRandomSource(int seed) => _random = new Random(seed);

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
