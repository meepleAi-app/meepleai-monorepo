/**
 * AccessibleModal Component (UI-05) - Migrated to shadcn/ui
 *
 * A fully accessible modal dialog component following WCAG 2.1 AA standards.
 * Now uses shadcn Dialog internally while preserving all accessibility features.
 *
 * Features:
 * - Proper ARIA attributes (role="dialog", aria-modal, aria-labelledby)
 * - Focus trap (prevents Tab outside modal) - handled by Dialog
 * - Focus restoration (returns focus to trigger element on close) - handled by Dialog
 * - ESC key to close - handled by Dialog
 * - Backdrop click to close (optional)
 * - Scroll lock on body - handled by Dialog
 * - Screen reader announcements
 *
 * @example
 * ```tsx
 * const [isOpen, setIsOpen] = useState(false);
 *
 * <AccessibleModal
 *   isOpen={isOpen}
 *   onClose={() => setIsOpen(false)}
 *   title="Confirm Delete"
 *   description="Are you sure you want to delete this item?"
 * >
 *   <p>This action cannot be undone.</p>
 *   <div className="flex gap-2 mt-4">
 *     <AccessibleButton variant="danger" onClick={handleDelete}>
 *       Delete
 *     </AccessibleButton>
 *     <AccessibleButton variant="secondary" onClick={() => setIsOpen(false)}>
 *       Cancel
 *     </AccessibleButton>
 *   </div>
 * </AccessibleModal>
 * ```
 */

import { ReactNode } from 'react';

import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/overlays/dialog';
import { cn } from '@/lib/utils';

export interface AccessibleModalProps {
  /**
   * Whether the modal is open
   */
  isOpen: boolean;

  /**
   * Callback when modal should close (ESC key, backdrop click, close button)
   */
  onClose: () => void;

  /**
   * Modal title (required for accessibility)
   * Used as aria-labelledby
   */
  title: string;

  /**
   * Optional description
   * Used as aria-describedby
   */
  description?: string;

  /**
   * Modal content
   */
  children: ReactNode;

  /**
   * Whether clicking backdrop closes modal
   * @default true
   */
  closeOnBackdropClick?: boolean;

  /**
   * Whether to show close button (✕)
   * @default true
   */
  showCloseButton?: boolean;

  /**
   * Additional CSS classes for modal content
   */
  className?: string;

  /**
   * Size of the modal
   * @default 'md'
   */
  size?: 'sm' | 'md' | 'lg' | 'xl' | 'full';

  /**
   * Test ID for E2E testing (language-independent selector)
   */
  'data-testid'?: string;
}

/**
 * AccessibleModal component with full WCAG 2.1 AA compliance
 * Now powered by shadcn/ui Dialog
 */
export function AccessibleModal({
  isOpen,
  onClose,
  title,
  description,
  children,
  closeOnBackdropClick = true,
  showCloseButton = true,
  className = '',
  size = 'md',
  'data-testid': dataTestId,
}: AccessibleModalProps) {
  // Get size class based on size prop
  const getSizeClass = (s: typeof size): string => {
    switch (s) {
      case 'sm':
        return 'sm:max-w-[350px]';
      case 'md':
        return 'sm:max-w-[500px]';
      case 'lg':
        return 'sm:max-w-[700px]';
      case 'xl':
        return 'sm:max-w-[900px]';
      case 'full':
        return 'sm:max-w-[95vw]';
      default:
        return 'sm:max-w-[500px]';
    }
  };

  // Handle dialog close
  const handleOpenChange = (open: boolean) => {
    if (!open) {
      onClose();
    }
  };

  // Handle backdrop click (Dialog handles this via onInteractOutside)
  const handleInteractOutside = (e: Event) => {
    if (!closeOnBackdropClick) {
      e.preventDefault();
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={handleOpenChange}>
      <DialogContent
        className={cn(getSizeClass(size), className)}
        aria-modal="true"
        onInteractOutside={handleInteractOutside}
        hideCloseButton={!showCloseButton}
        data-testid={dataTestId}
      >
        {/* Title - Radix manages the ID internally for aria-labelledby linking */}
        <DialogTitle>{title}</DialogTitle>

        {/* Description - always present for Radix accessibility requirements */}
        <DialogDescription className={!description ? 'sr-only' : ''}>
          {description || 'Dialog content'}
        </DialogDescription>

        {/* Modal Content */}
        <div className="mt-4">{children}</div>
      </DialogContent>
    </Dialog>
  );
}
