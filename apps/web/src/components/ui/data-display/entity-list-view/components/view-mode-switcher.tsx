/**
 * ViewModeSwitcher - Segmented control for view mode selection
 *
 * A macOS/iOS-inspired segmented control for switching between
 * Grid, List, and Carousel view modes. Features keyboard navigation,
 * responsive layout, and full accessibility support.
 *
 * @module components/ui/data-display/entity-list-view/components/view-mode-switcher
 *
 * Features:
 * - 3 view modes: Grid, List, Carousel
 * - Icons from lucide-react (Grid3x3, List, GalleryHorizontal)
 * - Keyboard navigation: Arrow keys to navigate, Enter/Space to select
 * - ARIA attributes for screen readers
 * - Responsive: Icons only on mobile, icons + labels on desktop
 * - Active state: Orange underline + background tint
 *
 * @example
 * ```tsx
 * <ViewModeSwitcher
 *   value="grid"
 *   onChange={(mode) => setViewMode(mode)}
 *   availableModes={['grid', 'list', 'carousel']}
 * />
 * ```
 */

'use client';

import React from 'react';

import { Grid3x3, List as ListIcon, GalleryHorizontal, Table2 } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { ViewMode } from '../entity-list-view.types';

// ============================================================================
// Types
// ============================================================================

export interface ViewModeSwitcherProps {
  /** Current active view mode */
  value: ViewMode;
  /** Callback when mode changes */
  onChange: (mode: ViewMode) => void;
  /** Available modes (default: all 3) */
  availableModes?: ViewMode[];
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

// ============================================================================
// Constants
// ============================================================================

/**
 * Configuration for each view mode
 */
const MODE_CONFIG: Record<ViewMode, { icon: typeof Grid3x3; label: string; ariaLabel: string }> =
  {
    grid: {
      icon: Grid3x3,
      label: 'Grid',
      ariaLabel: 'Grid view',
    },
    list: {
      icon: ListIcon,
      label: 'List',
      ariaLabel: 'List view',
    },
    carousel: {
      icon: GalleryHorizontal,
      label: 'Carousel',
      ariaLabel: 'Carousel view',
    },
    table: {
      icon: Table2,
      label: 'Table',
      ariaLabel: 'Table view',
    },
  };

// ============================================================================
// Main Component
// ============================================================================

/**
 * ViewModeSwitcher component for selecting Grid/List/Carousel modes
 */
export const ViewModeSwitcher = React.memo(function ViewModeSwitcher({
  value,
  onChange,
  availableModes = ['grid', 'list', 'carousel', 'table'],
  className,
  'data-testid': testId,
}: ViewModeSwitcherProps) {
  /**
   * Keyboard navigation handler
   * Arrow Left/Up: Previous mode
   * Arrow Right/Down: Next mode
   */
  const handleKeyDown = (e: React.KeyboardEvent) => {
    const currentIdx = availableModes.indexOf(value);

    if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
      e.preventDefault();
      const nextIdx = (currentIdx + 1) % availableModes.length;
      // eslint-disable-next-line security/detect-object-injection
      onChange(availableModes[nextIdx]);
    } else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
      e.preventDefault();
      const prevIdx = (currentIdx - 1 + availableModes.length) % availableModes.length;
      // eslint-disable-next-line security/detect-object-injection
      onChange(availableModes[prevIdx]);
    }
  };

  return (
    <div
      role="radiogroup"
      aria-label="View mode selection"
      className={cn(
        'inline-flex items-center gap-1',
        'p-1 rounded-lg',
        'bg-muted/20 border border-border/50',
        className
      )}
      onKeyDown={handleKeyDown}
      data-testid={testId || 'view-mode-switcher'}
    >
      {availableModes.map((mode) => {
        // eslint-disable-next-line security/detect-object-injection
        const config = MODE_CONFIG[mode];
        const Icon = config.icon;
        const isActive = value === mode;

        return (
          <button
            key={mode}
            type="button"
            onClick={() => onChange(mode)}
            role="radio"
            aria-checked={isActive}
            aria-label={config.ariaLabel}
            tabIndex={isActive ? 0 : -1}
            className={cn(
              'flex items-center gap-2 px-3 py-2 rounded-md',
              'text-sm font-medium',
              'transition-all duration-200',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1',
              // Mobile: min touch target 44x44
              'min-h-[44px] sm:min-h-[36px]',
              // Active state
              isActive
                ? [
                    'bg-primary/10 text-primary',
                    'border-b-2 border-primary',
                    // Subtle shadow for active
                    'shadow-sm',
                  ]
                : [
                    'text-muted-foreground',
                    'hover:bg-muted/40 hover:text-foreground',
                    'border-b-2 border-transparent',
                  ]
            )}
            data-testid={`view-mode-${mode}`}
          >
            <Icon className="w-4 h-4" aria-hidden="true" />
            {/* Show label on desktop only */}
            <span className="hidden sm:inline">{config.label}</span>
          </button>
        );
      })}
    </div>
  );
});
