/**
 * Wave B.1 (Issue #633) — library-view v2 component barrel.
 *
 * Re-exports the 5 feature components implementing the `sp4-games-index` library
 * mockup, plus their public type contracts.
 *
 * NOTE (Issue #1521): these components were originally wired into the
 * `/games?tab=library` orchestrator (`GamesLibraryView`), which was removed when
 * `/games` was consolidated into a redirect to `/library`. They are currently
 * shelf-ready (tested, mockup-faithful) awaiting a follow-up that wires them into
 * `/library` (LibraryHub) in place of its older implementation.
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
