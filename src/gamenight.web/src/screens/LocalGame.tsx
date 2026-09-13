import { useEffect, useState } from "react";
import { PassDevice } from "./PassDevice";
import { GameBoard } from "../games/GameBoard";
import type { GameView, Seat } from "../net/contracts";

export interface LocalGameProps {
  seats: Seat[];
  views: Record<string, GameView>;
  /** Whether a turn shows something the rest of the table must not see. */
  privateTurns: boolean;
  onActionAs: (playerId: string, payload: unknown) => void;
}

/**
 * Local play is the online game with the turns laid end to end. The server still projects
 * a separate view per player and still hides what it should; this only decides whose hands
 * the device belongs in.
 *
 * It asks the views, never the game. Every view answers `yourTurn` and `openToAll`, so
 * this file has no idea Spectrum, Last Word or Impostor exist - which is the point, and
 * is what the third game finally forced.
 */
export function LocalGame({ seats, views, privateTurns, onActionAs }: LocalGameProps) {
  const sample = seats.map((seat) => views[seat.playerId]).find(Boolean);
  const active = seats.find((seat) => views[seat.playerId]?.yourTurn) ?? null;
  const activeId = active?.playerId ?? null;

  const [unlockedFor, setUnlockedFor] = useState<string | null>(null);

  // Any change of hands re-arms the gate. Without this the device stays unlocked from
  // the previous player's turn and the next person walks straight into their secret.
  useEffect(() => {
    setUnlockedFor(null);
  }, [activeId, sample?.modeId, sample?.phase]);

  if (!sample) {
    return <p className="muted">Dealing&hellip;</p>;
  }

  // Nothing left to hide - the table looks at one screen together.
  if (sample.openToAll) {
    const holder = seats[0];
    return (
      <GameBoard
        view={views[holder.playerId] ?? sample}
        onAction={(payload) => onActionAs(holder.playerId, payload)}
      />
    );
  }

  if (!activeId || !active) {
    return <p className="muted">Waiting&hellip;</p>;
  }

  // A gate on a ten-second turn is friction, not privacy. Only games that actually hide
  // something from the table ask for one.
  if (privateTurns && unlockedFor !== activeId) {
    return (
      <PassDevice
        name={active.displayName}
        view={views[activeId]}
        onReady={() => setUnlockedFor(activeId)}
      />
    );
  }

  return (
    <div className="stack">
      {!privateTurns && (
        <p className="turn-banner">
          {active.displayName}
          <span>&mdash; you&rsquo;re up</span>
        </p>
      )}
      <GameBoard
        view={views[activeId]}
        onAction={(payload) => onActionAs(activeId, payload)}
      />
    </div>
  );
}
