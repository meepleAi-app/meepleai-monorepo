/**
 * SharedGamesGrid — dispatcher for /shared-games index 5-state surface.
 *
 * Wave A.3b (Issue #596). Mirrors mockup `sp3-shared-games.jsx` lines 708-736.
 *
 * State machine (consumed via single `state` prop):
 *  - 'loading'        → 9 SkeletonCard (responsive grid)
 *  - 'error'          → ErrorState (caller wires onRetry)
 *  - 'empty-search'   → EmptyState kind='empty-search' (caller wires onReset)
 *  - 'filtered-empty' → EmptyState kind='filtered-empty' (caller wires onReset)
 *  - 'default'        → grid of MeepleCardGame, one per `games` item
 *
 * The grid uses a 2/3/4-col responsive layout with consistent gap; the card
 * tile is intentionally compact-enabled on mobile so all 5 states share the
 * same outer spacing and visual rhythm.
 */

'use client';

import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

import { AdminCoverEditAffordance } from '@/components/features/cover-editor';
import {
  MeepleCardGame,
  type MeepleCardGameLabels,
  type MeepleCardGameProps,
} from '@/components/ui/shared-games/meeple-card-game';
import { SkeletonCard } from '@/components/ui/shared-games/skeleton-card';
import { useAdminRole } from '@/hooks/useAdminRole';
import { shouldUsePlaceholder } from '@/lib/games/cover-utils';

export type SharedGamesGridState =
  'default' | 'loading' | 'error' | 'empty-search' | 'filtered-empty';

export type SharedGamesGridGame = Omit<MeepleCardGameProps, 'labels' | 'className'> & {
  /**
   * #3611 — punto focale piatto come lo espone il DTO (dominio [0,1]); questo
   * componente lo converte nell'oggetto `coverFocal` richiesto da `MeepleCardGame`.
   */
  readonly coverFocalX?: number;
  readonly coverFocalY?: number;
};

export interface SharedGamesGridProps {
  readonly state: SharedGamesGridState;
  readonly games: readonly SharedGamesGridGame[];
  /** Required when state === 'default' or 'loading' (cards/skeletons need labels). */
  readonly cardLabels: MeepleCardGameLabels;
  /** Slot for the EmptyState/ErrorState surfaces — caller composes those nodes. */
  readonly emptyNode?: ReactNode;
  readonly errorNode?: ReactNode;
  readonly compact?: boolean;
  readonly className?: string;
}

const SKELETON_COUNT = 9;

export function SharedGamesGrid({
  state,
  games,
  cardLabels,
  emptyNode,
  errorNode,
  compact = false,
  className,
}: SharedGamesGridProps): JSX.Element {
  // #3470 Slice 1d-c — role gate computed once (not per card). Non-admins get no slot,
  // so the tile DOM is unchanged for the public audience; admins get an inline editor.
  const { isEditorOrAbove } = useAdminRole();

  const gridClasses = clsx(
    'grid grid-cols-2 gap-3 md:grid-cols-3 md:gap-4 lg:grid-cols-4',
    className
  );

  if (state === 'loading') {
    return (
      <div data-slot="shared-games-grid" data-state="loading" className={gridClasses}>
        {Array.from({ length: SKELETON_COUNT }, (_, i) => (
          <SkeletonCard key={i} compact={compact} />
        ))}
      </div>
    );
  }

  if (state === 'error') {
    return (
      <div data-slot="shared-games-grid" data-state="error" className={gridClasses}>
        {errorNode}
      </div>
    );
  }

  if (state === 'empty-search' || state === 'filtered-empty') {
    return (
      <div data-slot="shared-games-grid" data-state={state} className={gridClasses}>
        {emptyNode}
      </div>
    );
  }

  return (
    <div data-slot="shared-games-grid" data-state="default" className={gridClasses}>
      {games.map(game => (
        <MeepleCardGame
          key={game.id}
          {...game}
          labels={cardLabels}
          coverFocal={
            game.coverFocalX != null && game.coverFocalY != null
              ? { x: game.coverFocalX, y: game.coverFocalY }
              : undefined
          }
          coverEditSlot={
            // Title is intentionally omitted here: useGameTitle is a hook (illegal in
            // this map callback), and the picker dialog identifies the game by the card
            // the admin clicked. The hero surface passes the locale-resolved title.
            isEditorOrAbove ? (
              <AdminCoverEditAffordance
                gameId={game.id}
                needsAttention={shouldUsePlaceholder(game.coverUrl)}
              />
            ) : undefined
          }
        />
      ))}
    </div>
  );
}
