/**
 * usePdfProcessingStatus - TanStack Query hook for PDF indexing/processing status
 *
 * Issue #4946: Show PDF indexing progress in wizard and on game card.
 *
 * Polls the backend every 3 seconds and stops automatically when status
 * reaches a terminal state (indexed | failed).
 */

import { useQuery, UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { PdfIndexingStatus } from '@/lib/api/schemas/private-games.schemas';

export const pdfStatusKeys = {
  all: ['pdf-status'] as const,
  byGame: (gameId: string) => [...pdfStatusKeys.all, gameId] as const,
};

const TERMINAL_STATUSES = new Set<PdfIndexingStatus['status']>(['indexed', 'failed']);

/**
 * Hook to poll PDF indexing status for a game.
 *
 * @param gameId - UUID of the private game (leave empty/undefined to disable)
 * @returns UseQueryResult with PdfIndexingStatus data
 */
export function usePdfProcessingStatus(
  gameId: string | null | undefined
): UseQueryResult<PdfIndexingStatus, Error> {
  return useQuery({
    queryKey: pdfStatusKeys.byGame(gameId ?? ''),
    queryFn: () => api.library.getPdfProcessingStatus(gameId!),
    enabled: !!gameId,
    // Poll every 3 s; stop when terminal status is reached
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (!status || TERMINAL_STATUSES.has(status)) return false;
      return 3000;
    },
    // Don't auto-refetch on window focus — rely on polling only
    refetchOnWindowFocus: false,
    // Short stale time so polling stays fresh
    staleTime: 0,
  });
}
