/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 21).
 *
 * Mutation hook: request cooperative cancellation of a running recalc job.
 * Wraps `api.admin.cancelRecalcJob(jobId)`. The endpoint just sets the
 * `cancellationRequested` flag on the aggregate; the worker observes it on
 * its next iteration and transitions the job to a terminal status.
 *
 * Toast policy:
 *  - `toast.info` (NOT `toast.success`) on enqueue. The cancellation is
 *    cooperative and async — calling it a "success" right away would mislead
 *    operators into thinking the worker has stopped. The drawer reports the
 *    actual terminal status when the worker honours the flag.
 *  - Error toast on failure (e.g., 409 if job already terminal).
 *
 * Side-effect: invalidates the recalc-job status query so the next poll
 * picks up `cancellationRequested=true` immediately for snappy UI feedback,
 * rather than waiting up to ~2s for the next poll tick.
 */

'use client';

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';

import { mechanicValidationKeys } from './mechanicValidationKeys';

export function useCancelRecalcJob(): UseMutationResult<void, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (jobId: string) => api.admin.cancelRecalcJob(jobId),
    onSuccess: (_data, jobId) => {
      queryClient.invalidateQueries({
        queryKey: mechanicValidationKeys.recalcJob.byId(jobId),
      });
      toast.info('Cancellation requested — worker will stop on its next iteration');
    },
    onError: error => {
      toast.error(`Failed to cancel recalc: ${error.message}`);
    },
  });
}
