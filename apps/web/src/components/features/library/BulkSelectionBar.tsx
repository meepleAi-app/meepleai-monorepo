/**
 * BulkSelectionBar — Wave B.3 v2 component (Issue #574).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (BulkSelectionBar). Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md
 * §3.2 + AC-6.
 *
 * Mounted/unmounted by `LibraryHubV2` orchestrator based on
 * `selectionMode === 'select'` (mounted even at `selectedCount === 0`
 * to provide explicit Annulla affordance and avoid layout flash).
 *
 * Confirm flow uses Radix `<AlertDialog>` primitives (focus trap via
 * `<FocusScope>` automatic, role="alertdialog" automatic, Esc handling
 * automatic at dialog level — only catch Esc at bar level when dialog
 * is closed).
 *
 * Pure component (mirror Wave B.1 GamesEmptyState + B.2 EmptyAgents):
 *   labels resolved via prop — no `useTranslation` import. Parent
 *   (`LibraryHubV2`) owns i18n resolution and re-interpolates `counter`
 *   + `confirmTitle` per `selectedCount` change.
 *
 * Slide-in animation gated by `motion-safe:transition` Tailwind class
 * (collapse a 0.01ms sotto `prefers-reduced-motion: reduce`).
 */

'use client';

import { useState, type KeyboardEvent, type ReactElement } from 'react';

import clsx from 'clsx';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/overlays/alert-dialog-primitives';

export interface BulkSelectionBarLabels {
  readonly regionLabel: string;
  readonly counter: string;
  readonly cancel: string;
  readonly archive: string;
  readonly confirmTitle: string;
  readonly confirmDescription?: string;
  readonly confirmCta: string;
  readonly cancelCta: string;
}

export interface BulkSelectionBarProps {
  readonly selectedCount: number;
  readonly labels: BulkSelectionBarLabels;
  readonly onExitSelectMode: () => void;
  readonly onArchive: () => Promise<void>;
  readonly disabled?: boolean;
  readonly className?: string;
}

export function BulkSelectionBar({
  selectedCount: _selectedCount,
  labels,
  onExitSelectMode,
  onArchive,
  disabled = false,
  className,
}: BulkSelectionBarProps): ReactElement {
  const [isDialogOpen, setIsDialogOpen] = useState(false);

  const handleConfirm = async () => {
    await onArchive();
    setIsDialogOpen(false);
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Escape' && !isDialogOpen) {
      onExitSelectMode();
    }
  };

  return (
    <div
      data-slot="library-bulk-selection-bar"
      role="region"
      aria-label={labels.regionLabel}
      aria-live="polite"
      aria-atomic="true"
      onKeyDown={handleKeyDown}
      className={clsx(
        'fixed bottom-6 left-1/2 z-40 -translate-x-1/2',
        'flex items-center gap-3 rounded-full border border-border bg-card px-5 py-3 shadow-lg',
        'motion-safe:transition-transform motion-safe:duration-200 motion-safe:ease-out',
        className
      )}
    >
      <span
        data-slot="library-bulk-selection-counter"
        className="text-sm font-medium text-foreground"
      >
        {labels.counter}
      </span>

      <button
        type="button"
        onClick={onExitSelectMode}
        disabled={disabled}
        data-slot="library-bulk-selection-cancel"
        className={clsx(
          'inline-flex h-9 items-center justify-center rounded-full px-4 text-sm font-medium',
          'border border-border bg-background text-foreground',
          'hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          'disabled:cursor-not-allowed disabled:opacity-50'
        )}
      >
        {labels.cancel}
      </button>

      <AlertDialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
        <AlertDialogTrigger asChild>
          <button
            type="button"
            disabled={disabled}
            data-slot="library-bulk-selection-archive"
            className={clsx(
              'inline-flex h-9 items-center justify-center rounded-full px-4 text-sm font-medium',
              'bg-destructive text-destructive-foreground shadow-sm',
              'hover:bg-destructive/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              'disabled:cursor-not-allowed disabled:opacity-50'
            )}
          >
            {labels.archive}
          </button>
        </AlertDialogTrigger>
        <AlertDialogContent data-slot="library-bulk-selection-dialog">
          <AlertDialogHeader>
            <AlertDialogTitle>{labels.confirmTitle}</AlertDialogTitle>
            <AlertDialogDescription className={labels.confirmDescription ? undefined : 'sr-only'}>
              {labels.confirmDescription ?? labels.confirmTitle}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel data-slot="library-bulk-selection-dialog-cancel">
              {labels.cancelCta}
            </AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirm}
              data-slot="library-bulk-selection-dialog-confirm"
            >
              {labels.confirmCta}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
