/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 21).
 *
 * Trigger button for the dashboard's mass-recalculation flow. On click it
 * fires `useEnqueueRecalcAll().mutate()` (no variables — actor id is sourced
 * from the session on the server). When the 202 Accepted comes back, the
 * `jobId` is forwarded to the parent via `onJobStarted` so the page can
 * mount `<RecalcProgressDrawer jobId={jobId} ... />`.
 *
 * Toasts are owned by the underlying hook (no success toast on enqueue;
 * the drawer owns terminal toasts). The button only forwards the jobId.
 *
 * The button stays disabled while the mutation is in flight to prevent a
 * double-enqueue race during the brief 202 round-trip.
 */

'use client';

import { Loader2Icon, RefreshCcwIcon } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useEnqueueRecalcAll } from '@/hooks/admin/useEnqueueRecalcAll';

export interface RecalcAllButtonProps {
  /**
   * Callback invoked with the new jobId once the enqueue 202 is parsed.
   * Parent should stash it in local state and conditionally render the
   * progress drawer.
   */
  onJobStarted?: (jobId: string) => void;
}

export function RecalcAllButton({ onJobStarted }: RecalcAllButtonProps) {
  const mutation = useEnqueueRecalcAll();

  const handleClick = () => {
    mutation.mutate(undefined, {
      onSuccess: response => {
        onJobStarted?.(response.jobId);
      },
    });
  };

  return (
    <Button
      type="button"
      onClick={handleClick}
      disabled={mutation.isPending}
      data-testid="recalc-all-button"
    >
      {mutation.isPending ? (
        <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />
      ) : (
        <RefreshCcwIcon className="mr-1 h-4 w-4" />
      )}
      {mutation.isPending ? 'Enqueueing…' : 'Recalculate all'}
    </Button>
  );
}
