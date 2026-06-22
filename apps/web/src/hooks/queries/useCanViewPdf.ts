/**
 * useCanViewPdf — verifica se l'utente corrente può visualizzare il PDF
 * di un documentId specifico (G4 v3 ownership gate).
 *
 * Logica:
 *   1. Se !enabled → no fetch, canView=false
 *   2. Se !gameId → impossibile decidere → canView=false (safe default)
 *   3. Altrimenti fetch getGameDocuments(gameId) e verifica se documentId è nella lista
 *
 * NB: Citation.isPublic è gestito a monte (shortcut in CitationPdfTab); questo
 * hook si attiva SOLO per il fallback per documenti non-public.
 *
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.4
 */
import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';

export interface UseCanViewPdfArgs {
  readonly documentId: string;
  readonly gameId?: string;
  readonly enabled?: boolean;
}

export interface UseCanViewPdfResult {
  readonly canView: boolean;
  readonly isLoading: boolean;
  readonly isError: boolean;
}

const DOCUMENTS_STALE_TIME_MS = 5 * 60 * 1000;

export function useCanViewPdf({
  documentId,
  gameId,
  enabled = true,
}: UseCanViewPdfArgs): UseCanViewPdfResult {
  const queryEnabled = enabled && !!gameId;

  const query = useQuery({
    queryKey: ['game-documents', gameId],
    queryFn: () => api.knowledgeBase.getGameDocuments(gameId!),
    enabled: queryEnabled,
    staleTime: DOCUMENTS_STALE_TIME_MS,
  });

  if (!queryEnabled) {
    return { canView: false, isLoading: false, isError: false };
  }

  return {
    canView: query.data?.some(d => d.id === documentId) ?? false,
    isLoading: query.isLoading,
    isError: query.isError,
  };
}
