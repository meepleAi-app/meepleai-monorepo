/**
 * GamesRecentRail — v2 horizontal rail per il flusso "serata di gioco" (G3).
 *
 * Pure component (no fetch, no router, no i18n hook): orchestrator passa
 * items + labels + onSelect. Riusa pattern Wave B.1 (vedi GamesHero).
 *
 * Spec: docs/superpowers/specs/2026-05-09-game-night-user-flow-design.md §G3
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export type RecentKbBadge = 'ready' | 'processing' | 'none';

export interface GamesRecentRailItem {
  readonly id: string;
  readonly title: string;
  readonly imageUrl?: string;
  readonly kbBadge: RecentKbBadge;
}

export interface GamesRecentRailLabels {
  readonly sectionTitle: string;
  readonly emptyHint: string;
}

export interface GamesRecentRailProps {
  readonly items: readonly GamesRecentRailItem[];
  readonly labels: GamesRecentRailLabels;
  readonly isLoading?: boolean;
  readonly onSelect: (id: string) => void;
  readonly className?: string;
}

const SKELETON_COUNT = 3;

const KB_BADGE_LABEL: Record<RecentKbBadge, string> = {
  ready: '📖 KB pronta',
  processing: '⏳ KB in elaborazione',
  none: '',
};

export function GamesRecentRail({
  items,
  labels,
  isLoading = false,
  onSelect,
  className,
}: GamesRecentRailProps): ReactElement {
  return (
    <section
      role="region"
      aria-label={labels.sectionTitle}
      data-slot="games-recent-rail"
      className={clsx('w-full px-4 py-3', className)}
    >
      <h2 className="text-base font-semibold text-foreground mb-3">{labels.sectionTitle}</h2>

      {isLoading ? (
        <div className="flex gap-3 overflow-x-auto pb-1">
          {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
            <Skeleton
              key={i}
              data-testid="games-recent-rail-skeleton"
              className="h-24 w-40 shrink-0 rounded-xl"
            />
          ))}
        </div>
      ) : items.length === 0 ? (
        <p className="text-sm text-muted-foreground py-4">{labels.emptyHint}</p>
      ) : (
        <div className="flex gap-3 overflow-x-auto pb-1 snap-x snap-mandatory">
          {items.map(item => (
            <button
              key={item.id}
              type="button"
              onClick={() => onSelect(item.id)}
              aria-label={item.title}
              data-slot="games-recent-rail-card"
              data-kb-badge={item.kbBadge}
              className={clsx(
                'shrink-0 snap-start text-left rounded-xl border border-border bg-card',
                'h-24 w-40 px-3 py-2 transition-colors hover:bg-muted',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
              )}
            >
              <span className="block text-sm font-medium text-foreground line-clamp-2">
                {item.title}
              </span>
              {item.kbBadge !== 'none' && (
                <span className="mt-1 block text-xs text-muted-foreground">
                  {KB_BADGE_LABEL[item.kbBadge]}
                </span>
              )}
            </button>
          ))}
        </div>
      )}
    </section>
  );
}
