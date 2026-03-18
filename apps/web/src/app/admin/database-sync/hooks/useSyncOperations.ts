'use client';

import { useQuery } from '@tanstack/react-query';

import type { SyncHistoryEntry } from '../types/db-sync';

const BASE = '/api/v1/admin/database-sync';

export function useSyncHistory(limit = 50) {
  return useQuery<SyncHistoryEntry[]>({
    queryKey: ['db-sync', 'history', limit],
    queryFn: async () => {
      const res = await fetch(`${BASE}/operations/history?limit=${limit}`, {
        credentials: 'include',
      });
      if (!res.ok) throw new Error(`${res.status}`);
      return res.json() as Promise<SyncHistoryEntry[]>;
    },
    retry: 1,
  });
}
