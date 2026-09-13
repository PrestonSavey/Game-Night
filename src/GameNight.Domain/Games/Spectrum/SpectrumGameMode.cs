using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.Spectrum;

/// <summary>
/// One player secretly sees a target on a 1-100 dial, gives a one-word clue, and everyone
/// else places the dial where they think that clue sits. Closer guesses score more.
///
/// Everything here is pure. No clock, no I/O, no unseeded randomness - which is why the
/// whole game, timeouts included, can be played out inside a unit test in microseconds.
/// </summary>
public sealed class SpectrumGameMode : IGameMode
{
    public const string ModeId = "spectrum";

    /// <summary>How long guessers have once a clue is on the board.</summary>
    public static readonly TimeSpan GuessTimeout = TimeSpan.FromSeconds(60);

    public const string RoundTimerId = "spectrum.round";

    /// <summary>
    /// Below this the game cannot continue - one player has nobody to give a clue to.
    /// </summary>
    private const int MinimumToContinue = 2;

    public GameModeInfo Info { get; } = new(
        Id: ModeId,
        Name: "Spectrum",
        Tagline: "One clue. One dial. How close can everyone get?",
        MinPlayers: 3,
        MaxPlayers: 12,
        Setting: new GameSetting(
            Label: "Rounds",
            Options: Enumerable.Range(1, 12)
                .Select(n => new GameSettingOption(n, n == 1 ? "1 round" : $"{n} rounds"))
                .ToList(),
            Default: 3,
            // One clue each is the natural length, but the roster decides what that means.
            DefaultToPlayerCount: true),
        // The clue giver's target must not be read over their shoulder.
        PrivateTurns: true);

    public StepResult Start(GameStartContext context)
    {
        if (context.Players.Count < Info.MinPlayers)
        {
            throw new ArgumentException(
                $"Spectrum needs at least {Info.MinPlayers} players.", nameof(context));
        }

        var deck = Shuffle(SpectrumCards.Default, context.Random);

        // The host's choice, or one clue each if they did not make one.
        var totalRounds = Math.Clamp(context.Setting ?? context.Players.Count, 1, 12);

        // Every target for the whole game, drawn now. Step cannot draw its own.
        var targets = new List<int>(totalRounds);
        for (var round = 0; round < totalRounds; round++)
        {
            targets.Add(context.Random.Next(1, 101));
        }

        var state = new SpectrumState
        {
            Players = context.Players,
            RoundNumber = 1,
            TotalRounds = totalRounds,
            ClueGiverIndex = 0,
            Card = deck[0],
            Target = targets[0],
            Clue = null,
            Guesses = new Dictionary<PlayerId, int>(),
            Scores = context.Players.ToDictionary(p => p.Id, _ => 0),
            Phase = SpectrumPhase.AwaitingClue,
            Deck = deck.Skip(1).ToList(),
            UpcomingTargets = targets.Skip(1).ToList(),
        };

        // Nothing is on the clock yet - the clue phase is deliberately untimed.
        return StepResult.From(state);
    }

    public StepResult Step(GameState state, GameEvent evt)
    {
        var spectrum = (SpectrumState)state;

        if (spectrum.IsFinished)
        {
            return StepResult.Unchanged(spectrum);
        }

        return evt switch
        {
            GameEvent.Action action => ApplyAction(spectrum, action),
            GameEvent.TimerFired timer => ApplyTimeout(spectrum, timer),
            GameEvent.PlayerLeft left => ApplyPlayerLeft(spectrum, left),
            _ => StepResult.Unchanged(spectrum),
        };
    }

    // ---------------------------------------------------------------------------------
    // Moves
    //
    // Every illegal move returns the state unchanged rather than throwing. A player on a
    // laggy phone double-tapping Submit, or acting a moment after the round moved on, is
    // ordinary traffic - not an exception.
    // ---------------------------------------------------------------------------------

    private static StepResult ApplyAction(SpectrumState state, GameEvent.Action action)
    {
        var move = SpectrumAction.TryParse(action.Payload);
        if (move is null)
        {
            return StepResult.Unchanged(state);
        }

        return move switch
        {
            SpectrumAction.GiveClue clue => GiveClue(state, action.Player, clue),
            SpectrumAction.SubmitGuess guess => SubmitGuess(state, action.Player, guess),
            SpectrumAction.Continue => NextOrFinish(state, action.Player),
            _ => StepResult.Unchanged(state),
        };
    }

    private static StepResult GiveClue(
        SpectrumState state, PlayerId player, SpectrumAction.GiveClue move)
    {
        if (state.Phase != SpectrumPhase.AwaitingClue) return StepResult.Unchanged(state);
        if (player != state.ClueGiver.Id) return StepResult.Unchanged(state);

        var next = state with { Clue = move.Clue, Phase = SpectrumPhase.Guessing };

        // The clock only starts once there is something to guess at. Thinking of a clue is
        // deliberately untimed - the pressure there is social, not mechanical.
        return StepResult.From(next, new Effect.ScheduleTimer(RoundTimerId, GuessTimeout));
    }

