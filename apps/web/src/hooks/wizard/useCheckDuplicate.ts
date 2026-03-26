/**
 * useCheckDuplicate - Duplicate Game Check Hook
 * Issue #4167: Duplicate warning modal
 *
 * Checks if a game already exists in SharedGameCatalog by BGG ID.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { BggDuplicateCheckResult } from '@/lib/api/schemas/shared-games.schemas';

export interface UseCheckDuplicateOptions {
  /** BGG ID to check for duplicates */
  bggId: number | null;
  /** Enable query (default: true if bggId is provided) */
  enabled?: boolean;
}

/**
 * Check if a game with the given BGG ID already exists
 *
 * @example
 * const { data, isLoading } = useCheckDuplicate({ bggId: 13 });
 * if (data?.isDuplicate) {
 *   // Show duplicate warning
 * }
 */
export function useCheckDuplicate({ bggId, enabled }: UseCheckDuplicateOptions) {
  const shouldEnable = enabled !== undefined ? enabled : bggId !== null;

  return useQuery<BggDuplicateCheckResult, Error>({
    queryKey: ['bgg', 'check-duplicate', bggId],
    queryFn: () => {
      if (!bggId) {
        throw new Error('ID richiesto');
      }
      return api.sharedGames.checkBggDuplicate(bggId);
    },
    enabled: shouldEnable && bggId !== null,
    staleTime: 60 * 1000, // 1 minute
    gcTime: 5 * 60 * 1000, // 5 minutes
  });
}
