/**
 * GamesResultsGrid — Wave B.1 v2 component (Issue #633).
 *
 * Mapped from `admin-mockups/design_files/sp4-games-index.jsx` (GameCardGrid).
 * Spec: docs/superpowers/specs/2026-04-29-v2-migration-wave-b-1-games.md §3.2
 *
 * MeepleCard zero-fork (BLOCKER #4 spec §10): wraps the existing
 * `MeepleCard` from `@/components/ui/data-display/meeple-card` 1:1 — no fork,
 * no entity-prefix, no spec extension. Each card is wrapped in a Next.js
 * `<Link>` to `/games/{gameId}` for navigation (MeepleCard itself has no
 * href prop; href is applied at the wrapper level).
 *
 * Component is dumb: no fetch, no filter, no sort. Orchestrator
 * (GamesLibraryView, Commit 3) owns the derivation pipeline.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

export type GamesResultsView = 'grid' | 'list';

export interface GamesResultsGridProps {
  readonly entries: readonly UserLibraryEntry[];
  readonly view: GamesResultsView;
  /** Mobile: force grid, hide list view dispatcher. */
  readonly compact?: boolean;
  readonly className?: string;
}

export function GamesResultsGrid({
  entries,
  view,
  compact = false,
  className,
}: GamesResultsGridProps): ReactElement {
  const isList = !compact && view === 'list';
  const cardVariant = isList ? 'list' : 'grid';

  return (
    <div
      data-slot="games-results-grid"
      className={clsx(
        compact
          ? 'grid grid-cols-2 gap-3 px-4'
          : isList
            ? 'flex flex-col gap-2 px-8'
            : 'grid grid-cols-3 lg:grid-cols-4 gap-4 px-8',
        className
      )}
    >
      {entries.map(entry => (
        <Link
          key={entry.id}
          href={`/games/${entry.gameId}`}
          className="block focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-2xl"
          data-slot="games-results-grid-link"
        >
          <MeepleCard
            entity="game"
            variant={cardVariant}
            id={entry.id}
            title={entry.gameTitle}
            subtitle={entry.gamePublisher ?? undefined}
            imageUrl={entry.gameImageUrl ?? undefined}
            rating={entry.averageRating ?? undefined}
            ratingMax={10}
          />
        </Link>
      ))}
    </div>
  );
}
