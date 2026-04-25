/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 35)
 *
 * Mutation hook: create a curator-authored golden claim for a shared game.
 * Wraps `api.admin.createClaim(request)`.
 *
 * The backend client signature already embeds `sharedGameId` in the request
 * body; we still accept it as a separate variable so the hook can invalidate
 * the per-game golden query on success.
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';
import type {
  CreateGoldenClaimRequest,
  CreateGoldenClaimResponse,
} from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { mechanicValidationKeys } from './mechanicValidationKeys';

export interface UseCreateGoldenClaimVariables {
  sharedGameId: string;
  request: CreateGoldenClaimRequest;
}

export function useCreateGoldenClaim(): UseMutationResult<
  CreateGoldenClaimResponse,
  Error,
  UseCreateGoldenClaimVariables
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ request }: UseCreateGoldenClaimVariables) => api.admin.createClaim(request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.golden.byGame(variables.sharedGameId),
      });
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.dashboard.all,
      });

      toast.success('Golden claim created successfully');
    },
    onError: error => {
      toast.error(`Failed to create golden claim: ${error.message}`);
    },
  });
}
