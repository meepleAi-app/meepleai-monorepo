'use client';

import { useQuery, useMutation } from '@tanstack/react-query';

import { fetchJson, DB_SYNC_API } from '../lib/api';

import type { TableInfo, DataDiffResult, SyncResult, SyncTableDataRequest } from '../types/db-sync';

export function useTableList(enabled: boolean) {
  return useQuery<TableInfo[]>({
    queryKey: ['db-sync', 'tables'],
    queryFn: () => fetchJson<TableInfo[]>(`${DB_SYNC_API}/tables`),
    enabled,
    retry: 1,
  });
}

export function useTableCompare(tableName: string | null) {
  return useQuery<DataDiffResult>({
    queryKey: ['db-sync', 'table-compare', tableName],
    queryFn: () => fetchJson<DataDiffResult>(`${DB_SYNC_API}/tables/${tableName}/compare`),
    enabled: !!tableName,
    retry: 1,
  });
}

export function useSyncTable() {
  return useMutation({
    mutationFn: ({ tableName, ...req }: SyncTableDataRequest & { tableName: string }) =>
      fetchJson<SyncResult>(`${DB_SYNC_API}/tables/${tableName}/sync`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
      }),
  });
}
