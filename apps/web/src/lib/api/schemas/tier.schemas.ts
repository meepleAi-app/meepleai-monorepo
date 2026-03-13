/**
 * Tier & Usage Schemas (Game Night Improvvisata - E2-4/E2-5)
 *
 * Zod schemas for user usage snapshot and tier definitions.
 */

import { z } from 'zod';

// ========================================
// Usage Snapshot
// ========================================

export const UsageSnapshotSchema = z.object({
  privateGames: z.number(),
  privateGamesMax: z.number(),
  pdfThisMonth: z.number(),
  pdfThisMonthMax: z.number(),
  agentQueriesToday: z.number(),
  agentQueriesTodayMax: z.number(),
  sessionQueries: z.number(),
  sessionQueriesMax: z.number(),
  agents: z.number(),
  agentsMax: z.number(),
  photosThisSession: z.number(),
  photosThisSessionMax: z.number(),
  sessionSaveEnabled: z.boolean(),
  catalogProposalsThisWeek: z.number(),
  catalogProposalsThisWeekMax: z.number(),
});

export type UsageSnapshot = z.infer<typeof UsageSnapshotSchema>;

// ========================================
// Tier Limit Definition
// ========================================

export const TierLimitSchema = z.object({
  key: z.string(),
  value: z.number(),
});

export type TierLimit = z.infer<typeof TierLimitSchema>;

// ========================================
// Tier Definition
// ========================================

export const TierDefinitionSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  displayName: z.string(),
  description: z.string().nullable().optional(),
  monthlyPriceEur: z.number(),
  isActive: z.boolean(),
  limits: z.array(TierLimitSchema),
});

export type TierDefinition = z.infer<typeof TierDefinitionSchema>;

// ========================================
// Tier List Response
// ========================================

export const TierListResponseSchema = z.array(TierDefinitionSchema);
