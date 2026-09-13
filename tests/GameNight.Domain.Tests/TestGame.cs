using System.Text.Json;
using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Tests;

/// <summary>Small builders so the tests read like the game and not like plumbing.</summary>
internal static class TestGame
{
    public static List<Player> Players(params string[] names) =>
        names.Select(name => new Player(PlayerId.New(), name)).ToList();

    public static JsonElement Payload(object value) => JsonSerializer.SerializeToElement(value);

    public static GameEvent.Action Clue(Player player, string clue) =>
        new(player.Id, Payload(new { type = "clue", clue }));

    public static GameEvent.Action Guess(Player player, int position) =>
        new(player.Id, Payload(new { type = "guess", position }));

    public static GameEvent.Action Continue(Player player) =>
        new(player.Id, Payload(new { type = "continue" }));

    public static GameEvent.Action Word(Player player, string word) =>
        new(player.Id, Payload(new { type = "word", word }));

    public static GameEvent.Action CallVote(Player player) =>
        new(player.Id, Payload(new { type = "callVote" }));

    public static GameEvent.Action VoteFor(Player player, Player target) =>
        new(player.Id, Payload(new { type = "vote", target = target.Id.ToString() }));

    public static GameEvent.Action GuessWord(Player player, string word) =>
        new(player.Id, Payload(new { type = "guess", word }));
}
