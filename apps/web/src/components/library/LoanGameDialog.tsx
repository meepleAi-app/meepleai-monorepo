/**
 * LoanGameDialog Component
 *
 * Dialog for recording that a game has been loaned out.
 * Captures borrower info and transitions the game state to InPrestito.
 */

'use client';

import { useState } from 'react';

import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useMarkAsOnLoan } from '@/hooks/queries/useLoanStatus';

export interface LoanGameDialogProps {
  /** Game UUID */
  gameId: string;
  /** Game title for display */
  gameTitle: string;
  /** Dialog open state */
  open: boolean;
  /** Callback to change open state */
  onOpenChange: (open: boolean) => void;
}

export function LoanGameDialog({ gameId, gameTitle, open, onOpenChange }: LoanGameDialogProps) {
  const [borrowerInfo, setBorrowerInfo] = useState('');
  const { mutate: markAsOnLoan, isPending } = useMarkAsOnLoan(gameId);

  function handleConfirm() {
    if (!borrowerInfo.trim()) return;
    markAsOnLoan(borrowerInfo.trim(), {
      onSuccess: () => {
        onOpenChange(false);
        setBorrowerInfo('');
      },
    });
  }

  function handleClose(nextOpen: boolean) {
    if (!nextOpen) {
      setBorrowerInfo('');
    }
    onOpenChange(nextOpen);
  }

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[440px]">
        <DialogHeader>
          <DialogTitle>Presta &quot;{gameTitle}&quot;</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="space-y-2">
            <Label htmlFor="borrower-info">Prestato a</Label>
            <Input
              id="borrower-info"
              placeholder="Nome o contatto del debitore"
              value={borrowerInfo}
              onChange={e => setBorrowerInfo(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
            Annulla
          </Button>
          <Button onClick={handleConfirm} disabled={!borrowerInfo.trim() || isPending}>
            Conferma prestito
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
