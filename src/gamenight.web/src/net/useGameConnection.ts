import { useCallback, useEffect, useRef, useState } from "react";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import type {
  GameView,
  JoinResponse,
  LobbyView,
  LocalJoinResponse,
  PlayMode,
  Seat,
} from "./contracts";

// Empty means same origin, which is the normal case: the dev server proxies /hub to the
// API, and in production the client is served alongside it. Set VITE_API_URL only when
// the two really are on different hosts.
const API_URL = import.meta.env.VITE_API_URL ?? "";
const SEAT_KEY = "gamenight.seat";

const UNREACHABLE =
  `Cannot reach the game server${API_URL ? ` at ${API_URL}` : ""}. Is the API running?`;

/**
 * The seats, not the connection. A connection dies every time a phone locks; the seats are
 * what get you back into the same game, so they live in storage and the connection does not.
 *
 * Online holds one seat. Local play holds every seat on the one device, which is the only
 * difference between the two modes as far as this file is concerned.
 */
interface StoredRoom {
  code: string;
  mode: PlayMode;
  seats: Seat[];
}

function loadRoom(): StoredRoom | null {
  try {
    const raw = window.localStorage.getItem(SEAT_KEY);
    return raw ? (JSON.parse(raw) as StoredRoom) : null;
  } catch {
    return null;
  }
}

function saveRoom(room: StoredRoom | null) {
  try {
    if (room) window.localStorage.setItem(SEAT_KEY, JSON.stringify(room));
    else window.localStorage.removeItem(SEAT_KEY);
  } catch {
    /* private mode, blocked storage - the app still works, it just cannot resume */
  }
}

export type ConnectionStatus =
  | "idle"
  | "connecting"
  | "connected"
  | "reconnecting"
  | "offline";

export interface GameConnection {
  status: ConnectionStatus;
  playMode: PlayMode | null;
  lobby: LobbyView | null;
  seats: Seat[];
  /** Keyed by player id. Online has one entry; local has one per seat. */
  views: Record<string, GameView>;
  error: string | null;
  notice: string | null;
  createOnlineRoom: (displayName: string) => Promise<void>;
  createLocalRoom: (displayNames: string[]) => Promise<void>;
  joinRoom: (code: string, displayName: string) => Promise<void>;
  startGame: (modeId: string, setting: number | null) => Promise<void>;
  submitActionAs: (playerId: string, payload: unknown) => Promise<void>;
  leave: () => Promise<void>;
  dismissError: () => void;
}

