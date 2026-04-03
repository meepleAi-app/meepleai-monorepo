/**
 * useSessionToolLog — Fire-and-forget session tool event logger.
 *
 * Sends ToolAction events to the SessionTracking BC via:
 *   POST /api/v1/game-sessions/{sessionId}/events
 *
 * Error handling: silent — network failures are logged to console only.
 * No state, no blocking, no retry.
 *
 * Phase 2 toolkit logging (companion to Phase 1 standalone toolkit).
 */

import { useCallback } from 'react';

import { logger } from '@/lib/logger';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE ?? '';

export interface UseSessionToolLogResult {
  /** Fire-and-forget: log a tool action to the session diary. */
  logToolAction: (toolType: string, action: string, result: string) => void;
}

export function useSessionToolLog(sessionId: string): UseSessionToolLogResult {
  const logToolAction = useCallback(
    (toolType: string, action: string, result: string) => {
      void fetch(`${API_BASE}/api/v1/game-sessions/${encodeURIComponent(sessionId)}/events`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({
          eventType: 'ToolAction',
          payload: JSON.stringify({ toolType, action, result }),
          source: 'toolkit',
        }),
      })
        .then(res => {
          if (!res.ok) {
            logger.warn('[useSessionToolLog] server rejected tool event', {
              metadata: { status: res.status },
            });
          }
        })
        .catch((err: unknown) => {
          logger.warn('[useSessionToolLog] failed to log tool action', {
            metadata: { error: String(err) },
          });
        });
    },
    [sessionId]
  );

  return { logToolAction };
}
