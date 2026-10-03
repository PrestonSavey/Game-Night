import { SpectrumBoard } from "./spectrum/SpectrumBoard";
import { LastWordBoard } from "./lastword/LastWordBoard";
import { ImpostorBoard } from "./impostor/ImpostorBoard";
import { BluffBoard } from "./bluff/BluffBoard";
import type { GameView } from "../net/contracts";

/**
 * One place that knows which board belongs to which game. Adding a game means adding a
 * case here and a line in the server's DI registration - nothing else in the app changes.
 */
export function GameBoard({
  view,
  onAction,
}: {
  view: GameView;
  onAction: (payload: unknown) => void;
}) {
  switch (view.modeId) {
    case "spectrum":
      return <SpectrumBoard view={view} onAction={onAction} />;
    case "lastword":
      return <LastWordBoard view={view} onAction={onAction} />;
    case "impostor":
      return <ImpostorBoard view={view} onAction={onAction} />;
    case "bluff":
      return <BluffBoard view={view} onAction={onAction} />;
    default:
      return <p className="muted">This client does not know that game yet.</p>;
  }
}
