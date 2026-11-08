/**
 * useRecordRoundScore — writes one cell of a LiveGameSession's `RoundScores` (#4156 fetta 2).
 *
 * A new cell goes through `POST /api/v1/live-sessions/{id}/scores`, a correction through `PUT`.
 * The backend replaces the cell either way and recalculates the totals, so on success the live
 * session is refetched: the totals on screen are always the backend's.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { RecordScoreRequest } from '@/lib/api/schemas/live-sessions.schemas';

import { liveSessionKeys } from './useLiveSession';

export interface RecordRoundScoreInput {
  readonly request: RecordScoreRequest;
  /** The cell already has a value: the write is a correction. */
  readonly correction: boolean;
}

export function useRecordRoundScore(
  sessionId: string
): UseMutationResult<void, Error, RecordRoundScoreInput> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ request, correction }: RecordRoundScoreInput) =>
      correction
        ? api.liveSessions.editScore(sessionId, request)
        : api.liveSessions.recordScore(sessionId, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: liveSessionKeys.detail(sessionId) }),
  });
}
