'use client';

import React from 'react';

import { Loader2Icon } from 'lucide-react';

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

interface BulkActionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Confirmation dialog title, e.g. "Approve all pending claims?". */
  title: string;
  /** Predicted number of claims the action will affect. */
  count: number;
  onConfirm: () => void;
  isPending: boolean;
}

/**
 * Confirmation modal for bulk claim actions (#526 ME-M1.4 AC-3).
 *
 * Mirrors {@link RejectClaimDialog} but is generic over the bulk action
 * (approve-pending / reject-long-quote / future additions), surfacing only
 * the predicted affected-claim count for the reviewer to confirm.
 */
export function BulkActionDialog({
  open,
  onOpenChange,
  title,
  count,
  onConfirm,
  isPending,
}: BulkActionDialogProps): React.JSX.Element {
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>
            This will affect <strong data-testid="bulk-action-count">{count}</strong> claim
            {count === 1 ? '' : 's'}. This action cannot be undone.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={e => {
              e.preventDefault();
              if (count === 0) return;
              onConfirm();
            }}
            disabled={count === 0 || isPending}
            data-testid="bulk-action-confirm"
          >
            {isPending ? <Loader2Icon className="mr-1 h-4 w-4 animate-spin" /> : null}
            Confirm
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
