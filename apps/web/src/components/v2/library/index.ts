/**
 * Wave B.3 (Issue #574) — `/library` v2 component family barrel.
 *
 * Centralised re-exports so `LibraryHubV2` (Commit 3) and downstream
 * page-clients can import from `@/components/v2/library` without churning
 * deep paths. Each subcomponent owns its own JSDoc — see source files for
 * spec mapping (docs/superpowers/specs/2026-04-30-v2-migration-wave-b-3-library.md).
 */

export { BulkSelectionBar } from './BulkSelectionBar';
export type { BulkSelectionBarLabels, BulkSelectionBarProps } from './BulkSelectionBar';

export { EmptyLibrary } from './EmptyLibrary';
export type {
  EmptyLibraryCopy,
  EmptyLibraryKind,
  EmptyLibraryLabels,
  EmptyLibraryProps,
} from './EmptyLibrary';

export { LibraryHeroDesktop } from './LibraryHeroDesktop';
export type {
  LibraryHeroDesktopLabels,
  LibraryHeroDesktopProps,
  LibraryHeroStat,
  LibraryHeroStatKey,
} from './LibraryHeroDesktop';

export { LibraryHybridGrid } from './LibraryHybridGrid';
export type {
  LibraryHybridGridProps,
  LibrarySelectionMode,
  LibraryViewMode,
} from './LibraryHybridGrid';

export { LibraryTabs } from './LibraryTabs';
export type { LibraryEntityKey, LibraryTabConfig, LibraryTabsProps } from './LibraryTabs';

export { RecentActivityRail } from './RecentActivityRail';
export type { ActivityItem, ActivityKind, RecentActivityRailProps } from './RecentActivityRail';
