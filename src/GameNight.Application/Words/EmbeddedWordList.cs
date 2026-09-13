using System.IO.Compression;
using GameNight.Domain.Abstractions;

namespace GameNight.Application.Words;

/// <summary>
/// The dictionary, shipped inside the assembly. No API call, no network dependency, no
/// rate limit, and it works on a laptop with no signal at somebody's kitchen table.
///
/// Around 113,000 words, which costs a few megabytes of memory and buys sub-microsecond
/// lookups. Loaded once at startup and never mutated, so every game can share it.
/// </summary>
public sealed class EmbeddedWordList : IWordList
{
    private const string ResourceName = "GameNight.Application.Words.words.txt.gz";

    private readonly HashSet<string> _words;

    public EmbeddedWordList()
    {
        using var stream = typeof(EmbeddedWordList).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Word list '{ResourceName}' is missing from the assembly.");

        using var decompressed = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(decompressed);

        _words = new HashSet<string>(StringComparer.Ordinal);

        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0) _words.Add(line);
        }
    }

    public int Count => _words.Count;

    public bool Contains(string word) => _words.Contains(word);

    public IEnumerable<string> All => _words;
}
