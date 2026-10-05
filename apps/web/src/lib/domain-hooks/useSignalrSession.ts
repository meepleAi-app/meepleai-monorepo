/**
 * useSignalRSession Hook
 *
 * Game Night Improvvisata — Task 13
 *
 * Manages a SignalR connection to GameStateHub for real-time session events.
 * Populates the live-session-store on incoming events.
 * Exposes methods to send score proposals and session signals.
 *
 * Hub: /hubs/gamestate (senza trattino — e' il path che il backend mappa, vedi sotto)
 * Requires: @microsoft/signalr
 */

import { useState, useEffect, useRef } from 'react';

import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
import { getHubBase } from '@/lib/api/core/httpClient';
import { logger } from '@/lib/logger';
import {
  useLiveSessionStore,
  type RuleDispute,
  type ScoreProposal,
} from '@/lib/stores/live-session-store';

// -------- Hub event payload shapes --------

interface DisputeResolvedPayload {
  id: string;
  description: string;
  verdict: string;
  ruleReferences: string[];
  raisedByPlayerName: string;
  timestamp: string;
}

interface ProposeScorePayload {
  id: string;
  playerName: string;
  delta: number;
  timestamp: number;
}

interface ScoringConfiguredPayload {
  sessionId: string;
  scoringType: ScoreType;
  /** JSON-stringified `ScoreDataByType[scoringType]` (BE serializes the column verbatim). */
  scoreData: string;
}

// -------- Hook return type --------

export interface UseSignalRSessionReturn {
  /** Current SignalR HubConnection instance (null before first connect) */
  connection: HubConnection | null;
  /** Whether the hub is currently connected */
  isConnected: boolean;
  /** Propose a score delta that the host must confirm */
  proposeScore: (playerName: string, delta: number) => Promise<void>;
  /** Signal that this client went to the background (mobile PWA pause) */
  appBackgrounded: () => Promise<void>;
}

/**
 * useSignalRSession
 *
 * Connects to /hubs/game-state when sessionId is non-null.
 * Tears down the connection on unmount or when sessionId changes.
 *
 * @param sessionId - The live session GUID, or null to skip connection.
 */
export function useSignalRSession(sessionId: string | null): UseSignalRSessionReturn {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  // Keep stable references to store actions so the effect closure stays current
  const store = useLiveSessionStore;
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!sessionId) return;

    // 🔴 Due difetti indipendenti in una riga, ciascuno sufficiente da solo a rendere la
    // sessione live priva di eventi in tempo reale. Qui c'era `.withUrl('/hubs/game-state')`.
    //
    //   1. **Il path non esiste.** Il backend mappa un solo hub, `/hubs/gamestate` SENZA
    //      trattino (`Program.cs`: `app.MapHub<GameStateHub>("/hubs/gamestate")`).
    //   2. **L'URL era relativo**, quindi risolveva sull'origine del frontend, dove non c'e'
    //      ne' proxy ne' rewrite per `/hubs` — a differenza di `/api/v1/*`. Vedi `getHubBase`.
    //
    // Misurato con tre richieste che triangolano le due cause:
    //   :3000/hubs/game-state/negotiate -> 404 (origine sbagliata E path sbagliato)
    //   :8080/hubs/game-state/negotiate -> 404 (origine giusta, path sbagliato)
    //   :8080/hubs/gamestate/negotiate  -> 200 con connectionToken
    //
    // Il sintomo non era un errore visibile: il `.catch` in fondo a questo effetto registra
    // `setConnected(false)` e logga, quindi la sessione live risultava solo «non connessa» e
    // DisputeResolved / SessionPaused / SessionResumed non arrivavano mai.
    //
    // Perche' nessun test lo vedeva: `__tests__/useSignalrSession.test.ts` sostituisce
    // `HubConnectionBuilder` per intero con un mock il cui `withUrl` e' una spia di cui non si
    // asserisce mai l'argomento. Il test esercita la logica del hook e NON PUO' osservare un
    // URL sbagliato. L'asserzione ora c'e'.
    const conn = new HubConnectionBuilder()
      .withUrl(`${getHubBase()}/hubs/gamestate`, { withCredentials: true })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    // ---- Register incoming event handlers ----

    conn.on('DisputeResolved', (data: DisputeResolvedPayload) => {
      const dispute: RuleDispute = {
        id: data.id,
        description: data.description,
        verdict: data.verdict,
        ruleReferences: data.ruleReferences ?? [],
        raisedByPlayerName: data.raisedByPlayerName,
        timestamp: data.timestamp,
      };
      store.getState().addDispute(dispute);
    });

    conn.on('SessionPaused', () => {
      store.getState().setSession({ status: 'Paused' });
    });

    conn.on('SessionResumed', () => {
      store.getState().setSession({ status: 'InProgress' });
    });

    conn.on('SessionCompleted', () => {
      store.getState().setSession({ status: 'Completed' });
    });

    conn.on('ProposeScore', (data: ProposeScorePayload) => {
      const proposal: ScoreProposal = {
        id: data.id,
        playerName: data.playerName,
        delta: data.delta,
        timestamp: data.timestamp,
      };
      store.getState().addProposal(proposal);
    });

    // #2389 Block A — polymorphic scoring config broadcast.
    // BE serializes the raw column (string); we parse defensively so a malformed
    // payload from a future server version doesn't crash the hub.
    conn.on('ScoringConfigured', (data: ScoringConfiguredPayload) => {
      try {
        const parsed = JSON.parse(data.scoreData) as ScoreDataByType[ScoreType];
        store.getState().setScoringConfig({
          scoringType: data.scoringType,
          scoreData: parsed,
        });
      } catch (err) {
        console.warn('[useSignalRSession] failed to parse ScoringConfigured payload', err);
      }
    });

    // ---- Connection lifecycle ----

    conn
      .start()
      .then(() => {
        return conn.invoke('JoinSession', sessionId);
      })
      .then(() => {
        setIsConnected(true);
        store.getState().setConnected(true);
        store.getState().setOffline(false);
      })
      .catch((err: unknown) => {
        logger.error('[useSignalRSession] Connection failed:', err);
        store.getState().setConnected(false);
      });

    conn.onreconnected(() => {
      conn.invoke('JoinSession', sessionId).catch((err: unknown) => {
        logger.error('[useSignalRSession] Re-join failed:', err);
      });
      setIsConnected(true);
      store.getState().setConnected(true);
      store.getState().setOffline(false);
    });

    conn.onreconnecting(() => {
      setIsConnected(false);
      store.getState().setConnected(false);
      store.getState().setOffline(true);
    });

    conn.onclose(() => {
      setIsConnected(false);
      store.getState().setConnected(false);
    });

    connectionRef.current = conn;
    setConnection(conn);

    return () => {
      conn.stop().catch((err: unknown) => {
        logger.error('[useSignalRSession] Stop failed:', err);
      });
      connectionRef.current = null;
      setConnection(null);
      setIsConnected(false);
    };
  }, [sessionId]); // eslint-disable-line react-hooks/exhaustive-deps

  // ---- Outbound methods ----

  const proposeScore = async (playerName: string, delta: number): Promise<void> => {
    const conn = connectionRef.current;
    if (!conn || conn.state !== HubConnectionState.Connected) return;
    await conn.invoke('ProposeScore', sessionId, { playerName, delta });
  };

  const appBackgrounded = async (): Promise<void> => {
    const conn = connectionRef.current;
    if (!conn || conn.state !== HubConnectionState.Connected) return;
    await conn.invoke('AppBackgrounded', sessionId);
  };

  return {
    connection,
    isConnected,
    proposeScore,
    appBackgrounded,
  };
}
