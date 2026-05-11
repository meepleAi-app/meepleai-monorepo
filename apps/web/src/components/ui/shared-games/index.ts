/**
 * Barrel for /shared-games v2 components — Wave A.3b (Issue #596).
 *
 * Components: 8 files + 1 barrel. Tests live alongside (`*.test.tsx`).
 */

export { ContributorsSidebar } from '@/components/ui/shared-games/contributors-sidebar';
export type {
  ContributorsSidebarItem,
  ContributorsSidebarLabels,
  ContributorsSidebarProps,
} from '@/components/ui/shared-games/contributors-sidebar';

export { EmptyState } from '@/components/ui/shared-games/empty-state';
export type { EmptyStateKind, EmptyStateProps } from '@/components/ui/shared-games/empty-state';

export { ErrorState } from '@/components/ui/shared-games/error-state';
export type { ErrorStateProps } from '@/components/ui/shared-games/error-state';

export { MeepleCardGame } from '@/components/ui/shared-games/meeple-card-game';
export type { MeepleCardGameLabels, MeepleCardGameProps } from '@/components/ui/shared-games/meeple-card-game';

export { SharedGamesFilters } from '@/components/ui/shared-games/shared-games-filters';
export type {
  SharedGamesFiltersChipDef,
  SharedGamesFiltersLabels,
  SharedGamesFiltersOption,
  SharedGamesFiltersProps,
} from '@/components/ui/shared-games/shared-games-filters';

export { SharedGamesGrid } from '@/components/ui/shared-games/shared-games-grid';
export type {
  SharedGamesGridGame,
  SharedGamesGridProps,
  SharedGamesGridState,
} from '@/components/ui/shared-games/shared-games-grid';

export { SharedGamesHero } from '@/components/ui/shared-games/shared-games-hero';
export type { SharedGamesHeroProps } from '@/components/ui/shared-games/shared-games-hero';

export { SkeletonCard } from '@/components/ui/shared-games/skeleton-card';
export type { SkeletonCardProps } from '@/components/ui/shared-games/skeleton-card';
