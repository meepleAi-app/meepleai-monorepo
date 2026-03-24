/**
 * useExtractMetadata - React Query hook for AI metadata extraction
 * Issue #4163: Step 2 - Metadata Extraction
 *
 * Handles AI-powered metadata extraction from uploaded PDF with confidence scoring.
 */

import { useMutation, type UseMutationOptions } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { ExtractedMetadata } from '@/stores/useGameImportWizardStore';

export interface ExtractMetadataInput {
  /** Document ID from Step 1 upload */
  documentId: string;
}

export type UseExtractMetadataOptions = Omit<
  UseMutationOptions<ExtractedMetadata, Error, ExtractMetadataInput>,
  'mutationFn'
>;

/**
 * Hook for extracting game metadata from uploaded PDF using AI
 *
 * @param options - Mutation options
 * @returns Mutation result with extracted metadata
 *
 * @example
 * ```tsx
 * const { mutate, isPending } = useExtractMetadata();
 *
 * const handleExtract = () => {
 *   mutate({ documentId: uploadedPdf.id }, {
 *     onSuccess: (metadata) => {
 *       console.log('Extracted:', metadata.title);
 *       console.log('Confidence:', metadata.confidence);
 *     },
 *     onError: (error) => console.error('Failed:', error),
 *   });
 * };
 * ```
 */
export function useExtractMetadata(options: UseExtractMetadataOptions = {}) {
  return useMutation<ExtractedMetadata, Error, ExtractMetadataInput>({
    mutationFn: async ({ documentId }) => {
      // Call backend API to extract metadata
      const result = await api.admin.extractGameMetadata(documentId);

      // Map backend DTO to store interface (field names now match)
      const metadata: ExtractedMetadata = {
        title: result.title,
        year: result.year,
        minPlayers: result.minPlayers,
        maxPlayers: result.maxPlayers,
        playingTime: result.playingTime,
        minAge: result.minAge,
        description: result.description,
        confidenceScore: result.confidenceScore,
      };

      return metadata;
    },
    ...options,
  });
}
