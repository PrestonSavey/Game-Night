import { GameTiles } from "../ui/GameTiles";
import { PlayerList, Standings } from "../ui/Pieces";
import type { LobbyView, PlayMode } from "../net/contracts";

export interface RoomProps {
  lobby: LobbyView;
  playMode: PlayMode;
  /** Every seat this device holds. One when online, all of them when local. */
  seatIds: string[];
  onPickGame: (modeId: string, setting: number | null) => void;
  onLeave: () => void;
}

export function Room({ lobby, playMode, seatIds, onPickGame, onLeave }: RoomProps) {
  // The host is whoever opened the room. On a local device that is always us.
  const youAreHost = seatIds.includes(lobby.hostPlayerId);
  const connected = lobby.players.filter((p) => p.isConnected).length;

  return (
    <div className="stack">
      <div className="row row--between">
        {playMode === "online" ? (
          <div className="stack stack--tight">
            <p className="label">Room code</p>
            <span className="code-chip">{lobby.code}</span>
          </div>
        ) : (
          <div className="stack stack--tight">
            <p className="label">Same screen</p>
            <span className="title">{lobby.players.length} playing</span>
          </div>
        )}
        <button className="btn btn--ghost btn--small" onClick={onLeave}>
          Leave
        </button>
      </div>

      {lobby.phase === "Results" && lobby.lastStandings.length > 0 && (
        <div className="panel stack">
          <p className="label">Final standings</p>
          <Standings standings={lobby.lastStandings} />
        </div>
      )}

      <div className="panel stack">
        <p className="label">
          {playMode === "online" ? `In the room · ${lobby.players.length}` : "Players"}
        </p>
        <PlayerList players={lobby.players} youId={seatIds[0] ?? null} />
        {playMode === "online" && lobby.players.length < 3 && (
          <p className="tiny muted" style={{ margin: 0 }}>
            Share the code above so people can join.
          </p>
        )}
      </div>

      <div className="stack stack--tight">
        <p className="label">
          {lobby.phase === "Results" ? "Play something else" : "Pick a game"}
        </p>
        <GameTiles
          modes={lobby.availableModes}
          playerCount={connected}
          canChoose={youAreHost}
          onPick={onPickGame}
        />
        {!youAreHost && (
          <p className="tiny muted" style={{ margin: 0 }}>
            The host is choosing.
          </p>
        )}
      </div>
    </div>
  );
}
