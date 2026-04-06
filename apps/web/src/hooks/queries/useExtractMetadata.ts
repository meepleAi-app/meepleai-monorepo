/**
 * useExtractMetadata — React Query hook for AI metadata extraction from PDF.
 * Calls GET /admin/shared-games/extract-metadata/{pdfId}.
 */

import { useQuery, type UseQueryOptions } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { WizardExtractedMetadata } from '@/lib/api/clients/sharedGamesClient';
import type { GameMetadata } from '@/stores/useGameImportWizardStore';

function mapToGameMetadata(dto: WizardExtractedMetadata): GameMetadata {
  return {
    title: dto.title,
    yearPublished: dto.yearPublished ?? undefined,
    description: dto.description ?? undefined,
    minPlayers: dto.minPlayers ?? undefined,
    maxPlayers: dto.maxPlayers ?? undefined,
    playingTimeMinutes: dto.playingTimeMinutes ?? undefined,
    minAge: dto.minAge ?? undefined,
    publishers: dto.publishers ?? undefined,
    designers: dto.designers ?? undefined,
    categories: dto.categories ?? undefined,
    mechanics: dto.mechanics ?? undefined,
    confidenceScore: dto.confidenceScore ?? undefined,
  };
}

export function useExtractMetadata(
  pdfDocumentId: string | null | undefined,
  options?: Omit<UseQueryOptions<GameMetadata | null, Error>, 'queryKey' | 'queryFn'>
) {
  return useQuery<GameMetadata | null, Error>({
    queryKey: ['extractMetadata', pdfDocumentId],
    queryFn: async () => {
      if (!pdfDocumentId) return null;
      const dto = await api.sharedGames.extractMetadataByPdfId(pdfDocumentId);
      return dto ? mapToGameMetadata(dto) : null;
    },
    enabled: !!pdfDocumentId,
    staleTime: 5 * 60 * 1000,
    ...options,
  });
}
