import { useState } from "react";
import type { GameModeSummary } from "../net/contracts";

/*
  Games that exist as a plan but not yet as an IGameMode on the server. When one ships it
  arrives in `availableModes` on its own, and its entry here gets deleted - the list should
  only ever shrink.
*/
const PLANNED: { id: string; name: string; tagline: string }[] = [];

export interface GameTilesProps {
  modes: GameModeSummary[];
  playerCount: number;
  /** Only the host chooses. Everyone else sees the same tiles, greyed. */
  canChoose: boolean;
  onPick: (modeId: string, setting: number | null) => void;
}

export function GameTiles({ modes, playerCount, canChoose, onPick }: GameTilesProps) {
  return (
    <div className="tiles">
      {modes.map((mode) => (
        <Tile
          key={mode.id}
          mode={mode}
          playerCount={playerCount}
          canChoose={canChoose}
          onPick={onPick}
        />
      ))}

      {PLANNED.filter((planned) => !modes.some((m) => m.id === planned.id)).map((planned) => (
        <div className="tile tile--soon" key={planned.id}>
          <span className="tile__name">{planned.name}</span>
          <span className="tile__body">{planned.tagline}</span>
          <span className="tile__foot">Being built</span>
        </div>
      ))}

      <div className="tile tile--soon tile--empty">
        <span className="tile__name">More soon</span>
        <span className="tile__body">There will be others.</span>
      </div>
    </div>
  );
}

function Tile({
  mode,
  playerCount,
  canChoose,
  onPick,
}: {
  mode: GameModeSummary;
  playerCount: number;
  canChoose: boolean;
  onPick: (modeId: string, setting: number | null) => void;
}) {
  const [setting, setSetting] = useState<number>(() => defaultFor(mode, playerCount));

  const short = playerCount < mode.minPlayers;
  const crowded = playerCount > mode.maxPlayers;
  const blocked = short || crowded;

  return (
    <div className={`tile ${blocked ? "tile--blocked" : ""}`}>
      <span className="tile__name">{mode.name}</span>
      <span className="tile__body">{mode.tagline}</span>

      {/* The lobby has no idea what this number means. The game named it and will be
          handed it back at Start, which is why a new game needs no changes here. */}
      {mode.setting && (
        <label className="tile__setting">
          <span className="tile__settingLabel">{mode.setting.label}</span>
          <select
            className="select"
            value={setting}
            disabled={!canChoose}
            onChange={(e) => setSetting(Number(e.target.value))}
          >
            {mode.setting.options.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
      )}

      <span className="tile__foot">
        {short
          ? `Needs ${mode.minPlayers - playerCount} more`
          : crowded
            ? `Up to ${mode.maxPlayers} players`
            : `${mode.minPlayers}–${mode.maxPlayers} players`}
      </span>

      <button
        className="btn btn--primary btn--small btn--block"
        disabled={!canChoose || blocked}
        onClick={() => onPick(mode.id, mode.setting ? setting : null)}
      >
        Play {mode.name}
      </button>
    </div>
  );
}

/** Spectrum wants one round per player; everything else wants what it said it wanted. */
function defaultFor(mode: GameModeSummary, playerCount: number): number {
  if (!mode.setting) return 0;

  if (mode.setting.defaultToPlayerCount) {
    const values = mode.setting.options.map((o) => o.value);
    const lowest = Math.min(...values);
    const highest = Math.max(...values);
    return Math.min(highest, Math.max(lowest, playerCount));
  }

  return mode.setting.default;
}
