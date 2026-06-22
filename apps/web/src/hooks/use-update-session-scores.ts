/**
 * useUpdateSessionScores — TanStack Query mutation for the polymorphic
 * `PUT /api/v1/game-sessions/{id}/scores-polymorphic` endpoint.
 *
 * The backend command `UpdateSessionScoresCommand` (asse A v2.1 SHIPPED in
 * PR #1917) expects `scoreData` as a JSON-encoded string; this hook handles
 * that serialization for callers. The IDOR guard returns 403 when the caller
 * is not the host of the target session, and 400 wraps per-strategy
 * validation failures from the matching `IScoringStrategy`.
 *
 * Asse D follow-up P1 (#1899) T5.
 *
 * @module hooks/use-update-session-scores
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';

import type { ScoreType } from '@/components/sessions/score-strategies/types';
import { getApiBase } from '@/lib/api/core/httpClient';

export interface UpdateSessionScoresPayload {
  sessionId: string;
  scoringType: ScoreType;
  /**
   * Strategy-specific payload. Shape MUST match `scoringType` (see
   * `ScoreDataByType`). It is JSON-stringified on the wire because the
   * backend command stores raw JSON for forward-compat with future strategy
   * additions.
   */
  scoreData: unknown;
}

export interface UpdateSessionScoresResult {
  sessionId: string;
  scoringType: ScoreType;
  computedWinnerId: string | null;
}

/**
 * Distinct mutation error so callers can surface IDOR rejections vs generic
 * validation/server failures with different UX (e.g. forbidden = navigate
 * away, validation = inline help text).
 */
export class UpdateSessionScoresError extends Error {
  constructor(
    message: string,
    public readonly kind: 'forbidden' | 'validation' | 'server',
    public readonly status: number,
    public readonly details?: unknown
  ) {
    super(message);
    this.name = 'UpdateSessionScoresError';
  }
}

export function useUpdateSessionScores() {
  const queryClient = useQueryClient();

  return useMutation<
    UpdateSessionScoresResult,
    UpdateSessionScoresError,
    UpdateSessionScoresPayload
  >({
    mutationFn: async ({ sessionId, scoringType, scoreData }) => {
      const apiBase = getApiBase();
      const res = await fetch(`${apiBase}/api/v1/game-sessions/${sessionId}/scores-polymorphic`, {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          scoringType,
          scoreData: JSON.stringify(scoreData),
        }),
      });

      if (res.status === 403) {
        throw new UpdateSessionScoresError(
          'Non sei autorizzato ad aggiornare i punteggi (solo host).',
          'forbidden',
          403
        );
      }

      if (res.status === 400) {
        let details: unknown;
        try {
          details = await res.json();
        } catch {
          details = await res.text().catch(() => undefined);
        }
        throw new UpdateSessionScoresError(
          'Validazione punteggi fallita.',
          'validation',
          400,
          details
        );
      }

      if (!res.ok) {
        throw new UpdateSessionScoresError(
          `Aggiornamento punteggi fallito: ${res.status}`,
          'server',
          res.status
        );
      }

      return (await res.json()) as UpdateSessionScoresResult;
    },
    onSuccess: (_data, { sessionId }) => {
      // The session and live-session caches both reflect score state.
      queryClient.invalidateQueries({ queryKey: ['session', sessionId] });
      queryClient.invalidateQueries({ queryKey: ['live-session', sessionId] });
    },
  });
}
