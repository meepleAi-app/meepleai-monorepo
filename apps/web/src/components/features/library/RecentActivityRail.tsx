/**
 * RecentActivityRail — Wave B.3 v2 component (Issue #574).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (RecentActivityRail sidebar).
 * Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md §3.2
 *
 * Phase 1 contract (locked by AC-7):
 *   No backend endpoint `GET /api/v1/library/activity` exists yet, so the
 *   orchestrator (LibraryHub) ALWAYS passes `items={[]}`. The component
 *   renders the placeholder copy "Activity feed prossimamente". A loading
 *   variant (`isLoading`) renders 3 skeleton lines so the sidebar reserves
 *   space and avoids layout shift if/when the endpoint lands in Phase 2.
 *
 * Phase 2 readiness:
 *   Even though `items` is currently always empty, the component already
 *   renders a list when given data. This avoids a rewrite when the backend
 *   endpoint ships — the orchestrator just stops short-circuiting and the
 *   sidebar lights up. Each item carries `data-activity-kind` so per-kind
 *   icon/styling can be layered without touching the component contract.
 *
 * Sidebar width: 280px (`lg:w-72`) hidden under `lg` breakpoint.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export type ActivityKind = 'play' | 'add' | 'kb-indexed' | 'rating-changed' | 'removed';

export interface ActivityItem {
  readonly id: string;
  readonly kind: ActivityKind;
  readonly entityTitle: string;
  readonly timestamp: string;
}

export interface RecentActivityRailProps {
  readonly items: ReadonlyArray<ActivityItem>;
  readonly isLoading?: boolean;
  readonly className?: string;
}

const SKELETON_LINES = 3;

const KIND_ICON: Record<ActivityKind, string> = {
  play: '🎲',
  add: '➕',
  'kb-indexed': '📖',
  'rating-changed': '⭐',
  removed: '🗑️',
};

export function RecentActivityRail({
  items,
  isLoading = false,
  className,
}: RecentActivityRailProps): ReactElement {
  const state: 'loading' | 'empty' | 'populated' = isLoading
    ? 'loading'
    : items.length === 0
      ? 'empty'
      : 'populated';

  return (
    <aside
      data-slot="library-activity-rail"
      data-state={state}
      aria-busy={isLoading || undefined}
      aria-live="polite"
      className={clsx(
        'hidden flex-col gap-3 rounded-2xl border border-border bg-card p-4 shadow-sm lg:flex lg:w-72',
        className
      )}
    >
      <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
        Recent activity
      </h2>

      {state === 'loading' ? (
        <div className="flex flex-col gap-2">
          {Array.from({ length: SKELETON_LINES }, (_, index) => (
            <Skeleton
              key={index}
              data-slot="library-activity-skeleton"
              className="h-12 w-full rounded-lg"
            />
          ))}
        </div>
      ) : null}

      {state === 'empty' ? (
        <p className="text-sm text-muted-foreground" data-slot="library-activity-empty">
          Activity feed prossimamente
        </p>
      ) : null}

      {state === 'populated' ? (
        <ul className="flex flex-col gap-2">
          {items.map(item => (
            <li
              key={item.id}
              data-slot="library-activity-item"
              data-activity-kind={item.kind}
              className="flex items-center gap-3 rounded-lg border border-border/60 bg-muted/20 px-3 py-2"
            >
              <span aria-hidden="true" className="text-lg">
                {KIND_ICON[item.kind]}
              </span>
              <span className="text-sm text-foreground">{item.entityTitle}</span>
            </li>
          ))}
        </ul>
      ) : null}
    </aside>
  );
}
