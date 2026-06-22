/**
 * /toolkits Marketplace Schemas (Wave 3 Phase 2, Issue #805)
 *
 * Zod schemas for the SP4 /toolkits/[id] marketplace surface. Backend
 * contract is defined in PR #732 §5.3:
 *   - §5.3.1 — GET  /api/v1/toolkits/{id}             (ToolkitDetailResponse)
 *   - §5.3.2 — GET  /api/v1/toolkits/{id}/versions    (ToolkitVersionsResponse)
 *   - §5.3.5 — POST /api/v1/toolkits/{id}/install     (InstallToolkitResponse)
 *
 * Schema reality v1 carryovers (Gate B) — backend stubs documented inline:
 *   - installCount: always 0 (no ToolkitInstallation entity yet).
 *   - ratingAverage / ratingCount: null / 0 (no ToolkitRating entity yet).
 *   - currentVersion: "1.0.{int}" until semver schema lands.
 *   - publishedAt: derived from UpdatedAt for approved toolkits, else null.
 *   - yankedAt: always null in v1 (no yank workflow yet).
 *
 * Wire shape is stable so the FE surface can adopt the real metrics in
 * Phase 4 without a fetch-shape change.
 */

import { z } from 'zod';

// ========== /api/v1/toolkits/{id} (PR #732 §5.3.1) ==========

export const ToolkitAgentSummarySchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  systemPromptPreview: z.string(),
});
export type ToolkitAgentSummary = z.infer<typeof ToolkitAgentSummarySchema>;

export const ToolkitDetailSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  description: z.string(),
  authorId: z.string().uuid(),
  authorName: z.string().min(1),
  authorAvatarUrl: z.string().nullable(),
  coverImageUrl: z.string().nullable(),
  agent: ToolkitAgentSummarySchema,
  kbDocsCount: z.number().int().nonnegative(),
  toolsCount: z.number().int().nonnegative(),
  // Schema reality v1 carryover (Gate B): backend always returns 0 until
  // ToolkitInstallation tracking lands.
  installCount: z.number().int().nonnegative(),
  // Schema reality v1 carryover (Gate B): null/0 until ToolkitRating lands.
  ratingAverage: z.number().nullable(),
  ratingCount: z.number().int().nonnegative(),
  createdAt: z.string().datetime({ offset: true }),
  publishedAt: z.string().datetime({ offset: true }).nullable(),
  yankedAt: z.string().datetime({ offset: true }).nullable(),
  // Schema reality v1 carryover (Gate B): "1.0.{int}" until semver lands.
  currentVersion: z.string().min(1),
  // ── Stage 3 marketplace extension (PR #1156 / issue #1144) ──
  /** SPDX-like license string (e.g. "CC BY-SA 4.0"). Nullable → FE hides meta row. */
  license: z.string().nullable().optional(),
  /** LEFT JOIN of GameEntity via GameToolkit.GameId. Null when no game / soft-deleted. */
  gameName: z.string().nullable().optional(),
  /** UTF-8 byte count of AgentConfig + tool/template JSON. */
  sizeBytes: z.number().int().nonnegative().nullable().optional(),
});
export type ToolkitDetail = z.infer<typeof ToolkitDetailSchema>;

export const ViewerContextSchema = z.object({
  isOwner: z.boolean(),
  hasInstalled: z.boolean(),
  // Server-derived: hasInstalled && !alreadyRated && !isOwner. With v1 stub
  // hasInstalled=false the flag collapses to false.
  canRate: z.boolean(),
});
export type ViewerContext = z.infer<typeof ViewerContextSchema>;

export const ToolkitDetailResponseSchema = z.object({
  toolkit: ToolkitDetailSchema,
  viewerContext: ViewerContextSchema,
});
export type ToolkitDetailResponse = z.infer<typeof ToolkitDetailResponseSchema>;

// ========== /api/v1/toolkits/{id}/versions (PR #732 §5.3.2) ==========

export const ToolkitVersionSchema = z.object({
  version: z.string().min(1),
  publishedAt: z.string().datetime({ offset: true }),
  yankedAt: z.string().datetime({ offset: true }).nullable(),
  changelog: z.string(),
  isCurrent: z.boolean(),
});
export type ToolkitVersion = z.infer<typeof ToolkitVersionSchema>;

export const ToolkitVersionsResponseSchema = z.object({
  items: z.array(ToolkitVersionSchema),
});
export type ToolkitVersionsResponse = z.infer<typeof ToolkitVersionsResponseSchema>;

// ========== POST /api/v1/toolkits/{id}/install (PR #732 §5.3.5) ==========

export const InstallToolkitResponseSchema = z.object({
  // Schema reality v1 carryover (Gate B): always 0 until installation entity.
  installCount: z.number().int().nonnegative(),
  hasInstalled: z.boolean(),
});
export type InstallToolkitResponse = z.infer<typeof InstallToolkitResponseSchema>;
