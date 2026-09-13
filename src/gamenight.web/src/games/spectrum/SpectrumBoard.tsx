import { useEffect, useState } from "react";
import { Dial } from "../../ui/Dial";
import { Scoreboard, WaitingFor } from "../../ui/Pieces";
import type { SpectrumView } from "../../net/contracts";

export interface SpectrumBoardProps {
  view: SpectrumView;
  onAction: (payload: unknown) => void;
}

export function SpectrumBoard({ view, onAction }: SpectrumBoardProps) {
  const [guess, setGuess] = useState(view.yourGuess ?? 50);
  const [clue, setClue] = useState("");

  // A new round arrives as a new view; reset the local dial rather than leaving it
  // parked wherever the last round ended.
  useEffect(() => {
    setGuess(view.yourGuess ?? 50);
    if (view.phase === "AwaitingClue") setClue("");
  }, [view.roundNumber, view.phase, view.yourGuess]);

  const outstanding = view.guesses.filter((g) => !g.hasAnswered).map((g) => g.displayName);
  const revealed = view.phase === "Reveal" || view.phase === "Finished";

  return (
    <div className="stack">
      <div className="row row--between">
        <p className="label">
          Round {view.roundNumber} of {view.totalRounds}
        </p>
        <p className="label">
          {view.youAreClueGiver ? "You give the clue" : `${view.clueGiverName} gives the clue`}
        </p>
      </div>

      <div className="panel stack">
        <Dial
          value={revealed ? (view.yourGuess ?? 50) : guess}
          leftLabel={view.leftLabel}
          rightLabel={view.rightLabel}
          target={view.target}
          markers={
            revealed
              ? view.guesses
                  .filter((g) => g.position !== null)
                  .map((g) => ({ id: g.playerId, label: g.displayName, position: g.position! }))
              : []
          }
          onChange={
            view.phase === "Guessing" && !view.youAreClueGiver ? setGuess : undefined
          }
          disabled={view.phase !== "Guessing" || view.youAreClueGiver}
          showNeedle={!view.youAreClueGiver}
        />

        {view.clue && <p className="clue">&ldquo;{view.clue}&rdquo;</p>}

        {view.phase === "Guessing" && !view.youAreClueGiver && (
          <>
            <p className="dial__readout">{guess}</p>
            <button
              className="btn btn--primary btn--block"
              onClick={() => onAction({ type: "guess", position: guess })}
            >
              {view.yourGuess === null ? "Lock it in" : "Change to " + guess}
            </button>
          </>
        )}
      </div>

      {view.phase === "AwaitingClue" && view.youAreClueGiver && (
        <div className="panel stack">
          <p className="label">Your target is marked. Give one word.</p>
          <input
            className="input"
            value={clue}
            onChange={(e) => setClue(e.target.value)}
            placeholder="One word"
            maxLength={40}
            autoFocus
          />
          <button
            className="btn btn--primary btn--block"
            disabled={clue.trim().length === 0}
            onClick={() => onAction({ type: "clue", clue: clue.trim() })}
          >
            Send the clue
          </button>
        </div>
      )}

      {view.phase === "AwaitingClue" && !view.youAreClueGiver && (
        <p className="muted" style={{ textAlign: "center" }}>
          {view.clueGiverName} is thinking&hellip;
        </p>
      )}

      {view.phase === "Guessing" && <WaitingFor names={outstanding} />}

      {revealed && (
        <div className="panel stack">
          <p className="label">The answer was {view.target}</p>
          <Scoreboard scores={view.scores} />
          {/* Anyone can move the game on. The server agrees - restricting this to the
              clue giver means one dead phone at the reveal stalls the whole table. */}
          <button
            className="btn btn--cold btn--block"
            onClick={() => onAction({ type: "continue" })}
          >
            {view.roundNumber >= view.totalRounds ? "See final scores" : "Next round"}
          </button>
        </div>
      )}

      {!revealed && view.phase !== "AwaitingClue" && (
        <div className="panel panel--flat">
          <Scoreboard scores={view.scores} />
        </div>
      )}
    </div>
  );
}
