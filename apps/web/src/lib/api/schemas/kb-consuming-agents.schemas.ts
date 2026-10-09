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
  // Issue #4138: a required `type: z.string()` stood here, mirroring a backend field
  // that is gone. Left in place it would have failed the parse on every response.
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
