/**
 * useRemoveCustomCover — L3 mutation hook for DELETE /api/v1/library/{gameId}/cover.
 *
 * Removes the custom cover image for a library game, reverting to the catalog cover.
 * Uses raw fetch consistent with useCustomCoverUpload.
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

/**
 * Hook for removing the custom cover image from a library game entry.
 * DELETEs /api/v1/library/{gameId}/cover. On success the backend clears
 * the R2 blob and sets CustomCoverR2Key = null on the library entry.
 *
 * @param gameId UUID of the library game entry
 */
export function useRemoveCustomCover(gameId: string) {
  const queryClient = useQueryClient();

  return useMutation<void, ApiError, void>({
    mutationFn: async () => {
      const response = await fetch(`/api/v1/library/${gameId}/cover`, {
        method: 'DELETE',
        credentials: 'include',
      });

      if (!response.ok) {
        const errorBody = await response.json().catch(() => ({ error: 'Remove failed' }));
        throw new ApiError({
          message: errorBody.error || errorBody.message || 'Remove failed',
          statusCode: response.status,
          endpoint: `/api/v1/library/${gameId}/cover`,
        });
      }

      // 204 No Content — nothing to return
    },

    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameDetail(gameId) });
      queryClient.invalidateQueries({ queryKey: libraryKeys.all });
    },
  });
}
