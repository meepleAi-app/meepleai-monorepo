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

export { GamesHero } from '@/components/features/games/GamesHero';
export type {
  GamesHeroLabels,
  GamesHeroProps,
  GamesHeroStat,
} from '@/components/features/games/GamesHero';

export { GamesFiltersInline } from '@/components/features/games/GamesFiltersInline';
export type {
  GamesFiltersInlineLabels,
  GamesFiltersInlineProps,
  GamesSortKey,
  GamesStatusKey,
  GamesViewKey,
} from '@/components/features/games/GamesFiltersInline';

export { GamesResultsGrid } from '@/components/features/games/GamesResultsGrid';
export type {
  GamesResultsGridProps,
  GamesResultsView,
} from '@/components/features/games/GamesResultsGrid';

export { GamesEmptyState } from '@/components/features/games/GamesEmptyState';
export type {
  GamesEmptyKind,
  GamesEmptyStateCopy,
  GamesEmptyStateLabels,
  GamesEmptyStateProps,
} from '@/components/features/games/GamesEmptyState';

export { GamesRecentRail } from '@/components/features/games/GamesRecentRail';
export type {
  GamesRecentRailItem,
  GamesRecentRailLabels,
  GamesRecentRailProps,
  RecentKbBadge,
} from '@/components/features/games/GamesRecentRail';
