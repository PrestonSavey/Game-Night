import { useEffect, useState } from "react";
import type { ImpostorView } from "../../net/contracts";

export interface ImpostorBoardProps {
  view: ImpostorView;
  onAction: (payload: unknown) => void;
}

export function ImpostorBoard({ view, onAction }: ImpostorBoardProps) {
  const [clue, setClue] = useState("");
  const [guess, setGuess] = useState("");

  useEffect(() => {
    setClue("");
  }, [view.round, view.currentPlayerName]);

  return (
    <div className="stack">
      <div className="row row--between">
        <p className="label">Round {view.round}</p>
        <p className="label">{view.category}</p>
      </div>

      <SecretCard view={view} />

      {view.phase === "Clues" && (
        <>
          {view.yourTurn ? (
            <div className="panel stack">
              <p className="label">Your clue — one word</p>
              <input
                className="input"
                value={clue}
                onChange={(e) => setClue(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && clue.trim()) onAction({ type: "clue", clue: clue.trim() });
                }}
                placeholder="One word"
                maxLength={40}
                autoComplete="off"
                autoFocus
              />
              <button
                className="btn btn--primary btn--block"
                disabled={clue.trim().length === 0}
                onClick={() => onAction({ type: "clue", clue: clue.trim() })}
              >
                Give the clue
              </button>
            </div>
          ) : (
            <p className="muted" style={{ textAlign: "center" }}>
              {view.currentPlayerName} is thinking&hellip;
            </p>
          )}

          {view.canCallVote && (
            <button className="btn btn--block" onClick={() => onAction({ type: "callVote" })}>
              Call a vote
            </button>
          )}
        </>
      )}

      {view.phase === "Voting" && (
        <div className="panel stack">
          <p className="label">
            Who is the impostor? &middot; {view.votesCast} of {view.players.length} in
          </p>

          {view.yourVote ? (
            <p className="muted" style={{ margin: 0 }}>
              Your vote is in. Waiting for the rest.
            </p>
          ) : (
            <div className="stack stack--tight">
              {view.players
                .filter((player) => !player.isYou)
                .map((player) => (
                  <button
                    key={player.playerId}
                    className="btn btn--block"
                    onClick={() => onAction({ type: "vote", target: player.playerId })}
                  >
                    {player.displayName}
                  </button>
                ))}
            </div>
          )}
        </div>
      )}

      {view.phase === "LastChance" &&
        (view.youAreTheImpostor ? (
          <div className="panel stack">
            <p className="label">They got you. Name the word and you still win.</p>
            <input
              className="input"
              value={guess}
              onChange={(e) => setGuess(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && guess.trim()) onAction({ type: "guess", word: guess.trim() });
              }}
              placeholder="The secret word"
              maxLength={40}
              autoComplete="off"
              autoFocus
            />
            <button
              className="btn btn--primary btn--block"
              disabled={guess.trim().length === 0}
              onClick={() => onAction({ type: "guess", word: guess.trim() })}
            >
              That is my answer
            </button>
          </div>
        ) : (
          <div className="panel stack">
            <p className="title" style={{ textAlign: "center" }}>
              Caught them
            </p>
            <p className="muted" style={{ textAlign: "center", margin: 0 }}>
              They get one guess at the word. Hold your breath.
            </p>
          </div>
        ))}

      {view.phase === "Finished" && (
        <div className="panel stack">
          <p className="title" style={{ textAlign: "center" }}>
            {view.impostorWon ? "The impostor wins" : "The table wins"}
          </p>
          <p className="muted" style={{ textAlign: "center", margin: 0 }}>
            {view.outcome}
          </p>
          <p style={{ textAlign: "center", margin: 0 }}>
            It was <strong>{view.impostorName}</strong>.
          </p>
        </div>
      )}

      {view.clues.length > 0 && (
        <div className="panel panel--flat stack stack--tight">
          <p className="label">Clues so far</p>
          {view.clues.map((c, i) => (
            <div className="score" key={`${c.displayName}-${c.round}-${i}`}>
              <span className="score__rank">{c.round}</span>
              <span>{c.displayName}</span>
              <span className="score__value">{c.word}</span>
            </div>
          ))}
        </div>
      )}

      <div className="panel panel--flat stack stack--tight">
        <p className="label">Table</p>
        <ul className="players">
          {view.players.map((player) => (
            <li
              key={player.playerId}
              className={[
                "player",
                player.isCurrent ? "player--you" : "",
                player.wasTheImpostor ? "player--impostor" : "",
              ]
                .filter(Boolean)
                .join(" ")}
            >
              <span className="player__dot" aria-hidden />
              <span className="player__name">{player.displayName}</span>
              {player.wasTheImpostor && <span className="player__tag">Impostor</span>}
              {!player.wasTheImpostor && player.hasVoted && (
                <span className="player__tag">Voted</span>
              )}
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

function SecretCard({ view }: { view: ImpostorView }) {
  if (view.youAreTheImpostor && view.phase !== "Finished") {
    return (
      <div className="secret secret--impostor">
        <p className="label">You are</p>
        <p className="secret__word">THE IMPOSTOR</p>
        <p className="secret__hint">
          You do not know the word. Listen, and give a clue that sounds like you do.
        </p>
      </div>
    );
  }

  return (
    <div className="secret">
      <p className="label">The word is</p>
      <p className="secret__word">{view.secretWord}</p>
      {view.phase !== "Finished" && (
        <p className="secret__hint">One of you cannot see this.</p>
      )}
    </div>
  );
}

