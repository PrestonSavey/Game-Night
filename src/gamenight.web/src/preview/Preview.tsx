import { useState } from "react";
import { Start } from "../screens/Start";
import { Room } from "../screens/Room";
import { PassDevice } from "../screens/PassDevice";
import { GameBoard } from "../games/GameBoard";
import type {
  GameModeSummary,
  GameView,
  ImpostorView,
  LastWordView,
  LobbyView,
  SpectrumView,
} from "../net/contracts";

/*
  A server-free tour of every screen, so the look of the thing can be worked on without a
  running API, a lobby, or four friends. Delete it when it stops earning its place.
*/

const noop = () => {};

const MODES: GameModeSummary[] = [
  {
    id: "spectrum",
    name: "Spectrum",
    tagline: "One clue. One dial. How close can everyone get?",
    minPlayers: 3,
    maxPlayers: 12,
    privateTurns: true,
    setting: {
      label: "Rounds",
      default: 3,
      defaultToPlayerCount: true,
      options: [1, 2, 3, 4, 5, 6, 8, 10, 12].map((n) => ({
        value: n,
        label: n === 1 ? "1 round" : `${n} rounds`,
      })),
    },
  },
  {
    id: "lastword",
    name: "Last Word",
    tagline: "A word from A to D, before the clock runs out. Miss it and you are out.",
    minPlayers: 2,
    maxPlayers: 16,
    privateTurns: false,
    setting: {
      label: "Seconds per turn",
      default: 10,
      defaultToPlayerCount: false,
      options: [
        { value: 5, label: "5 seconds — brutal" },
        { value: 10, label: "10 seconds" },
        { value: 15, label: "15 seconds" },
        { value: 20, label: "20 seconds — gentle" },
      ],
    },
  },
  {
    id: "impostor",
    name: "Impostor",
    tagline: "Everyone knows the word. One of you is bluffing.",
    minPlayers: 3,
    maxPlayers: 12,
    privateTurns: true,
    setting: {
      label: "Clues each before a vote",
      default: 1,
      defaultToPlayerCount: false,
      options: [
        { value: 1, label: "1 clue each" },
        { value: 2, label: "2 clues each" },
        { value: 3, label: "3 clues each" },
      ],
    },
  },
];

const LOBBY: LobbyView = {
  code: "KJ7M",
  phase: "Waiting",
  hostPlayerId: "p1",
  selectedModeId: null,
  canStart: true,
  cannotStartReason: null,
  players: [
    { playerId: "p1", displayName: "Preston", isHost: true, isConnected: true },
    { playerId: "p2", displayName: "Ava", isHost: false, isConnected: true },
    { playerId: "p3", displayName: "Ben", isHost: false, isConnected: true },
    { playerId: "p4", displayName: "Cara", isHost: false, isConnected: false },
  ],
  availableModes: MODES,
  lastStandings: [],
};

const RESULTS: LobbyView = {
  ...LOBBY,
  phase: "Results",
  lastStandings: [
    { playerId: "p2", displayName: "Ava", rank: 1, label: "11 pts" },
    { playerId: "p1", displayName: "Preston", rank: 2, label: "9 pts" },
    { playerId: "p3", displayName: "Ben", rank: 3, label: "7 pts" },
    { playerId: "p4", displayName: "Cara", rank: 4, label: "4 pts" },
  ],
};

const SPECTRUM: SpectrumView = {
  modeId: "spectrum",
  yourTurn: false,
  openToAll: false,
  phase: "AwaitingClue",
  roundNumber: 2,
  totalRounds: 4,
  leftLabel: "Underrated",
  rightLabel: "Overrated",
  clueGiverName: "Ava",
  youAreClueGiver: false,
  target: null,
  clue: null,
  yourGuess: null,
  guesses: [
    { playerId: "p1", displayName: "Preston", position: null, hasAnswered: false },
    { playerId: "p3", displayName: "Ben", position: null, hasAnswered: false },
    { playerId: "p4", displayName: "Cara", position: null, hasAnswered: false },
  ],
  scores: [
    { playerId: "p2", displayName: "Ava", score: 7 },
    { playerId: "p1", displayName: "Preston", score: 5 },
    { playerId: "p3", displayName: "Ben", score: 4 },
    { playerId: "p4", displayName: "Cara", score: 2 },
  ],
};

const LAST_WORD: LastWordView = {
  modeId: "lastword",
  yourTurn: false,
  openToAll: false,
  phase: "Playing",
  isYourTurn: false,
  startLetter: "A",
  endLetter: "D",
  currentPlayerName: "Ben",
  youAreOut: false,
  turnSeconds: 10,
  turnToken: 4,
  used: ["acid", "afraid", "around", "aloud"],
  justAccepted: "aloud",
  rejection: null,
  players: [
    { playerId: "p1", displayName: "Preston", isAlive: true, isCurrent: false },
    { playerId: "p2", displayName: "Ava", isAlive: false, isCurrent: false },
    { playerId: "p3", displayName: "Ben", isAlive: true, isCurrent: true },
    { playerId: "p4", displayName: "Cara", isAlive: true, isCurrent: false },
  ],
};

