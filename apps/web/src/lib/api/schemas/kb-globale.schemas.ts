/**
 * KB Globale Schemas (Issue #1482 Task 0)
 *
 * Zod schemas for the cross-game global KB search endpoint
 * (BE Phase 1 #1661): POST /api/v1/knowledge-base/search/global.
 *
 * Matches `GlobalKbSearchResultDto` from
 * apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/GlobalKbSearchResultDto.cs.
 */

import { z } from 'zod';

/**
 * SearchMode enum for the search request.
 * Currently only "Semantic" is supported in v1.
 * Will be extended when BE adds Keyword or other search modes.
 * @see apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/SearchMode.cs
 */
export const SearchModeSchema = z.enum(['Semantic']);

export type SearchMode = z.infer<typeof SearchModeSchema>;

/**
 * GlobalKbSearchResultDto — a single enriched result from cross-game KB search.
 * Mirrors the BE DTO fields 1:1 with camelCase transformation.
 */
export const GlobalKbSearchResultSchema = z.object({
  chunkId: z.string(),
  docId: z.string().uuid(),
  docTitle: z.string(),
  gameId: z.string().uuid(),
  gameName: z.string(),
  docType: z.string(),
  headingPath: z.string().nullable(),
  snippet: z.string(),
  pageNumber: z.number().int().nullable(),
  score: z.number(),
});

export type GlobalKbSearchResult = z.infer<typeof GlobalKbSearchResultSchema>;

/**
 * GlobalKbSearchRequest — the POST /knowledge-base/search/global request body.
 */
export const GlobalKbSearchRequestSchema = z.object({
  query: z.string().min(1, 'Query must not be empty'),
  limit: z.number().int().positive().optional(),
  cursor: z.string().nullable().optional(),
  mode: SearchModeSchema.optional(),
});

export type GlobalKbSearchRequest = z.infer<typeof GlobalKbSearchRequestSchema>;

/**
 * GlobalKbSearchResponse envelope — the complete response from /knowledge-base/search/global.
 */
export const GlobalKbSearchResponseSchema = z
  .object({
    results: z.array(GlobalKbSearchResultSchema),
    hasMore: z.boolean(),
    nextCursor: z.string().nullable(),
  })
  .strict();

export type GlobalKbSearchResponse = z.infer<typeof GlobalKbSearchResponseSchema>;
