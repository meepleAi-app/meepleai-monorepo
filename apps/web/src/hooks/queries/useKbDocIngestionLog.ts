import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { fetchKbDocIngestionLog } from '@/lib/api/admin-kb-ingestion';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

const ACTIVE_POLL_INTERVAL_MS = 6000;
const ACTIVE_STATUSES = new Set(['Queued', 'Processing']);

export const kbDocIngestionLogKeys = {
  all: ['kb', 'doc', 'ingestion-log'] as const,
  byId: (docId: string) => [...kbDocIngestionLogKeys.all, docId] as const,
};

export interface UseKbDocIngestionLogOptions {
  readonly docId: string | null | undefined;
  readonly enabled?: boolean;
}

/**
 * TanStack Query hook for the KB ingestion log of a document.
 * - Refetches every 6s while the job status is Queued or Processing.
 * - Stops polling automatically when the job reaches a terminal state.
 * - Returns null when no job exists for the document.
 * Backend contract: GET /api/v1/admin/kb/docs/{docId}/ingestion-log.
 * Issue #1650.
 */
export function useKbDocIngestionLog(
  options: UseKbDocIngestionLogOptions
): UseQueryResult<IngestionLog | null, Error> {
  const { docId, enabled = true } = options;
  const isValid = typeof docId === 'string' && docId.length > 0;

  return useQuery<IngestionLog | null, Error>({
    queryKey: isValid ? kbDocIngestionLogKeys.byId(docId) : kbDocIngestionLogKeys.all,
    queryFn: async () => (isValid ? fetchKbDocIngestionLog(docId) : null),
    enabled: enabled && isValid,
    refetchInterval: query => {
      const status = query.state.data?.status;
      return status && ACTIVE_STATUSES.has(status) ? ACTIVE_POLL_INTERVAL_MS : false;
    },
    staleTime: 5_000,
  });
}
