/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 21).
 *
 * Query hook: poll the status of a `MechanicRecalcJob` aggregate. Drives the
 * `RecalcProgressDrawer` UI. Wraps `api.admin.getRecalcJobStatus(jobId)`.
 *
 * Polling contract:
 *  - Disabled when `jobId` is null (no fetch fires).
 *  - Refetches every 2s while status ∈ {Pending, Running}.
 *  - Stops polling once the worker reports a terminal status
 *    (Completed / Failed / Cancelled) — `refetchInterval` returns `false`,
 *    which TanStack Query interprets as "stop the timer".
 *
 * Cooperative cancellation: the cancel endpoint just sets a flag on the
 * aggregate; the worker observes it on its next iteration and transitions
 * the job to a terminal status. The drawer reads `cancellationRequested`
 * from this hook to render the "Cancelling…" affordance in the meantime.
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { RecalcJobStatusDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { mechanicValidationKeys } from './mechanicValidationKeys';

const POLL_INTERVAL_MS = 2_000;

const TERMINAL_STATUSES: ReadonlySet<RecalcJobStatusDto['status']> = new Set([
  'Completed',
  'Failed',
  'Cancelled',
]);

export function useRecalcJobStatus(
  jobId: string | null
): UseQueryResult<RecalcJobStatusDto, Error> {
  return useQuery({
    queryKey: mechanicValidationKeys.recalcJob.byId(jobId ?? ''),
    queryFn: () => api.admin.getRecalcJobStatus(jobId as string),
    enabled: !!jobId,
    refetchInterval: query => {
      const status = query.state.data?.status;
      if (status && TERMINAL_STATUSES.has(status)) {
        return false;
      }
      return POLL_INTERVAL_MS;
    },
  });
}
