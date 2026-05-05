/**
 * AchievementBadgeGrid — Wave 3 /players/[id] v2 component (Task 2).
 *
 * Mapped from `admin-mockups/design_files/sp4-player-detail.jsx`
 * (Achievements tab badge grid — achievement list in ACHIEVEMENTS fixture).
 *
 * Pure component: all i18n strings injected via `labels`. No hooks.
 *
 * Renders:
 *   - Title + "view all" link row
 *   - N badge placeholder tiles when count > 0
 *   - Empty-state text when count === 0
 *
 * The component renders placeholder badge tiles (no actual achievement data —
 * per spec the orchestrator passes only the count). Full achievement data is
 * available via the viewAllHref sub-route. This matches the mockup pattern
 * where the overview tab shows a compact badge strip.
 *
 * WCAG:
 *   - "View all" link has aria-label from labels.viewAllAriaLabel.
 *   - Badge placeholders are aria-hidden (decorative, no data yet).
 *   - Empty-state uses role="status" for polite announcement.
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

export interface AchievementBadgeGridLabels {
  readonly title: string;
  readonly viewAll: string;
  readonly viewAllAriaLabel: string;
  readonly empty: string;
}

export interface AchievementBadgeGridProps {
  readonly count: number;
  /** href to the full achievements sub-route, e.g. "/players/{id}/achievements" */
  readonly viewAllHref: string;
  readonly labels: AchievementBadgeGridLabels;
  readonly className?: string;
}

/** Shared achievement emoji set for badge visual variety. */
const BADGE_EMOJIS = ['🥇', '🔥', '⭐', '🎉', '🧰', '🎯', '💯', '🛡️', '🧭', '🤖', '🏋️', '✨'];

export function AchievementBadgeGrid({
  count,
  viewAllHref,
  labels,
  className,
}: AchievementBadgeGridProps): ReactElement {
  return (
    <div
      data-slot="player-detail-achievement-grid"
      className={clsx(
        'flex flex-col gap-3 rounded-2xl border border-border bg-card p-4 shadow-sm',
        className
      )}
    >
      {/* Header row: title + view-all link */}
      <div className="flex items-center justify-between gap-2">
        <h3 className="font-display text-[15px] font-extrabold text-foreground">{labels.title}</h3>
        {count > 0 ? (
          <Link
            href={viewAllHref}
            aria-label={labels.viewAllAriaLabel}
            data-slot="player-detail-achievement-view-all"
            className={clsx(
              'rounded-md border border-border px-2.5 py-1',
              'font-display text-[11px] font-bold text-foreground',
              'transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
            )}
          >
            {labels.viewAll}
          </Link>
        ) : null}
      </div>

      {/* Badge grid or empty state */}
      {count > 0 ? (
        <div aria-hidden="true" className="flex flex-wrap gap-2">
          {Array.from({ length: count }, (_, i) => (
            <div
              key={i}
              data-slot="achievement-badge"
              className={clsx(
                'flex h-10 w-10 items-center justify-center rounded-xl',
                'bg-amber-500/10 text-lg',
                'ring-1 ring-amber-400/30 dark:ring-amber-700/30'
              )}
            >
              <span aria-hidden="true">{BADGE_EMOJIS[i % BADGE_EMOJIS.length]}</span>
            </div>
          ))}
        </div>
      ) : (
        <p
          data-slot="player-detail-achievement-empty"
          role="status"
          className="text-sm text-muted-foreground"
        >
          {labels.empty}
        </p>
      )}
    </div>
  );
}
