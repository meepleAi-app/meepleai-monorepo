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

interface UseDocumentsByGameOptions {
  gameId: string | null;
  enabled?: boolean;
}

export function useDocumentsByGame({ gameId, enabled = true }: UseDocumentsByGameOptions) {
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
  });
}
