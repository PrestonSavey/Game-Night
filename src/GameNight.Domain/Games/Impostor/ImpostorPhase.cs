namespace GameNight.Domain.Games.Impostor;

public enum ImpostorPhase
{
    /// <summary>Going round the table, one clue each.</summary>
    Clues,

    /// <summary>Somebody called it. Everyone picks who they think it is.</summary>
    Voting,

    /// <summary>They were caught, and now get one shot at naming the word anyway.</summary>
    LastChance,

    Finished,
}
