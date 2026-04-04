'use client';

import { useQuery, useMutation } from '@tanstack/react-query';

import { fetchJson, DB_SYNC_API } from '../lib/api';

import type { SchemaDiffResult, SyncResult, ApplyMigrationsRequest } from '../types/db-sync';

export function useSchemaCompare(enabled: boolean) {
  return useQuery<SchemaDiffResult>({
    queryKey: ['db-sync', 'schema-compare'],
    queryFn: () => fetchJson<SchemaDiffResult>(`${DB_SYNC_API}/schema/compare`),
    enabled,
    retry: 1,
  });
}

export function usePreviewSql() {
  return useMutation({
    mutationFn: () => fetchJson<string>(`${DB_SYNC_API}/schema/preview-sql`, { method: 'POST' }),
  });
}

export function useApplyMigrations() {
  return useMutation({
    mutationFn: (req: ApplyMigrationsRequest) =>
      fetchJson<SyncResult>(`${DB_SYNC_API}/schema/apply`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
      }),
  });
}
