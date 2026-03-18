'use client';

import { useQuery, useMutation } from '@tanstack/react-query';

import type { SchemaDiffResult, SyncResult, ApplyMigrationsRequest } from '../types/db-sync';

const BASE = '/api/v1/admin/database-sync';

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { ...init, credentials: 'include' });
  if (!res.ok) throw new Error(`${res.status}: ${await res.text()}`);
  return res.json() as Promise<T>;
}

export function useSchemaCompare(enabled: boolean) {
  return useQuery<SchemaDiffResult>({
    queryKey: ['db-sync', 'schema-compare'],
    queryFn: () => fetchJson<SchemaDiffResult>(`${BASE}/schema/compare`),
    enabled,
    retry: 1,
  });
}

export function usePreviewSql() {
  return useMutation({
    mutationFn: () => fetchJson<string>(`${BASE}/schema/preview-sql`, { method: 'POST' }),
  });
}

export function useApplyMigrations() {
  return useMutation({
    mutationFn: (req: ApplyMigrationsRequest) =>
      fetchJson<SyncResult>(`${BASE}/schema/apply`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
      }),
  });
}
