'use client';

/**
 * Approve Confirmation Dialog Component (Issue #3181)
 *
 * Simple confirmation dialog for approving typologies.
 * Handles both single and bulk approvals.
 *
 * Part of Epic #3174 (AI Agent System).
 */

import { CheckCircle } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

interface ApproveConfirmDialogProps {
  isOpen: boolean;
  isBulk: boolean;
  typologyName?: string;
  selectedCount?: number;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ApproveConfirmDialog({
  isOpen,
  isBulk,
  typologyName,
  selectedCount = 0,
  onConfirm,
  onCancel,
}: ApproveConfirmDialogProps) {
  const title = isBulk
    ? `Approve ${selectedCount} Typologies`
    : `Approve Typology`;

  const description = isBulk
    ? `Are you sure you want to approve ${selectedCount} typologies? They will be published and available for use.`
    : `Are you sure you want to approve "${typologyName}"? It will be published and available for use.`;

  return (
    <Dialog open={isOpen} onOpenChange={onCancel}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <CheckCircle className="h-5 w-5 text-green-600" />
            {title}
          </DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button
            variant="default"
            onClick={onConfirm}
            className="bg-green-600 hover:bg-green-700"
          >
            <CheckCircle className="h-4 w-4 mr-2" />
            Approve
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
