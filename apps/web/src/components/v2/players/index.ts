/**
 * Barrel exports for /players v2 components (Wave 4 D1, Issue #682).
 *
 * 4 pure presentation components — orchestrator (PlayersView) imports from here.
 * Orphan stubs are not re-exported until they are in-scope for a wave.
 */

export { PlayersHero } from './PlayersHero';
export type { PlayersHeroProps, PlayersHeroLabels } from './PlayersHero';

export { PlayersFiltersInline } from './PlayersFiltersInline';
export type { PlayersFiltersInlineProps, PlayersFiltersInlineLabels } from './PlayersFiltersInline';

export { PlayersResultsGrid } from './PlayersResultsGrid';
export type { PlayersResultsGridProps, PlayersResultsGridLabels } from './PlayersResultsGrid';

export { EmptyPlayers } from './EmptyPlayers';
export type {
  EmptyPlayersProps,
  EmptyPlayersLabels,
  EmptyPlayersCopy,
  EmptyPlayersKind,
} from './EmptyPlayers';
