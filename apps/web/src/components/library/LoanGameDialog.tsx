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

interface LoanGameDialogProps {
  gameId: string;
  gameTitle: string;
  open: boolean;
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

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
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
          <Button variant="outline" onClick={() => onOpenChange(false)}>
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
