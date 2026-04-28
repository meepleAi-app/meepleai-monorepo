/**
 * Barrel for /shared-games v2 components — Wave A.3b (Issue #596).
 *
 * Components: 8 files + 1 barrel. Tests live alongside (`*.test.tsx`).
 */

export { ContributorsSidebar } from './contributors-sidebar';
export type {
  ContributorsSidebarItem,
  ContributorsSidebarLabels,
  ContributorsSidebarProps,
} from './contributors-sidebar';

export { EmptyState } from './empty-state';
export type { EmptyStateKind, EmptyStateProps } from './empty-state';

export { ErrorState } from './error-state';
export type { ErrorStateProps } from './error-state';

export { MeepleCardGame } from './meeple-card-game';
export type { MeepleCardGameLabels, MeepleCardGameProps } from './meeple-card-game';

export { SharedGamesFilters } from './shared-games-filters';
export type {
  SharedGamesFiltersChipDef,
  SharedGamesFiltersLabels,
  SharedGamesFiltersOption,
  SharedGamesFiltersProps,
} from './shared-games-filters';

export { SharedGamesGrid } from './shared-games-grid';
export type {
  SharedGamesGridGame,
  SharedGamesGridProps,
  SharedGamesGridState,
} from './shared-games-grid';

export { SharedGamesHero } from './shared-games-hero';
export type { SharedGamesHeroProps } from './shared-games-hero';

export { SkeletonCard } from './skeleton-card';
export type { SkeletonCardProps } from './skeleton-card';
