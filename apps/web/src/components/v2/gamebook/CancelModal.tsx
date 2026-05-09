/**
 * CancelModal — SP6 Phase C.2.B v2 component (Issue #789).
 *
 * Confirmation modal shown when user taps × on Step 2 viewfinder OR Cancel
 * on Step 3 OfflineBanner with capturedCount > 0. Mirror of Wave D.2
 * `PauseOverlay` focus-trap + ESC behavior matrix. Pure component (Wave D.3
 * pattern): all i18n labels are resolved by orchestrator and injected via
 * `labels` prop.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (`CancelModal` block).
 *
 * ESC behavior matrix (per contract §5.10 + Wave D.2):
 *   - ESC                  → invokes onDismiss (default cancel-cancel —
 *                            destructive action requires deliberate confirm)
 *   - Click overlay        → invokes onDismiss
 *   - Confirm "Sì, annulla" → invokes onConfirm → orchestrator FSM transitions
 *                             to wizard-cancelled
 *   - Continue "Continua"  → invokes onDismiss
 *
 * Initial focus on Continue button (safe default — mirrors PauseOverlay
 * close-first focus pattern).
 *
 * Lazy-loading hint (orchestrator):
 *   import { lazy } from 'react';
 *   const CancelModal = lazy(() =>
 *     import('@/components/v2/gamebook/CancelModal').then(m => ({ default: m.CancelModal }))
 *   );
 *
 * Gate C — MeepleCard fit decision (DIVERGE):
 *   Modals are not card displays. `MeepleCard` is for entity tiles, not
 *   transient confirmation dialogs. Mirror Wave D.2 PauseOverlay/EndgameDialog
 *   divergence pattern. Documented per contract §13 Gate C.
 *
 * a11y:
 *   - `role="alertdialog"` (destructive action dialogs use alertdialog
 *     instead of plain dialog per WAI-ARIA APG)
 *   - `aria-modal="true"` + `aria-labelledby` to title id + `aria-describedby`
 *     to body text id
 *   - Focus trap: Tab cycles within dialog focusables only (mirror Wave D.2)
 *   - ESC closes dialog (calls onDismiss)
 *   - Focus restored to previously-focused element on unmount
 *   - prefers-reduced-motion: backdrop opacity transition disabled
 *
 * data-slot="cancel-modal" + data-open for E2E selectors.
 */

'use client';

import type { ReactElement } from 'react';
import { useCallback, useEffect, useId, useRef } from 'react';

import clsx from 'clsx';
import { AlertTriangle } from 'lucide-react';

export interface CancelModalLabels {
  /** Modal title (e.g. "Annullare l'indicizzazione?"). */
  readonly title: string;
  /** Modal body description (pre-resolved capturedCount, e.g. "Hai già scattato 24 pagine..."). */
  readonly description: string;
  /** Confirm CTA visible label (e.g. "Sì, annulla"). */
  readonly confirm: string;
  /** Dismiss CTA visible label (e.g. "Continua a indicizzare"). */
  readonly dismiss: string;
  /** Pre-resolved aria-label for confirm button. */
  readonly confirmAria: string;
  /** Pre-resolved aria-label for dismiss button. */
  readonly dismissAria: string;
}

export interface CancelModalProps {
  /** Whether modal is open. */
  readonly isOpen: boolean;
  /** Confirm handler — destructive, transitions FSM to wizard-cancelled. */
  readonly onConfirm: () => void;
  /** Dismiss handler — closes modal, keeps wizard open. ESC + overlay click + dismiss button all invoke this. */
  readonly onDismiss: () => void;
  readonly labels: CancelModalLabels;
  readonly className?: string;
}

// Focus-trap helper — mirror Wave D.2 PauseOverlay implementation.
const FOCUSABLE_SELECTORS =
  'a[href], button:not([disabled]), textarea:not([disabled]), ' +
  'input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

function getFocusables(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTORS)).filter(
    el => !el.closest('[aria-hidden="true"]')
  );
}

// event entity colours — replaced inline HSL with Tailwind entity-token classes (P2 #807 Task 6+7+8)
// EVENT_HSL_SOLID → bg-entity-event / EVENT_HSL_BG → bg-entity-event/12

