namespace GameNight.Domain.Abstractions;

/// <summary>
/// A fixed set of valid words, supplied to a game at construction.
///
/// This is not I/O sneaking into the domain: the list is immutable data handed to the game
/// once, so Step is still a pure function of (state, event) for any given dictionary. Tests
/// pass a handful of words; production passes a hundred thousand.
/// </summary>
public interface IWordList
{
    bool Contains(string word);

    /// <summary>Every word, for games that need to index the list up front.</summary>
    IEnumerable<string> All { get; }
}