const IMPOSTOR: ImpostorView = {
  modeId: "impostor",
  yourTurn: false,
  openToAll: false,
  phase: "Clues",
  isYourTurn: false,
  category: "A place",
  secretWord: "Airport",
  youAreTheImpostor: false,
  round: 2,
  currentPlayerName: "Ava",
  canCallVote: true,
  clues: [
    { displayName: "Preston", round: 1, word: "queue" },
    { displayName: "Ava", round: 1, word: "wheels" },
    { displayName: "Ben", round: 1, word: "waiting" },
    { displayName: "Cara", round: 1, word: "duty-free" },
  ],
  players: [
    { playerId: "p1", displayName: "Preston", isYou: true, isCurrent: false, hasVoted: false, wasTheImpostor: false },
    { playerId: "p2", displayName: "Ava", isYou: false, isCurrent: true, hasVoted: false, wasTheImpostor: false },
    { playerId: "p3", displayName: "Ben", isYou: false, isCurrent: false, hasVoted: false, wasTheImpostor: false },
    { playerId: "p4", displayName: "Cara", isYou: false, isCurrent: false, hasVoted: false, wasTheImpostor: false },
  ],
  yourVote: null,
  votesCast: 0,
  accusedName: null,
  impostorName: null,
  impostorGuess: null,
  impostorWon: false,
  outcome: null,
};

const BOARDS: Record<string, GameView> = {
  "Clue · giver": { ...SPECTRUM, youAreClueGiver: true, clueGiverName: "You", target: 88, yourTurn: true },
  "Clue · waiting": SPECTRUM,
  Guessing: {
    ...SPECTRUM,
    phase: "Guessing",
    clue: "wasabi",
    yourTurn: true,
    guesses: [
      { playerId: "p1", displayName: "Preston", position: null, hasAnswered: false },
      { playerId: "p3", displayName: "Ben", position: null, hasAnswered: true },
      { playerId: "p4", displayName: "Cara", position: null, hasAnswered: true },
    ],
  },
  Reveal: {
    ...SPECTRUM,
    phase: "Reveal",
    openToAll: true,
    clue: "wasabi",
    target: 88,
    yourGuess: 74,
    guesses: [
      { playerId: "p1", displayName: "Preston", position: 74, hasAnswered: true },
      { playerId: "p3", displayName: "Ben", position: 91, hasAnswered: true },
      { playerId: "p4", displayName: "Cara", position: 39, hasAnswered: true },
    ],
  },
  "Word · yours": { ...LAST_WORD, yourTurn: true, isYourTurn: true, currentPlayerName: "You" },
  "Word · rejected": {
    ...LAST_WORD,
    yourTurn: true,
    isYourTurn: true,
    currentPlayerName: "You",
    rejection: "Not a word I know.",
  },
  "Word · waiting": LAST_WORD,
  "Impostor · innocent": { ...IMPOSTOR, yourTurn: true, isYourTurn: true },
  "Impostor · you": {
    ...IMPOSTOR,
    yourTurn: true,
    isYourTurn: true,
    youAreTheImpostor: true,
    secretWord: null,
  },
  "Impostor · vote": { ...IMPOSTOR, phase: "Voting", yourTurn: true, isYourTurn: true, votesCast: 2 },
  "Impostor · caught": {
    ...IMPOSTOR,
    phase: "LastChance",
    youAreTheImpostor: true,
    secretWord: null,
    yourTurn: true,
    isYourTurn: true,
  },
  "Impostor · over": {
    ...IMPOSTOR,
    phase: "Finished",
    openToAll: true,
    accusedName: "Ben",
    impostorName: "Ben",
    impostorGuess: "Train station",
    impostorWon: false,
    outcome: "Caught, and they guessed Train station. The word was Airport.",
    players: IMPOSTOR.players.map((p) =>
      p.displayName === "Ben" ? { ...p, wasTheImpostor: true } : p,
    ),
  },
};

const SCREENS = [
  "Start",
  "Room · online",
  "Room · local",
  "Results",
  "Pass",
  ...Object.keys(BOARDS),
] as const;

type ScreenName = (typeof SCREENS)[number];

export function Preview() {
  const [screen, setScreen] = useState<ScreenName>("Start");
  const board = BOARDS[screen];

  return (
    <div className="app">
      <div className="preview-bar">
        {SCREENS.map((name) => (
          <button
            key={name}
            className={`btn btn--small ${name === screen ? "btn--cold" : "btn--ghost"}`}
            onClick={() => setScreen(name)}
          >
            {name}
          </button>
        ))}
      </div>

      {screen === "Start" && (
        <Start onCreateOnline={noop} onCreateLocal={noop} onJoin={noop} busy={false} />
      )}

      {screen === "Room · online" && (
        <Room lobby={LOBBY} playMode="online" seatIds={["p1"]} onPickGame={noop} onLeave={noop} />
      )}

      {screen === "Room · local" && (
        <Room
          lobby={LOBBY}
          playMode="local"
          seatIds={["p1", "p2", "p3", "p4"]}
          onPickGame={noop}
          onLeave={noop}
        />
      )}

      {screen === "Results" && (
        <Room lobby={RESULTS} playMode="online" seatIds={["p1"]} onPickGame={noop} onLeave={noop} />
      )}

      {screen === "Pass" && <PassDevice name="Ava" view={IMPOSTOR} onReady={noop} />}

      {board && <GameBoard key={screen} view={board} onAction={noop} />}
    </div>
  );
}
