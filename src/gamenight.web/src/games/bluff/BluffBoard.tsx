import { useEffect, useState } from "react";
import type { BluffView } from "../../net/contracts";

export interface BluffBoardProps {
  view: BluffView;
  onAction: (payload: unknown) => void;
}

export function BluffBoard({ view, onAction }: BluffBoardProps) {
  const [lie, setLie] = useState("");

  useEffect(() => {
    setLie("");
  }, [view.questionNumber]);

  const revealed = view.phase === "Reveal" || view.phase === "Finished";

  return (
    <div className="stack">
      <div className="row row--between">
        <p className="label">
          Question {view.questionNumber} of {view.totalQuestions}
        </p>
        <p className="label">
          {view.phase === "Writing"
            ? `${view.submitted} of ${view.playerCount} written`
            : view.phase === "Voting"
              ? `${view.votesCast} of ${view.playerCount} voted`
              : ""}
        </p>
      </div>

      <div className="panel">
        <p className="prompt">{view.prompt}</p>
        {revealed && (
          <p className="prompt__answer">
            The answer was <strong>{view.answer}</strong>
          </p>
        )}
      </div>

      {view.phase === "Writing" &&
        (view.yourLie ? (
          <div className="panel stack stack--tight">
            <p className="label">Your answer is in</p>
            <p className="title">{view.yourLie}</p>
            <p className="tiny muted" style={{ margin: 0 }}>
              Waiting for everyone else.
            </p>
          </div>
        ) : (
          <div className="panel stack">
            <p className="label">Make one up. Make it believable.</p>
            <input
              className="input"
              value={lie}
              onChange={(e) => setLie(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && lie.trim()) onAction({ type: "lie", text: lie.trim() });
              }}
              placeholder="Your invented answer"
              maxLength={60}
              autoComplete="off"
              autoFocus
            />
            <button
              className="btn btn--primary btn--block"
              disabled={lie.trim().length === 0}
              onClick={() => onAction({ type: "lie", text: lie.trim() })}
            >
              Submit it
            </button>
            {/* Usually "you actually got it right", which is worth a point and a smile. */}
            {view.rejection && <p className="banner banner--warn">{view.rejection}</p>}
          </div>
        ))}

      {view.phase === "Voting" && (
        <div className="stack stack--tight">
          <p className="label">
            {view.yourVote ? "Your answer is locked in" : "Which one is true?"}
          </p>
          {view.options.map((option) => (
            <button
              key={option.key}
              className={`option ${option.key === view.yourVote ? "option--picked" : ""}`}
              disabled={option.isYours || view.yourVote !== null}
              onClick={() => onAction({ type: "vote", option: option.key })}
            >
              <span className="option__text">{option.text}</span>
              {/* Only you are told which one is yours. */}
              {option.isYours && <span className="option__tag">Yours</span>}
            </button>
          ))}
        </div>
      )}

      {revealed && (
        <div className="stack stack--tight">
          {view.options.map((option) => (
            <div
              key={option.key}
              className={`option option--static ${option.isTruth ? "option--truth" : ""}`}
            >
              <span className="option__text">{option.text}</span>
              <span className="option__meta">
                {option.isTruth
                  ? "The truth"
                  : option.authorNames.length > 0
                    ? `by ${option.authorNames.join(" & ")}`
                    : ""}
                {option.voterNames.length > 0 && ` · fooled ${option.voterNames.join(", ")}`}
              </span>
            </div>
          ))}

          {view.knewItNames.length > 0 && (
            <p className="tiny muted" style={{ margin: 0 }}>
              {view.knewItNames.join(", ")} already knew it.
            </p>
          )}
        </div>
      )}

      {view.phase === "Reveal" && (
        <button className="btn btn--cold btn--block" onClick={() => onAction({ type: "continue" })}>
          {view.questionNumber >= view.totalQuestions ? "See final scores" : "Next question"}
        </button>
      )}

      <div className="panel panel--flat">
        {view.scores.map((score, index) => (
          <div className="score" key={score.playerId}>
            <span className="score__rank">{index + 1}</span>
            <span>{score.displayName}</span>
            <span className="score__value">{score.score}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
