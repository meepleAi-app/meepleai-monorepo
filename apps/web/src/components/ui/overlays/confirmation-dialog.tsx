/**
 * ConfirmationDialog Component - Issue #903 Enhancement
 *
 * Reusable confirmation dialog for destructive actions.
 * Replaces native window.confirm() with consistent UI.
 *
 * Features:
 * - Customizable title, message, and buttons
 * - Variant support (default, destructive, warning)
 * - Keyboard accessible (ESC to cancel, Enter to confirm)
 * - WCAG 2.1 AA compliant
 */

import React from 'react';

import { AlertTriangle, AlertCircle, Info } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';

export interface ConfirmationDialogProps {
  /** Whether the dialog is open */
  isOpen: boolean;

  /** Callback when dialog should close */
  onClose: () => void;

  /** Callback when user confirms */
  onConfirm: () => void;

  /** Dialog title */
  title: string;

  /** Dialog message/description */
  message: string;

  /** Confirm button text (default: "Confirm") */
  confirmText?: string;

  /** Cancel button text (default: "Cancel") */
  cancelText?: string;

  /** Variant (affects icon and button color) */
  variant?: 'default' | 'destructive' | 'warning';

  /** Whether to show loading state on confirm button */
  isLoading?: boolean;
}

/**
 * ConfirmationDialog component
 */
export function ConfirmationDialog({
  isOpen,
  onClose,
  onConfirm,
  title,
  message,
  confirmText = 'Confirm',
  cancelText = 'Cancel',
  variant = 'default',
  isLoading = false,
}: ConfirmationDialogProps) {
  const handleConfirm = () => {
    onConfirm();
    onClose();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !isLoading) {
      e.preventDefault();
      handleConfirm();
    }
  };

  const Icon =
    variant === 'destructive' ? AlertTriangle : variant === 'warning' ? AlertCircle : Info;
  const iconColor =
    variant === 'destructive'
      ? 'text-red-600'
      : variant === 'warning'
        ? 'text-yellow-600'
        : 'text-blue-600';

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent onKeyDown={handleKeyDown} className="sm:max-w-[425px]">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <div className={`flex-shrink-0 ${iconColor}`}>
              <Icon className="h-6 w-6" aria-hidden="true" />
            </div>
            <DialogTitle>{title}</DialogTitle>
          </div>
          <DialogDescription className="pt-2">{message}</DialogDescription>
        </DialogHeader>
        <DialogFooter className="flex-row gap-2 sm:gap-2">
          <Button
            variant="outline"
            onClick={onClose}
            disabled={isLoading}
            className="flex-1"
            autoFocus={variant === 'default'}
          >
            {cancelText}
          </Button>
          <Button
            variant={variant === 'destructive' ? 'destructive' : 'default'}
            onClick={handleConfirm}
            disabled={isLoading}
            className="flex-1"
            autoFocus={variant !== 'default'}
          >
            {isLoading ? 'Processing...' : confirmText}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default ConfirmationDialog;
