/**
 * DeleteDialog — confirmation dialog for PDF deletion with cleanup preview.
 *
 * Pure presentational. Issue #1481.
 * Mapped from `admin-mockups/design_files/sp4-kb-hub.jsx` DeleteDialog.
 *
 * Custom Dialog (not ConfirmModal) because the mockup requires a cleanup list slot
 * inside the modal body. ConfirmModal's flat title+message contract is insufficient.
 * The destructive CTA mirrors ConfirmModal's destructive variant for visual parity.
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

export interface DeleteDialogCleanupItem {
  readonly key: string;
  readonly icon: string;
  readonly label: string;
}

export interface DeleteDialogLabels {
  readonly title: string;
  readonly subtitlePrefix: string; // e.g. "PDF:"
  readonly listHeader: string;
  readonly warning: string;
  readonly deleteCta: string;
  readonly cancelCta: string;
}

export interface DeleteDialogProps {
  readonly open: boolean;
  readonly pdfName: string;
  readonly labels: DeleteDialogLabels;
  readonly onConfirm: () => void;
  readonly onCancel: () => void;
  /** Optional cleanup metadata (P83 — hide entire block when undefined or empty). */
  readonly cleanupItems?: ReadonlyArray<DeleteDialogCleanupItem>;
}

export function DeleteDialog(props: DeleteDialogProps): ReactElement {
  const { open, pdfName, labels, onConfirm, onCancel, cleanupItems } = props;
  const hasCleanup = cleanupItems !== undefined && cleanupItems.length > 0;

  return (
    <Dialog open={open} onOpenChange={(isOpen: boolean) => !isOpen && onCancel()}>
      <DialogContent data-slot="kb-hub-delete-dialog" className="max-w-md">
        <DialogHeader>
          <DialogTitle className="text-destructive">{labels.title}</DialogTitle>
          <DialogDescription>
            {labels.subtitlePrefix} <span className="font-semibold">{pdfName}</span>
          </DialogDescription>
        </DialogHeader>

        {hasCleanup && (
          <div
            data-slot="kb-hub-delete-cleanup-list"
            className="rounded-md border border-destructive/15 bg-destructive/5 px-4 py-3"
          >
            <div className="mb-2 font-mono text-xs font-bold uppercase tracking-wider text-destructive">
              {labels.listHeader}
            </div>
            <ul className="space-y-1">
              {cleanupItems!.map(item => (
                <li
                  key={item.key}
                  className="flex items-center gap-2 border-b border-border/50 py-1 text-xs text-foreground last:border-b-0"
                >
                  <span aria-hidden="true">{item.icon}</span>
                  <span>{item.label}</span>
                </li>
              ))}
            </ul>
          </div>
        )}

        <div
          data-slot="kb-hub-delete-warning"
          className="flex items-center gap-1.5 text-xs font-bold text-destructive"
        >
          <span aria-hidden="true">⚠️</span>
          <span>{labels.warning}</span>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onCancel} data-slot="kb-hub-delete-cancel">
            {labels.cancelCta}
          </Button>
          <Button variant="destructive" onClick={onConfirm} data-slot="kb-hub-delete-confirm">
            {labels.deleteCta}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
