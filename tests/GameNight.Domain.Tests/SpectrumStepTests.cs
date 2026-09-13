using GameNight.Domain.Abstractions;
using GameNight.Domain.Games.Spectrum;
using Xunit;

namespace GameNight.Domain.Tests;

/// <summary>
/// These are RED on purpose. They are the specification for SpectrumGameMode.Step,
/// which is your week 1 exercise. Work down the file making them green one at a time.
///
/// Note what is NOT here: no server, no SignalR, no Task.Delay, no clock. A full round
/// including a timeout runs in microseconds, because time enters the game as an event.
/// </summary>
public class SpectrumStepTests
{
    private static readonly SpectrumGameMode Mode = new();

    private readonly List<Player> _players = TestGame.Players("Ava", "Ben", "Cara");

    private SpectrumState Start(int seed = 42) =>
        (SpectrumState)Mode.Start(new GameStartContext(_players, new SeededRandomSource(seed))).State;

    private Player ClueGiver => _players[0];
    private Player Ben => _players[1];
    private Player Cara => _players[2];

    // --- giving the clue ------------------------------------------------------------

    [Fact]
    public void Giving_a_clue_moves_the_round_to_guessing()
    {
        var result = Mode.Step(Start(), TestGame.Clue(ClueGiver, "fire"));

        var state = Assert.IsType<SpectrumState>(result.State);
        Assert.Equal(SpectrumPhase.Guessing, state.Phase);
        Assert.Equal("fire", state.Clue);
    }

    [Fact]
    public void Giving_a_clue_starts_the_guessing_timer()
    {
        var result = Mode.Step(Start(), TestGame.Clue(ClueGiver, "fire"));

        var timer = Assert.Single(result.Effects.OfType<Effect.ScheduleTimer>());
        Assert.Equal(SpectrumGameMode.RoundTimerId, timer.TimerId);
        Assert.Equal(SpectrumGameMode.GuessTimeout, timer.Delay);
    }

