using GameNight.Domain.Abstractions;

namespace GameNight.Domain.Games.LastWord;

/// <summary>
/// The app names two letters. Players take turns; each has a few seconds to say a real
/// word that starts with the first and ends with the second. Miss it and you are out.
/// Last player standing wins.
///
/// Written second on purpose. Where Spectrum is simultaneous, scored in points and full of
/// hidden information, this is strictly round-robin, decided by elimination and almost
/// entirely open - so anything the platform quietly assumed about games shows up here.
/// </summary>
public sealed class LastWordGameMode : IGameMode
{
    public const string ModeId = "lastword";

    /// <summary>
    /// A pair is only offered if there are this many words to find. Otherwise a random
    /// draw eventually serves up something like X…Z and the game is unplayable through no
    /// fault of the players.
    /// </summary>
    private const int MinimumWordsPerPair = 25;

    private const string TimerPrefix = "lastword.turn.";

    private readonly IWordList _words;
    private readonly IReadOnlyList<LetterPair> _viablePairs;

    public LastWordGameMode(IWordList words)
    {
        _words = words;

        var counts = new Dictionary<LetterPair, int>();
        foreach (var word in words.All)
        {
            if (word.Length < 2) continue;
            var pair = new LetterPair(word[0], word[^1]);
            counts[pair] = counts.GetValueOrDefault(pair) + 1;
        }

        // Ordered so that a given seed always draws the same pairs.
        _viablePairs = counts
            .Where(kv => kv.Value >= MinimumWordsPerPair)
            .Select(kv => kv.Key)
            .OrderBy(p => p.Start)
            .ThenBy(p => p.End)
            .ToList();

        if (_viablePairs.Count == 0)
        {
            throw new ArgumentException("No letter pair has enough words to play.", nameof(words));
        }
    }

    public GameModeInfo Info { get; } = new(
        Id: ModeId,
        Name: "Last Word",
        Tagline: "A word from A to D, before the clock runs out. Miss it and you are out.",
        MinPlayers: 2,
        MaxPlayers: 16,
        Setting: new GameSetting(
            Label: "Seconds per turn",
            Options: new List<GameSettingOption>
            {
                new(5, "5 seconds — brutal"),
                new(10, "10 seconds"),
                new(15, "15 seconds"),
                new(20, "20 seconds — gentle"),
            },
            Default: 10));

    public StepResult Start(GameStartContext context)
    {
        if (context.Players.Count < Info.MinPlayers)
        {
            throw new ArgumentException(
                $"Last Word needs at least {Info.MinPlayers} players.", nameof(context));
        }

        var seconds = Math.Clamp(context.Setting ?? 10, 3, 60);

        // One pair to open with and one for every elimination, plus slack.
        var pairs = DrawPairs(context.Random, context.Players.Count + 2);

        var state = new LastWordState
        {
            Players = context.Players,
            Alive = context.Players.Select(p => p.Id).ToList(),
            TurnIndex = 0,
            Pair = pairs[0],
            Used = Array.Empty<string>(),
            KnockedOut = Array.Empty<PlayerId>(),
            TurnSeconds = seconds,
            TurnToken = 1,
            Phase = LastWordPhase.Playing,
            UpcomingPairs = pairs.Skip(1).ToList(),
        };

        // The first player is already on the clock. This is why Start returns effects.
        return StepResult.From(
            state,
            new Effect.ScheduleTimer(TimerId(state.TurnToken), TimeSpan.FromSeconds(seconds)));
    }

    public StepResult Step(GameState state, GameEvent evt)
    {
        var lastWord = (LastWordState)state;

        if (lastWord.IsFinished) return StepResult.Unchanged(lastWord);

        return evt switch
        {
            GameEvent.Action action => ApplyAction(lastWord, action),
            GameEvent.TimerFired timer => ApplyTimeout(lastWord, timer),
            GameEvent.PlayerLeft left => ApplyPlayerLeft(lastWord, left),
            _ => StepResult.Unchanged(lastWord),
        };
    }

    // ---------------------------------------------------------------------------------

