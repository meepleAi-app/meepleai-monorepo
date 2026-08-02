/**
 * Admin Cover Picker (Epic #3470 — Slice 1d-c).
 *
 * Mutation hook: pin a cover source (+ focal point) for a UI context. Optimistically
 * updates the cached per-context assignment, rolls back on error, and invalidates the
 * candidates query + the shared-game detail on settle. Wraps `api.admin.assignCover`.
 *
 * User-facing feedback is owned by the picker dialog (PR2), not this hook — matching
 * the toast-free optimistic pattern of `useUpdateTierStrategyAccess`.
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  AssignCoverRequest,
  CoverAssignment,
  CoverCandidates,
  CoverContext,
} from '@/lib/api/schemas/admin/admin-cover.schemas';

import { coverEditorKeys } from './coverEditorKeys';

export interface UseAssignCoverVariables {
  gameId: string;
  context: CoverContext;
  body: AssignCoverRequest;
}

interface AssignCoverMutationContext {
  previous?: CoverCandidates;
}

/** Maps a `CoverContext` ("Card"/"Hero"/"Social") to its `assignments` object key. */
function assignmentKey(context: CoverContext): keyof CoverCandidates['assignments'] {
  return context.toLowerCase() as keyof CoverCandidates['assignments'];
}

export function useAssignCover(): UseMutationResult<
  CoverAssignment,
  Error,
  UseAssignCoverVariables,
  AssignCoverMutationContext
> {
  const queryClient = useQueryClient();

  return useMutation<CoverAssignment, Error, UseAssignCoverVariables, AssignCoverMutationContext>({
    mutationFn: ({ gameId, context, body }) => api.admin.assignCover(gameId, context, body),
    onMutate: async ({ gameId, context, body }) => {
      const key = coverEditorKeys.candidates(gameId);
      await queryClient.cancelQueries({ queryKey: key });
      const previous = queryClient.getQueryData<CoverCandidates>(key);
      if (previous) {
        queryClient.setQueryData<CoverCandidates>(key, {
          ...previous,
          assignments: { ...previous.assignments, [assignmentKey(context)]: body.source },
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
