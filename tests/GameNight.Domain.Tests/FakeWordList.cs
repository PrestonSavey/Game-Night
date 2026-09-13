using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Tests;

/// <summary>
/// A dictionary small enough to reason about. This is the payoff of taking IWordList as a
/// constructor argument rather than reading a file: the rules can be tested against twenty
/// words instead of a hundred thousand, and the tests say exactly which ones.
/// </summary>
internal sealed class FakeWordList : IWordList
{
    private readonly HashSet<string> _words;

    public FakeWordList(params string[] words) =>
        _words = new HashSet<string>(words, StringComparer.Ordinal);

    /// <summary>
    /// Enough A-to-D words to clear the game's viability threshold, so every draw is A…D
    /// and the tests never have to guess which letters came up.
    /// </summary>
    public static FakeWordList AtoD()
    {
        var words = new List<string>
        {
            "acid", "aid", "and", "arid", "award", "abroad", "afraid", "aground",
            "ahead", "aloud", "amid", "anted", "applaud", "arced", "armed", "around",
            "ascend", "asked", "aspired", "aired", "attend", "avoid", "awaited", "awed",
            "abound", "accord", "acred", "addled", "adored", "aged",
        };
        return new FakeWordList(words.ToArray());
    }

    public bool Contains(string word) => _words.Contains(word);

    public IEnumerable<string> All => _words;
}
