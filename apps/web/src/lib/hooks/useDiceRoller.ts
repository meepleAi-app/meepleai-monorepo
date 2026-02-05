/**
 * useDiceRoller Hook (Issue #3342)
 *
 * Manages dice rolling state, API calls, and SSE event handling.
 *
 * Features:
 * - Roll dice with formulas (2d6+3, 1d20-2)
 * - Track roll history
 * - Real-time updates via SSE
 * - Optimistic updates
 *
 * Backend endpoint:
 * - POST /api/v1/game-sessions/{sessionId}/dice
 * - GET /api/v1/game-sessions/{sessionId}/dice (history)
 *
 * @example
 * ```typescript
 * const { roll, rollHistory, isRolling, error } = useDiceRoller({
 *   sessionId: 'abc123',
 *   participantId: 'participant-id',
 *   participantName: 'Player 1',
 * });
 *
 * const result = await roll('2d6+3', 'Attack roll');
 * ```
 */

import { useState, useCallback, useEffect, useRef } from 'react';

import type { DiceRoll } from '@/components/session/types';

/**
 * API Response Types
 */
interface RollDiceResponse {
  diceRollId: string;
  formula: string;
  rolls: number[];
  modifier: number;
  total: number;
  timestamp: string;
}

interface DiceRollHistoryResponse {
  id: string;
  participantId: string;
  participantName: string;
  formula: string;
  label?: string;
  rolls: number[];
  modifier: number;
  total: number;
  timestamp: string;
}

/**
 * SSE Event for dice rolls
 */
interface DiceRolledEventData {
  diceRollId: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  formula: string;
  label?: string;
  rolls: number[];
  modifier: number;
  total: number;
  timestamp: string;
}

/**
 * Hook options
 */
export interface UseDiceRollerOptions {
  /** Session ID */
  sessionId: string;

  /** Current participant ID */
  participantId: string;

  /** Current participant display name */
  participantName: string;

  /** API base URL */
  apiBaseUrl?: string;

  /** Maximum history items to fetch */
  historyLimit?: number;

  /** Callback when a roll is received (from self or others) */
  onRollReceived?: (roll: DiceRoll) => void;
}

/**
 * Hook return value
 */
export interface DiceRollerState {
  /** Roll dice with a formula */
  roll: (formula: string, label?: string) => Promise<DiceRoll>;

  /** Recent roll history */
  rollHistory: DiceRoll[];

  /** Whether a roll is in progress */
  isRolling: boolean;

  /** Error if roll failed */
  error: Error | null;

  /** Add a roll from SSE event */
  addRollFromEvent: (event: DiceRolledEventData) => void;

  /** Refresh roll history from server */
  refreshHistory: () => Promise<void>;
}

/**
 * useDiceRoller Hook
 *
 * Manages dice rolling operations and history
 */
export function useDiceRoller(options: UseDiceRollerOptions): DiceRollerState {
  const {
    sessionId,
    participantId,
    participantName,
    apiBaseUrl,
    historyLimit = 20,
    onRollReceived,
  } = options;

  const [rollHistory, setRollHistory] = useState<DiceRoll[]>([]);
  const [isRolling, setIsRolling] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  // Track pending roll IDs to avoid duplicate optimistic updates
  const pendingRollsRef = useRef<Set<string>>(new Set());

  const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';

  /**
   * Fetch roll history from server
   */
  const refreshHistory = useCallback(async () => {
    try {
      const response = await fetch(
        `${baseUrl}/api/v1/game-sessions/${sessionId}/dice?limit=${historyLimit}`,
        {
          method: 'GET',
          credentials: 'include',
          headers: {
            'Content-Type': 'application/json',
          },
        }
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch dice history: ${response.status}`);
      }

      const data: DiceRollHistoryResponse[] = await response.json();

      setRollHistory(
        data.map((roll) => ({
          id: roll.id,
          participantId: roll.participantId,
          participantName: roll.participantName,
          formula: roll.formula,
          label: roll.label,
          rolls: roll.rolls,
          modifier: roll.modifier,
          total: roll.total,
          timestamp: new Date(roll.timestamp),
        }))
      );
    } catch (err) {
      console.error('[useDiceRoller] Failed to fetch history:', err);
    }
  }, [baseUrl, sessionId, historyLimit]);

  /**
   * Roll dice
   */
  const roll = useCallback(
    async (formula: string, label?: string): Promise<DiceRoll> => {
      setIsRolling(true);
      setError(null);

      try {
        const response = await fetch(
          `${baseUrl}/api/v1/game-sessions/${sessionId}/dice`,
          {
            method: 'POST',
            credentials: 'include',
            headers: {
              'Content-Type': 'application/json',
            },
            body: JSON.stringify({
              sessionId,
              participantId,
              formula,
              label,
            }),
          }
        );

        if (!response.ok) {
          const errorBody = await response.text();
          throw new Error(`Roll failed: ${response.status} - ${errorBody}`);
        }

        const data: RollDiceResponse = await response.json();

        const diceRoll: DiceRoll = {
          id: data.diceRollId,
          participantId,
          participantName,
          formula: data.formula,
          label,
          rolls: data.rolls,
          modifier: data.modifier,
          total: data.total,
          timestamp: new Date(data.timestamp),
        };

        // Mark this roll as pending (will be replaced by SSE event)
        pendingRollsRef.current.add(data.diceRollId);

        // Optimistic update
        setRollHistory((prev) => [diceRoll, ...prev].slice(0, historyLimit));

        onRollReceived?.(diceRoll);

        return diceRoll;
      } catch (err) {
        const errorObj = err instanceof Error ? err : new Error('Unknown error');
        setError(errorObj);
        throw errorObj;
      } finally {
        setIsRolling(false);
      }
    },
    [baseUrl, sessionId, participantId, participantName, historyLimit, onRollReceived]
  );

  /**
   * Add a roll from SSE event (from other participants or confirmation)
   */
  const addRollFromEvent = useCallback(
    (event: DiceRolledEventData) => {
      const diceRoll: DiceRoll = {
        id: event.diceRollId,
        participantId: event.participantId,
        participantName: event.participantName,
        formula: event.formula,
        label: event.label,
        rolls: event.rolls,
        modifier: event.modifier,
        total: event.total,
        timestamp: new Date(event.timestamp),
      };

      // If this is a roll we made, we already have it via optimistic update
      if (pendingRollsRef.current.has(event.diceRollId)) {
        pendingRollsRef.current.delete(event.diceRollId);
        return; // Skip duplicate
      }

      // Add roll from other participant
      setRollHistory((prev) => {
        // Check if we already have this roll (by ID)
        if (prev.some((r) => r.id === event.diceRollId)) {
          return prev;
        }
        return [diceRoll, ...prev].slice(0, historyLimit);
      });

      onRollReceived?.(diceRoll);
    },
    [historyLimit, onRollReceived]
  );

  // Fetch initial history on mount
  useEffect(() => {
    void refreshHistory();
  }, [refreshHistory]);

  return {
    roll,
    rollHistory,
    isRolling,
    error,
    addRollFromEvent,
    refreshHistory,
  };
}
