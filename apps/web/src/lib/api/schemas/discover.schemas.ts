/**
 * /discover Schemas (Wave 3 Phase 1, Issue #805)
 *
 * Zod schemas for the /discover route's quick-win rails. Backend contract
 * is defined in PR #732 §4.3:
 *   - §4.3.2 — GET /api/v1/catalog/games/new       (NewGame)
 *   - §4.3.3 — GET /api/v1/agents/popular          (PopularAgent)
 *   - §4.3.5 — GET /api/v1/kb-docs/recent          (RecentKbDoc)
 *
 * All three follow the PR #732 §3.4 empty-state contract: response shape is
 * `{ items: [...] }` and an empty list is a 200 (not a 404 / 204).
 */

import { z } from 'zod';

// ========== /api/v1/catalog/games/new (PR #732 §4.3.2) ==========

export const NewGameSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  publisher: z.string().nullable(),
  // Year is nullable because legacy SharedGame rows can have YearPublished == 0
  // which the backend surfaces as null.
  year: z.number().int().nullable(),
  imageUrl: z.string().nullable(),
  // ISO 8601 timestamp; backend serialises with offset.
  createdAt: z.string().datetime({ offset: true }),
});
export type NewGame = z.infer<typeof NewGameSchema>;

export const NewGamesResponseSchema = z.object({
  items: z.array(NewGameSchema),
});
export type NewGamesResponse = z.infer<typeof NewGamesResponseSchema>;

// ========== /api/v1/agents/popular (PR #732 §4.3.3) ==========

export const PopularAgentSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  gameId: z.string().uuid().nullable(),
  gameName: z.string().nullable(),
  // Schema reality v1 carryover (Gate B): backend always returns 0 until
  // AgentInstallation tracking lands. The field is part of the wire contract
  // so the rail UI can adopt the real metric without a fetch shape change.
  installCount: z.number().int().nonnegative(),
  invocationCount: z.number().int().nonnegative(),
});
export type PopularAgent = z.infer<typeof PopularAgentSchema>;

export const PopularAgentsResponseSchema = z.object({
  items: z.array(PopularAgentSchema),
});
export type PopularAgentsResponse = z.infer<typeof PopularAgentsResponseSchema>;

// ========== /api/v1/kb-docs/recent (PR #732 §4.3.5) ==========

/**
 * KB document type vocabulary for the wire contract (PR #732 §4.3.5).
 *
 * Backend collapses the 7-value `DocumentCategory` enum into this 4-value
 * surface. `'faq'` is reserved for forward-compat with the
 * `GameFaqEntity` surface (currently not emitted by /kb-docs/recent in v1).
 */
export const RecentKbDocTypeSchema = z.enum(['rulebook', 'faq', 'errata', 'guide']);
export type RecentKbDocType = z.infer<typeof RecentKbDocTypeSchema>;

export const RecentKbDocSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  gameId: z.string().uuid().nullable(),
  gameName: z.string().nullable(),
  docType: RecentKbDocTypeSchema,
  lastIngestedAt: z.string().datetime({ offset: true }),
  chunkCount: z.number().int().nonnegative(),
});
export type RecentKbDoc = z.infer<typeof RecentKbDocSchema>;

export const RecentKbDocsResponseSchema = z.object({
  items: z.array(RecentKbDocSchema),
});
export type RecentKbDocsResponse = z.infer<typeof RecentKbDocsResponseSchema>;
