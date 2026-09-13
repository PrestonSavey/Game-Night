using GameNight.Domain.Abstractions;
using GameNight.Domain.Games.Spectrum;
using Xunit;

namespace GameNight.Domain.Tests;

/// <summary>
/// These cover the parts already written for you, so they should be green from the
/// first build. If one of them goes red later, something has regressed in the
/// redaction rules - which is the one place in this codebase a bug is a cheating bug.
/// </summary>
public class SpectrumStartAndProjectionTests
{
    private static readonly SpectrumGameMode Mode = new();

    private static SpectrumState Start(IReadOnlyList<Player> players, int seed = 42) =>
        (SpectrumState)Mode.Start(new GameStartContext(players, new SeededRandomSource(seed))).State;

    [Fact]
    public void The_same_seed_produces_the_same_game()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");

        var first = Start(players);
        var second = Start(players);

        Assert.Equal(first.Target, second.Target);
        Assert.Equal(first.Card, second.Card);
        Assert.Equal(first.Deck, second.Deck);
    }

    [Fact]
    public void A_different_seed_produces_a_different_game()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");

        // Not a guarantee for any single pair of seeds, but across a spread it must vary,
        // otherwise the random source is not actually being consulted.
        var targets = Enumerable.Range(1, 25).Select(seed => Start(players, seed).Target).ToHashSet();

        Assert.True(targets.Count > 1);
    }

    [Fact]
    public void Everyone_starts_on_zero_and_the_first_player_gives_the_first_clue()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");

        var state = Start(players);

        Assert.Equal(SpectrumPhase.AwaitingClue, state.Phase);
        Assert.Equal(1, state.RoundNumber);
        Assert.Equal(players.Count, state.TotalRounds);
        Assert.Equal(players[0].Id, state.ClueGiver.Id);
        Assert.All(players, p => Assert.Equal(0, state.Scores[p.Id]));
    }

    [Fact]
    public void The_target_is_always_on_the_dial()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");

        foreach (var seed in Enumerable.Range(1, 200))
        {
            var target = Start(players, seed).Target;
            Assert.InRange(target, 1, 100);
        }
    }

    [Fact]
    public void The_clue_giver_can_see_the_target()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");
        var state = Start(players);

        var view = (SpectrumView)Mode.Project(state, players[0].Id);

        Assert.True(view.YouAreClueGiver);
        Assert.Equal(state.Target, view.Target);
    }

    [Fact]
    public void Nobody_else_can_see_the_target()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");
        var state = Start(players);

        foreach (var guesser in players.Skip(1))
        {
            var view = (SpectrumView)Mode.Project(state, guesser.Id);

            Assert.False(view.YouAreClueGiver);
            Assert.Null(view.Target);
        }
    }

    [Fact]
    public void During_guessing_you_can_see_that_someone_answered_but_not_what_they_said()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");
        var state = Start(players) with
        {
            Phase = SpectrumPhase.Guessing,
            Clue = "fire",
            Guesses = new Dictionary<PlayerId, int> { [players[1].Id] = 88 },
        };

        var view = (SpectrumView)Mode.Project(state, players[2].Id);

        var ben = view.Guesses.Single(g => g.DisplayName == "Ben");
        Assert.True(ben.HasAnswered);
        Assert.Null(ben.Position);

        var cara = view.Guesses.Single(g => g.DisplayName == "Cara");
        Assert.False(cara.HasAnswered);
    }

    [Fact]
    public void You_can_always_see_your_own_guess()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");
        var state = Start(players) with
        {
            Phase = SpectrumPhase.Guessing,
            Clue = "fire",
            Guesses = new Dictionary<PlayerId, int> { [players[1].Id] = 88 },
        };

        var view = (SpectrumView)Mode.Project(state, players[1].Id);

        Assert.Equal(88, view.YourGuess);
    }

    [Fact]
    public void At_reveal_everyone_sees_the_target_and_every_guess()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");
        var state = Start(players) with
        {
            Phase = SpectrumPhase.Reveal,
            Clue = "fire",
            Guesses = new Dictionary<PlayerId, int>
            {
                [players[1].Id] = 88,
                [players[2].Id] = 61,
            },
        };

        var view = (SpectrumView)Mode.Project(state, players[2].Id);

        Assert.Equal(state.Target, view.Target);
        Assert.Equal(88, view.Guesses.Single(g => g.DisplayName == "Ben").Position);
        Assert.Equal(61, view.Guesses.Single(g => g.DisplayName == "Cara").Position);
    }

    [Fact]
    public void The_clue_giver_is_not_listed_among_the_guessers()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");
        var state = Start(players);

        var view = (SpectrumView)Mode.Project(state, players[1].Id);

        Assert.DoesNotContain(view.Guesses, g => g.DisplayName == "Ava");
        Assert.Equal(2, view.Guesses.Count);
    }

    [Fact]
    public void The_scoreboard_is_ordered_best_first()
    {
        var players = TestGame.Players("Ava", "Ben", "Cara");
        var state = Start(players) with
        {
            Scores = new Dictionary<PlayerId, int>
            {
                [players[0].Id] = 1,
                [players[1].Id] = 7,
                [players[2].Id] = 4,
            },
        };

        var view = (SpectrumView)Mode.Project(state, players[0].Id);

        Assert.Equal(new[] { "Ben", "Cara", "Ava" }, view.Scores.Select(s => s.DisplayName));
    }
}
