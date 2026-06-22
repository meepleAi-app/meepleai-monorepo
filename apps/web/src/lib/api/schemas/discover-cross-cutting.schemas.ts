/**
 * /discover Cross-Cutting Schemas (Wave 3 Phase 4a, Issue #805 / PR #732 §4.3)
 *
 * Zod schemas for the SP4 /discover route's Phase 4a rails. Backend contract:
 *   - §4.3.4 — GET /api/v1/toolkits/recommended       (RecommendedToolkit)
 *   - §4.3.6 — GET /api/v1/users/top-contributors      (TopUserContributor)
 *
 * Both follow the PR #732 §3.4 empty-state contract: response shape is
 * `{ items: [...] }` and an empty list is a 200 (not 404 / 204).
 *
 * Schema reality v1 carryovers (Gate B) — backend stubs documented inline:
 *   - RecommendedToolkit.installCount: 0 (no ToolkitInstallation entity)
 *   - RecommendedToolkit.ratingAverage: null (no ToolkitRating entity)
 *   - RecommendedToolkit.ratingCount: 0 (same)
 *   - RecommendedToolkit.coverImageUrl: null (no cover column)
 *   - TopUserContributor.breakdown.faqsCount: 0 (no FAQ creator FK)
 *   - TopUserContributor.breakdown.agentsCreatedCount: 0 (no agent owner FK)
 *
 * In v1 only `breakdown.kbUploadsCount` carries a real signal, and
 * `contributionCount` therefore equals `kbUploadsCount`.
 */

import { z } from 'zod';

// ========== /api/v1/toolkits/recommended (PR #732 §4.3.4) ==========

export const RecommendedToolkitSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  authorName: z.string().min(1),
  // Gate B v1: backend always returns 0 until ToolkitInstallation lands.
  installCount: z.number().int().nonnegative(),
  // Gate B v1: backend always returns null until ToolkitRating lands.
  ratingAverage: z.number().nullable(),
  ratingCount: z.number().int().nonnegative(),
  // Gate B v1: backend always returns null until a cover-image column lands.
  coverImageUrl: z.string().nullable(),
});
export type RecommendedToolkit = z.infer<typeof RecommendedToolkitSchema>;

export const RecommendedToolkitsResponseSchema = z.object({
  items: z.array(RecommendedToolkitSchema),
});
export type RecommendedToolkitsResponse = z.infer<typeof RecommendedToolkitsResponseSchema>;

// ========== /api/v1/users/top-contributors (PR #732 §4.3.6) ==========

export const TopUserContributorBreakdownSchema = z.object({
  // Gate B v1: GameFaqEntity has no creator FK column → always 0.
  faqsCount: z.number().int().nonnegative(),
  // Real signal in v1 — derived from PdfDocumentEntity.UploadedByUserId.
  kbUploadsCount: z.number().int().nonnegative(),
  // Gate B v1: AgentDefinition has no owner FK column → always 0.
  agentsCreatedCount: z.number().int().nonnegative(),
});
export type TopUserContributorBreakdown = z.infer<typeof TopUserContributorBreakdownSchema>;

export const TopUserContributorSchema = z.object({
  id: z.string().uuid(),
  displayName: z.string().min(1),
  avatarUrl: z.string().nullable(),
  contributionCount: z.number().int().nonnegative(),
  breakdown: TopUserContributorBreakdownSchema,
});
export type TopUserContributor = z.infer<typeof TopUserContributorSchema>;

export const TopUserContributorsResponseSchema = z.object({
  items: z.array(TopUserContributorSchema),
});
export type TopUserContributorsResponse = z.infer<typeof TopUserContributorsResponseSchema>;
