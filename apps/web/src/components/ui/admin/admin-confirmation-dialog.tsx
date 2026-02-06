/**
 * AdminConfirmationDialog Component - Issue #3690
 *
 * Enhanced confirmation dialog for admin dashboard with two confirmation levels:
 * - Level 1: Warning modal with Cancel/Confirm buttons
 * - Level 2: Critical action requiring typing "CONFIRM" to proceed
 *
 * Features:
 * - Role-aware confirmation flows
 * - Audit log integration ready
 * - Keyboard accessible
 * - WCAG 2.1 AA compliant
 */

import React, { useState, useEffect } from 'react';

import { AlertTriangle, ShieldAlert } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

/**
 * Confirmation levels matching backend ConfirmationLevel enum
 */
export enum AdminConfirmationLevel {
  /** No confirmation required */
  None = 0,
  /** Warning modal with Cancel/Confirm */
  Level1 = 1,
  /** Critical action requiring typing CONFIRM */
  Level2 = 2,
}

export interface AdminConfirmationDialogProps {
  /** Whether the dialog is open */
  isOpen: boolean;

  /** Callback when dialog should close */
  onClose: () => void;

  /** Callback when user confirms */
  onConfirm: () => void | Promise<void>;

  /** Confirmation level (Level1 or Level2) */
  level: AdminConfirmationLevel;

  /** Dialog title */
  title: string;

  /** Dialog message/description */
  message: string;

  /** Optional warning details */
  warningMessage?: string;

  /** Confirm button text (default based on level) */
  confirmText?: string;

  /** Cancel button text (default: "Cancel") */
  cancelText?: string;

  /** Whether to show loading state on confirm button */
  isLoading?: boolean;
}

/**
 * AdminConfirmationDialog component with Level 1 and Level 2 support
 */
export function AdminConfirmationDialog({
  isOpen,
  onClose,
  onConfirm,
  level,
  title,
  message,
  warningMessage,
  confirmText,
  cancelText = 'Annulla',
  isLoading = false,
}: AdminConfirmationDialogProps) {
  const [typedConfirmation, setTypedConfirmation] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const isLevel2 = level === AdminConfirmationLevel.Level2;
  const isConfirmDisabled = isLevel2 && typedConfirmation !== 'CONFIRM';

  // Reset typed confirmation and submission state when dialog opens
  useEffect(() => {
    if (isOpen) {
      setTypedConfirmation('');
      setIsSubmitting(false);
    }
  }, [isOpen]);

  const handleConfirm = async () => {
    if (isConfirmDisabled || isSubmitting) return;
    setIsSubmitting(true);
    try {
      await onConfirm();
      onClose();
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !isLoading && !isConfirmDisabled && !isSubmitting) {
      e.preventDefault();
      handleConfirm();
    }
  };

  const Icon = isLevel2 ? ShieldAlert : AlertTriangle;
  const iconColor = isLevel2 ? 'text-red-600 dark:text-red-500' : 'text-yellow-600 dark:text-yellow-500';
  const defaultConfirmText = isLevel2 ? 'Conferma Azione Critica' : 'Conferma';

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent onKeyDown={handleKeyDown} className="sm:max-w-[500px]">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <div className={`flex-shrink-0 ${iconColor}`}>
              <Icon className="h-6 w-6" aria-hidden="true" />
            </div>
            <DialogTitle className={isLevel2 ? 'text-red-600 dark:text-red-500' : ''}>
              {isLevel2 && '🚨 '}
              {title}
            </DialogTitle>
          </div>
          <DialogDescription className="pt-3 space-y-2">
            <p className="text-base">{message}</p>
            {warningMessage && (
              <p className="text-sm text-yellow-600 dark:text-yellow-500 font-medium">
                ⚠️ {warningMessage}
              </p>
            )}
          </DialogDescription>
        </DialogHeader>

        {isLevel2 && (
          <div className="space-y-2 pt-2">
            <Label htmlFor="confirm-input" className="text-sm font-medium">
              Digita <span className="font-mono font-bold text-red-600 dark:text-red-500">CONFIRM</span> per procedere:
            </Label>
            <Input
              id="confirm-input"
              type="text"
              value={typedConfirmation}
              onChange={(e) => setTypedConfirmation(e.target.value)}
              placeholder="CONFIRM"
              className="font-mono"
              autoFocus
              disabled={isLoading || isSubmitting}
              aria-label="Type CONFIRM to proceed with critical action"
            />
            {typedConfirmation && typedConfirmation !== 'CONFIRM' && (
              <p className="text-xs text-red-600 dark:text-red-500">
                La parola deve corrispondere esattamente: CONFIRM
              </p>
            )}
          </div>
        )}

        <DialogFooter className="flex-row gap-2 sm:gap-2">
          <Button
            variant="outline"
            onClick={onClose}
            disabled={isLoading || isSubmitting}
            className="flex-1"
            autoFocus={!isLevel2}
          >
            {cancelText}
          </Button>
          <Button
            variant={isLevel2 ? 'destructive' : 'default'}
            onClick={handleConfirm}
            disabled={isLoading || isConfirmDisabled || isSubmitting}
            className="flex-1"
          >
            {isLoading || isSubmitting ? 'Elaborazione...' : confirmText || defaultConfirmText}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default AdminConfirmationDialog;
