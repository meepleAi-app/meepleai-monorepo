'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { RefreshCwIcon, ArchiveRestoreIcon } from 'lucide-react';

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
import { useToast } from '@/hooks/use-toast';

import { bulkReindexFailed } from '../lib/queue-api';

export function BulkActionsBar() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const reindexMutation = useMutation({
    mutationFn: bulkReindexFailed,
    onSuccess: data => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
      toast({
        title: 'Bulk reindex complete',
        description: `${data.enqueuedCount} re-queued, ${data.skippedCount} skipped.`,
      });
    },
    onError: () => {
      toast({ title: 'Error', description: 'Bulk reindex failed.', variant: 'destructive' });
    },
  });

  return (
    <div className="flex items-center gap-2">
      <AlertDialog>
        <AlertDialogTrigger asChild>
          <Button variant="outline" size="sm" disabled={reindexMutation.isPending}>
            <ArchiveRestoreIcon className="h-4 w-4 mr-1" />
            {reindexMutation.isPending ? 'Reindexing...' : 'Reindex All Failed'}
          </Button>
        </AlertDialogTrigger>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Reindex all failed documents?</AlertDialogTitle>
            <AlertDialogDescription>
              This will re-queue all failed documents at Low priority. Documents that have exceeded
              max retries or already have active jobs will be skipped.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={() => reindexMutation.mutate()}>
              <RefreshCwIcon className="h-4 w-4 mr-1" />
              Reindex
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
