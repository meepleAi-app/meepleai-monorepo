/**
 * Focus Trap Hook
 * Epic #4068 - Issue #4180
 *
 * Traps focus within a container for accessible modals/tooltips
 */

'use client';

import { useEffect, type RefObject } from 'react';

/**
 * useFocusTrap traps keyboard focus within a container
 * Useful for interactive tooltips, modals, and dialogs
 *
 * @param containerRef - Ref to container element
 * @param isActive - Whether focus trap is active
 *
 * @example
 * const tooltipRef = useRef<HTMLDivElement>(null);
 * useFocusTrap(tooltipRef, isOpen);
 */
export function useFocusTrap(
  containerRef: RefObject<HTMLElement | null>,
  isActive: boolean
): void {
  useEffect(() => {
    if (!isActive || !containerRef.current) return;

    const container = containerRef.current;

    // Find all focusable elements within container
    const focusableElements = container.querySelectorAll<HTMLElement>(
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    );

    if (focusableElements.length === 0) return;

    const firstElement = focusableElements[0];
    const lastElement = focusableElements[focusableElements.length - 1];

    // Handle Tab key to trap focus
    const handleTabKey = (e: KeyboardEvent) => {
      if (e.key !== 'Tab') return;

      // Shift+Tab on first element → move to last
      if (e.shiftKey && document.activeElement === firstElement) {
        e.preventDefault();
        lastElement.focus();
      }
      // Tab on last element → move to first
      else if (!e.shiftKey && document.activeElement === lastElement) {
        e.preventDefault();
        firstElement.focus();
      }
    };

    container.addEventListener('keydown', handleTabKey);

    // Optional: auto-focus first element when trap activates
    // firstElement.focus();

    return () => {
      container.removeEventListener('keydown', handleTabKey);
    };
  }, [isActive, containerRef]);
}