export function CancelModal({
  isOpen,
  onConfirm,
  onDismiss,
  labels,
  className,
}: CancelModalProps): ReactElement | null {
  const titleId = useId();
  const descId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);
  const dismissButtonRef = useRef<HTMLButtonElement>(null);

  // Save currently focused element on open, restore on close
  useEffect(() => {
    if (!isOpen) return;
    previousFocusRef.current = document.activeElement as HTMLElement;

    // Initial focus: Continue (dismiss) button — safe default per contract §5.10
    if (dismissButtonRef.current) {
      dismissButtonRef.current.focus();
    } else {
      // Fallback: first focusable in dialog
      const focusables = dialogRef.current ? getFocusables(dialogRef.current) : [];
      if (focusables.length > 0) {
        focusables[0].focus();
      }
    }

    return () => {
      // Restore focus to previously-focused element on unmount
      previousFocusRef.current?.focus();
    };
  }, [isOpen]);

  // ESC + Tab focus trap handler
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLDivElement>) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onDismiss();
        return;
      }

      if (e.key === 'Tab' && dialogRef.current) {
        const focusables = getFocusables(dialogRef.current);
        if (focusables.length === 0) return;

        const firstEl = focusables[0];
        const lastEl = focusables[focusables.length - 1];

        if (e.shiftKey) {
          if (document.activeElement === firstEl) {
            e.preventDefault();
            lastEl.focus();
          }
        } else {
          if (document.activeElement === lastEl) {
            e.preventDefault();
            firstEl.focus();
          }
        }
      }
    },
    [onDismiss]
  );

  // Click on overlay (NOT inside dialog) → dismiss
  const handleOverlayClick = useCallback(
    (e: React.MouseEvent<HTMLDivElement>) => {
      if (e.target === e.currentTarget) {
        onDismiss();
      }
    },
    [onDismiss]
  );

  if (!isOpen) return null;

  return (
    <div
      data-slot="cancel-modal-overlay"
      onClick={handleOverlayClick}
      className={clsx(
        'fixed inset-0 z-50 flex items-center justify-center bg-black/55 p-5 backdrop-blur-sm',
        'motion-safe:transition-opacity motion-reduce:transition-none'
      )}
    >
      <div
        ref={dialogRef}
        data-slot="cancel-modal"
        data-open={isOpen ? 'true' : 'false'}
        role="alertdialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descId}
        onKeyDown={handleKeyDown}
        className={clsx(
          'relative flex w-full max-w-xs flex-col items-center gap-2.5 rounded-xl border border-border bg-card p-5 text-center shadow-2xl',
          'motion-safe:animate-in motion-safe:fade-in motion-safe:duration-200 motion-reduce:animate-none',
          className
        )}
      >
        {/* Icon */}
        <div
          aria-hidden="true"
          className="flex h-14 w-14 items-center justify-center rounded-full bg-entity-event/12 text-entity-event"
        >
          <AlertTriangle className="h-6 w-6" aria-hidden="true" />
        </div>

        {/* Title */}
        <h3
          id={titleId}
          data-slot="cancel-modal-title"
          className="font-display text-lg font-bold leading-tight text-foreground"
        >
          {labels.title}
        </h3>

        {/* Description */}
        <p
          id={descId}
          data-slot="cancel-modal-description"
          className="text-sm leading-normal text-slate-700"
        >
          {labels.description}
        </p>

        {/* Actions row */}
        <div className="mt-1.5 flex w-full gap-2">
          <button
            ref={dismissButtonRef}
            type="button"
            data-slot="cancel-modal-dismiss"
            onClick={onDismiss}
            aria-label={labels.dismissAria}
            className={clsx(
              'flex-1 rounded-lg border border-border bg-card px-4 py-3 text-sm font-semibold text-foreground',
              'cursor-pointer transition-colors motion-reduce:transition-none',
              'hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
            )}
          >
            {labels.dismiss}
          </button>
          <button
            type="button"
            data-slot="cancel-modal-confirm"
            onClick={onConfirm}
            aria-label={labels.confirmAria}
            className={clsx(
              'flex-1 rounded-lg border-none px-4 py-3 text-sm font-bold text-white',
              'cursor-pointer transition-opacity motion-reduce:transition-none',
              'bg-entity-event hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-500'
            )}
          >
            {labels.confirm}
          </button>
        </div>
      </div>
    </div>
  );
}
