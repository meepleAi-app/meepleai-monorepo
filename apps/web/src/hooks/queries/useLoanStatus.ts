/**
 * useLoanStatus - TanStack Query hooks for game loan management
 *
 * Library Improvements: Loan flow UI
 *
 * Provides hooks for querying loan status and mutating game loan state.
 */

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';

/**
 * Query key factory for loan status queries
 */
export const loanStatusKeys = {
  all: ['library', 'loan-status'] as const,
  byGame: (gameId: string) => [...loanStatusKeys.all, gameId] as const,
};

/**
 * Hook to fetch the current loan status for a game
 *
 * @param gameId - Game UUID
 * @returns UseQueryResult with loan status or null
 */
export function useLoanStatus(gameId: string) {
  return useQuery({
    queryKey: loanStatusKeys.byGame(gameId),
    queryFn: () => api.library.getLoanStatus(gameId),
    staleTime: 30_000,
    enabled: !!gameId,
  });
}

/**
 * Hook to mark a game as on loan (state → InPrestito)
 * stateNotes is used to store the borrower info
 *
 * @param gameId - Game UUID
 */
export function useMarkAsOnLoan(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (borrowerInfo: string) =>
      api.library.updateGameState(gameId, { newState: 'InPrestito', stateNotes: borrowerInfo }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: loanStatusKeys.byGame(gameId) });
      queryClient.invalidateQueries({ queryKey: ['library'] });
    },
  });
}

/**
 * Hook to mark a game as returned (state → Owned)
 *
 * @param gameId - Game UUID
 */
export function useMarkAsReturned(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.library.updateGameState(gameId, { newState: 'Owned', stateNotes: null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: loanStatusKeys.byGame(gameId) });
      queryClient.invalidateQueries({ queryKey: ['library'] });
    },
  });
}
