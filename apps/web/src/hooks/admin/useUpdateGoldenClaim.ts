/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 35)
 *
 * Mutation hook: update an existing golden claim (statement / expectedPage /
 * sourceQuote; section is immutable). Wraps `api.admin.updateClaim(id, request)`.
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';
import type { UpdateGoldenClaimRequest } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { mechanicValidationKeys } from './mechanicValidationKeys';

export interface UseUpdateGoldenClaimVariables {
  sharedGameId: string;
  claimId: string;
  request: UpdateGoldenClaimRequest;
}

export function useUpdateGoldenClaim(): UseMutationResult<
  void,
  Error,
  UseUpdateGoldenClaimVariables
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ claimId, request }: UseUpdateGoldenClaimVariables) =>
      api.admin.updateClaim(claimId, request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.golden.byGame(variables.sharedGameId),
      });

      toast.success('Golden claim updated successfully');
    },
    onError: error => {
      toast.error(`Failed to update golden claim: ${error.message}`);
    },
  });
}
