using GameNight.Domain.Abstractions;
using GameNight.Domain.Games.Impostor;
using Xunit;

namespace GameNight.Domain.Tests;

public class ImpostorTests
{
    private readonly ImpostorGameMode _mode = new();
    private readonly List<Player> _players = TestGame.Players("Ava", "Ben", "Cara", "Dev");

    private ImpostorState Start(int seed = 3, int rounds = 1) =>
        (ImpostorState)_mode.Start(
            new GameStartContext(_players, new SeededRandomSource(seed), rounds)).State;

    private ImpostorState Step(ImpostorState state, GameEvent evt) =>
        (ImpostorState)_mode.Step(state, evt).State;

    private Player Impostor(ImpostorState state) => _players.Single(p => p.Id == state.Impostor);

    private List<Player> Innocents(ImpostorState state) =>
        _players.Where(p => p.Id != state.Impostor).ToList();

    /// <summary>Everyone gives one clue, in turn order, so a vote becomes legal.</summary>
    private ImpostorState FullRoundOfClues(ImpostorState state)
    {
        for (var i = 0; i < _players.Count; i++)
        {
            state = Step(state, TestGame.Clue(state.Current, "something"));
        }
        return state;
    }

    // --- the setup -------------------------------------------------------------------

    [Fact]
    public void Exactly_one_player_is_the_impostor()
    {
        var state = Start();

        Assert.Contains(_players, p => p.Id == state.Impostor);
        Assert.Single(_players.Where(p => p.Id == state.Impostor));
    }

    [Fact]
    public void The_impostor_is_told_they_are_the_impostor_and_nothing_else()
    {
        var state = Start();

        var view = (ImpostorView)_mode.Project(state, state.Impostor);

        Assert.True(view.YouAreTheImpostor);
        Assert.Null(view.SecretWord);
        // The category is their only foothold, so they do get that.
        Assert.False(string.IsNullOrWhiteSpace(view.Category));
    }

    [Fact]
    public void Everybody_else_is_told_the_word()
    {
        var state = Start();

        foreach (var innocent in Innocents(state))
        {
            var view = (ImpostorView)_mode.Project(state, innocent.Id);

            Assert.False(view.YouAreTheImpostor);
            Assert.Equal(state.Secret.Word, view.SecretWord);
        }
    }

    [Fact]
    public void Nobody_can_work_out_who_the_impostor_is_from_their_own_view()
    {
        var state = Start();

        foreach (var player in _players)
        {
            var view = (ImpostorView)_mode.Project(state, player.Id);

            Assert.Null(view.ImpostorName);
            Assert.DoesNotContain(view.Players, p => p.WasTheImpostor);
        }
    }

    // --- clues -----------------------------------------------------------------------

    [Fact]
    public void Clues_go_round_the_table_in_order()
    {
        var state = Start();
        var first = state.Current;

        state = Step(state, TestGame.Clue(first, "sand"));

        Assert.Equal(_players[1].Id, state.Current.Id);
        Assert.Equal("sand", Assert.Single(state.Clues).Word);
    }

    [Fact]
    public void Only_the_player_whose_turn_it_is_may_give_a_clue()
    {
        var state = Start();

        var result = _mode.Step(state, TestGame.Clue(_players[2], "sand"));

        Assert.Same(state, result.State);
    }

    // --- calling the vote ------------------------------------------------------------

    [Fact]
    public void A_vote_cannot_be_called_before_everyone_has_spoken()
    {
        var state = Start();

        var result = _mode.Step(state, TestGame.CallVote(_players[0]));

        // Voting on no information is a coin flip, not a deduction.
        Assert.Same(state, result.State);
        Assert.Equal(ImpostorPhase.Clues, ((ImpostorState)result.State).Phase);
    }

    [Fact]
    public void Anyone_can_call_a_vote_once_everyone_has_spoken()
    {
        var state = FullRoundOfClues(Start());

        var called = Step(state, TestGame.CallVote(_players[2]));

        Assert.Equal(ImpostorPhase.Voting, called.Phase);
    }

    [Fact]
    public void Two_clues_each_can_be_required_instead()
    {
        var state = FullRoundOfClues(Start(rounds: 2));

        var tooEarly = _mode.Step(state, TestGame.CallVote(_players[0]));
        Assert.Same(state, tooEarly.State);

        var afterSecond = Step(FullRoundOfClues(state), TestGame.CallVote(_players[0]));
        Assert.Equal(ImpostorPhase.Voting, afterSecond.Phase);
    }

    // --- voting ----------------------------------------------------------------------

    [Fact]
    public void You_cannot_vote_for_yourself()
    {
        var state = Step(FullRoundOfClues(Start()), TestGame.CallVote(_players[0]));

        var result = _mode.Step(state, TestGame.VoteFor(_players[0], _players[0]));

        Assert.Same(state, result.State);
    }

