import { z } from 'zod';

/**
 * Issue #1674: Per-doc embeddings viewer metadata DTO.
 *
 * Security note (Newman corpus-reconstruction risk): NO raw vector values
 * are ever serialized in this DTO. Whitelist-only fields. See spec
 * docs/superpowers/specs/2026-06-05-per-doc-embeddings-viewer-design.md §7.
 */
export const DocumentEmbeddingsMetaDtoSchema = z.object({
  docId: z.string().uuid(),
  model: z.string().min(1),
  dimensions: z.number().int().positive(),
  totalChunks: z.number().int().nonnegative(),
  indexedAt: z.string().nullable(),
  language: z.string().nullable(),
});

export type DocumentEmbeddingsMetaDto = z.infer<typeof DocumentEmbeddingsMetaDtoSchema>;

/**
 * Mirrors BE DocChunkSearchHit (Issue #1653 F3-FU-4 +#1674 G.0 wire-up).
 * Source: apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/
 * SearchDocumentChunks/SearchDocumentChunksByVectorQuery.cs
 */
export const DocChunkSearchHitSchema = z.object({
  chunkIndex: z.number().int().nonnegative(),
  pageNumber: z.number().int().nullable(),
  score: z.number(),
  snippet: z.string(),
});

export type DocChunkSearchHit = z.infer<typeof DocChunkSearchHitSchema>;

export const SearchDocumentChunksResultDtoSchema = z.object({
  results: z.array(DocChunkSearchHitSchema),
  errorMessage: z.string().nullable(),
});

export type SearchDocumentChunksResultDto = z.infer<typeof SearchDocumentChunksResultDtoSchema>;