    [Fact]
    public void A_guesser_cannot_give_the_clue()
    {
        var start = Start();

        var result = Mode.Step(start, TestGame.Clue(Ben, "fire"));

        Assert.Same(start, result.State);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void A_malformed_payload_is_ignored_rather_than_throwing()
    {
        var start = Start();
        var nonsense = new GameEvent.Action(ClueGiver.Id, TestGame.Payload(new { type = "wat" }));

        var result = Mode.Step(start, nonsense);

        Assert.Same(start, result.State);
    }

    // --- guessing --------------------------------------------------------------------

    [Fact]
    public void A_guess_is_recorded()
    {
        var guessing = (SpectrumState)Mode.Step(Start(), TestGame.Clue(ClueGiver, "fire")).State;

        var state = (SpectrumState)Mode.Step(guessing, TestGame.Guess(Ben, 88)).State;

        Assert.Equal(88, state.Guesses[Ben.Id]);
    }

    [Fact]
    public void Changing_your_mind_before_the_reveal_overwrites_your_guess()
    {
        var guessing = (SpectrumState)Mode.Step(Start(), TestGame.Clue(ClueGiver, "fire")).State;
        var once = (SpectrumState)Mode.Step(guessing, TestGame.Guess(Ben, 88)).State;

        var twice = (SpectrumState)Mode.Step(once, TestGame.Guess(Ben, 71)).State;

        Assert.Equal(71, twice.Guesses[Ben.Id]);
    }

    [Fact]
    public void The_clue_giver_cannot_guess()
    {
        var guessing = (SpectrumState)Mode.Step(Start(), TestGame.Clue(ClueGiver, "fire")).State;

        var result = Mode.Step(guessing, TestGame.Guess(ClueGiver, 50));

        Assert.Same(guessing, result.State);
    }

    [Fact]
    public void The_round_reveals_as_soon_as_the_last_guesser_answers()
    {
        var guessing = (SpectrumState)Mode.Step(Start(), TestGame.Clue(ClueGiver, "fire")).State;
        var partway = (SpectrumState)Mode.Step(guessing, TestGame.Guess(Ben, 88)).State;
        Assert.Equal(SpectrumPhase.Guessing, partway.Phase);

        var result = Mode.Step(partway, TestGame.Guess(Cara, 61));

        var state = (SpectrumState)result.State;
        Assert.Equal(SpectrumPhase.Reveal, state.Phase);

        // Nobody should be sitting through a timer that no longer means anything.
        Assert.Contains(result.Effects.OfType<Effect.CancelTimer>(),
            e => e.TimerId == SpectrumGameMode.RoundTimerId);
    }

    // --- scoring ---------------------------------------------------------------------

    [Theory]
    [InlineData(50, 50)]
    [InlineData(52, 50)]
    [InlineData(48, 50)]
    public void A_near_perfect_guess_scores_the_most(int guess, int target) =>
        Assert.Equal(4, SpectrumGameMode.ScoreFor(guess, target));

    [Fact]
    public void Scores_fall_off_as_the_guess_gets_further_away()
    {
        var close = SpectrumGameMode.ScoreFor(52, 50);
        var nearby = SpectrumGameMode.ScoreFor(58, 50);
        var far = SpectrumGameMode.ScoreFor(68, 50);
        var wild = SpectrumGameMode.ScoreFor(5, 50);

        Assert.True(close > nearby);
        Assert.True(nearby > far);
        Assert.True(far > wild);
        Assert.Equal(0, wild);
    }

    [Fact]
    public void Distance_is_symmetric()
    {
        Assert.Equal(SpectrumGameMode.ScoreFor(58, 50), SpectrumGameMode.ScoreFor(42, 50));
    }

    [Fact]
    public void Revealing_awards_points_to_the_guessers()
    {
        var start = Start();
        var guessing = (SpectrumState)Mode.Step(start, TestGame.Clue(ClueGiver, "fire")).State;
        var withOne = (SpectrumState)Mode.Step(guessing, TestGame.Guess(Ben, start.Target)).State;
        var revealed = (SpectrumState)Mode.Step(withOne, TestGame.Guess(Cara, 1)).State;

        Assert.Equal(SpectrumGameMode.ScoreFor(start.Target, start.Target), revealed.Scores[Ben.Id]);
        Assert.Equal(SpectrumGameMode.ScoreFor(1, start.Target), revealed.Scores[Cara.Id]);
    }

    // --- moving on -------------------------------------------------------------------

    [Fact]
    public void Continuing_starts_the_next_round_with_the_next_clue_giver()
    {
        var revealed = Revealed(out var start);

        var next = (SpectrumState)Mode.Step(revealed, TestGame.Continue(ClueGiver)).State;

        Assert.Equal(2, next.RoundNumber);
        Assert.Equal(Ben.Id, next.ClueGiver.Id);
        Assert.Equal(SpectrumPhase.AwaitingClue, next.Phase);
        Assert.Empty(next.Guesses);
        Assert.Null(next.Clue);
        Assert.NotEqual(start.Card, next.Card);
    }

    [Fact]
    public void Scores_carry_across_rounds()
    {
        var revealed = Revealed(out _);
        var carried = revealed.Scores[Ben.Id];

        var next = (SpectrumState)Mode.Step(revealed, TestGame.Continue(ClueGiver)).State;

        Assert.Equal(carried, next.Scores[Ben.Id]);
    }

    [Fact]
    public void The_game_finishes_after_everyone_has_given_a_clue()
    {
        var state = Start();

        for (var round = 0; round < _players.Count; round++)
        {
            var giver = state.ClueGiver;
            state = (SpectrumState)Mode.Step(state, TestGame.Clue(giver, "fire")).State;

            foreach (var guesser in _players.Where(p => p.Id != giver.Id))
            {
                state = (SpectrumState)Mode.Step(state, TestGame.Guess(guesser, 50)).State;
            }

            var result = Mode.Step(state, TestGame.Continue(giver));
            state = (SpectrumState)result.State;

            if (round == _players.Count - 1)
            {
                Assert.True(state.IsFinished);
                var finished = Assert.Single(result.Effects.OfType<Effect.Finished>());
                Assert.Equal(_players.Count, finished.Standings.Count);
                Assert.Equal(1, finished.Standings.Min(s => s.Rank));
            }
        }
    }

    // --- timeouts --------------------------------------------------------------------

    [Fact]
    public void Running_out_of_time_while_guessing_reveals_with_whatever_arrived()
    {
        var guessing = (SpectrumState)Mode.Step(Start(), TestGame.Clue(ClueGiver, "fire")).State;
        var partway = (SpectrumState)Mode.Step(guessing, TestGame.Guess(Ben, 88)).State;

        var state = (SpectrumState)Mode
            .Step(partway, new GameEvent.TimerFired(SpectrumGameMode.RoundTimerId)).State;

        Assert.Equal(SpectrumPhase.Reveal, state.Phase);
        Assert.Equal(0, state.Scores[Cara.Id]);
    }

    [Fact]
    public void A_stale_timer_from_a_previous_round_is_ignored()
    {
        var start = Start();

        var result = Mode.Step(start, new GameEvent.TimerFired("some.other.timer"));

        Assert.Same(start, result.State);
    }

    // --- players leaving -------------------------------------------------------------

    [Fact]
    public void If_the_last_outstanding_guesser_leaves_the_round_reveals()
    {
        var guessing = (SpectrumState)Mode.Step(Start(), TestGame.Clue(ClueGiver, "fire")).State;
        var partway = (SpectrumState)Mode.Step(guessing, TestGame.Guess(Ben, 88)).State;

        var state = (SpectrumState)Mode.Step(partway, new GameEvent.PlayerLeft(Cara.Id)).State;

        Assert.Equal(SpectrumPhase.Reveal, state.Phase);
    }

    private SpectrumState Revealed(out SpectrumState start)
    {
        start = Start();
        var guessing = (SpectrumState)Mode.Step(start, TestGame.Clue(ClueGiver, "fire")).State;
        var withOne = (SpectrumState)Mode.Step(guessing, TestGame.Guess(Ben, 88)).State;
        return (SpectrumState)Mode.Step(withOne, TestGame.Guess(Cara, 61)).State;
    }
}
