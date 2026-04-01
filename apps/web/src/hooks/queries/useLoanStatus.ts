/**
 * useLoanStatus - TanStack Query hooks for loan flow
 *
 * Provides query and mutations for marking games as on loan,
 * marking them as returned, and fetching current loan status.
 */

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { libraryKeys } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';

/**
 * Hook to fetch the loan status of a game in user's library
 *
 * @param gameId - Game UUID
 * @returns UseQueryResult with loan status or null
 */
export function useLoanStatus(gameId: string) {
  return useQuery({
    queryKey: libraryKeys.loanStatus(gameId),
    queryFn: () => api.library.getLoanStatus(gameId),
    staleTime: 30_000,
  });
}

/**
 * Hook to mark a game as on loan (state → InPrestito)
 *
 * Invalidates loan-status and library queries on success.
 *
 * @param gameId - Game UUID
 * @returns UseMutationResult for marking game as on loan
 */
export function useMarkAsOnLoan(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (borrowerInfo: string) =>
      api.library.updateGameState(gameId, { newState: 'InPrestito', stateNotes: borrowerInfo }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: libraryKeys.loanStatus(gameId) });
      queryClient.invalidateQueries({ queryKey: libraryKeys.all });
    },
  });
}

/**
 * Hook to mark a game as returned (state → Owned)
 *
 * Invalidates loan-status and library queries on success.
 *
 * @param gameId - Game UUID
 * @returns UseMutationResult for marking game as returned
 */
export function useMarkAsReturned(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.library.updateGameState(gameId, { newState: 'Owned', stateNotes: null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: libraryKeys.loanStatus(gameId) });
      queryClient.invalidateQueries({ queryKey: libraryKeys.all });
    },
  });
}