    private StepResult ApplyAction(LastWordState state, GameEvent.Action action)
    {
        if (LastWordAction.TryParse(action.Payload) is not LastWordAction.Submit submit)
        {
            return StepResult.Unchanged(state);
        }

        if (action.Player != state.CurrentPlayer) return StepResult.Unchanged(state);

        var word = submit.Word.Trim().ToLowerInvariant();
        var rejection = Check(state, word);

        if (rejection is not null)
        {
            // The clock keeps running. Only time eliminates anybody, so a wrong guess
            // costs you seconds rather than the game - you can keep trying.
            return StepResult.From(state with
            {
                Rejection = rejection,
                RejectionFor = action.Player,
                JustAccepted = null,
            });
        }

        var next = state with
        {
            Used = state.Used.Append(word).ToList(),
            Rejection = null,
            RejectionFor = null,
            JustAccepted = word,
            TurnIndex = (state.TurnIndex + 1) % state.Alive.Count,
            TurnToken = state.TurnToken + 1,
        };

        return StepResult.From(
            next,
            new Effect.CancelTimer(TimerId(state.TurnToken)),
            new Effect.ScheduleTimer(
                TimerId(next.TurnToken), TimeSpan.FromSeconds(next.TurnSeconds)),
            new Effect.Announce("lastword.accepted", word));
    }

    /// <summary>Null means the word is good. Otherwise, what to tell the player.</summary>
    private string? Check(LastWordState state, string word)
    {
        if (word.Length < 2 || !word.All(char.IsAsciiLetterLower))
        {
            return "Letters only, and at least two of them.";
        }

        if (word[0] != state.Pair.Start)
        {
            return $"Has to start with {char.ToUpperInvariant(state.Pair.Start)}.";
        }

        if (word[^1] != state.Pair.End)
        {
            return $"Has to end with {char.ToUpperInvariant(state.Pair.End)}.";
        }

        if (state.Used.Contains(word, StringComparer.Ordinal))
        {
            return "Somebody already used that one.";
        }

        return _words.Contains(word) ? null : "Not a word I know.";
    }

    private StepResult ApplyTimeout(LastWordState state, GameEvent.TimerFired timer)
    {
        // A timer belonging to a turn that has already ended can always arrive late.
        if (timer.TimerId != TimerId(state.TurnToken)) return StepResult.Unchanged(state);

        return Eliminate(state, state.CurrentPlayer);
    }

    private StepResult ApplyPlayerLeft(LastWordState state, GameEvent.PlayerLeft left) =>
        state.Alive.Contains(left.Player)
            ? Eliminate(state, left.Player)
            : StepResult.Unchanged(state);

    private StepResult Eliminate(LastWordState state, PlayerId player)
    {
        var index = IndexOf(state.Alive, player);
        if (index < 0) return StepResult.Unchanged(state);

        var wasTheirTurn = index == state.TurnIndex;
        var alive = state.Alive.Where(id => id != player).ToList();
        var knockedOut = state.KnockedOut.Append(player).ToList();

        if (alive.Count <= 1)
        {
            var done = state with
            {
                Alive = alive,
                KnockedOut = knockedOut,
                TurnIndex = 0,
                Phase = LastWordPhase.Finished,
                Rejection = null,
                RejectionFor = null,
            };

            return StepResult.From(
                done,
                new Effect.CancelTimer(TimerId(state.TurnToken)),
                new Effect.Finished(Standings(done)));
        }

        // Removing someone earlier in the order shifts the current player's index down.
        var turnIndex = (index < state.TurnIndex ? state.TurnIndex - 1 : state.TurnIndex)
                        % alive.Count;

        if (!wasTheirTurn)
        {
            // Someone dropped out of the queue while another player is mid-turn. Their
            // clock and their letters are none of this player's business - leave them be.
            return StepResult.From(state with
            {
                Alive = alive,
                KnockedOut = knockedOut,
                TurnIndex = turnIndex,
            });
        }

        // Fresh letters for the survivors, so one cruel pair cannot take out three people
        // in a row while everyone watches.
        var next = state with
        {
            Alive = alive,
            KnockedOut = knockedOut,
            TurnIndex = turnIndex,
            Pair = state.UpcomingPairs.Count > 0 ? state.UpcomingPairs[0] : state.Pair,
            UpcomingPairs = state.UpcomingPairs.Skip(1).ToList(),
            Used = Array.Empty<string>(),
            TurnToken = state.TurnToken + 1,
            Rejection = null,
            RejectionFor = null,
            JustAccepted = null,
        };

        return StepResult.From(
            next,
            new Effect.CancelTimer(TimerId(state.TurnToken)),
            new Effect.ScheduleTimer(
                TimerId(next.TurnToken), TimeSpan.FromSeconds(next.TurnSeconds)),
            new Effect.Announce("lastword.out", NameOf(state, player)));
    }

