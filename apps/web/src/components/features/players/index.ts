/**
 * Barrel exports for /players v2 components (Wave 4 D1, Issue #682).
 *
 * 4 pure presentation components — orchestrator (PlayersView) imports from here.
 * Orphan stubs are not re-exported until they are in-scope for a wave.
 */

export { PlayersHero } from '@/components/features/players/PlayersHero';
export type {
  PlayersHeroProps,
  PlayersHeroLabels,
} from '@/components/features/players/PlayersHero';

export { PlayersFiltersInline } from '@/components/features/players/PlayersFiltersInline';
export type {
  PlayersFiltersInlineProps,
  PlayersFiltersInlineLabels,
} from '@/components/features/players/PlayersFiltersInline';

export { PlayersResultsGrid } from '@/components/features/players/PlayersResultsGrid';
export type {
  PlayersResultsGridProps,
  PlayersResultsGridLabels,
} from '@/components/features/players/PlayersResultsGrid';

export { EmptyPlayers } from '@/components/features/players/EmptyPlayers';
export type {
  EmptyPlayersProps,
  EmptyPlayersLabels,
  EmptyPlayersCopy,
  EmptyPlayersKind,
} from '@/components/features/players/EmptyPlayers';
