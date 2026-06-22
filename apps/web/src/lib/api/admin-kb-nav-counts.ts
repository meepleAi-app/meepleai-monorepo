/**
 * Admin KB SubNav Counts API client (Issue #1655 Task 7)
 *
 * Fetches badge counts for the KbSubNav tabs:
 * - processingQueue: documents currently pending/processing
 * - feedback7d: RAG feedback items from the last 7 days
 *
 * Endpoint: GET /api/v1/admin/kb/nav-counts
 */

import { apiClient } from '@/lib/api/client';
import {
  KbNavCountsDtoSchema,
  type KbNavCountsDto,
} from '@/lib/api/schemas/admin-knowledge-base.schemas';

export type { KbNavCountsDto };

/**
 * GET /api/v1/admin/kb/nav-counts
 * Returns badge counts for the KB sub-navigation tabs.
 */
export async function fetchKbNavCounts(options?: {
  signal?: AbortSignal;
}): Promise<KbNavCountsDto | null> {
  return apiClient.get<KbNavCountsDto>('/api/v1/admin/kb/nav-counts', KbNavCountsDtoSchema, {
    signal: options?.signal,
  });
}
