/**
 * useKbHub — consolidated React Query hooks for KB Hub route (Issue #1481).
 *
 * - useUserKbStatus(gameId): KB stats (documentCount, coverageLevel)
 * - useGamePdfs(gameId): PDF list for the game
 * - useReindexKb(gameId): trigger full re-index
 * - useRebuildRaptor(gameId): trigger RAPTOR rebuild (Pro tier only — backend enforces)
 * - useDeletePdf(gameId): delete a PDF (Owner/Admin endpoint)
 *
 * Cache keys isolated under `kbHub.*` namespace to avoid cross-feature
 * invalidation surprises. Mutations invalidate both status + pdfs lists
 * because re-index/delete change derived counts.
 */

'use client';

import {
  useMutation,
  useQuery,
  useQueryClient,
  type UseMutationResult,
  type UseQueryResult,
} from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { UserGameKbStatus } from '@/lib/api/schemas/knowledge-base.schemas';
import type { GamePdfDto } from '@/lib/api/schemas/pdf.schemas';

export const kbHubKeys = {
  all: ['kbHub'] as const,
  status: (gameId: string) => [...kbHubKeys.all, 'status', gameId] as const,
  pdfs: (gameId: string) => [...kbHubKeys.all, 'pdfs', gameId] as const,
};

/**
 * Fetch user-facing KB status (documentCount, coverageLevel, coverageScore).
 * Disabled when gameId is undefined.
 */
export function useUserKbStatus(
  gameId: string | undefined
): UseQueryResult<UserGameKbStatus | null> {
  return useQuery({
    queryKey: gameId ? kbHubKeys.status(gameId) : [...kbHubKeys.all, 'status', '__skip'],
    queryFn: () => api.knowledgeBase.getUserGameKbStatus(gameId as string),
    enabled: !!gameId,
    staleTime: 60_000,
  });
}

/**
 * Fetch indexed PDFs for the game.
 * Disabled when gameId is undefined.
 */
export function useGamePdfs(gameId: string | undefined): UseQueryResult<GamePdfDto[]> {
  return useQuery({
    queryKey: gameId ? kbHubKeys.pdfs(gameId) : [...kbHubKeys.all, 'pdfs', '__skip'],
    queryFn: () => api.library.getGamePdfs(gameId as string),
    enabled: !!gameId,
    staleTime: 60_000,
  });
}

/**
 * Trigger full KB re-index. Invalidates status + pdfs on success
 * (chunk/embedding counts change post-reindex).
 */
export function useReindexKb(gameId: string): UseMutationResult<void, Error, void> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.knowledgeBase.reindexKb(gameId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: kbHubKeys.status(gameId) });
      qc.invalidateQueries({ queryKey: kbHubKeys.pdfs(gameId) });
    },
  });
}

/**
 * Trigger RAPTOR hierarchical clustering rebuild. Invalidates status only.
 * Backend enforces Pro tier — call may 403 for free users.
 */
export function useRebuildRaptor(gameId: string): UseMutationResult<void, Error, void> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.knowledgeBase.rebuildRaptor(gameId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: kbHubKeys.status(gameId) });
    },
  });
}

/**
 * Delete a PDF document. Invalidates pdfs + status on success.
 * The underlying endpoint is `DELETE /api/v1/pdf/{pdfId}` (Owner/Admin enforced server-side).
 */
export function useDeletePdf(gameId: string): UseMutationResult<void, Error, string> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (pdfId: string) => api.pdf.adminDeletePdf(pdfId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: kbHubKeys.pdfs(gameId) });
      qc.invalidateQueries({ queryKey: kbHubKeys.status(gameId) });
    },
  });
}
