/**
 * useIndexerVersions — TanStack Query hook for the indexer version registry.
 * Issue #1673.
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { IndexerVersionList } from '@/lib/api/schemas/indexer-versions.schemas';

export const indexerVersionsKeys = {
  all: ['admin', 'indexer', 'versions'] as const,
};

/**
 * Fetch the selectable indexer versions for the reindex dropdown.
 * The registry is static within a deploy, so we cache for 1 hour and disable refetch on focus.
 *
 * @remarks
 * `pdfClient.getIndexerVersions()` returns `IndexerVersionList | null` because the
 * underlying httpClient.get can resolve to null on 401. Consumers should use
 * `data ?? []` when iterating.
 */
export function useIndexerVersions(): UseQueryResult<IndexerVersionList | null, Error> {
  return useQuery({
    queryKey: indexerVersionsKeys.all,
    queryFn: () => api.pdf.getIndexerVersions(),
    staleTime: 60 * 60 * 1000,
    refetchOnWindowFocus: false,
  });
}
