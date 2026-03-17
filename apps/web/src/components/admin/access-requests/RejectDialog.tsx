/**
 * RejectDialog Component
 *
 * Dialog for rejecting an access request with an optional reason.
 */

'use client';

import { useState } from 'react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';

const MAX_REASON_LENGTH = 500;

export interface RejectDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (reason?: string) => Promise<void>;
  email?: string;
}

export function RejectDialog({ open, onOpenChange, onConfirm, email }: RejectDialogProps) {
  const [reason, setReason] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  function handleOpenChange(nextOpen: boolean) {
    if (!nextOpen && !isSubmitting) {
      setReason('');
    }
    onOpenChange(nextOpen);
  }

  async function handleConfirm() {
    setIsSubmitting(true);
    try {
      await onConfirm(reason.trim() || undefined);
      setReason('');
      onOpenChange(false);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Reject Access Request</DialogTitle>
          <DialogDescription>
            {email ? (
              <>
                Reject the access request from <strong>{email}</strong>.
              </>
            ) : (
              'Reject this access request.'
            )}{' '}
            You may optionally provide a reason.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-2">
          <Label htmlFor="reject-reason">
            Reason <span className="text-muted-foreground font-normal">(optional)</span>
          </Label>
          <Textarea
            id="reject-reason"
            placeholder="Enter rejection reason..."
            value={reason}
            onChange={e => setReason(e.target.value.slice(0, MAX_REASON_LENGTH))}
            disabled={isSubmitting}
            rows={3}
            className="resize-none"
          />
          <p className="text-xs text-muted-foreground text-right">
            {reason.length}/{MAX_REASON_LENGTH}
          </p>
        </div>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => handleOpenChange(false)}
            disabled={isSubmitting}
          >
            Cancel
          </Button>
          <Button
            type="button"
            variant="destructive"
            onClick={handleConfirm}
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Rejecting...' : 'Reject Request'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
