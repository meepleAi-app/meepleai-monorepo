/**
 * useSubmitMechanicCardFeedback — mutation for per-claim 👍/👎 feedback on a
 * published mechanic card (ME-M3.1, Issue #533).
 *
 * Wraps `api.sharedGames.submitMechanicCardFeedback(cardId, body)`:
 *   - resolves on 201 (created) / 200 (updated — idempotent vote change)
 *   - rejects with a typed `ApiError` on 404 / 429 / 401 / 5xx so the caller can
 *     branch on `instanceof NotFoundError` / `instanceof RateLimitError`
 *
 * The published card DTO carries no feedback state (votes are tracked client-side
 * for the current session), so there is deliberately NO query invalidation here —
 * re-fetching the whole `staleTime: 10min` card on every vote would be wasteful.
 */
import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { SubmitMechanicCardFeedbackBody } from '@/lib/api/schemas/mechanic-card-feedback.schemas';

export interface SubmitMechanicCardFeedbackInput {
  /** Published card UUID (the card the claim belongs to). */
  cardId: string;
  body: SubmitMechanicCardFeedbackBody;
}

export type UseSubmitMechanicCardFeedbackResult = UseMutationResult<
  void,
  Error,
  SubmitMechanicCardFeedbackInput
>;

export function useSubmitMechanicCardFeedback(): UseSubmitMechanicCardFeedbackResult {
  return useMutation<void, Error, SubmitMechanicCardFeedbackInput>({
    mutationFn: ({ cardId, body }) => api.sharedGames.submitMechanicCardFeedback(cardId, body),
  });
}
