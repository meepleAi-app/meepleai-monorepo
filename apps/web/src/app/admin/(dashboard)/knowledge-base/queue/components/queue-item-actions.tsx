'use client';

import { useState, useCallback } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  MoreHorizontalIcon,
  ArrowUpIcon,
  XCircleIcon,
  FileTextIcon,
  RefreshCwIcon,
} from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/use-toast';

import { ExtractedTextPreviewModal } from './extracted-text-preview-modal';
import { bumpPriority, cancelJob, retryJob } from '../lib/queue-api';

import type { ProcessingJobDto, ProcessingPriority } from '../lib/queue-api';

interface QueueItemActionsProps {
  job: ProcessingJobDto;
}

export function QueueItemActions({ job }: QueueItemActionsProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [textPreviewOpen, setTextPreviewOpen] = useState(false);

  const invalidate = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
  }, [queryClient]);

  const bumpMutation = useMutation({
    mutationFn: (priority: ProcessingPriority) => bumpPriority(job.id, priority),
    onSuccess: () => {
      invalidate();
      toast({ title: 'Priority updated' });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to bump priority.', variant: 'destructive' });
    },
  });

  const cancelMutation = useMutation({
    mutationFn: () => cancelJob(job.id),
    onSuccess: () => {
      invalidate();
      setCancelDialogOpen(false);
      toast({ title: 'Job cancelled' });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to cancel job.', variant: 'destructive' });
    },
  });

  const retryMutation = useMutation({
    mutationFn: () => retryJob(job.id),
    onSuccess: () => {
      invalidate();
      toast({ title: 'Job retried', description: 'Re-queued for processing.' });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Failed to retry job.', variant: 'destructive' });
    },
  });

  const canBump = job.status === 'Queued';
  const canCancel = job.status === 'Queued' || job.status === 'Processing';
  const canRetry = job.status === 'Failed' && job.canRetry;

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="icon"
            className="h-7 w-7 shrink-0"
            onClick={e => e.stopPropagation()}
          >
            <MoreHorizontalIcon className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" onClick={e => e.stopPropagation()}>
          {canBump && (
            <>
              <DropdownMenuItem onClick={() => bumpMutation.mutate('High')}>
                <ArrowUpIcon className="h-4 w-4 mr-2" />
                Bump to High
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => bumpMutation.mutate('Urgent')}>
                <ArrowUpIcon className="h-4 w-4 mr-2 text-red-500" />
                Bump to Urgent
              </DropdownMenuItem>
              <DropdownMenuSeparator />
            </>
          )}

          <DropdownMenuItem onClick={() => setTextPreviewOpen(true)}>
            <FileTextIcon className="h-4 w-4 mr-2" />
            Preview Text
          </DropdownMenuItem>

          {canRetry && (
            <DropdownMenuItem onClick={() => retryMutation.mutate()}>
              <RefreshCwIcon className="h-4 w-4 mr-2" />
              Retry
            </DropdownMenuItem>
          )}

          {canCancel && (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                onClick={() => setCancelDialogOpen(true)}
                className="text-red-600 dark:text-red-400"
              >
                <XCircleIcon className="h-4 w-4 mr-2" />
                Cancel
              </DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>

      {/* Cancel confirmation dialog */}
      <AlertDialog open={cancelDialogOpen} onOpenChange={setCancelDialogOpen}>
        <AlertDialogContent onClick={e => e.stopPropagation()}>
          <AlertDialogHeader>
            <AlertDialogTitle>Cancel job?</AlertDialogTitle>
            <AlertDialogDescription>
              This will cancel processing for &quot;{job.pdfFileName}&quot;.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep</AlertDialogCancel>
            <AlertDialogAction onClick={() => cancelMutation.mutate()}>
              Cancel Job
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Extracted text preview modal */}
      <ExtractedTextPreviewModal
        pdfDocumentId={job.pdfDocumentId}
        fileName={job.pdfFileName}
        open={textPreviewOpen}
        onOpenChange={setTextPreviewOpen}
      />
    </>
  );
}
