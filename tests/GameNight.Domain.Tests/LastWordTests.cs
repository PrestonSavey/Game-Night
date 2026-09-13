using GameNight.Domain.Abstractions;
using GameNight.Domain.Games.LastWord;
using Xunit;

namespace GameNight.Domain.Tests;

/// <summary>
/// The whole game, elimination and all, without a server, a socket or a real clock.
/// Ten seconds of turn timer costs nothing here because time arrives as an event.
/// </summary>
public class LastWordTests
{
    private readonly LastWordGameMode _mode = new(FakeWordList.AtoD());
    private readonly List<Player> _players = TestGame.Players("Ava", "Ben", "Cara");

    private Player Ava => _players[0];
    private Player Ben => _players[1];
    private Player Cara => _players[2];

    private StepResult Opening(int seed = 1, int seconds = 10) =>
        _mode.Start(new GameStartContext(_players, new SeededRandomSource(seed), seconds));

    private LastWordState Start(int seed = 1, int seconds = 10) =>
        (LastWordState)Opening(seed, seconds).State;

    private LastWordState Step(LastWordState state, GameEvent evt) =>
        (LastWordState)_mode.Step(state, evt).State;

    // --- opening ---------------------------------------------------------------------

    [Fact]
    public void The_first_player_is_on_the_clock_before_anyone_has_moved()
    {
        var opening = Opening(seconds: 10);

        var timer = Assert.Single(opening.Effects.OfType<Effect.ScheduleTimer>());
        Assert.Equal(TimeSpan.FromSeconds(10), timer.Delay);
        Assert.Equal(Ava.Id, ((LastWordState)opening.State).CurrentPlayer);
    }

    [Fact]
    public void The_same_seed_deals_the_same_letters()
    {
        Assert.Equal(Start(seed: 7).Pair, Start(seed: 7).Pair);
    }

    [Fact]
    public void The_host_chooses_the_turn_length()
    {
        Assert.Equal(5, Start(seconds: 5).TurnSeconds);
        Assert.Equal(20, Start(seconds: 20).TurnSeconds);
    }

    // --- answering -------------------------------------------------------------------

    [Fact]
    public void A_good_word_passes_the_turn_along()
    {
        var next = Step(Start(), TestGame.Word(Ava, "acid"));

        Assert.Equal(Ben.Id, next.CurrentPlayer);
        Assert.Contains("acid", next.Used);
        Assert.Equal("acid", next.JustAccepted);
    }

    [Fact]
    public void A_good_word_restarts_the_clock_for_the_next_player()
    {
        var start = Start();

        var result = _mode.Step(start, TestGame.Word(Ava, "acid"));

        Assert.Contains(result.Effects.OfType<Effect.CancelTimer>(),
            e => e.TimerId == LastWordGameMode.TimerId(start.TurnToken));
        Assert.Single(result.Effects.OfType<Effect.ScheduleTimer>());
    }

    [Fact]
    public void Only_the_player_on_the_clock_may_answer()
    {
        var start = Start();

        var result = _mode.Step(start, TestGame.Word(Ben, "acid"));

        Assert.Same(start, result.State);
    }

