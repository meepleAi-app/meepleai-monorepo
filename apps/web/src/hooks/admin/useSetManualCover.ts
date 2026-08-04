/**
 * Admin Cover Picker (Epic #3470 — Slice 3d).
 *
 * Mutation hook: set a manual (arbitrary-URL) cover for a game. Wraps
 * `api.admin.setManualCover`. Unlike `useAssignCover` there is NO optimistic update —
 * the server mints the DB key + presigned URL and materializes a `Manual` candidate — so
 * the hook simply invalidates the candidates query (+ the shared-game detail) on settle so
 * the new candidate appears in the picker grid and the public cover reflects the change.
 *
 * User-facing feedback is owned by the picker dialog, not this hook (toast-free pattern).
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  ManualCoverResult,
  SetManualCoverRequest,
} from '@/lib/api/schemas/admin/admin-cover.schemas';

import { coverEditorKeys } from './coverEditorKeys';

export interface UseSetManualCoverVariables {
  gameId: string;
  body: SetManualCoverRequest;
}

export function useSetManualCover(): UseMutationResult<
  ManualCoverResult,
  Error,
  UseSetManualCoverVariables
> {
  const queryClient = useQueryClient();

  return useMutation<ManualCoverResult, Error, UseSetManualCoverVariables>({
    mutationFn: ({ gameId, body }) => api.admin.setManualCover(gameId, body),
    onSettled: (_data, _error, { gameId }) => {
      queryClient.invalidateQueries({ queryKey: coverEditorKeys.candidates(gameId) });
      queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games', gameId] });
    },
  });
}
