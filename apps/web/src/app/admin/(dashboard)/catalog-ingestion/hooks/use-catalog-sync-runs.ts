'use client';
import { useInfiniteQuery } from '@tanstack/react-query';

import { CATALOG_SYNC_RUNS_KEY } from './use-catalog-sync-status';
import {
  fetchCatalogSyncRuns,
  type PagedCatalogSyncRunsResponse,
} from '../lib/catalog-ingestion-api';

/**
 * Issue #1881 — paginated infinite query for the SyncRunTimeline.
 *
 * Returns the standard useInfiniteQuery shape (`fetchNextPage`, `hasNextPage`,
 * `isFetchingNextPage`) plus a flattened `data.items` helper preserved for
 * callers that previously consumed `data.items` directly. `data.total` mirrors
 * the BE total so the header can still show "ultime N run".
 */
export function useCatalogSyncRuns(pageSize = 12) {
  const query = useInfiniteQuery<PagedCatalogSyncRunsResponse>({
    queryKey: [...CATALOG_SYNC_RUNS_KEY, pageSize],
    queryFn: ({ pageParam }) => fetchCatalogSyncRuns({ page: pageParam as number, pageSize }),
    initialPageParam: 1,
    getNextPageParam: lastPage => (lastPage.hasMore ? lastPage.page + 1 : undefined),
  });

  return query;
}
