import type { GameView } from "../net/contracts";

export interface PassDeviceProps {
  name: string;
  /** Used only to word the warning. The gate itself does not care which game it is. */
  view: GameView | undefined;
  onReady: () => void;
}

/**
 * The price of playing a hidden-information game on one screen. Without this gate the
 * server's careful redaction is pointless, because everyone is looking at the same phone.
 */
export function PassDevice({ name, view, onReady }: PassDeviceProps) {
  return (
    <div className="pass">
      <p className="label">Pass the device</p>
      <p className="pass__name">{name}</p>
      <p className="pass__why">{warningFor(view)}</p>
      <button className="btn btn--primary btn--block" onClick={onReady}>
        I am {name}
      </button>
    </div>
  );
}

function warningFor(view: GameView | undefined): string {
  if (!view) return "Everyone else, look away.";

  if (view.modeId === "spectrum") {
    return view.youAreClueGiver
      ? "Only you may see the target. Everyone else, look away."
      : "Your turn to place the dial. Keep it to yourself.";
  }

  if (view.modeId === "impostor") {
    if (view.phase === "Voting") return "Your vote is yours alone. Nobody peek.";
    if (view.phase === "LastChance") return "One guess. No help from the table.";
    return "Your word is on the next screen. Everyone else, look away.";
  }

  return "Your turn. Everyone else, look away.";
}
