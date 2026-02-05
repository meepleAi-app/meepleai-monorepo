/**
 * useRandomTools Hook (Issue #3345)
 *
 * Manages random tools state, API calls, and SSE event handling.
 *
 * Features:
 * - Timer start/pause/resume/reset with SSE sync
 * - Coin flip with history
 * - Wheel spinner with custom options
 *
 * @example
 * ```typescript
 * const { timerState, coinFlipHistory, wheelSpinHistory, ... } = useRandomTools({
 *   sessionId: 'abc123',
 *   participantId: 'participant-id',
 *   participantName: 'Player 1',
 * });
 * ```
 */

import { useState, useCallback, useEffect } from 'react';

import type {
  TimerState,
  TimerStatus,
  CoinFlipResult,
  WheelOption,
  WheelSpinResult,
} from '@/components/session/types';

// ============================================================================
// API Response Types
// ============================================================================

interface TimerStartResponse {
  sessionId: string;
  timerId: string;
  durationSeconds: number;
  startedAt: string;
}

interface TimerStatusResponse {
  sessionId: string;
  timerId: string;
  status: TimerStatus;
  remainingSeconds: number;
  updatedAt: string;
}

interface CoinFlipResponse {
  flipId: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  result: 'heads' | 'tails';
  timestamp: string;
}

interface WheelSpinResponse {
  spinId: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  selectedOption: WheelOption;
  timestamp: string;
}

// ============================================================================
// SSE Event Types
// ============================================================================

interface TimerStartedEventData {
  sessionId: string;
  timerId: string;
  durationSeconds: number;
  startedBy: string;
  startedByName: string;
  startedAt: string;
}

interface TimerPausedEventData {
  sessionId: string;
  timerId: string;
  remainingSeconds: number;
  pausedAt: string;
}

interface TimerResumedEventData {
  sessionId: string;
  timerId: string;
  remainingSeconds: number;
  resumedAt: string;
}

interface TimerCompletedEventData {
  sessionId: string;
  timerId: string;
  completedAt: string;
}

interface TimerResetEventData {
  sessionId: string;
  timerId: string;
  resetAt: string;
}

interface CoinFlippedEventData {
  flipId: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  result: 'heads' | 'tails';
  timestamp: string;
}

interface WheelSpunEventData {
  spinId: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  selectedOption: WheelOption;
  timestamp: string;
}

// ============================================================================
// Hook Options
// ============================================================================

export interface UseRandomToolsOptions {
  /** Session ID */
  sessionId: string;

  /** Current participant ID */
  participantId: string;

  /** Current participant name */
  participantName: string;

  /** API base URL */
  apiBaseUrl?: string;

  /** Callback when timer completes */
  onTimerComplete?: () => void;

  /** Callback when coin is flipped */
  onCoinFlipped?: (result: CoinFlipResult) => void;

  /** Callback when wheel is spun */
  onWheelSpun?: (result: WheelSpinResult) => void;
}

// ============================================================================
// Hook Return Value
// ============================================================================

export interface RandomToolsState {
  /** Timer state */
  timerState: TimerState | undefined;

  /** Coin flip history */
  coinFlipHistory: CoinFlipResult[];

  /** Wheel spin history */
  wheelSpinHistory: WheelSpinResult[];

  /** Loading state */
  isLoading: boolean;

  /** Error state */
  error: Error | null;

  /** Start timer */
  startTimer: (durationSeconds: number) => Promise<void>;

  /** Pause timer */
  pauseTimer: () => Promise<void>;

  /** Resume timer */
  resumeTimer: () => Promise<void>;

  /** Reset timer */
  resetTimer: () => Promise<void>;

  /** Flip coin */
  flipCoin: () => Promise<CoinFlipResult>;

  /** Spin wheel */
  spinWheel: (options: WheelOption[]) => Promise<WheelSpinResult>;

