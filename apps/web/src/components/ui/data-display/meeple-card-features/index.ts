/**
 * MeepleCard Feature Components
 * Issue #3820 - MeepleCard System Refactoring
 *
 * Optional feature components for extending MeepleCard functionality.
 * All features are opt-in (enabled via props).
 */

export { WishlistButton, type WishlistButtonProps } from './WishlistButton';
export {
  QuickActionsMenu,
  type QuickActionsMenuProps,
  type QuickAction,
} from './QuickActionsMenu';
export { StatusBadge, type StatusBadgeProps, type GameStatus } from './StatusBadge';
export { HoverPreview, type HoverPreviewProps, type HoverPreviewData } from './HoverPreview';
export { DragHandle, type DragHandleProps, type DragData } from './DragHandle';
export { BulkSelectCheckbox, type BulkSelectCheckboxProps } from './BulkSelectCheckbox';
export { FlipCard, type FlipCardProps, type MeepleCardFlipData } from './FlipCard';

// Issue #4400: ChatSession-specific display components
export { ChatStatusBadge, type ChatStatusBadgeProps, type ChatStatus } from './ChatStatusBadge';
export { ChatAgentInfo, type ChatAgentInfoProps, type ChatAgent } from './ChatAgentInfo';
export { ChatStatsDisplay, type ChatStatsDisplayProps, type ChatStats, formatDuration } from './ChatStatsDisplay';
export { ChatGameContext, type ChatGameContextProps, type ChatGame } from './ChatGameContext';
export { ChatUnreadBadge, type ChatUnreadBadgeProps } from './ChatUnreadBadge';

// Issue #4758: Snapshot History Slider + Time Travel
export { SnapshotHistorySlider, type SnapshotHistorySliderProps } from './SnapshotHistorySlider';
export { TimeTravelOverlay, type TimeTravelOverlayProps } from './TimeTravelOverlay';

// Issue #5001: Document/KB-specific display components
export {
  DocumentStatusBadge,
  type DocumentIndexingStatus,
} from './DocumentStatusBadge';
