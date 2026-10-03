using GameNight.Domain.Abstractions;
using GameNight.Domain.Games.Bluff;
using Xunit;

namespace GameNight.Domain.Tests;

public class BluffTests
{
    private readonly BluffGameMode _mode = new();
    private readonly List<Player> _players = TestGame.Players("Ava", "Ben", "Cara");

    private Player Ava => _players[0];
    private Player Ben => _players[1];
    private Player Cara => _players[2];

    private StepResult Opening(int seed = 5, int questions = 3) =>
        _mode.Start(new GameStartContext(_players, new SeededRandomSource(seed), questions));

    private BluffState Start(int seed = 5, int questions = 3) =>
        (BluffState)Opening(seed, questions).State;

    private BluffState Step(BluffState state, GameEvent evt) =>
        (BluffState)_mode.Step(state, evt).State;

    private BluffView See(BluffState state, Player player) =>
        (BluffView)_mode.Project(state, player.Id);

    /// <summary>Three distinct lies, so voting opens with four options.</summary>
    private BluffState EveryoneLies(BluffState state) =>
        Step(Step(Step(state,
            TestGame.Lie(Ava, "a tuffet")),
            TestGame.Lie(Ben, "a wembly")),
            TestGame.Lie(Cara, "a gubbins"));

    private static string KeyOf(BluffState state, string text) =>
        state.Options.Single(o => o.Text == text).Key;

    private static string TruthKey(BluffState state) => state.Options.Single(o => o.IsTruth).Key;

    // --- opening ---------------------------------------------------------------------

    [Fact]
    public void The_writing_clock_starts_immediately()
    {
        Assert.Single(Opening().Effects.OfType<Effect.ScheduleTimer>());
    }

    [Fact]
    public void The_same_seed_asks_the_same_questions()
    {
        Assert.Equal(Start(seed: 9).Question, Start(seed: 9).Question);
    }

    // --- writing ---------------------------------------------------------------------

    [Fact]
    public void Voting_opens_once_everybody_has_written_something()
    {
        var state = EveryoneLies(Start());

        Assert.Equal(BluffPhase.Voting, state.Phase);
        // Three lies plus the truth.
        Assert.Equal(4, state.Options.Count);
    }

    [Fact]
    public void Nobody_sees_anybody_elses_lie_while_writing()
    {
        var state = Step(Start(), TestGame.Lie(Ava, "a tuffet"));

        Assert.Equal("a tuffet", See(state, Ava).YourLie);
        Assert.Null(See(state, Ben).YourLie);
        Assert.Empty(See(state, Ben).Options);
    }

    [Fact]
    public void Typing_the_real_answer_is_bounced_and_quietly_credited()
    {
        var start = Start();

        var state = Step(start, TestGame.Lie(Ava, start.Question.Answer.ToUpperInvariant()));

        // It cannot go on the board - two correct answers would break the round.
        Assert.DoesNotContain(Ava.Id, state.Lies.Keys);
        Assert.Contains(Ava.Id, state.KnewIt);
        Assert.NotNull(See(state, Ava).Rejection);
        // And it stays between them and the server.
        Assert.Null(See(state, Ben).Rejection);
    }

    [Fact]
    public void Two_people_who_invent_the_same_lie_share_one_option()
    {
        var state = Step(Step(Step(Start(),
            TestGame.Lie(Ava, "a wembly")),
            TestGame.Lie(Ben, "A Wembly!")),
            TestGame.Lie(Cara, "a gubbins"));

        Assert.Equal(3, state.Options.Count);

        var shared = state.Options.Single(o => !o.IsTruth && o.Authors.Count == 2);
        Assert.Contains(Ava.Id, shared.Authors);
        Assert.Contains(Ben.Id, shared.Authors);
    }

    [Fact]
    public void Running_out_of_writing_time_goes_with_whatever_arrived()
    {
        var state = Step(Start(), TestGame.Lie(Ava, "a tuffet"));

        state = Step(state, new GameEvent.TimerFired(BluffGameMode.WritingTimer(1)));

        Assert.Equal(BluffPhase.Voting, state.Phase);
        Assert.Equal(2, state.Options.Count);
    }

    // --- the two rules the whole game rests on ---------------------------------------

    [Fact]
    public void You_are_told_which_option_is_yours_and_nobody_else_is()
    {
        var state = EveryoneLies(Start());
        var key = KeyOf(state, "a tuffet");

        Assert.True(See(state, Ava).Options.Single(o => o.Key == key).IsYours);
        Assert.False(See(state, Ben).Options.Single(o => o.Key == key).IsYours);
        Assert.False(See(state, Cara).Options.Single(o => o.Key == key).IsYours);
    }

    [Fact]
    public void Nobody_is_told_which_option_is_true_until_the_reveal()
    {
        var state = EveryoneLies(Start());

        foreach (var player in _players)
        {
            Assert.All(See(state, player).Options, o => Assert.Null(o.IsTruth));
            Assert.Null(See(state, player).Answer);
        }
    }

