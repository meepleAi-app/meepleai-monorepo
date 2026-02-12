/**
 * SharedGameCatalog Components (Issue #2373: Phase 4)
 *
 * User-facing components for SharedGameCatalog integration.
 */

export {
  SharedGameSearch,
  type SharedGameSearchResult,
  type SharedGameSearchProps,
} from './SharedGameSearch';
export {
  SharedGameSearchFilters,
  DEFAULT_FILTERS,
  type SearchFilters,
  type SharedGameSearchFiltersProps,
} from './SharedGameSearchFilters';
export { SharedGameDetailModal, type SharedGameDetailModalProps } from './SharedGameDetailModal';
export { KnowledgeBaseTab, type KnowledgeBaseTabProps } from './KnowledgeBaseTab';
export { ContributorsSection } from './ContributorsSection';
export { ContributorCard } from './ContributorCard';
export {
  CatalogGameCard,
  CatalogGameCardSkeleton,
  type CatalogGameCardProps,
  type CommunityStats,
} from './CatalogGameCard';
