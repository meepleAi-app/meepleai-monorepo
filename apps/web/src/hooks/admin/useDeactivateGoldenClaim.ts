/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 35)
 *
 * Mutation hook: soft-delete (deactivate) a golden claim.
 * Wraps `api.admin.deactivateClaim(id)`.
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';

import { mechanicValidationKeys } from './mechanicValidationKeys';

export interface UseDeactivateGoldenClaimVariables {
  sharedGameId: string;
  claimId: string;
}

export function useDeactivateGoldenClaim(): UseMutationResult<
  void,
  Error,
  UseDeactivateGoldenClaimVariables
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ claimId }: UseDeactivateGoldenClaimVariables) =>
      api.admin.deactivateClaim(claimId),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.golden.byGame(variables.sharedGameId),
      });
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.dashboard.all,
      });

      toast.success('Golden claim deactivated successfully');
    },
    onError: error => {
      toast.error(`Failed to deactivate golden claim: ${error.message}`);
    },
  });
}
