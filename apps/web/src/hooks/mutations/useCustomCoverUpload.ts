/**
 * useCustomCoverUpload — L3 mutation hook for POST /api/v1/library/{gameId}/cover.
 *
 * Uploads a cropped WebP blob as a custom cover image for a library game.
 * Uses raw fetch (httpClient does not support multipart FormData — see gamesClient.ts).
 *
 * Cache invalidation:
 *   - libraryKeys.gameDetail(gameId) → game detail panel (cover shown there)
 *   - libraryKeys.all              → list views (cover thumbnail visible in grid)
 *
 * Issue #1824 (L3 Custom Cover Upload).
 */
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { libraryKeys } from '@/hooks/queries/useLibrary';
import { ApiError } from '@/lib/api/core/errors';

export interface CustomCoverUploadResult {
  coverR2Key: string;
  presignedUrl: string;
}

/**
 * Hook for uploading a custom cover blob for a game in the user's library.
 * Accepts a Blob (expected: WebP ≤500 KB after crop/compress) and POSTs it
 * as multipart/form-data to /api/v1/library/{gameId}/cover.
 *
 * @param gameId UUID of the library game entry
 */
export function useCustomCoverUpload(gameId: string) {
  const queryClient = useQueryClient();

  return useMutation<CustomCoverUploadResult, ApiError, Blob>({
    mutationFn: async (blob: Blob) => {
      const formData = new FormData();
      formData.append('file', blob, 'cover.webp');

      // Raw fetch required — httpClient.post() always sets Content-Type: application/json
      // and calls JSON.stringify(body), which breaks multipart.
      const response = await fetch(`/api/v1/library/${gameId}/cover`, {
        method: 'POST',
        body: formData,
        credentials: 'include',
        // Do NOT set Content-Type — browser sets multipart boundary automatically
      });

      if (!response.ok) {
        const errorBody = await response.json().catch(() => ({ error: 'Upload failed' }));
        throw new ApiError({
          message: errorBody.error || errorBody.message || 'Upload failed',
          statusCode: response.status,
          endpoint: `/api/v1/library/${gameId}/cover`,
        });
      }

      return response.json() as Promise<CustomCoverUploadResult>;
    },

    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameDetail(gameId) });
      queryClient.invalidateQueries({ queryKey: libraryKeys.all });
    },
  });
}
