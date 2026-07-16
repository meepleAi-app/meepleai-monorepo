import { z } from 'zod';

import { GameIdString } from './common.schemas';

/**
 * Mirrors backend `KbDocConsumingAgentDto`
 * (BoundedContexts/KnowledgeBase/Application/DTOs/KbDocConsumingAgentDto.cs).
 * Issue #1651: F3-FU-2 — Used-by tab.
 */
export const KbDocConsumingAgentSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  type: z.string(),
  isActive: z.boolean(),
  status: z.enum(['Draft', 'Testing', 'Published']),
  isSystemDefined: z.boolean(),
  typologySlug: z.string().nullable(),
  gameId: GameIdString.nullable(),
  gameName: z.string().nullable(),
  invocationCount: z.number().int().nonnegative(),
  lastInvokedAt: z.string().datetime({ offset: true }).nullable(),
});
export type KbDocConsumingAgent = z.infer<typeof KbDocConsumingAgentSchema>;

export const KbDocConsumingAgentsResponseSchema = z.array(KbDocConsumingAgentSchema);
