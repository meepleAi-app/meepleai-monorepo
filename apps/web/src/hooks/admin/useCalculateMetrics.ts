/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 35)
 *
 * Mutation hook: compute + persist a metrics snapshot for a mechanic analysis.
 * Wraps `api.admin.calculateMetrics(analysisId)`.
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';
import type { CalculateMetricsResponse } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { mechanicValidationKeys } from './mechanicValidationKeys';

export function useCalculateMetrics(): UseMutationResult<CalculateMetricsResponse, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (analysisId: string) => api.admin.calculateMetrics(analysisId),
    onSuccess: (_data, analysisId) => {
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.dashboard.all,
      });
      // Forward-looking invalidation — the per-analysis detail query may not
      // exist yet in Sprint 1, but keeping the contract here avoids churn later.
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.mechanicAnalysis.detail(analysisId),
      });

      toast.success('Metrics calculated successfully');
    },
    onError: error => {
      toast.error(`Failed to calculate metrics: ${error.message}`);
    },
  });
}