export function useGameConnection(): GameConnection {
  const connectionRef = useRef<HubConnection | null>(null);
  const [status, setStatus] = useState<ConnectionStatus>("idle");
  const [playMode, setPlayMode] = useState<PlayMode | null>(null);
  const [lobby, setLobby] = useState<LobbyView | null>(null);
  const [seats, setSeats] = useState<Seat[]>([]);
  const [views, setViews] = useState<Record<string, GameView>>({});
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const ensureConnection = useCallback(async (): Promise<HubConnection> => {
    if (connectionRef.current) {
      if (connectionRef.current.state === HubConnectionState.Disconnected) {
        await connectionRef.current.start();
      }
      return connectionRef.current;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hub/game`)
      // Backoff rather than a tight retry loop: bad wifi at a party is the normal case.
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("LobbyUpdated", (next: LobbyView) => setLobby(next));

    // The player id arrives with the view, because on a local-play device one connection
    // receives one of these per seat and has to keep them apart.
    connection.on("ViewUpdated", (playerId: string, next: GameView) =>
      setViews((current) => ({ ...current, [playerId]: next })),
    );

    connection.on("Announcement", (key: string) => {
      setNotice(key);
      window.setTimeout(() => setNotice(null), 2600);
    });
    connection.on("Rejected", (_code: string, message: string) => setError(message));

    connection.onreconnecting(() => setStatus("reconnecting"));
    connection.onclose(() => setStatus("offline"));
    connection.onreconnected(() => {
      setStatus("connected");
      void resumeStored(connection);
    });

    connectionRef.current = connection;
    setStatus("connecting");

    try {
      await connection.start();
    } catch {
      // Without this the failure vanished: the promise rejected inside a void call, the
      // status stayed "connecting" so every button stayed disabled, and pressing the
      // button did visibly nothing. A dead server must always say so.
      connectionRef.current = null;
      setStatus("offline");
      throw new Error(UNREACHABLE);
    }

    setStatus("connected");
    return connection;
  }, []);

  /** A reconnect is a new connection id, so every seat has to introduce itself again. */
  const resumeStored = async (connection: HubConnection): Promise<boolean> => {
    const room = loadRoom();
    if (!room) return false;

    let anySucceeded = false;
    for (const seat of room.seats) {
      const response = await connection.invoke<JoinResponse>(
        "Resume",
        room.code,
        seat.playerId,
        seat.rejoinToken,
      );
      if (response.success) anySucceeded = true;
    }

    if (anySucceeded) {
      setPlayMode(room.mode);
      setSeats(room.seats);
    } else {
      saveRoom(null);
    }

    return anySucceeded;
  };

  const createOnlineRoom = useCallback(
    async (displayName: string) => {
      try {
      const connection = await ensureConnection();
      const response = await connection.invoke<JoinResponse>("CreateLobby", displayName);

      if (!response.success) {
        setError(response.errorMessage ?? "Could not open a room.");
        return;
      }

      const seat: Seat = {
        playerId: response.playerId!,
        displayName,
        rejoinToken: response.rejoinToken!,
      };

      setPlayMode("online");
      setSeats([seat]);
      setError(null);
      saveRoom({ code: response.lobbyCode!, mode: "online", seats: [seat] });
      } catch (cause) {
        setError(cause instanceof Error ? cause.message : UNREACHABLE);
      }
    },
    [ensureConnection],
  );

  const createLocalRoom = useCallback(
    async (displayNames: string[]) => {
      try {
      const connection = await ensureConnection();
      const response = await connection.invoke<LocalJoinResponse>(
        "CreateLocalLobby",
        displayNames,
      );

      if (!response.success) {
        setError(response.errorMessage ?? "Could not open a room.");
        return;
      }

      setPlayMode("local");
      setSeats(response.seats);
      setError(null);
      saveRoom({ code: response.lobbyCode!, mode: "local", seats: response.seats });
      } catch (cause) {
        setError(cause instanceof Error ? cause.message : UNREACHABLE);
      }
    },
    [ensureConnection],
  );

  const joinRoom = useCallback(
    async (code: string, displayName: string) => {
      try {
      const connection = await ensureConnection();
      const response = await connection.invoke<JoinResponse>(
        "JoinLobby",
        code.toUpperCase(),
        displayName,
      );

      if (!response.success) {
        setError(response.errorMessage ?? "Could not join that room.");
        return;
      }

      const seat: Seat = {
        playerId: response.playerId!,
        displayName,
        rejoinToken: response.rejoinToken!,
      };

      setPlayMode("online");
      setSeats([seat]);
      setError(null);
      saveRoom({ code: response.lobbyCode!, mode: "online", seats: [seat] });
      } catch (cause) {
        setError(cause instanceof Error ? cause.message : UNREACHABLE);
      }
    },
    [ensureConnection],
  );

  const send = useCallback(async (method: string, ...args: unknown[]) => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) return;
    await connection.send(method, ...args);
  }, []);

  const startGame = useCallback(
    (modeId: string, setting: number | null) => send("StartGame", modeId, setting),
    [send],
  );

  const submitActionAs = useCallback(
    (playerId: string, payload: unknown) => send("SubmitActionAs", playerId, payload),
    [send],
  );

  const leave = useCallback(async () => {
    saveRoom(null);
    setLobby(null);
    setViews({});
    setSeats([]);
    setPlayMode(null);
    await connectionRef.current?.stop();
    connectionRef.current = null;
    setStatus("idle");
  }, []);

  // On a cold page load, try to walk straight back into whatever we were playing.
  useEffect(() => {
    if (!loadRoom()) return;

    let cancelled = false;

    void (async () => {
      try {
        const connection = await ensureConnection();
        if (!cancelled) await resumeStored(connection);
      } catch {
        setStatus("offline");
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [ensureConnection]);

  useEffect(() => () => void connectionRef.current?.stop(), []);

  return {
    status,
    playMode,
    lobby,
    seats,
    views,
    error,
    notice,
    createOnlineRoom,
    createLocalRoom,
    joinRoom,
    startGame,
    submitActionAs,
    leave,
    dismissError: () => setError(null),
  };
}
