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
 *   The grid never decides navigation itself. It calls `onCardClick(item.id)`
 *   and the orchestrator (`LibraryHub`) pushes the detail route (`item.href`).
 *   Keeping dispatch in the orchestrator keeps this component pure and
 *   testable without router or state-store mocks.
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
import type {
  MeepleCardMetadata,
  MeepleCardVariant,
} from '@/components/ui/data-display/meeple-card';
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';

export type LibraryViewMode = 'grid' | 'list' | 'compact';

export interface LibraryHybridGridProps {
  readonly items: ReadonlyArray<HybridHubItem>;
  readonly view: LibraryViewMode;
  readonly onCardClick: (itemId: string) => void;
  readonly className?: string;
}

const VIEW_TO_VARIANT: Record<LibraryViewMode, MeepleCardVariant> = {
  grid: 'grid',
  list: 'list',
  compact: 'compact',
};

// Mockup conformance — admin-mockups/design_files/sp4-library-desktop.jsx:846-890
// (LibraryGrid). Wrapper layout per view mode:
//   grid    → display:grid, repeat(N, minmax(0,1fr)), gap:12px
//             → Tailwind responsive grid-cols (2/3/4) + gap-3.
//   list    → display:flex flex-col, gap:6px
//             → Tailwind flex flex-col gap-1.5.
//   compact → bordered card container that wraps stacked rows (NOT a grid; the
//             stacked rows + per-row separators are the compact MeepleCard
//             primitive's responsibility — Task 2.2 / issue #1856).
//             → Tailwind bg-card border border-border rounded-lg overflow-hidden.
const VIEW_TO_LAYOUT: Record<LibraryViewMode, string> = {
  grid: 'grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4',
  list: 'flex flex-col gap-1.5',
  compact: 'bg-card border border-border rounded-lg overflow-hidden',
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

/**
 * Game-only visual extra: metadata chip that surfaces a "KB linked" state
 * at a glance. The flag is mapped from `UserLibraryEntry` in
 * `libraryEntryToHubItem` (see `lib/library/hybrid-hub.mappers.ts`) via
 * `isKbEntry`, which returns true when EITHER the backend reports the
 * shared game's `has_knowledge_base` flag (≥ 1 PDF fully indexed end-to-end
 * by #2244 / epic #2242 BE pipeline) OR at least one PDF document is
 * linked but still in the pipeline (`kbCardCount > 0`). The single
 * `📄 KB` label intentionally collapses the two states for now; a
 * separate `⏳ KB in elaborazione` variant for in-flight uploads is
 * tracked as Block E follow-up of #2247.
 *
 * Non-game variants render nothing.
 */
function itemMetadata(item: HybridHubItem): MeepleCardMetadata[] | undefined {
  if (item.entity !== 'game' || !item.hasKb) return undefined;
  return [{ label: '📄 KB' }];
}

export function LibraryHybridGrid({
  items,
  view,
  onCardClick,
  className,
}: LibraryHybridGridProps): ReactElement {
  const variant = VIEW_TO_VARIANT[view];
  const layoutClass = VIEW_TO_LAYOUT[view];

  return (
    <div
      data-slot="library-hybrid-grid-container"
      data-view={view}
      className={clsx(layoutClass, className)}
    >
      {items.map(item => (
        <button
          key={item.id}
          type="button"
          data-slot="library-grid-card"
          data-entry-id={item.id}
          onClick={() => onCardClick(item.id)}
          className={clsx(
            'relative block w-full text-left',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-2xl'
          )}
        >
          <MeepleCard
            entity={item.entity}
            variant={variant}
            id={item.id}
            title={item.title}
            subtitle={item.subtitle}
            imageUrl={itemImageUrl(item)}
            rating={itemRating(item)}
            ratingMax={10}
            metadata={itemMetadata(item)}
            headingLevel={2}
          />
        </button>
      ))}
    </div>
  );
}
