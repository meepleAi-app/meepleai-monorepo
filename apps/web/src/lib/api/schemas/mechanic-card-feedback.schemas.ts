/**
 * Per-claim mechanic-card feedback schemas (ME-M3.1, Issue #533).
 *
 * Backend endpoint (locked contract, already implemented):
 *   POST /api/v1/mechanic-cards/{cardId}/feedback   → authenticated
 *
 * Request body (camelCase JSON):
 *   { claimId, isPositive, errorType, description, suggestedCitation }
 *
 * `errorType` is meaningful ONLY for a 👎 (isPositive === false) and MUST be
 * `null` for a 👍. `description` + `suggestedCitation` are always optional.
 *
 * Responses (status-only, no body needed):
 *   201 → created (first vote for this (card,user,claim))
 *   200 → updated (idempotent — the user changed their vote)
 *   404 → card missing or suppressed/taken down
 *   429 → per-day cap reached (10 new/day)
 *   401 → unauthenticated
 */
import { z } from 'zod';

/** The three "report error" categories a 👎 can carry. */
export const MECHANIC_CARD_ERROR_TYPES = ['factual', 'ambiguous', 'contradicts_rule'] as const;
export type MechanicCardErrorType = (typeof MECHANIC_CARD_ERROR_TYPES)[number];

/** Human labels for the report-error type select (matches the card's English copy). */
export const MECHANIC_CARD_ERROR_TYPE_LABELS: Record<MechanicCardErrorType, string> = {
  factual: 'Factual error',
  ambiguous: 'Ambiguous',
  contradicts_rule: 'Contradicts a rule',
};

export const MechanicCardErrorTypeSchema = z.enum(MECHANIC_CARD_ERROR_TYPES);

/**
 * Request body for POST /api/v1/mechanic-cards/{cardId}/feedback.
 * Kept as a plain type (the endpoint is status-only, there is no response DTO to
 * parse) but mirrored by a zod schema so tests / defensive callers can validate.
 */
export const SubmitMechanicCardFeedbackBodySchema = z.object({
  claimId: z.string().uuid(),
  isPositive: z.boolean(),
  /** Only for 👎; MUST be null for 👍. */
  errorType: MechanicCardErrorTypeSchema.nullable(),
  description: z.string().nullable(),
  suggestedCitation: z.string().nullable(),
});
export type SubmitMechanicCardFeedbackBody = z.infer<typeof SubmitMechanicCardFeedbackBodySchema>;

export const MECHANIC_CARD_FEEDBACK_ROUTES = {
  /** Authenticated per-claim feedback endpoint. `cardId` is the published card id. */
  feedback: (cardId: string) => `/api/v1/mechanic-cards/${cardId}/feedback`,
} as const;
