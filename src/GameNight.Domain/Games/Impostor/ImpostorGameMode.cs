using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.Impostor;

/// <summary>
/// Everyone gets a secret word except one player, who is only told they are the impostor.
/// Clues go round the table. Anyone can call a vote once everyone has spoken. If the table
/// pins the impostor they win - unless the impostor can name the word from the clues alone.
///
/// The third game, and the one that put the most pressure on the platform: a hidden role
/// rather than a hidden number, a phase that ends on a tally rather than a clock, and an
/// ending that depends on who guessed what.
/// </summary>
public sealed class ImpostorGameMode : IGameMode
{
    public const string ModeId = "impostor";

    private const int MinimumToContinue = 3;

    public GameModeInfo Info { get; } = new(
        Id: ModeId,
        Name: "Impostor",
        Tagline: "Everyone knows the word. One of you is bluffing.",
        MinPlayers: 3,
        MaxPlayers: 12,
        Setting: new GameSetting(
            Label: "Clues each before a vote",
            Options: new List<GameSettingOption>
            {
                new(1, "1 clue each"),
                new(2, "2 clues each"),
                new(3, "3 clues each"),
            },
            Default: 1),
        // Your word, and whether you have one, is the entire game.
        PrivateTurns: true);

    public StepResult Start(GameStartContext context)
    {
        if (context.Players.Count < Info.MinPlayers)
        {
            throw new ArgumentException(
                $"Impostor needs at least {Info.MinPlayers} players.", nameof(context));
        }

        var secret = SecretWords.Default[context.Random.Next(0, SecretWords.Default.Count)];
        var impostor = context.Players[context.Random.Next(0, context.Players.Count)];

        var state = new ImpostorState
        {
            Players = context.Players,
            Impostor = impostor.Id,
            Secret = secret,
            TurnIndex = 0,
            CompletedRounds = 0,
            RoundsBeforeVote = Math.Clamp(context.Setting ?? 1, 1, 5),
            Clues = Array.Empty<ImpostorClue>(),
            Votes = new Dictionary<PlayerId, PlayerId>(),
            Phase = ImpostorPhase.Clues,
        };

        // Nothing is on a clock. The pressure here is entirely social.
        return StepResult.From(state);
    }

    public StepResult Step(GameState state, GameEvent evt)
    {
        var impostor = (ImpostorState)state;

        if (impostor.IsFinished) return StepResult.Unchanged(impostor);

        return evt switch
        {
            GameEvent.Action action => ApplyAction(impostor, action),
            GameEvent.PlayerLeft left => ApplyPlayerLeft(impostor, left),
            _ => StepResult.Unchanged(impostor),
        };
    }

    private static StepResult ApplyAction(ImpostorState state, GameEvent.Action action)
    {
        var move = ImpostorAction.TryParse(action.Payload);
        if (move is null) return StepResult.Unchanged(state);

        if (state.Players.All(p => p.Id != action.Player)) return StepResult.Unchanged(state);

        return move switch
        {
            ImpostorAction.GiveClue clue => GiveClue(state, action.Player, clue),
            ImpostorAction.CallVote => CallVote(state),
            ImpostorAction.Vote vote => CastVote(state, action.Player, vote),
            ImpostorAction.Guess guess => GuessWord(state, action.Player, guess),
            _ => StepResult.Unchanged(state),
        };
    }

    private static StepResult GiveClue(
        ImpostorState state, PlayerId player, ImpostorAction.GiveClue move)
    {
        if (state.Phase != ImpostorPhase.Clues) return StepResult.Unchanged(state);
        if (player != state.Current.Id) return StepResult.Unchanged(state);

        var nextIndex = (state.TurnIndex + 1) % state.Players.Count;

        var next = state with
        {
            Clues = state.Clues
                .Append(new ImpostorClue(player, state.CompletedRounds + 1, move.Clue))
                .ToList(),
            TurnIndex = nextIndex,
            // Back to the top of the table means everyone has now spoken once more.
            CompletedRounds = nextIndex == 0 ? state.CompletedRounds + 1 : state.CompletedRounds,
        };

        return StepResult.From(next, new Effect.Announce("impostor.clue", move.Clue));
    }

