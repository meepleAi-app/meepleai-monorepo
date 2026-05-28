/**
 * Wave B.3 (Issue #574) — `/library` v2 component family barrel.
 *
 * Centralised re-exports so `LibraryHub` (Commit 3) and downstream
 * page-clients can import from `@/components/features/library` without churning
 * deep paths. Each subcomponent owns its own JSDoc — see source files for
 * spec mapping (docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md).
 */

export { BulkSelectionBar } from '@/components/features/library/BulkSelectionBar';
export type {
  BulkSelectionBarLabels,
  BulkSelectionBarProps,
} from '@/components/features/library/BulkSelectionBar';

export { EmptyLibrary } from '@/components/features/library/EmptyLibrary';
export type {
  EmptyLibraryCopy,
  EmptyLibraryKind,
  EmptyLibraryLabels,
  EmptyLibraryProps,
} from '@/components/features/library/EmptyLibrary';

export { LibraryHeroDesktop } from '@/components/features/library/LibraryHeroDesktop';
export type {
  LibraryHeroDesktopLabels,
  LibraryHeroDesktopProps,
  LibraryHeroStat,
  LibraryHeroStatKey,
} from '@/components/features/library/LibraryHeroDesktop';

export { LibraryHybridGrid } from '@/components/features/library/LibraryHybridGrid';
export type {
  LibraryHybridGridProps,
  LibrarySelectionMode,
  LibraryViewMode,
} from '@/components/features/library/LibraryHybridGrid';

export { LibraryTabs } from '@/components/features/library/LibraryTabs';
export type { LibraryTabConfig, LibraryTabsProps } from '@/components/features/library/LibraryTabs';
export type { LibraryEntityKey } from '@/lib/library/library-filters';

export { RecentActivityRail } from '@/components/features/library/RecentActivityRail';
export type {
  ActivityItem,
  ActivityKind,
  RecentActivityRailProps,
} from '@/components/features/library/RecentActivityRail';

export { CrossEntityFilters } from '@/components/features/library/CrossEntityFilters';
export type {
  CrossEntityFiltersProps,
  GameStateFilter,
} from '@/components/features/library/CrossEntityFilters';

export { AdvancedFiltersDrawer } from '@/components/features/library/AdvancedFiltersDrawer';
export type {
  AdvancedFiltersDrawerProps,
  LibraryFilters,
  GameLibraryFilters,
  AgentLibraryFilters,
  SessionLibraryFilters,
  KbLibraryFilters,
  ChatLibraryFilters,
  FiltersForScope,
} from '@/components/features/library/AdvancedFiltersDrawer';
