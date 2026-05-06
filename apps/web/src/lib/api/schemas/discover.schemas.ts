/**
 * Discover API Schemas (Issue #728)
 *
 * Zod schemas for the composite /discover dashboard payload.
 * Mirrors the backend DiscoverDto and its row DTOs.
 */

import { z } from 'zod';

// ========== Row Schemas ==========

export const newGameSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  imageUrl: z.string().nullable().optional(),
  createdAt: z.string().datetime({ offset: true }),
  ratingAverage: z.number().nullable().optional(),
});
export type NewGame = z.infer<typeof newGameSchema>;

export const topAgentSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  gameName: z.string(),
  agentType: z.string(),
  installCount: z.number().int().nonnegative(),
  createdAt: z.string().datetime({ offset: true }),
});
export type TopAgent = z.infer<typeof topAgentSchema>;

export const recommendedToolkitSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  gameName: z.string(),
  version: z.number().int().nonnegative(),
  installCount: z.number().int().nonnegative(),
  createdAt: z.string().datetime({ offset: true }),
});
export type RecommendedToolkit = z.infer<typeof recommendedToolkitSchema>;

export const recentKbDocSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  gameName: z.string(),
  documentCategory: z.string(),
  indexedAt: z.string().datetime({ offset: true }),
  language: z.string(),
});
export type RecentKbDoc = z.infer<typeof recentKbDocSchema>;

export const topContributorSchema = z.object({
  userId: z.string().uuid(),
  displayName: z.string(),
  avatarUrl: z.string().nullable().optional(),
  contributionCount: z.number().int().nonnegative(),
  kbUploadCount: z.number().int().nonnegative(),
  agentCount: z.number().int().nonnegative(),
});
export type TopContributor = z.infer<typeof topContributorSchema>;

// ========== Composite Schema ==========

export const discoverSchema = z.object({
  newGames: z.array(newGameSchema),
  topAgents: z.array(topAgentSchema),
  recommendedToolkits: z.array(recommendedToolkitSchema),
  recentKb: z.array(recentKbDocSchema),
  topContributors: z.array(topContributorSchema),
});
export type Discover = z.infer<typeof discoverSchema>;
