/**
 * LibraryHybridGrid — Wave B.3 v2 component (Issue #574).
 *
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (LibraryGrid). Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md
 * §3.2.
 *
 * Phase 2a (#1605): accepts a heterogeneous `items: HybridHubItem[]`
 * (game/agent/kb/session/chat discriminated union) instead of the games-only
 * `UserLibraryEntry[]`. `entity`, `title`, `subtitle`, `href` come from the
 * common base; `rating`/`imageUrl` are game-only visual extras gated by the
 * discriminant. The `data-entry-id` attribute now carries the hybrid item id.
 *
 * Single-handler click contract:
 *   The grid never decides toggle vs drill-into. It just calls
 *   `onCardClick(item.id)` and the orchestrator (`LibraryHub`) dispatches
 *   based on `selectionMode`:
 *     - browse → push detail route (`item.href`)
 *     - select → toggle membership in `selected` Set
 *   Keeping dispatch in the orchestrator keeps this component pure and
 *   testable without router or state-store mocks.
 *
 * Selection mode FSM mapping:
 *   - browse → no aria-pressed, no overlay slot (clean visual baseline).
 *   - select → aria-pressed on EVERY card (ARIA toggle button pattern: the
 *              role advertises a binary state, so every member must expose
 *              the attribute even when false). Check overlay only when
 *              `selected.has(item.id)`.
 *
 * MeepleCard reuse mandate: this is the canonical entity card for the entire
 * app (game/player/agent/kb/...). Wrapping it in a `<button>` with overlay
 * lets us layer selection affordance without forking the card API.
 *
 * Layout: Tailwind grid template tracks per view mode. Compact uses 6-col on
 * `lg`, list collapses to a single column with vertical gap.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';

export type LibraryViewMode = 'grid' | 'list' | 'compact';
export type LibrarySelectionMode = 'browse' | 'select';

export interface LibraryHybridGridProps {
  readonly items: ReadonlyArray<HybridHubItem>;
  readonly view: LibraryViewMode;
  readonly selectionMode: LibrarySelectionMode;
  readonly selected: ReadonlySet<string>;
  readonly onCardClick: (itemId: string) => void;
  readonly onLongPressEnter?: (itemId: string) => void;
  readonly className?: string;
}

const VIEW_TO_VARIANT: Record<LibraryViewMode, MeepleCardVariant> = {
  grid: 'grid',
  list: 'list',
  compact: 'compact',
};

const VIEW_TO_LAYOUT: Record<LibraryViewMode, string> = {
  grid: 'grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4',
  list: 'flex flex-col gap-2',
  compact: 'grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-6',
};

// Game-only visual extras: rating + cover image are part of the `game` variant
// only. Non-game items (session/chat/agent/kb) render the same MeepleCard
// shell without these to keep the grid heterogeneous-safe.
function itemImageUrl(item: HybridHubItem): string | undefined {
  return item.entity === 'game' ? item.imageUrl : undefined;
}
function itemRating(item: HybridHubItem): number | undefined {
  return item.entity === 'game' ? item.rating : undefined;
}

export function LibraryHybridGrid({
  items,
  view,
  selectionMode,
  selected,
  onCardClick,
  className,
}: LibraryHybridGridProps): ReactElement {
  const variant = VIEW_TO_VARIANT[view];
  const layoutClass = VIEW_TO_LAYOUT[view];
  const isSelectMode = selectionMode === 'select';

  return (
    <div
      data-slot="library-hybrid-grid"
      data-view={view}
      data-selection-mode={selectionMode}
      className={clsx(layoutClass, className)}
    >
      {items.map(item => {
        const isSelected = selected.has(item.id);
        return (
          <button
            key={item.id}
            type="button"
            data-slot="library-grid-card"
            data-selection-mode={selectionMode}
            data-entry-id={item.id}
            aria-pressed={isSelectMode ? isSelected : undefined}
            onClick={() => onCardClick(item.id)}
            className={clsx(
              'relative block w-full text-left',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-2xl',
              isSelectMode && isSelected && 'ring-2 ring-primary'
            )}
          >
            <MeepleCard
              entity={item.entity}
              variant={variant}
              title={item.title}
              subtitle={item.subtitle}
              imageUrl={itemImageUrl(item)}
              rating={itemRating(item)}
              ratingMax={10}
            />
            {isSelectMode && isSelected ? (
              <span
                data-testid="library-grid-card-check"
                aria-hidden="true"
                className="pointer-events-none absolute right-3 top-3 inline-flex h-6 w-6 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-sm"
              >
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  className="h-4 w-4"
                >
                  <path
                    fillRule="evenodd"
                    d="M16.704 5.29a1 1 0 010 1.42l-7.5 7.5a1 1 0 01-1.42 0l-3.5-3.5a1 1 0 011.42-1.42L8.5 12.08l6.79-6.79a1 1 0 011.414 0z"
                    clipRule="evenodd"
                  />
                </svg>
              </span>
            ) : null}
          </button>
        );
      })}
    </div>
  );
}
