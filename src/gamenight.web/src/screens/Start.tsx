import { useState } from "react";

export interface StartProps {
  onCreateOnline: (name: string) => void;
  onCreateLocal: (names: string[]) => void;
  onJoin: (code: string, name: string) => void;
  busy: boolean;
}

type Step = "choose" | "local" | "online" | "join";

export function Start({ onCreateOnline, onCreateLocal, onJoin, busy }: StartProps) {
  const [step, setStep] = useState<Step>("choose");

  return (
    <div className="stack">
      <div className="stack stack--tight">
        <p className="label">
          {step === "local" ? "One screen, passed around" : "Everyone on their own phone"}
        </p>
        <h1 className="display">Game Night</h1>
      </div>

      {step === "choose" && <Choose onPick={setStep} />}
      {step === "local" && (
        <LocalSetup busy={busy} onBack={() => setStep("choose")} onGo={onCreateLocal} />
      )}
      {step === "online" && (
        <OnlineSetup busy={busy} onBack={() => setStep("choose")} onGo={onCreateOnline} />
      )}
      {step === "join" && (
        <JoinSetup busy={busy} onBack={() => setStep("choose")} onGo={onJoin} />
      )}
    </div>
  );
}

function Choose({ onPick }: { onPick: (step: Step) => void }) {
  return (
    <div className="stack">
      <button className="choice" onClick={() => onPick("local")}>
        <span className="choice__name">Same screen</span>
        <span className="choice__body">
          One device passed around the table. Everyone plays from here.
        </span>
      </button>

      <button className="choice" onClick={() => onPick("online")}>
        <span className="choice__name">Our own phones</span>
        <span className="choice__body">
          Open a room and share the code. Everyone joins from where they are.
        </span>
      </button>

      <button className="btn btn--ghost btn--block" onClick={() => onPick("join")}>
        I have a code
      </button>
    </div>
  );
}

function LocalSetup({
  busy,
  onBack,
  onGo,
}: {
  busy: boolean;
  onBack: () => void;
  onGo: (names: string[]) => void;
}) {
  const [names, setNames] = useState<string[]>(["", "", ""]);

  const filled = names.map((n) => n.trim()).filter((n) => n.length > 0);
  const duplicated =
    new Set(filled.map((n) => n.toLowerCase())).size !== filled.length;
  const ready = filled.length >= 2 && !duplicated;

  const update = (index: number, value: string) =>
    setNames((current) => current.map((n, i) => (i === index ? value : n)));

  return (
    <div className="stack">
      <div className="panel stack">
        <p className="label">Who is playing?</p>

        {names.map((name, index) => (
          <div className="row" key={index}>
            <span className="seat-number num">{index + 1}</span>
            <input
              className="input grow"
              value={name}
              onChange={(e) => update(index, e.target.value)}
              placeholder={index === 0 ? "You" : "Player " + (index + 1)}
              maxLength={16}
              autoComplete="off"
            />
            {names.length > 2 && (
              <button
                className="btn btn--ghost btn--small"
                aria-label={"Remove player " + (index + 1)}
                onClick={() => setNames((c) => c.filter((_, i) => i !== index))}
              >
                &times;
              </button>
            )}
          </div>
        ))}

        <button
          className="btn btn--ghost btn--block btn--small"
          disabled={names.length >= 16}
          onClick={() => setNames((c) => [...c, ""])}
        >
          Add another player
        </button>

        {duplicated && (
          <p className="tiny muted" style={{ margin: 0 }}>
            Two players have the same name.
          </p>
        )}
      </div>

      <button
        className="btn btn--primary btn--block"
        disabled={!ready || busy}
        onClick={() => onGo(filled)}
      >
        Open the room
      </button>

      <button className="btn btn--ghost btn--block" onClick={onBack}>
        Back
      </button>
    </div>
  );
}

function OnlineSetup({
  busy,
  onBack,
  onGo,
}: {
  busy: boolean;
  onBack: () => void;
  onGo: (name: string) => void;
}) {
  const [name, setName] = useState("");

  return (
    <div className="stack">
      <div className="panel stack">
        <label className="label" htmlFor="name">
          Your name
        </label>
        <input
          id="name"
          className="input"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Preston"
          maxLength={16}
          autoComplete="off"
          autoFocus
        />
      </div>

      <button
        className="btn btn--primary btn--block"
        disabled={name.trim().length === 0 || busy}
        onClick={() => onGo(name.trim())}
      >
        Open the room
      </button>

      <button className="btn btn--ghost btn--block" onClick={onBack}>
        Back
      </button>
    </div>
  );
}

function JoinSetup({
  busy,
  onBack,
  onGo,
}: {
  busy: boolean;
  onBack: () => void;
  onGo: (code: string, name: string) => void;
}) {
  const [code, setCode] = useState("");
  const [name, setName] = useState("");

  return (
    <div className="stack">
      <div className="panel stack">
        <label className="label" htmlFor="code">
          Room code
        </label>
        <input
          id="code"
          className="input input--code"
          value={code}
          onChange={(e) => setCode(e.target.value.toUpperCase().slice(0, 4))}
          placeholder="XXXX"
          autoComplete="off"
          autoCapitalize="characters"
          spellCheck={false}
          autoFocus
        />
      </div>

      <div className="panel stack">
        <label className="label" htmlFor="joinName">
          Your name
        </label>
        <input
          id="joinName"
          className="input"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Preston"
          maxLength={16}
          autoComplete="off"
        />
      </div>

      <button
        className="btn btn--cold btn--block"
        disabled={code.length !== 4 || name.trim().length === 0 || busy}
        onClick={() => onGo(code, name.trim())}
      >
        Join the room
      </button>

      <button className="btn btn--ghost btn--block" onClick={onBack}>
        Back
      </button>
    </div>
  );
}
