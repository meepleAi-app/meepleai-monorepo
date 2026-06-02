/**
 * Indexer Versions Schemas (Issue #1673)
 *
 * Zod schemas for GET /api/v1/admin/indexer/versions.
 * Matches IndexerVersionDto from
 * apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/IndexerVersionDto.cs.
 */

import { z } from 'zod';

export const IndexerVersionSchema = z.object({
  version: z.string().min(1),
  displayName: z.string().min(1),
  isCurrent: z.boolean(),
});

export type IndexerVersion = z.infer<typeof IndexerVersionSchema>;

export const IndexerVersionListSchema = z.array(IndexerVersionSchema);
export type IndexerVersionList = z.infer<typeof IndexerVersionListSchema>;
