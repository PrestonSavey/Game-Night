import { useCallback, useId, useRef } from "react";

const CX = 150;
const CY = 152;
const R = 128;

/** 1 sits at the far left of the arc, 100 at the far right. */
function angleFor(value: number): number {
  const clamped = Math.min(100, Math.max(1, value));
  return Math.PI * (1 - (clamped - 1) / 99);
}

function pointFor(value: number, radius = R) {
  const theta = angleFor(value);
  return { x: CX + radius * Math.cos(theta), y: CY - radius * Math.sin(theta) };
}

export interface DialMarker {
  id: string;
  label: string;
  position: number;
}

export interface DialProps {
  value: number;
  leftLabel: string;
  rightLabel: string;
  onChange?: (value: number) => void;
  /** Shown only once the round is revealed, or to the clue giver. */
  target?: number | null;
  markers?: DialMarker[];
  disabled?: boolean;
  /** The clue giver has no guess of their own, so they should see no needle. */
  showNeedle?: boolean;
}

/**
 * The one component people will remember. Everything else in this app is a list or a
 * button; this is the game.
 */
export function Dial({
  value,
  leftLabel,
  rightLabel,
  onChange,
  target,
  markers = [],
  disabled = false,
  showNeedle = true,
}: DialProps) {
  const svgRef = useRef<SVGSVGElement | null>(null);
  const gradientId = useId();

  const positionFromPointer = useCallback((clientX: number, clientY: number) => {
    const svg = svgRef.current;
    if (!svg) return null;

    const rect = svg.getBoundingClientRect();
    const scale = 300 / rect.width;
    const x = (clientX - rect.left) * scale;
    const y = (clientY - rect.top) * scale;

    const theta = Math.atan2(Math.max(0, CY - y), x - CX);
    const fraction = 1 - theta / Math.PI;
    return Math.round(1 + fraction * 99);
  }, []);

  const handlePointer = useCallback(
    (event: React.PointerEvent<SVGSVGElement>) => {
      if (disabled || !onChange) return;
      if (event.type === "pointermove" && event.buttons === 0) return;

      const next = positionFromPointer(event.clientX, event.clientY);
      if (next !== null) onChange(Math.min(100, Math.max(1, next)));
    },
    [disabled, onChange, positionFromPointer],
  );

  const knob = pointFor(value);
  const needleStart = pointFor(value, 16);
  const needleEnd = pointFor(value, R - 20);

  return (
    <div className="stack stack--tight">
      <svg
        ref={svgRef}
        className="dial"
        viewBox="0 0 300 170"
        role={onChange ? "slider" : "img"}
        aria-label={`${leftLabel} to ${rightLabel}`}
        aria-valuemin={onChange ? 1 : undefined}
        aria-valuemax={onChange ? 100 : undefined}
        aria-valuenow={onChange ? value : undefined}
        tabIndex={onChange && !disabled ? 0 : -1}
        onPointerDown={(e) => {
          if (!disabled && onChange) e.currentTarget.setPointerCapture(e.pointerId);
          handlePointer(e);
        }}
        onPointerMove={handlePointer}
        onKeyDown={(e) => {
          if (disabled || !onChange) return;
          if (e.key === "ArrowLeft") onChange(Math.max(1, value - 1));
          if (e.key === "ArrowRight") onChange(Math.min(100, value + 1));
        }}
      >
        <defs>
          <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="0">
            {/*
              A midpoint stop, for two reasons. Practically, teal interpolated straight
              to orange in sRGB passes through olive and looks like a rendering bug.
              Semantically, the middle of a spectrum is neither end, so a neutral there
              is the honest colour.
            */}
            <stop offset="0%" stopColor="var(--cold)" />
            <stop offset="50%" stopColor="var(--dial-mid)" />
            <stop offset="100%" stopColor="var(--hot)" />
          </linearGradient>
        </defs>

        <path
          d={`M ${CX - R} ${CY} A ${R} ${R} 0 0 1 ${CX + R} ${CY}`}
          fill="none"
          stroke={`url(#${gradientId})`}
          strokeWidth="26"
          strokeLinecap="round"
        />

        {/* Other players' guesses, once there is nothing left to hide. */}
        {markers.map((marker) => {
          const outer = pointFor(marker.position, R + 15);
          const inner = pointFor(marker.position, R - 15);
          return (
            <g key={marker.id}>
              <line
                x1={inner.x}
                y1={inner.y}
                x2={outer.x}
                y2={outer.y}
                stroke="var(--text)"
                strokeWidth="3"
                strokeLinecap="round"
                opacity="0.8"
              />
            </g>
          );
        })}

        {typeof target === "number" && (
          <g>
            <line
              x1={pointFor(target, R - 24).x}
              y1={pointFor(target, R - 24).y}
              x2={pointFor(target, R + 22).x}
              y2={pointFor(target, R + 22).y}
              stroke="var(--signal)"
              strokeWidth="5"
              strokeLinecap="round"
            />
            <circle
              cx={pointFor(target, R + 22).x}
              cy={pointFor(target, R + 22).y}
              r="7"
              fill="var(--signal)"
            />
          </g>
        )}

        {/* The needle you actually move. */}
        {showNeedle && (
          <>
            <line
              x1={needleStart.x}
              y1={needleStart.y}
              x2={needleEnd.x}
              y2={needleEnd.y}
              stroke="var(--text)"
              strokeWidth="6"
              strokeLinecap="round"
            />
            <circle cx={CX} cy={CY} r="12" fill="var(--text)" />
            <circle
              cx={knob.x}
              cy={knob.y}
              r="12"
              fill="var(--text)"
              stroke="var(--ink)"
              strokeWidth="3"
            />
          </>
        )}
      </svg>

      <div className="dial__ends">
        <span className="dial__end--cold">{leftLabel}</span>
        <span className="dial__end--hot">{rightLabel}</span>
      </div>
    </div>
  );
}
