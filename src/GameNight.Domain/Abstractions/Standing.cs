namespace GameNight.Domain.Abstractions;

/// <param name="Label">
/// Rendered verbatim by the client. Deliberately a string rather than a score, because
/// not every game has points - Last Word finishes with "Eliminated in round 3".
/// </param>
public sealed record Standing(PlayerId Player, int Rank, string Label);