  /** Handle SSE events */
  addRandomToolEventFromSSE: (eventType: string, data: unknown) => void;
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function useRandomTools(options: UseRandomToolsOptions): RandomToolsState {
  const {
    sessionId,
    participantId,
    participantName,
    apiBaseUrl,
    onTimerComplete,
    onCoinFlipped,
    onWheelSpun,
  } = options;

  const [timerState, setTimerState] = useState<TimerState | undefined>();
  const [coinFlipHistory, setCoinFlipHistory] = useState<CoinFlipResult[]>([]);
  const [wheelSpinHistory, setWheelSpinHistory] = useState<WheelSpinResult[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';

  // ========================================================================
  // Timer Operations
  // ========================================================================

  const startTimer = useCallback(
    async (durationSeconds: number): Promise<void> => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/timer/start`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              sessionId,
              participantId,
              durationSeconds,
            }),
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Start timer failed: ${response.status} - ${errorBody}`);
        }

        const data: TimerStartResponse = await response.json();

        setTimerState({
          id: data.timerId,
          sessionId: data.sessionId,
          durationSeconds: data.durationSeconds,
          remainingSeconds: data.durationSeconds,
          status: 'running',
          startedBy: participantId,
          startedByName: participantName,
          startedAt: new Date(data.startedAt),
        });
      } catch (err) {
        const errorObj = err instanceof Error ? err : new Error('Unknown error');
        setError(errorObj);
        throw errorObj;
      } finally {
        setIsLoading(false);
      }
    },
    [baseUrl, sessionId, participantId, participantName]
  );

  const pauseTimer = useCallback(async (): Promise<void> => {
    if (!timerState) return;

    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(
        `${baseUrl}/api/v1/game-sessions/${sessionId}/timer/pause`,
        {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            sessionId,
            participantId,
          }),
        }
      );

      if (!response.ok) {
        const errorBody = await response.text();
        throw new Error(`Pause timer failed: ${response.status} - ${errorBody}`);
      }

      const data: TimerStatusResponse = await response.json();

      setTimerState((prev) =>
        prev
          ? {
              ...prev,
              status: 'paused',
              remainingSeconds: data.remainingSeconds,
              pausedAt: new Date(data.updatedAt),
            }
          : undefined
      );
    } catch (err) {
      const errorObj = err instanceof Error ? err : new Error('Unknown error');
      setError(errorObj);
      throw errorObj;
    } finally {
      setIsLoading(false);
    }
  }, [baseUrl, sessionId, participantId, timerState]);

  const resumeTimer = useCallback(async (): Promise<void> => {
    if (!timerState) return;

    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(
        `${baseUrl}/api/v1/game-sessions/${sessionId}/timer/resume`,
        {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            sessionId,
            participantId,
          }),
        }
      );

      if (!response.ok) {
        const errorBody = await response.text();
        throw new Error(`Resume timer failed: ${response.status} - ${errorBody}`);
      }

      const data: TimerStatusResponse = await response.json();

      setTimerState((prev) =>
        prev
          ? {
              ...prev,
              status: 'running',
              remainingSeconds: data.remainingSeconds,
            }
          : undefined
      );
    } catch (err) {
      const errorObj = err instanceof Error ? err : new Error('Unknown error');
      setError(errorObj);
      throw errorObj;
    } finally {
      setIsLoading(false);
    }
  }, [baseUrl, sessionId, participantId, timerState]);

  const resetTimer = useCallback(async (): Promise<void> => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(
        `${baseUrl}/api/v1/game-sessions/${sessionId}/timer/reset`,
        {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            sessionId,
            participantId,
          }),
        }
      );

      if (!response.ok) {
        const errorBody = await response.text();
        throw new Error(`Reset timer failed: ${response.status} - ${errorBody}`);
      }

      setTimerState(undefined);
    } catch (err) {
      const errorObj = err instanceof Error ? err : new Error('Unknown error');
      setError(errorObj);
      throw errorObj;
    } finally {
      setIsLoading(false);
    }
  }, [baseUrl, sessionId, participantId]);

  // ========================================================================
  // Coin Flip Operations
  // ========================================================================

  const flipCoin = useCallback(async (): Promise<CoinFlipResult> => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await fetch(
        `${baseUrl}/api/v1/game-sessions/${sessionId}/coin-flip`,
        {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            sessionId,
            participantId,
            participantName,
          }),
        }
      );

      if (!response.ok) {
        const errorBody = await response.text();
        throw new Error(`Coin flip failed: ${response.status} - ${errorBody}`);
      }

      const data: CoinFlipResponse = await response.json();

      const result: CoinFlipResult = {
        id: data.flipId,
        sessionId: data.sessionId,
        participantId: data.participantId,
        participantName: data.participantName,
        result: data.result,
        timestamp: new Date(data.timestamp),
      };

      setCoinFlipHistory((prev) => [result, ...prev]);
      onCoinFlipped?.(result);

      return result;
    } catch (err) {
      const errorObj = err instanceof Error ? err : new Error('Unknown error');
      setError(errorObj);
      throw errorObj;
    } finally {
      setIsLoading(false);
    }
  }, [baseUrl, sessionId, participantId, participantName, onCoinFlipped]);

  // ========================================================================
  // Wheel Spin Operations
  // ========================================================================

  const spinWheel = useCallback(
    async (wheelOptions: WheelOption[]): Promise<WheelSpinResult> => {
      setIsLoading(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/wheel-spin`,
          {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              sessionId,
              participantId,
              participantName,
              options: wheelOptions,
            }),
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Wheel spin failed: ${response.status} - ${errorBody}`);
        }

        const data: WheelSpinResponse = await response.json();

        const result: WheelSpinResult = {
          id: data.spinId,
          sessionId: data.sessionId,
          participantId: data.participantId,
          participantName: data.participantName,
          selectedOption: data.selectedOption,
          timestamp: new Date(data.timestamp),
        };

        setWheelSpinHistory((prev) => [result, ...prev]);
        onWheelSpun?.(result);

        return result;
      } catch (err) {
        const errorObj = err instanceof Error ? err : new Error('Unknown error');
        setError(errorObj);
        throw errorObj;
      } finally {
        setIsLoading(false);
      }
    },
    [baseUrl, sessionId, participantId, participantName, onWheelSpun]
  );

  // ========================================================================
  // SSE Event Handling
  // ========================================================================

  const addRandomToolEventFromSSE = useCallback(
    (eventType: string, data: unknown) => {
      switch (eventType) {
        case 'TimerStartedEvent': {
          const event = data as TimerStartedEventData;
          setTimerState({
            id: event.timerId,
            sessionId: event.sessionId,
            durationSeconds: event.durationSeconds,
            remainingSeconds: event.durationSeconds,
            status: 'running',
            startedBy: event.startedBy,
            startedByName: event.startedByName,
            startedAt: new Date(event.startedAt),
          });
          break;
        }
        case 'TimerPausedEvent': {
          const event = data as TimerPausedEventData;
          setTimerState((prev) =>
            prev
              ? {
                  ...prev,
                  status: 'paused',
                  remainingSeconds: event.remainingSeconds,
                  pausedAt: new Date(event.pausedAt),
                }
              : undefined
          );
          break;
        }
        case 'TimerResumedEvent': {
          const event = data as TimerResumedEventData;
          setTimerState((prev) =>
            prev
              ? {
                  ...prev,
                  status: 'running',
                  remainingSeconds: event.remainingSeconds,
                }
              : undefined
          );
          break;
        }
        case 'TimerCompletedEvent': {
          setTimerState((prev) =>
            prev
              ? {
                  ...prev,
                  status: 'completed',
                  remainingSeconds: 0,
                }
              : undefined
          );
          onTimerComplete?.();
          break;
        }
        case 'TimerResetEvent': {
          setTimerState(undefined);
          break;
        }
        case 'CoinFlippedEvent': {
          const event = data as CoinFlippedEventData;
          const result: CoinFlipResult = {
            id: event.flipId,
            sessionId: event.sessionId,
            participantId: event.participantId,
            participantName: event.participantName,
            result: event.result,
            timestamp: new Date(event.timestamp),
          };
          setCoinFlipHistory((prev) => [result, ...prev]);
          onCoinFlipped?.(result);
          break;
        }
        case 'WheelSpunEvent': {
          const event = data as WheelSpunEventData;
          const result: WheelSpinResult = {
            id: event.spinId,
            sessionId: event.sessionId,
            participantId: event.participantId,
            participantName: event.participantName,
            selectedOption: event.selectedOption,
            timestamp: new Date(event.timestamp),
          };
          setWheelSpinHistory((prev) => [result, ...prev]);
          onWheelSpun?.(result);
          break;
        }
      }
    },
    [onTimerComplete, onCoinFlipped, onWheelSpun]
  );

  return {
    timerState,
    coinFlipHistory,
    wheelSpinHistory,
    isLoading,
    error,
    startTimer,
    pauseTimer,
    resumeTimer,
    resetTimer,
    flipCoin,
    spinWheel,
    addRandomToolEventFromSSE,
  };
}
