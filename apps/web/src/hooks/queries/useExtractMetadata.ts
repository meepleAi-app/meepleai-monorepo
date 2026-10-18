/**
 * useExtractMetadata — React Query hook for AI metadata extraction from PDF.
 * Calls GET /admin/shared-games/extract-metadata/{pdfId}.
 */

import { useQuery, type UseQueryOptions } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { WizardExtractedMetadata } from '@/lib/api/clients/sharedGamesClient';
import type { GameMetadata } from '@/stores/useGameImportWizardStore';

/** The backend's names (`year`, `playingTime`) onto the wizard's form fields (#4459). */
function mapToGameMetadata(dto: WizardExtractedMetadata): GameMetadata {
  return {
    title: dto.title ?? '',
    yearPublished: dto.year ?? undefined,
    description: dto.description ?? undefined,
    minPlayers: dto.minPlayers ?? undefined,
    maxPlayers: dto.maxPlayers ?? undefined,
    playingTimeMinutes: dto.playingTime ?? undefined,
    minAge: dto.minAge ?? undefined,
    publishers: dto.publishers ?? undefined,
    designers: dto.designers ?? undefined,
    categories: dto.categories ?? undefined,
    mechanics: dto.mechanics ?? undefined,
    confidenceScore: dto.confidenceScore ?? undefined,
  };
}

/**
 * True when the extraction found nothing. The backend answers 200 with every field null
 * (`GameMetadataDto.CreateEmpty()`) when the PDF has no readable text or the LLM gives no answer:
 * that is a result to fill in by hand, not an error (#4459).
 */
export function isEmptyExtraction(metadata: GameMetadata): boolean {
  const lists = [metadata.publishers, metadata.designers, metadata.categories, metadata.mechanics];
  return (
    !metadata.title.trim() &&
    !metadata.description?.trim() &&
    metadata.yearPublished === undefined &&
    metadata.minPlayers === undefined &&
    metadata.maxPlayers === undefined &&
    metadata.playingTimeMinutes === undefined &&
    metadata.minAge === undefined &&
    lists.every(list => !list?.length)
  );
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
