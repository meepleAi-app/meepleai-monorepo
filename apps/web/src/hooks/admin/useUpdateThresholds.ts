/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 15)
 *
 * Mutation hook: replace the singleton `CertificationThresholds` value object
 * via `PUT /api/v1/admin/mechanic-extractor/thresholds`.
 *
 * The endpoint returns 204 No Content; the mutation resolves to `void`. On
 * success the dashboard + trend + thresholds query keys are invalidated so
 * already-rendered admin surfaces refetch with the new bounds.
 *
 * Server-side cache eviction (the `mechanic-validation-dashboard` and
 * `mechanic-validation-trend` HybridCache tags) is the responsibility of
 * `MechanicMetricsRecalculatedCacheInvalidationHandler` (Task 13) and the
 * upcoming threshold-update audit handler — this hook only invalidates the
 * client-side TanStack cache.
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';
import type { UpdateCertificationThresholdsRequest } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { mechanicValidationKeys } from './mechanicValidationKeys';

export function useUpdateThresholds(): UseMutationResult<
  void,
  Error,
  UpdateCertificationThresholdsRequest
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: UpdateCertificationThresholdsRequest) =>
      api.admin.updateThresholds(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: mechanicValidationKeys.thresholds.all });
      queryClient.invalidateQueries({ queryKey: mechanicValidationKeys.dashboard.all });
      queryClient.invalidateQueries({ queryKey: mechanicValidationKeys.trend.all });

      toast.success('Certification thresholds updated successfully');
    },
    onError: error => {
      toast.error(`Failed to update thresholds: ${error.message}`);
    },
  });
}
