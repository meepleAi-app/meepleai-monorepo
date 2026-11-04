/**
 * useDocumentsByGame - Fetch PDF documents for a game
 *
 * Issue #2051: Document source selection hook
 *
 * React Query hook for fetching all PDF documents associated with a game.
 * Used by DocumentSourceSelector to populate available sources.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';
import { mapProcessingStateToDisplayStatus } from '@/lib/kb/processing-status';

interface UseDocumentsByGameOptions {
  gameId: string | null;
  enabled?: boolean;
  /**
   * #4360: rilegge l'elenco a intervalli finché almeno un documento è in elaborazione, così lo stato
   * mostrato per ciascuno segue il backend. Si ferma da solo quando tutti sono in uno stato finale.
   */
  pollWhileProcessing?: boolean;
}

/** Ogni quanto si rilegge l'elenco mentre un documento è in elaborazione. */
const PROCESSING_POLL_MS = 3_000;

function hasDocumentInProgress(documents: PdfDocumentDto[] | undefined): boolean {
  return (
    documents?.some(
      doc => mapProcessingStateToDisplayStatus(doc.processingState) === 'processing'
    ) ?? false
  );
}

export function useDocumentsByGame({
  gameId,
  enabled = true,
  pollWhileProcessing = false,
}: UseDocumentsByGameOptions) {
  return useQuery<PdfDocumentDto[], Error>({
    queryKey: ['documents', 'by-game', gameId] as const,
    queryFn: async () => {
      if (!gameId) {
        throw new Error('Game ID is required');
      }
      return api.documents.getDocumentsByGame(gameId);
    },
    enabled: enabled && gameId !== null,
    staleTime: 30_000, // 30 seconds - documents don't change frequently
    gcTime: 5 * 60_000, // 5 minutes cache
    refetchInterval: query =>
      pollWhileProcessing && hasDocumentInProgress(query.state.data) ? PROCESSING_POLL_MS : false,
  });
}
