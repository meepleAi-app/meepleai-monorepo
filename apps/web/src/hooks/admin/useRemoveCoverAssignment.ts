/**
 * Admin Cover Picker (Epic #3470 — Slice 1d-c).
 *
 * Mutation hook: reset a UI context's cover to implicit precedence. Optimistically
 * clears the cached per-context assignment, rolls back on error, and invalidates the
 * candidates query + the shared-game detail on settle. Wraps `api.admin.removeCoverAssignment`.
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { CoverCandidates, CoverContext } from '@/lib/api/schemas/admin/admin-cover.schemas';

import { coverEditorKeys } from './coverEditorKeys';

export interface UseRemoveCoverAssignmentVariables {
  gameId: string;
  context: CoverContext;
}

interface RemoveCoverMutationContext {
  previous?: CoverCandidates;
}

/** Maps a `CoverContext` ("Card"/"Hero"/"Social") to its `assignments` object key. */
function assignmentKey(context: CoverContext): keyof CoverCandidates['assignments'] {
  return context.toLowerCase() as keyof CoverCandidates['assignments'];
}

export function useRemoveCoverAssignment(): UseMutationResult<
  void,
  Error,
  UseRemoveCoverAssignmentVariables,
  RemoveCoverMutationContext
> {
  const queryClient = useQueryClient();

  return useMutation<void, Error, UseRemoveCoverAssignmentVariables, RemoveCoverMutationContext>({
    mutationFn: ({ gameId, context }) => api.admin.removeCoverAssignment(gameId, context),
    onMutate: async ({ gameId, context }) => {
      const key = coverEditorKeys.candidates(gameId);
      await queryClient.cancelQueries({ queryKey: key });
      const previous = queryClient.getQueryData<CoverCandidates>(key);
      if (previous) {
        queryClient.setQueryData<CoverCandidates>(key, {
          ...previous,
          assignments: { ...previous.assignments, [assignmentKey(context)]: null },
        });
      }
      return { previous };
    },
    onError: (_error, { gameId }, mutationContext) => {
      if (mutationContext?.previous) {
        queryClient.setQueryData(coverEditorKeys.candidates(gameId), mutationContext.previous);
      }
    },
    onSettled: (_data, _error, { gameId }) => {
      queryClient.invalidateQueries({ queryKey: coverEditorKeys.candidates(gameId) });
      queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games', gameId] });
    },
  });
}
