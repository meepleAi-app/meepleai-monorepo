/**
 * useSignalRSession Hook
 *
 * Game Night Improvvisata — Task 13
 *
 * Manages a SignalR connection to GameStateHub for real-time session events.
 * Populates the live-session-store on incoming events.
 * Exposes methods to send score proposals and session signals.
 *
 * Hub: /hubs/game-state
 * Requires: @microsoft/signalr
 */

import { useState, useEffect, useRef } from 'react';

import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

import { logger } from '@/lib/logger';
import {
  useLiveSessionStore,
  type RuleDispute,
  type ScoreProposal,
} from '@/lib/stores/live-session-store';

// -------- Hub event payload shapes --------

interface ScoreUpdatedPayload {
  playerName: string;
  score: number;
}

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

// -------- Hook return type --------

export interface UseSignalRSessionReturn {
  /** Current SignalR HubConnection instance (null before first connect) */
  connection: HubConnection | null;
  /** Whether the hub is currently connected */
  isConnected: boolean;
  /** Notify all clients that a player's total score changed */
  sendScore: (playerName: string, score: number) => Promise<void>;
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

    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/game-state')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    // ---- Register incoming event handlers ----

    conn.on('ScoreUpdated', (data: ScoreUpdatedPayload) => {
      store.getState().updateScore(data.playerName, data.score);
    });

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

  const sendScore = async (playerName: string, score: number): Promise<void> => {
    const conn = connectionRef.current;
    if (!conn || conn.state !== HubConnectionState.Connected) return;
    await conn.invoke('NotifyScoreUpdated', sessionId, { playerName, score });
  };

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
    sendScore,
    proposeScore,
    appBackgrounded,
  };
}
