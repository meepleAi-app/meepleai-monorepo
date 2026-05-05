/**
 * PlayersResultsGrid — Wave 4 D1 v2 component (Issue #682).
 *
 * Mapped from `admin-mockups/design_files/sp4-players-index.jsx` (PlayerCard grid).
 * Spec-panel decision: reuse MeepleCard entity="game" — no fork, no entity extension.
 *
 * Pure component: no fetch, no filter, no sort. Orchestrator (PlayersView) owns the
 * derivation pipeline. Items already pre-sorted by `transformStatsToItems` (playCount
 * desc).
 *
 * Field mapping (PlayerListItem → MeepleCard):
 *   - title    = item.gameName
 *   - subtitle = "{playCount} partite" (from labels.cardSubtitle template)
 *   - entity   = 'game' (spec-panel decision, inherits game token color)
 *   - variant  = 'grid'
 *
 * Layout: CSS-grid 3-col `auto-fit minmax(280px, 1fr)` max-w-[1280px],
 * matching Wave B.1 GamesResultsGrid pattern.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { PlayerListItem } from '@/lib/players/players-filters';

export interface PlayersResultsGridLabels {
  readonly resultsAriaLabel: string;
  /** Template string with {count} placeholder, e.g. "{count} risultati" */
  readonly resultsCount: string;
  /** Template string with {count} placeholder, e.g. "{count} partite" */
  readonly cardSubtitle: string;
  /** Template string with {gameName} placeholder, e.g. "Apri {gameName}" */
  readonly cardOpenAriaLabel: string;
}

export interface PlayersResultsGridProps {
  readonly items: ReadonlyArray<PlayerListItem>;
  readonly onItemClick: (item: PlayerListItem) => void;
  readonly labels: PlayersResultsGridLabels;
  readonly className?: string;
}

export function PlayersResultsGrid({
  items,
  onItemClick,
  labels,
  className,
}: PlayersResultsGridProps): ReactElement {
  return (
    <div
      data-slot="players-results-grid"
      aria-label={labels.resultsAriaLabel}
      className={clsx(
        'grid grid-cols-[repeat(auto-fit,minmax(280px,1fr))] gap-4 px-4 sm:px-8 max-w-[1280px] mx-auto',
        className
      )}
    >
      {items.map(item => {
        const subtitle = labels.cardSubtitle.replace('{count}', String(item.playCount));
        const openAriaLabel = labels.cardOpenAriaLabel.replace('{gameName}', item.gameName);

        return (
          <button
            key={item.id}
            type="button"
            onClick={() => onItemClick(item)}
            className="block w-full text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-2xl"
            data-slot="players-results-grid-item"
            data-item-id={item.id}
            aria-label={openAriaLabel}
          >
            <MeepleCard
              entity="game"
              variant="grid"
              id={item.id}
              title={item.gameName}
              subtitle={subtitle}
            />
          </button>
        );
      })}
    </div>
  );
}
