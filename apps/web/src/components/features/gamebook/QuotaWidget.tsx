/**
 * QuotaWidget — SP6 Phase B Task 2 v2 component (Issue #788).
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-index.jsx`
 * (QuotaWidget + HardLimitBanner).
 *
 * Pure component: all i18n strings injected via `labels` (orchestrator
 * resolves ICU plural `usedCount` upstream — Wave D.3 pattern).
 *
 * Variants:
 *   - `default`  : healthy quota (< 90% used) — green progress bar (toolkit
 *                  entity), no warning banner, no upgrade CTA.
 *   - `soft`     : ≥ 90% used (< 100%) — amber progress bar (event entity),
 *                  warning banner inline, upgrade CTA visible.
 *   - `hard`     : 100% used — red progress bar (event entity), blocking
 *                  banner with role="alert", prominent upgrade CTA.
 *
 * The component is purely presentational — variant precedence is decided
 * by the orchestrator's FSM (`deriveGamebookIndexState`) and passed in.
 *
 * a11y:
 *   - Outer container has `role="status"` so SR announce quota changes.
 *   - `hard` variant uses an inner `role="alert"` banner for immediate
 *     announcement when quota is exceeded.
 *
 * data-slot="quota-widget" with `data-variant` mirror for E2E selectors.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { QuotaInfo } from '@/lib/gamebook-index/schemas';

export type QuotaWidgetVariant = 'default' | 'soft' | 'hard';

export interface QuotaWidgetLabels {
  /** Header label (e.g. "Quota traduzioni"). */
  readonly title: string;
  /** Pre-formatted "X / Y" via orchestrator ICU plural `usedCount`. */
  readonly usedLabel: string;
  /** Reset date sentence (e.g. "Resetta il 1 giugno"). */
  readonly resetIn: string;
  /** Soft warning banner text (only rendered when variant=soft). */
  readonly softWarning: string;
  /** Hard limit banner text (only rendered when variant=hard). */
  readonly hardLimit: string;
  /** Upgrade CTA label (rendered when variant != default). */
  readonly upgrade: string;
}

export interface QuotaWidgetProps {
  readonly quota: QuotaInfo;
  readonly variant: QuotaWidgetVariant;
  /** Click handler for upgrade CTA — required when variant != default. */
  readonly onUpgradeClick?: () => void;
  readonly labels: QuotaWidgetLabels;
  readonly className?: string;
}

// Ratio helpers — variant is authoritative; we just compute width %.
function computeFillPercent(used: number, total: number): number {
  if (total <= 0) return 0;
  return Math.min(100, Math.round((used / total) * 100));
}

// toolkit + event entity colours replaced with Tailwind entity-token classes (P2 #807 Task 6+7+8)

export function QuotaWidget({
  quota,
  variant,
  onUpgradeClick,
  labels,
  className,
}: QuotaWidgetProps): ReactElement {
  const fillPct = computeFillPercent(quota.used, quota.total);
  const isHard = variant === 'hard';
  const isSoft = variant === 'soft';
  const showUpgrade = isSoft || isHard;
  const fillClass = variant === 'default' ? 'bg-entity-toolkit' : 'bg-entity-event';

  return (
    <section
      data-slot="quota-widget"
      data-variant={variant}
      role="status"
      aria-label={`${labels.title}: ${labels.usedLabel}`}
      className={clsx(
        'flex flex-col gap-3 rounded-lg bg-card p-4 sm:p-5',
        isHard ? 'border-2 border-entity-event/35' : 'border',
        className
      )}
    >
      {/* Header row — title + counter */}
      <div className="flex items-center justify-between gap-3">
        <h2
          data-slot="quota-widget-title"
          className="flex items-center gap-1.5 text-sm font-semibold text-foreground"
        >
          <span aria-hidden="true">📊</span>
          <span>{labels.title}</span>
        </h2>
        <span
          data-slot="quota-widget-counter"
          className={clsx(
            'font-mono text-sm font-bold tabular-nums',
            isHard && 'text-entity-event'
          )}
        >
          {labels.usedLabel}
        </span>
      </div>

      {/* Progress bar */}
      <div
        data-slot="quota-widget-bar"
        aria-hidden="true"
        className="h-2 overflow-hidden rounded-full bg-muted"
      >
        <div
          data-slot="quota-widget-fill"
          className={clsx(
            'h-full rounded-full transition-[width] duration-300 motion-reduce:transition-none',
            fillClass
          )}
          style={{ width: `${fillPct}%` }}
        />
      </div>

      {/* Footer row — reset date + upgrade CTA */}
      <div className="flex items-center justify-between gap-2">
        <p className="font-mono text-[10px] uppercase tracking-wide text-foreground">
          {labels.resetIn}
        </p>
        {showUpgrade && onUpgradeClick && (
          <button
            type="button"
            onClick={onUpgradeClick}
            data-slot="quota-widget-upgrade-cta"
            className={clsx(
              'inline-flex items-center text-xs font-bold text-entity-event',
              'transition-opacity motion-reduce:transition-none hover:opacity-80',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded'
            )}
          >
            {labels.upgrade}
            <span aria-hidden="true" className="ml-1">
              →
            </span>
          </button>
        )}
      </div>

      {/* Inline warning banner — soft */}
      {isSoft && (
        <div
          data-slot="quota-widget-soft-banner"
          className="flex items-center gap-2 rounded-md bg-entity-event/10 px-3 py-2 text-xs font-medium text-entity-event"
        >
          <span aria-hidden="true">⚠️</span>
          <span>{labels.softWarning}</span>
        </div>
      )}

      {/* Blocking banner — hard */}
      {isHard && (
        <div
          data-slot="quota-widget-hard-banner"
          role="alert"
          className="flex items-center gap-2 rounded-md bg-entity-event/10 px-3 py-2 text-xs font-bold text-entity-event"
        >
          <span aria-hidden="true">⚠️</span>
          <span>{labels.hardLimit}</span>
        </div>
      )}
    </section>
  );
}
