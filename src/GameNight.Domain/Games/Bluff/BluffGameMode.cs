using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.Bluff;

/// <summary>
/// A question with an answer nobody knows. Everyone invents a convincing lie, then the
/// lies and the truth go up together, shuffled, and everyone picks the one they believe.
/// Points for finding the truth, and more for being believed.
///
/// The reason this game is worth building: the board has to show you every option except
/// it must never show you which one is yours to anyone else, and must never show anyone
/// which one is true until the reveal. Both rules live in Project, where they cannot be
/// worked around by a client.
/// </summary>
public sealed class BluffGameMode : IGameMode
{
    public const string ModeId = "bluff";

    /// <summary>Finding the real answer.</summary>
    private const int FindingTheTruth = 3;

    /// <summary>Per person who falls for your lie. Being believed beats being right.</summary>
    private const int PerPersonFooled = 2;

    /// <summary>For typing the real answer while trying to invent one.</summary>
    private const int KnowingIt = 1;

    private const int WritingSeconds = 75;
    private const int VotingSeconds = 45;

    private const int MinimumToContinue = 2;

    public GameModeInfo Info { get; } = new(
        Id: ModeId,
        Name: "Bluff",
        Tagline: "Invent an answer. Spot the real one. Being believed is worth more.",
        MinPlayers: 3,
        MaxPlayers: 12,
        Setting: new GameSetting(
            Label: "Questions",
            Options: new List<GameSettingOption>
            {
                new(3, "3 questions"),
                new(5, "5 questions"),
                new(7, "7 questions"),
                new(10, "10 questions"),
            },
            Default: 5),
        // Your lie, and which option is yours, must not be read over your shoulder.
        PrivateTurns: true);

    public StepResult Start(GameStartContext context)
    {
        if (context.Players.Count < Info.MinPlayers)
        {
            throw new ArgumentException(
                $"Bluff needs at least {Info.MinPlayers} players.", nameof(context));
        }

        var total = Math.Clamp(context.Setting ?? 5, 1, 20);
        var deck = Shuffle(TriviaQuestions.Default, context.Random).Take(total + 2).ToList();

        var state = new BluffState
        {
            Players = context.Players,
            QuestionNumber = 1,
            TotalQuestions = total,
            Question = deck[0],
            Upcoming = deck.Skip(1).ToList(),
            Lies = new Dictionary<PlayerId, string>(),
            KnewIt = Array.Empty<PlayerId>(),
            Options = Array.Empty<BluffAnswer>(),
            Votes = new Dictionary<PlayerId, string>(),
            Scores = context.Players.ToDictionary(p => p.Id, _ => 0),
            Rejections = new Dictionary<PlayerId, string>(),
            Phase = BluffPhase.Writing,
        };

        return StepResult.From(
            state,
            new Effect.ScheduleTimer(WritingTimer(1), TimeSpan.FromSeconds(WritingSeconds)));
    }

    public StepResult Step(GameState state, GameEvent evt)
    {
        var bluff = (BluffState)state;

        if (bluff.IsFinished) return StepResult.Unchanged(bluff);

        return evt switch
        {
            GameEvent.Action action => ApplyAction(bluff, action),
            GameEvent.TimerFired timer => ApplyTimeout(bluff, timer),
            GameEvent.PlayerLeft left => ApplyPlayerLeft(bluff, left),
            _ => StepResult.Unchanged(bluff),
        };
    }

    private static StepResult ApplyAction(BluffState state, GameEvent.Action action)
    {
        var move = BluffAction.TryParse(action.Payload);
        if (move is null) return StepResult.Unchanged(state);
        if (state.Players.All(p => p.Id != action.Player)) return StepResult.Unchanged(state);

        return move switch
        {
            BluffAction.Submit submit => SubmitLie(state, action.Player, submit),
            BluffAction.Vote vote => CastVote(state, action.Player, vote),
            BluffAction.Continue => NextOrFinish(state),
            _ => StepResult.Unchanged(state),
        };
    }

