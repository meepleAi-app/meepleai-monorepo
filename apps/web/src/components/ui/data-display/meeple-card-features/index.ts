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
