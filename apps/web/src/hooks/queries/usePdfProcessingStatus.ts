/**
 * usePdfProcessingStatus - TanStack Query hook for PDF indexing/processing status
 *
 * Issue #4946: Show PDF indexing progress in wizard and on game card.
 *
 * Polls the backend every 3 seconds and stops automatically when status
 * reaches a terminal state (indexed | failed).
 */

import { useRef } from 'react';

import { useQuery, UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { PdfIndexingStatus } from '@/lib/api/schemas/private-games.schemas';

export const pdfStatusKeys = {
  all: ['pdf-status'] as const,
  byGame: (gameId: string) => [...pdfStatusKeys.all, gameId] as const,
};

const TERMINAL_STATUSES = new Set<PdfIndexingStatus['status']>(['indexed', 'failed']);

/** Exported for testing — returns polling interval based on unchanged cycle count */
export function getAdaptiveInterval(cycleCount: number): number {
  if (cycleCount >= 20) return 10_000;
  if (cycleCount >= 10) return 5_000;
  return 3_000;
}

/**
 * Hook to poll PDF indexing status for a game.
 *
 * @param gameId - UUID of the private game (leave empty/undefined to disable)
 * @returns UseQueryResult with PdfIndexingStatus data
 */
export function usePdfProcessingStatus(
  gameId: string | null | undefined
): UseQueryResult<PdfIndexingStatus, Error> {
  const prevStatusRef = useRef<string | null>(null);
  const cycleCountRef = useRef(0);

  return useQuery({
    queryKey: pdfStatusKeys.byGame(gameId ?? ''),
    queryFn: () => api.library.getPdfProcessingStatus(gameId!),
    enabled: !!gameId,
    // Poll every 3 s; stop only when a terminal status is reached.
    // Keep polling on errors (e.g. transient 404 while the pipeline is starting up)
    // so the status appears once the backend catches up — do NOT stop on !status.
    refetchInterval: query => {
      const status = query.state.data?.status;
      if (status && TERMINAL_STATUSES.has(status)) return false;

      // Adaptive interval: slow down when status isn't changing
      if (status && status === prevStatusRef.current) {
        cycleCountRef.current += 1;
      } else {
        cycleCountRef.current = 0;
        prevStatusRef.current = status ?? null;
      }

      return getAdaptiveInterval(cycleCountRef.current);
    },
    // Don't auto-refetch on window focus — rely on polling only
    refetchOnWindowFocus: false,
    // Short stale time so polling stays fresh
    staleTime: 0,
    // Don't retry — polling every 3 s already handles transient failures.
    // Retrying on 404 ("no PDF yet") would generate redundant network requests.
    retry: false,
  });
}