    private static StepResult SubmitLie(
        BluffState state, PlayerId player, BluffAction.Submit move)
    {
        if (state.Phase != BluffPhase.Writing) return StepResult.Unchanged(state);

        var text = move.Text.Trim();

        if (text.Length > 60) return Reject(state, player, "Keep it under sixty characters.");
        if (Normalise(text).Length == 0) return Reject(state, player, "Letters and numbers, please.");

        if (Normalise(text) == Normalise(state.Question.Answer))
        {
            // They actually knew it. Credit it, tell them, and make them invent something -
            // otherwise the board would carry two correct answers and the round is broken.
            var knew = state.KnewIt.Contains(player)
                ? state.KnewIt
                : state.KnewIt.Append(player).ToList();

            return StepResult.From(state with
            {
                KnewIt = knew,
                Rejections = With(state.Rejections, player,
                    "That is the real answer — well done. Now make one up."),
            });
        }

        var next = state with
        {
            Lies = With(state.Lies, player, text),
            Rejections = Without(state.Rejections, player),
        };

        return state.Players.All(p => next.Lies.ContainsKey(p.Id))
            ? BeginVoting(next)
            : StepResult.From(next);
    }

    private static StepResult CastVote(BluffState state, PlayerId player, BluffAction.Vote move)
    {
        if (state.Phase != BluffPhase.Voting) return StepResult.Unchanged(state);

        var option = state.Options.FirstOrDefault(o => o.Key == move.OptionKey);
        if (option is null) return StepResult.Unchanged(state);

        // You cannot vote for your own lie. The client hides it; this is what enforces it.
        if (option.Authors.Contains(player)) return StepResult.Unchanged(state);

        var next = state with { Votes = With(state.Votes, player, option.Key) };

        return state.Players.All(p => next.Votes.ContainsKey(p.Id))
            ? Reveal(next)
            : StepResult.From(next);
    }

    private static StepResult ApplyTimeout(BluffState state, GameEvent.TimerFired timer)
    {
        if (state.Phase == BluffPhase.Writing &&
            timer.TimerId == WritingTimer(state.QuestionNumber))
        {
            // Go with whatever arrived. A player who wrote nothing simply has no lie in play.
            return BeginVoting(state);
        }

        if (state.Phase == BluffPhase.Voting &&
            timer.TimerId == VotingTimer(state.QuestionNumber))
        {
            return Reveal(state);
        }

        return StepResult.Unchanged(state);
    }

    private static StepResult ApplyPlayerLeft(BluffState state, GameEvent.PlayerLeft left)
    {
        if (state.Players.All(p => p.Id != left.Player)) return StepResult.Unchanged(state);

        var players = state.Players.Where(p => p.Id != left.Player).ToList();

        var next = state with
        {
            Players = players,
            Lies = Without(state.Lies, left.Player),
            Votes = Without(state.Votes, left.Player),
            Rejections = Without(state.Rejections, left.Player),
            KnewIt = state.KnewIt.Where(id => id != left.Player).ToList(),
        };

        if (players.Count < MinimumToContinue)
        {
            return Finish(next);
        }

        // They may have been the last person everyone was waiting on.
        if (next.Phase == BluffPhase.Writing && players.All(p => next.Lies.ContainsKey(p.Id)))
        {
            return BeginVoting(next);
        }

        if (next.Phase == BluffPhase.Voting && players.All(p => next.Votes.ContainsKey(p.Id)))
        {
            return Reveal(next);
        }

        return StepResult.From(next);
    }

    // ---------------------------------------------------------------------------------

    private sealed record Candidate(string Text, IReadOnlyList<PlayerId> Authors, bool IsTruth);

