/**
 * useRecentSessions
 *
 * Fetches the N most recent play records for display in the profile overview.
 * Uses api.playRecords.getHistory which returns PlayHistoryResponse
 * (records: PlayRecordSummary[], totalCount, page, pageSize, totalPages).
 */

import { useEffect, useState } from 'react';

import { api } from '@/lib/api';
import type { PlayRecordSummary } from '@/lib/api/schemas/play-records.schemas';

export type RecentSession = PlayRecordSummary;

export interface UseRecentSessionsResult {
  sessions: RecentSession[];
  isLoading: boolean;
  error: string | null;
}

export function useRecentSessions(limit: number): UseRecentSessionsResult {
  const [sessions, setSessions] = useState<RecentSession[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    api.playRecords
      .getHistory({ page: 1, pageSize: limit })
      .then(data => {
        if (!cancelled) setSessions(data.records ?? []);
      })
      .catch(err => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Errore nel caricamento');
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [limit]);

  return { sessions, isLoading, error };
}
