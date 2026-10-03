namespace GameNight.Domain.Games.Bluff;

/// <param name="Prompt">Contains ___ where the answer goes.</param>
/// <param name="Answer">
/// The truth. Compared against submissions loosely - case, spacing and punctuation are
/// ignored - so a player who happens to know it is spotted rather than accidentally
/// adding a second correct answer to the board.
/// </param>
public sealed record TriviaQuestion(string Prompt, string Answer);

/// <summary>
/// Questions are chosen to have answers that are surprising but sayable: obscure enough
/// that nobody just knows them, concrete enough that a good liar can invent something
/// plausible. A question whose answer is guessable is a dead round.
/// </summary>
public static class TriviaQuestions
{
    public static readonly IReadOnlyList<TriviaQuestion> Default = new List<TriviaQuestion>
    {
        new("A group of crows is called a ___.", "murder"),
        new("A group of owls is called a ___.", "parliament"),
        new("A group of flamingos is called a ___.", "flamboyance"),
        new("A group of jellyfish is called a ___.", "smack"),
        new("A group of pandas is called an ___.", "embarrassment"),
        new("A group of ferrets is called a ___.", "business"),
        new("A group of porcupines is called a ___.", "prickle"),
        new("A group of rhinoceroses is called a ___.", "crash"),
        new("A group of giraffes is called a ___.", "tower"),
        new("A group of starlings in flight is called a ___.", "murmuration"),

        new("The dot over a lowercase i or j is called a ___.", "tittle"),
        new("The plastic tip at the end of a shoelace is called an ___.", "aglet"),
        new("The groove between your nose and your upper lip is called the ___.", "philtrum"),
        new("The smooth space between your eyebrows is called the ___.", "glabella"),
        new("The white crescent at the base of a fingernail is called the ___.", "lunule"),
        new("The metal band holding the eraser onto a pencil is called the ___.", "ferrule"),
        new("The # symbol is formally called an ___.", "octothorpe"),
        new("A holder that stops a hot coffee cup burning your hand is called a ___.", "zarf"),
        new("The dots on a domino or a die are called ___.", "pips"),
        new("The smell of rain falling on dry earth is called ___.", "petrichor"),

        new("Octopuses have ___ hearts.", "three"),
        new("An octopus's blood is ___ in colour.", "blue"),
        new("Camels have ___ eyelids on each eye.", "three"),
        new("Slugs have ___ noses.", "four"),
        new("Wombat droppings are ___ shaped.", "cube"),
        new("Polar bears' skin, underneath the fur, is ___ in colour.", "black"),
        new("Butterflies taste with their ___.", "feet"),
        new("Cats are unable to taste ___.", "sweetness"),
        new("Koalas, like humans, have ___.", "fingerprints"),
        new("A baby platypus is called a ___.", "puggle"),
        new("Among seahorses, it is the ___ that carries the young.", "male"),
        new("Elephants are the only mammal that cannot ___.", "jump"),
        new("A shrimp's heart is located in its ___.", "head"),

        new("Bananas are botanically berries, but ___ are not.", "strawberries"),
        new("Peanuts are not nuts at all - they are ___.", "legumes"),
        new("Properly sealed, honey ___ spoils.", "never"),

        new("The national animal of Scotland is the ___.", "unicorn"),
        new("The world's largest desert is ___.", "Antarctica"),
        new("Russia spans ___ time zones.", "eleven"),
        new("The Hawaiian alphabet has ___ letters.", "thirteen"),
        new("Alaska is the northernmost, westernmost and ___most state in the USA.", "eastern"),
        new("The world's smallest country by area is ___.", "Vatican City"),
        new("Wimbledon requires players to wear almost entirely ___.", "white"),

        new("Nintendo was founded in the year ___.", "1889"),
        new("Oxford University was already teaching before the ___ Empire existed.", "Aztec"),
        new("Cleopatra lived closer in time to the ___ than to the building of the Great Pyramid.", "Moon landing"),
        new("Sharks have existed for longer than ___ have.", "trees"),
        new("Tug of war was once an official ___ event.", "Olympic"),
        new("Bubble wrap was originally invented and sold as ___.", "wallpaper"),
        new("The first item ever sold on eBay was a broken ___.", "laser pointer"),
        new("The man who designed the Pringles can had his ashes buried in ___.", "a Pringles can"),
        new("The two M's in M&M's stand for Mars and ___.", "Murrie"),
        new("The W in George W. Bush stands for ___.", "Walker"),

        new("Saturn is light enough that it would ___ in water.", "float"),
        new("A single day on Venus lasts longer than a single ___ on Venus.", "year"),
        new("Venus is the only planet that rotates ___.", "clockwise"),

        new("The word 'defenestration' means throwing someone out of a ___.", "window"),
        new("The 'mare' in 'nightmare' originally meant an evil ___.", "spirit"),
        new("A 'sesquipedalian' is someone fond of very long ___.", "words"),
        new("'Strengths' is often called the longest English word with only one ___.", "vowel"),
        new("The English word with the most dictionary definitions is generally said to be ___.", "set"),
        new("The medical name for brain freeze is sphenopalatine ___.", "ganglioneuralgia"),
        new("'Pneumonoultramicroscopicsilicovolcanoconiosis' is a disease of the ___.", "lungs"),
        new("A 'jiffy' is a genuine scientific unit of ___.", "time"),
        new("Geese on the ground are a gaggle; in flight they are a ___.", "skein"),
        new("The fear of the number thirteen is called ___phobia.", "triskaideka"),
    };
}