    private static StepResult BeginVoting(BluffState state)
    {
        var candidates = new List<Candidate>
        {
            new(state.Question.Answer, Array.Empty<PlayerId>(), true),
        };

        // Two people who invented the same lie share one option, and share the credit for
        // anyone who falls for it.
        candidates.AddRange(state.Lies
            .GroupBy(kv => Normalise(kv.Value))
            .Select(g => new Candidate(
                g.First().Value,
                g.Select(kv => kv.Key).ToList(),
                false)));

        /*
          Step is pure, so there is no random source to shuffle with. A stable scatter of
          the text and the question number gives an order nobody can read anything into,
          and one that replays identically - the truth is not always first, and it does not
          drift between two players looking at the same board.
        */
        var ordered = candidates
            .OrderBy(c => Scatter(c.Text, state.QuestionNumber))
            .ThenBy(c => c.Text, StringComparer.Ordinal)
            .Select((c, i) => new BluffAnswer($"o{i}", c.Text, c.Authors, c.IsTruth))
            .ToList();

        var next = state with
        {
            Options = ordered,
            Phase = BluffPhase.Voting,
            Rejections = new Dictionary<PlayerId, string>(),
        };

        // Nobody wrote anything, so there is nothing to choose between.
        if (ordered.Count < 2) return Reveal(next);

        return StepResult.From(
            next,
            new Effect.CancelTimer(WritingTimer(state.QuestionNumber)),
            new Effect.ScheduleTimer(
                VotingTimer(state.QuestionNumber), TimeSpan.FromSeconds(VotingSeconds)));
    }

    private static StepResult Reveal(BluffState state)
    {
        var scores = new Dictionary<PlayerId, int>(state.Scores);
        var byKey = state.Options.ToDictionary(o => o.Key, StringComparer.Ordinal);

        foreach (var (voter, key) in state.Votes)
        {
            if (!byKey.TryGetValue(key, out var option)) continue;

            if (option.IsTruth)
            {
                Award(scores, voter, FindingTheTruth);
                continue;
            }

            foreach (var author in option.Authors)
            {
                Award(scores, author, PerPersonFooled);
            }
        }

        foreach (var player in state.KnewIt)
        {
            Award(scores, player, KnowingIt);
        }

        return StepResult.From(
            state with { Phase = BluffPhase.Reveal, Scores = scores },
            new Effect.CancelTimer(VotingTimer(state.QuestionNumber)));
    }

    private static StepResult NextOrFinish(BluffState state)
    {
        if (state.Phase != BluffPhase.Reveal) return StepResult.Unchanged(state);
        if (state.QuestionNumber >= state.TotalQuestions) return Finish(state);

        var next = state with
        {
            QuestionNumber = state.QuestionNumber + 1,
            Question = state.Upcoming.Count > 0 ? state.Upcoming[0] : state.Question,
            Upcoming = state.Upcoming.Skip(1).ToList(),
            Lies = new Dictionary<PlayerId, string>(),
            KnewIt = Array.Empty<PlayerId>(),
            Options = Array.Empty<BluffAnswer>(),
            Votes = new Dictionary<PlayerId, string>(),
            Rejections = new Dictionary<PlayerId, string>(),
            Phase = BluffPhase.Writing,
        };

        return StepResult.From(
            next,
            new Effect.ScheduleTimer(
                WritingTimer(next.QuestionNumber), TimeSpan.FromSeconds(WritingSeconds)));
    }

    private static StepResult Finish(BluffState state)
    {
        var done = state with { Phase = BluffPhase.Finished };
        return StepResult.From(done, new Effect.Finished(Standings(done)));
    }

    private static IReadOnlyList<Standing> Standings(BluffState state)
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
            if (i > 0 && ordered[i].Score != ordered[i - 1].Score) rank = i + 1;

