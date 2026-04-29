/**
 * Wave B.1 (Issue #633) — `/games?tab=library` v2 component barrel.
 *
 * Re-exports the 4 v2 components introduced in Commit 2 plus their public
 * type contracts. Consumed by Commit 3's `GamesLibraryView` orchestrator
 * and `app/(authenticated)/games/page.tsx` wiring.
 *
 * NOTE: `AdvancedFiltersDrawer` remains a Phase 0 stub (Issue #573 backlog) —
 * not part of Wave B.1 scope (BLOCKER #1 spec §10: filters inline, no drawer).
 */

export { GamesHero } from './GamesHero';
export type { GamesHeroLabels, GamesHeroProps, GamesHeroStat } from './GamesHero';

export { GamesFiltersInline } from './GamesFiltersInline';
export type {
  GamesFiltersInlineLabels,
  GamesFiltersInlineProps,
  GamesSortKey,
  GamesStatusKey,
  GamesViewKey,
} from './GamesFiltersInline';

export { GamesResultsGrid } from './GamesResultsGrid';
export type { GamesResultsGridProps, GamesResultsView } from './GamesResultsGrid';

export { GamesEmptyState } from './GamesEmptyState';
export type {
  GamesEmptyKind,
  GamesEmptyStateCopy,
  GamesEmptyStateLabels,
  GamesEmptyStateProps,
} from './GamesEmptyState';
