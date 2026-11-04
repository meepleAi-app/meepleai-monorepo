/**
 * Ownership & RAG Access API Schemas
 *
 * Zod schemas for ownership declaration and quick-create tutor flows.
 */

import { z } from 'zod';

// ========== Ownership Declaration ==========

/**
 * Result of declaring ownership of a game
 * POST /api/v1/library/{gameId}/declare-ownership
 */
export const OwnershipResultSchema = z.object({
  gameState: z.string(),
  ownershipDeclaredAt: z.string().datetime({ offset: true }).nullable(),
  hasRagAccess: z.boolean(),
  kbCardCount: z.number().int().nonnegative(),
  isRagPublic: z.boolean(),
});

export type OwnershipResult = z.infer<typeof OwnershipResultSchema>;
