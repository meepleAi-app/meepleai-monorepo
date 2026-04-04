/**
 * useSessionSignalR Hook
 * Game Night Improvvisata: Multi-device session events
 *
 * SignalR connection for real-time session events including
 * score proposals, agent access changes, and participant management.
 */

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import * as signalR from '@microsoft/signalr';

import { logger } from '@/lib/logger';

// ---------------------------------------------------------------------------
// Event types
// ---------------------------------------------------------------------------

export interface ScoreProposedEvent {
  sessionId: string;
  targetPlayerId: string;
  round: number;
  dimension: string;
  value: number;
  proposerName: string;
}

export interface ScoreConfirmedEvent {
  sessionId: string;
  targetPlayerId: string;
  round: number;
  dimension: string;
  value: number;
}

export interface AgentAccessChangedEvent {
  sessionId: string;
  participantId: string;
  enabled: boolean;
}

export interface ParticipantJoinedEvent {
  sessionId: string;
  participantId: string;
  displayName: string;
  role: string;
}

export interface ParticipantLeftEvent {
  sessionId: string;
  participantId: string;
}

// ---------------------------------------------------------------------------
// Hook options
// ---------------------------------------------------------------------------

export interface UseSessionSignalROptions {
  sessionId: string;
  enabled?: boolean;
  /** Guest session token. When provided the token is sent as a query param instead of using JWT. */
  sessionToken?: string;
  onScoreProposed?: (data: ScoreProposedEvent) => void;
  onScoreConfirmed?: (data: ScoreConfirmedEvent) => void;
  onAgentAccessChanged?: (data: AgentAccessChangedEvent) => void;
  onParticipantJoined?: (data: ParticipantJoinedEvent) => void;
  onParticipantLeft?: (data: ParticipantLeftEvent) => void;
}

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

export function useSessionSignalR({
  sessionId,
  enabled = true,
  sessionToken,
  onScoreProposed,
  onScoreConfirmed,
  onAgentAccessChanged,
  onParticipantJoined,
  onParticipantLeft,
}: UseSessionSignalROptions) {
  const [isConnected, setIsConnected] = useState(false);
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  // Keep latest callbacks in refs so the effect doesn't re-run on every render
  const callbacksRef = useRef({
    onScoreProposed,
    onScoreConfirmed,
    onAgentAccessChanged,
    onParticipantJoined,
    onParticipantLeft,
  });
  callbacksRef.current = {
    onScoreProposed,
    onScoreConfirmed,
    onAgentAccessChanged,
    onParticipantJoined,
    onParticipantLeft,
  };

  useEffect(() => {
    if (!enabled || !sessionId) return;

    const setupConnection = async () => {
      try {
        const apiBase = process.env.NEXT_PUBLIC_API_URL ?? '';

        // Guest users authenticate via query-string token; JWT users via accessTokenFactory.
        const hubUrl = sessionToken
          ? `${apiBase}/hubs/gamestate?sessionToken=${encodeURIComponent(sessionToken)}`
          : `${apiBase}/hubs/gamestate`;

        const httpOptions: signalR.IHttpConnectionOptions = sessionToken
          ? {} // token already in URL — no accessTokenFactory needed
          : {
              accessTokenFactory: async () => {
                // TODO: Retrieve JWT from auth system
                return '';
              },
            };

        const connection = new signalR.HubConnectionBuilder()
          .withUrl(hubUrl, httpOptions)
          .withAutomaticReconnect({
            nextRetryDelayInMilliseconds: retryContext => {
              // Exponential backoff: 0s, 2s, 10s, 30s, then cap at 60s
              if (retryContext.previousRetryCount === 0) return 0;
              if (retryContext.previousRetryCount === 1) return 2000;
              if (retryContext.previousRetryCount === 2) return 10000;
              if (retryContext.previousRetryCount === 3) return 30000;
              return 60000;
            },
          })
          .configureLogging(signalR.LogLevel.Information)
          .build();

        // ----- Reconnection handlers -----

        connection.onreconnecting(error => {
          logger.warn(`[useSessionSignalR] Reconnecting... ${error?.message ?? ''}`);
          setIsConnected(false);
          setConnectionError('Reconnecting...');
        });

        connection.onreconnected(_connectionId => {
          setIsConnected(true);
          setConnectionError(null);

          // Rejoin session group after reconnect
          connection.invoke('JoinSession', sessionId).catch(err => {
            logger.error('[useSessionSignalR] Failed to rejoin session:', err);
          });
        });

        connection.onclose(error => {
          logger.error('[useSessionSignalR] Disconnected:', error);
          setIsConnected(false);
          setConnectionError(error?.message ?? 'Connection closed');
        });

        // ----- Event subscriptions -----

        connection.on('ScoreProposed', (data: ScoreProposedEvent) => {
          callbacksRef.current.onScoreProposed?.(data);
        });

        connection.on('ScoreConfirmed', (data: ScoreConfirmedEvent) => {
          callbacksRef.current.onScoreConfirmed?.(data);
        });

        connection.on('AgentAccessChanged', (data: AgentAccessChangedEvent) => {
          callbacksRef.current.onAgentAccessChanged?.(data);
        });

        connection.on('ParticipantJoined', (data: ParticipantJoinedEvent) => {
          callbacksRef.current.onParticipantJoined?.(data);
        });

        connection.on('ParticipantLeft', (data: ParticipantLeftEvent) => {
          callbacksRef.current.onParticipantLeft?.(data);
        });

        // ----- Start & join -----

        await connection.start();
        await connection.invoke('JoinSession', sessionId);

        connectionRef.current = connection;
        setIsConnected(true);
        setConnectionError(null);
      } catch (err) {
        logger.error('[useSessionSignalR] Connection failed:', err);
        const errorMsg = err instanceof Error ? err.message : 'Failed to connect';
        setConnectionError(errorMsg);
      }
    };

    setupConnection();

    // ----- Cleanup -----
    return () => {
      if (connectionRef.current) {
        connectionRef.current
          .invoke('LeaveSession', sessionId)
          .catch(err => logger.error('[useSessionSignalR] Failed to leave session:', err));

        connectionRef.current
          .stop()
          .catch(err => logger.error('[useSessionSignalR] Failed to stop connection:', err));

        connectionRef.current = null;
      }
    };
  }, [sessionId, enabled, sessionToken]);

  // Expose a stable send helper so consumers can invoke hub methods if needed
  const invoke = useCallback(
    async (method: string, ...args: unknown[]) => {
      if (!connectionRef.current || !isConnected) {
        throw new Error('SignalR not connected');
      }
      return connectionRef.current.invoke(method, ...args);
    },
    [isConnected]
  );

  return {
    isConnected,
    connectionError,
    invoke,
  };
}
