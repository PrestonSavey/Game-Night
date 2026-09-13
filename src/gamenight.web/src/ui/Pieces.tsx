import type { LobbyPlayerView, SpectrumScoreView, StandingView } from "../net/contracts";

export function PlayerList({
  players,
  youId,
}: {
  players: LobbyPlayerView[];
  youId: string | null;
}) {
  return (
    <ul className="players">
      {players.map((player) => (
        <li
          key={player.playerId}
          className={[
            "player",
            player.isConnected ? "" : "player--offline",
            player.playerId === youId ? "player--you" : "",
          ]
            .filter(Boolean)
            .join(" ")}
        >
          <span className="player__dot" aria-hidden />
          <span className="player__name">{player.displayName}</span>
          {player.isHost && <span className="player__tag">Host</span>}
          {!player.isConnected && <span className="player__tag">Away</span>}
        </li>
      ))}
    </ul>
  );
}

export function Scoreboard({ scores }: { scores: SpectrumScoreView[] }) {
  return (
    <div>
      {scores.map((score, index) => (
        <div className="score" key={score.playerId}>
          <span className="score__rank">{index + 1}</span>
          <span>{score.displayName}</span>
          <span className="score__value">{score.score}</span>
        </div>
      ))}
    </div>
  );
}

export function Standings({ standings }: { standings: StandingView[] }) {
  return (
    <div>
      {standings.map((standing) => (
        <div className="score" key={standing.playerId}>
          <span className="score__rank">{standing.rank}</span>
          <span>{standing.displayName}</span>
          <span className="score__value">{standing.label}</span>
        </div>
      ))}
    </div>
  );
}

export function WaitingFor({ names }: { names: string[] }) {
  if (names.length === 0) return null;
  return (
    <p className="tiny muted" style={{ margin: 0 }}>
      Waiting for {names.join(", ")}
    </p>
  );
}
