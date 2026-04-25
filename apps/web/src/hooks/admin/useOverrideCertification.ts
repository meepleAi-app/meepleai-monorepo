/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 35)
 *
 * Mutation hook: admin certification override on a mechanic analysis snapshot.
 * Wraps `api.admin.overrideCertification(analysisId, reason)`.
 *
 * Reason must be 20..500 characters per backend validation; callers enforce
 * the UX-level check (AlertDialog in admin UI, task 36+).
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';

import { mechanicValidationKeys } from './mechanicValidationKeys';

export interface UseOverrideCertificationVariables {
  analysisId: string;
  reason: string;
}

export function useOverrideCertification(): UseMutationResult<
  void,
  Error,
  UseOverrideCertificationVariables
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ analysisId, reason }: UseOverrideCertificationVariables) =>
      api.admin.overrideCertification(analysisId, reason),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.dashboard.all,
      });
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.mechanicAnalysis.detail(variables.analysisId),
      });

      toast.success('Certification overridden successfully');
    },
    onError: error => {
      toast.error(`Failed to override certification: ${error.message}`);
    },
  });
}
