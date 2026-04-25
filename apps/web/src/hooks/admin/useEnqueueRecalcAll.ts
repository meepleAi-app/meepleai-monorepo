/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 21).
 *
 * Mutation hook: enqueue an async mass-recalculation of mechanic-extractor
 * metrics across every Published analysis. Wraps `api.admin.enqueueRecalcAll()`
 * (POST → 202 Accepted with `{ jobId }`); the actual work runs in
 * `MechanicRecalcBackgroundService`.
 *
 * Toast policy:
 *  - No success toast on enqueue. The drawer (`RecalcProgressDrawer`) owns
 *    Completed / Failed / Cancelled toasts to avoid double-notifying.
 *  - Error toast on failure so the dashboard never sees the raw exception.
 */

'use client';

import { useMutation, type UseMutationResult } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';
import type { EnqueueRecalcAllResponse } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

export function useEnqueueRecalcAll(): UseMutationResult<EnqueueRecalcAllResponse, Error, void> {
  return useMutation({
    mutationFn: () => api.admin.enqueueRecalcAll(),
    onError: error => {
      toast.error(`Failed to enqueue recalc: ${error.message}`);
    },
  });
}