    // ---------------------------------------------------------------------------------

    public PlayerView Project(GameState state, PlayerId viewer)
    {
        var s = (LastWordState)state;
        var alive = s.Alive.ToHashSet();
        var current = s.Alive.Count > 0 && s.TurnIndex < s.Alive.Count
            ? s.CurrentPlayer
            : (PlayerId?)null;

        return new LastWordView
        {
            Phase = s.Phase.ToString(),
            StartLetter = char.ToUpperInvariant(s.Pair.Start),
            EndLetter = char.ToUpperInvariant(s.Pair.End),
            CurrentPlayerName = current is null ? string.Empty : NameOf(s, current.Value),
            IsYourTurn = s.Phase == LastWordPhase.Playing && current == viewer,
            YouAreOut = !alive.Contains(viewer),
            TurnSeconds = s.TurnSeconds,
            TurnToken = s.TurnToken,
            Used = s.Used,
            JustAccepted = s.JustAccepted,

            // Nothing much is secret in this game, but a rejection is still between the
            // server and the person who typed it - the table does not need to see you
            // trying "aardvard" three times.
            Rejection = s.RejectionFor == viewer ? s.Rejection : null,

            Players = s.Players
                .Select(p => new LastWordPlayerView(
                    p.Id.ToString(),
                    p.DisplayName,
                    alive.Contains(p.Id),
                    current == p.Id))
                .ToList(),
        };
    }

    private static IReadOnlyList<Standing> Standings(LastWordState state)
    {
        var standings = new List<Standing>();
        var rank = 1;

        foreach (var winner in state.Alive)
        {
            standings.Add(new Standing(winner, rank++, "Winner"));
        }

        // Last one knocked out came closest, so walk the eliminations backwards.
        foreach (var player in state.KnockedOut.Reverse())
        {
            standings.Add(new Standing(player, rank, Ordinal(rank)));
            rank++;
        }

        return standings;
    }

    private IReadOnlyList<LetterPair> DrawPairs(IRandomSource random, int count)
    {
        var drawn = new List<LetterPair>(count);
        var remaining = _viablePairs.ToList();

        for (var i = 0; i < count; i++)
        {
            // Draw without replacement while we can, so the same letters do not come back
            // twice in one short game.
            if (remaining.Count == 0) remaining = _viablePairs.ToList();

            var index = random.Next(0, remaining.Count);
            drawn.Add(remaining[index]);
            remaining.RemoveAt(index);
        }

        return drawn;
    }

    private static string Ordinal(int rank) => rank switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ when rank % 100 is >= 11 and <= 13 => $"{rank}th",
        _ => (rank % 10) switch
        {
            1 => $"{rank}st",
            2 => $"{rank}nd",
            3 => $"{rank}rd",
            _ => $"{rank}th",
        },
    };

    private static string NameOf(LastWordState state, PlayerId id) =>
        state.Players.FirstOrDefault(p => p.Id == id)?.DisplayName ?? "Player";

    private static int IndexOf(IReadOnlyList<PlayerId> players, PlayerId player)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (players[i] == player) return i;
        }
        return -1;
    }

    /// <summary>Public so tests can fire a specific turn's timer, including a stale one.</summary>
    public static string TimerId(int token) => TimerPrefix + token;
}
