'use client';
import { useEffect, useRef } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';

import { useDocumentVisibility } from './use-document-visibility';
import {
  fetchCatalogSyncStatus,
  type CatalogSyncStatusResponse,
} from '../lib/catalog-ingestion-api';

const CATALOG_SYNC_STATUS_KEY = ['catalog-sync-status'] as const;
export const CATALOG_SYNC_RUNS_KEY = ['catalog-sync-runs'] as const;

export function useCatalogSyncStatus() {
  const isVisible = useDocumentVisibility();
  const queryClient = useQueryClient();
  const previousStatusRef = useRef<string | null>(null);

  const query = useQuery<CatalogSyncStatusResponse>({
    queryKey: CATALOG_SYNC_STATUS_KEY,
    queryFn: fetchCatalogSyncStatus,
    refetchInterval: q => (isVisible && q.state.data?.status === 'running' ? 5000 : false),
    refetchIntervalInBackground: false,
  });

  useEffect(() => {
    const current = query.data?.status ?? null;
    if (previousStatusRef.current === 'running' && current === 'idle') {
      queryClient.invalidateQueries({ queryKey: CATALOG_SYNC_RUNS_KEY });
    }
    previousStatusRef.current = current;
  }, [query.data?.status, queryClient]);

  return query;
}
