# Game Night

A real-time multiplayer platform for party word games. Everyone plays on their own phone;
one person opens a lobby, the rest join with a four-letter code and a name.

The games are the content. The engineering is a **server-authoritative state machine with
pluggable game modes** — which is the part that's actually interesting.

## Design in three sentences

1. **Game logic is a pure reducer.** `Step(state, event) -> (state, effects)`. No clock, no
   sockets, no database, no unseeded randomness. A complete game — timeouts included — runs
   inside a unit test in microseconds.
2. **Time arrives as an event, not a clock read.** A game can't start a timer; it *returns*
   an effect asking to be woken, and the runtime owns the real clock.
3. **Clients never receive game state.** They receive a per-viewer projection. The clue
   giver's projection contains the secret target; nobody else's does.

```
PLATFORM   lobby codes · join/rejoin · roster · reconnection · timers · scoreboard
           ─────────────────────── IGameMode ───────────────────────
GAMES      Spectrum      Last Word      (next one)
```

## Layout

| Path | What's in it |
| --- | --- |
| `src/GameNight.Domain` | Abstractions and game rules. **Zero dependencies**, by design. |
| `tests/GameNight.Domain.Tests` | Where the game rules are actually specified. |
| `src/GameNight.Application` | Sessions, lobby codes, rejoin tokens, timer scheduling. |
| `src/GameNight.Api` | Minimal API + SignalR hub. |
| `src/gamenight.web` | Vite + React + TypeScript client and design system. |
| `infra/` | *(next)* Bicep for Azure Container Apps + Static Web Apps. |

## Running it

Requires the .NET 10 SDK.

```bash
dotnet restore
dotnet build
dotnet test
```

The client needs Node 20+:

```bash
cd src/gamenight.web
npm install
npm run dev
```

On Windows, `run-api.bat` and `run-web.bat` in the repo root start each one and log to
`api-log.txt` / `web-log.txt`.

**Seeing the UI without a server:** http://localhost:5173/?preview walks every screen
with fake data - landing, lobby, results, and all four Spectrum phases from both the
clue giver and a guesser point of view. Useful while `Step` is still unimplemented.

## Status

Solution builds clean on .NET 10. Runtime and transport are in place: lobby codes, join and
rejoin with a signed token, a two-minute disconnect grace period, host transfer, server-owned
timers, and per-player view dispatch.

**Two ways to play.** Online, each person joins from their own device with the room code.
Locally, one device claims every seat and passes round the table. The server does not have a
concept of local play - those are ordinary players who happen to share a connection, which is
why nothing below the transport had to change. The hidden information still lives on the
server; the client just puts a pass-the-device gate in front of each turn.

Spectrum is complete and its 31 domain tests pass: clue giving, guess overwriting,
reveal-on-last-answer, the scoring curve, round rotation, timeouts, and players leaving
mid-round. The whole game including timeouts runs in about a second of test time, because
none of it touches a clock.

Run the API with `dotnet run --project src/GameNight.Api` — it listens on
`http://localhost:5080`, hub at `/hub/game`, health at `/healthz`.

## Games

**Spectrum** — one player secretly sees a target on a 1–100 dial between two labels
("Cold / Hot"), gives a one-word clue, everyone else places the dial. Closer guesses score
more. The clue giver rotates each round.

**Last Word** *(next)* — the app names a start letter and an end letter. Players take turns;
each has ten seconds to say a valid word that fits. Miss it and you're out. Last player
standing wins.

The two are deliberately dissimilar — simultaneous vs round-robin, points vs elimination,
hidden information vs none — so that anything the shared platform assumes gets caught early.