    [Theory]
    [InlineData("bald", "start")]      // wrong first letter
    [InlineData("apple", "end")]       // wrong last letter
    [InlineData("abcd", "know")]       // not in the dictionary
    [InlineData("a", "least")]         // too short
    public void A_bad_word_is_rejected_with_a_reason(string word, string because)
    {
        var next = Step(Start(), TestGame.Word(Ava, word));

        Assert.NotNull(next.Rejection);
        Assert.Contains(because, next.Rejection!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_bad_word_does_not_cost_you_your_turn()
    {
        var start = Start();

        var result = _mode.Step(start, TestGame.Word(Ava, "banana"));
        var next = (LastWordState)result.State;

        // Still yours, still the same clock. Only time eliminates anybody.
        Assert.Equal(Ava.Id, next.CurrentPlayer);
        Assert.Equal(start.TurnToken, next.TurnToken);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void A_word_somebody_already_used_is_rejected()
    {
        var afterAva = Step(Start(), TestGame.Word(Ava, "acid"));

        var next = Step(afterAva, TestGame.Word(Ben, "acid"));

        Assert.Contains("already", next.Rejection!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Ben.Id, next.CurrentPlayer);
    }

    [Fact]
    public void A_rejection_is_only_shown_to_the_player_who_typed_it()
    {
        var next = Step(Start(), TestGame.Word(Ava, "banana"));

        var hers = (LastWordView)_mode.Project(next, Ava.Id);
        var his = (LastWordView)_mode.Project(next, Ben.Id);

        Assert.NotNull(hers.Rejection);
        Assert.Null(his.Rejection);
    }

    // --- the clock -------------------------------------------------------------------

    [Fact]
    public void Running_out_of_time_knocks_you_out()
    {
        var start = Start();

        var next = Step(start, new GameEvent.TimerFired(LastWordGameMode.TimerId(start.TurnToken)));

        Assert.DoesNotContain(Ava.Id, next.Alive);
        Assert.Contains(Ava.Id, next.KnockedOut);
        Assert.Equal(Ben.Id, next.CurrentPlayer);
    }

    [Fact]
    public void A_timer_from_a_turn_that_has_already_ended_is_ignored()
    {
        var start = Start();
        var afterAva = Step(start, TestGame.Word(Ava, "acid"));

        // Ava's timer, arriving late, must not eliminate Ben.
        var result = _mode.Step(
            afterAva, new GameEvent.TimerFired(LastWordGameMode.TimerId(start.TurnToken)));

        Assert.Same(afterAva, result.State);
    }

    [Fact]
    public void Knocking_someone_out_wipes_the_words_and_restarts_the_clock()
    {
        var start = Start();
        var afterAva = Step(start, TestGame.Word(Ava, "acid"));

        var result = _mode.Step(
            afterAva, new GameEvent.TimerFired(LastWordGameMode.TimerId(afterAva.TurnToken)));
        var next = (LastWordState)result.State;

        Assert.Empty(next.Used);
        Assert.Single(result.Effects.OfType<Effect.ScheduleTimer>());
    }

    // --- ending ----------------------------------------------------------------------

    [Fact]
    public void The_last_player_standing_wins()
    {
        var state = Start();

        // Ava times out, then Ben times out, leaving Cara.
        state = Step(state, new GameEvent.TimerFired(LastWordGameMode.TimerId(state.TurnToken)));
        var result = _mode.Step(
            state, new GameEvent.TimerFired(LastWordGameMode.TimerId(state.TurnToken)));

        var finished = (LastWordState)result.State;
        Assert.True(finished.IsFinished);
        Assert.Equal(new[] { Cara.Id }, finished.Alive);

        var ending = Assert.Single(result.Effects.OfType<Effect.Finished>());
        Assert.Equal(3, ending.Standings.Count);

        var winner = Assert.Single(ending.Standings, s => s.Rank == 1);
        Assert.Equal(Cara.Id, winner.Player);
        Assert.Equal("Winner", winner.Label);

        // Last one knocked out came closest.
        Assert.Equal(Ben.Id, ending.Standings.Single(s => s.Rank == 2).Player);
        Assert.Equal(Ava.Id, ending.Standings.Single(s => s.Rank == 3).Player);
    }

    // --- people leaving --------------------------------------------------------------

    [Fact]
    public void Leaving_when_it_is_not_your_turn_does_not_disturb_the_clock()
    {
        var start = Start();

        var result = _mode.Step(start, new GameEvent.PlayerLeft(Cara.Id));
        var next = (LastWordState)result.State;

        Assert.Equal(Ava.Id, next.CurrentPlayer);
        Assert.Equal(start.TurnToken, next.TurnToken);
        Assert.Empty(result.Effects);
        Assert.DoesNotContain(Cara.Id, next.Alive);
    }

    [Fact]
    public void Leaving_on_your_own_turn_passes_play_on()
    {
        var start = Start();

        var next = Step(start, new GameEvent.PlayerLeft(Ava.Id));

        Assert.Equal(Ben.Id, next.CurrentPlayer);
        Assert.NotEqual(start.TurnToken, next.TurnToken);
    }

    [Fact]
    public void Everyone_leaving_but_one_ends_the_game()
    {
        var state = Step(Start(), new GameEvent.PlayerLeft(Cara.Id));

        var result = _mode.Step(state, new GameEvent.PlayerLeft(Ava.Id));

        Assert.True(((LastWordState)result.State).IsFinished);
        Assert.Single(result.Effects.OfType<Effect.Finished>());
    }
}
