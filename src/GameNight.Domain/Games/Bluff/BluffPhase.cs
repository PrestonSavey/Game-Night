namespace GameNight.Domain.Games.Bluff;

public enum BluffPhase
{
    /// <summary>Everyone is inventing a convincing lie. Nobody sees anyone else's.</summary>
    Writing,

    /// <summary>The lies and the truth are on the table, shuffled. Pick one.</summary>
    Voting,

    /// <summary>Who wrote what, who fell for what, and what the answer actually was.</summary>
    Reveal,

    Finished,
}
