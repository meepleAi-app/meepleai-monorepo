/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
/**
 * OfflineBanner — SP6 Phase C.2.B v2 component (Issue #789).
 *
 * Sticky banner shown during offline retry budget for /gamebook/upload Step 3.
 * Surfaces 31s retry budget visibility (5 retries × exponential backoff) per
 * contract §10. Pure component (Wave D.3 pattern): all i18n labels are
 * resolved by orchestrator and injected via `labels` prop.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (`pu-offline-banner` block in `IndexingScreen` variant=offline).
 *
 * Visible UI:
 *   - 📡 icon + title "Connessione persa"
 *   - Pre-resolved countdown text "Tentativo {n}/5 in {seconds}s"
 *   - Linear progress bar showing 31s budget elapsed (totalElapsed/31000)
 *   - "Riprova ora" button → invokes onRetryNow
 *   - "Annulla" button → invokes onCancel (opens cancel modal)
 *
 * Gate C — MeepleCard fit decision (DIVERGE):
 *   This is an inline alert banner, not a card pattern. `MeepleCard` is for
 *   entity displays (game/player/collection/event), not transient status
 *   messages. Mirror Wave D.2 ConnectionLostBanner pattern (also DIVERGE).
 *   Documented per contract §13 Gate C.
 *
 * a11y:
 *   - `role="alert"` + `aria-live="polite"` — network failures need assertive
 *     SR announcement BUT polite to avoid double-announce on every tick
 *   - Progress bar uses `role="progressbar"` + `aria-valuenow={elapsed}` +
 *     `aria-valuemax={budgetTotal}` for SR-readable budget consumption
 *   - Buttons have explicit aria-labels via labels prop
 *   - prefers-reduced-motion: progress bar uses `transition-none` fallback
 *
 * data-slot="offline-banner" + data-attempt for E2E selectors.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface OfflineBannerLabels {
  /** Banner title (e.g. "Connessione persa"). */
  readonly title: string;
  /** Pre-resolved countdown text (e.g. "Tentativo 2/5 in 4s..."). Empty string when idle. */
  readonly retryIn: string;
  /** Manual retry CTA visible label (e.g. "Riprova ora"). */
  readonly retryNow: string;
  /** Cancel CTA visible label (e.g. "Annulla"). */
  readonly cancel: string;
  /** Pre-resolved progress aria-valuetext (e.g. "7s di 31s"). */
  readonly progressAria: string;
  /** Pre-resolved aria-label for retry button. */
  readonly retryNowAria: string;
  /** Pre-resolved aria-label for cancel button. */
  readonly cancelAria: string;
}

export interface OfflineBannerProps {
  /** Current attempt number (1-5; 0 = idle no banner expected). */
  readonly attempt: number;
  /** Seconds remaining until next retry (display + accessibility). */
  readonly nextRetryInSeconds: number;
  /** Total elapsed seconds since first failure (drives progress bar). */
  readonly totalElapsedSeconds: number;
  /** Total budget (31s per contract, exposed for tests/override). */
  readonly budgetTotalSeconds: number;
  /** Manual retry handler. */
  readonly onRetryNow: () => void;
  /** Cancel handler (opens CancelModal in orchestrator). */
  readonly onCancel: () => void;
  readonly labels: OfflineBannerLabels;
  readonly className?: string;
}

// event entity colours replaced with Tailwind entity-token classes (P2 #807 Task 6+7+8)
// EVENT_HSL_SOLID → bg-entity-event / EVENT_HSL_BG → bg-entity-event/12 / EVENT_HSL_BORDER → border-entity-event/40

export function OfflineBanner({
  attempt,
  nextRetryInSeconds: _nextRetryInSeconds,
  totalElapsedSeconds,
  budgetTotalSeconds,
  onRetryNow,
  onCancel,
  labels,
  className,
}: OfflineBannerProps): ReactElement {
  // `nextRetryInSeconds` is reserved for the orchestrator API contract and
  // currently consumed via the pre-resolved `labels.retryIn` (ICU pattern).
  // The numeric prop is preserved on the public interface for future
  // animations / countdown progress without re-resolving the label.
  const safeBudget = Math.max(1, budgetTotalSeconds);
  const elapsedClamped = Math.min(totalElapsedSeconds, safeBudget);
  const pct = Math.round((elapsedClamped / safeBudget) * 100);

  return (
    <aside
      data-slot="offline-banner"
      data-attempt={attempt}
      role="alert"
      aria-live="polite"
      className={clsx(
        'flex flex-col gap-2 rounded-lg border border-entity-event/40 bg-entity-event/12 px-3 py-2.5',
        'transition-colors motion-reduce:transition-none',
        className
      )}
    >
      {/* Top row: icon + title + countdown */}
      <div className="flex items-center gap-2.5">
        <span aria-hidden="true" className="text-base leading-none">
          📡
        </span>
        <div className="flex flex-1 flex-col gap-0.5 min-w-0">
          <span
            data-slot="offline-banner-title"
            className="font-display text-xs font-bold text-foreground leading-tight"
          >
            {labels.title}
          </span>
          <span
            data-slot="offline-banner-countdown"
            className="text-[11px] text-foreground leading-tight tabular-nums"
            aria-live="polite"
          >
            {labels.retryIn}
          </span>
        </div>
        {/* Action buttons */}
        <div className="flex shrink-0 items-center gap-1.5">
          <button
            type="button"
            data-slot="offline-banner-retry"
            aria-label={labels.retryNowAria}
            onClick={onRetryNow}
            className={clsx(
              'rounded-md border-none px-2.5 py-1 text-[11px] font-bold text-white',
              'cursor-pointer transition-opacity motion-reduce:transition-none',
              'bg-entity-event hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/30'
            )}
          >
            {labels.retryNow}
          </button>
          <button
            type="button"
            data-slot="offline-banner-cancel"
            aria-label={labels.cancelAria}
            onClick={onCancel}
            className={clsx(
              'rounded-md border border-border bg-card px-2.5 py-1 text-[11px] font-medium text-foreground',
              'cursor-pointer transition-colors motion-reduce:transition-none',
              'hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
            )}
          >
            {labels.cancel}
          </button>
        </div>
      </div>

      {/* Progress bar (31s budget) */}
      <div
        data-slot="offline-banner-progress"
        role="progressbar"
        aria-valuenow={elapsedClamped}
        aria-valuemin={0}
        aria-valuemax={safeBudget}
        aria-valuetext={labels.progressAria}
        className="h-1 w-full overflow-hidden rounded-full bg-muted"
      >
        <div
          className="h-full bg-entity-event transition-[width] duration-300 motion-reduce:transition-none"
          style={{ width: `${pct}%` }}
        />
      </div>
    </aside>
  );
}
