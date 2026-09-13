namespace GameNight.Domain.Games.LastWord;

/// <summary>The two letters a word must start and end with. Stored lower case.</summary>
public readonly record struct LetterPair(char Start, char End)
{
    public override string ToString() =>
        $"{char.ToUpperInvariant(Start)}…{char.ToUpperInvariant(End)}";
}
