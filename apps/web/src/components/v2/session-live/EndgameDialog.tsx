'use client';

/**
 * EndgameDialog — Wave D.2 Interactions sub-PR (Issue #750).
 *
 * Modal dialog shown at session end with final scores.
 *
 * A11y (WCAG 2.1 contract, Gate C — intentional deviation documented):
 *   - role="dialog" aria-modal="true" aria-labelledby
 *   - Focus trap: Tab cycles within dialog focusables only
 *   - ⚠️ ESC DISABLED: intentional WCAG 2.1 SC 3.2.2 deviation.
 *     The endgame summary presents critical session-end data. Accidental
 *     dismiss via ESC would cause data loss (no other path back to scores).
 *     The only exit path is the Acknowledge CTA button.
 *     This deviation is documented per the Phase 0.5 contract §4.3 and
 *     §5.11, validated by UX/a11y review.
 *   - Default focus: Acknowledge CTA button (primary exit action)
 *   - Focus restored to previously-focused element on close
 *   - prefers-reduced-motion: motion-reduce:transition-none
 *
 * Named export (orchestrator uses React.lazy with .then(m => ({ default: m.EndgameDialog }))).
 *
 * data-slot="endgame-dialog" — required by unit tests.
 */

import { type ReactElement, useId, useEffect, useRef, useCallback } from 'react';

import { Trophy } from 'lucide-react';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface FinalScoreEntry {
  readonly playerName: string;
  readonly score: number;
  readonly isWinner: boolean;
}

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface EndgameDialogLabels {
  readonly title: string;
  readonly winnerLabel: string;
  readonly acknowledgeCta: string;
  readonly viewSummaryCta: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface EndgameDialogProps {
  readonly finalScores: ReadonlyArray<FinalScoreEntry>;
  readonly endedAt: string;
  readonly endedBy: string;
  readonly onAcknowledge: () => void;
  readonly labels: EndgameDialogLabels;
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

export function EndgameDialog({
  finalScores,
  endedAt,
  endedBy,
  onAcknowledge,
  labels,
}: EndgameDialogProps): ReactElement {
  const titleId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const acknowledgeRef = useRef<HTMLButtonElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);

  // Save currently focused element, focus the Acknowledge CTA by default
  useEffect(() => {
    previousFocusRef.current = document.activeElement as HTMLElement;
    // Focus Acknowledge button as the primary exit action
    acknowledgeRef.current?.focus();
    return () => {
      // Restore focus on unmount
      previousFocusRef.current?.focus();
    };
  }, []);

  // Key handler: focus trap ONLY (ESC DISABLED — intentional, see JSDoc)
  const handleKeyDown = useCallback((e: React.KeyboardEvent<HTMLDivElement>) => {
    // ESC DISABLED: do NOT call onAcknowledge or close on Escape
    // (intentional WCAG deviation — accidental dismiss = data loss)
    if (e.key === 'Escape') {
      e.preventDefault();
      // No action — dialog remains open
      return;
    }

    // Focus trap: Tab cycles within focusables
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
  }, []);

  // Sort scores: winners first, then descending score
  const sortedScores = [...finalScores].sort((a, b) => {
    if (a.isWinner && !b.isWinner) return -1;
    if (!a.isWinner && b.isWinner) return 1;
    return b.score - a.score;
  });

  return (
    /* Backdrop */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/80
        motion-safe:transition-opacity motion-reduce:transition-none"
    >
      {/* Dialog */}
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        data-slot="endgame-dialog"
        onKeyDown={handleKeyDown}
        className="w-full max-w-md rounded-xl border border-slate-700/60
          bg-[hsl(240,40%,14%)] p-6 shadow-2xl
          motion-safe:animate-in motion-safe:fade-in motion-safe:duration-200
          motion-reduce:animate-none"
      >
        {/* Title */}
        <h2
          id={titleId}
          className="mb-1 flex items-center gap-2 text-lg font-semibold text-slate-100"
        >
          <Trophy className="h-5 w-5 text-amber-400" aria-hidden="true" />
          {labels.title}
        </h2>

        {/* Subtitle */}
        <p className="mb-5 text-sm text-slate-400">
          {endedBy} · {endedAt}
        </p>

        {/* Scores list */}
        <ol className="mb-6 flex flex-col gap-2" aria-label="Punteggi finali">
          {sortedScores.map((entry, idx) => (
            <li
              key={entry.playerName}
              className={`flex items-center justify-between rounded-lg px-4 py-2.5 ${
                entry.isWinner ? 'bg-amber-900/30 ring-1 ring-amber-700/40' : 'bg-slate-800/50'
              }`}
            >
              <div className="flex items-center gap-2">
                <span className="w-5 text-center text-xs text-slate-500">{idx + 1}.</span>
                <span
                  className={`text-sm font-medium ${entry.isWinner ? 'text-amber-200' : 'text-slate-200'}`}
                >
                  {entry.playerName}
                </span>
                {entry.isWinner && (
                  <span className="rounded-full bg-amber-700/50 px-1.5 py-0.5 text-xs text-amber-300">
                    {labels.winnerLabel}
                  </span>
                )}
              </div>
              <span
                className={`text-sm font-semibold ${entry.isWinner ? 'text-amber-300' : 'text-slate-300'}`}
              >
                {entry.score}
              </span>
            </li>
          ))}
        </ol>

        {/* Acknowledge CTA — only exit path */}
        <button
          ref={acknowledgeRef}
          type="button"
          onClick={onAcknowledge}
          className="w-full rounded-lg bg-slate-700 px-4 py-3 text-sm font-semibold
            text-slate-100 transition-colors hover:bg-slate-600
            focus-visible:outline-none focus-visible:ring-2
            focus-visible:ring-slate-400"
        >
          {labels.acknowledgeCta}
        </button>
      </div>
    </div>
  );
}
