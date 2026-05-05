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
/**
 * @deprecated Wave A.4 (Issue #603) — prefer linking to the
 * `/shared-games/[id]` route instead of mounting this modal.
 * See `./SharedGameDetailModal` for migration details.
 */
export { SharedGameDetailModal, type SharedGameDetailModalProps } from './SharedGameDetailModal';
export { KnowledgeBaseTab, type KnowledgeBaseTabProps } from './KnowledgeBaseTab';
export { ContributorsSection } from './ContributorsSection';
export { MeepleContributorCard } from './MeepleContributorCard';

// Re-export the replacement component for migration convenience
export {
  MeepleGameCatalogCard,
  MeepleGameCatalogCardSkeleton,
  type MeepleGameCatalogCardProps,
} from '../catalog/MeepleGameCatalogCard';
