/**
 * AccessibleModal Component (UI-05)
 *
 * A fully accessible modal dialog component following WCAG 2.1 AA standards.
 *
 * Features:
 * - Proper ARIA attributes (role="dialog", aria-modal, aria-labelledby)
 * - Focus trap (prevents Tab outside modal)
 * - Focus restoration (returns focus to trigger element on close)
 * - ESC key to close
 * - Backdrop click to close (optional)
 * - Scroll lock on body
 * - Screen reader announcements
 * - Smooth animations with framer-motion
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

import { useEffect, useRef, ReactNode } from 'react';
import { motion, AnimatePresence } from 'framer-motion';

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
}

/**
 * AccessibleModal component with full WCAG 2.1 AA compliance
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
}: AccessibleModalProps) {
  // Refs for focus management
  const modalRef = useRef<HTMLDivElement>(null);
  const previouslyFocusedElement = useRef<HTMLElement | null>(null);
  const titleId = useRef(`modal-title-${Math.random().toString(36).substr(2, 9)}`);
  const descriptionId = useRef(
    description ? `modal-desc-${Math.random().toString(36).substr(2, 9)}` : undefined
  );

  // Size classes
  const sizeClasses = {
    sm: 'max-w-sm',
    md: 'max-w-md',
    lg: 'max-w-lg',
    xl: 'max-w-xl',
    full: 'max-w-full mx-4',
  };

  // Focus trap effect
  useEffect(() => {
    if (!isOpen) return;

    // Store currently focused element to restore later
    previouslyFocusedElement.current = document.activeElement as HTMLElement;

    // Focus the modal container after opening
    const focusModal = () => {
      if (modalRef.current) {
        // Try to focus first focusable element, or modal itself
        const focusableElements = modalRef.current.querySelectorAll<HTMLElement>(
          'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
        );

        if (focusableElements.length > 0) {
          focusableElements[0].focus();
        } else {
          modalRef.current.focus();
        }
      }
    };

    // Small delay to ensure modal is rendered
    const timeoutId = setTimeout(focusModal, 100);

    // Handle Tab key to trap focus
    const handleTab = (e: KeyboardEvent) => {
      if (e.key !== 'Tab' || !modalRef.current) return;

      const focusableElements = modalRef.current.querySelectorAll<HTMLElement>(
        'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
      );

      if (focusableElements.length === 0) return;

      const firstElement = focusableElements[0];
      const lastElement = focusableElements[focusableElements.length - 1];

      // Shift+Tab on first element -> focus last
      if (e.shiftKey && document.activeElement === firstElement) {
        e.preventDefault();
        lastElement.focus();
      }
      // Tab on last element -> focus first
      else if (!e.shiftKey && document.activeElement === lastElement) {
        e.preventDefault();
        firstElement.focus();
      }
    };

    document.addEventListener('keydown', handleTab);

    return () => {
      clearTimeout(timeoutId);
      document.removeEventListener('keydown', handleTab);
    };
  }, [isOpen]);

  // ESC key handler
  useEffect(() => {
    if (!isOpen) return;

    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('keydown', handleEscape);

    return () => {
      document.removeEventListener('keydown', handleEscape);
    };
  }, [isOpen, onClose]);

  // Body scroll lock
  useEffect(() => {
    if (isOpen) {
      // Prevent body scroll
      document.body.style.overflow = 'hidden';
    } else {
      // Restore body scroll
      document.body.style.overflow = '';
    }

    return () => {
      document.body.style.overflow = '';
    };
  }, [isOpen]);

  // Focus restoration on close
  useEffect(() => {
    if (!isOpen && previouslyFocusedElement.current) {
      previouslyFocusedElement.current.focus();
      previouslyFocusedElement.current = null;
    }
  }, [isOpen]);

  // Handle backdrop click
  const handleBackdropClick = (e: React.MouseEvent) => {
    if (closeOnBackdropClick && e.target === e.currentTarget) {
      onClose();
    }
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4"
          aria-live="polite"
          aria-atomic="true"
        >
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="absolute inset-0 bg-black/70 backdrop-blur-sm"
            onClick={handleBackdropClick}
            aria-hidden="true"
          />

          {/* Modal */}
          <motion.div
            ref={modalRef}
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ duration: 0.2 }}
            role="dialog"
            aria-modal="true"
            aria-labelledby={titleId.current}
            aria-describedby={descriptionId.current}
            tabIndex={-1}
            className={`relative bg-white dark:bg-slate-900 rounded-2xl shadow-2xl w-full ${sizeClasses[size]} ${className}`}
          >
            {/* Modal Header */}
            <div className="p-6 border-b border-slate-200 dark:border-slate-700">
              <div className="flex items-start justify-between gap-4">
                <div className="flex-1">
                  <h2
                    id={titleId.current}
                    className="text-xl font-semibold text-slate-900 dark:text-white"
                  >
                    {title}
                  </h2>
                  {description && (
                    <p
                      id={descriptionId.current}
                      className="mt-1 text-sm text-slate-600 dark:text-slate-400"
                    >
                      {description}
                    </p>
                  )}
                </div>

                {/* Close Button */}
                {showCloseButton && (
                  <button
                    onClick={onClose}
                    aria-label="Close dialog"
                    className="
                      flex-shrink-0
                      p-1
                      text-slate-400
                      hover:text-slate-600
                      dark:hover:text-slate-200
                      transition-colors
                      rounded
                      focus-visible:outline-none
                      focus-visible:ring-2
                      focus-visible:ring-primary-500
                    "
                  >
                    <svg
                      className="w-5 h-5"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                      aria-hidden="true"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M6 18L18 6M6 6l12 12"
                      />
                    </svg>
                  </button>
                )}
              </div>
            </div>

            {/* Modal Content */}
            <div className="p-6">{children}</div>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}
