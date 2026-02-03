/**
 * Bulk Reject Dialog Component (Issue #2896)
 *
 * Confirmation dialog for bulk rejection of approval queue items.
 * Requires rejection reason (min 10 characters) per CLAUDE.md pattern.
 */

'use client';

import React, { useState } from 'react';

import { AlertCircle } from 'lucide-react';

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
import { cn } from '@/lib/utils';

export interface BulkRejectDialogProps {
  /**
   * Whether dialog is open
   */
  open: boolean;

  /**
   * Callback to change open state
   */
  onOpenChange: (open: boolean) => void;

  /**
   * Number of selected items to reject
   */
  selectedCount: number;

  /**
   * Callback when rejection is confirmed
   * @param reason Rejection reason (min 10 characters)
   */
  onConfirm: (reason: string) => void;

  /**
   * Whether rejection is in progress
   */
  isLoading?: boolean;
}

const MIN_REASON_LENGTH = 10;

/**
 * BulkRejectDialog Component
 *
 * Shows confirmation dialog before bulk rejection.
 * Enforces minimum 10 character reason per approval workflow standards.
 */
export function BulkRejectDialog({
  open,
  onOpenChange,
  selectedCount,
  onConfirm,
  isLoading = false,
}: BulkRejectDialogProps) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleConfirm = () => {
    // Validate reason length
    if (reason.trim().length < MIN_REASON_LENGTH) {
      setError(`Il motivo deve contenere almeno ${MIN_REASON_LENGTH} caratteri`);
      return;
    }

    setError(null);
    onConfirm(reason.trim());
  };

  const handleOpenChange = (newOpen: boolean) => {
    if (!newOpen && !isLoading) {
      // Reset state when closing
      setReason('');
      setError(null);
    }
    onOpenChange(newOpen);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-[500px]" data-testid="bulk-reject-dialog">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <AlertCircle className="h-5 w-5 text-red-500" />
            Conferma Rifiuto Multiplo
          </DialogTitle>
          <DialogDescription>
            Stai per rifiutare <strong>{selectedCount}</strong>{' '}
            {selectedCount === 1 ? 'gioco' : 'giochi'}. Questa azione non può essere annullata.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {/* Rejection Reason */}
          <div className="space-y-2">
            <Label htmlFor="rejection-reason">
              Motivo del rifiuto <span className="text-red-500">*</span>
            </Label>
            <Textarea
              id="rejection-reason"
              placeholder="Inserisci il motivo del rifiuto (minimo 10 caratteri)..."
              value={reason}
              onChange={(e) => {
                setReason(e.target.value);
                if (error) setError(null); // Clear error on input
              }}
              disabled={isLoading}
              className={cn(error ? 'border-red-500' : '')}
              rows={4}
              data-testid="bulk-reject-reason"
            />
            {error && (
              <p className="text-sm text-red-500" data-testid="bulk-reject-error">
                {error}
              </p>
            )}
            <p className="text-xs text-muted-foreground">
              {reason.length}/{MIN_REASON_LENGTH} caratteri minimi
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => handleOpenChange(false)}
            disabled={isLoading}
            data-testid="bulk-reject-cancel"
          >
            Annulla
          </Button>
          <Button
            variant="destructive"
            onClick={handleConfirm}
            disabled={isLoading || reason.trim().length < MIN_REASON_LENGTH}
            data-testid="bulk-reject-confirm"
          >
            {isLoading ? 'Rifiuto in corso...' : `Rifiuta ${selectedCount} Giochi`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

BulkRejectDialog.displayName = 'BulkRejectDialog';