    private static StepResult CallVote(ImpostorState state)
    {
        if (state.Phase != ImpostorPhase.Clues) return StepResult.Unchanged(state);

        // Voting before anyone has spoken is a coin flip, not a deduction.
        if (!state.CanCallVote) return StepResult.Unchanged(state);

        return StepResult.From(
            state with { Phase = ImpostorPhase.Voting, Votes = new Dictionary<PlayerId, PlayerId>() },
            new Effect.Announce("impostor.voteCalled", null));
    }

    private static StepResult CastVote(
        ImpostorState state, PlayerId voter, ImpostorAction.Vote move)
    {
        if (state.Phase != ImpostorPhase.Voting) return StepResult.Unchanged(state);
        if (!Guid.TryParse(move.TargetPlayerId, out var raw)) return StepResult.Unchanged(state);

        var target = new PlayerId(raw);
        if (target == voter) return StepResult.Unchanged(state);
        if (state.Players.All(p => p.Id != target)) return StepResult.Unchanged(state);

        var votes = new Dictionary<PlayerId, PlayerId>(state.Votes) { [voter] = target };
        var next = state with { Votes = votes };

        return votes.Count < state.Players.Count ? StepResult.From(next) : CountVotes(next);
    }

    /// <summary>
    /// Most votes wins, and the impostor survives a tie. A scattered vote is a failure to
    /// agree, and failing to agree is how the impostor gets away with it.
    /// </summary>
    private static StepResult CountVotes(ImpostorState state)
    {
        var tally = state.Votes.Values
            .GroupBy(id => id)
            .Select(g => new { Player = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var top = tally[0];
        var contested = tally.Count(x => x.Count == top.Count) > 1;
        var accused = contested ? (PlayerId?)null : top.Player;

        if (accused != state.Impostor)
        {
            return Finish(
                state with { Accused = accused, ImpostorWon = true },
                impostorWon: true,
                outcome: contested
                    ? "The vote split, and the impostor walked away with it."
                    : $"{NameOf(state, accused!.Value)} was not the impostor.");
        }

        // Caught - but they get one shot at the word from the clues alone.
        return StepResult.From(
            state with { Accused = accused, Phase = ImpostorPhase.LastChance },
            new Effect.Announce("impostor.caught", null));
    }

    private static StepResult GuessWord(
        ImpostorState state, PlayerId player, ImpostorAction.Guess move)
    {
        if (state.Phase != ImpostorPhase.LastChance) return StepResult.Unchanged(state);
        if (player != state.Impostor) return StepResult.Unchanged(state);

        var correct = string.Equals(
            Normalise(move.Word), Normalise(state.Secret.Word), StringComparison.Ordinal);

        return Finish(
            state with { ImpostorGuess = move.Word, ImpostorWon = correct },
            impostorWon: correct,
            outcome: correct
                ? $"Caught - but they named it. The word was {state.Secret.Word}."
                : $"Caught, and they guessed {move.Word}. The word was {state.Secret.Word}.");
    }

    private static StepResult ApplyPlayerLeft(ImpostorState state, GameEvent.PlayerLeft left)
    {
        var index = IndexOf(state.Players, left.Player);
        if (index < 0) return StepResult.Unchanged(state);

        if (left.Player == state.Impostor)
        {
            // Nobody left to catch, and nobody else knows the word is safe.
            return Finish(
                state with { ImpostorWon = false },
                impostorWon: false,
                outcome: "The impostor left the game.");
        }

        var players = state.Players.Where(p => p.Id != left.Player).ToList();

        var next = state with
        {
            Players = players,
            TurnIndex = players.Count == 0
                ? 0
                : (index < state.TurnIndex ? state.TurnIndex - 1 : state.TurnIndex) % players.Count,
            Votes = state.Votes
                .Where(kv => kv.Key != left.Player && kv.Value != left.Player)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
        };

        if (players.Count < MinimumToContinue)
        {
            return Finish(next, impostorWon: true, outcome: "Too few players left to finish.");
        }

        // They may have been the last vote everyone was waiting on.
        return next.Phase == ImpostorPhase.Voting && next.Votes.Count >= next.Players.Count
            ? CountVotes(next)
            : StepResult.From(next);
    }

    private static StepResult Finish(ImpostorState state, bool impostorWon, string outcome)
    {
        var done = state with
        {
            Phase = ImpostorPhase.Finished,
            ImpostorWon = impostorWon,
            Outcome = outcome,
        };

        return StepResult.From(done, new Effect.Finished(Standings(done)));
    }

    private static IReadOnlyList<Standing> Standings(ImpostorState state) =>
        state.Players
            .Select(p =>
            {
                var isImpostor = p.Id == state.Impostor;
                var won = isImpostor == state.ImpostorWon;

                return new Standing(
                    p.Id,
                    won ? 1 : 2,
                    isImpostor
                        ? state.ImpostorWon ? "Impostor — got away" : "Impostor — caught"
                        : won ? "Caught them" : "Fooled");
            })
            .OrderBy(s => s.Rank)
            .ToList();

    // ---------------------------------------------------------------------------------

    public PlayerView Project(GameState state, PlayerId viewer)
    {
        var s = (ImpostorState)state;
        var youAreTheImpostor = s.Impostor == viewer;
        var over = s.Phase == ImpostorPhase.Finished;

        return new ImpostorView
        {
            Phase = s.Phase.ToString(),

            IsYourTurn = s.Phase switch
            {
                ImpostorPhase.Clues => s.Current.Id == viewer,
                ImpostorPhase.Voting => !s.Votes.ContainsKey(viewer),
                ImpostorPhase.LastChance => youAreTheImpostor,
                _ => false,
            },

            Category = s.Secret.Category,

            // The one line the whole game rests on. The impostor is told nothing, and
            // everyone learns it together at the end.
            SecretWord = youAreTheImpostor && !over ? null : s.Secret.Word,

            YouAreTheImpostor = youAreTheImpostor,
            Round = s.CompletedRounds + 1,
            CurrentPlayerName = s.Current.DisplayName,
            CanCallVote = s.Phase == ImpostorPhase.Clues && s.CanCallVote,

            Clues = s.Clues
                .Select(c => new ImpostorClueView(NameOf(s, c.Player), c.Round, c.Word))
                .ToList(),

            Players = s.Players
                .Select(p => new ImpostorPlayerView(
                    p.Id.ToString(),
                    p.DisplayName,
                    p.Id == viewer,
                    s.Phase == ImpostorPhase.Clues && s.Current.Id == p.Id,
                    s.Votes.ContainsKey(p.Id),
                    // Who it was stays hidden until it cannot matter any more.
                    over && p.Id == s.Impostor))
                .ToList(),

            // Your own vote comes back to you. Nobody else's ever does.
            YourVote = s.Votes.TryGetValue(viewer, out var mine) ? mine.ToString() : null,
            VotesCast = s.Votes.Count,

            AccusedName = over && s.Accused is { } accused ? NameOf(s, accused) : null,
            ImpostorName = over ? NameOf(s, s.Impostor) : null,
            ImpostorGuess = over ? s.ImpostorGuess : null,
            ImpostorWon = over && s.ImpostorWon,
            Outcome = over ? s.Outcome : null,
        };
    }

    private static string Normalise(string word) =>
        new string(word.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string NameOf(ImpostorState state, PlayerId id) =>
        state.Players.FirstOrDefault(p => p.Id == id)?.DisplayName ?? "Player";

    private static int IndexOf(IReadOnlyList<Player> players, PlayerId player)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i].Id == player) return i;
        }
        return -1;
    }
}
