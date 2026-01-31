/**
 * MobileSidebar - Mobile-optimized drawer sidebar for chat (Issue #2232)
 *
 * DEPRECATED: This component is no longer used.
 * Mobile sidebar functionality is now handled by ChatLayout using Sheet.
 *
 * @deprecated Use ChatLayout with sidebarContent prop instead
 */

interface MobileSidebarProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * @deprecated Use ChatLayout component instead. This component is kept for backward compatibility only.
 */
export function MobileSidebar({ open: _open, onOpenChange: _onOpenChange }: MobileSidebarProps) {
  console.warn('MobileSidebar is deprecated. Use ChatLayout with sidebarContent prop instead.');

  return null;
}
