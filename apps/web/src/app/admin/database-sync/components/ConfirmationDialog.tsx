'use client';

import { useState } from 'react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Input } from '@/components/ui/primitives/input';

interface ConfirmationDialogProps {
  title: string;
  description: string;
  expectedText: string;
  onConfirm: () => void;
  isLoading: boolean;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ConfirmationDialog({
  title,
  description,
  expectedText,
  onConfirm,
  isLoading,
  open,
  onOpenChange,
}: ConfirmationDialogProps) {
  const [inputValue, setInputValue] = useState('');
  const isMatch = inputValue === expectedText;

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) setInputValue('');
    onOpenChange(nextOpen);
  };

  const handleConfirm = () => {
    if (!isMatch) return;
    onConfirm();
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <div className="space-y-3 py-2">
          <p className="text-sm text-muted-foreground">
            Type{' '}
            <code className="rounded bg-muted px-1.5 py-0.5 font-mono text-xs font-semibold text-foreground">
              {expectedText}
            </code>{' '}
            to confirm.
          </p>
          <Input
            value={inputValue}
            onChange={e => setInputValue(e.target.value)}
            placeholder={expectedText}
            autoComplete="off"
            disabled={isLoading}
          />
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)} disabled={isLoading}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={handleConfirm} disabled={!isMatch || isLoading}>
            {isLoading ? 'Applying...' : 'Confirm'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
