namespace GameNight.Domain.Games.Impostor;

/// <param name="Category">
/// Shown to everyone, impostor included. Without it the impostor is guessing in the dark
/// and the first clue gives them nothing to build on.
/// </param>
public sealed record SecretWord(string Word, string Category);

/// <summary>
/// Words chosen to be cluable without being trivially cluable - concrete enough that a
/// one-word clue can point at them, common enough that nobody needs to explain them.
/// </summary>
public static class SecretWords
{
    public static readonly IReadOnlyList<SecretWord> Default = new List<SecretWord>
    {
        new("Beach", "A place"),
        new("Airport", "A place"),
        new("Library", "A place"),
        new("Hospital", "A place"),
        new("Casino", "A place"),
        new("Cemetery", "A place"),
        new("Gym", "A place"),
        new("Zoo", "A place"),
        new("Kitchen", "A place"),
        new("Prison", "A place"),

        new("Elephant", "An animal"),
        new("Penguin", "An animal"),
        new("Octopus", "An animal"),
        new("Hamster", "An animal"),
        new("Shark", "An animal"),
        new("Owl", "An animal"),
        new("Sloth", "An animal"),
        new("Bee", "An animal"),

        new("Pizza", "Something to eat"),
        new("Sushi", "Something to eat"),
        new("Pancakes", "Something to eat"),
        new("Popcorn", "Something to eat"),
        new("Cheese", "Something to eat"),
        new("Soup", "Something to eat"),
        new("Birthday cake", "Something to eat"),

        new("Umbrella", "An object"),
        new("Toothbrush", "An object"),
        new("Ladder", "An object"),
        new("Mirror", "An object"),
        new("Candle", "An object"),
        new("Suitcase", "An object"),
        new("Guitar", "An object"),
        new("Alarm clock", "An object"),
        new("Wallet", "An object"),
        new("Telescope", "An object"),

        new("Wedding", "An event"),
        new("Funeral", "An event"),
        new("Job interview", "An event"),
        new("First date", "An event"),
        new("Road trip", "An event"),
        new("Snow day", "An event"),
        new("Concert", "An event"),
        new("Haircut", "An event"),

        new("Firefighter", "A job"),
        new("Dentist", "A job"),
        new("Pilot", "A job"),
        new("Chef", "A job"),
        new("Teacher", "A job"),
        new("Referee", "A job"),
        new("Lifeguard", "A job"),
        new("Barber", "A job"),

        new("Swimming", "An activity"),
        new("Camping", "An activity"),
        new("Karaoke", "An activity"),
        new("Fishing", "An activity"),
        new("Skiing", "An activity"),
        new("Gardening", "An activity"),
        new("Bowling", "An activity"),
        new("Yoga", "An activity"),
    };
}