    private static StepResult SubmitGuess(
        SpectrumState state, PlayerId player, SpectrumAction.SubmitGuess move)
    {
        if (state.Phase != SpectrumPhase.Guessing) return StepResult.Unchanged(state);
        if (player == state.ClueGiver.Id) return StepResult.Unchanged(state);
        if (state.Players.All(p => p.Id != player)) return StepResult.Unchanged(state);

        // Overwrite rather than ignore. People change their mind, and a UI where the first
        // tap silently wins feels broken.
        var guesses = new Dictionary<PlayerId, int>(state.Guesses)
        {
            [player] = Math.Clamp(move.Position, 1, 100),
        };

        var next = state with { Guesses = guesses };

        // Nobody should sit through a timer that no longer means anything.
        return EveryoneAnswered(next) ? Reveal(next) : StepResult.From(next);
    }

    /// <summary>
    /// Any player may move the game on, not only the clue giver. Restricting it to the
    /// giver means one dead phone at the reveal stalls the whole table.
    /// </summary>
    private static StepResult NextOrFinish(SpectrumState state, PlayerId player)
    {
        if (state.Phase != SpectrumPhase.Reveal) return StepResult.Unchanged(state);
        if (state.Players.All(p => p.Id != player)) return StepResult.Unchanged(state);

        return state.RoundNumber >= state.TotalRounds ? Finish(state) : NextRound(state);
    }

    // ---------------------------------------------------------------------------------
    // Time and absence
    // ---------------------------------------------------------------------------------

    private static StepResult ApplyTimeout(SpectrumState state, GameEvent.TimerFired timer)
    {
        // A timer from a round that has already ended can always arrive late. Ignore it.
        if (timer.TimerId != RoundTimerId) return StepResult.Unchanged(state);
        if (state.Phase != SpectrumPhase.Guessing) return StepResult.Unchanged(state);

        // Reveal with whatever arrived. A missing guess simply scores nothing.
        return Reveal(state);
    }

    private static StepResult ApplyPlayerLeft(SpectrumState state, GameEvent.PlayerLeft left)
    {
        var index = IndexOf(state, left.Player);
        if (index < 0) return StepResult.Unchanged(state);

        var players = state.Players.Where(p => p.Id != left.Player).ToList();
        var wasClueGiver = index == state.ClueGiverIndex;

        // Removing someone before the giver shifts the giver's index down. Removing the
        // giver leaves the index pointing at whoever was next, which is exactly right.
        var clueGiverIndex = index < state.ClueGiverIndex
            ? state.ClueGiverIndex - 1
            : state.ClueGiverIndex;

        var next = state with
        {
            Players = players,
            ClueGiverIndex = players.Count == 0 ? 0 : clueGiverIndex % players.Count,
            Guesses = Without(state.Guesses, left.Player),
            Scores = Without(state.Scores, left.Player),
        };

        if (players.Count < MinimumToContinue)
        {
            return Finish(next);
        }

        if (wasClueGiver)
        {
            // Only they knew the target, so this round cannot be finished by anyone.
            return next.RoundNumber >= next.TotalRounds ? Finish(next) : NextRound(next);
        }

        // They may have been the last person everyone was waiting on.
        return next.Phase == SpectrumPhase.Guessing && EveryoneAnswered(next)
            ? Reveal(next)
            : StepResult.From(next);
    }

    // ---------------------------------------------------------------------------------
    // Round transitions
    // ---------------------------------------------------------------------------------

    private static StepResult Reveal(SpectrumState state)
    {
        var scores = new Dictionary<PlayerId, int>(state.Scores);

        foreach (var (player, guess) in state.Guesses)
        {
            if (!scores.ContainsKey(player)) continue;
            scores[player] += ScoreFor(guess, state.Target);
        }

        var next = state with { Phase = SpectrumPhase.Reveal, Scores = scores };

        return StepResult.From(next, new Effect.CancelTimer(RoundTimerId));
    }

    private static StepResult NextRound(SpectrumState state)
    {
        var next = state with
        {
            RoundNumber = state.RoundNumber + 1,
            ClueGiverIndex = (state.ClueGiverIndex + 1) % state.Players.Count,

            // Now that the host picks the round count, the deck really can run dry, so
            // both of these wrap rather than crash. Repeating a card late in a long game
            // is a far smaller problem than an index out of range mid-party.
            Card = state.Deck.Count > 0
                ? state.Deck[0]
                : SpectrumCards.Default[state.RoundNumber % SpectrumCards.Default.Count],
            Deck = state.Deck.Skip(1).ToList(),

            Target = state.UpcomingTargets.Count > 0
                ? state.UpcomingTargets[0]
                : 1 + (state.Target * 37 + 11) % 100,
            UpcomingTargets = state.UpcomingTargets.Skip(1).ToList(),

            Clue = null,
            Guesses = new Dictionary<PlayerId, int>(),
            Phase = SpectrumPhase.AwaitingClue,
        };

        return StepResult.From(next, new Effect.CancelTimer(RoundTimerId));
    }

