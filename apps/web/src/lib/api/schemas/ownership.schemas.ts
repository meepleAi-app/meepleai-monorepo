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
  ownershipDeclaredAt: z.string().datetime().nullable(),
  hasRagAccess: z.boolean(),
  kbCardCount: z.number().int().nonnegative(),
  isRagPublic: z.boolean(),
});

export type OwnershipResult = z.infer<typeof OwnershipResultSchema>;

// ========== Quick Create Tutor ==========

/**
 * Result of quick-creating a tutor agent
 * POST /api/v1/agents/quick-create
 */
export const QuickCreateResultSchema = z.object({
  agentId: z.string().uuid(),
  chatThreadId: z.string().uuid(),
  agentName: z.string(),
  kbCardCount: z.number().int().nonnegative(),
});

export type QuickCreateResult = z.infer<typeof QuickCreateResultSchema>;
