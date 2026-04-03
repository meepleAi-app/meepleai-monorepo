import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';

/**
 * Query hook to fetch the downgrade preview: which games would be kept vs removed
 * when the user's library quota drops to `newQuota`.
 *
 * @param newQuota - The target quota after downgrade
 * @param enabled  - Whether to fetch (set to `open` state of the modal)
 */
export function useLibraryDowngradePreview(newQuota: number, enabled: boolean) {
  return useQuery({
    queryKey: ['library', 'downgrade-preview', newQuota],
    queryFn: () => api.library.getLibraryForDowngrade(newQuota),
    enabled,
    staleTime: 0, // Always fresh — library contents may have changed
  });
}

/**
 * Mutation hook to bulk-remove games from the library by their gameIds.
 * On success, invalidates all library-related queries.
 */
export function useBulkRemoveFromLibrary() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (gameIds: string[]) => api.library.bulkRemoveFromLibrary(gameIds),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['library'] });
    },
  });
}