            standings.Add(new Standing(
                ordered[i].Player.Id,
                rank,
                $"{ordered[i].Score} pt{(ordered[i].Score == 1 ? "" : "s")}"));
        }

        return standings;
    }

    // ---------------------------------------------------------------------------------

    public PlayerView Project(GameState state, PlayerId viewer)
    {
        var s = (BluffState)state;
        var revealed = s.Phase is BluffPhase.Reveal or BluffPhase.Finished;

        return new BluffView
        {
            Phase = s.Phase.ToString(),

            IsYourTurn = s.Phase switch
            {
                BluffPhase.Writing => !s.Lies.ContainsKey(viewer),
                BluffPhase.Voting => !s.Votes.ContainsKey(viewer),
                _ => false,
            },

            QuestionNumber = s.QuestionNumber,
            TotalQuestions = s.TotalQuestions,
            Prompt = s.Question.Prompt,

            YourLie = s.Lies.TryGetValue(viewer, out var mine) ? mine : null,
            Submitted = s.Lies.Count,
            PlayerCount = s.Players.Count,

            Options = s.Options
                .Select(o => new BluffOptionView(
                    Key: o.Key,
                    Text: o.Text,

                    // Told to you, and to nobody else. This is what lets the client stop
                    // you voting for your own lie without telling the table which it is.
                    IsYours: o.Authors.Contains(viewer),

                    // The answer stays hidden from everyone, including whoever wrote it.
                    IsTruth: revealed ? o.IsTruth : null,
                    AuthorNames: revealed
                        ? o.Authors.Select(a => NameOf(s, a)).ToList()
                        : Array.Empty<string>(),
                    VoterNames: revealed
                        ? s.Votes.Where(kv => kv.Value == o.Key)
                            .Select(kv => NameOf(s, kv.Key)).ToList()
                        : Array.Empty<string>()))
                .ToList(),

            YourVote = s.Votes.TryGetValue(viewer, out var vote) ? vote : null,
            VotesCast = s.Votes.Count,

            Answer = revealed ? s.Question.Answer : null,

            Scores = s.Players
                .Select(p => new BluffScoreView(
                    p.Id.ToString(), p.DisplayName, s.Scores.GetValueOrDefault(p.Id)))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),

            Rejection = s.Rejections.TryGetValue(viewer, out var why) ? why : null,

            KnewItNames = revealed
                ? s.KnewIt.Select(p => NameOf(s, p)).ToList()
                : Array.Empty<string>(),
        };
    }

    // ---------------------------------------------------------------------------------

    private static StepResult Reject(BluffState state, PlayerId player, string why) =>
        StepResult.From(state with { Rejections = With(state.Rejections, player, why) });

    private static void Award(Dictionary<PlayerId, int> scores, PlayerId player, int points)
    {
        // Someone who has left keeps no score, and must not be resurrected by one.
        if (scores.ContainsKey(player)) scores[player] += points;
    }

    private static IReadOnlyDictionary<PlayerId, T> With<T>(
        IReadOnlyDictionary<PlayerId, T> source, PlayerId key, T value)
    {
        var copy = new Dictionary<PlayerId, T>(source) { [key] = value };
        return copy;
    }

    private static IReadOnlyDictionary<PlayerId, T> Without<T>(
        IReadOnlyDictionary<PlayerId, T> source, PlayerId key) =>
        source.Where(kv => kv.Key != key).ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>Case, spacing and punctuation are all noise when comparing answers.</summary>
    private static string Normalise(string text) =>
        new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    /// <summary>A stable, arbitrary-looking sort key. FNV-1a, chosen for being boring.</summary>
    private static uint Scatter(string text, int salt)
    {
        unchecked
        {
            var hash = 2166136261u;

            foreach (var c in Normalise(text))
            {
                hash = (hash ^ c) * 16777619u;
            }

            hash = (hash ^ (uint)salt) * 16777619u;
            return hash;
        }
    }

    private static string NameOf(BluffState state, PlayerId id) =>
        state.Players.FirstOrDefault(p => p.Id == id)?.DisplayName ?? "Player";

    /// <summary>Public so tests can fire a specific phase timer, including a stale one.</summary>
    public static string WritingTimer(int question) => $"bluff.writing.{question}";

    public static string VotingTimer(int question) => $"bluff.voting.{question}";

    private static List<TriviaQuestion> Shuffle(
        IReadOnlyList<TriviaQuestion> source, IRandomSource random)
    {
        var questions = source.ToList();

        for (var i = questions.Count - 1; i > 0; i--)
        {
            var j = random.Next(0, i + 1);
            (questions[i], questions[j]) = (questions[j], questions[i]);
        }

        return questions;
    }
}
