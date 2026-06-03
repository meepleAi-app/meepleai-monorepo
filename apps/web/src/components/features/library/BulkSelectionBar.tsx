/**
 * BulkSelectionBar — Wave B.3 v2 component (Issue #574).
 *
 * SP4 mockup conformance (Issue #1585-followup, plan
 * docs/superpowers/plans/2026-06-03-library-sp4-mockup-conformance.md Task 3.2).
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (BulkSelectionBar, lines 895-944): dark floating bar with entity-game count
 * chip, 3 action buttons (Archivia / Tag / Esporta), close ✕ affordance.
 *
 * Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md
 * §3.2 + AC-6.
 *
 * Mounted/unmounted by `LibraryHub` orchestrator based on
 * `selectionMode === 'select'` (mounted even at `selectedCount === 0`
 * to provide explicit close affordance and avoid layout flash).
 *
 * Confirm flow uses Radix `<AlertDialog>` primitives (focus trap via
 * `<FocusScope>` automatic, role="alertdialog" automatic, Esc handling
 * automatic at dialog level — only catch Esc at bar level when dialog
 * is closed).
 *
 * Pure component (mirror Wave B.1 GamesEmptyState + B.2 EmptyAgents):
 *   labels resolved via prop — no `useTranslation` import. Parent
 *   (`LibraryHub`) owns i18n resolution and re-interpolates `regionLabel`
 *   + `confirmTitle` per `selectedCount` change.
 *
 * Slide-in animation gated by `motion-safe:` Tailwind classes
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
  readonly counterCompact?: string;
  readonly archive: string;
  readonly tag: string;
  readonly exportLabel: string;
  readonly closeAriaLabel: string;
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
  readonly onTag?: () => void;
  readonly onExport?: () => void;
  readonly compact?: boolean;
  readonly disabled?: boolean;
  readonly className?: string;
}

const ACTION_BUTTON_CLASS = clsx(
  'inline-flex flex-shrink-0 items-center gap-1.5 rounded-md border-0',
  'bg-white/[0.12] text-inherit font-display text-[11.5px] font-bold',
  'hover:bg-white/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60',
  'disabled:cursor-not-allowed disabled:opacity-50',
  'motion-safe:transition-colors motion-safe:duration-150'
);

export function BulkSelectionBar({
  selectedCount,
  labels,
  onExitSelectMode,
  onArchive,
  onTag,
  onExport,
  compact = false,
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

  const counterText = compact && labels.counterCompact ? labels.counterCompact : labels.counter;

  const actionPadding = compact ? 'px-2 py-1.5' : 'px-2.5 py-1.5';

  return (
    <div
      data-slot="library-bulk-selection-bar"
      role="region"
      aria-label={labels.regionLabel}
      aria-live="polite"
      aria-atomic="true"
      onKeyDown={handleKeyDown}
      className={clsx(
        'fixed bottom-4 left-1/2 z-40 -translate-x-1/2',
        'flex items-center gap-2.5',
        'max-w-[720px] rounded-2xl bg-foreground px-3.5 py-2.5 text-background',
        'shadow-[0_12px_32px_rgba(0,0,0,0.3)]',
        'motion-safe:animate-in motion-safe:slide-in-from-bottom-4 motion-safe:duration-200 motion-safe:ease-out',
        className
      )}
    >
      <span
        data-slot="library-bulk-selection-count-chip"
        className="flex-shrink-0 rounded-full bg-entity-game px-2 py-0.5 font-mono text-[11px] font-extrabold tabular-nums text-white"
      >
        {selectedCount}
      </span>

      <span
        data-slot="library-bulk-selection-counter"
        className="flex-1 whitespace-nowrap font-display text-[12.5px] font-bold"
      >
        {counterText}
      </span>

      <AlertDialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
        <AlertDialogTrigger asChild>
          <button
            type="button"
            disabled={disabled}
            data-slot="library-bulk-selection-archive"
            aria-label={labels.archive}
            className={clsx(ACTION_BUTTON_CLASS, actionPadding)}
          >
            <span aria-hidden="true">⊘</span>
            {!compact && <span>{labels.archive}</span>}
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

      <button
        type="button"
        onClick={onTag}
        disabled={disabled || !onTag}
        data-slot="library-bulk-selection-tag"
        aria-label={labels.tag}
        className={clsx(ACTION_BUTTON_CLASS, actionPadding)}
      >
        <span aria-hidden="true">🏷</span>
        {!compact && <span>{labels.tag}</span>}
      </button>

      <button
        type="button"
        onClick={onExport}
        disabled={disabled || !onExport}
        data-slot="library-bulk-selection-export"
        aria-label={labels.exportLabel}
        className={clsx(ACTION_BUTTON_CLASS, actionPadding)}
      >
        <span aria-hidden="true">↗</span>
        {!compact && <span>{labels.exportLabel}</span>}
      </button>

      <button
        type="button"
        onClick={onExitSelectMode}
        disabled={disabled}
        data-slot="library-bulk-selection-cancel"
        aria-label={labels.closeAriaLabel}
        className={clsx(
          'inline-flex flex-shrink-0 items-center justify-center rounded-md',
          'border border-white/20 bg-transparent px-2.5 py-1.5 text-xs text-inherit',
          'hover:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/60',
          'disabled:cursor-not-allowed disabled:opacity-50',
          'motion-safe:transition-colors motion-safe:duration-150'
        )}
      >
        <span aria-hidden="true">✕</span>
      </button>
    </div>
  );
}