    private static StepResult Finish(SpectrumState state)
    {
        var next = state with { Phase = SpectrumPhase.Finished };

        return StepResult.From(
            next,
            new Effect.CancelTimer(RoundTimerId),
            new Effect.Finished(Standings(next)));
    }

    // ---------------------------------------------------------------------------------
    // Scoring
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Points for one guess. A step curve rather than a smooth one, because the argument
    /// afterwards should be about the clue, not about arithmetic.
    /// </summary>
    public static int ScoreFor(int guess, int target)
    {
        var distance = Math.Abs(Math.Clamp(guess, 1, 100) - Math.Clamp(target, 1, 100));

        return distance switch
        {
            <= 2 => 4,
            <= 5 => 3,
            <= 10 => 2,
            <= 20 => 1,
            _ => 0,
        };
    }

    /// <summary>
    /// Competition ranking, so a tie shares a place and the next player is pushed down.
    /// The label is a string because the platform must not assume every game has points.
    /// </summary>
    private static IReadOnlyList<Standing> Standings(SpectrumState state)
    {
        var ordered = state.Players
            .Select(p => (Player: p, Score: state.Scores.GetValueOrDefault(p.Id)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Player.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var standings = new List<Standing>(ordered.Count);
        var rank = 1;

        for (var i = 0; i < ordered.Count; i++)
        {
            if (i > 0 && ordered[i].Score != ordered[i - 1].Score)
            {
                rank = i + 1;
            }

            standings.Add(new Standing(
                ordered[i].Player.Id,
                rank,
                $"{ordered[i].Score} pt{(ordered[i].Score == 1 ? "" : "s")}"));
        }

        return standings;
    }

    // ---------------------------------------------------------------------------------
    // Redaction. The most important method in the game.
    // ---------------------------------------------------------------------------------

    public PlayerView Project(GameState state, PlayerId viewer)
    {
        var s = (SpectrumState)state;
        var isClueGiver = s.ClueGiver.Id == viewer;
        var revealed = s.Phase is SpectrumPhase.Reveal or SpectrumPhase.Finished;

        return new SpectrumView
        {
            Phase = s.Phase.ToString(),
            RoundNumber = s.RoundNumber,
            TotalRounds = s.TotalRounds,
            LeftLabel = s.Card.LeftLabel,
            RightLabel = s.Card.RightLabel,
            ClueGiverName = s.ClueGiver.DisplayName,
            YouAreClueGiver = isClueGiver,

            // The whole game hangs on this line: the answer reaches the clue giver,
            // and nobody else, until the round is over.
            Target = isClueGiver || revealed ? s.Target : null,

            Clue = s.Clue,
            YourGuess = s.Guesses.TryGetValue(viewer, out var mine) ? mine : null,

            // You can see THAT someone has locked in, never WHERE, until the reveal.
            Guesses = s.Guessers
                .Select(p => new SpectrumGuessView(
                    PlayerId: p.Id.ToString(),
                    DisplayName: p.DisplayName,
                    Position: revealed && s.Guesses.TryGetValue(p.Id, out var g) ? g : null,
                    HasAnswered: s.Guesses.ContainsKey(p.Id)))
                .ToList(),

            Scores = s.Players
                .Select(p => new SpectrumScoreView(
                    p.Id.ToString(),
                    p.DisplayName,
                    s.Scores.GetValueOrDefault(p.Id)))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    // ---------------------------------------------------------------------------------

    private static bool EveryoneAnswered(SpectrumState state) =>
        state.Guessers.All(p => state.Guesses.ContainsKey(p.Id));

    private static int IndexOf(SpectrumState state, PlayerId player)
    {
        for (var i = 0; i < state.Players.Count; i++)
        {
            if (state.Players[i].Id == player) return i;
        }
        return -1;
    }

    private static IReadOnlyDictionary<PlayerId, int> Without(
        IReadOnlyDictionary<PlayerId, int> source, PlayerId player) =>
        source.Where(kv => kv.Key != player).ToDictionary(kv => kv.Key, kv => kv.Value);

    private static List<SpectrumCard> Shuffle(
        IReadOnlyList<SpectrumCard> source, IRandomSource random)
    {
        var cards = source.ToList();
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = random.Next(0, i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
        return cards;
    }
}
