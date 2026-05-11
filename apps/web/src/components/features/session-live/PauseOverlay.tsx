'use client';

/**
 * PauseOverlay — Wave D.2 Interactions sub-PR (Issue #750).
 *
 * Modal dialog shown when session is paused.
 *
 * A11y (WCAG 2.1 contract, Gate C):
 *   - role="dialog" aria-modal="true" aria-labelledby aria-describedby
 *   - Focus trap: Tab cycles within dialog focusables only
 *   - ESC closes dialog (calls onClose)
 *   - Focus restored to previously-focused element on close (via useRef)
 *   - Background: aria-hidden via parent (orchestrator sets in Task 3)
 *   - prefers-reduced-motion: motion-reduce:transition-none
 *
 * Role variants:
 *   - Spectator/Player: can only close (no Resume CTA)
 *   - Host: can Resume (onResume) or close (onClose)
 *
 * Named export (orchestrator uses React.lazy with .then(m => ({ default: m.PauseOverlay }))).
 *
 * data-slot="pause-overlay" — required by unit tests.
 */

import { type ReactElement, useId, useEffect, useRef, useCallback } from 'react';

import { X } from 'lucide-react';

import type { ParticipantRole } from '@/lib/session-live/participant-role';

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface PauseOverlayLabels {
  readonly title: string;
  readonly resumeCta: string;
  readonly closeCta: string;
  readonly closeAriaLabel: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface PauseOverlayProps {
  readonly pausedBy: string;
  readonly pausedAt: string;
  readonly viewerRole: ParticipantRole;
  readonly onResume?: () => void;
  readonly onClose: () => void;
  readonly labels: PauseOverlayLabels;
}

// ─── Focus trap helper ────────────────────────────────────────────────────────

const FOCUSABLE_SELECTORS =
  'a[href], button:not([disabled]), textarea:not([disabled]), ' +
  'input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

function getFocusables(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTORS)).filter(
    el => !el.closest('[aria-hidden="true"]')
  );
}

// ─── Component ────────────────────────────────────────────────────────────────

export function PauseOverlay({
  pausedBy,
  pausedAt,
  viewerRole,
  onResume,
  onClose,
  labels,
}: PauseOverlayProps): ReactElement {
  const titleId = useId();
  const descId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);

  const isHost = viewerRole === 'Host';

  // Save currently focused element, focus first focusable inside dialog
  useEffect(() => {
    previousFocusRef.current = document.activeElement as HTMLElement;
    // Focus first focusable element in the dialog
    const focusables = dialogRef.current ? getFocusables(dialogRef.current) : [];
    if (focusables.length > 0) {
      focusables[0].focus();
    }
    return () => {
      // Restore focus on unmount
      previousFocusRef.current?.focus();
    };
  }, []);

  // ESC key handler: closes dialog
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLDivElement>) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
        return;
      }

      // Focus trap: Tab cycles within focusables
      if (e.key === 'Tab' && dialogRef.current) {
        const focusables = getFocusables(dialogRef.current);
        if (focusables.length === 0) return;

        const firstEl = focusables[0];
        const lastEl = focusables[focusables.length - 1];

        if (e.shiftKey) {
          // Shift+Tab: if first → wrap to last
          if (document.activeElement === firstEl) {
            e.preventDefault();
            lastEl.focus();
          }
        } else {
          // Tab: if last → wrap to first
          if (document.activeElement === lastEl) {
            e.preventDefault();
            firstEl.focus();
          }
        }
      }
    },
    [onClose]
  );

  return (
    /* Backdrop */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/70
        motion-safe:transition-opacity motion-reduce:transition-none"
    >
      {/* Dialog */}
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descId}
        data-slot="pause-overlay"
        data-viewer-role={viewerRole}
        onKeyDown={handleKeyDown}
        className="relative w-full max-w-md rounded-xl border border-slate-700/60
          bg-[hsl(240,40%,14%)] p-6 shadow-2xl
          motion-safe:animate-in motion-safe:fade-in motion-safe:duration-200
          motion-reduce:animate-none"
      >
        {/* Close button */}
        <button
          type="button"
          onClick={onClose}
          aria-label={labels.closeAriaLabel}
          className="absolute right-4 top-4 rounded-md p-1 text-slate-400
            transition-colors hover:text-slate-200
            focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400"
        >
          <X className="h-4 w-4" aria-hidden="true" />
        </button>

        {/* Title */}
        <h2 id={titleId} className="mb-2 text-lg font-semibold text-slate-100">
          {labels.title}
        </h2>

        {/* Description */}
        <p id={descId} className="mb-6 text-sm text-slate-400">
          {pausedBy} · {pausedAt}
        </p>

        {/* CTAs */}
        <div className="flex gap-3">
          {isHost && onResume && (
            <button
              type="button"
              onClick={onResume}
              className="flex-1 rounded-lg bg-emerald-700 px-4 py-2.5 text-sm
                font-semibold text-white transition-colors hover:bg-emerald-600
                focus-visible:outline-none focus-visible:ring-2
                focus-visible:ring-emerald-400"
            >
              {labels.resumeCta}
            </button>
          )}
          <button
            type="button"
            onClick={onClose}
            className="flex-1 rounded-lg border border-slate-700/60 bg-slate-800/60
              px-4 py-2.5 text-sm font-medium text-slate-200 transition-colors
              hover:bg-slate-700/60
              focus-visible:outline-none focus-visible:ring-2
              focus-visible:ring-slate-400"
          >
            {labels.closeCta}
          </button>
        </div>
      </div>
    </div>
  );
}
