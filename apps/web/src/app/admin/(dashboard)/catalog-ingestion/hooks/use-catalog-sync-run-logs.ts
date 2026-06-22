'use client';
import { useQuery } from '@tanstack/react-query';

import {
  fetchCatalogSyncRunLogs,
  type CatalogSyncRunLogsResponse,
} from '../lib/catalog-ingestion-api';

export function useCatalogSyncRunLogs(runId: string | null, tail = 100) {
  return useQuery<CatalogSyncRunLogsResponse | null>({
    queryKey: ['catalog-sync-run-logs', runId, tail],
    queryFn: () => fetchCatalogSyncRunLogs(runId!, tail),
    enabled: runId !== null,
  });
}
