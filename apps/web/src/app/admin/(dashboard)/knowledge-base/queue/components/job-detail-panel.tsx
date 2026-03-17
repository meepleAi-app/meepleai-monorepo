'use client';

import { useCallback, useState } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { XCircleIcon, RefreshCwIcon, Trash2Icon, FileTextIcon, ClockIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Separator } from '@/components/ui/navigation/separator';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/useToast';

import { JobLogViewer } from './job-log-viewer';
import { JobStepTimeline } from './job-step-timeline';
import { cancelJob, retryJob, removeJob } from '../lib/queue-api';

import type { ProcessingJobDetailDto } from '../lib/queue-api';

interface JobDetailPanelProps {
  job: ProcessingJobDetailDto | null | undefined;
  isLoading: boolean;
}

function formatDateTime(dateStr: string | null): string {
  if (!dateStr) return '-';
  return new Date(dateStr).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });
}

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'Queued':
      return 'bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300';
    case 'Processing':
      return 'bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300';
    case 'Completed':
      return 'bg-emerald-100 text-emerald-900 dark:bg-emerald-900/30 dark:text-emerald-300';
    case 'Failed':
      return 'bg-red-100 text-red-900 dark:bg-red-900/30 dark:text-red-300';
    case 'Cancelled':
      return 'bg-slate-100 text-slate-900 dark:bg-zinc-700/50 dark:text-zinc-300';
    default:
      return '';
  }
}

export function JobDetailPanel({ job, isLoading }: JobDetailPanelProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [removeDialogOpen, setRemoveDialogOpen] = useState(false);

  const invalidateQueries = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
  }, [queryClient]);

  const cancelMutation = useMutation({
    mutationFn: (jobId: string) => cancelJob(jobId),
    onSuccess: () => {
      invalidateQueries();
      toast({ title: 'Job cancelled', description: 'The job has been cancelled.' });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to cancel job.', variant: 'destructive' });
    },
  });

  const retryMutation = useMutation({
    mutationFn: (jobId: string) => retryJob(jobId),
    onSuccess: () => {
      invalidateQueries();
      toast({ title: 'Job retried', description: 'The job has been re-queued.' });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to retry job.', variant: 'destructive' });
    },
  });

  const removeMutation = useMutation({
    mutationFn: (jobId: string) => removeJob(jobId),
    onSuccess: () => {
      invalidateQueries();
      setRemoveDialogOpen(false);
      toast({ title: 'Job removed', description: 'The job has been removed.' });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to remove job.', variant: 'destructive' });
    },
  });

  if (!job && !isLoading) {
    return (
      <div className="flex flex-col items-center justify-center h-full text-center p-8">
        <FileTextIcon className="h-12 w-12 text-muted-foreground/30 mb-3" />
        <p className="text-sm text-muted-foreground">Select a job to view details</p>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="p-6 space-y-4">
        <Skeleton className="h-8 w-3/4" />
        <Skeleton className="h-4 w-1/2" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-24 w-full" />
      </div>
    );
  }

  if (!job) return null;

  // Collect all log entries from all steps
  const allLogs = job.steps
    .flatMap(s => s.logEntries)
    .sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());

  const canCancel = job.status === 'Queued' || job.status === 'Processing';
  const canRetry = job.status === 'Failed' && job.canRetry;
  const canRemove = job.status === 'Failed' || job.status === 'Cancelled';

  return (
    <div className="p-6 space-y-5 overflow-y-auto h-full">
      {/* Header */}
      <div>
        <div className="flex items-start justify-between gap-3">
          <h2 className="font-quicksand text-lg font-bold text-foreground truncate">
            {job.pdfFileName}
          </h2>
          <Badge variant="outline" className={getStatusBadgeClass(job.status)}>
            {job.status}
          </Badge>
        </div>

        {/* Meta info */}
        <div className="flex flex-wrap gap-x-4 gap-y-1 mt-2 text-xs text-muted-foreground">
          <span className="flex items-center gap-1">
            <ClockIcon className="h-3 w-3" />
            Created: {formatDateTime(job.createdAt)}
          </span>
          {job.startedAt && <span>Started: {formatDateTime(job.startedAt)}</span>}
          {job.completedAt && <span>Done: {formatDateTime(job.completedAt)}</span>}
          <span>Priority: {job.priority}</span>
        </div>

        {job.errorMessage && (
          <div className="mt-3 p-3 bg-red-50/80 dark:bg-red-900/20 rounded-lg border border-red-200/50 dark:border-red-800/30">
            <p className="text-xs text-red-700 dark:text-red-300 font-mono">{job.errorMessage}</p>
          </div>
        )}
      </div>

      <Separator />

      {/* Step Timeline */}
      <div>
        <h3 className="text-sm font-semibold text-foreground mb-3">Processing Steps</h3>
        <JobStepTimeline steps={job.steps} />
      </div>

      <Separator />

      {/* Logs */}
      <div>
        <h3 className="text-sm font-semibold text-foreground mb-3">Logs ({allLogs.length})</h3>
        <JobLogViewer logs={allLogs} />
      </div>

      {/* Actions */}
      {(canCancel || canRetry || canRemove) && (
        <>
          <Separator />
          <div className="flex gap-2">
            {canCancel && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => cancelMutation.mutate(job.id)}
                disabled={cancelMutation.isPending}
              >
                <XCircleIcon className="h-4 w-4 mr-1" />
                Cancel
              </Button>
            )}
            {canRetry && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => retryMutation.mutate(job.id)}
                disabled={retryMutation.isPending}
              >
                <RefreshCwIcon className="h-4 w-4 mr-1" />
                Retry
              </Button>
            )}
            {canRemove && (
              <AlertDialog open={removeDialogOpen} onOpenChange={setRemoveDialogOpen}>
                <AlertDialogTrigger asChild>
                  <Button variant="destructive" size="sm" disabled={removeMutation.isPending}>
                    <Trash2Icon className="h-4 w-4 mr-1" />
                    Remove
                  </Button>
                </AlertDialogTrigger>
                <AlertDialogContent>
                  <AlertDialogHeader>
                    <AlertDialogTitle>Remove job?</AlertDialogTitle>
                    <AlertDialogDescription>
                      This will permanently remove &quot;{job.pdfFileName}&quot; from the queue.
                      This action cannot be undone.
                    </AlertDialogDescription>
                  </AlertDialogHeader>
                  <AlertDialogFooter>
                    <AlertDialogCancel>Cancel</AlertDialogCancel>
                    <AlertDialogAction onClick={() => removeMutation.mutate(job.id)}>
                      Remove
                    </AlertDialogAction>
                  </AlertDialogFooter>
                </AlertDialogContent>
              </AlertDialog>
            )}
          </div>
        </>
      )}
    </div>
  );
}
