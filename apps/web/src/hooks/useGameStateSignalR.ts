/**
 * useGameStateSignalR Hook
 * Issue #2406: Game State Editor UI
 *
 * SignalR connection for real-time game state updates.
 */

'use client';

import { useEffect, useRef, useState } from 'react';

import * as signalR from '@microsoft/signalr';

import { useGameStateStore } from '@/lib/stores/game-state-store';
import type { GameState, StateConflict, StateUpdateMessage } from '@/types/game-state';

interface UseGameStateSignalROptions {
  sessionId: string;
  enabled?: boolean;
  onStateChanged?: (state: GameState) => void;
  onConflictDetected?: (conflict: StateConflict) => void;
}

export function useGameStateSignalR({
  sessionId,
  enabled = true,
  onStateChanged,
  onConflictDetected,
}: UseGameStateSignalROptions) {
  const [isConnected, setIsConnected] = useState(false);
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const { setState, detectConflict, setConnectionStatus } = useGameStateStore();

  useEffect(() => {
    if (!enabled || !sessionId) return;

    const setupConnection = async () => {
      try {
        // TODO: Replace with actual SignalR hub URL from environment
        const hubUrl = `${process.env.NEXT_PUBLIC_API_URL}/hubs/gamestate`;

        const connection = new signalR.HubConnectionBuilder()
          .withUrl(hubUrl, {
            accessTokenFactory: async () => {
              // TODO: Get auth token from your auth system
              return 'your-auth-token';
            },
          })
          .withAutomaticReconnect({
            nextRetryDelayInMilliseconds: retryContext => {
              // Exponential backoff: 0s, 2s, 10s, 30s, then 60s
              if (retryContext.previousRetryCount === 0) return 0;
              if (retryContext.previousRetryCount === 1) return 2000;
              if (retryContext.previousRetryCount === 2) return 10000;
              if (retryContext.previousRetryCount === 3) return 30000;
              return 60000;
            },
          })
          .configureLogging(signalR.LogLevel.Information)
          .build();

        // Handle reconnection
        connection.onreconnecting(error => {
          console.warn('SignalR reconnecting...', error);
          setIsConnected(false);
          setConnectionError('Reconnecting...');
          setConnectionStatus(false, 'Reconnecting...');
        });

        connection.onreconnected(_connectionId => {
          setIsConnected(true);
          setConnectionError(null);
          setConnectionStatus(true);

          // Rejoin session group
          connection.invoke('JoinSession', sessionId).catch(err => {
            console.error('Failed to rejoin session:', err);
          });
        });

        connection.onclose(error => {
          console.error('SignalR disconnected:', error);
          setIsConnected(false);
          setConnectionError(error?.message || 'Connection closed');
          setConnectionStatus(false, error?.message);
        });

        // Subscribe to state updates
        connection.on('StateChanged', (message: StateUpdateMessage) => {
          if (message.sessionId !== sessionId) return;

          switch (message.type) {
            case 'state-changed': {
              const newState = message.data as GameState;
              setState(newState, 'Remote update');
              onStateChanged?.(newState);
              break;
            }
            case 'conflict-detected': {
              const conflict = message.data as StateConflict;
              detectConflict(conflict);
              onConflictDetected?.(conflict);
              break;
            }
            case 'snapshot-created': {
              // Handled by store
              break;
            }
          }
        });

        // Start connection
        await connection.start();

        // Join session group
        await connection.invoke('JoinSession', sessionId);

        connectionRef.current = connection;
        setIsConnected(true);
        setConnectionError(null);
        setConnectionStatus(true);
      } catch (err) {
        console.error('SignalR connection failed:', err);
        const errorMsg = err instanceof Error ? err.message : 'Failed to connect';
        setConnectionError(errorMsg);
        setConnectionStatus(false, errorMsg);
      }
    };

    setupConnection();

    // Cleanup
    return () => {
      if (connectionRef.current) {
        connectionRef.current
          .invoke('LeaveSession', sessionId)
          .catch(err => console.error('Failed to leave session:', err));

        connectionRef.current.stop().catch(err => console.error('Failed to stop connection:', err));
        connectionRef.current = null;
      }
    };
  }, [
    sessionId,
    enabled,
    setState,
    detectConflict,
    setConnectionStatus,
    onStateChanged,
    onConflictDetected,
  ]);

  const broadcastChange = async (change: Partial<GameState>) => {
    if (!connectionRef.current || !isConnected) {
      throw new Error('SignalR not connected');
    }

    try {
      await connectionRef.current.invoke('BroadcastStateChange', sessionId, change);
    } catch (err) {
      console.error('Failed to broadcast change:', err);
      throw err;
    }
  };

  return {
    isConnected,
    connectionError,
    broadcastChange,
  };
}
