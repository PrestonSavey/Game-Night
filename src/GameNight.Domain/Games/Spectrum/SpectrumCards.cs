namespace GameNight.Domain.Games.Spectrum;

/// <summary>
/// The starter deck. Good cards are the ones where both ends are genuinely arguable -
/// "Cold / Hot" is easy, "Underrated / Overrated" is where the game gets fun.
/// Add your own; this is the cheapest way to make the game better.
/// </summary>
public static class SpectrumCards
{
    public static readonly IReadOnlyList<SpectrumCard> Default = new List<SpectrumCard>
    {
        new SpectrumCard("Cold", "Hot"),
        new SpectrumCard("Underrated", "Overrated"),
        new SpectrumCard("Forgettable", "Unforgettable"),
        new SpectrumCard("Guilty pleasure", "Respected"),
        new SpectrumCard("Useless", "Essential"),
        new SpectrumCard("Cheap", "Expensive"),
        new SpectrumCard("Quiet", "Loud"),
        new SpectrumCard("Casual", "Formal"),
        new SpectrumCard("Comfort food", "Fine dining"),
        new SpectrumCard("Villain", "Hero"),
        new SpectrumCard("Boring", "Chaotic"),
        new SpectrumCard("Kids' stuff", "Grown-up"),
        new SpectrumCard("Round", "Pointy"),
        new SpectrumCard("Would not survive", "Would survive"),
        new SpectrumCard("Bad advice", "Good advice"),
        new SpectrumCard("Fleeting trend", "Timeless"),
    };
}
