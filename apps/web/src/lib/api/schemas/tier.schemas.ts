/**
 * Tier & Usage Schemas (Game Night Improvvisata - E2-4/E2-5)
 *
 * Zod schemas for user usage snapshot and tier definitions.
 * Matches backend TierDefinitionDto and TierLimitsDto.
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
// Tier Limits (matches backend TierLimitsDto)
// ========================================

export const TierLimitsSchema = z.object({
  maxPrivateGames: z.number().int(),
  maxPdfUploadsPerMonth: z.number().int(),
  maxPdfSizeBytes: z.number().int(),
  maxAgents: z.number().int(),
  maxAgentQueriesPerDay: z.number().int(),
  maxSessionQueries: z.number().int(),
  maxSessionPlayers: z.number().int(),
  maxPhotosPerSession: z.number().int(),
  sessionSaveEnabled: z.boolean(),
  maxCatalogProposalsPerWeek: z.number().int(),
});

export type TierLimits = z.infer<typeof TierLimitsSchema>;

// ========================================
// Tier Limit (legacy array format — kept for UI compatibility)
// ========================================

export const TierLimitSchema = z.object({
  key: z.string(),
  value: z.number(),
});

export type TierLimit = z.infer<typeof TierLimitSchema>;

// ========================================
// Tier Definition (matches backend TierDefinitionDto)
// ========================================

export const TierDefinitionSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  displayName: z.string(),
  limits: TierLimitsSchema,
  llmModelTier: z.string(),
  isDefault: z.boolean(),
  createdAt: z.string(),
  updatedAt: z.string(),
});

export type TierDefinition = z.infer<typeof TierDefinitionSchema>;

// ========================================
// Tier List Response
// ========================================

export const TierListResponseSchema = z.array(TierDefinitionSchema);
