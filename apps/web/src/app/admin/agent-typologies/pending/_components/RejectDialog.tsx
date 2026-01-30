'use client';

/**
 * Reject Dialog Component (Issue #3181)
 *
 * Dialog for rejecting typologies with required reason field.
 * Handles both single and bulk rejections.
 *
 * Part of Epic #3174 (AI Agent System).
 */

import { useState } from 'react';
import { XCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';

interface RejectDialogProps {
  isOpen: boolean;
  isBulk: boolean;
  typologyName?: string;
  selectedCount?: number;
  onConfirm: (reason: string) => void;
  onCancel: () => void;
}

export function RejectDialog({
  isOpen,
  isBulk,
  typologyName,
  selectedCount = 0,
  onConfirm,
  onCancel,
}: RejectDialogProps) {
  const [reason, setReason] = useState('');

  const title = isBulk
    ? `Reject ${selectedCount} Typologies`
    : `Reject Typology`;

  const description = isBulk
    ? `Please provide a reason for rejecting ${selectedCount} typologies. This will be shared with the proposers.`
    : `Please provide a reason for rejecting "${typologyName}". This will be shared with the proposer.`;

  const handleConfirm = () => {
    if (!reason.trim()) return;
    onConfirm(reason);
    setReason(''); // Reset for next use
  };

  const handleCancel = () => {
    setReason('');
    onCancel();
  };

  return (
    <Dialog open={isOpen} onOpenChange={handleCancel}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <XCircle className="h-5 w-5 text-destructive" />
            {title}
          </DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <div className="space-y-2">
          <Label htmlFor="rejection-reason">
            Rejection Reason <span className="text-destructive">*</span>
          </Label>
          <Textarea
            id="rejection-reason"
            placeholder="Explain why this typology is being rejected..."
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={4}
            className="resize-none"
          />
          <p className="text-xs text-muted-foreground">
            This reason will be visible to the proposer.
          </p>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleCancel}>
            Cancel
          </Button>
          <Button
            variant="destructive"
            onClick={handleConfirm}
            disabled={!reason.trim()}
          >
            <XCircle className="h-4 w-4 mr-2" />
            Reject
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
