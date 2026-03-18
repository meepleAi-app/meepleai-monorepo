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
/**
 * @deprecated Use MeepleGameCatalogCard from '@/components/catalog' instead.
 * Kept temporarily for backward compatibility — will be removed in Task 15.
 */
export {
  CatalogGameCard,
  CatalogGameCardSkeleton,
  type CatalogGameCardProps,
  type CommunityStats,
} from './CatalogGameCard';

// Re-export the replacement component for migration convenience
export {
  MeepleGameCatalogCard,
  MeepleGameCatalogCardSkeleton,
  type MeepleGameCatalogCardProps,
} from '../catalog/MeepleGameCatalogCard';
