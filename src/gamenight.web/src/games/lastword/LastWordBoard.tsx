import { useEffect, useState } from "react";
import type { LastWordView } from "../../net/contracts";

export interface LastWordBoardProps {
  view: LastWordView;
  onAction: (payload: unknown) => void;
}

export function LastWordBoard({ view, onAction }: LastWordBoardProps) {
  const [word, setWord] = useState("");
  const [remaining, setRemaining] = useState(view.turnSeconds);

  /*
    The countdown restarts when the turn does. It is the client's own clock, not the
    server's: Step is pure and has no clock to stamp a deadline from. The server still
    decides who actually runs out of time, so the worst this can be is a round-trip out
    of step with the timer that decides the outcome.
  */
  useEffect(() => {
    setWord("");
    setRemaining(view.turnSeconds);

    const started = Date.now();
    const tick = window.setInterval(() => {
      const left = view.turnSeconds - Math.floor((Date.now() - started) / 1000);
      setRemaining(Math.max(0, left));
    }, 200);

    return () => window.clearInterval(tick);
  }, [view.turnToken, view.turnSeconds]);

  const fraction = Math.max(0, Math.min(1, remaining / view.turnSeconds));
  const urgent = remaining <= 3;

  return (
    <div className="stack">
      <div className="row row--between">
        <p className="label">{view.used.length} played this round</p>
        <p className="label">
          {view.yourTurn ? "Your turn" : `${view.currentPlayerName}'s turn`}
        </p>
      </div>

      <div className="panel stack">
        <div className="letters">
          <span className="letters__letter">{view.startLetter}</span>
          <span className="letters__gap">&hellip;</span>
          <span className="letters__letter letters__letter--end">{view.endLetter}</span>
        </div>

        <div className={`clock ${urgent ? "clock--urgent" : ""}`}>
          <div className="clock__bar" style={{ transform: `scaleX(${fraction})` }} />
          <span className="clock__num num">{remaining}</span>
        </div>

        {view.yourTurn ? (
          <>
            <input
              className="input"
              value={word}
              onChange={(e) => setWord(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && word.trim()) onAction({ type: "word", word: word.trim() });
              }}
              placeholder={`${view.startLetter}…${view.endLetter}`}
              autoComplete="off"
              autoCapitalize="none"
              spellCheck={false}
              autoFocus
            />
            <button
              className="btn btn--primary btn--block"
              disabled={word.trim().length === 0}
              onClick={() => onAction({ type: "word", word: word.trim() })}
            >
              Say it
            </button>
            {/* A rejection costs seconds, never the game - so it is advice, not a verdict. */}
            {view.rejection && <p className="banner banner--warn">{view.rejection}</p>}
          </>
        ) : (
          <p className="muted" style={{ textAlign: "center", margin: 0 }}>
            {view.youAreOut
              ? "You are out. Enjoy watching."
              : `Waiting on ${view.currentPlayerName}…`}
          </p>
        )}
      </div>

      {view.used.length > 0 && (
        <div className="panel panel--flat stack stack--tight">
          <p className="label">Already gone</p>
          <div className="chips">
            {view.used.map((used) => (
              <span className="chip" key={used}>
                {used}
              </span>
            ))}
          </div>
        </div>
      )}

      <div className="panel panel--flat stack stack--tight">
        <p className="label">Still in</p>
        <ul className="players">
          {view.players.map((player) => (
            <li
              key={player.playerId}
              className={[
                "player",
                player.isAlive ? "" : "player--out",
                player.isCurrent ? "player--you" : "",
              ]
                .filter(Boolean)
                .join(" ")}
            >
              <span className="player__dot" aria-hidden />
              <span className="player__name">{player.displayName}</span>
              {!player.isAlive && <span className="player__tag">Out</span>}
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
