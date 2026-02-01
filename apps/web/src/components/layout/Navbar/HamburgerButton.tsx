/**
 * HamburgerButton - Animated mobile menu toggle
 * Issue #3288 - Phase 2: Navbar Components
 *
 * Features:
 * - Animated hamburger ↔ X transition
 * - WCAG 2.1 AA accessible (44px touch target)
 * - Smooth 200ms morph animation
 * - Focus visible ring for keyboard navigation
 */

'use client';

import { forwardRef } from 'react';

import { cn } from '@/lib/utils';

export interface HamburgerButtonProps {
  /** Whether the menu is open */
  isOpen: boolean;
  /** Toggle handler */
  onToggle: () => void;
  /** Additional className */
  className?: string;
  /** Accessible label */
  'aria-label'?: string;
}

/**
 * HamburgerButton Component
 *
 * Three-line hamburger icon that morphs to an X when activated.
 * Designed for mobile navigation toggle.
 */
export const HamburgerButton = forwardRef<HTMLButtonElement, HamburgerButtonProps>(
  ({ isOpen, onToggle, className, 'aria-label': ariaLabel, ...props }, ref) => {
    return (
      <button
        ref={ref}
        type="button"
        onClick={onToggle}
        aria-label={ariaLabel || (isOpen ? 'Chiudi menu' : 'Apri menu')}
        aria-expanded={isOpen}
        aria-controls="mobile-menu"
        className={cn(
          // Base styles
          'relative flex items-center justify-center',
          // Touch target size (44px minimum)
          'h-11 w-11',
          // Visual appearance
          'rounded-lg',
          'bg-transparent hover:bg-muted/80',
          // Transitions
          'transition-colors duration-200',
          // Focus state
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
          className
        )}
        {...props}
      >
        {/* Hamburger icon container */}
        <div className="relative h-5 w-6">
          {/* Top line */}
          <span
            className={cn(
              'absolute left-0 h-0.5 w-6 rounded-full bg-foreground',
              'transition-all duration-200 ease-in-out',
              isOpen
                ? 'top-[9px] rotate-45'
                : 'top-0.5'
            )}
            aria-hidden="true"
          />
          {/* Middle line */}
          <span
            className={cn(
              'absolute left-0 top-[9px] h-0.5 rounded-full bg-foreground',
              'transition-all duration-200 ease-in-out',
              isOpen
                ? 'w-0 opacity-0'
                : 'w-6 opacity-100'
            )}
            aria-hidden="true"
          />
          {/* Bottom line */}
          <span
            className={cn(
              'absolute left-0 h-0.5 w-6 rounded-full bg-foreground',
              'transition-all duration-200 ease-in-out',
              isOpen
                ? 'top-[9px] -rotate-45'
                : 'top-[17px]'
            )}
            aria-hidden="true"
          />
        </div>
      </button>
    );
  }
);

HamburgerButton.displayName = 'HamburgerButton';
