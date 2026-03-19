/**
 * Library Components Index
 *
 * Components for user game library management.
 */

export { AddToLibraryButton, type AddToLibraryButtonProps } from './AddToLibraryButton';
export { FavoriteToggle, type FavoriteToggleProps } from './FavoriteToggle';
export { QuotaStatusBar, type QuotaStatusBarProps } from './QuotaStatusBar';
export { QuotaStickyHeader, type QuotaStickyHeaderProps } from './QuotaStickyHeader';
export { LibraryFilters, type LibraryFiltersProps } from './LibraryFilters';
export { EditNotesModal, type EditNotesModalProps } from './EditNotesModal';
export { RemoveGameDialog, type RemoveGameDialogProps } from './RemoveGameDialog';
export { MeepleLibraryGameCard, MeepleLibraryGameCardSkeleton } from './MeepleLibraryGameCard';
export { AgentConfigModal } from './AgentConfigModal';

// Dynamic import to prevent DOMMatrix SSR error with react-pdf (Issue #4133)
import dynamic from 'next/dynamic';
export const PdfUploadModal = dynamic(
  () => import('./PdfUploadModal').then(mod => ({ default: mod.PdfUploadModal })),
  { ssr: false }
);

export { GameActionsModal, type GameActionsModalProps } from './GameActionsModal';
export { RecentLibraryCard, type RecentLibraryCardProps } from './RecentLibraryCard';

// Bulk Operations (Issue #2613)
export { BulkActionBar, type BulkActionBarProps } from './BulkActionBar';
export { BulkRemoveDialog, type BulkRemoveDialogProps } from './BulkRemoveDialog';

// Library Sharing (Issue #2614)
export { ShareLibraryModal } from './ShareLibraryModal';
export { SharedLibraryGameCard } from './SharedLibraryGameCard';

// View Mode Toggle (Issue #2866)
export { ViewModeToggle, type ViewModeToggleProps, type ViewMode } from './ViewModeToggle';

// Library Navigation Tabs (Issue #4055)
export { LibraryNavTabs } from './LibraryNavTabs';

// BGG Search Integration (Issue #4053)
export { BggGameSearch, type BggGameSearchProps } from './BggGameSearch';
export { AddPrivateGameWithBgg, type AddPrivateGameWithBggProps } from './AddPrivateGameWithBgg';

// Game Table Components (replaces legacy game-detail — Issue #3513)
export {
  GameTableLayout,
  GameTableDrawer,
  GameTableZoneTools,
  GameTableZoneKnowledge,
  GameTableZoneSessions,
  GameStatsPanel,
  GameTableSkeleton,
} from './game-table';

// Shelf Components — vetrina layout for library browse pages
export { ShelfCard, type ShelfCardProps, type ManaPip, type ManaPipType } from './ShelfCard';
export { ShelfRow, type ShelfRowProps } from './ShelfRow';

// Mechanic Filter Chip Row
export { MechanicFilter, type MechanicFilterProps } from './MechanicFilter';

// Personal Library Page — vetrina layout
export { PersonalLibraryPage, type PersonalLibraryPageProps } from './PersonalLibraryPage';

// Library Toolbar — search + count controls
export { LibraryToolbar, type LibraryToolbarProps } from './LibraryToolbar';

// Public Library Page — shared catalog browse experience
export { PublicLibraryPage, type PublicLibraryPageProps } from './PublicLibraryPage';
