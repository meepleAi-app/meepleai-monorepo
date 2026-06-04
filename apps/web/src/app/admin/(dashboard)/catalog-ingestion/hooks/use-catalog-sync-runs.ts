'use client';
import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { CATALOG_SYNC_RUNS_KEY } from './use-catalog-sync-status';
import {
  fetchCatalogSyncRuns,
  type PagedCatalogSyncRunsResponse,
} from '../lib/catalog-ingestion-api';

export function useCatalogSyncRuns(page = 1, pageSize = 12) {
  return useQuery<PagedCatalogSyncRunsResponse>({
    queryKey: [...CATALOG_SYNC_RUNS_KEY, page, pageSize],
    queryFn: () => fetchCatalogSyncRuns({ page, pageSize }),
    placeholderData: keepPreviousData,
  });
}