    [Fact]
    public void Your_own_vote_comes_back_to_you_and_nobody_elses()
    {
        var state = Step(FullRoundOfClues(Start()), TestGame.CallVote(_players[0]));
        state = Step(state, TestGame.VoteFor(_players[0], _players[1]));

        var yours = (ImpostorView)_mode.Project(state, _players[0].Id);
        var theirs = (ImpostorView)_mode.Project(state, _players[2].Id);

        Assert.Equal(_players[1].Id.ToString(), yours.YourVote);
        Assert.Null(theirs.YourVote);
        // You can see THAT they voted, never for whom.
        Assert.True(theirs.Players.Single(p => p.DisplayName == "Ava").HasVoted);
    }

    [Fact]
    public void Everyone_agreeing_on_the_impostor_catches_them()
    {
        var state = Step(FullRoundOfClues(Start()), TestGame.CallVote(_players[0]));
        var impostor = Impostor(state);

        state = VoteEveryone(state, onto: impostor);

        Assert.Equal(ImpostorPhase.LastChance, state.Phase);
        Assert.Equal(impostor.Id, state.Accused);
    }

    [Fact]
    public void Pinning_the_wrong_player_hands_the_impostor_the_game()
    {
        var state = Step(FullRoundOfClues(Start()), TestGame.CallVote(_players[0]));
        var innocent = Innocents(state)[0];

        state = VoteEveryone(state, onto: innocent);

        Assert.True(state.IsFinished);
        Assert.True(state.ImpostorWon);
    }

    [Fact]
    public void A_split_vote_lets_the_impostor_walk()
    {
        var state = Step(FullRoundOfClues(Start()), TestGame.CallVote(_players[0]));

        // Everyone votes for the player on their left: one vote each, nobody agreed on.
        for (var i = 0; i < _players.Count; i++)
        {
            state = Step(state, TestGame.VoteFor(_players[i], _players[(i + 1) % _players.Count]));
        }

        Assert.True(state.IsFinished);
        Assert.True(state.ImpostorWon);
        Assert.Contains("split", state.Outcome!, StringComparison.OrdinalIgnoreCase);
    }

    // --- the last chance -------------------------------------------------------------

    [Fact]
    public void A_caught_impostor_who_names_the_word_steals_the_win()
    {
        var state = Step(FullRoundOfClues(Start()), TestGame.CallVote(_players[0]));
        var impostor = Impostor(state);
        state = VoteEveryone(state, onto: impostor);

        state = Step(state, TestGame.GuessWord(impostor, state.Secret.Word.ToUpperInvariant()));

        Assert.True(state.IsFinished);
        Assert.True(state.ImpostorWon);
    }

    [Fact]
    public void A_caught_impostor_who_guesses_wrong_loses()
    {
        var state = Step(FullRoundOfClues(Start()), TestGame.CallVote(_players[0]));
        var impostor = Impostor(state);
        state = VoteEveryone(state, onto: impostor);

        var result = _mode.Step(state, TestGame.GuessWord(impostor, "definitely not it"));
        var done = (ImpostorState)result.State;

        Assert.True(done.IsFinished);
        Assert.False(done.ImpostorWon);

        var ending = Assert.Single(result.Effects.OfType<Effect.Finished>());
        Assert.Equal(_players.Count, ending.Standings.Count);
        Assert.Equal(1, ending.Standings.Where(s => s.Player != impostor.Id).Min(s => s.Rank));
    }

    [Fact]
    public void Only_the_impostor_may_take_the_last_guess()
    {
        var state = Step(FullRoundOfClues(Start()), TestGame.CallVote(_players[0]));
        state = VoteEveryone(state, onto: Impostor(state));

        var result = _mode.Step(state, TestGame.GuessWord(Innocents(state)[0], "airport"));

        Assert.Same(state, result.State);
    }

    [Fact]
    public void Everyone_learns_who_it_was_once_it_is_over()
    {
        var state = Step(FullRoundOfClues(Start()), TestGame.CallVote(_players[0]));
        var impostor = Impostor(state);
        state = VoteEveryone(state, onto: impostor);
        state = Step(state, TestGame.GuessWord(impostor, "wrong"));

        foreach (var player in _players)
        {
            var view = (ImpostorView)_mode.Project(state, player.Id);

            Assert.Equal(impostor.DisplayName, view.ImpostorName);
            Assert.Equal(state.Secret.Word, view.SecretWord);
        }
    }

    // ---------------------------------------------------------------------------------

    private ImpostorState VoteEveryone(ImpostorState state, Player onto)
    {
        foreach (var voter in _players.Where(p => p.Id != onto.Id))
        {
            state = Step(state, TestGame.VoteFor(voter, onto));
        }

        // The accused still has to vote for somebody, and it cannot be themselves.
        var elsewhere = _players.First(p => p.Id != onto.Id);
        return Step(state, TestGame.VoteFor(onto, elsewhere));
    }
}
