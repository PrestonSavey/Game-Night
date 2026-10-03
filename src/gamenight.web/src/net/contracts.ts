/*
  Mirrors the server's wire contracts. SignalR serializes with a camelCase policy, so
  these names must match the C# property names lower-cased on the first letter.

  Worth generating from the C# rather than hand-maintaining once this grows - a rename
  on the server should break the client build, not fail silently at a party.
*/

export type LobbyPhase = "Waiting" | "InGame" | "Results";

export interface LobbyPlayerView {
  playerId: string;
  displayName: string;
  isHost: boolean;
  isConnected: boolean;
}

export interface GameSettingOptionSummary {
  value: number;
  label: string;
}

/** Whatever the game says the host may choose. The lobby renders it without understanding it. */
export interface GameSettingSummary {
  label: string;
  options: GameSettingOptionSummary[];
  default: number;
  defaultToPlayerCount: boolean;
}

export interface GameModeSummary {
  id: string;
  name: string;
  tagline: string;
  minPlayers: number;
  maxPlayers: number;
  setting: GameSettingSummary | null;
  /** Shared-screen play puts a pass-the-device gate between turns when this is true. */
  privateTurns: boolean;
}

export interface StandingView {
  playerId: string;
  displayName: string;
  rank: number;
  label: string;
}

export interface LobbyView {
  code: string;
  phase: LobbyPhase;
  hostPlayerId: string;
  selectedModeId: string | null;
  canStart: boolean;
  cannotStartReason: string | null;
  players: LobbyPlayerView[];
  availableModes: GameModeSummary[];
  lastStandings: StandingView[];
}

export type SpectrumPhase = "AwaitingClue" | "Guessing" | "Reveal" | "Finished";

export interface SpectrumGuessView {
  playerId: string;
  displayName: string;
  /** Null until the reveal. This is the redaction, visible from the client side. */
  position: number | null;
  hasAnswered: boolean;
}

export interface SpectrumScoreView {
  playerId: string;
  displayName: string;
  score: number;
}

/** Every view answers these, so the shared-screen code never asks which game it is. */
export interface ViewBase {
  yourTurn: boolean;
  openToAll: boolean;
}

export interface SpectrumView extends ViewBase {
  modeId: "spectrum";
  phase: SpectrumPhase;
  roundNumber: number;
  totalRounds: number;
  leftLabel: string;
  rightLabel: string;
  clueGiverName: string;
  youAreClueGiver: boolean;
  /** Only ever arrives if you are the clue giver, or the round has been revealed. */
  target: number | null;
  clue: string | null;
  yourGuess: number | null;
  guesses: SpectrumGuessView[];
  scores: SpectrumScoreView[];
}

export interface LastWordPlayerView {
  playerId: string;
  displayName: string;
  isAlive: boolean;
  isCurrent: boolean;
}

export interface LastWordView extends ViewBase {
  modeId: "lastword";
  phase: "Playing" | "Finished";
  startLetter: string;
  endLetter: string;
  currentPlayerName: string;
  youAreOut: boolean;
  turnSeconds: number;
  /** Changes on every turn; the client restarts its countdown when it does. */
  turnToken: number;
  used: string[];
  justAccepted: string | null;
  /** Only ever set for the player whose word bounced. */
  rejection: string | null;
  players: LastWordPlayerView[];
}

export interface ImpostorClueView {
  displayName: string;
  round: number;
  word: string;
}

export interface ImpostorPlayerView {
  playerId: string;
  displayName: string;
  isYou: boolean;
  isCurrent: boolean;
  hasVoted: boolean;
  /** Only ever true once the game is over. */
  wasTheImpostor: boolean;
}

export interface ImpostorView extends ViewBase {
  modeId: "impostor";
  phase: "Clues" | "Voting" | "LastChance" | "Finished";
  category: string;
  /** Null while you are the impostor. Everyone learns it at the end. */
  secretWord: string | null;
  youAreTheImpostor: boolean;
  round: number;
  currentPlayerName: string;
  canCallVote: boolean;
  clues: ImpostorClueView[];
  players: ImpostorPlayerView[];
  yourVote: string | null;
  votesCast: number;
  accusedName: string | null;
  impostorName: string | null;
  impostorGuess: string | null;
  impostorWon: boolean;
  outcome: string | null;
}

export interface BluffOptionView {
  key: string;
  text: string;
  /** You wrote this one. Told to you and to nobody else. */
  isYours: boolean;
  /** Null until the reveal, for everybody. */
  isTruth: boolean | null;
  authorNames: string[];
  voterNames: string[];
}

export interface BluffScoreView {
  playerId: string;
  displayName: string;
  score: number;
}

export interface BluffView extends ViewBase {
  modeId: "bluff";
  phase: "Writing" | "Voting" | "Reveal" | "Finished";
  isYourTurn: boolean;
  questionNumber: number;
  totalQuestions: number;
  prompt: string;
  yourLie: string | null;
  submitted: number;
  playerCount: number;
  options: BluffOptionView[];
  yourVote: string | null;
  votesCast: number;
  /** Null until the reveal. The field the whole game protects. */
  answer: string | null;
  scores: BluffScoreView[];
  rejection: string | null;
  knewItNames: string[];
}

/** Discriminated on modeId, so the client picks a board the same way the server picks a mode. */
export type GameView = SpectrumView | LastWordView | ImpostorView | BluffView;

export interface JoinResponse {
  success: boolean;
  errorCode: string | null;
  errorMessage: string | null;
  lobbyCode: string | null;
  playerId: string | null;
  rejoinToken: string | null;
}

/** One player's place in a room. Local play hands back several of these at once. */
export interface Seat {
  playerId: string;
  displayName: string;
  rejoinToken: string;
}

export interface LocalJoinResponse {
  success: boolean;
  errorCode: string | null;
  errorMessage: string | null;
  lobbyCode: string | null;
  seats: Seat[];
}

/** Everyone round one screen, or everyone on their own. */
export type PlayMode = "local" | "online";
