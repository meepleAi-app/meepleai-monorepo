/**
 * useLibraryDowngrade - TanStack Query hook for downgrade preview
 *
 * Fetches the split of library entries (keep vs remove) for a quota downgrade.
 * Task 12: DowngradeTierModal
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { LibraryForDowngrade } from '@/lib/api/schemas/library.schemas';

/**
 * Hook to fetch library downgrade preview
 *
 * Returns two lists: games that fit within newQuota (gamesToKeep)
 * and games that would be removed (gamesToRemove).
 *
 * @param newQuota - The new library quota being considered
 * @param enabled - Whether to run the query (default: false)
 * @returns UseQueryResult with downgrade preview data
 */
export function useLibraryDowngradePreview(newQuota: number, enabled: boolean) {
  return useQuery<LibraryForDowngrade, Error>({
    queryKey: ['library', 'downgrade-preview', newQuota],
    queryFn: () => api.library.getLibraryForDowngrade(newQuota),
    enabled,
    staleTime: 0, // Always fresh — quota context can change
  });
}
