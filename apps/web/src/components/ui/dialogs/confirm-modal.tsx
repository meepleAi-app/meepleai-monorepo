/**
 * ConfirmModal - Generic confirm/cancel dialog (Issue #1464).
 *
 * Pure presentational. Caller controls open state and i18n labels.
 * Wraps the shared Dialog primitives; the destructive variant uses the
 * Button's `destructive` styling for the confirm CTA.
 */

'use client';

import type { ReactElement } from 'react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

export interface ConfirmModalProps {
  /** Whether the dialog is open. */
  readonly open: boolean;
  /** Localized title (caller-side i18n). */
  readonly title: string;
  /** Localized message/body. */
  readonly message: string;
  /** Localized confirm CTA label. */
  readonly confirmLabel: string;
  /** Localized cancel CTA label. */
  readonly cancelLabel: string;
  /** When 'destructive', the confirm CTA renders in red (delete-like flows). */
  readonly variant?: 'default' | 'destructive';
  /** Invoked when user confirms. Caller is responsible for closing the dialog. */
  readonly onConfirm: () => void;
  /** Invoked when user cancels or the dialog requests close (Esc / overlay click). */
  readonly onCancel: () => void;
}

export function ConfirmModal(props: ConfirmModalProps): ReactElement {
  const {
    open,
    title,
    message,
    confirmLabel,
    cancelLabel,
    variant = 'default',
    onConfirm,
    onCancel,
  } = props;

  return (
    <Dialog open={open} onOpenChange={(isOpen: boolean) => !isOpen && onCancel()}>
      <DialogContent data-slot="confirm-modal" className="max-w-md">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{message}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={onCancel} data-slot="confirm-modal-cancel">
            {cancelLabel}
          </Button>
          <Button
            variant={variant === 'destructive' ? 'destructive' : 'default'}
            onClick={onConfirm}
            data-slot="confirm-modal-confirm"
          >
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
