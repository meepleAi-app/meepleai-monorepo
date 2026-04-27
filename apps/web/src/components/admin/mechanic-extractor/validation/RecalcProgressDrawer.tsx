/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 21).
 *
 * Progress drawer rendered by the dashboard whenever there's an active recalc
 * job. Mounts a polled status surface (`useRecalcJobStatus`) that:
 *  - Renders the progress fraction (`processed / total`), counters (failed,
 *    skipped, consecutive failures), ETA, last error tail, and last heartbeat.
 *  - Renders a "Cancel" button while the job is non-terminal. The button
 *    fires cooperative cancellation via `useCancelRecalcJob`. Once
 *    `cancellationRequested === true` the affordance flips to a disabled
 *    "Cancelling…" state so the operator can't double-fire (the call is
 *    idempotent server-side, but the UX should be coherent).
 *  - Fires terminal toasts exactly once per status transition:
 *      Completed → green `Recalculated N analyses`
 *      Failed    → red   `Recalc failed: ${lastError}`
 *      Cancelled → blue  `Recalc cancelled (N processed before stop)`
 *    Re-poll cycles that return the same terminal snapshot do NOT re-toast.
 *
 * Render contract:
 *  - When `jobId === null`, the drawer renders nothing (parent owns mount).
 *  - When `jobId` is set, the drawer renders inline (Sprint 2 ships an
 *    inline panel rather than a portal sheet — the dashboard reserves a
 *    dedicated slot for it; future sprints may swap to `Sheet`).
 */

'use client';

import { useEffect, useRef } from 'react';

import { Loader2Icon, XIcon } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';
import { Progress } from '@/components/ui/progress';
import { useCancelRecalcJob } from '@/hooks/admin/useCancelRecalcJob';
import { useRecalcJobStatus } from '@/hooks/admin/useRecalcJobStatus';
import type { RecalcJobStatusDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

export interface RecalcProgressDrawerProps {
  /** Active job id; null hides the drawer. */
  jobId: string | null;
  /** Called when the operator dismisses a terminal job (frees the slot). */
  onClose: () => void;
}

type TerminalStatus = Extract<RecalcJobStatusDto['status'], 'Completed' | 'Failed' | 'Cancelled'>;

const TERMINAL_STATUSES: ReadonlySet<RecalcJobStatusDto['status']> = new Set([
  'Completed',
  'Failed',
  'Cancelled',
]);

function isTerminal(status: RecalcJobStatusDto['status']): status is TerminalStatus {
  return TERMINAL_STATUSES.has(status);
}

function formatEta(seconds: number | null): string {
  if (seconds === null || seconds < 0) return '—';
  if (seconds < 60) return `${Math.round(seconds)}s`;
  const minutes = Math.floor(seconds / 60);
  const remainder = Math.round(seconds % 60);
  return remainder > 0 ? `${minutes}m ${remainder}s` : `${minutes}m`;
}

function progressPercent(processed: number, total: number): number {
  if (total <= 0) return 0;
  return Math.min(100, Math.round((processed / total) * 100));
}

export function RecalcProgressDrawer({ jobId, onClose }: RecalcProgressDrawerProps) {
  const { data, isLoading, error } = useRecalcJobStatus(jobId);
  const cancel = useCancelRecalcJob();

  // Track the last terminal status we've already toasted for to avoid
  // double-firing across re-renders (each successful poll re-renders with
  // the same data; only a *transition* should toast).
  const lastToastedTerminalRef = useRef<TerminalStatus | null>(null);

  useEffect(() => {
    if (!data) return;
    if (!isTerminal(data.status)) {
      // Reset the latch when a new job comes back to non-terminal — protects
      // against the (Sprint 3) case of the slot being reused for a fresh job.
      lastToastedTerminalRef.current = null;
      return;
    }
    if (lastToastedTerminalRef.current === data.status) return;
    lastToastedTerminalRef.current = data.status;

    if (data.status === 'Completed') {
      toast.success(`Recalculated ${data.processed} analyses`);
    } else if (data.status === 'Failed') {
      const reason = data.lastError ?? 'unknown error';
      toast.error(`Recalc failed: ${reason}`);
    } else if (data.status === 'Cancelled') {
      toast.info(`Recalc cancelled (${data.processed} processed before stop)`);
    }
  }, [data]);

  if (jobId === null) return null;

  return (
    <div
      data-testid="recalc-progress-drawer"
      className="rounded-md border border-amber-300 bg-amber-50/70 p-4 text-sm dark:border-amber-800/60 dark:bg-amber-950/20"
      role="region"
      aria-label="Recalculation progress"
    >
      <div className="flex items-start justify-between">
        <div>
          <h2 className="font-quicksand text-base font-semibold text-foreground">
            Mass recalculation
          </h2>
          <p className="text-xs text-muted-foreground">Job ID: {jobId}</p>
        </div>
        {data && isTerminal(data.status) && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={onClose}
            data-testid="recalc-progress-drawer-close"
            aria-label="Dismiss recalculation drawer"
          >
            <XIcon className="h-4 w-4" />
          </Button>
        )}
      </div>

      {isLoading && !data && (
        <p className="mt-3 flex items-center gap-2 text-muted-foreground">
          <Loader2Icon className="h-4 w-4 animate-spin" />
          Loading job status…
        </p>
      )}

      {error && !data && (
        <p className="mt-3 text-rose-700 dark:text-rose-300">
          Failed to load job status: {error.message}
        </p>
      )}

      {data && (
        <div className="mt-3 space-y-3">
          <div>
            <div className="flex items-baseline justify-between">
              <span className="font-medium">
                {data.processed} / {data.total}
              </span>
              <span className="text-xs uppercase tracking-wide text-muted-foreground">
                {data.status}
              </span>
            </div>
            <Progress
              value={progressPercent(data.processed, data.total)}
              className="mt-1"
              indicatorClassName="bg-amber-500"
            />
          </div>

          <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
            <div>
              <dt className="inline text-muted-foreground">failed: </dt>
              <dd className="inline font-medium">{data.failed}</dd>
            </div>
            <div>
              <dt className="inline text-muted-foreground">skipped: </dt>
              <dd className="inline font-medium">{data.skipped}</dd>
            </div>
            <div>
              <dt className="inline text-muted-foreground">consecutive failures: </dt>
              <dd className="inline font-medium">{data.consecutiveFailures}</dd>
            </div>
            <div>
              <dt className="inline text-muted-foreground">ETA: </dt>
              <dd className="inline font-medium">{formatEta(data.etaSeconds)}</dd>
            </div>
          </dl>

          {data.lastError && (
            <p className="rounded bg-rose-50 p-2 text-xs text-rose-800 dark:bg-rose-950/30 dark:text-rose-300">
              Last error: {data.lastError}
            </p>
          )}

          {!isTerminal(data.status) &&
            (data.cancellationRequested ? (
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled
                data-testid="recalc-progress-drawer-cancelling"
              >
                <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />
                Cancelling…
              </Button>
            ) : (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => cancel.mutate(jobId)}
                disabled={cancel.isPending}
                data-testid="recalc-progress-drawer-cancel"
              >
                {cancel.isPending && <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />}
                Cancel
              </Button>
            ))}
        </div>
      )}
    </div>
  );
}
