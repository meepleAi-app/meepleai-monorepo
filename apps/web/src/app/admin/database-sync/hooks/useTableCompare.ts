'use client';

import { useQuery, useMutation } from '@tanstack/react-query';

import type { TableInfo, DataDiffResult, SyncResult, SyncTableDataRequest } from '../types/db-sync';

const BASE = '/api/v1/admin/database-sync';

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { ...init, credentials: 'include' });
  if (!res.ok) throw new Error(`${res.status}: ${await res.text()}`);
  return res.json() as Promise<T>;
}

export function useTableList(enabled: boolean) {
  return useQuery<TableInfo[]>({
    queryKey: ['db-sync', 'tables'],
    queryFn: () => fetchJson<TableInfo[]>(`${BASE}/tables`),
    enabled,
    retry: 1,
  });
}

export function useTableCompare(tableName: string | null) {
  return useQuery<DataDiffResult>({
    queryKey: ['db-sync', 'table-compare', tableName],
    queryFn: () => fetchJson<DataDiffResult>(`${BASE}/tables/${tableName}/compare`),
    enabled: !!tableName,
    retry: 1,
  });
}

export function useSyncTable() {
  return useMutation({
    mutationFn: ({ tableName, ...req }: SyncTableDataRequest & { tableName: string }) =>
      fetchJson<SyncResult>(`${BASE}/tables/${tableName}/sync`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
      }),
  });
}