    [Fact]
    public void Everyone_sees_the_options_in_the_same_order()
    {
        var state = EveryoneLies(Start());

        var hers = See(state, Ava).Options.Select(o => (o.Key, o.Text));
        var his = See(state, Ben).Options.Select(o => (o.Key, o.Text));

        Assert.Equal(hers, his);
    }

    [Fact]
    public void You_cannot_vote_for_your_own_lie()
    {
        var state = EveryoneLies(Start());

        var result = _mode.Step(state, TestGame.PickOption(Ava, KeyOf(state, "a tuffet")));

        Assert.Same(state, result.State);
    }

    [Fact]
    public void Your_own_vote_comes_back_to_you_and_nobody_elses()
    {
        var state = EveryoneLies(Start());
        state = Step(state, TestGame.PickOption(Ava, KeyOf(state, "a wembly")));

        Assert.NotNull(See(state, Ava).YourVote);
        Assert.Null(See(state, Ben).YourVote);
    }

    // --- scoring ---------------------------------------------------------------------

    [Fact]
    public void Finding_the_truth_is_worth_three()
    {
        var state = EveryoneLies(Start());
        var truth = TruthKey(state);

        state = Step(state, TestGame.PickOption(Ava, truth));
        state = Step(state, TestGame.PickOption(Ben, truth));
        state = Step(state, TestGame.PickOption(Cara, truth));

        Assert.Equal(BluffPhase.Reveal, state.Phase);
        Assert.All(_players, p => Assert.Equal(3, state.Scores[p.Id]));
    }

    [Fact]
    public void Being_believed_is_worth_two_from_every_person_fooled()
    {
        var state = EveryoneLies(Start());
        var tuffet = KeyOf(state, "a tuffet");

        state = Step(state, TestGame.PickOption(Ben, tuffet));
        state = Step(state, TestGame.PickOption(Cara, tuffet));
        state = Step(state, TestGame.PickOption(Ava, TruthKey(state)));

        // Two people fell for Ava's lie, and she found the truth herself.
        Assert.Equal(2 + 2 + 3, state.Scores[Ava.Id]);
        Assert.Equal(0, state.Scores[Ben.Id]);
        Assert.Equal(0, state.Scores[Cara.Id]);
    }

    [Fact]
    public void Everything_is_laid_bare_at_the_reveal()
    {
        var start = Start();
        var state = EveryoneLies(start);
        var tuffet = KeyOf(state, "a tuffet");

        state = Step(state, TestGame.PickOption(Ben, tuffet));
        state = Step(state, TestGame.PickOption(Cara, tuffet));
        state = Step(state, TestGame.PickOption(Ava, TruthKey(state)));

        var view = See(state, Cara);

        Assert.Equal(start.Question.Answer, view.Answer);
        Assert.All(view.Options, o => Assert.NotNull(o.IsTruth));

        var lie = view.Options.Single(o => o.Text == "a tuffet");
        Assert.Equal(new[] { "Ava" }, lie.AuthorNames);
        Assert.Equal(2, lie.VoterNames.Count);
    }

    // --- moving on -------------------------------------------------------------------

    [Fact]
    public void Continuing_asks_a_new_question_and_keeps_the_scores()
    {
        var start = Start(questions: 3);
        var state = EveryoneLies(start);
        state = Step(state, TestGame.PickOption(Ava, TruthKey(state)));
        state = Step(state, TestGame.PickOption(Ben, TruthKey(state)));
        state = Step(state, TestGame.PickOption(Cara, TruthKey(state)));

        var carried = state.Scores[Ava.Id];
        var next = Step(state, TestGame.Continue(Ava));

        Assert.Equal(2, next.QuestionNumber);
        Assert.Equal(BluffPhase.Writing, next.Phase);
        Assert.NotEqual(start.Question, next.Question);
        Assert.Empty(next.Lies);
        Assert.Empty(next.Options);
        Assert.Equal(carried, next.Scores[Ava.Id]);
    }

    [Fact]
    public void The_game_ends_after_the_last_question()
    {
        var state = Start(questions: 1);
        state = EveryoneLies(state);
        state = Step(state, TestGame.PickOption(Ava, TruthKey(state)));
        state = Step(state, TestGame.PickOption(Ben, TruthKey(state)));
        state = Step(state, TestGame.PickOption(Cara, TruthKey(state)));

        var result = _mode.Step(state, TestGame.Continue(Ava));

        Assert.True(((BluffState)result.State).IsFinished);

        var ending = Assert.Single(result.Effects.OfType<Effect.Finished>());
        Assert.Equal(_players.Count, ending.Standings.Count);
        Assert.Equal(1, ending.Standings.Min(s => s.Rank));
    }
}
