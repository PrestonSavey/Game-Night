import { useGameConnection } from "./net/useGameConnection";
import { Start } from "./screens/Start";
import { Room } from "./screens/Room";
import { LocalGame } from "./screens/LocalGame";
import { GameBoard } from "./games/GameBoard";

export function App() {
  const game = useGameConnection();
  const seatIds = game.seats.map((seat) => seat.playerId);
  const inGame = game.lobby?.phase === "InGame";

  // Which game is running, as the server described it. The client never hardcodes this.
  const running = game.views[seatIds[0]];
  const mode = game.lobby?.availableModes.find((m) => m.id === running?.modeId);

  let body;

  if (!game.lobby || !game.playMode) {
    body = (
      <Start
        onCreateOnline={(name) => void game.createOnlineRoom(name)}
        onCreateLocal={(names) => void game.createLocalRoom(names)}
        onJoin={(code, name) => void game.joinRoom(code, name)}
        busy={game.status === "connecting"}
      />
    );
  } else if (inGame && game.playMode === "local") {
    body = (
      <LocalGame
        seats={game.seats}
        views={game.views}
        privateTurns={mode?.privateTurns ?? true}
        onActionAs={(playerId, payload) => void game.submitActionAs(playerId, payload)}
      />
    );
  } else if (inGame && running) {
    body = (
      <GameBoard
        view={running}
        onAction={(payload) => void game.submitActionAs(seatIds[0], payload)}
      />
    );
  } else {
    body = (
      <Room
        lobby={game.lobby}
        playMode={game.playMode}
        seatIds={seatIds}
        onPickGame={(modeId, setting) => void game.startGame(modeId, setting)}
        onLeave={() => void game.leave()}
      />
    );
  }

  return (
    <div className="app">
      {game.status === "reconnecting" && (
        <div className="banner banner--warn">Reconnecting&hellip; hold tight.</div>
      )}
      {/* Only meaningful once we had a game to lose. Before that, a failed connection
          is reported as an error with the address that could not be reached. */}
      {game.status === "offline" && game.lobby && (
        <div className="banner banner--bad">
          Lost the connection. Refresh and you will get your seat back.
        </div>
      )}
      {game.error && (
        <button className="banner banner--bad" onClick={game.dismissError}>
          {game.error}
        </button>
      )}

      {body}

      {game.notice && <div className="toast">{game.notice}</div>}
    </div>
  );
}
